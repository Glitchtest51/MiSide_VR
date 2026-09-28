using HarmonyLib;
using MiSide_VR.Core;
using MiSide_VR.Input;
using UnityEngine;
using static MiSide_VR.Plugin;

namespace MiSide_VR.UI.Patches;

[HarmonyPatch(typeof(MenuMita), "Update")]
public static class MenuMitaPatch {
	private struct TargetState {
		public Transform Target;
		public Vector3 Position;
	}

	private static void Prefix(MenuMita __instance, out TargetState __state) {
		__state = default;
		if (!(VREnabled && GameContext.Scene.name == "SceneMenu" && __instance) || !__instance.changeMenu || !__instance.changeMenu.caseChangeNow) return;

		var target = __instance.changeMenu.caseChangeNow.transform;
		var canvas = target.GetComponentInParent<Canvas>();
		var captureCamera = canvas ? canvas.worldCamera : null;
		var sourceCamera = GameContext.SourceCamera;
		if (!canvas || !captureCamera || !sourceCamera || captureCamera == sourceCamera) return;

		var viewport = captureCamera.WorldToViewportPoint(target.position);
		if (viewport.z <= 0f) return;

		__state.Target = target;
		__state.Position = target.position;
		var depth = Mathf.Max(canvas.planeDistance, sourceCamera.nearClipPlane + 0.01f);
		target.position = sourceCamera.ViewportToWorldPoint(new Vector3(viewport.x, viewport.y, depth));
	}

	private static void Postfix(TargetState __state) { if (__state.Target) __state.Target.position = __state.Position; }
}

[HarmonyPatch(typeof(MenuPersonage), "Update")]
public static class MenuSpinPatch {
	private const float SpinSensitivity = 3.2f;
	private static MenuPersonage _owner;
	private static Transform _target;
	private static Quaternion _authoredRotation;
	private static float _yawOffset;
	private static bool _hasAuthoredRotation;
	private static bool _dragging;
	private static Vector3 _lastAim;
	private static int _lastAppliedFrame = -1;

	public static void Prefix(MenuPersonage __instance) {
		if (!CanUseViewer(__instance)) {
			Reset();
			return;
		}

		var target = GetTarget(__instance);
		if (_target != target || !_hasAuthoredRotation) return;

		try { _target.rotation = _authoredRotation; }
		catch { Reset(); }
	}

	public static void Postfix(MenuPersonage __instance) {
		var target = GetTarget(__instance);
		if (!target) {
			Reset();
			return;
		}

		if (_target != target) {
			_owner = __instance;
			_target = target;
			_yawOffset = 0f;
			_dragging = false;
		}
		_authoredRotation = target.rotation;
		_hasAuthoredRotation = true;

		var hand = VRInput.GetHand();
		if (!hand) {
			_dragging = false;
			return;
		}

		if (VRInput.GetButtonDown("Interact")) {
			_dragging = !hand.IsPointingAtInteractableUI;
			_lastAim = hand.AimRay.direction;
		}
		if (!_dragging) return;
		if (!VRInput.GetButton("Interact")) {
			_dragging = false;
			return;
		}

		var currentAim = hand.AimRay.direction;
		var previousFlat = Vector3.ProjectOnPlane(_lastAim, Vector3.up);
		var currentFlat = Vector3.ProjectOnPlane(currentAim, Vector3.up);
		_lastAim = currentAim;
		if (previousFlat.sqrMagnitude < 0.0001f || currentFlat.sqrMagnitude < 0.0001f) return;

		_yawOffset -= Vector3.SignedAngle(previousFlat, currentFlat, Vector3.up) * SpinSensitivity;
	}

	public static void ApplyLate() {
		if (!CanUseViewer(_owner) || !_target || !_hasAuthoredRotation || _lastAppliedFrame == Time.frameCount) return;
		if (!_owner.objectPersonage || _owner.objectPersonage.transform != _target) {
			Reset();
			return;
		}

		_lastAppliedFrame = Time.frameCount;
		try { _target.rotation = Quaternion.AngleAxis(_yawOffset, Vector3.up) * _authoredRotation; }
		catch { Reset(); }
	}

	private static Transform GetTarget(MenuPersonage instance) {
		if (!CanUseViewer(instance) || !instance.contentDescription3D || !instance.contentDescription3D.gameObject.activeInHierarchy || !instance.objectPersonage || !instance.objectPersonage.activeInHierarchy) return null;
		return instance.objectPersonage.transform;
	}

	private static bool CanUseViewer(MenuPersonage instance) => VREnabled && GameContext.Scene.name == "SceneMenu" && instance && !instance.loadingNow;

	private static void Reset() {
		_owner = null;
		_target = null;
		_hasAuthoredRotation = false;
		_yawOffset = 0f;
		_dragging = false;
		_lastAppliedFrame = -1;
	}
}

[HarmonyPatch(typeof(MenuMitaDance), "Update")]
public static class MenuMitaOutfitSpinPatch {
	private const float DragSensitivity = 3.2f;
	private static bool _dragging;
	private static Vector3 _lastAim;

	public static void Postfix(MenuMitaDance __instance) {
		// use MenuMitaDances native animations
		if (!VREnabled || !__instance || !__instance.cloth) {
			_dragging = false;
			return;
		}

		var hand = VRInput.GetHand();
		if (!hand) {
			_dragging = false;
			return;
		}

		if (VRInput.GetButtonDown("Interact")) {
			_dragging = !hand.IsPointingAtInteractableUI;
			_lastAim = hand.AimRay.direction;
		}
		if (!_dragging) return;
		if (!VRInput.GetButton("Interact")) {
			_dragging = false;
			return;
		}

		var currentAim = hand.AimRay.direction;
		var previousFlat = Vector3.ProjectOnPlane(_lastAim, Vector3.up);
		var currentFlat = Vector3.ProjectOnPlane(currentAim, Vector3.up);
		_lastAim = currentAim;
		if (previousFlat.sqrMagnitude < 0.0001f || currentFlat.sqrMagnitude < 0.0001f) return;

		__instance.rotationPlace = Mathf.Repeat(__instance.rotationPlace - Vector3.SignedAngle(previousFlat, currentFlat, Vector3.up) * DragSensitivity, 360f);
		__instance.timeSpeedRotate = 5f;
	}
}
