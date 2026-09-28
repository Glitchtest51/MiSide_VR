using HarmonyLib;
using MiSide_VR.Core;
using UnityEngine;
using MiSide_VR.UI;
using MiSide_VR.UI.Patches;

namespace MiSide_VR.Input.Patches;

[HarmonyPatch]
public static class InputPatch {
	private enum InputPhase { Current, Down, Up }
	private const float MobilePlayerLookSpeed = 120f;
	private static bool _centerManekenCameraPointer;
	private static bool _readingMobilePlayerLook;
	private static bool _readingMenuMitaDance;
	private static Location14_PCGames _pcGames;
	private static Collider _pcMouseCollider;
	private static Camera _pcOriginalCamera;
	private static Camera _pcInputCamera;
	private static Camera _pcOriginalCanvasCamera;
	private static Canvas _pcCanvas;
	private static Vector3 _lastPcMousePosition;
	private static bool _hasLastPcMousePosition;
	private static bool CanInjectVrInput => VRInput.IsInitialized;

	[HarmonyPatch(typeof(Location14_PCGames), nameof(Location14_PCGames.Update))]
	[HarmonyPrefix]
	private static void CapturePcMonitor(Location14_PCGames __instance) {
		if (_pcGames != __instance) {
			RestorePcInputCamera();
			_pcGames = __instance;
			var plane = __instance ? __instance.transform.Find("Plane") : null;
			_pcMouseCollider = plane ? plane.GetComponent<Collider>() : null;
			_hasLastPcMousePosition = false;
		}
		if (!__instance || !__instance.gamePlay) {
			RestorePcInputCamera();
			return;
		}
		if (!_pcInputCamera && __instance.cameraPlayer) {
			_pcOriginalCamera = __instance.cameraPlayer;
			_pcCanvas = __instance.GetComponent<Canvas>();
			if (_pcCanvas) {
				_pcOriginalCanvasCamera = _pcCanvas.worldCamera;
				var cameraObject = new GameObject("[VR PC Input Camera]");
				cameraObject.transform.SetParent(_pcOriginalCamera.transform, false);
				_pcInputCamera = cameraObject.AddComponent<Camera>();
				_pcInputCamera.CopyFrom(_pcOriginalCamera);
				_pcInputCamera.transform.localPosition = Vector3.zero;
				_pcInputCamera.transform.localRotation = Quaternion.identity;
				_pcInputCamera.stereoTargetEye = StereoTargetEyeMask.None;
				_pcInputCamera.enabled = false;
				__instance.cameraPlayer = _pcInputCamera;
				_pcCanvas.worldCamera = _pcInputCamera;
			}
		}
		if (_pcInputCamera) {
			// use desktop aspect for the PC minigames clickable viewport
			_pcInputCamera.fieldOfView = _pcOriginalCamera.fieldOfView;
			_pcInputCamera.aspect = (float)Screen.width / Screen.height;
			_pcInputCamera.enabled = false;
		}
	}

	private static void RestorePcInputCamera() {
		if (!_pcInputCamera) return;
		if (_pcGames && _pcGames.cameraPlayer == _pcInputCamera) _pcGames.cameraPlayer = _pcOriginalCamera;
		if (_pcCanvas && _pcCanvas.worldCamera == _pcInputCamera) _pcCanvas.worldCamera = _pcOriginalCanvasCamera;
		Object.Destroy(_pcInputCamera.gameObject);
		_pcInputCamera = null;
		_pcOriginalCamera = null;
		_pcOriginalCanvasCamera = null;
		_pcCanvas = null;
	}

	[HarmonyPatch(typeof(Location14_PlayerMobileMove), nameof(Location14_PlayerMobileMove.Update))]
	[HarmonyPrefix]
	public static void BeforeMobilePlayerUpdate(Location14_PlayerMobileMove __instance) => _readingMobilePlayerLook = CanInjectVrInput && __instance && __instance.play;

	[HarmonyPatch(typeof(Location14_PlayerMobileMove), nameof(Location14_PlayerMobileMove.Update))]
	[HarmonyPostfix]
	public static void AfterMobilePlayerUpdate() => _readingMobilePlayerLook = false;

	[HarmonyPatch(typeof(Location14_PlayerMobileMove), nameof(Location14_PlayerMobileMove.Update))]
	[HarmonyFinalizer]
	public static System.Exception FinalizeMobilePlayerUpdate(System.Exception __exception) {
		_readingMobilePlayerLook = false;
		return __exception;
	}

