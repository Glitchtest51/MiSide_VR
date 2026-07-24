using System;
using UnityEngine;
using Valve.VR;
using static MiSide_VR.Plugin;

namespace MiSide_VR.Core;

public class StereoRender: MonoBehaviour {
	public StereoRender(IntPtr value): base(value) { }

	public static StereoRender Instance { get; private set; }
	
	public StereoRenderPass stereoRenderPass;

	public Transform head;
	public Camera headCamera;

	private const float Separation = 0.064f;
	public Camera leftMainCamera, rightMainCamera;
	// public Camera leftPersonsCamera, rightPersonsCamera;
	public Camera leftUICamera, rightUICamera;
	public RenderTexture leftRT, rightRT;
	
	private const int CullingMask  = 80453 | 416;
	private const float ClipStart = 0.015f;
	private const float FOV = 109.363f;

	private int _currentWidth, _currentHeight;

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
		
		// Head
		head = transform.Find("Head");
		if (!head) head = new GameObject("Head").transform;
		
		head.SetParent(transform, false);
		head.localPosition = Vector3.zero;
		head.localRotation = Quaternion.identity;
		head.gameObject.GetOrAddComponent<SteamVR_TrackedObject>().index = SteamVR_TrackedObject.EIndex.Hmd;
		
		headCamera = head.gameObject.GetOrAddComponent<Camera>();
		headCamera.enabled = false;
		headCamera.clearFlags = CameraClearFlags.SolidColor;
		headCamera.nearClipPlane = ClipStart;
		headCamera.fieldOfView = FOV;
		headCamera.depth = 0;

		// Left Eye
		var leftEye = head.Find("LeftEye");
		if (!leftEye) leftEye = new GameObject("LeftEye").transform;

		leftEye.SetParent(head, false);
		leftEye.localPosition = new (-Separation * 0.5f, 0, 0);
		leftEye.localRotation = Quaternion.identity;
		
		leftMainCamera = leftEye.gameObject.GetOrAddComponent<Camera>();
		leftMainCamera.enabled = true;
		leftMainCamera.stereoTargetEye = StereoTargetEyeMask.None;
		leftMainCamera.clearFlags = CameraClearFlags.SolidColor;
		leftMainCamera.nearClipPlane = ClipStart;
		leftMainCamera.fieldOfView = FOV;
		leftMainCamera.depth = -1;
		leftMainCamera.cullingMask = CullingMask;
		
		// var leftPersonsObject = leftEye.Find("LeftPersonsEye");
		// if (!leftPersonsObject) leftPersonsObject = new GameObject("LeftPersonsEye").transform;
		//
		// leftPersonsObject.SetParent(leftEye, false);
		// leftPersonsObject.localPosition = Vector3.zero;
		// leftPersonsObject.localRotation = Quaternion.identity;
		//
		// leftPersonsCamera = leftPersonsObject.gameObject.GetOrAddComponent<Camera>();
		// leftPersonsCamera.enabled = true;
		// leftPersonsCamera.stereoTargetEye = StereoTargetEyeMask.None;
		// leftPersonsCamera.clearFlags = CameraClearFlags.Nothing;
		// leftPersonsCamera.nearClipPlane = ClipStart;
		// leftPersonsCamera.fieldOfView = FOV;
		// leftPersonsCamera.depth = 1;
		// leftPersonsCamera.cullingMask = 416;
		
		var leftUIObject = leftEye.Find("LeftUIEye");
		if (!leftUIObject) leftUIObject = new GameObject("LeftUIEye").transform;

		leftUIObject.SetParent(leftEye, false);
		leftUIObject.localPosition = Vector3.zero;
		leftUIObject.localRotation = Quaternion.identity;
		
		leftUICamera = leftUIObject.gameObject.GetOrAddComponent<Camera>();
		leftUICamera.enabled = true;
		leftUICamera.stereoTargetEye = StereoTargetEyeMask.None;
		leftUICamera.clearFlags = CameraClearFlags.Depth;
		leftUICamera.nearClipPlane = ClipStart;
		leftUICamera.fieldOfView = FOV;
		leftUICamera.depth = 2;
		leftUICamera.farClipPlane = 100;
		leftUICamera.cullingMask = 1 << VRPlayer.VRUILayer;

		// Right Eye
		var rightEye = head.Find("RightEye");
		if (!rightEye) rightEye = new GameObject("RightEye").transform;

		rightEye.SetParent(head, false);
		rightEye.localPosition = new (Separation * 0.5f, 0, 0);
		rightEye.localRotation = Quaternion.identity;

		rightMainCamera = rightEye.gameObject.GetOrAddComponent<Camera>();
		rightMainCamera.enabled = true;
		rightMainCamera.stereoTargetEye = StereoTargetEyeMask.None;
		rightMainCamera.clearFlags = CameraClearFlags.SolidColor;
		rightMainCamera.nearClipPlane = ClipStart;
		rightMainCamera.fieldOfView = FOV;
		rightMainCamera.depth = -1;
		rightMainCamera.cullingMask = CullingMask;
		
