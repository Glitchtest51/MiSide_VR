using EPOOutline;
using HarmonyLib;
using MiSide_VR.Core;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.PostProcessing;

namespace MiSide_VR.Core.Rendering.Patches;

[HarmonyPatch(typeof(PostProcessLayer), nameof(PostProcessLayer.Render))]
static class UiOverlayPatch {
	private static Camera _hetoorFlipCamera;
	private static CommandBuffer _hetoorFlipBuffer;

	private readonly struct RenderState {
		internal readonly bool changed;
		internal readonly bool flip;
		internal readonly bool stereoActive;

		internal RenderState(PostProcessRenderContext context) {
			changed = true;
			flip = context.flip;
			stereoActive = context.stereoActive;
		}
	}

	private static void Prefix(PostProcessRenderContext context, out RenderState __state) {
		__state = default;
		var player = VRPlayer.Instance;
		if (!player || !player.headCamera || context == null || context.camera != player.headCamera) return;

		if (GameContext.Mode == GameMode.Hetoor) {
			// Hetoor needs the same final PPv2 orientation fix as normal gameplay.
			__state = new RenderState(context);
			context.flip = true;
			return;
		}

		if (!player.headsetUiCamera || !player.headsetUiCamera.isActiveAndEnabled || GameContext.IsPanelMode) return;

		// Correct the final PPv2 copy without changing projection or UI orientation.
		__state = new RenderState(context);
		context.flip = true;
	}

	[HarmonyFinalizer]
	private static void Finalizer(PostProcessRenderContext context, RenderState __state) {
		// Clear the reused render context after every blit.
		if (__state.changed && context != null) {
			context.flip = __state.flip;
			context.stereoActive = __state.stereoActive;
		}
	}

	internal static void UpdateHetoorFinalFlip() {
		var player = VRPlayer.Instance;
		var source = GameContext.SourceCamera;
		var required = GameContext.Mode == GameMode.Hetoor && player && player.headCamera && source && HasAncestorNamed(source.transform, "Animation Win");
		if (!required) {
			ReleaseHetoorFinalFlip();
			return;
		}
		if (_hetoorFlipCamera == player.headCamera && _hetoorFlipBuffer != null) return;

		ReleaseHetoorFinalFlip();
		var temporary = Shader.PropertyToID("_MiSideVrHetoorFinalFlip");
		_hetoorFlipCamera = player.headCamera;
		_hetoorFlipBuffer = new CommandBuffer { name = "MiSide_VR Hetoor final flip" };
		_hetoorFlipBuffer.GetTemporaryRT(temporary, -1, -1, 0, FilterMode.Bilinear);
		_hetoorFlipBuffer.Blit(BuiltinRenderTextureType.CameraTarget, temporary);
		_hetoorFlipBuffer.Blit(temporary, BuiltinRenderTextureType.CameraTarget, new Vector2(1f, -1f), new Vector2(0f, 1f));
		_hetoorFlipBuffer.ReleaseTemporaryRT(temporary);
		_hetoorFlipCamera.AddCommandBuffer(CameraEvent.AfterImageEffects, _hetoorFlipBuffer);
	}

	internal static void ReleaseHetoorFinalFlip() {
		if (_hetoorFlipCamera && _hetoorFlipBuffer != null) _hetoorFlipCamera.RemoveCommandBuffer(CameraEvent.AfterImageEffects, _hetoorFlipBuffer);
		_hetoorFlipBuffer?.Release();
		_hetoorFlipBuffer = null;
		_hetoorFlipCamera = null;
	}

	private static bool HasAncestorNamed(Transform current, string objectName) {
		while (current) {
			if (current.name == objectName) return true;
			current = current.parent;
		}
		return false;
	}
}

