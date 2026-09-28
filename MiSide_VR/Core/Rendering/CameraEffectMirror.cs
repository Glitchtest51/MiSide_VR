using System;
using System.Collections.Generic;
using System.Reflection;
using EPOOutline;
using HarmonyLib;
using Il2CppInterop.Runtime;
using MiSide_VR.Core.Rendering.Patches;
using MiSide_VR.UI.Patches;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;
using static MiSide_VR.Plugin;

namespace MiSide_VR.Core.Rendering;

sealed class CameraEffectMirror {
	private static readonly Dictionary<Type, PropertyInfo[]> ColorfulPropertyCache = new();
	private static readonly Dictionary<Type, FieldInfo[]> ColorfulFieldCache = new();
	private static readonly HashSet<string> ColorfulWarnings = new(StringComparer.Ordinal);
	private readonly ColorfulMirrorState _headColorfulState = new();
	private readonly ColorfulMirrorState _desktopColorfulState = new();
	private readonly DatamoshMirrorState _headDatamoshState = new();
	private readonly DatamoshMirrorState _desktopDatamoshState = new();

	private sealed class ColorfulMirrorState {
		public Camera Source;
		public Camera Target;
		public readonly List<ColorfulMirrorPair> Pairs = new();
	}

	private sealed class ColorfulMirrorPair {
		public Colorful.BaseEffect Source;
		public Colorful.BaseEffect Target;
	}

	private sealed class DatamoshMirrorState {
		public Camera Target;
		public Kino.Datamosh LeftOrMono;
		public Kino.Datamosh Right;
		public int LastSourceSequence;
	}

	internal void CopyCameraData(Camera source, Camera target) {
		if (!source || !target) return;

		target.backgroundColor = source.backgroundColor;
		target.clearFlags = source.clearFlags;
		target.orthographic = false;
		target.farClipPlane = Mathf.Max(source.farClipPlane, 10f);
		target.renderingPath = source.renderingPath;
		target.allowHDR = source.allowHDR;
		target.allowMSAA = source.allowMSAA;

		var combinedMask = source.cullingMask;
		var personsCamera = source.transform.Find("CameraPersons")?.GetComponent<Camera>();
		if (personsCamera) combinedMask |= personsCamera.cullingMask;
		var uiMask = 1 << VRPlayer.VrUiLayer;
		var pointerMask = 1 << VRPlayer.VrPointerLayer;
		target.cullingMask = GameContext.Mode == GameMode.Novella ? (1 << VRPlayer.VrUiLayer) | pointerMask : (combinedMask & ~(1 << CanvasPatch.SourceUiLayer)) | uiMask | pointerMask;
		CopyOutliner(source, target);

		var sourcePostProcessing = source.GetComponent<PostProcessLayer>();
		var targetPostProcessing = target.GetComponent<PostProcessLayer>();
		if (!sourcePostProcessing) {
			if (targetPostProcessing) targetPostProcessing.enabled = false;
		} else {
			targetPostProcessing ??= target.gameObject.AddComponent<PostProcessLayer>();
			targetPostProcessing.m_Resources = sourcePostProcessing.m_Resources;
			targetPostProcessing.volumeLayer = sourcePostProcessing.volumeLayer;
			// taa corrupts the copied stereo image so use fxaa instead
			targetPostProcessing.antialiasingMode = sourcePostProcessing.antialiasingMode == PostProcessLayer.Antialiasing.TemporalAntialiasing ? PostProcessLayer.Antialiasing.FastApproximateAntialiasing : sourcePostProcessing.antialiasingMode;
			targetPostProcessing.stopNaNPropagation = sourcePostProcessing.stopNaNPropagation;
			targetPostProcessing.volumeTrigger = target.transform;
			// PPV2s final blit flips the VR render so orientation is handled ourselves
			targetPostProcessing.finalBlitToCameraTarget = false;
			targetPostProcessing.enabled = sourcePostProcessing.enabled;
		}

		CopyOutlinePostprocess(source, target);
		CopyFlareLayer(source, target);
	}

	internal void Synchronize(Camera source, VRPlayer player, bool refreshEffects) {
		SynchronizeEffects(source, player.headCamera, _headColorfulState, _headDatamoshState, refreshEffects);
		SynchronizeEffects(source, player.desktopCamera, _desktopColorfulState, _desktopDatamoshState, refreshEffects);
	}

