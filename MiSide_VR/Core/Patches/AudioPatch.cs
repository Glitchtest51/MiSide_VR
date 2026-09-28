using HarmonyLib;

namespace MiSide_VR.Core.Patches;

// some bug, idk if this was caused by my mod or is just in the game but i added a patch anyways
[HarmonyPatch(typeof(Audio_BlendShapeVoice), "LateUpdate")]
static class AudioBlendShapeVoicePatch {
	[HarmonyPrefix]
	private static bool HasValidVoiceTarget(Audio_BlendShapeVoice __instance) { return __instance && __instance.mesh && __instance.mesh.sharedMesh && __instance.audioVoice && __instance.audioVoice.clip; }
}
