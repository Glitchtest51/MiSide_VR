using System;
using UnityEngine;
using UnityEngine.XR;
using Valve.VR;
using static MiSide_VR.Plugin;

namespace MiSide_VR.Player
{
    public class StereoRender : MonoBehaviour
    {
        public StereoRender(IntPtr value) : base(value) { }

        public static StereoRender Instance;
        public Transform Head;
        public Camera HeadCam;
        public Camera LeftCam, RightCam;
        public RenderTexture LeftRT, RightRT;
        public float separation = 0.031f;
        private float clipStart = 0.1f;
        private float clipEnd = 300f;
        // this culling mask was from the game's FPS camera
        public static int defaultCullingMask = 490708959;
        private int currentWidth, currentHeight;
        Camera mainCamera = Camera.main;

        public StereoRenderPass stereoRenderPass;

        TrackedDevicePose_t[] renderPoseArray = new TrackedDevicePose_t[OpenVR.k_unMaxTrackedDeviceCount];
        TrackedDevicePose_t[] gamePoseArray = new TrackedDevicePose_t[OpenVR.k_unMaxTrackedDeviceCount];

        public void Awake()
        {
            Instance = this;

            Setup();
        }

        public void Setup()
        {
            Head = transform.Find("Head");
            if (!Head)
                Head = new GameObject("Head").transform;
            Head.parent = transform;
            Head.localPosition = Vector3.zero;
            Head.localRotation = Quaternion.identity;
            Head.gameObject.GetOrAddComponent<SteamVR_TrackedObject>().index = SteamVR_TrackedObject.EIndex.Hmd;

            var leftEye = Head.Find("LeftEye");
            if (!leftEye)
                leftEye = new GameObject("LeftEye").transform;

            leftEye.parent = Head;
            leftEye.localPosition = new Vector3(-separation, 0, 0);
            leftEye.localEulerAngles = new Vector3(0, 0, 0);

            LeftCam = leftEye.gameObject.GetOrAddComponent<Camera>();
            LeftCam.cullingMask = defaultCullingMask;
            LeftCam.stereoTargetEye = StereoTargetEyeMask.None;
            LeftCam.clearFlags = CameraClearFlags.SolidColor;
            LeftCam.nearClipPlane = clipStart;
            LeftCam.fieldOfView = 109.363f;
            LeftCam.farClipPlane = clipEnd;
            LeftCam.depth = 0;
            LeftCam.backgroundColor = mainCamera.backgroundColor;

            var rightEye = Head.Find("RightEye");
            if (!rightEye)
                rightEye = new GameObject("RightEye").transform;
            rightEye.parent = Head;
            rightEye.localPosition = new Vector3(separation, 0, 0);
            rightEye.localEulerAngles = new Vector3(0, 0, 0);

            RightCam = rightEye.gameObject.GetOrAddComponent<Camera>();
            RightCam.cullingMask = defaultCullingMask;
            RightCam.stereoTargetEye = StereoTargetEyeMask.None;
            RightCam.clearFlags = CameraClearFlags.SolidColor;
            RightCam.fieldOfView = 109.363f;
            RightCam.nearClipPlane = clipStart;
            RightCam.farClipPlane = clipEnd;
            RightCam.depth = 0;
            RightCam.backgroundColor = mainCamera.backgroundColor;

            HeadCam = Head.gameObject.GetOrAddComponent<Camera>();
            HeadCam.cullingMask = 0;
            HeadCam.depth = 100;
            HeadCam.enabled = false;
            HeadCam.nearClipPlane = clipStart;
            HeadCam.farClipPlane = clipEnd;
            HeadCam.backgroundColor = mainCamera.backgroundColor;

            UpdateProjectionMatrix();
            UpdateResolution();

            stereoRenderPass = new StereoRenderPass(this);
            Debug.Log("XRSettings:" + XRSettings.eyeTextureWidth + "x" + XRSettings.eyeTextureHeight);
        }

        public void SetCameraMask(int mask)
        {
            LeftCam.cullingMask = mask;
            RightCam.cullingMask = mask;
        }

        public void UpdateProjectionMatrix()
        {
            var l = OpenVR.System.GetProjectionMatrix(EVREye.Eye_Left, clipStart, clipEnd);
            var r = OpenVR.System.GetProjectionMatrix(EVREye.Eye_Right, clipStart, clipEnd);
            LeftCam.projectionMatrix = l.ConvertToMatrix4x4();
            RightCam.projectionMatrix = r.ConvertToMatrix4x4();
        }

        public void UpdateResolution()
        {
            currentWidth = (SteamVR.instance.sceneWidth <= 0) ? 2208 : (int)SteamVR.instance.sceneWidth;
            currentHeight = (SteamVR.instance.sceneHeight <= 0) ? 2452 : (int)SteamVR.instance.sceneHeight;
            if (LeftRT != null)
                Destroy(LeftRT);
            if (RightRT != null)
                Destroy(RightRT);
            LeftRT = new RenderTexture(currentWidth, currentHeight, 24, RenderTextureFormat.ARGB32);
            RightRT = new RenderTexture(currentWidth, currentHeight, 24, RenderTextureFormat.ARGB32);
            LeftRT.antiAliasing = 4;
            RightRT.antiAliasing = 4;
            LeftCam.targetTexture = LeftRT;
            RightCam.targetTexture = RightRT;
        }

        public void OnDestroy()
        {
            Instance = null;
        }

        public void FixedUpdate()
        {
            if (currentWidth < (int)SteamVR.instance.sceneWidth - 1 || currentHeight < (int)SteamVR.instance.sceneHeight - 1)
            {
                UpdateResolution();
            }
        }

        public void OnPostRender()
        {
            Log.Message("OnPostRender");
            //stereoRenderPass.Execute();
        }

        public void LateUpdate() {
            //MelonLogger.Msg("LateUpdate");
            if (OpenVR.Compositor != null)
                OpenVR.Compositor.WaitGetPoses(renderPoseArray, gamePoseArray);
        }

        public class StereoRenderPass
        {
            private StereoRender stereoRender;
            public bool isRendering;

            public StereoRenderPass(StereoRender stereoRender)
            {
                this.stereoRender = stereoRender;
            }

            public void Execute()
            {
                if (!stereoRender.enabled)
                    return;

                var leftTex = new Texture_t
                {
                    handle = stereoRender.LeftRT.GetNativeTexturePtr(),
                    eType = SteamVR.instance.textureType,
                    eColorSpace = EColorSpace.Auto
                };
                var rightTex = new Texture_t
                {
                    handle = stereoRender.RightRT.GetNativeTexturePtr(),
                    eType = SteamVR.instance.textureType,
                    eColorSpace = EColorSpace.Auto
                };
                var textureBounds = new VRTextureBounds_t();
                textureBounds.uMin = 0;
                textureBounds.vMin = 1;
                textureBounds.uMax = 1;
                textureBounds.vMax = 0;
                EVRCompositorError errorL = OpenVR.Compositor.Submit(EVREye.Eye_Left, ref leftTex, ref textureBounds, EVRSubmitFlags.Submit_Default);
                EVRCompositorError errorR = OpenVR.Compositor.Submit(EVREye.Eye_Right, ref rightTex, ref textureBounds, EVRSubmitFlags.Submit_Default);
            }
        }

    }

}
