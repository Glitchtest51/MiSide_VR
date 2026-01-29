using System;
using UnityEngine;
using Valve.VR;
using static MiSide_VR.Plugin;

namespace MiSide_VR.VR;

public class StereoRender : MonoBehaviour {
    public StereoRender(IntPtr value) : base(value) { }

    public static StereoRender Instance { get; private set; }
    
    public Transform head;
    public Camera headCamera;

    private const float Separation = 0.064f;
    public Camera leftCamera, rightCamera;
    public RenderTexture leftRT, rightRT;
    
    private const float ClipStart = 0.015f;
    private const float ClipEnd = 240000;
    public const int DefaultCullingMask = -1;
    
    private int _currentWidth, _currentHeight;
    
    public StereoRenderPass stereoRenderPass;
    
    private readonly TrackedDevicePose_t[] _renderPoseArray = new TrackedDevicePose_t[OpenVR.k_unMaxTrackedDeviceCount];
    private readonly TrackedDevicePose_t[] _gamePoseArray = new TrackedDevicePose_t[OpenVR.k_unMaxTrackedDeviceCount];
    
    public void Awake() {
        Log.LogInfo("[StereoRender] StereoRender Created.");
        
        if (Instance) {
            Log.LogWarning("[StereoRender] Duplicate StereoRender detected, destroying duplicate.");
            Destroy(gameObject);
            return;
        }
        Instance = this;
        
        Setup();
    }
    
    public void Setup() {
        Log.LogInfo("[StereoRender] Initializing StereoRender...");

        // Create or find the HMD transform
        head = transform.Find("Head");
        if (!head) 
            head = new GameObject("Head").transform;
        head.SetParent(transform, false);
        head.localPosition = Vector3.zero;
        head.localRotation = Quaternion.identity;

        // Mark this object as tracked by SteamVR (HMD)
        head.gameObject.GetOrAddComponent<SteamVR_TrackedObject>().index = SteamVR_TrackedObject.EIndex.Hmd;
        
        // --- Head Camera ---
        headCamera = head.gameObject.GetOrAddComponent<Camera>();
        headCamera.enabled = false;
        headCamera.cullingMask = DefaultCullingMask;
        headCamera.clearFlags = CameraClearFlags.Skybox;
        headCamera.nearClipPlane = ClipStart;
        headCamera.farClipPlane = ClipEnd;
        headCamera.fieldOfView = 109.363f; 
        headCamera.depth = 0;
        
        // --- LEFT EYE ---
        var leftEye = head.Find("LeftEye");
        if (!leftEye)
            leftEye = new GameObject("LeftEye").transform;
        
        leftEye.SetParent(head, false);
        leftEye.localPosition = new Vector3(-Separation * 0.5f, 0, 0);
        leftEye.localRotation = Quaternion.identity;

        leftCamera = leftEye.gameObject.GetOrAddComponent<Camera>();
        leftCamera.cullingMask = DefaultCullingMask;
        leftCamera.stereoTargetEye = StereoTargetEyeMask.None;
        leftCamera.clearFlags = CameraClearFlags.Skybox;
        leftCamera.nearClipPlane = ClipStart;
        leftCamera.farClipPlane = ClipEnd;
        leftCamera.fieldOfView = 109.363f;
        leftCamera.depth = 0;
        leftCamera.enabled = true;

        // --- RIGHT EYE ---
        var rightEye = head.Find("RightEye");
        if (!rightEye)
            rightEye = new GameObject("RightEye").transform;
        
        rightEye.SetParent(head, false);
        rightEye.localPosition = new Vector3(Separation * 0.5f, 0, 0); // Offset right
        rightEye.localRotation = Quaternion.identity;

        rightCamera = rightEye.gameObject.GetOrAddComponent<Camera>();
        rightCamera.cullingMask = DefaultCullingMask;
        rightCamera.stereoTargetEye = StereoTargetEyeMask.None;
        rightCamera.clearFlags = CameraClearFlags.Skybox;
        rightCamera.nearClipPlane = ClipStart;
        rightCamera.farClipPlane = ClipEnd;
        rightCamera.fieldOfView = 109.363f;
        rightCamera.depth = 0;
        rightCamera.enabled = true;

        // Apply projection matrices from SteamVR (non-linear, per-eye)
        UpdateProjectionMatrix();

        // Allocate render textures at recommended resolution
        UpdateResolution();

        // Initialize the OpenVR submission pass
        stereoRenderPass = new StereoRenderPass(this);
        
        Log.LogInfo("[StereoRender] StereoRender Initialized.");
    }
    
