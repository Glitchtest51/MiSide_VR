using MiSide_VR.Player.Controls;
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.LowLevel;
using UnityEngine.SceneManagement;
using Valve.VR;
using static MiSide_VR.Player.Controls.HandController;
using static MiSide_VR.Plugin;

namespace MiSide_VR.Player
{
    public class VRPlayer : MonoBehaviour
    {
        public VRPlayer(IntPtr value) : base(value) { }

        public static VRPlayer Instance { get; private set; }
        public Transform Origin { get; private set; }
        public Transform Body { get; private set; }
        public Camera Camera { get; private set; }
        public Camera FPSCam { get; private set; }
        public HandController LeftHand { get; private set; }
        public HandController RightHand { get; private set; }
        public StereoRender StereoRender { get; private set; }

        public bool isUIMode = false;

        private void Awake()
        {
            if (Instance)
            {
                Log.Error("Trying to create duplicate VRPlayer!");
                enabled = false;
                return;
            }
            Instance = this;

            Body = transform;
            Origin = transform.parent;

            Plugin.onSceneLoaded += OnSceneLoaded;

            SteamVR_Settings.instance.poseUpdateMode = SteamVR_UpdateModes.OnLateUpdate;
            SteamVR.InitializeStandalone(EVRApplicationType.VRApplication_Scene);

            SetupImmediately();

            //test
            DontDestroyOnLoad(Origin);
        }

        private static bool setupLock = false;

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode) {
            MelonCoroutines.Start(Setup());
        }

        public IEnumerator Setup()
        {
            if (setupLock)
                yield break;
            setupLock = true;
            if (StereoRender)
            {
                if (StereoRender.Head)
                    Destroy(StereoRender.Head.gameObject);
                Destroy(StereoRender);
            }
            yield return new WaitForSeconds(0.5f);
            FPSCam = Camera.main;
            if (FPSCam != null)
            {
                StereoRender = Body.gameObject.AddComponent<StereoRender>();
                //Origin.SetParent(FPSCam.transform, false);
                Origin.localPosition = Vector3.zero;
                Origin.localRotation = FPSCam.transform.rotation;
                Origin.localScale = new Vector3(1.5f, 1.5f, 1.5f);
            }

            LeftHand = transform.Find("LeftHand").gameObject.GetOrAddComponent<HandController>();
            RightHand = transform.Find("RightHand").gameObject.GetOrAddComponent<HandController>();
            LeftHand.Setup(HandType.Left);
            RightHand.Setup(HandType.Right);

            setupLock = false;
        }
        public void SetupImmediately()
        {
            if (StereoRender)
            {
                if (StereoRender.Head)
                    Destroy(StereoRender.Head.gameObject);
                Destroy(StereoRender);
            }
            FPSCam = Camera.main;
            if (FPSCam != null)
            {
                StereoRender = Body.gameObject.AddComponent<StereoRender>();
                //Origin.SetParent(FPSCam.transform, false);
                Origin.localPosition = Vector3.zero;
                Origin.localRotation = FPSCam.transform.rotation;
                Origin.localScale = new Vector3(1.5f, 1.5f, 1.5f);
            }

            LeftHand = transform.Find("LeftHand").gameObject.GetOrAddComponent<HandController>();
            RightHand = transform.Find("RightHand").gameObject.GetOrAddComponent<HandController>();
            LeftHand.Setup(HandType.Left);
            RightHand.Setup(HandType.Right);
        }

        private void LateUpdate()
        {
            if (FPSCam != null && StereoRender != null && Origin != null) {
                //if (GetPlayerHeight() < 1.75f) {
                //    float tmp = 1.9f / GetPlayerHeight();
                //    Origin.localScale = new Vector3(tmp, tmp, tmp);
                //}

                Origin.position = new Vector3(FPSCam.transform.position.x, 0, FPSCam.transform.position.z);
                FPSCam.transform.rotation = StereoRender.Head.rotation;

                //if (Origin.localPosition.y == 0f || iscutscene) {
                //    Origin.localPosition = Origin.localPosition - Vector3.up * StereoRender.Head.localPosition.y;
                //}
                //Origin.position = new Vector3(FPSCam.transform.position.x, Origin.position.y, FPSCam.transform.position.z);

                // Check if VRPlayer.Instance is not null and ensure the proper conditions are met
                if (VRPlayer.Instance != null && VRPlayer.Instance.StereoRender != null && VRPlayer.Instance.StereoRender.stereoRenderPass != null) {
                    VRPlayer.Instance.StereoRender.stereoRenderPass.Execute();
                }
            }
        }

        private void OnDestroy()
        {
            Plugin.onSceneLoaded -= OnSceneLoaded;
        }

        //private void SetOriginHome() {
        //    SetOriginPosRotScl(new Vector3(0f, -1.70f, 0f), new Vector3(0, 0, 0), new Vector3(1, 1, 1));
        //}

        //public void SetOriginPosRotScl(Vector3 pos, Vector3 euler, Vector3 scale) {
        //    Origin.position = pos;
        //    Origin.localEulerAngles = euler;
        //    Origin.localScale = scale;
        //}

        //public void SetOriginScale(float scale) {
        //    Origin.localScale = new Vector3(scale, scale, scale);
        //}

        //public Vector3 GetWorldForward() {
        //    return StereoRender.Head.forward;
        //}

        //public Vector3 GetFlatForwardDirection(Vector3 foward) {
        //    foward.y = 0;
        //    return foward.normalized;
        //}

        public float GetPlayerHeight() {
            if (!StereoRender.Head) {
                return 1.6f;
            }
            return StereoRender.Head.localPosition.y;
        }
    }
}
