using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using Il2CppInterop.Runtime.Injection;
using MiSide_VR.Core;
using MiSide_VR.Input;
using MiSide_VR.UI;
using MiSide_VR.UI.Patches;
using UnityEngine;
using UnityEngine.SceneManagement;
using Valve.VR;

namespace MiSide_VR;

public enum TurnStyle { Snap, Smooth, Disabled }

[BepInPlugin(PLUGIN_GUID, PLUGIN_NAME, PLUGIN_VERSION)]
[BepInProcess("MiSideFull.exe")]
public sealed class Plugin : BasePlugin {
	public const string PLUGIN_NAME = "MiSide_VR";
	public const string PLUGIN_AUTHOR = "Glitchtest51";
	public const string PLUGIN_GUID = $"com.{PLUGIN_AUTHOR}.{PLUGIN_NAME}";
	public const string PLUGIN_VERSION = "0.9.0";
	
	internal new static ManualLogSource Log;
	internal static bool VREnabled;
	private static ConfigEntry<bool> _leftHanded;
	private static ConfigEntry<TurnStyle> _turnStyle;
	private static ConfigEntry<float> _snapTurnAngle;
	private static ConfigEntry<float> _smoothTurnSpeed;
	private static ConfigEntry<float> _datamoshStrength;

	internal static event Action<Scene, LoadSceneMode> SceneLoaded;
	
	public static bool LeftHanded => _leftHanded?.Value ?? false;
	public static TurnStyle TurningStyle => _turnStyle?.Value ?? TurnStyle.Snap;
	public static float SnapTurnAngle => _snapTurnAngle?.Value ?? 30f;
	public static float SmoothTurnSpeed => _smoothTurnSpeed?.Value ?? 90f;
	public static float DatamoshStrength => _datamoshStrength?.Value ?? 0.5f;
	
	public const bool DebugMode = true;

	public override void Load() {
		Log = base.Log;
		BindConfig();
		Log.LogInfo($"Loading {PLUGIN_GUID} v{PLUGIN_VERSION}...");

		try {
			InitializeVR();
		} catch (Exception exception) {
			Log.LogError($"VR initialization crashed: {exception}");
			VREnabled = false;
		}

		Log.LogInfo($"{PLUGIN_GUID} {PLUGIN_VERSION} loaded.");
	}

	private void BindConfig() {
		_leftHanded = Config.Bind("Controls", "LeftHanded", false, "Allows you to interact and aim with the Left controller.");
		_turnStyle = Config.Bind("Turning", "Mode", TurnStyle.Snap, "Snap, Smooth, or Disabled");
		_snapTurnAngle = Config.Bind("Turning", "SnapAngle", 30f, new ConfigDescription("Degrees rotated for each snap-turn.", new AcceptableValueRange<float>(15f, 90f)));
		_smoothTurnSpeed = Config.Bind("Turning", "SmoothSpeed", 90f, new ConfigDescription("Maximum smooth-turn speed in degrees/s.", new AcceptableValueRange<float>(30f, 360f)));
		_datamoshStrength = Config.Bind("Visuals", "DatamoshStrength", 0.5f, new ConfigDescription("Datamosh Strength. Set to 1 for the original strength.", new AcceptableValueRange<float>(0.1f, 1f)));
	}

	private static void InitializeVR() {
		VREnabled = false;

		if (!LoadDll("openvr_api.dll")) {
			Log.LogError("Failed to load openvr_api.dll. VR disabled.");
			return;
		}

		var steamVRready = false;
		var error = EVRInitError.None;
		var initialized = false;
		try {
			var system = OpenVR.Init(ref error, EVRApplicationType.VRApplication_Background);
			initialized = error == EVRInitError.None;
			steamVRready = initialized && system != null && system.IsTrackedDeviceConnected(OpenVR.k_unTrackedDeviceIndex_Hmd);
		} catch { steamVRready = false; }
		finally { if (initialized) OpenVR.Shutdown(); }
		
		if (!steamVRready) {
			Log.LogWarning("No headset detected! VR disabled.");
			return;
		}

		RegisterInIL2CPP();
		new Harmony(PLUGIN_GUID).PatchAll(Assembly.GetExecutingAssembly());
		SceneManager.sceneLoaded += new Action<Scene, LoadSceneMode>(OnSceneLoaded);

		VREnabled = true;
		Log.LogInfo("VR initialized.");
	}

	private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) {
		if (!VREnabled) return;
				if (!VRSystem.Instance) {
			Log.LogInfo("Creating VRSystem...");
			new GameObject("VRSystem").AddComponent<VRSystem>();
		}
		CanvasPatch.ResetSceneState();
		CanvasPatch.ProcessExistingCanvases();
		SceneLoaded?.Invoke(scene, mode);
	}

	private static void RegisterInIL2CPP() {
		ClassInjector.RegisterTypeInIl2Cpp<VRSystem>();
		ClassInjector.RegisterTypeInIl2Cpp<VRPlayer>();
		ClassInjector.RegisterTypeInIl2Cpp<VRController>();
		ClassInjector.RegisterTypeInIl2Cpp<VirtualScreen>();
	}

	private static bool LoadDll(string dll) {
		var dllDirectory = Path.Combine(Paths.GameRootPath, "MiSideFull_Data", "Plugins", "x86_64");
		var dllPath = Path.Combine(dllDirectory, dll);
		SetDllDirectory(dllDirectory);

		if (!File.Exists(dllPath)) {
			Log.LogError($"{dllPath} does not exist");
			return false;
		}

		var result = LoadLibrary(dll);
		if (DebugMode) Log.LogDebug($"Load {dll} result: {result}");

		if (result == IntPtr.Zero) {
			Log.LogError($"Failed to load library, Win32 Error: {Marshal.GetLastWin32Error()}, result: {result}");
			return false;
		}

		return true;
	}

	[SuppressUnmanagedCodeSecurity]
	[DllImport("Kernel32.dll", EntryPoint = "LoadLibrary", CallingConvention = CallingConvention.Winapi)]
	private static extern IntPtr LoadLibrary(string lpFileName);

	[DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
	private static extern bool SetDllDirectory(string path);
}
