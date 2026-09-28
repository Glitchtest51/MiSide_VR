using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using BepInEx;
using MiSide_VR.Core;
using UnityEngine;
using UnityEngine.XR;
using Valve.VR;
using static MiSide_VR.Plugin;

namespace MiSide_VR.Input;

public static class VRInput {
	private static readonly Dictionary<string, ulong> DigitalHandles = new(StringComparer.OrdinalIgnoreCase);
	private static readonly Dictionary<string, ulong> AnalogHandles = new(StringComparer.OrdinalIgnoreCase);
	private static readonly Dictionary<string, ulong> HapticHandles = new(StringComparer.OrdinalIgnoreCase);
	private static readonly Dictionary<string, DigitalState> DigitalStates = new(StringComparer.OrdinalIgnoreCase);
	private static readonly Dictionary<string, Vector3> AnalogStates = new(StringComparer.OrdinalIgnoreCase);

	private static CVRInput _input;
	private static VRActiveActionSet_t[] _activeActionSets;
	private static ulong _leftHandHandle;
	private static ulong _rightHandHandle;
	private static Vector2 _previousMove;
	private static Vector2 _currentMove;
	private static bool _sprintToggled;
	private static bool _previousSprintToggled;
	private static bool _anyButtonDown;

	private enum ButtonPhase { Current, Down, Up }

	private static readonly Dictionary<string, string[]> ButtonMap = new(StringComparer.OrdinalIgnoreCase) {
		["Interactive"] = new[] { "Interact" },
		["Jump"] = new[] { "GrabRight" },
		["MouseClick"] = new[] { "Interact" },
		["Submit"] = new[] { "Interact" },
		["Cancel"] = new[] { "Menu" },
		["Q"] = new[] { "Decline" },
		["Sit"] = new[] { "GrabLeft" },
		["Space"] = new[] { "Interact" },
		["Open"] = new[] { "Interact" },
		["AnykeyPlay"] = new[] { "Interact" },
		["Shift"] = new[] { "SprintToggle" },
		["Left"] = Array.Empty<string>(),
		["Right"] = Array.Empty<string>(),
		["Up"] = Array.Empty<string>(),
		["Down"] = Array.Empty<string>()
	};

	private static readonly Dictionary<string, AxisMapping> AxisMap = new(StringComparer.OrdinalIgnoreCase) {
		["Horizontal"] = new("Move", AxisComponent.X),
		["Vertical"] = new("Move", AxisComponent.Y),
		["Mouse X"] = new("Pose", AxisComponent.X),
		["Mouse Y"] = new("Pose", AxisComponent.Y)
	};

	private static readonly Dictionary<int, string> MouseButtonMap = new() { [0] = "Interact" };

	private readonly struct AxisMapping {
		public AxisMapping(string actionName, AxisComponent component) {
			ActionName = actionName;
			Component = component;
		}

		public string ActionName { get; }
		public AxisComponent Component { get; }
	}

	private enum AxisComponent { X, Y }

	public static bool IsInitialized { get; private set; }

