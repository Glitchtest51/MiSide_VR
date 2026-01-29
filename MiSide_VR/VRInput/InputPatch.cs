using HarmonyLib;
using UnityEngine;

namespace MiSide_VR.VRInput;

[HarmonyPatch]
public static class InputPatches {
    [HarmonyPatch(typeof(Input), nameof(Input.GetButton))]
    [HarmonyPrefix]
    public static bool GetButton_Prefix(string buttonName, ref bool __result) {
        __result = VRInputManager.GetMappedButton(buttonName);
        return false;
    }

    [HarmonyPatch(typeof(Input), nameof(Input.GetButtonDown))]
    [HarmonyPrefix]
    public static bool GetButtonDown_Prefix(string buttonName, ref bool __result) {
        __result = VRInputManager.GetMappedButtonDown(buttonName);
        return false;
    }

    [HarmonyPatch(typeof(Input), nameof(Input.GetButtonUp))]
    [HarmonyPrefix]
    public static bool GetButtonUp_Prefix(string buttonName, ref bool __result) {
        __result = VRInputManager.GetMappedButtonUp(buttonName);
        return false;
    }

    [HarmonyPatch(typeof(Input), nameof(Input.GetAxis))]
    [HarmonyPrefix]
    public static bool GetAxis_Prefix(string axisName, ref float __result) {
        __result = VRInputManager.GetMappedAxis(axisName);
        return false;
    }

    [HarmonyPatch(typeof(Input), nameof(Input.GetMouseButton))]
    [HarmonyPrefix]
    public static bool GetMouseButton_Prefix(int button, ref bool __result) {
        __result = VRInputManager.GetMappedMouseButton(button);
        return false;
    }

    [HarmonyPatch(typeof(Input), nameof(Input.GetMouseButtonDown))]
    [HarmonyPrefix]
    public static bool GetMouseButtonDown_Prefix(int button, ref bool __result) {
        __result = VRInputManager.GetMappedMouseButtonDown(button);
        return false;
    }

    [HarmonyPatch(typeof(Input), nameof(Input.GetMouseButtonUp))]
    [HarmonyPrefix]
    public static bool GetMouseButtonUp_Prefix(int button, ref bool __result) {
        __result = VRInputManager.GetMappedMouseButtonUp(button);
        return false;
    }
}