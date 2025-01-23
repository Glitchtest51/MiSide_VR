using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using static MiSide_VR.Plugin;
using HarmonyLib;

namespace MiSide_VR.Player
{

    /// <summary>
    /// Responsible for seting up all VR related classes and handling focus state changes.
    /// </summary>
    public class VRSystems : MonoBehaviour
    {
        public VRSystems(IntPtr value) : base(value) { }

        public static VRSystems Instance { get; private set; }
        public static HarmonyLib.Harmony HarmonyInstance { get; set; }

        private void Awake()
        {
            if (Instance)
            {
                Log.Error("Trying to create duplicate VRSystems class!");
                enabled = false;
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            if (HarmonyInstance == null)
                HarmonyInstance = new HarmonyLib.Harmony("com.Glitchtest51.MiSide_VR");
            HarmonyInstance.PatchAll();

            Plugin.onSceneLoaded += OnSceneLoaded;
        }

        private void Update()
        {
            CreateCameraRig();
        }

        private void CreateCameraRig()
        {
            if (!VRPlayer.Instance) {
                GameObject rig = new GameObject("[VRCameraRig]");
                rig.transform.parent = transform;
                rig.AddComponent<VRPlayer>();
            }
        }


        private void OnSceneLoaded(Scene scene, LoadSceneMode mode) 
        {
            if (VRPlayer.Instance == null)
            {
                CreateCameraRig();
            }
        }

        private void TogglePlayerCam(bool toggle)
        {
            if (toggle)
            {
                VRPlayer.Instance.StereoRender.LeftCam.cullingMask = 0;
                VRPlayer.Instance.StereoRender.RightCam.cullingMask = 0;
            }
            else
            {
                VRPlayer.Instance.StereoRender.LeftCam.cullingMask = StereoRender.defaultCullingMask;
                VRPlayer.Instance.StereoRender.RightCam.cullingMask = StereoRender.defaultCullingMask;
            }
        }

        private void OnDestroy()
        {
            Plugin.onSceneLoaded -= OnSceneLoaded;
        }
    }
}