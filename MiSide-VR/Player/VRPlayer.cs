using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using Valve.VR;
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

            // 初始化VR相机和左右手
            Body = transform;
            Origin = transform.parent;

            Plugin.onSceneLoaded += OnSceneLoaded;

            //SteamVR_Actions.PreInitialize();
            SteamVR.InitializeStandalone(EVRApplicationType.VRApplication_Scene);

            SetupImmediately();
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
                Origin.SetParent(FPSCam.transform, false);
                Origin.localPosition = Vector3.zero;
                Origin.localRotation = Quaternion.identity;
            }
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
                Origin.SetParent(FPSCam.transform, false);
                Origin.localPosition = Vector3.zero;
                Origin.localRotation = Quaternion.identity;

                // make cubes on cameras for debugging purposes, ill delete it when i fix the cam
                //GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                //cube.transform.localScale = new Vector3(0.1f, 0.1f, 0.1f);
                //cube.transform.position = Camera.main.transform.position;
                //cube.transform.SetParent(Camera.main.transform);
                //Renderer renderer = cube.GetComponent<Renderer>();
                //if (renderer != null) {
                //    renderer.material.color = Color.red;
                //}

                //GameObject cube2 = GameObject.CreatePrimitive(PrimitiveType.Cube);
                //cube2.transform.localScale = new Vector3(0.1f, 0.1f, 0.1f);
                //cube2.transform.position = StereoRender.Head.gameObject.transform.position;
                //cube2.transform.SetParent(StereoRender.Head.gameObject.transform);
                //Renderer renderer2 = cube2.GetComponent<Renderer>();
                //if (renderer2 != null) {
                //    renderer2.material.color = Color.red;
                //}
            }
        }

        private void LateUpdate()
        {
            if (FPSCam != null && StereoRender != null && Origin != null)
            {
                //FPSCam.transform.rotation = StereoRender.Head.rotation;

                if (Origin.localPosition.y == 0f) {
                    Debug.Log("Origin.localPosition.y == 0f");
                    Origin.localPosition = Origin.localPosition - Vector3.up * StereoRender.Head.localPosition.y;
                }
                Origin.position = new Vector3(FPSCam.transform.position.x, Origin.position.y, FPSCam.transform.position.z);

                // Check if VRPlayer.Instance is not null and ensure the proper conditions are met
                if (VRPlayer.Instance != null && VRPlayer.Instance.StereoRender != null && VRPlayer.Instance.StereoRender.stereoRenderPass != null) {
                    // Execute the stereoRenderPass
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

        //public float GetPlayerHeight() {
        //    if (!StereoRender.Head) {
        //        return 1.8f;
        //    }
        //    return StereoRender.Head.localPosition.y;
        //}
    }
}