	public static void Initialize() {
		if (IsInitialized) return;

		_input = OpenVR.Input ?? throw new InvalidOperationException("SteamVR input interface is unavailable.");

		var manifestPath = Path.Combine(Paths.GameRootPath, "MiSideFull_Data", "StreamingAssets", "SteamVR", "actions.json");

		if (!File.Exists(manifestPath)) throw new FileNotFoundException("SteamVR action manifest was not deployed.", manifestPath);

		Check(_input.SetActionManifestPath(manifestPath), "SetActionManifestPath");
		using var document = JsonDocument.Parse(File.ReadAllText(manifestPath));
		var root = document.RootElement;
		var activeSets = new List<VRActiveActionSet_t>();

		foreach (var actionSet in root.GetProperty("action_sets").EnumerateArray()) {
			var path = actionSet.GetProperty("name").GetString();
			ulong handle = 0;
			Check(_input.GetActionSetHandle(path, ref handle), $"GetActionSetHandle({path})");
			activeSets.Add(new VRActiveActionSet_t { ulActionSet = handle, ulRestrictedToDevice = OpenVR.k_ulInvalidInputValueHandle, ulSecondaryActionSet = OpenVR.k_ulInvalidActionSetHandle, nPriority = 0 });
		}

		foreach (var action in root.GetProperty("actions").EnumerateArray()) {
			var path = action.GetProperty("name").GetString();
			var type = action.GetProperty("type").GetString();
			var name = ShortName(path);

			switch (type) {
				case "boolean": RegisterDigital(name, path); break;
				case "vector1":
				case "vector2":
				case "vector3": RegisterAnalog(name, path); break;
				case "vibration": RegisterHaptic(name, path); break;
			}
		}

		_activeActionSets = activeSets.ToArray();
		if (_activeActionSets.Length == 0) throw new InvalidOperationException("The SteamVR manifest contains no action sets.");

		Check(_input.GetInputSourceHandle("/user/hand/left", ref _leftHandHandle), "GetInputSourceHandle(left)");
		Check(_input.GetInputSourceHandle("/user/hand/right", ref _rightHandHandle), "GetInputSourceHandle(right)");

		IsInitialized = true;
		if (DebugMode) Log.LogDebug($"[VRInput] Loaded {DigitalHandles.Count} digital, {AnalogHandles.Count} analog and {HapticHandles.Count} haptic SteamVR actions");
	}

	public static void UpdateInput() {
		if (!IsInitialized) return;
		_anyButtonDown = false;

		var updateError = _input.UpdateActionState(_activeActionSets, (uint)Marshal.SizeOf<VRActiveActionSet_t>());

		if (updateError != EVRInputError.None) return;

		foreach (var pair in DigitalHandles) {
			var data = new InputDigitalActionData_t();
			var error = _input.GetDigitalActionData(pair.Value, ref data, (uint)Marshal.SizeOf<InputDigitalActionData_t>(), OpenVR.k_ulInvalidInputValueHandle);
			
			var previous = DigitalStates[pair.Key].Current;
			var current = error == EVRInputError.None && data.bActive && data.bState;
			_anyButtonDown |= current && !previous;
			
			DigitalStates[pair.Key] = new DigitalState { Previous = previous, Current = current };
		}

		_previousSprintToggled = _sprintToggled;
		if (GetButtonDown("Sprint")) _sprintToggled = !_sprintToggled;

		foreach (var pair in AnalogHandles) {
			var data = new InputAnalogActionData_t();
			var error = _input.GetAnalogActionData(pair.Value, ref data, (uint)Marshal.SizeOf<InputAnalogActionData_t>(), OpenVR.k_ulInvalidInputValueHandle);

			AnalogStates[pair.Key] = error == EVRInputError.None && data.bActive ? new Vector3(data.x, data.y, data.z) : Vector3.zero;
		}

		_previousMove = _currentMove;
		_currentMove = GetVector2("Move");
		MinigameInputAdapter.Update(_currentMove);
	}

	public static bool GetButton(string actionName) => ReadButton(actionName, ButtonPhase.Current);
	public static bool GetButtonDown(string actionName) => ReadButton(actionName, ButtonPhase.Down);
	public static bool GetButtonUp(string actionName) => ReadButton(actionName, ButtonPhase.Up);
	public static bool GetAnyButtonDown() => IsInitialized && _anyButtonDown;

	private static bool ReadButton(string actionName, ButtonPhase phase) {
		if (actionName.Equals("SprintToggle", StringComparison.OrdinalIgnoreCase)) return phase switch { ButtonPhase.Current => _sprintToggled, ButtonPhase.Down => _sprintToggled && !_previousSprintToggled, ButtonPhase.Up => !_sprintToggled && _previousSprintToggled, _ => false };
		if (!DigitalStates.TryGetValue(ResolveDigitalAction(actionName), out var state)) return false;
		return phase switch { ButtonPhase.Current => state.Current, ButtonPhase.Down => state.Current && !state.Previous, ButtonPhase.Up => !state.Current && state.Previous, _ => false };
	}

	private static string ResolveDigitalAction(string actionName) {
		if (actionName.Equals("Interact", StringComparison.OrdinalIgnoreCase)) return LeftHanded ? "LeftTrigger" : "RightTrigger";
		if (actionName.Equals("Decline", StringComparison.OrdinalIgnoreCase)) return LeftHanded ? "RightTrigger" : "LeftTrigger";
		return actionName;
	}

