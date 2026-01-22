using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using static MiSide_VR.Plugin;

namespace MiSide_VR.VR;

public class VRSystem : MonoBehaviour {
    public VRSystem(IntPtr value) : base(value) { }
    
    public static VRSystem Instance { get; private set; }
    
    private int UpdateCounter = 0;
    
    private Camera UsedCamera = null;
    
    private string UsedSceneName = "";
    
    private bool RespawnPlayer = false;
    
    private bool RigCreated = false;
    
    private void Awake() {
        Log.LogMessage("########### INITIALIZING VR SYSTEM #############");
        
        if (Instance != null) {
            Log.LogError("Trying to create duplicate VRSystem instance! Disabling this one.");
            enabled = false;
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Subscribe to scene load events (currently used for cleanup/extensibility)
        // onSceneLoaded += new Action<Scene, LoadSceneMode>(OnSceneLoaded);
    }
    
    private void LateUpdate() {
        UpdateCounter++;
        
        if (UpdateCounter >= 50) {
            UpdateCounter = 0;
            
            var sceneAndCam = FindActiveGameplayScene();
            
            string sceneName = sceneAndCam.scene.name;
            // if (sceneName == "BlankScene" || sceneName == "SonsMain" || sceneName == "SonsTitleScene")
            if (true) {
                var activeCam = sceneAndCam.camera;
                if (activeCam != null) {
                    if (sceneAndCam.scene.name != UsedSceneName || UsedCamera == null || activeCam != UsedCamera) {
                        Log.LogWarning($"[VRSystem] Scene or camera changed — respawning VR player rig");
                        UsedSceneName = sceneAndCam.scene.name;
                        UsedCamera = activeCam;
                        
                        if (!RigCreated) {
                            CreateCameraRig(UsedCamera);
                        }
                    } else {
                        if (VRPlayer.Instance != null && sceneAndCam.camera != null) {
                            VRPlayer.Instance.SetSceneAndCamera(sceneAndCam);
                        }
                    }
                } else {
                    Log.LogInfo($"[VRSystem] No active camera found in scene: {sceneAndCam.scene.name}");
                }
            } else {
                // Not a gameplay scene — clear references
                UsedCamera = null;
                // Optional: hide VR rig here if needed
            }
        }
    }
    
    private ActiveSceneWithCamera FindActiveGameplayScene() {
        ActiveSceneWithCamera result = new ActiveSceneWithCamera();

        int sceneCount = SceneManager.sceneCount;
        for (int i = 0; i < sceneCount; i++) {
            var scene = SceneManager.GetSceneAt(i);
            if (!scene.IsValid() || !scene.isLoaded)
                continue;

            result.scene = scene;
            bool hasActiveCamera = false;

            // Search all root objects in the scene for cameras
            var rootObjects = scene.GetRootGameObjects();
            foreach (var root in rootObjects) {
                var cameras = root.GetComponentsInChildren<Camera>(true);
                foreach (var cam in cameras) {
                    if (!cam.isActiveAndEnabled)
                        continue;
                    
                    // if (cam.name.Equals("MainCameraFP"))
                    // {
                    //     result.camera = cam;
                    //     hasActiveCamera = true;
                    //     break;
                    // }
                    
                    result.camera = cam;
                    hasActiveCamera = true;
                    break;
                }
                
                if (hasActiveCamera)
                    break;
            }

            if (hasActiveCamera)
                break;
        }

        return result;
    }
    
    private void CleanupExistingRigs() {
        for (int i = transform.childCount - 1; i >= 0; i--) {
            Transform child = transform.GetChild(i);
            if (child.name == "[VRCameraRig]") {
                Log.LogInfo("Cleaning up old VRCameraRig");
                Destroy(child.gameObject);
            }
        }

        RigCreated = false;
    }
    
    public void CreateCameraRig(Camera usedCamera) {
        CleanupExistingRigs();

        if (VRPlayer.Instance == null) {
            Log.LogWarning($"[VRSystem] #### CREATING NEW VR CAMERA RIG ####");
            GameObject rig = new GameObject("[VRCameraRig]");
            rig.transform.SetParent(transform, false);
            rig.AddComponent<VRPlayer>();
            RigCreated = true;
        }
    }
    
    private void OnSceneLoaded(int buildIndex, string sceneName) {
        // Intentionally empty
    }
    
    private void TogglePlayerCam(bool toggle) {
        if (VRPlayer.Instance == null || VRPlayer.Instance.StereoRender == null)
            return;

        int mask = toggle ? 0 : StereoRender.defaultCullingMask;
        VRPlayer.Instance.StereoRender.LeftCam.cullingMask = mask;
        VRPlayer.Instance.StereoRender.RightCam.cullingMask = mask;
    }
    
    private void OnDestroy() {
        // onSceneLoaded -= new Action<Scene, LoadSceneMode>(OnSceneLoaded);
    }

    private void OnEnable() { 
        Log.LogInfo("[VRSystem] VRSystem ENABLED");
    }

    private void OnDisable() {
        Log.LogInfo("[VRSystem] VRSystem DISABLED");
    }
}