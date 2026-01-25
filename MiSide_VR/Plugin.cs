using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using Il2CppInterop.Runtime.Injection;
using System.IO;
using UnityEngine;
using System;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security;
using MiSide_VR.VR;
using Valve.VR;
using UnityEngine.SceneManagement;

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

    public struct ActiveSceneAndCamera {
        public Scene Scene;
        public Camera Camera;
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
        } catch (Exception ex) {
            Log.LogError($"SteamVR initialization failed: {ex.Message}");
            VREnabled = false;
            return;
        }
        
        SetupIL2CPPClassInjections();
        
        SceneManager.sceneLoaded += new Action<Scene, LoadSceneMode>(OnSceneLoaded);

        VREnabled = true;
        Log.LogInfo("VR initialized.");
    }
    
    private static void SetupIL2CPPClassInjections() {
        ClassInjector.RegisterTypeInIl2Cpp<VRSystem>();
        ClassInjector.RegisterTypeInIl2Cpp<VRPlayer>();
        ClassInjector.RegisterTypeInIl2Cpp<StereoRender>();
        ClassInjector.RegisterTypeInIl2Cpp<VRLoop>();
        ClassInjector.RegisterTypeInIl2Cpp<HandController>();
    }
    
    public static bool LoadDll(string dll) {
        var dllDirectory = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "UserLibs");
        var dllPath = Path.Combine(dllDirectory, dll);
        SetDllDirectory(dllDirectory);
        
        if (!File.Exists(dllPath)) {
            Log.LogError($"{dllPath} does not exist");
            return false;
        }
        
        var result = LoadLibrary(dll);
        
        if (DebugMode) 
            Log.LogDebug($"Load {dll} result: {result}");
        
        if (result == IntPtr.Zero) {
            Log.LogError($"Failed to load library, Win32 Error: {Marshal.GetLastWin32Error()}, result: {result}");
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
        if (!GameObject.Find("VR_Loop")) {
            Log.LogInfo("Creating VR_Loop...");
            var gameObject = new GameObject("VR_Loop");
            UnityEngine.Object.DontDestroyOnLoad(gameObject);
            gameObject.AddComponent<VRLoop>();
        }
        
        Log.LogInfo(scene.name);
        
        onSceneLoaded?.Invoke(scene, mode);
    }
}

public class VRLoop : MonoBehaviour {
    public VRLoop(IntPtr value) : base(value) { }
    
    private static bool _initialized;
    private int _initFrameCount;

    private void Update() {
        if (!_initialized) {
            _initFrameCount++;
            if (!(_initFrameCount > 3)) return;
            
            _initialized = true;
            if (!VRSystem.Instance) {
                if (Plugin.DebugMode) 
                    Plugin.Log.LogInfo("[VR_Loop] Creating VR_Globals...");
                new GameObject("VR_Globals").AddComponent<VRSystem>();
            }
        }
        
        if (Plugin.VREnabled) {
            // NativeVRInput.Update();
        }

        // Inject simulated keyboard/mouse input based on VR gestures
        // kh?.InjectMovement();
    }
    
    private void LateUpdate() {
        if (VRPlayer.Instance && VRPlayer.Instance.StereoRender && VRPlayer.Instance.StereoRender.stereoRenderPass != null) 
            VRPlayer.Instance.StereoRender.stereoRenderPass.Execute();
    }
}

public static class Utils {
    public static T GetOrAddComponent<T>(this GameObject gameObject) where T : Component {
        if (!gameObject)
            throw new ArgumentNullException(nameof(gameObject));

        var comp = gameObject.GetComponent<T>();
        if (!comp)
            comp = gameObject.AddComponent<T>();

        return comp;
    }
    
    public static Matrix4x4 ConvertToMatrix4X4(this HmdMatrix44_t hm) {
        var m = new Matrix4x4 {
            m00 = hm.m0,
            m01 = hm.m1,
            m02 = hm.m2,
            m03 = hm.m3,
            m10 = hm.m4,
            m11 = hm.m5,
            m12 = hm.m6,
            m13 = hm.m7,
            m20 = hm.m8,
            m21 = hm.m9,
            m22 = hm.m10,
            m23 = hm.m11,
            m30 = hm.m12,
            m31 = hm.m13,
            m32 = hm.m14,
            m33 = hm.m15
        };
        return m;
    }
}
