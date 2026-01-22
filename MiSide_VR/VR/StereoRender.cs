using System;
using UnityEngine;
using Valve.VR;
using static MiSide_VR.Plugin;

namespace MiSide_VR.VR;

public class StereoRender : MonoBehaviour {
    public StereoRender(IntPtr value) : base(value) { }
    
    public static StereoRender Instance;
    
    public Transform Head;
    
    public Camera HeadCam;
    
    public Camera LeftCam, RightCam;

    public Camera vrCam;
    
    public RenderTexture LeftRT, RightRT;
    
    public float separation = 0.064f;
    
    private float clipStart = 0.1f;
    
    private float clipEnd = 1000f;
    
    public static int defaultCullingMask = -1; // -1 = everything
    
    private int currentWidth, currentHeight;
    
    public StereoRenderPass stereoRenderPass;

    // Pose arrays for OpenVR: one for rendering, one for game logic (unused here but required by API)
    private readonly TrackedDevicePose_t[] renderPoseArray = new TrackedDevicePose_t[OpenVR.k_unMaxTrackedDeviceCount];
    private readonly TrackedDevicePose_t[] gamePoseArray = new TrackedDevicePose_t[OpenVR.k_unMaxTrackedDeviceCount];
    
    public void Awake() {
        if (Instance != null) {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        
        Setup();
    }
    
    public void Setup() {
        Log.LogInfo($"[StereoRender] ### SETTING UP STEREO RENDERING PIPELINE ###");

        // Create or find the HMD transform
        Head = transform.Find("Head");
        if (!Head) 
            Head = new GameObject("Head").transform;
        Head.SetParent(transform, false);
        Head.localPosition = Vector3.zero;
        Head.localRotation = Quaternion.identity;

        // Mark this object as tracked by SteamVR (HMD)
        Head.gameObject.GetOrAddComponent<SteamVR_TrackedObject>().index = SteamVR_TrackedObject.EIndex.Hmd;

        // --- VR Camera ---
        var vrCamera = Head.Find("vrCamera");
        if (!vrCamera)
            vrCamera = new GameObject("vrCamera").transform;
        
        vrCamera.SetParent(Head, false);
        vrCamera.localPosition = new Vector3(-separation * 0.5f, 0, 0); // Offset left
        vrCamera.localRotation = Quaternion.identity;

        vrCam = vrCamera.gameObject.GetOrAddComponent<Camera>();
        vrCam.cullingMask = defaultCullingMask;
        vrCam.stereoTargetEye = StereoTargetEyeMask.None; // We handle stereo manually
        vrCam.clearFlags = CameraClearFlags.Skybox;
        vrCam.nearClipPlane = clipStart;
        vrCam.farClipPlane = clipEnd;
        vrCam.fieldOfView = 109.363f; // Matches typical headset FoV
        vrCam.depth = 0;
        vrCam.enabled = true;
        
        // --- LEFT EYE ---
        var leftEye = Head.Find("LeftEye");
        if (!leftEye)
            leftEye = new GameObject("LeftEye").transform;
        
        leftEye.SetParent(Head, false);
        leftEye.localPosition = new Vector3(-separation * 0.5f, 0, 0); // Offset left
        leftEye.localRotation = Quaternion.identity;

        LeftCam = leftEye.gameObject.GetOrAddComponent<Camera>();
        LeftCam.cullingMask = defaultCullingMask;
        LeftCam.stereoTargetEye = StereoTargetEyeMask.None; // We handle stereo manually
        LeftCam.clearFlags = CameraClearFlags.Skybox;
        LeftCam.nearClipPlane = clipStart;
        LeftCam.farClipPlane = clipEnd;
        LeftCam.fieldOfView = 109.363f; // Matches typical headset FoV
        LeftCam.depth = 0;
        LeftCam.enabled = true;

        // --- RIGHT EYE ---
        var rightEye = Head.Find("RightEye");
        if (!rightEye)
            rightEye = new GameObject("RightEye").transform;
        
        rightEye.SetParent(Head, false);
        rightEye.localPosition = new Vector3(separation * 0.5f, 0, 0); // Offset right
        rightEye.localRotation = Quaternion.identity;

        RightCam = rightEye.gameObject.GetOrAddComponent<Camera>();
        RightCam.cullingMask = defaultCullingMask;
        RightCam.stereoTargetEye = StereoTargetEyeMask.None;
        RightCam.clearFlags = CameraClearFlags.Skybox;
        RightCam.nearClipPlane = clipStart;
        RightCam.farClipPlane = clipEnd;
        RightCam.fieldOfView = 109.363f;
        RightCam.depth = 0;
        RightCam.enabled = true;

        // Apply projection matrices from SteamVR (non-linear, per-eye)
        UpdateProjectionMatrix();

        // Allocate render textures at recommended resolution
        UpdateResolution();

        // Initialize the OpenVR submission pass
        stereoRenderPass = new StereoRenderPass(this);
    }
    
    public void SetCameraMask(int mask) {
        LeftCam.cullingMask = mask;
        RightCam.cullingMask = mask;
    }
    
    public void UpdateProjectionMatrix() {
        var leftProj = OpenVR.System.GetProjectionMatrix(EVREye.Eye_Left, clipStart, clipEnd);
        var rightProj = OpenVR.System.GetProjectionMatrix(EVREye.Eye_Right, clipStart, clipEnd);

        LeftCam.projectionMatrix = leftProj.ConvertToMatrix4x4();
        RightCam.projectionMatrix = rightProj.ConvertToMatrix4x4();
    }
    
    public void UpdateResolution() {
        // Use SteamVR's recommended eye texture size, with fallbacks
        currentWidth = (SteamVR.instance.sceneWidth > 0) ? (int)SteamVR.instance.sceneWidth : 2208;
        currentHeight = (SteamVR.instance.sceneHeight > 0) ? (int)SteamVR.instance.sceneHeight : 2452;

        // Clean up old textures
        if (LeftRT != null) Destroy(LeftRT);
        if (RightRT != null) Destroy(RightRT);

        // Create new render textures
        LeftRT = new RenderTexture(currentWidth, currentHeight, 24, RenderTextureFormat.ARGB32) { antiAliasing = 2 };
        RightRT = new RenderTexture(currentWidth, currentHeight, 24, RenderTextureFormat.ARGB32) { antiAliasing = 2 };

        // Assign as render targets
        LeftCam.targetTexture = LeftRT;
        RightCam.targetTexture = RightRT;
    }
    
    public void OnDestroy() {
        Instance = null;
    }
    
    public void FixedUpdate() {
        // Allow a small tolerance (-1 pixel) to avoid unnecessary updates
        if (currentWidth < (int)SteamVR.instance.sceneWidth - 1 || currentHeight < (int)SteamVR.instance.sceneHeight - 1) 
            UpdateResolution();
    }
    
    public void LateUpdate() {
        // Fetch latest device poses from OpenVR compositor
        OpenVR.Compositor.WaitGetPoses(renderPoseArray, gamePoseArray);

        // Apply HMD rotation to the Head transform
        var hmdPose = renderPoseArray[(int)OpenVR.k_unTrackedDeviceIndex_Hmd];
        if (hmdPose.bPoseIsValid)
            Head.localRotation = hmdPose.mDeviceToAbsoluteTracking.GetRotation();
        

        // Refresh projection matrices (they can change with IPD or calibration)
        UpdateProjectionMatrix();
    }

    /// <summary>
    /// Custom render pass that submits left and right eye textures to the OpenVR compositor.
    /// This is the final step that makes the rendered frames visible in the VR headset.
    /// </summary>
    public class StereoRenderPass {
        private readonly StereoRender stereoRender;
        public bool isRendering;

        public StereoRenderPass(StereoRender stereoRender) {
            this.stereoRender = stereoRender;
        }
        
        public void Execute() {
            if (!stereoRender.enabled)
                return;

            // Wrap Unity render texture handles into OpenVR-compatible Texture_t structs
            var leftTex = new Texture_t {
                handle = stereoRender.LeftRT.GetNativeTexturePtr(),
                eType = SteamVR.instance.textureType,
                eColorSpace = EColorSpace.Auto
            };

            var rightTex = new Texture_t {
                handle = stereoRender.RightRT.GetNativeTexturePtr(),
                eType = SteamVR.instance.textureType,
                eColorSpace = EColorSpace.Auto
            };

            // Flip V coordinates to match OpenVR's expected texture layout
            var textureBounds = new VRTextureBounds_t {
                uMin = 0,
                vMin = 1, // Top in Unity = bottom in OpenVR
                uMax = 1,
                vMax = 0  // Bottom in Unity = top in OpenVR
            };

            // Submit frames to compositor
            EVRCompositorError errorL = OpenVR.Compositor.Submit(EVREye.Eye_Left, ref leftTex, ref textureBounds, EVRSubmitFlags.Submit_Default);
            EVRCompositorError errorR = OpenVR.Compositor.Submit(EVREye.Eye_Right, ref rightTex, ref textureBounds, EVRSubmitFlags.Submit_Default);

            // Optional: log errors during development
            // if (errorL != EVRCompositorError.None) MelonLogger.Warning($"Left eye submit error: {errorL}");
            // if (errorR != EVRCompositorError.None) MelonLogger.Warning($"Right eye submit error: {errorR}");
        }
    }
}

internal static class Utils {
    public static T GetOrAddComponent<T>(this GameObject gameObject) where T : Component {
        if (gameObject == null)
            throw new ArgumentNullException("GetOrAddComponent: gameObject is null!");

        T comp = gameObject.GetComponent<T>();
        if (comp == null)
            comp = gameObject.AddComponent<T>();

        return comp;
    }
    
    public static Matrix4x4 ConvertToMatrix4x4(this HmdMatrix44_t hm) {
        var m = new Matrix4x4();
        m.m00 = hm.m0;
        m.m01 = hm.m1;
        m.m02 = hm.m2;
        m.m03 = hm.m3;
        m.m10 = hm.m4;
        m.m11 = hm.m5;
        m.m12 = hm.m6;
        m.m13 = hm.m7;
        m.m20 = hm.m8;
        m.m21 = hm.m9;
        m.m22 = hm.m10;
        m.m23 = hm.m11;
        m.m30 = hm.m12;
        m.m31 = hm.m13;
        m.m32 = hm.m14;
        m.m33 = hm.m15;
        return m;
    }
}