	[HarmonyPatch(typeof(MakeManeken_Main), nameof(MakeManeken_Main.Update))]
	[HarmonyPrefix]
	// keep MakeManekens camera centered while its raycasts use the VR pointer
	public static void BeforeMakeManekenUpdate(MakeManeken_Main __instance) { _centerManekenCameraPointer = CanInjectVrInput && __instance && GameContext.Mode == GameMode.MakeManeken; }

	[HarmonyPatch(typeof(MakeManeken_Main), nameof(MakeManeken_Main.Update))]
	[HarmonyPostfix]
	public static void AfterMakeManekenUpdate() => _centerManekenCameraPointer = false;

	[HarmonyPatch(typeof(MakeManeken_Main), nameof(MakeManeken_Main.Update))]
	[HarmonyFinalizer]
	public static System.Exception FinalizeMakeManekenUpdate(System.Exception __exception) {
		_centerManekenCameraPointer = false;
		return __exception;
	}

	[HarmonyPatch(typeof(MenuMitaDance), nameof(MenuMitaDance.Update))]
	[HarmonyPrefix]
	private static void BeforeMenuMitaDanceUpdate(out bool __state) {
		__state = _readingMenuMitaDance;
		_readingMenuMitaDance = true;
	}

	[HarmonyPatch(typeof(MenuMitaDance), nameof(MenuMitaDance.Update))]
	[HarmonyFinalizer]
	private static System.Exception AfterMenuMitaDanceUpdate(System.Exception __exception, bool __state) {
		_readingMenuMitaDance = __state;
		return __exception;
	}

	[HarmonyPatch(typeof(Camera), nameof(Camera.ScreenPointToRay), typeof(Vector3))]
	[HarmonyPrefix]
	private static bool UseMenuMitaAimRay(ref Ray __result) {
		if (!_readingMenuMitaDance || !VRInput.IsInitialized) return true;
		var hand = VRInput.GetHand();
		if (!hand) return true;

		__result = hand.AimRay;
		return false;
	}

	[HarmonyPatch(typeof(UnityEngine.Input), nameof(UnityEngine.Input.GetButton))]
	[HarmonyPostfix]
	public static void PatchGetButton(string buttonName, ref bool __result) => AddMappedButton(buttonName, InputPhase.Current, ref __result);

	[HarmonyPatch(typeof(UnityEngine.Input), nameof(UnityEngine.Input.GetButtonDown))]
	[HarmonyPostfix]
	public static void PatchGetButtonDown(string buttonName, ref bool __result) => AddMappedButton(buttonName, InputPhase.Down, ref __result);

	[HarmonyPatch(typeof(UnityEngine.Input), nameof(UnityEngine.Input.GetButtonUp))]
	[HarmonyPostfix]
	public static void PatchGetButtonUp(string buttonName, ref bool __result) => AddMappedButton(buttonName, InputPhase.Up, ref __result);

	[HarmonyPatch(typeof(UnityEngine.Input), nameof(UnityEngine.Input.GetAxis))]
	[HarmonyPostfix]
	public static void PatchGetAxis(string axisName, ref float __result) {
		if (!CanInjectVrInput || !VRInput.IsAxisMapped(axisName)) return;
		if (_readingMobilePlayerLook && (axisName.Equals("Mouse X", System.StringComparison.OrdinalIgnoreCase) || axisName.Equals("Mouse Y", System.StringComparison.OrdinalIgnoreCase))) {
			var stick = VRInput.GetVector2("Turn");
			var axis = axisName.Equals("Mouse X", System.StringComparison.OrdinalIgnoreCase) ? stick.x : stick.y;
			if (Mathf.Abs(axis) < 0.15f) axis = 0f;
			// normalize the phone controllers nondeltaTime mouse input
			__result = axis * MobilePlayerLookSpeed * Time.deltaTime / Mathf.Max(0.1f, GlobalGame.mouseSpeed + 1f);
			return;
		}
		if (GameContext.Mode == GameMode.Hetoor && (axisName.Equals("Mouse X", System.StringComparison.OrdinalIgnoreCase) || axisName.Equals("Mouse Y", System.StringComparison.OrdinalIgnoreCase))) {
			// HetoorPatch already writes absolute controller aim
			__result = 0f;
			return;
		}

		var vr = VRInput.GetMappedAxis(axisName);
		if (Mathf.Abs(vr) > Mathf.Abs(__result)) __result = vr;
	}

	[HarmonyPatch(typeof(UnityEngine.Input), nameof(UnityEngine.Input.GetMouseButton))]
	[HarmonyPostfix]
	public static void PatchGetMouseButton(int button, ref bool __result) => AddMappedMouseButton(button, InputPhase.Current, ref __result);