		// var rightPersonsObject = rightEye.Find("RightPersonsEye");
		// if (!rightPersonsObject) rightPersonsObject = new GameObject("RightPersonsEye").transform;
		//
		// rightPersonsObject.SetParent(rightEye, false);
		// rightPersonsObject.localPosition = Vector3.zero;
		// rightPersonsObject.localRotation = Quaternion.identity;
		//
		// rightPersonsCamera = rightPersonsObject.gameObject.GetOrAddComponent<Camera>();
		// rightPersonsCamera.enabled = true;
		// rightPersonsCamera.stereoTargetEye = StereoTargetEyeMask.None;
		// rightPersonsCamera.clearFlags = CameraClearFlags.Nothing;
		// rightPersonsCamera.nearClipPlane = ClipStart;
		// rightPersonsCamera.fieldOfView = FOV;
		// rightPersonsCamera.depth = 1;
		// rightPersonsCamera.cullingMask = 416;
		
		var rightUIObject = rightEye.Find("RightUIEye");
		if (!rightUIObject) rightUIObject = new GameObject("RightUIEye").transform;

		rightUIObject.SetParent(rightEye, false);
		rightUIObject.localPosition = Vector3.zero;
		rightUIObject.localRotation = Quaternion.identity;
		
		rightUICamera = rightUIObject.gameObject.GetOrAddComponent<Camera>();
		rightUICamera.enabled = true;
		rightUICamera.stereoTargetEye = StereoTargetEyeMask.None;
		rightUICamera.clearFlags = CameraClearFlags.Depth;
		rightUICamera.nearClipPlane = ClipStart;
		rightUICamera.fieldOfView = FOV;
		rightUICamera.depth = 2;
		rightUICamera.farClipPlane = 100;
		rightUICamera.cullingMask = 1 << VRPlayer.VRUILayer;
		
		// Apply projection matrices from SteamVR (non-linear, per-eye)
		UpdateProjectionMatrix();
		
		// Allocate render textures at recommended resolution
		UpdateResolution();

		// Initialize the OpenVR submission pass
		stereoRenderPass = new (this);

		Log.LogInfo("[StereoRender] StereoRender Initialized.");
	}

	public void UpdateProjectionMatrix() {
		var leftProj = OpenVR.System.GetProjectionMatrix(EVREye.Eye_Left, ClipStart, 25000);
		var rightProj = OpenVR.System.GetProjectionMatrix(EVREye.Eye_Right, ClipStart, 25000);

		var leftMat = leftProj.ConvertToMatrix4X4();
		var rightMat = rightProj.ConvertToMatrix4X4();

		leftMainCamera.projectionMatrix = leftMat;
		// leftPersonsCamera.projectionMatrix = leftMat;
		leftUICamera.projectionMatrix = leftMat;
		rightMainCamera.projectionMatrix = rightMat;
		// rightPersonsCamera.projectionMatrix = rightMat;
		rightUICamera.projectionMatrix = rightMat;
	}

	public void UpdateResolution() {
		// Use SteamVR's recommended eye texture size, with fallbacks
		_currentWidth = (SteamVR.instance.sceneWidth > 0) ? (int)SteamVR.instance.sceneWidth : 2208;
		_currentHeight = (SteamVR.instance.sceneHeight > 0) ? (int)SteamVR.instance.sceneHeight : 2452;

		// Clean up old textures
		if (leftRT) Destroy(leftRT);
		if (rightRT) Destroy(rightRT);

		// Create new render textures
		leftRT = new(_currentWidth, _currentHeight, 24, RenderTextureFormat.ARGB32) {antiAliasing = 2};
		rightRT = new(_currentWidth, _currentHeight, 24, RenderTextureFormat.ARGB32) {antiAliasing = 2};

		// Assign as render targets
		leftMainCamera.targetTexture = leftRT;
		// leftPersonsCamera.targetTexture = leftRT;
		leftUICamera.targetTexture = leftRT;
		rightMainCamera.targetTexture = rightRT;
		// rightPersonsCamera.targetTexture = rightRT;
		rightUICamera.targetTexture = rightRT;
	}

	public void FixedUpdate() {
		// Allow a small tolerance (-1 pixel) to avoid unnecessary updates
		if (_currentWidth < (int)SteamVR.instance.sceneWidth - 1 || _currentHeight < (int)SteamVR.instance.sceneHeight - 1)
			UpdateResolution();
	}

	public void LateUpdate() {
		stereoRenderPass?.Execute();

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
		if (Instance == this) Instance = null;
	}

	public class StereoRenderPass(StereoRender stereoRender) {
		public void Execute() {
			if (!stereoRender.enabled) return;
			
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
			
			var textureBounds = new VRTextureBounds_t {
				uMin = 0,
				vMin = 1,
				uMax = 1,
				vMax = 0
			};
			
			
			var errorL = OpenVR.Compositor.Submit(EVREye.Eye_Left, ref leftTex, ref textureBounds, EVRSubmitFlags.Submit_Default);
			var errorR = OpenVR.Compositor.Submit(EVREye.Eye_Right, ref rightTex, ref textureBounds, EVRSubmitFlags.Submit_Default);
			
			if (DebugMode) {
				if (errorL != EVRCompositorError.None) Log.LogWarning($"Left eye submit error: {errorL}");
				if (errorR != EVRCompositorError.None) Log.LogWarning($"Right eye submit error: {errorR}");
			}
		}
	}
}
