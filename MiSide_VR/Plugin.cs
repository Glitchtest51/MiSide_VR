using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using Il2CppInterop.Runtime.Injection;
using System.IO;
using System.Collections;
using UnityEngine;
using System;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security;
using MiSide_VR.Input;
using MiSide_VR.VR;
using Valve.VR;
using UnityEngine.SceneManagement;
using Logger = UnityEngine.Logger;

namespace MiSide_VR;

[BepInPlugin(PLUGIN_GUID, PLUGIN_NAME, PLUGIN_VERSION)]
[BepInProcess("MiSideFull.exe")]
public class Plugin : BasePlugin {
    
    // Plugin Info
    public const string PLUGIN_NAME = "MiSide_VR";
    public const string AUTHOR = "Glitchtest51";
    public const string PLUGIN_GUID = $"com.{AUTHOR}.{PLUGIN_NAME}";
    public const string PLUGIN_VERSION = "1.0.0";

    internal new static ManualLogSource Log;
    
    // Debug Mode Toggle
    public const bool DebugMode = true;
    
    public struct ActiveSceneWithCamera {
        public Scene scene;
        public Camera camera;
    }
    
    public delegate void OnSceneLoadedEvent(Scene scene, LoadSceneMode mode);
    public static OnSceneLoadedEvent onSceneLoaded;
    
    // private KeyboardHack kh = new KeyboardHack();
    
    internal static bool VREnabled;

    public override void Load() {
        Log = base.Log;
        
        InitVR();
        
        Log.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} v{PLUGIN_VERSION} is loaded!");
    }

    private void InitVR() {
        Log.LogInfo("Attempting to initialize VR...");
        VREnabled = false;

        if (!LoadDll("openvr_api.dll")) {
            Log.LogError("Failed to load openvr_api.dll. VR disabled.");
            VREnabled = false;
            return;
        }
        
        try {
            SteamVR.InitializeStandalone(EVRApplicationType.VRApplication_Scene);

            if (OpenVR.System == null) {
                Log.LogError("OpenVR System is null. SteamVR not detected.");
                VREnabled = false;
                return;
            }
            
            // NativeVRInput.Initialize();

            if (DebugMode) {
                Log.LogDebug($"[SteamVR] Total actions: {SteamVR_Input.actions?.Length ?? -1}");
                Log.LogDebug($"[SteamVR] Boolean actions: {SteamVR_Input.actionsBoolean?.Length ?? -1}");
                Log.LogDebug($"[SteamVR] Vector2 actions: {SteamVR_Input.actionsVector2?.Length ?? -1}");
            }
        } catch (System.Exception ex) {
            Log.LogError($"SteamVR initialization failed: {ex.Message}");
            VREnabled = false;
            return;
        }
        
        SetupIL2CPPClassInjections();
        
        SceneManager.sceneLoaded += new Action<Scene, LoadSceneMode>(OnSceneLoaded);

        VREnabled = true;
        Log.LogInfo("VR initalized successfully.");
    }
    
    private void SetupIL2CPPClassInjections() {
        ClassInjector.RegisterTypeInIl2Cpp<VRSystem>();
        ClassInjector.RegisterTypeInIl2Cpp<VRPlayer>();
        ClassInjector.RegisterTypeInIl2Cpp<StereoRender>();
        ClassInjector.RegisterTypeInIl2Cpp<VRMono>();
    }
    
    public static bool LoadDll(string dll) {
        string dllDirectory = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "UserLibs");
        string dllPath = Path.Combine(dllDirectory, dll);
        SetDllDirectory(dllDirectory);
        
        if (!System.IO.File.Exists(dllPath)) {
            Log.LogError($"{dllPath} does not exist");
            return false;
        }
        
        var result = LoadLibrary(dll);
        
        if (DebugMode) 
            Log.LogDebug($"Load {dll} result: {result}");
        
        if (result == IntPtr.Zero) {
            Log.LogError($"Failed to load library, Win32 Error: {Marshal.GetLastWin32Error()}");
            return false;
        }

        return true;
    }
    
    [SuppressUnmanagedCodeSecurity]
    [DllImport("Kernel32.dll", EntryPoint = "LoadLibrary", CallingConvention = CallingConvention.Winapi)]
    public static extern IntPtr LoadLibrary(string lpFileName);
    
    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern bool SetDllDirectory(string path);
    
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode) {
        if (GameObject.Find("VR_Loop") == null) {
            Log.LogInfo("Making main Loop");
            var gameObject = new GameObject("VR_Loop");
            UnityEngine.Object.DontDestroyOnLoad(gameObject);
            var vrMono = gameObject.AddComponent<VRMono>();
        }
        
        Log.LogInfo(scene.name);
        
        onSceneLoaded?.Invoke(scene, mode);
    }
}

public class VRMono : MonoBehaviour {
    public VRMono(IntPtr value) : base(value) { }
    
    private static bool initialized;
    private int initFrameCount = 0;

    private void Update() {
        if (!initialized) {
            initFrameCount++;
            if (initFrameCount > 3) {
                initialized = true;
                if (VRSystem.Instance == null) {
                    Plugin.Log.LogInfo("Making VR_Globals...");
                    new GameObject("VR_Globals").AddComponent<VRSystem>();
                }
            }
            return;
        }
        
        if (Plugin.VREnabled) {
            // NativeVRInput.Update();
        }

        // Inject simulated keyboard/mouse input based on VR gestures
        // kh?.InjectMovement();
    }
    
    private void LateUpdate() {
        if (VRPlayer.Instance != null && VRPlayer.Instance.StereoRender != null && VRPlayer.Instance.StereoRender.stereoRenderPass != null) 
            VRPlayer.Instance.StereoRender.stereoRenderPass.Execute();
    }
}