	public static Vector2 GetVector2(string actionName) => AnalogStates.TryGetValue(actionName, out var value) ? new Vector2(value.x, value.y) : Vector2.zero;

	public static bool TryGetControllerComponentPose(XRNode node, string componentName, out TrackedPose pose) {
		pose = default;
		if (!IsInitialized || node is not(XRNode.LeftHand or XRNode.RightHand)) return false;

		var system = OpenVR.System;
		var renderModels = OpenVR.RenderModels;
		if (system == null || renderModels == null) return false;

		var role = node == XRNode.LeftHand ? ETrackedControllerRole.LeftHand : ETrackedControllerRole.RightHand;
		var deviceIndex = system.GetTrackedDeviceIndexForControllerRole(role);
		if (deviceIndex == OpenVR.k_unTrackedDeviceIndexInvalid) return false;

		var propertyError = ETrackedPropertyError.TrackedProp_Success;
		var capacity = system.GetStringTrackedDeviceProperty(deviceIndex, ETrackedDeviceProperty.Prop_RenderModelName_String, null, 0, ref propertyError);
		if (capacity == 0) return false;

		var modelName = new StringBuilder((int)capacity);
		system.GetStringTrackedDeviceProperty(deviceIndex, ETrackedDeviceProperty.Prop_RenderModelName_String, modelName, capacity, ref propertyError);
		if (propertyError != ETrackedPropertyError.TrackedProp_Success) return false;

		var controllerState = new RenderModel_ControllerMode_State_t();
		var componentState = new RenderModel_ComponentState_t();
		var devicePath = node == XRNode.LeftHand ? _leftHandHandle : _rightHandHandle;
		if (!renderModels.GetComponentStateForDevicePath(modelName.ToString(), componentName, devicePath, ref controllerState, ref componentState)) return false;

		var matrix = componentState.mTrackingToComponentLocal;
		var position = new Vector3(matrix.m3, matrix.m7, -matrix.m11);
		var forward = new Vector3(-matrix.m2, -matrix.m6, matrix.m10);
		var up = new Vector3(matrix.m1, matrix.m5, -matrix.m9);
		var rotation = forward.sqrMagnitude > 0.0001f && up.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(forward, up) : Quaternion.identity;
		pose = new TrackedPose(position, rotation);
		return true;
	}

	public static void TriggerHaptic(string actionName, XRNode hand, float duration = 0.05f, float frequency = 100f, float amplitude = 0.5f) {
		if (!IsInitialized || !HapticHandles.TryGetValue(actionName, out var handle)) return;

		var handHandle = hand == XRNode.LeftHand ? _leftHandHandle : _rightHandHandle;
		_input.TriggerHapticVibrationAction(handle, 0f, duration, frequency, Mathf.Clamp01(amplitude), handHandle);
	}

	public static void Shutdown() {
		DigitalHandles.Clear();
		AnalogHandles.Clear();
		HapticHandles.Clear();
		DigitalStates.Clear();
		AnalogStates.Clear();
		_activeActionSets = null;
		_input = null;
		_leftHandHandle = 0;
		_rightHandHandle = 0;
		_previousMove = Vector2.zero;
		_currentMove = Vector2.zero;
		MinigameInputAdapter.Reset();
		_sprintToggled = false;
		_previousSprintToggled = false;
		IsInitialized = false;
	}

	private static void RegisterDigital(string name, string path) {
		DigitalHandles.Add(name, GetActionHandle(path));
		DigitalStates.Add(name, default);
	}

	private static void RegisterAnalog(string name, string path) {
		AnalogHandles.Add(name, GetActionHandle(path));
		AnalogStates.Add(name, Vector3.zero);
	}

	private static void RegisterHaptic(string name, string path) {
		HapticHandles.Add(name, GetActionHandle(path));
	}

	private static ulong GetActionHandle(string path) {
		ulong handle = 0;
		Check(_input.GetActionHandle(path, ref handle), $"GetActionHandle({path})");
		return handle;
	}

	private static string ShortName(string path) {
		var separator = path.LastIndexOf('/');
		return separator >= 0 ? path[(separator + 1)..] : path;
	}

