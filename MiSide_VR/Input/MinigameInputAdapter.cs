using MiSide_VR.Core;
using MiSide_VR.Input.Patches;
using UnityEngine;
using UnityEngine.XR;
using static MiSide_VR.Plugin;

namespace MiSide_VR.Input;

static class MinigameInputAdapter {
	private const float CartridgeStickDeadzone = 0.18f;
	private const float CartridgeStickSensitivity = 2f;
	private const float ManekenPointerSensitivity = 0.1f;

	private static Vector3 _previousHandPosition;
	private static Vector3 _handVelocity;
	private static bool _hasPreviousHandPosition;
	private static bool _cuttingFlick;
	private static bool _cuttingReady = true;
	private static TamagotchiGame_Cartridge _cartridge;
	private static MakeManeken_Tape _manekenTape;
	private static MakeManeken_Switch _manekenSwitch;
	private static MakeManeken_Box _manekenBox;
	private static Vector2 _previousManekenPointer;
	private static Vector2 _manekenPointerDelta;
	private static int _manekenPointerFrame = -1;
	private static bool _hasManekenPointer;

	internal static void Update(Vector2 move) {
		if (move.y > -0.52f) _cuttingReady = true;
		if (_cuttingReady && move.y < -0.72f) {
			_cuttingFlick = true;
			_cuttingReady = false;
		}
		UpdateHandVelocity();
	}

	internal static float GetMouseAxis(string axisName, Vector2 move) {
		if (axisName.Equals("Mouse X", System.StringComparison.OrdinalIgnoreCase)) {
			if (IsCartridgeMinigame()) return GetCartridgeStickAxis(move.x);
			if (IsDraggingManekenObject()) return GetManekenPointerDelta().x;
			return _handVelocity.x * 0.01f;
		}
		if (IsCartridgeMinigame()) return GetCartridgeStickAxis(move.y);
		if (IsDraggingManekenObject()) return GetManekenPointerDelta().y;
		if (!IsCookingMinigame()) return _handVelocity.y * 0.01f;
		return CookingInputAdapter.CutMotionActive ? -6f : _handVelocity.y * 12f;
	}

	internal static bool CartridgeStickDrag(Vector2 stick) => IsCartridgeMinigame() && (Mathf.Abs(stick.x) > CartridgeStickDeadzone || Mathf.Abs(stick.y) > CartridgeStickDeadzone);

	internal static bool ResetCuttingFlick() {
		if (!_cuttingFlick) return false;
		_cuttingFlick = false;
		return true;
	}

	internal static float CuttingHandVerticalVelocity => _handVelocity.y;

	internal static void Reset() {
		_previousHandPosition = Vector3.zero;
		_handVelocity = Vector3.zero;
		_hasPreviousHandPosition = false;
		_cuttingFlick = false;
		_cuttingReady = true;
		_cartridge = null;
	}

	private static void UpdateHandVelocity() {
		var node = LeftHanded ? XRNode.LeftHand : XRNode.RightHand;
		var current = InputTracking.GetLocalPosition(node);
		if (_hasPreviousHandPosition && Time.deltaTime > 0f) {
			var rawVelocity = (current - _previousHandPosition) / Time.deltaTime;
			_handVelocity = Vector3.Lerp(_handVelocity, rawVelocity, 0.6f);
		}

		_previousHandPosition = current;
		_hasPreviousHandPosition = true;
	}

	private static bool IsCookingMinigame() {
		if (GameContext.Mode != GameMode.Tamagotchi) return false;

		var cooking = Object.FindObjectOfType<TamagotchiGame_Cooking>();
		return cooking && cooking.isActiveAndEnabled;
	}

	private static bool IsCartridgeMinigame() {
		if (GameContext.Mode != GameMode.Tamagotchi) return false;

		if (!_cartridge || !_cartridge.gameObject.activeInHierarchy) _cartridge = Object.FindObjectOfType<TamagotchiGame_Cartridge>();
		return _cartridge && _cartridge.isActiveAndEnabled;
	}

	private static bool IsDraggingManekenObject() {
		if (GameContext.Mode != GameMode.MakeManeken) {
			ResetManekenPointer();
			return false;
		}

		if (_manekenTape && _manekenTape.gameObject.activeInHierarchy && _manekenTape.hold) return true;
		if (_manekenSwitch && _manekenSwitch.gameObject.activeInHierarchy && _manekenSwitch.switchHold) return true;
		if (_manekenBox && _manekenBox.gameObject.activeInHierarchy && _manekenBox.hold) return true;

		_manekenTape = null;
		foreach (var tape in Object.FindObjectsOfType<MakeManeken_Tape>(true)) {
			if (!tape || !tape.gameObject.activeInHierarchy || !tape.hold) continue;
			_manekenTape = tape;
			return true;
		}
		_manekenSwitch = null;
		foreach (var lever in Object.FindObjectsOfType<MakeManeken_Switch>(true)) {
			if (!lever || !lever.gameObject.activeInHierarchy || !lever.switchHold) continue;
			_manekenSwitch = lever;
			return true;
		}
		_manekenBox = null;
		foreach (var box in Object.FindObjectsOfType<MakeManeken_Box>(true)) {
			if (!box || !box.gameObject.activeInHierarchy || !box.hold) continue;
			_manekenBox = box;
			return true;
		}

		ResetManekenPointer();
		return false;
	}

	private static Vector2 GetManekenPointerDelta() {
		if (_manekenPointerFrame == Time.frameCount) return _manekenPointerDelta;
		_manekenPointerFrame = Time.frameCount;
		_manekenPointerDelta = Vector2.zero;

		var camera = GameContext.AuthoredPoseCamera;
		var hand = VRInput.GetHand();
		if (!camera || !hand) {
			_hasManekenPointer = false;
			return _manekenPointerDelta;
		}

		// use a fixed point so the dragged object cannot move its own pointer
		var screen = camera.WorldToScreenPoint(hand.AimRay.GetPoint(10f));
		var pointer = new Vector2(screen.x, screen.y);
		if (_hasManekenPointer) _manekenPointerDelta = (pointer - _previousManekenPointer) * ManekenPointerSensitivity;
		_previousManekenPointer = pointer;
		_hasManekenPointer = screen.z > 0f;
		return _manekenPointerDelta;
	}

	private static void ResetManekenPointer() {
		_hasManekenPointer = false;
		_manekenPointerDelta = Vector2.zero;
		_manekenPointerFrame = -1;
	}

	private static float GetCartridgeStickAxis(float axis) {
		var magnitude = Mathf.Abs(axis);
		if (magnitude <= CartridgeStickDeadzone) return 0f;
		return Mathf.Sign(axis) * Mathf.InverseLerp(CartridgeStickDeadzone, 1f, magnitude) * CartridgeStickSensitivity;
	}
}