	private static void CopyOutliner(Camera source, Camera target) {
		var sourceOutliner = source.GetComponent<Outliner>();
		var targetOutliner = target.GetComponent<Outliner>();
		if (!sourceOutliner) {
			if (targetOutliner) targetOutliner.enabled = false;
			return;
		}

		targetOutliner ??= target.gameObject.AddComponent<Outliner>();
		// disable while copying settings
		targetOutliner.enabled = false;
		targetOutliner.RenderingStrategy = sourceOutliner.RenderingStrategy;
		targetOutliner.RenderStage = sourceOutliner.RenderStage;
		targetOutliner.DilateQuality = sourceOutliner.DilateQuality;
		targetOutliner.BlurShift = sourceOutliner.BlurShift;
		targetOutliner.DilateShift = sourceOutliner.DilateShift;
		targetOutliner.OutlineLayerMask = sourceOutliner.OutlineLayerMask;
		targetOutliner.ScaleIndependent = sourceOutliner.ScaleIndependent;
		targetOutliner.InfoRendererScale = sourceOutliner.InfoRendererScale;
		targetOutliner.PrimaryRendererScale = sourceOutliner.PrimaryRendererScale;
		targetOutliner.BlurIterations = sourceOutliner.BlurIterations;
		targetOutliner.BlurType = sourceOutliner.BlurType;
		targetOutliner.DilateIterations = sourceOutliner.DilateIterations;
		targetOutliner.enabled = sourceOutliner.enabled;
	}

	private static void CopyOutlinePostprocess(Camera source, Camera target) {
		var sourcePass = source ? source.GetComponent<OutlinesPostprocessed>() : null;
		var targetPass = target.GetComponent<OutlinesPostprocessed>();
		var headsetTarget = VRPlayer.Instance && target == VRPlayer.Instance.headCamera;
		if (!sourcePass || !sourcePass.PostprocessMaterial) {
			if (targetPass) targetPass.enabled = false;
			if (headsetTarget) HeadsetOutlinePostprocess.Release();
			return;
		}
		if (headsetTarget) HeadsetOutlinePostprocess.Release();

		if (!targetPass) {
			targetPass = target.gameObject.AddComponent<OutlinesPostprocessed>();
			targetPass.enabled = false;
		}

		targetPass.PostprocessMaterial = sourcePass.PostprocessMaterial;
		targetPass.enabled = sourcePass.enabled;
	}

	private static void CopyFlareLayer(Camera source, Camera target) {
		var sourceFlares = source ? source.GetComponent<FlareLayer>() : null;
		var targetFlares = target.GetComponent<FlareLayer>();
		if (!sourceFlares) {
			if (targetFlares) targetFlares.enabled = false;
			return;
		}

		targetFlares ??= target.gameObject.AddComponent<FlareLayer>();
		targetFlares.enabled = sourceFlares.enabled;
	}

	private static void SynchronizeEffects(Camera source, Camera target, ColorfulMirrorState colorfulState, DatamoshMirrorState datamoshState, bool refreshEffects) {
		if (!target) return;

		var refreshColorful = refreshEffects || colorfulState.Source != source || colorfulState.Target != target;
		if (refreshColorful) RefreshColorfulEffects(source, target, colorfulState);
		foreach (var pair in colorfulState.Pairs) {
			if (!pair.Source || !pair.Target) continue;
			if (refreshColorful || pair.Source.enabled || pair.Target.enabled) CopyColorfulEffect(pair.Source, pair.Target);
			else pair.Target.enabled = false;
		}
		SynchronizeGlitch(source, target);
		SynchronizeComicBook(source, target);

		CopyOutlinePostprocess(source, target);
		CopyFlareLayer(source, target);
		SynchronizeDatamosh(source, target, datamoshState);

		var sourceOutliner = source ? source.GetComponent<Outliner>() : null;
		var targetOutliner = target.GetComponent<Outliner>();
		if (sourceOutliner && (!targetOutliner || sourceOutliner.enabled != targetOutliner.enabled)) CopyOutliner(source, target);
		else if (!sourceOutliner && targetOutliner) targetOutliner.enabled = false;
	}

