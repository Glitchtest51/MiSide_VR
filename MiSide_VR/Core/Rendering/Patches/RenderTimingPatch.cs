using System.Reflection;
using HarmonyLib;

namespace MiSide_VR.Core.Rendering.Patches;

// Application.onBeforeRender is stripped so this lets me use it
[HarmonyPatch]
static class RenderTimingPatch {
	private static MethodBase TargetMethod() {
		var helper = AccessTools.TypeByName("UnityEngine.BeforeRenderHelper");
		return helper == null ? null : AccessTools.Method(helper, "Invoke");
	}

	private static void Prefix() { VRPlayer.Instance?.ApplyBeforeRenderPose(); }
}