	[HarmonyPatch(typeof(UnityEngine.Input), nameof(UnityEngine.Input.GetMouseButtonDown))]
	[HarmonyPostfix]
	public static void PatchGetMouseButtonDown(int button, ref bool __result) => AddMappedMouseButton(button, InputPhase.Down, ref __result);

	[HarmonyPatch(typeof(UnityEngine.Input), nameof(UnityEngine.Input.GetMouseButtonUp))]
	[HarmonyPostfix]
	public static void PatchGetMouseButtonUp(int button, ref bool __result) => AddMappedMouseButton(button, InputPhase.Up, ref __result);

	[HarmonyPatch(typeof(UnityEngine.Input), nameof(UnityEngine.Input.GetKey), typeof(KeyCode))]
	[HarmonyPostfix]
	public static void PatchGetKey(KeyCode key, ref bool __result) => AddMappedKey(GetVrKeyAction(key), GetVrKeyDirection(key), InputPhase.Current, ref __result);

	[HarmonyPatch(typeof(UnityEngine.Input), nameof(UnityEngine.Input.GetKeyDown), typeof(KeyCode))]
	[HarmonyPostfix]
	public static void PatchGetKeyDown(KeyCode key, ref bool __result) => AddMappedKey(GetVrKeyAction(key), GetVrKeyDirection(key), InputPhase.Down, ref __result);

	[HarmonyPatch(typeof(UnityEngine.Input), nameof(UnityEngine.Input.GetKeyUp), typeof(KeyCode))]
	[HarmonyPostfix]
	public static void PatchGetKeyUp(KeyCode key, ref bool __result) => AddMappedKey(GetVrKeyAction(key), GetVrKeyDirection(key), InputPhase.Up, ref __result);

	[HarmonyPatch(typeof(UnityEngine.Input), nameof(UnityEngine.Input.GetKey), typeof(string))]
	[HarmonyPostfix]
	public static void PatchGetKey(string name, ref bool __result) => AddMappedKey(GetVrKeyAction(name), GetVrKeyDirection(name), InputPhase.Current, ref __result);

	[HarmonyPatch(typeof(UnityEngine.Input), nameof(UnityEngine.Input.GetKeyDown), typeof(string))]
	[HarmonyPostfix]
	public static void PatchGetKeyDown(string name, ref bool __result) => AddMappedKey(GetVrKeyAction(name), GetVrKeyDirection(name), InputPhase.Down, ref __result);

	[HarmonyPatch(typeof(UnityEngine.Input), nameof(UnityEngine.Input.GetKeyUp), typeof(string))]
	[HarmonyPostfix]
	public static void PatchGetKeyUp(string name, ref bool __result) => AddMappedKey(GetVrKeyAction(name), GetVrKeyDirection(name), InputPhase.Up, ref __result);
	
	[HarmonyPatch(typeof(UnityEngine.Input), nameof(UnityEngine.Input.anyKeyDown), MethodType.Getter)]
	[HarmonyPostfix]
	private static void PatchAnyKeyDown(ref bool __result) { __result |= VRInput.GetAnyButtonDown(); }

	private static void AddMappedButton(string buttonName, InputPhase phase, ref bool result) {
		if (CanInjectVrInput && VRInput.IsButtonMapped(buttonName) && !PointerOwnsInteract(buttonName)) result |= ReadMappedButton(buttonName, phase);
	}

	private static void AddMappedMouseButton(int button, InputPhase phase, ref bool result) {
		if (!CanInjectVrInput || !VRInput.IsMouseButtonMapped(button)) return;
		if (BackgroundUiInputAdapter.OwnsPointer && !MinigameInputAdapter.CartridgeStickDrag(VRInput.GetVector2("Move"))) return;

		result |= ReadMappedMouseButton(button, phase);
	}

	private static void AddMappedKey(string action, string direction, InputPhase phase, ref bool result) {
		if (CanApplyKeyAction(action)) result |= ReadAction(action, phase);
		if (CanInjectVrInput && direction != null) result |= ReadMappedButton(direction, phase);
	}

	private static bool ReadAction(string action, InputPhase phase) => phase switch { InputPhase.Current => VRInput.GetButton(action), InputPhase.Down => VRInput.GetButtonDown(action), InputPhase.Up => VRInput.GetButtonUp(action), _ => false };