[HarmonyPatch(typeof(OutlinesPostprocessed), "OnRenderImage")]
static class VrOutlineOrientationPatch {
	private static bool Prefix(OutlinesPostprocessed __instance, RenderTexture source, RenderTexture destination) {
		var player = VRPlayer.Instance;
		if (!player || !player.headCamera || __instance.gameObject != player.headCamera.gameObject || !__instance.PostprocessMaterial || !source || !destination) return true;

		var descriptor = source.descriptor;
		descriptor.depthBufferBits = 0;
		var flipped = RenderTexture.GetTemporary(descriptor);
		var outlined = RenderTexture.GetTemporary(descriptor);
		try {
			// match the depth normal texture orientation before running MiSide's outline shader
			Graphics.Blit(source, flipped, new Vector2(1f, -1f), new Vector2(0f, 1f));
			Graphics.Blit(flipped, outlined, __instance.PostprocessMaterial);
			Graphics.Blit(outlined, destination, new Vector2(1f, -1f), new Vector2(0f, 1f));
		} finally {
			RenderTexture.ReleaseTemporary(outlined);
			RenderTexture.ReleaseTemporary(flipped);
		}
		return false;
	}
}

static class HeadsetOutlinePostprocess {
	internal const string BufferName = "MiSide_VR headset outline postprocess";
	private static readonly int TemporaryTexture = Shader.PropertyToID("_MiSideVrHeadsetOutline");
	private static Camera _camera;
	private static Material _material;
	private static CommandBuffer _buffer;

	internal static void Synchronize(OutlinesPostprocessed source, Camera target) {
		var required = source && source.enabled && source.PostprocessMaterial && target;
		if (!required) {
			Release();
			return;
		}
		if (_camera == target && _material == source.PostprocessMaterial && _buffer != null) return;

		Release();
		_camera = target;
		_material = source.PostprocessMaterial;
		_buffer = new CommandBuffer { name = BufferName };
		_buffer.GetTemporaryRT(TemporaryTexture, -1, -1, 0, FilterMode.Bilinear);
		_buffer.Blit(BuiltinRenderTextureType.CameraTarget, TemporaryTexture, _material);
		_buffer.Blit(TemporaryTexture, BuiltinRenderTextureType.CameraTarget);
		_buffer.ReleaseTemporaryRT(TemporaryTexture);
		_camera.AddCommandBuffer(CameraEvent.BeforeImageEffects, _buffer);
	}

	internal static void Release() {
		if (_camera && _buffer != null) _camera.RemoveCommandBuffer(CameraEvent.BeforeImageEffects, _buffer);
		_buffer?.Release();
		_buffer = null;
		_material = null;
		_camera = null;
	}
}

[HarmonyPatch(typeof(Outliner), nameof(Outliner.OnPreRender))]
static class OutlineRenderOrderPatch {
	private static void Postfix(Outliner __instance) {
		var player = VRPlayer.Instance;
		if (!player || !player.headCamera || __instance.GetComponent<Camera>() != player.headCamera || __instance.Event != CameraEvent.BeforeImageEffects) return;

		var camera = player.headCamera;
		var outline = __instance.parameters?.Buffer;
		if (outline == null) return;
		var buffers = camera.GetCommandBuffers(CameraEvent.BeforeImageEffects);
		if (buffers.Length < 2) return;

		// match command buffers by name because il2cpp returns new wrappers
		var outlineIndex = -1;
		var postprocessIndex = -1;
		for (var index = 0; index < buffers.Length; index++) {
			if (buffers[index].name == outline.name) {
				if (outlineIndex >= 0) return;
				outlineIndex = index;
			} else if (buffers[index].name == HeadsetOutlinePostprocess.BufferName) {
				if (postprocessIndex >= 0) return;
				postprocessIndex = index;
			}
		}
		if (outlineIndex < 0) return;
		if (outlineIndex == 0 && (postprocessIndex < 0 || postprocessIndex == 1)) return;

		// keep both outline passes before PPV2s final orientation correction
		foreach (var buffer in buffers) camera.RemoveCommandBuffer(CameraEvent.BeforeImageEffects, buffer);
		camera.AddCommandBuffer(CameraEvent.BeforeImageEffects, buffers[outlineIndex]);
		if (postprocessIndex >= 0) camera.AddCommandBuffer(CameraEvent.BeforeImageEffects, buffers[postprocessIndex]);
		for (var index = 0; index < buffers.Length; index++)
			if (index != outlineIndex && index != postprocessIndex) camera.AddCommandBuffer(CameraEvent.BeforeImageEffects, buffers[index]);
	}
}