	private static void RefreshColorfulEffects(Camera source, Camera target, ColorfulMirrorState state) {
		state.Source = source;
		state.Target = target;
		state.Pairs.Clear();

		var targetEffects = new List<Colorful.BaseEffect>(GetTypedColorfulEffects(target));
		var targetsByType = new Dictionary<Type, Colorful.BaseEffect>();
		foreach (var targetEffect in targetEffects) {
			if (!targetEffect) continue;
			targetsByType[targetEffect.GetType()] = targetEffect;
		}

		if (!source) {
			foreach (var targetEffect in targetEffects)
				if (targetEffect) targetEffect.enabled = false;
			return;
		}

		var mirroredTypes = new HashSet<Type>();
		var addedEffect = false;
		foreach (var sourceEffect in GetSourceColorfulEffects(source)) {
			if (!sourceEffect) continue;

			var effectType = sourceEffect.GetType();
			var effectName = effectType.FullName;
			if (effectType == typeof(Colorful.Glitch) || effectType == typeof(Colorful.ComicBook)) continue;
			if (!mirroredTypes.Add(effectType)) continue;

			if (!targetsByType.TryGetValue(effectType, out var targetEffect)) {
				var component = target.gameObject.AddComponent(Il2CppType.From(effectType));
				targetEffect = GetTypedColorfulEffect(component.TryCast<Colorful.BaseEffect>());
				if (!targetEffect) {
					WarnColorfulCopy(effectName, "could not cast the new component to BaseEffect");
					continue;
				}
				targetEffect.enabled = false;
				targetsByType[effectType] = targetEffect;
				addedEffect = true;
			}

			state.Pairs.Add(new ColorfulMirrorPair { Source = sourceEffect, Target = targetEffect });
		}

		foreach (var targetEffect in targetEffects) if (targetEffect && !mirroredTypes.Contains(targetEffect.GetType())) targetEffect.enabled = false;

		if (addedEffect) {
			var outlinePass = target.GetComponent<OutlinesPostprocessed>();
			if (outlinePass) UnityEngine.Object.DestroyImmediate(outlinePass);
		}
	}

	private static void CopyColorfulEffect(Colorful.BaseEffect source, Colorful.BaseEffect target) {
		target.Shader = source.Shader;
		if (!target.Shader) {
			target.enabled = false;
			WarnColorfulCopy(source.GetType().FullName, "source shader is missing");
			return;
		}

		foreach (var property in GetColorfulProperties(source.GetType())) {
			try { property.SetValue(target, property.GetValue(source)); }
			catch (Exception exception) { WarnColorfulCopy(source.GetType().FullName, $"could not copy {property.Name}: {exception.GetBaseException().Message}"); }
		}
		foreach (var field in GetColorfulFields(source.GetType())) {
			try { field.SetValue(target, field.GetValue(source)); }
			catch (Exception exception) { WarnColorfulCopy(source.GetType().FullName, $"could not copy {field.Name}: {exception.GetBaseException().Message}"); }
		}

		target.enabled = IsEffectActive(source);
	}

	private static void SynchronizeGlitch(Camera sourceCamera, Camera targetCamera) {
		var source = FindSourceColorfulEffect<Colorful.Glitch>(sourceCamera);
		var target = targetCamera.GetComponent<Colorful.Glitch>();
		if (!source) {
			if (target) target.enabled = false;
			return;
		}

		target ??= targetCamera.gameObject.AddComponent<Colorful.Glitch>();
		target.enabled = false;
		target.Shader = source.Shader;
		if (!target.Shader) {
			WarnColorfulCopy(typeof(Colorful.Glitch).FullName, "source shader is missing");
			return;
		}

		target.RandomActivation = source.RandomActivation;
		target.RandomEvery = source.RandomEvery;
		target.RandomDuration = source.RandomDuration;
		target.Mode = source.Mode;
		target.SettingsInterferences = source.SettingsInterferences;
		target.SettingsTearing = source.SettingsTearing;
		target.m_Activated = source.m_Activated;
		target.m_EveryTimer = source.m_EveryTimer;
		target.m_EveryTimerEnd = source.m_EveryTimerEnd;
		target.m_DurationTimer = source.m_DurationTimer;
		target.m_DurationTimerEnd = source.m_DurationTimerEnd;
		target.enabled = IsEffectActive(source);
	}

	private static void SynchronizeComicBook(Camera sourceCamera, Camera targetCamera) {
		var source = FindSourceColorfulEffect<Colorful.ComicBook>(sourceCamera);
		var target = targetCamera.GetComponent<Colorful.ComicBook>();
		if (!source) {
			if (target) target.enabled = false;
			return;
		}

		target ??= targetCamera.gameObject.AddComponent<Colorful.ComicBook>();
		target.enabled = false;
		target.Shader = source.Shader;
		if (!target.Shader) {
			WarnColorfulCopy(typeof(Colorful.ComicBook).FullName, "source shader is missing");
			return;
		}

		target.StripAngle = source.StripAngle;
		target.StripDensity = source.StripDensity;
		target.StripThickness = source.StripThickness;
		target.StripLimits = source.StripLimits;
		target.StripInnerColor = source.StripInnerColor;
		target.StripOuterColor = source.StripOuterColor;
		target.FillColor = source.FillColor;
		target.BackgroundColor = source.BackgroundColor;
		target.EdgeDetection = source.EdgeDetection;
		target.EdgeThreshold = source.EdgeThreshold;
		target.EdgeColor = source.EdgeColor;
		target.Amount = source.Amount;
		target.enabled = IsEffectActive(source);
	}