    public void UpdateProjectionMatrix() {
        var leftProj = OpenVR.System.GetProjectionMatrix(EVREye.Eye_Left, ClipStart, ClipEnd);
        var rightProj = OpenVR.System.GetProjectionMatrix(EVREye.Eye_Right, ClipStart, ClipEnd);

        leftCamera.projectionMatrix = leftProj.ConvertToMatrix4X4();
        rightCamera.projectionMatrix = rightProj.ConvertToMatrix4X4();
    }
    
    public void UpdateResolution() {
        // Use SteamVR's recommended eye texture size, with fallbacks
        _currentWidth = (SteamVR.instance.sceneWidth > 0) ? (int)SteamVR.instance.sceneWidth : 2208;
        _currentHeight = (SteamVR.instance.sceneHeight > 0) ? (int)SteamVR.instance.sceneHeight : 2452;

        // Clean up old textures
        if (leftRT) Destroy(leftRT);
        if (rightRT) Destroy(rightRT);

        // Create new render textures
        leftRT = new RenderTexture(_currentWidth, _currentHeight, 24, RenderTextureFormat.ARGB32) { antiAliasing = 2 };
        rightRT = new RenderTexture(_currentWidth, _currentHeight, 24, RenderTextureFormat.ARGB32) { antiAliasing = 2 };

        // Assign as render targets
        leftCamera.targetTexture = leftRT;
        rightCamera.targetTexture = rightRT;
    }
    
    public void FixedUpdate() {
        // Allow a small tolerance (-1 pixel) to avoid unnecessary updates
        if (_currentWidth < (int)SteamVR.instance.sceneWidth - 1 || _currentHeight < (int)SteamVR.instance.sceneHeight - 1) 
            UpdateResolution();
    }
    
    public void LateUpdate() {
        // Fetch latest device poses from OpenVR compositor
        OpenVR.Compositor.WaitGetPoses(_renderPoseArray, _gamePoseArray);

        // Apply HMD rotation to the Head transform
        var hmdPose = _renderPoseArray[(int)OpenVR.k_unTrackedDeviceIndex_Hmd];
        if (hmdPose.bPoseIsValid)
            head.localRotation = hmdPose.mDeviceToAbsoluteTracking.GetRotation();

        // Refresh projection matrices (they can change with IPD or calibration)
        UpdateProjectionMatrix();
    }
    
    public void OnDestroy() {
        if (Instance == this)
            Instance = null;
    }
    
    public class StereoRenderPass(StereoRender stereoRender) {

        public void Execute() {
            if (!stereoRender.enabled)
                return;

            // Wrap Unity render texture handles into OpenVR-compatible Texture_t structs
            var leftTex = new Texture_t {
                handle = stereoRender.leftRT.GetNativeTexturePtr(),
                eType = SteamVR.instance.textureType,
                eColorSpace = EColorSpace.Auto
            };

            var rightTex = new Texture_t {
                handle = stereoRender.rightRT.GetNativeTexturePtr(),
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
            var errorL = OpenVR.Compositor.Submit(EVREye.Eye_Left, ref leftTex, ref textureBounds, EVRSubmitFlags.Submit_Default);
            var errorR = OpenVR.Compositor.Submit(EVREye.Eye_Right, ref rightTex, ref textureBounds, EVRSubmitFlags.Submit_Default);

            // Optional: log errors during development
            if (DebugMode) {
                if (errorL != EVRCompositorError.None) Log.LogWarning($"Left eye submit error: {errorL}");
                if (errorR != EVRCompositorError.None) Log.LogWarning($"Right eye submit error: {errorR}");
            }
        }
    }
}