using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Valve.VR;
using static MiSide_VR.Plugin;

namespace MiSide_VR.VRInput;

public static class VRInputManager {
    private static readonly Dictionary<string, Vector3> PrevPositions = new();
    private static readonly Dictionary<string, Vector3> Velocities = new();
    
    private static readonly Dictionary<string, ISteamVR_Action> Actions = new();
    
    private static readonly Dictionary<string, string[]> ButtonMap = new();
    private static readonly Dictionary<string, AxisMapping> AxisMap = new();
    private static readonly Dictionary<int, string> MouseButtonMap = new();
    
    private struct AxisMapping(string actionName, AxisComponent component) {
        public readonly string ActionName = actionName;
        public readonly AxisComponent Component = component;
    }
    
    private enum AxisComponent { X, Y }
    
    public static void Initialize() {
        Log.LogInfo("[VRInput] Initializing SteamVR Actions...");
        
        RegisterAction("Move", SteamVR_Actions.Gameplay.Move);
        RegisterAction("Turn", SteamVR_Actions.Gameplay.Turn);
        RegisterAction("Sprint", SteamVR_Actions.Gameplay.Sprint);
        RegisterAction("SnapTurnLeft", SteamVR_Actions.Gameplay.SnapTurnLeft);
        RegisterAction("SnapTurnRight", SteamVR_Actions.Gameplay.SnapTurnRight);
        RegisterAction("Interact", SteamVR_Actions.Gameplay.Interact);
        RegisterAction("Menu", SteamVR_Actions.Gameplay.Menu);
        RegisterAction("GrabLeft", SteamVR_Actions.Gameplay.GrabLeft);
        RegisterAction("GrabRight", SteamVR_Actions.Gameplay.GrabRight);
        RegisterAction("Pose", SteamVR_Actions.Gameplay.Pose);
        RegisterAction("SkeletonLeftHand", SteamVR_Actions.Gameplay.SkeletonLeftHand);
        RegisterAction("SkeletonRightHand", SteamVR_Actions.Gameplay.SkeletonRightHand);
        RegisterAction("Haptic", SteamVR_Actions.Gameplay.Haptic);
        
        PrevPositions["Pose"] = Vector3.zero;
        Velocities["Pose"] = Vector3.zero;
        
        // ButtonMap.Add("Interactive", ["Interact"]);
        // ButtonMap.Add("Jump", ["Interact"]);
        // ButtonMap.Add("MouseClick", ["Interact"]);
        // ButtonMap.Add("Submit", ["Interact"]);
        // ButtonMap.Add("Cancel", ["Menu"]);
        // ButtonMap.Add("Shift", ["Sprint"]);

        AxisMap.Add("Horizontal", new AxisMapping("Move", AxisComponent.X));
        AxisMap.Add("Vertical", new AxisMapping("Move", AxisComponent.Y));
        
        MouseButtonMap.Add(0, "Interact");
        
        Log.LogInfo("[VRInput] SteamVR Actions initialized");
    }
    
    private static void RegisterAction(string name, ISteamVR_Action action) {
        Actions[name] = action;
    }
    
    public static void UpdateInput() {
        UpdateHandVelocity("Pose");
    }
    
    private static T GetAction<T>(string actionName) where T : class, ISteamVR_Action {
        return Actions.TryGetValue(actionName, out var action) ? action as T : null;
    }
    
    public static bool GetButton(string actionName) {
        var action = GetAction<SteamVR_Action_Boolean>(actionName);
        return action != null && action.state;
    }
    
    public static bool GetButtonDown(string actionName) {
        var action = GetAction<SteamVR_Action_Boolean>(actionName);
        return action != null && action.stateDown;
    }
    
    public static bool GetButtonUp(string actionName) {
        var action = GetAction<SteamVR_Action_Boolean>(actionName);
        return action != null && action.stateUp;
    }
    
    public static float GetAnalog(string actionName) {
        var action = GetAction<SteamVR_Action_Vector2>(actionName);
        return action?.axis.x ?? 0f;
    }
    
    public static Vector2 GetThumbstick(string actionName) {
        var action = GetAction<SteamVR_Action_Vector2>(actionName);
        return action?.axis ?? Vector2.zero;
    }
    
    public static Pose GetPose(string actionName, SteamVR_Input_Sources inputSource = SteamVR_Input_Sources.Any) {
        var action = GetAction<SteamVR_Action_Pose>(actionName);
        if (action == null) {
            return new Pose {
                position = Vector3.zero,
                rotation = Quaternion.identity
            };
        }

        return new Pose {
            position = action.GetLocalPosition(inputSource),
            rotation = action.GetLocalRotation(inputSource)
        };
    }
    
    private static void UpdateHandVelocity(string handleName) {
        var currentPos = GetPose(handleName).position;

        if (Time.deltaTime > 0) {
            var rawVelocity = (currentPos - PrevPositions[handleName]) / Time.deltaTime;
            Velocities[handleName] = Vector3.Lerp(Velocities[handleName], rawVelocity, 0.6f);
        }
        
        PrevPositions[handleName] = currentPos;
    }
    
    public static Vector3 GetHandVelocity(string actionName) {
        return Velocities.TryGetValue(actionName, out var velocity) ? velocity : Vector3.zero;
    }
    
    public static bool GetMappedButton(string buttonName) {
        if (!ButtonMap.TryGetValue(buttonName, out var actionNames)) return false;
        return actionNames.Any(GetButton);
    }
    
    public static bool GetMappedButtonDown(string buttonName) {
        if (!ButtonMap.TryGetValue(buttonName, out var actionNames)) return false;
        return actionNames.Any(GetButtonDown);
    }
    
    public static bool GetMappedButtonUp(string buttonName) {
        if (!ButtonMap.TryGetValue(buttonName, out var actionNames)) return false;
        return actionNames.Any(GetButtonUp);
    }
    
    public static float GetMappedAxis(string axisName) {
        if (!AxisMap.TryGetValue(axisName, out var mapping)) return 0f;
        
        var action = GetAction<SteamVR_Action_Vector2>(mapping.ActionName);
        if (action == null) return 0f;
        
        return mapping.Component == AxisComponent.X ? action.axis.x : action.axis.y;
    }
    
    public static bool GetMappedMouseButton(int button) {
        if (!MouseButtonMap.TryGetValue(button, out var actionName)) return false;
        return GetButton(actionName);
    }
    
    public static bool GetMappedMouseButtonDown(int button) {
        if (!MouseButtonMap.TryGetValue(button, out var actionName)) return false;
        return GetButtonDown(actionName);
    }
    
    public static bool GetMappedMouseButtonUp(int button) {
        if (!MouseButtonMap.TryGetValue(button, out var actionName)) return false;
        return GetButtonUp(actionName);
    }
}