	private static T FindSourceColorfulEffect<T>(Camera source) where T : Colorful.BaseEffect {
		var direct = source ? source.GetComponent<T>() : null;
		if (direct) return direct;
		var composite = GetCompositeEffectsCamera(source);
		return composite ? composite.GetComponent<T>() : null;
	}

	private static bool IsEffectActive(Colorful.BaseEffect effect) {
		if (Time.timeScale <= 0f && effect.GetType().Name.Contains("Blur", StringComparison.Ordinal)) return false;
		var camera = effect.GetComponent<Camera>();
		return effect.isActiveAndEnabled && camera && camera.isActiveAndEnabled;
	}

	private static void SynchronizeDatamosh(Camera sourceCamera, Camera targetCamera, DatamoshMirrorState state) {
		var source = sourceCamera ? sourceCamera.GetComponent<Kino.Datamosh>() : null;
		if (!source) source = GetCompositeEffectsCamera(sourceCamera)?.GetComponent<Kino.Datamosh>();
		if (!source) {
			if (state.LeftOrMono) state.LeftOrMono.enabled = false;
			if (state.Right) state.Right.enabled = false;
			state.LastSourceSequence = 0;
			return;
		}

		if (state.Target != targetCamera || !state.LeftOrMono) {
			state.Target = targetCamera;
			state.LeftOrMono = targetCamera.gameObject.AddComponent<Kino.Datamosh>();
			state.LeftOrMono.enabled = false;
			if (targetCamera.stereoTargetEye == StereoTargetEyeMask.Both) {
				state.Right = targetCamera.gameObject.AddComponent<Kino.Datamosh>();
				state.Right.enabled = false;
				DatamoshStereoPatch.SetHeadsetPair(state.LeftOrMono, state.Right);
			}
			state.LastSourceSequence = 0;
		}

		var strength = state.Right ? DatamoshStrength : 1f;
		CopyDatamoshSettings(source, state.LeftOrMono, strength);
		if (state.Right) CopyDatamoshSettings(source, state.Right, strength);

		var active = source.isActiveAndEnabled;
		if (state.LeftOrMono.enabled != active) state.LeftOrMono.enabled = active;
		if (state.Right && state.Right.enabled != active) state.Right.enabled = active;
		if (!active) {
			state.LastSourceSequence = 0;
			return;
		}

		var sequence = source._sequence;
		var restarted = sequence == 1 && state.LastSourceSequence != 1;
		SynchronizeDatamoshSequence(state.LeftOrMono, sequence, restarted);
		if (state.Right) SynchronizeDatamoshSequence(state.Right, sequence, restarted);
		state.LastSourceSequence = sequence;
	}

	private static void CopyDatamoshSettings(Kino.Datamosh source, Kino.Datamosh target, float strength) {
		target._shader = source._shader;
		target.blockSize = source.blockSize;
		target.entropy = source.entropy * strength;
		target.noiseContrast = source.noiseContrast;
		target.velocityScale = source.velocityScale * strength;
		target.diffusion = source.diffusion * strength;
	}

	private static void SynchronizeDatamoshSequence(Kino.Datamosh target, int sourceSequence, bool restarted) {
		if (sourceSequence == 0) { if (target._sequence != 0) target.Reset(); }
		else if (target._workBuffer && (target._sequence == 0 || restarted)) { target.Glitch(); }
	}

	private static Camera GetCompositeEffectsCamera(Camera source) {
		if (!source) return null;
		var controller = source.GetComponent<PlayerCameraEffects>();
		var camera = controller ? controller.cameraPerson : source.transform.Find("CameraPersons")?.GetComponent<Camera>();
		// ignore cameras that render to textures
		return camera && camera != source && camera.isActiveAndEnabled && !camera.targetTexture && camera.targetDisplay == source.targetDisplay && camera.rect == source.rect && camera.clearFlags is CameraClearFlags.Nothing or CameraClearFlags.Depth ? camera : null;
	}

	private static IEnumerable<Colorful.BaseEffect> GetSourceColorfulEffects(Camera source) {
		foreach (var effect in GetTypedColorfulEffects(source)) yield return effect;
		foreach (var effect in GetTypedColorfulEffects(GetCompositeEffectsCamera(source))) yield return effect;
	}

