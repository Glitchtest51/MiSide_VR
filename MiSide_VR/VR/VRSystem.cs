using System;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;
using UnityEngine.SceneManagement;
using static MiSide_VR.Plugin;

namespace MiSide_VR.VR;

public class VRSystem : MonoBehaviour {
    public VRSystem(IntPtr value) : base(value) { }
    
    public static VRSystem Instance { get; private set; }
    
    private int _frameCounter;
    
    private Camera _lastCamera;
    
    private string _lastSceneName = "";
    
    private bool _rigCreated;
    
    private void Awake() {
        Log.LogInfo("[VRSystem] VRSystem Created.");
        
        if (Instance) {
            Log.LogWarning("[VRSystem] Duplicate VRSystem detected, destroying duplicate.");
            Destroy(gameObject);
            enabled = false;
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        
        onSceneLoaded += OnSceneLoaded;
    }
    
    private void LateUpdate() {
        _frameCounter++;
        
        if (_frameCounter >= 50) {
            _frameCounter = 0;

            UpdateActiveCamera();
        }
    }

    private void UpdateActiveCamera() {
        var sceneAndCamera = FindActiveSceneAndCamera();
            
        var activeCamera = sceneAndCamera.Camera;
        var activeScene = sceneAndCamera.Scene;
        if (activeCamera) {
            if (activeScene.name != _lastSceneName || !_lastCamera || activeCamera != _lastCamera) {
                Log.LogWarning($"[VRSystem] Scene or camera changed, respawning VR player rig...");
                _lastSceneName = activeScene.name;
                _lastCamera = activeCamera;
                    
                if (!_rigCreated) 
                    CreateCameraRig(_lastCamera);

                var stereoRender = VRPlayer.Instance?.StereoRender;
                if (!stereoRender) return;
                    
                CopyCameraData(_lastCamera, stereoRender.headCamera);
                CopyCameraData(_lastCamera, stereoRender.leftCamera);
                CopyCameraData(_lastCamera, stereoRender.rightCamera);
            } else if (VRPlayer.Instance) 
                VRPlayer.Instance.SetSceneAndCamera(sceneAndCamera);
        } else Log.LogInfo($"[VRSystem] No active camera found in scene: {activeScene.name}");
    }
    
    private void CopyCameraData(Camera source, Camera target) {
        if (!source || !target)
            return;
        
        var mirrorLayer = LayerMask.NameToLayer("ForMirror");
        var playerLayer = LayerMask.NameToLayer("Player");
        var uiLayer = LayerMask.NameToLayer("UI");
        
        target.clearFlags = source.clearFlags;
        target.backgroundColor = source.backgroundColor;
        target.orthographic = source.orthographic;
        target.orthographicSize = source.orthographicSize;
        target.fieldOfView = source.fieldOfView;
        target.nearClipPlane = source.nearClipPlane;
        target.farClipPlane = source.farClipPlane;
        target.cullingMask = source.cullingMask;
        target.depth = source.depth;
        target.renderingPath = source.renderingPath;
        target.allowHDR = source.allowHDR;
        target.allowMSAA = source.allowMSAA;
        if ((target.cullingMask & (1 << mirrorLayer)) != 0) 
            target.cullingMask &= ~(1 << mirrorLayer);
        if ((target.cullingMask & (1 << playerLayer)) == 0) 
            target.cullingMask |= 1 << playerLayer;
        if ((target.cullingMask & (1 << uiLayer)) == 0) 
            target.cullingMask |= 1 << uiLayer;
        
        var sourcePpLayer = source.GetComponent<PostProcessLayer>();
        if (!sourcePpLayer) {
            Log.LogWarning($"[VRSystem] No PostProcessLayer found in Camera: {source.name}, Tag: {source.tag}, Scene: {source.scene.name}.");
            return;
        }

        var targetPpLayer = target.gameObject.GetOrAddComponent<PostProcessLayer>();
        targetPpLayer.m_Resources = sourcePpLayer.m_Resources;
        targetPpLayer.volumeLayer = sourcePpLayer.volumeLayer;
        targetPpLayer.antialiasingMode = sourcePpLayer.antialiasingMode;
        targetPpLayer.stopNaNPropagation = sourcePpLayer.stopNaNPropagation;
        targetPpLayer.finalBlitToCameraTarget = sourcePpLayer.finalBlitToCameraTarget;
        targetPpLayer.volumeTrigger = target.transform;
    }

    private static ActiveSceneAndCamera FindActiveSceneAndCamera() {
        var result = new ActiveSceneAndCamera();
        var cam = Camera.main;

        if (!cam) {
            Camera[] cameras = FindObjectsOfType<Camera>(true);
            foreach (var c in cameras) {
                if (!c.isActiveAndEnabled)
                    continue;

                cam = c;
                break;
            }
        }

        if (!cam)
            return result;

        result.Camera = cam;
        result.Scene = cam.gameObject.scene;

        return result;
    }
    
    public void CreateCameraRig(Camera usedCamera) {
        CleanupExistingRigs();

        if (VRPlayer.Instance) 
            return;
        
        Log.LogWarning($"[VRSystem] Creating new VR Camera Rig...");
        GameObject rig = new GameObject("[VRCameraRig]");
        rig.transform.SetParent(transform, false);
        rig.AddComponent<VRPlayer>();
        _rigCreated = true;
    }
    
    private void CleanupExistingRigs() {
        for (var i = transform.childCount - 1; i >= 0; i--) {
            var child = transform.GetChild(i);
            if (child.name == "[VRCameraRig]") {
                Destroy(child.gameObject);
                if (DebugMode) Log.LogWarning($"[VRSystem] Destroying VR Camera Rig {child.name}...");
            }
        }
        
        _rigCreated = false;
    }
    
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode) {
        UpdateActiveCamera();
    }
    
    private void OnDestroy() {
        onSceneLoaded -= OnSceneLoaded;
        if (Instance == this)
            Instance = null;
    }
    
    // Unused
    private void TogglePlayerCam(bool toggle) {
        if (!VRPlayer.Instance || !VRPlayer.Instance.StereoRender)
            return;

        var mask = toggle ? 0 : StereoRender.DefaultCullingMask;
        VRPlayer.Instance.StereoRender.leftCamera.cullingMask = mask;
        VRPlayer.Instance.StereoRender.rightCamera.cullingMask = mask;
    }

    private void OnEnable() { 
        Log.LogInfo("[VRSystem] VRSystem Enabled.");
    }

    private void OnDisable() {
        Log.LogInfo("[VRSystem] VRSystem Disabled.");
    }
}