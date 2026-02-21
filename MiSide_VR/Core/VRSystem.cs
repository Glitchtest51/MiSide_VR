using System;
using MiSide_VR.VRInput;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;
using UnityEngine.SceneManagement;
using static MiSide_VR.Plugin;

namespace MiSide_VR.Core;

public class VRSystem: MonoBehaviour {
	public VRSystem(IntPtr value): base(value) { }

	public static VRSystem Instance { get; private set; }
	
	public struct SceneAndCamera {
		public Scene Scene;
		public Camera Camera;
	}
	
	private int _frameCounter;
	
	private SceneAndCamera _lastSceneAndCamera;

	private bool _vrPlayerCreated;

	private void Awake() {
		Log.LogInfo("[VRSystem] VRSystem Created.");

		if (Instance) {
			Log.LogWarning("[VRSystem] Duplicate VRSystem detected, destroying duplicate.");
			Destroy(gameObject);

			return;
		}

		Instance = this;
		DontDestroyOnLoad(gameObject);

		onSceneLoaded += OnSceneLoaded;
	}
	
	private void OnSceneLoaded(Scene scene, LoadSceneMode mode) {
		UpdateActiveCamera();
	}
	
	private void Update() {
		VRInputManager.UpdateInput();
	}
	

	private void LateUpdate() {
		_frameCounter++;

		if (_frameCounter >= 50) {
			_frameCounter = 0;

			UpdateActiveCamera();
		}
	}

	private void UpdateActiveCamera() {
		var sceneAndCamera = FindActiveSceneAndCamera();

		var activeCamera = sceneAndCamera.Camera;
		var activeScene = sceneAndCamera.Scene;

		if (activeCamera) {
			if (activeScene.name != _lastSceneAndCamera.Scene.name || !_lastSceneAndCamera.Camera || activeCamera != _lastSceneAndCamera.Camera) {
				Log.LogWarning($"[VRSystem] Scene or camera changed, respawning VR player rig...");
				_lastSceneAndCamera = sceneAndCamera;

				if (!_vrPlayerCreated) CreateCameraRig(activeCamera);

				var stereoRender = VRPlayer.Instance?.StereoRender;

				if (!stereoRender) return;

				CopyCameraData(activeCamera, stereoRender.headCamera);
				CopyCameraData(activeCamera, stereoRender.leftCamera);
				CopyCameraData(activeCamera, stereoRender.rightCamera);
			} else if (VRPlayer.Instance)
				VRPlayer.Instance.SetSceneAndCamera(sceneAndCamera);
		} else Log.LogInfo($"[VRSystem] No active camera found in scene: {activeScene.name}");
	}

	private static SceneAndCamera FindActiveSceneAndCamera() {
		var result = new SceneAndCamera();
		var cam = Camera.main;

		if (!cam) {
			Camera[] cameras = FindObjectsOfType<Camera>(true);

			foreach (var c in cameras) {
				if (!c.isActiveAndEnabled)
					continue;

				cam = c;

				break;
			}
		}

		// is this fine idk future me check ts out
		if (!cam) return result;

		result.Camera = cam;
		result.Scene = cam.gameObject.scene;

		return result;
	}
	
	public void CreateCameraRig(Camera usedCamera) {
		CleanupExistingRigs();

		if (VRPlayer.Instance)
			return;

		Log.LogWarning($"[VRSystem] Creating new VRPlayer...");
		GameObject rig = new GameObject("[VRPlayer]");
		rig.transform.SetParent(transform, false);
		rig.AddComponent<VRPlayer>();
		_vrPlayerCreated = true;
	}
	
	private void CleanupExistingRigs() {
		for (var i = transform.childCount - 1; i >= 0; i--) {
			var child = transform.GetChild(i);

			if (child.name == "[VRCameraRig]") {
				Destroy(child.gameObject);
				if (DebugMode) Log.LogWarning($"[VRSystem] Destroying VR Camera Rig {child.name}...");
			}
		}

		_vrPlayerCreated = false;
	}
	
	private static void CopyCameraData(Camera source, Camera target) {
		if (!source || !target)
			return;

		var mirrorLayer = LayerMask.NameToLayer("ForMirror");
		var playerLayer = LayerMask.NameToLayer("Player");
		var uiLayer = LayerMask.NameToLayer("UI");

		target.clearFlags = source.clearFlags;
		target.backgroundColor = source.backgroundColor;
		target.orthographic = source.orthographic;
		target.orthographicSize = source.orthographicSize;
		target.fieldOfView = source.fieldOfView;
		target.nearClipPlane = source.nearClipPlane;
		target.farClipPlane = source.farClipPlane;
		target.cullingMask = source.cullingMask;
		target.depth = source.depth;
		target.renderingPath = source.renderingPath;
		target.allowHDR = source.allowHDR;
		target.allowMSAA = source.allowMSAA;

		if ((target.cullingMask & (1 << mirrorLayer)) != 0)
			target.cullingMask &= ~(1 << mirrorLayer);

		if ((target.cullingMask & (1 << playerLayer)) == 0)
			target.cullingMask |= 1 << playerLayer;

		if ((target.cullingMask & (1 << uiLayer)) == 0)
			target.cullingMask |= 1 << uiLayer;

		var sourcePpLayer = source.GetComponent<PostProcessLayer>();

		if (!sourcePpLayer) {
			Log.LogWarning($"[VRSystem] No PostProcessLayer found in Camera: {source.name}, Tag: {source.tag}, Scene: {source.scene.name}.");

			return;
		}

		var targetPpLayer = target.gameObject.GetOrAddComponent<PostProcessLayer>();
		targetPpLayer.m_Resources = sourcePpLayer.m_Resources;
		targetPpLayer.volumeLayer = sourcePpLayer.volumeLayer;
		targetPpLayer.antialiasingMode = sourcePpLayer.antialiasingMode;
		targetPpLayer.stopNaNPropagation = sourcePpLayer.stopNaNPropagation;
		targetPpLayer.finalBlitToCameraTarget = sourcePpLayer.finalBlitToCameraTarget;
		targetPpLayer.volumeTrigger = target.transform;
	}
	
	private void OnDestroy() {
		onSceneLoaded -= OnSceneLoaded;

		if (Instance == this) Instance = null;
	}

	// Unused
	// private void TogglePlayerCam(bool toggle) {
	// 	if (!VRPlayer.Instance || !VRPlayer.Instance.StereoRender)
	// 		return;
	//
	// 	var mask = toggle ? 0 : StereoRender.DefaultCullingMask;
	// 	VRPlayer.Instance.StereoRender.leftCamera.cullingMask = mask;
	// 	VRPlayer.Instance.StereoRender.rightCamera.cullingMask = mask;
	// }
}