	private static void Check(EVRInputError error, string operation) {
		if (error != EVRInputError.None) throw new InvalidOperationException($"SteamVR {operation} failed: {error}");
	}

	private struct DigitalState {
		public bool Current;
		public bool Previous;
	}

	public static bool IsButtonMapped(string buttonName) => IsInitialized && ButtonMap.ContainsKey(buttonName);

	public static bool UsesInteractAction(string buttonName) {
		if (!ButtonMap.TryGetValue(buttonName, out var actions)) return false;
		foreach (var action in actions)
			if (action.Equals("Interact", StringComparison.OrdinalIgnoreCase)) return true;
		return false;
	}

	public static bool IsAxisMapped(string axisName) => IsInitialized && (AxisMap.ContainsKey(axisName) || axisName.Equals("Mouse X", StringComparison.OrdinalIgnoreCase) || axisName.Equals("Mouse Y", StringComparison.OrdinalIgnoreCase));

	public static bool IsMouseButtonMapped(int button) => IsInitialized && MouseButtonMap.ContainsKey(button);

	public static bool GetMappedButton(string buttonName) => ReadMappedButton(buttonName, ButtonPhase.Current);

	public static bool GetMappedButtonDown(string buttonName) => ReadMappedButton(buttonName, ButtonPhase.Down);

	public static bool GetMappedButtonUp(string buttonName) => ReadMappedButton(buttonName, ButtonPhase.Up);

	private static bool ReadMappedButton(string buttonName, ButtonPhase phase) {
		if (!ButtonMap.TryGetValue(buttonName, out var actions)) return false;
		foreach (var action in actions)
			if (ReadButton(action, phase)) return true;

		var current = GetDirectional(buttonName, _currentMove);
		var previous = GetDirectional(buttonName, _previousMove);
		return phase switch { ButtonPhase.Current => current, ButtonPhase.Down => current && !previous, ButtonPhase.Up => !current && previous, _ => false };
	}

	public static float GetMappedAxis(string axisName) {
		if (axisName.Equals("Mouse X", StringComparison.OrdinalIgnoreCase) || axisName.Equals("Mouse Y", StringComparison.OrdinalIgnoreCase)) return MinigameInputAdapter.GetMouseAxis(axisName, _currentMove);

		if (!AxisMap.TryGetValue(axisName, out var mapping)) return 0f;

		var value = GetVector2(mapping.ActionName);
		return mapping.Component == AxisComponent.X ? value.x : value.y;
	}

	public static bool GetMappedMouseButton(int button) => ReadMappedMouseButton(button, ButtonPhase.Current);

	public static bool GetMappedMouseButtonDown(int button) => ReadMappedMouseButton(button, ButtonPhase.Down);

	public static bool GetMappedMouseButtonUp(int button) => ReadMappedMouseButton(button, ButtonPhase.Up);

	private static bool ReadMappedMouseButton(int button, ButtonPhase phase) {
		if (!MouseButtonMap.TryGetValue(button, out var action)) return false;
		var current = GetButton(action) || MinigameInputAdapter.CartridgeStickDrag(_currentMove);
		if (phase == ButtonPhase.Current) return current;
		var previous = GetPreviousButton(action) || MinigameInputAdapter.CartridgeStickDrag(_previousMove);
		return phase == ButtonPhase.Down ? current && !previous : !current && previous;
	}

	private static bool GetPreviousButton(string actionName) => DigitalStates.TryGetValue(ResolveDigitalAction(actionName), out var state) && state.Previous;

	public static VRController GetHand() {
		var vrPlayer = VRPlayer.Instance;
		if (!vrPlayer) return null;

		return LeftHanded ? vrPlayer.leftController : vrPlayer.rightController;
	}

	private static bool GetDirectional(string name, Vector2 axis) {
		const float threshold = 0.65f;
		return name switch { "Left" => axis.x<-threshold, "Right" => axis.x> threshold, "Up" => axis.y > threshold, "Down" => axis.y < -threshold,
			_ => false };
	}
}

public readonly struct TrackedPose {
	public TrackedPose(Vector3 position, Quaternion rotation) {
		Position = position;
		Rotation = rotation;
	}

	public Vector3 Position { get; }
	public Quaternion Rotation { get; }
}