	private static bool ReadMappedButton(string buttonName, InputPhase phase) => phase switch { InputPhase.Current => VRInput.GetMappedButton(buttonName), InputPhase.Down => VRInput.GetMappedButtonDown(buttonName), InputPhase.Up => VRInput.GetMappedButtonUp(buttonName), _ => false };

	private static bool ReadMappedMouseButton(int button, InputPhase phase) => phase switch { InputPhase.Current => VRInput.GetMappedMouseButton(button), InputPhase.Down => VRInput.GetMappedMouseButtonDown(button), InputPhase.Up => VRInput.GetMappedMouseButtonUp(button), _ => false };

	[HarmonyPatch(typeof(UnityEngine.Input), nameof(UnityEngine.Input.mousePosition), MethodType.Getter)]
	[HarmonyPrefix]
	public static bool OverrideMousePosition(ref Vector3 __result) {
		if (_centerManekenCameraPointer) {
			__result = new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f);
			return false;
		}
		if (!CanInjectVrInput) return true;
		if (BackgroundUiInputAdapter.TryGetPointerPosition(out var capturedPixels)) {
			__result = new Vector3(capturedPixels.x, capturedPixels.y, 0f);
			return false;
		}
		if (TryGetPcMonitorMousePosition(out var pcPixels)) {
			__result = pcPixels;
			return false;
		}
		VRController hand = VRInput.GetHand();
		if (!VRPlayer.Instance || !VRPlayer.Instance.camera || !hand) return true;
		var ray = hand.AimRay;
		var panelHit = VirtualScreen.TryGetPointerHit(ray, out var pointerPixels, out _);
		var panelOwnsPointer = panelHit && CanvasPatch.IsPointerOverInteractable(pointerPixels);
		if (panelOwnsPointer) {
			__result = new Vector3(pointerPixels.x, pointerPixels.y, 0f);
			return false;
		}

		Vector3 worldPoint;
		if (Physics.Raycast(ray, out RaycastHit hit, 100f, hand.rayCastMask)) { worldPoint = hit.point;
		} else { worldPoint = ray.GetPoint(10f); }
		if (GameContext.Scene.name == "SceneMenu" && VirtualScreen.Instance && GameContext.SourceCamera) {
			// project menu effects into the capture texture instead of desktop resolution
			var viewport = GameContext.SourceCamera.WorldToViewportPoint(worldPoint);
			__result = viewport.z > 0f ? new Vector3(viewport.x * VirtualScreen.PixelWidth, viewport.y * VirtualScreen.PixelHeight, 0f) : new Vector3(-10000f, -10000f, 0f);
			return false;
		}

		// project MakeManeken input through its own animated camera
		var pointerCamera = GameContext.Mode == GameMode.MakeManeken && GameContext.AuthoredPoseCamera ? GameContext.AuthoredPoseCamera : VRPlayer.Instance.camera;
		Vector3 screen = pointerCamera.WorldToScreenPoint(worldPoint);
		if (screen.z < 0f) return true;

