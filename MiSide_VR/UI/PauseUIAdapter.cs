using MiSide_VR.UI.Patches;
using UnityEngine;

namespace MiSide_VR.UI;

static class PauseUIAdapter {
	internal static void Synchronize() {
		var menu = GameObject.Find("FastMenu") ?? GameObject.Find("FastMenu(Clone)");
		if (!menu) return;
		var transform = menu.transform;
		
		var canvas = menu.GetOrAddComponent<Canvas>();
		canvas.overrideSorting = true;
		canvas.sortingOrder = 32760;
		menu.GetOrAddComponent<UnityEngine.UI.GraphicRaycaster>();
		CanvasPatch.SetLayerRecursively(transform, CanvasPatch.SourceUiLayer);
		transform.SetAsLastSibling();
	}
}
