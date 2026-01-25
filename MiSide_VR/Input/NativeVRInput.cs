using System.Collections.Generic;
using Valve.VR;
using System.Runtime.InteropServices;
using UnityEngine;
using static MiSide_VR.Plugin;

namespace MiSide_VR.Input;

public static class NativeVRInput
{
    private static Dictionary<string, ulong> actionHandles = new Dictionary<string, ulong>();

    // Handle for the default action set ("/actions/default")
    private static ulong defaultSetHandle;

    // Cached struct sizes for efficient marshalling with OpenVR API
    private static readonly uint sizeDigital = (uint)Marshal.SizeOf(typeof(InputDigitalActionData_t));
    private static readonly uint sizeAnalog = (uint)Marshal.SizeOf(typeof(InputAnalogActionData_t));
    private static readonly uint sizePose = (uint)Marshal.SizeOf(typeof(InputPoseActionData_t));

    // Velocity tracking buffers for smooth hand velocity estimation
    private static readonly Dictionary<string, Vector3> _prevPositions = new Dictionary<string, Vector3>();
    private static readonly Dictionary<string, Vector3> _velocities = new Dictionary<string, Vector3>();

    /// <summary>
    /// Initializes OpenVR input by registering all required action handles from the "/actions/default" set.
    /// Must be called once during mod startup (e.g., in MelonMod.OnInitializeMelon).
    /// </summary>
    public static void Initialize()
    {
        Log.LogInfo("[NativeVR] Pre-initializing raw OpenVR action handles...");

        // Get handle for the default action set
        OpenVR.Input.GetActionSetHandle("/actions/default", ref defaultSetHandle);

        // --- RIGHT CONTROLLER ---
        RegisterHandle("ButtonRight_A_Touch");
        RegisterHandle("ButtonRight_A_Press");

        RegisterHandle("ButtonRight_B_Touch");
        RegisterHandle("ButtonRight_B_Press");

        RegisterHandle("ButtonRight_System_Press");

        RegisterHandle("ThumbstickRight");
        RegisterHandle("ThumbstickRight_Touch");
        RegisterHandle("ThumbstickRight_Press");

        RegisterHandle("GripRight_Press");
        RegisterHandle("GripRight_Value");

        RegisterHandle("TriggerRight_Touch");
        RegisterHandle("TriggerRight_Press");
        RegisterHandle("TriggerRight_Value");

        // --- LEFT CONTROLLER ---
        RegisterHandle("ButtonLeft_X_Touch");
        RegisterHandle("ButtonLeft_X_Press");

        RegisterHandle("ButtonLeft_Y_Touch");
        RegisterHandle("ButtonLeft_Y_Press");

        RegisterHandle("ButtonLeft_Menu_Press");

        RegisterHandle("ThumbstickLeft");
        RegisterHandle("ThumbstickLeft_Touch");
        RegisterHandle("ThumbstickLeft_Press");

        RegisterHandle("GripLeft_Press");
        RegisterHandle("GripLeft_Value");

        RegisterHandle("TriggerLeft_Touch");
        RegisterHandle("TriggerLeft_Press");
        RegisterHandle("TriggerLeft_Value");

        // --- HAND POSES ---
        RegisterHandle("PoseRight");
        RegisterHandle("PoseLeft");

        // Initialize velocity tracking buffers to avoid null checks during runtime
        _prevPositions["PoseLeft"] = Vector3.zero;
        _prevPositions["PoseRight"] = Vector3.zero;
        _velocities["PoseLeft"] = Vector3.zero;
        _velocities["PoseRight"] = Vector3.zero;
    }

    /// <summary>
    /// Registers an OpenVR action by name and caches its handle for fast access.
    /// </summary>
    /// <param name="actionName">The name of the action as defined in the action manifest under "/actions/default/in/".</param>
    private static void RegisterHandle(string actionName)
    {
        ulong handle = 0;
        EVRInputError error = OpenVR.Input.GetActionHandle($"/actions/default/in/{actionName}", ref handle);

        if (error == EVRInputError.None)
        {
            actionHandles[actionName] = handle;
            Log.LogInfo($"[NativeVR] Action '{actionName}' registered with handle: {handle}");
        }
        else
        {
            Log.LogError($"[NativeVR] Failed to register action '{actionName}': {error}");
        }
    }

    /// <summary>
    /// Updates the current state of all registered OpenVR actions.
    /// Must be called every frame (e.g., in OnUpdate).
    /// Also updates hand velocity estimates based on pose changes.
    /// </summary>
    public static void Update()
    {
        // Activate the default action set for this frame
        VRActiveActionSet_t activeSet = new VRActiveActionSet_t
        {
            ulActionSet = defaultSetHandle,
            ulRestrictedToDevice = OpenVR.k_ulInvalidInputValueHandle,
            nPriority = 0
        };

        // Inform OpenVR which action sets are active this frame
        OpenVR.Input.UpdateActionState(
            new[] { activeSet },
            (uint)Marshal.SizeOf(typeof(VRActiveActionSet_t))
        );

        // Update hand velocity estimates for both controllers
        UpdateVelocityInternal("PoseLeft");
        UpdateVelocityInternal("PoseRight");
    }

    // --- DIGITAL INPUT (BUTTONS) ---

    /// <summary>
    /// Reads the current state of a digital (boolean) input action, such as a button press.
    /// </summary>
    /// <param name="actionName">Name of the registered digital action (e.g., "ButtonRight_A_Press").</param>
    /// <returns>True if the button is currently pressed and active; otherwise, false.</returns>
    public static bool GetBoolean(string actionName)
    {
        if (!actionHandles.TryGetValue(actionName, out ulong handle))
            return false;

        InputDigitalActionData_t data = new InputDigitalActionData_t();
        OpenVR.Input.GetDigitalActionData(handle, ref data, sizeDigital, 0);
        return data.bActive && data.bState;
    }

