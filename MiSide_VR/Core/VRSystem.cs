using System;
using MiSide_VR.Core.Rendering;
using MiSide_VR.Input;
using MiSide_VR.Input.Patches;
using MiSide_VR.UI;
using MiSide_VR.UI.Patches;
using UnityEngine;
using UnityEngine.SceneManagement;
using static MiSide_VR.Plugin;

namespace MiSide_VR.Core;

public sealed class VRSystem : MonoBehaviour {
	public VRSystem(IntPtr value) : base(value) {}

	public static VRSystem Instance { get; private set; }

	private readonly CameraEffectMirror _cameraEffects = new();
	private VRRendering _vrRendering;
	private Camera _lastSourceCamera;
	private Camera _lastOverlayCamera;
	private GameMode _lastMode = GameMode.Unsupported;
	private int _effectRefreshCounter;
	private int _contextRefreshCounter;
	private bool _startupAttempted;

	private void Awake() {
		Log.LogInfo("[VRSystem] Created.");
		if (Instance) {
			Log.LogWarning("[VRSystem] Duplicate system destroyed.");
			Destroy(gameObject);
			return;
		}

		Instance = this;
		DontDestroyOnLoad(gameObject);
		SceneLoaded += OnSceneLoaded;

		Application.runInBackground = true;
		Application.targetFrameRate = -1;
		QualitySettings.vSyncCount = 0;
	}

	private void OnSceneLoaded(Scene scene, LoadSceneMode mode) {
		VirtualScreen.Instance?.ResetComfortAnchor();
		GameContext.Refresh();
		ApplyContext(true);
		VRPlayer.Instance?.RequestHeightCalibration();
	}

	private void Update() {
		ReleaseDesktopCursor();

		if (!_startupAttempted && Time.frameCount >= 10) {
			_startupAttempted = true;
			_vrRendering = new VRRendering();
			_vrRendering.Start();
			TrackingOriginMonitor.Initialize();
			GameContext.Refresh();
			ApplyContext(true);
		}

		if (_startupAttempted) {
			VRInput.UpdateInput();
			BackgroundUiInputAdapter.Update();
			CookingInputAdapter.Update();
		}
	}

	private void LateUpdate() {
		ReleaseDesktopCursor();
		if (!_startupAttempted) return;

		CanvasPatch.SynchronizeCaptureCamera();

		var player = VRPlayer.Instance;
		var source = GameContext.SourceCamera;

		var refreshEffects = ++_effectRefreshCounter >= 30;
		if (refreshEffects) _effectRefreshCounter = 0;
		if (player) _cameraEffects.Synchronize(source, player, refreshEffects);

		if (++_contextRefreshCounter < 10) return;
		_contextRefreshCounter = 0;

		GameContext.Refresh();
		ApplyContext(false);
		TamagotchiMonitor.Synchronize();
		PauseUIAdapter.Synchronize();
		CanvasPatch.ProcessExistingCanvases();
		CameraEffectMirror.SynchronizeOutlineTargets();
	}

	private static void ReleaseDesktopCursor() {
		if (Cursor.lockState != CursorLockMode.None) Cursor.lockState = CursorLockMode.None;
		if (!Cursor.visible) Cursor.visible = true;
	}

	private void ApplyContext(bool force) {
		var source = GameContext.SourceCamera;
		var overlay = GameContext.OverlayCamera;
		var changed = force || source != _lastSourceCamera || overlay != _lastOverlayCamera || GameContext.Mode != _lastMode;
		if (!changed) return;

		_lastSourceCamera = source;
		_lastOverlayCamera = overlay;
		_lastMode = GameContext.Mode;

		if (source && _startupAttempted && !VRPlayer.Instance) CreateCameraRig();

		var player = VRPlayer.Instance;
		if (!player) return;

		player.SetContext(GameContext.Mode, GameContext.PlayerMove, source);
		if (source) {
			_cameraEffects.CopyCameraData(source, player.headCamera);
			_cameraEffects.CopyCameraData(source, player.desktopCamera);
			_cameraEffects.Synchronize(source, player, true);
			_effectRefreshCounter = 0;
			source.stereoTargetEye = StereoTargetEyeMask.None;
			player.headCamera.enabled = true;
			player.desktopCamera.enabled = true;
		}
		player.SetUiOverlayMode(source && !GameContext.IsPanelMode);

		SetOnlyVRCameras(player);
		CanvasPatch.ProcessExistingCanvases();
		TamagotchiMonitor.Synchronize();
	}

	private void CreateCameraRig() {
		for (var index = transform.childCount - 1; index >= 0; index--) {
			var child = transform.GetChild(index);
			if (child.name == "[VRPlayer]") Destroy(child.gameObject);
		}
		if (VRPlayer.Instance) return;
		
		Log.LogInfo("[VRSystem] Creating VRPlayer.");
		var rig = new GameObject("[VRPlayer]");
		rig.transform.SetParent(transform, false);
		rig.AddComponent<VRPlayer>();
	}

	private static void SetOnlyVRCameras(VRPlayer player) {
		foreach (var camera in FindObjectsOfType<Camera>(true)) camera.stereoTargetEye = StereoTargetEyeMask.None;

		player.headCamera.stereoTargetEye = StereoTargetEyeMask.Both;
		player.headCamera.enabled = true;
		if (player.headsetUiCamera) player.headsetUiCamera.stereoTargetEye = StereoTargetEyeMask.Both;
	}

	private void OnDestroy() {
		SceneLoaded -= OnSceneLoaded;
		if (Instance == this) Instance = null;
		TamagotchiMonitor.Dispose();
		BackgroundUiInputAdapter.Reset();
		VRInput.Shutdown();
		_vrRendering?.Dispose();
	}
}
