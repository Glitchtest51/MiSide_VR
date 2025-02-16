using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using Valve.VR;
using static MiSide_VR.Plugin;
using MiSide_VR.Assets;
namespace MiSide_VR.Player
{

    /// <summary>
    /// Responsible for seting up all VR related classes and handling focus state changes.
    /// </summary>
    public class VRSystems : MonoBehaviour
    {
        public VRSystems(IntPtr value) : base(value) { }
        public string SceneName { get; private set; }

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
                GameObject Origin = new GameObject("[Origin]");
                GameObject rig = Instantiate(AssetLoader.VRCameraRig).gameObject;
                rig.transform.parent = Origin.transform;
                rig.transform.localPosition = new Vector3(0, 0, 0);
                rig.transform.localRotation = Quaternion.identity;
                rig.AddComponent<VRPlayer>();
            }
        }


        private void OnSceneLoaded(Scene scene, LoadSceneMode mode) 
        {
            Log.Debug("OnSceneLoaded: " + scene.name);
            SceneName = scene.name;
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