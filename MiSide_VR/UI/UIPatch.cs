using System.Linq;
using HarmonyLib;
using Il2CppSystem.Collections.Generic;
using MiSide_VR.VR;
using MiSide_VR.VRInput;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static MiSide_VR.Plugin;

namespace MiSide_VR.UI;

[HarmonyPatch(typeof(CanvasScaler), "OnEnable")]
internal class UIPatch {
    private static readonly HashSet<Canvas> ProcessedCanvases = new();

    private const bool LeftHanded = false;
    private static GameObject _cachedEventSystem;

    public static void Postfix(CanvasScaler __instance) {
        var vrPlayer = VRPlayer.Instance;
        if (!VREnabled) return;
        var canvas = __instance.GetComponent<Canvas>();
        if (!canvas) return;
        if (ProcessedCanvases.Contains(canvas)) return;
        if (CanvasesToIgnore.Contains(canvas.name)) return;
        if (!vrPlayer) return;
        
        if (!_cachedEventSystem) 
            SetupEventSystemForVR();
        
        if (!_cachedEventSystem) return;
        
        ProcessedCanvases.Add(canvas);
        
        var hand = LeftHanded ? vrPlayer.Left : vrPlayer.Right;
        canvas.worldCamera = hand.eventCamera;
        
        if (canvas.renderMode == RenderMode.WorldSpace) return;
        
        canvas.renderMode = RenderMode.WorldSpace;
        // switch (canvas.gameObject.scene.name) {
        //     case "SceneAihasto":
        //         canvas.transform.localScale = new Vector3(0.002f, 0.002f, 0.002f);
        //         canvas.transform.localPosition = new Vector3(12.3258f, 1.8956f, 3.7663f);
        //         canvas.transform.localRotation = Quaternion.Euler(0f, -90f, 0f);
        //         return;
        //     case "SceneLoading":
        //         canvas.transform.localScale = new Vector3(0.002f, 0.002f, 0.002f);
        //         canvas.transform.localPosition = new Vector3(12.3258f, 1.8956f, 3.7663f);
        //         canvas.transform.localRotation = Quaternion.Euler(0f, -90f, 0f);
        //         return;
        //     case "SceneMenu":
        //         canvas.transform.localScale = new Vector3(0.002f, 0.002f, 0.002f);
        //         canvas.transform.localPosition = new Vector3(12.3258f, 1.8956f, 3.7663f);
        //         canvas.transform.localRotation = Quaternion.Euler(0f, -90f, 0f);
        //         return;
        // }
          
        canvas.transform.localScale = Vector3.one * 0.0005f;
        canvas.gameObject.AddComponent<UIFollowCamera>();        
    }
    
    private static void SetupEventSystemForVR() {
        var vrPlayer = VRPlayer.Instance;
        if (!vrPlayer) return;
        
        if (_cachedEventSystem == null || !_cachedEventSystem.activeInHierarchy)
            _cachedEventSystem = FindEventSystem();
    
        if (!_cachedEventSystem) return;

        var eventSystemComponent = _cachedEventSystem.GetComponent<EventSystem>();
        var inputModule = _cachedEventSystem.GetComponent<StandaloneInputModule>();
        
        if (!eventSystemComponent || !inputModule) return;
        
        var hand = LeftHanded ? vrPlayer.Left : vrPlayer.Right;
        hand.SetupEventSystem(eventSystemComponent, inputModule);
        
        var vrPointerInput = _cachedEventSystem.GetOrAddComponent<VRPointerInput>();
        vrPointerInput.eventCamera = hand.eventCamera;
        
        hand.SetUIMode(true);
    }
    
    private static GameObject FindEventSystem() {
        var gameObj = GameObject.Find("Game");
        if (gameObj) {
            var eventSystemGO = gameObj.transform.Find("ConsoleCall/EventSystem")?.gameObject;
            if (eventSystemGO) return eventSystemGO;
        }
        
        var universeLib = GameObject.Find("UniverseLibCanvas");
        if (universeLib) {
            var eventSystem = universeLib.GetComponent<EventSystem>();
            if (eventSystem) return eventSystem.gameObject;
        }
        
        var foundEventSystem = UnityEngine.Object.FindObjectOfType<EventSystem>();
        return foundEventSystem ? foundEventSystem.gameObject : null;
    }
    
    private static readonly string[] CanvasesToIgnore = [
        "com.sinai.unityexplorer_Root",
        "com.sinai.unityexplorer.MouseInspector_Root",
        "ExplorerCanvas",
        "HudUI"
    ];
}