	private static IEnumerable<Colorful.BaseEffect> GetTypedColorfulEffects(Camera camera) {
		if (!camera) yield break;
		foreach (var effect in camera.GetComponents<Colorful.BaseEffect>()) {
			var typed = GetTypedColorfulEffect(effect);
			if (typed) yield return typed;
		}
	}

	private static Colorful.BaseEffect GetTypedColorfulEffect(Colorful.BaseEffect effect) {
		if (!effect) return null;
		var nativeName = effect.GetIl2CppType().FullName;
		var type = typeof(Colorful.BaseEffect).Assembly.GetType(nativeName);
		if (type == null || !typeof(Colorful.BaseEffect).IsAssignableFrom(type)) {
			WarnColorfulCopy(nativeName, "no matching interop effect type");
			return null;
		}
		return (Colorful.BaseEffect)Activator.CreateInstance(type, effect.Pointer);
	}

	private static PropertyInfo[] GetColorfulProperties(Type effectType) {
		if (ColorfulPropertyCache.TryGetValue(effectType, out var cached)) return cached;

		var properties = new List<PropertyInfo>();
		for (var type = effectType; type != null && typeof(Colorful.BaseEffect).IsAssignableFrom(type); type = type.BaseType) {
			foreach (var property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)) {
				if (!property.CanRead || !property.CanWrite || property.GetIndexParameters().Length != 0) continue;
				if (property.Name is "Shader" or "m_Material") continue;
				properties.Add(property);
			}
		}

		cached = properties.ToArray();
		ColorfulPropertyCache[effectType] = cached;
		return cached;
	}

	private static FieldInfo[] GetColorfulFields(Type effectType) {
		if (ColorfulFieldCache.TryGetValue(effectType, out var cached)) return cached;

		var fields = new List<FieldInfo>();
		for (var type = effectType; type != null && typeof(Colorful.BaseEffect).IsAssignableFrom(type); type = type.BaseType) {
			foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)) {
				if (field.IsInitOnly || field.IsLiteral || field.Name == "Shader") continue;
				fields.Add(field);
			}
		}

		cached = fields.ToArray();
		ColorfulFieldCache[effectType] = cached;
		return cached;
	}

	private static void WarnColorfulCopy(string effectName, string reason) {
		var warning = $"{effectName}: {reason}";
		if (ColorfulWarnings.Add(warning)) Log.LogWarning($"[VRSystem] Colorful mirror skipped part of {warning}");
	}

	internal static void SynchronizeOutlineTargets() {
		foreach (var outlinable in UnityEngine.Object.FindObjectsOfType<Outlinable>(true)) {
			if (!outlinable || !outlinable.gameObject.activeInHierarchy) continue;
			var animator = outlinable.GetComponentInParent<Animator>();
			if (!animator) continue;

			var targets = outlinable.outlineTargets;
			foreach (var renderer in animator.GetComponentsInChildren<Renderer>(true)) {
				if (!renderer || HasOutlineTarget(targets, renderer)) continue;
				var materials = renderer.sharedMaterials;
				var submeshCount = materials == null ? 1 : Mathf.Max(1, materials.Length);
				for (var submesh = 0; submesh < submeshCount; submesh++) targets.Add(new OutlineTarget(renderer, submesh));
			}
		}
	}

	private static bool HasOutlineTarget(Il2CppSystem.Collections.Generic.List<OutlineTarget> targets, Renderer renderer) {
		for (var index = 0; index < targets.Count; index++) if (targets[index] != null && targets[index].Renderer == renderer) return true;
		return false;
	}
}

[HarmonyPatch(typeof(Kino.Datamosh), "OnRenderImage")]
static class DatamoshStereoPatch {
	private static int _leftId;
	private static int _rightId;

	internal static void SetHeadsetPair(Kino.Datamosh left, Kino.Datamosh right) {
		_leftId = left.GetInstanceID();
		_rightId = right.GetInstanceID();
	}

	[HarmonyPrefix]
	private static bool RenderOnlyMatchingEye(Kino.Datamosh __instance, RenderTexture source, RenderTexture destination) {
		var id = __instance.GetInstanceID();
		if (id != _leftId && id != _rightId) return true;

		var eye = __instance.GetComponent<Camera>().stereoActiveEye;
		var matches = id == _leftId ? eye == Camera.MonoOrStereoscopicEye.Left : eye == Camera.MonoOrStereoscopicEye.Right;
		if (matches) return true;

		Graphics.Blit(source, destination);
		return false;
	}
}