		// keep coords outside the desktop view so VR can reach every control
		__result = GameContext.Mode == GameMode.MakeManeken ? new Vector3(screen.x, screen.y, 0f) : new Vector3(Mathf.Clamp(screen.x, 0f, Screen.width), Mathf.Clamp(screen.y, 0f, Screen.height), 0f);
		return false;
	}

	private static bool TryGetPcMonitorMousePosition(out Vector3 pixels) {
		pixels = default;
		var pc = _pcGames;
		if (!pc || !pc.gamePlay || !pc.canMouseMove || !pc.cameraPlayer || !_pcMouseCollider) return false;
		var hand = VRInput.GetHand();
		if (!hand) return false;

		var aim = hand.AimRay;
		if (Physics.Raycast(aim, out var hit, 50f, pc.mouseLayer.value) && hit.collider == _pcMouseCollider) {
			// use MiSides monitor collider hit
			var projected = pc.cameraPlayer.WorldToScreenPoint(hit.point);
			if (projected.z > 0f) {
				// do not clamp the worldspace monitor to desktop resolution
				pixels = new Vector3(projected.x, projected.y, 0f);
				_lastPcMousePosition = pixels;
				_hasLastPcMousePosition = true;
				return true;
			}
		}
		// keep the last monitor coordinate when the ray leaves its plane
		return UseLastPcMousePosition(out pixels);
	}

	private static bool UseLastPcMousePosition(out Vector3 pixels) {
		pixels = _lastPcMousePosition;
		return _hasLastPcMousePosition;
	}

	private static bool PointerOwnsInteract(string buttonName = null) {
		if (buttonName != null && !VRInput.UsesInteractAction(buttonName)) return false;
		return BackgroundUiInputAdapter.OwnsPointer;
	}

	private static bool CanApplyKeyAction(string action) => CanInjectVrInput && action != null && (action != "Interact" || !PointerOwnsInteract());

	private static string GetVrKeyAction(KeyCode key) => key switch { KeyCode.E or KeyCode.Space => "Interact", KeyCode.Q => "Decline", KeyCode.Escape => "Menu", KeyCode.LeftShift => "SprintToggle", _ => null };

	private static string GetVrKeyDirection(KeyCode key) => key switch { KeyCode.A => "Left", KeyCode.D => "Right", KeyCode.S => "Down", _ => null };

	private static string GetVrKeyDirection(string key) {
		if (key == null) return null;
		if (key.Equals("a", System.StringComparison.OrdinalIgnoreCase)) return "Left";
		if (key.Equals("s", System.StringComparison.OrdinalIgnoreCase)) return "Down";
		return key.Equals("d", System.StringComparison.OrdinalIgnoreCase) ? "Right" : null;
	}

	private static string GetVrKeyAction(string key) {
		if (key == null) return null;
		if (key.Equals("e", System.StringComparison.OrdinalIgnoreCase) || key.Equals("space", System.StringComparison.OrdinalIgnoreCase)) return "Interact";
		if (key.Equals("q", System.StringComparison.OrdinalIgnoreCase)) return "Decline";
		if (key.Equals("left shift", System.StringComparison.OrdinalIgnoreCase) || key.Equals("leftshift", System.StringComparison.OrdinalIgnoreCase)) return "SprintToggle";
		return key.Equals("escape", System.StringComparison.OrdinalIgnoreCase) ? "Menu" : null;
	}
}

[HarmonyPatch]
public static class CursorPatch {
	[HarmonyPatch(typeof(Cursor), nameof(Cursor.lockState), MethodType.Setter)]
	[HarmonyPrefix]
	public static void KeepCursorUnlocked(ref CursorLockMode __0) { __0 = CursorLockMode.None; }

	[HarmonyPatch(typeof(Cursor), nameof(Cursor.visible), MethodType.Setter)]
	[HarmonyPrefix]
	public static void KeepCursorVisible(ref bool __0) { __0 = true; }
}

[HarmonyPatch(typeof(ButtonMouseClick), nameof(ButtonMouseClick.Update))]
public static class MenuButtonSelectionPatch {
	public static void Prefix(ButtonMouseClick __instance) {
		// clear MiSides native button hover when the VR pointer leaves clickable UI
		if (!GameContext.IsPanelMode || !__instance || !__instance.changeNow) return;

		var hand = VRInput.GetHand();
		if (hand && !hand.IsPointingAtInteractableUI) __instance.PointerExit();
	}
}

static class CookingInputAdapter {
	private static TamagotchiGame_Cooking _cooking;
	private static int _cutMotionFrames;
	private static int _cutStartIndex;
	private static bool _handChopArmed = true;
	private const float HandChopDownVelocity = -0.55f;
	private const float HandChopResetVelocity = 0.15f;

	internal static bool CutMotionActive => _cutMotionFrames > 0;

	public static void Update() {
		if (!_cooking || !_cooking.gameObject.activeInHierarchy) _cooking = UnityEngine.Object.FindObjectOfType<TamagotchiGame_Cooking>();
		if (!_cooking || !_cooking.isActiveAndEnabled) {
			ResetMotion();
			return;
		}

		var handVelocity = MinigameInputAdapter.CuttingHandVerticalVelocity;
		if (handVelocity > HandChopResetVelocity) _handChopArmed = true;
		var handChop = _handChopArmed && handVelocity < HandChopDownVelocity;
		var stickChop = MinigameInputAdapter.ResetCuttingFlick();
		if ((stickChop || handChop) && _cooking.canCut && !_cooking.cutBack) {
			_cutStartIndex = _cooking.indexCut;
			_cutMotionFrames = 9;
			if (handChop) _handChopArmed = false;
		}

		if (_cutMotionFrames > 0 && (!_cooking.canCut || _cooking.cutBack || _cooking.indexCut != _cutStartIndex)) _cutMotionFrames = 0;

		// feed stick and controller chops through MiSides native Mouse Y path
		_cooking.CutHold(VRInput.GetButton("Interact") || CutMotionActive);
		if (_cutMotionFrames > 0) _cutMotionFrames--;
	}

	private static void ResetMotion() {
		_cutMotionFrames = 0;
		_handChopArmed = true;
	}
}
