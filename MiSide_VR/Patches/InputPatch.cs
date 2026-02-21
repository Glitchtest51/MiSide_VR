using HarmonyLib;
using MiSide_VR.VRInput;
using UnityEngine;

namespace MiSide_VR.Patches;

[HarmonyPatch]
public static class InputPatch {
	[HarmonyPatch(typeof(Input), nameof(Input.GetButton))]
	[HarmonyPrefix]
	public static bool HkGetButton(string buttonName, ref bool __result) {
		__result = VRInputManager.GetMappedButton(buttonName);

		return false;
	}

	[HarmonyPatch(typeof(Input), nameof(Input.GetButtonDown))]
	[HarmonyPrefix]
	public static bool HkGetButtonDown(string buttonName, ref bool __result) {
		__result = VRInputManager.GetMappedButtonDown(buttonName);

		return false;
	}

	[HarmonyPatch(typeof(Input), nameof(Input.GetButtonUp))]
	[HarmonyPrefix]
	public static bool HkGetButtonUp(string buttonName, ref bool __result) {
		__result = VRInputManager.GetMappedButtonUp(buttonName);

		return false;
	}

	[HarmonyPatch(typeof(Input), nameof(Input.GetAxis))]
	[HarmonyPrefix]
	public static bool HkGetAxis(string axisName, ref float __result) {
		__result = VRInputManager.GetMappedAxis(axisName);

		return false;
	}

	[HarmonyPatch(typeof(Input), nameof(Input.GetMouseButton))]
	[HarmonyPrefix]
	public static bool HkGetMouseButton(int button, ref bool __result) {
		__result = VRInputManager.GetMappedMouseButton(button);

		return false;
	}

	[HarmonyPatch(typeof(Input), nameof(Input.GetMouseButtonDown))]
	[HarmonyPrefix]
	public static bool HkGetMouseButtonDown(int button, ref bool __result) {
		__result = VRInputManager.GetMappedMouseButtonDown(button);

		return false;
	}

	[HarmonyPatch(typeof(Input), nameof(Input.GetMouseButtonUp))]
	[HarmonyPrefix]
	public static bool HkGetMouseButtonUp(int button, ref bool __result) {
		__result = VRInputManager.GetMappedMouseButtonUp(button);

		return false;
	}
}
