using HarmonyLib;

namespace MiSide_VR.Core.Patches;

[HarmonyPatch(typeof(PlayerMove), "FixedUpdate")]
public static class PlayerRoomscalePatch {
	[HarmonyPostfix]
	[HarmonyPriority(Priority.Last)]
	public static void Postfix(PlayerMove __instance) { VRPlayer.Instance?.ApplyRoomscaleMovement(__instance); }
}
