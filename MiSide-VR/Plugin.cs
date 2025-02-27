﻿using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using Il2CppInterop.Runtime.Injection;
using System.IO;
using UnityEngine;
using System;
using System.Runtime.InteropServices;
using System.Security;
using MiSide_VR.Player;
using UnityEngine.SceneManagement;
using System.Diagnostics;
using System.Collections.Generic;
using MiSide_VR.Player.Controls;
using MiSide_VR.Assets;

namespace MiSide_VR;

[BepInPlugin(PLUGIN_GUID, PLUGIN_NAME, PLUGIN_VERSION)]
public class Plugin : BasePlugin
{
    public const string PLUGIN_GUID = "com.Glitchtest51.MiSideVR";
    public const string PLUGIN_NAME = "MiSide-VR";
    public const string AUTHOR = "Glitchtest51";
    public const string PLUGIN_VERSION = "1.0.0";

    public static Plugin Instance { get; private set; }

    public delegate void OnSceneLoadedEvent(Scene scene, LoadSceneMode mode);
    public static OnSceneLoadedEvent onSceneLoaded;

    public override void Load() {
        Log.Setup(base.Log);
        Log.Info($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");

        Instance = this;

        //if (SteamVRRunningCheck()) {
        InitVR();
        //} else {
        //    Log.Warning("VR launch aborted, VR is disabled or SteamVR is off!");
        //}

        Log.Info("Load() End");
    }

    private void InitVR() {
        Log.Info("InitVR() Start");

        SetupIL2CPPClassInjections();
        LoadDll();
        SceneManager.sceneLoaded += new Action<Scene, LoadSceneMode>(OnSceneLoaded);
        AssetLoader.LoadAssets();

        Log.Info("InitVR() End");
    }

    public static void LoadDll() {
        try {
            SetUnmanagedDllDirectory();
            var result = LoadLibrary("openvr_api.dll");
            Log.Info("Load openvr_api.dll result: " + result);
            if (result == IntPtr.Zero) {
                throw new InvalidOperationException($"Failed to load library, Win32 Error: {Marshal.GetLastWin32Error()}");
            }
        } catch (Exception ex) {
            Log.Error($"Error loading DLL: {ex.Message}");
        }
    }

    private void SetupIL2CPPClassInjections() {
        ClassInjector.RegisterTypeInIl2Cpp<VRSystems>();
        ClassInjector.RegisterTypeInIl2Cpp<VRPlayer>();
        ClassInjector.RegisterTypeInIl2Cpp<StereoRender>();
        ClassInjector.RegisterTypeInIl2Cpp<HandController>();
    }

    public static void SetUnmanagedDllDirectory() {
        string path = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) + "/Plugins";
        Log.Info("SetUnmanagedDllDirectory: " + path);
        SetDllDirectory(path);
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern bool SetDllDirectory(string path);

    [SuppressUnmanagedCodeSecurity]
    [DllImport("Kernel32.dll", EntryPoint = "LoadLibrary", CallingConvention = CallingConvention.Winapi)]
    public static extern IntPtr LoadLibrary(string lpFileName);

    public void OnSceneLoaded(Scene scene, LoadSceneMode mode) // Runs when a Scene has Loaded and is passed the Scene's Build Index and Name.
    {
        Log.Message("Plugin OnSceneLoaded() Start");
        if (!VRSystems.Instance) {
            new GameObject("[VR_Globals]").AddComponent<VRSystems>();
        }
        if (onSceneLoaded != null)
            onSceneLoaded.Invoke(scene, mode);
    }

    //private bool SteamVRRunningCheck() {
    //    List<Process> possibleVRProcesses = new List<Process>();

    //    possibleVRProcesses.AddRange(Process.GetProcessesByName("vrserver"));
    //    possibleVRProcesses.AddRange(Process.GetProcessesByName("vrcompositor"));

    //    Log.Info("VR processes found - " + possibleVRProcesses.Count);
    //    foreach (Process p in possibleVRProcesses) {
    //        Log.Info(p.ToString());
    //    }
    //    return possibleVRProcesses.Count > 0;
    //}

    public static new class Log {
        static ManualLogSource log;

        public static void Setup(ManualLogSource log) {
            Log.log = log;
        }

        public static void Warning(string msg) {
            log.LogWarning(msg);
        }

        public static void Error(string msg) {
            log.LogError(msg);
        }

        public static void Info(string msg) {
            log.LogInfo(msg);
        }

        public static void Debug(string msg) {
            log.LogDebug(msg);
        }

        public static void Message(string msg) {
            log.LogMessage(msg);
        }
    }
}