    // --- ANALOG INPUT (TRIGGERS, GRIPS) ---

    /// <summary>
    /// Reads the current value of an analog input action (e.g., trigger pull or grip squeeze).
    /// </summary>
    /// <param name="actionName">Name of the registered analog action (e.g., "TriggerRight_Value").</param>
    /// <returns>Normalized value [0.0f, 1.0f] if active; otherwise, 0.0f.</returns>
    public static float GetFloat(string actionName)
    {
        if (!actionHandles.TryGetValue(actionName, out ulong handle))
            return 0f;

        InputAnalogActionData_t data = new InputAnalogActionData_t();
        OpenVR.Input.GetAnalogActionData(handle, ref data, sizeAnalog, 0);
        return data.bActive ? data.x : 0f;
    }

    // --- 2D ANALOG INPUT (THUMBSTICKS) ---

    /// <summary>
    /// Reads a 2D vector from an analog action, typically used for thumbsticks.
    /// </summary>
    /// <param name="actionName">Name of the registered vector action (e.g., "ThumbstickLeft").</param>
    /// <returns>Vector2 with X and Y components if active; otherwise, Vector2.zero.</returns>
    public static UnityEngine.Vector2 GetVector2(string actionName)
    {
        if (!actionHandles.TryGetValue(actionName, out ulong handle))
            return UnityEngine.Vector2.zero;

        InputAnalogActionData_t data = new InputAnalogActionData_t();
        OpenVR.Input.GetAnalogActionData(handle, ref data, sizeAnalog, 0);
        return data.bActive ? new UnityEngine.Vector2(data.x, data.y) : UnityEngine.Vector2.zero;
    }

    // --- POSE INPUT (HAND TRACKING) ---

    /// <summary>
    /// Retrieves the current 6-DOF pose (position + rotation) of a tracked device (e.g., controller).
    /// Uses raw, uncalibrated tracking space for minimal latency.
    /// </summary>
    /// <param name="actionName">Name of the registered pose action (e.g., "PoseRight").</param>
    /// <returns>A Pose struct containing position and rotation. Returns identity if pose is invalid or missing.</returns>
    public static Pose GetPose(string actionName)
    {
        // Return a safe default if the action wasn't registered
        if (!actionHandles.TryGetValue(actionName, out ulong handle))
        {
            Pose RetPose = new Pose();
            RetPose.position = Vector3.zero;
            RetPose.rotation = Quaternion.identity;
            return RetPose;
        }

        InputPoseActionData_t data = new InputPoseActionData_t();

        EVRInputError error = OpenVR.Input.GetPoseActionDataRelativeToNow(
            handle,
            ETrackingUniverseOrigin.TrackingUniverseRawAndUncalibrated,
            0f,
            ref data,
            sizePose,
            OpenVR.k_ulInvalidInputValueHandle
        );

        // Return safe default if pose data is invalid or inactive
        if (error != EVRInputError.None || !data.bActive || !data.pose.bPoseIsValid)
        {
            Pose RetPose = new Pose();
            RetPose.position = Vector3.zero;
            RetPose.rotation = Quaternion.identity;
            return RetPose;
        }

        var m = data.pose.mDeviceToAbsoluteTracking;

        // Extract position (note: OpenVR uses Z-forward, Unity uses Z-backward)
        Vector3 position = new Vector3(m.m3, m.m7, -m.m11);

        // Extract forward and up vectors for rotation
        Vector3 forward = new Vector3(m.m2, m.m6, -m.m10);
        Vector3 up = new Vector3(m.m1, m.m5, -m.m9);

        // Safely compute rotation using LookRotation; fallback to identity on failure
        Quaternion rotation = Quaternion.identity;
        if (forward.sqrMagnitude > 0.001f)
        {
            try
            {
                rotation = Quaternion.LookRotation(forward, up);
            }
            catch
            {
                // Keep identity rotation if LookRotation fails (e.g., degenerate vectors)
            }
        }

        Pose _rPose = new Pose();
        _rPose.position = position;
        _rPose.rotation = rotation;
        return _rPose;
    }

    // --- HAND VELOCITY ESTIMATION ---

    /// <summary>
    /// Internally updates the estimated velocity of a hand by comparing its current and previous positions.
    /// Applies a simple low-pass filter to reduce tracking noise.
    /// </summary>
    /// <param name="handleName">The pose action name (e.g., "PoseLeft").</param>
    private static void UpdateVelocityInternal(string handleName)
    {
        Vector3 currentPos = GetPose(handleName).position;

        if (Time.deltaTime > 0)
        {
            // Compute raw instantaneous velocity
            Vector3 rawVelocity = (currentPos - _prevPositions[handleName]) / Time.deltaTime;

            // Apply smoothing (60% weight to new sample) to reduce jitter
            _velocities[handleName] = Vector3.Lerp(_velocities[handleName], rawVelocity, 0.6f);
        }

        // Store current position for next frame
        _prevPositions[handleName] = currentPos;
    }

    /// <summary>
    /// Returns the smoothed estimated velocity of a hand (in meters per second).
    /// </summary>
    /// <param name="actionName">The pose action name (e.g., "PoseRight").</param>
    /// <returns>Velocity vector in world space, or Vector3.zero if not tracked.</returns>
    public static Vector3 GetHandVelocity(string actionName)
    {
        return _velocities.TryGetValue(actionName, out Vector3 velocity) ? velocity : Vector3.zero;
    }
}