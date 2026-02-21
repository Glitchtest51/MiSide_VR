using System.Linq;
using HarmonyLib;
using Il2CppSystem.Collections.Generic;
using MiSide_VR.Core;
using MiSide_VR.UI;
using MiSide_VR.VRInput;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static MiSide_VR.Plugin;

namespace MiSide_VR.Patches;

[HarmonyPatch(typeof(CanvasScaler), "OnEnable")]
class CanvasPatch {
	private static readonly HashSet<Canvas> ProcessedCanvases = new();

	public static GameObject CachedEventSystem;

	public static void Postfix(CanvasScaler __instance) {
		var canvas = __instance.GetComponent<Canvas>();

		if (!VRPlayer.Instance) return;
		if (!VREnabled) return;
		if (!canvas) return;
		if (ProcessedCanvases.Contains(canvas)) return;
		if (CanvasesToIgnore.Contains(canvas.name)) return;

		Transform current = canvas.transform;
		
		var done = false;
		while (current.parent && !done) {
			switch (current.name) {
				case "CutScenes": 
					done = true; 
					break;
				case "World":
				case "House": 
					return;
			}
    
			if (!done) {
				current = current.parent;
			}
		}

		if (!CachedEventSystem) SetupVREventSystem();

		if (!CachedEventSystem) return;

		ProcessedCanvases.Add(canvas);

		VRController hand = VRInputManager.GetHand();
		canvas.worldCamera = hand.eventCamera;

		if (canvas.renderMode == RenderMode.WorldSpace) return;

		canvas.renderMode = RenderMode.WorldSpace;

		switch (canvas.gameObject.scene.name) {
		    case "SceneAihasto":
			    if (Camera.main != null) {
				    Transform playerCamera = Camera.main.transform;
				    canvas.transform.position = playerCamera.position + playerCamera.forward * 2f;
				    canvas.transform.rotation = Quaternion.LookRotation(canvas.transform.position - playerCamera.position);
				    canvas.transform.localScale = Vector3.one * 0.002f;
			    }
			    return;
		    case "SceneLoading":
			    if (Camera.main != null) {
				    Transform playerCamera = Camera.main.transform;
				    canvas.transform.position = playerCamera.position + playerCamera.forward * 2f;
				    canvas.transform.rotation = Quaternion.LookRotation(canvas.transform.position - playerCamera.position);
				    canvas.transform.localScale = Vector3.one * 0.002f;
			    }
			    return;
		    // case "SceneMenu":
		    //     canvas.transform.localScale = new Vector3(0.002f, 0.002f, 0.002f);
		    //     canvas.transform.localPosition = new Vector3(12.3258f, 1.8956f, 3.7663f);
		    //     canvas.transform.localRotation = Quaternion.Euler(0f, -90f, 0f);
		    //     return;
		}

		canvas.transform.localScale = Vector3.one * 0.0005f;
		canvas.gameObject.AddComponent<UIFollowCamera>();
	}

	public static void SetupVREventSystem() {
		if (!VRPlayer.Instance) return;

		if (!CachedEventSystem || !CachedEventSystem.activeInHierarchy)
			CachedEventSystem = FindEventSystem();

		if (!CachedEventSystem) return;

		var eventSystemComponent = CachedEventSystem.GetComponent<EventSystem>();
		var inputModule = CachedEventSystem.GetComponent<StandaloneInputModule>();

		if (!eventSystemComponent || !inputModule) return;

		VRController hand = VRInputManager.GetHand();
		hand.SetupEventSystem(eventSystemComponent, inputModule);

		var vrPointerInput = CachedEventSystem.GetOrAddComponent<VRPointerInput>();
		vrPointerInput.eventCamera = hand.eventCamera;
		inputModule.inputOverride = vrPointerInput;

		hand.uiMode = true;
	}

	private static GameObject FindEventSystem() {
		GameObject gameObj = GameObject.Find("Game");

		if (gameObj) {
			GameObject eventSystem = gameObj.transform.Find("ConsoleCall/EventSystem")?.gameObject;

			if (eventSystem) return eventSystem;
		}

		GameObject universeLib = GameObject.Find("UniverseLibCanvas");

		if (universeLib) {
			var eventSystem = universeLib.GetComponent<EventSystem>();

			if (eventSystem) return eventSystem.gameObject;
		}

		var foundEventSystem = Object.FindObjectOfType<EventSystem>();

		return foundEventSystem ? foundEventSystem.gameObject : null;
	}

	private static readonly string[] CanvasesToIgnore = [
		// "com.sinai.unityexplorer_Root",
		// "com.sinai.unityexplorer.MouseInspector_Root",
		// "ExplorerCanvas",
		// "HudUI"
	];
}
