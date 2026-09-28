using System;
using MiSide_VR.Core.Rendering.Patches;
using MiSide_VR.Input;
using MiSide_VR.Input.Patches;
using MiSide_VR.UI;
using MiSide_VR.UI.Patches;
using UnityEngine;
using UnityEngine.XR;
using static MiSide_VR.Plugin;

namespace MiSide_VR.Core;

public class VRPlayer : MonoBehaviour {
	public VRPlayer(IntPtr value) : base(value) {}

	public static VRPlayer Instance { get; private set; }

	public Transform Body { get; private set; }
	public Transform Origin { get; private set; }
	public Transform head;
	public Camera headCamera;
	public Camera headsetUiCamera;
	public Camera desktopCamera;
	public Camera desktopUiCamera;
	public GameObject leftControllerObject;
	public GameObject rightControllerObject;
	public VRController leftController;
	public VRController rightController;

	public GameObject localPlayer;
	public PlayerMove playerMove;
	public Transform playerPerson;
	public Camera camera;
	public GameMode mode;
	public Vector3 positionOffset;
	public static readonly int VrUiLayer = 17;
	public static readonly int VrPointerLayer = 19;

	private bool _snapTurnReset = true;
	private readonly PlayerBodyIK _bodyIk = new();
	private bool _trackingCalibrated;
	private Vector3 _trackingReference;
	private Vector3 _roomscalePositionReference;
	private bool _roomscaleMovePending;
	private Vector3 _roomscaleMoveStart;
	private Vector3 _roomscaleRequestedDelta;
	private Vector3 _roomscaleExpectedGameDelta;
	private Quaternion _roomscaleRequestYaw;
	private float _roomscaleRequestScale = 1f;
	private bool _roomscaleSolidContact;
	private float _roomscaleSolidContactDistance;
	private bool _roomscaleBlocked;
	private Vector3 _roomscaleBlockedDirection;
	private bool _roomscaleYawCalibrated;
	private bool _roomscaleControlActive;
	private float _roomscaleLastHeadYaw;
	private float _roomscalePendingYaw;
	private float _roomscaleConsumedYaw;
	private bool _playerTrackingYawValid;
	private float _playerTrackingYaw;
	private bool _playerAnimationYawActive;
	private float _playerAnimationLastSourceYaw;
	private bool _trackingOriginUpdatePending;
	private bool _rawHeadPoseValid;
	private bool _heightCalibrationPending = true;
	private Vector3 _lastRawHeadPosition;
	private Quaternion _lastRawHeadRotation;
	private Vector3 _playerHeadOffset = new(0f, 1.7f, 0.1f);
	private Location19_Game3 _staticGlitchMinigame;
	private Vector3 _staticGlitchMinigameBodyPosition;
	private Quaternion _staticGlitchMinigameBodyRotation;

	private const float StickThreshold = 0.7f;
	private const float SmoothTurnDeadzone = 0.2f;
	private const int CullingMask = 80453 | 416;
	private const float ClipStart = 0.015f;
	private const float FieldOfView = 109.363f;
	private const float DesktopFieldOfView = 60f;
	private const float MinimumEyeHeight = 0.75f;
	private const float MaximumEyeHeight = 2.25f;
	private const float MinimumRigScale = 0.7f;
	private const float MaximumRigScale = 1.3f;
	private const float TrackingHeightComfortScale = 1.03f;
	private const float RoomscaleRotationStep = 0.5f;
	private const float MaximumRoomscaleVelocity = 2f;
	private const float RoomscaleTriggerProbeDepth = 0.015f;
	private const float GlitchMinigame3CameraBack = 0.6f;
	private const float RecenterMinimumPositionJump = 0.12f;
	private const float RecenterMinimumRotationJump = 15f;
	private const float RecenterPositionSpeed = 6f;
	private const float RecenterRotationSpeed = 900f;

	private void Awake() {
		Log.LogInfo("[VRPlayer] Created.");
		if (Instance) {
			Log.LogWarning("[VRPlayer] Duplicate player destroyed.");
			Destroy(gameObject);
			return;
		}

		Instance = this;
		Setup();
	}

	private void Setup() {
		Body = transform;
		Origin = transform.parent;
		if (!GetComponentInParent<VRSystem>()) {
			Log.LogError("[VRPlayer] Parent VRSystem is missing.");
			Destroy(gameObject);
			return;
		}

		Body.SetParent(Origin, false);
		Body.localScale = Vector3.one;
		DontDestroyOnLoad(Origin);

		head = transform.Find("Head");
		if (!head) head = new GameObject("Head").transform;
		head.SetParent(transform, false);

		headCamera = head.gameObject.GetOrAddComponent<Camera>();
		ConfigureVRCamera(headCamera);

		var desktopCameraObject = new GameObject("DesktopSpectator");
		desktopCameraObject.transform.SetParent(head, false);
		desktopCamera = desktopCameraObject.AddComponent<Camera>();
		ConfigureDesktopCamera(desktopCamera);
		desktopUiCamera = CreateUiOverlayCamera("Desktop UI Overlay", StereoTargetEyeMask.None);
		headsetUiCamera = CreateUiOverlayCamera("Headset UI Overlay", StereoTargetEyeMask.Both);

		leftController = CreateController("LeftHand", VRController.HandType.Left, out leftControllerObject);
		rightController = CreateController("RightHand", VRController.HandType.Right, out rightControllerObject);

		// Start tracking only after the full rig exists.
		TrackingOriginMonitor.Initialize();
		Log.LogInfo("[VRPlayer] Rig created.");
	}

	private VRController CreateController(string objectName, VRController.HandType handType, out GameObject controllerObject) {
		controllerObject = new GameObject(objectName);
		controllerObject.transform.SetParent(Body, false);
		var controller = controllerObject.AddComponent<VRController>();
		controller.Setup(handType);
		return controller;
	}

	private static void ConfigureVRCamera(Camera target) {
		target.enabled = true;
		target.stereoTargetEye = StereoTargetEyeMask.Both;
		target.clearFlags = CameraClearFlags.SolidColor;
		target.nearClipPlane = ClipStart;
		target.farClipPlane = 1000f;
		target.fieldOfView = FieldOfView;
		target.depth = -1f;
		target.cullingMask = CullingMask | (1 << VrUiLayer) | (1 << VrPointerLayer);
	}

	private static void ConfigureDesktopCamera(Camera target) {
		target.enabled = true;
		target.stereoTargetEye = StereoTargetEyeMask.None;
		target.depth = 100f;
		target.fieldOfView = DesktopFieldOfView;
		target.nearClipPlane = ClipStart;
		target.rect = new Rect(0f, 0f, 1f, 1f);
	}

	private Camera CreateUiOverlayCamera(string objectName, StereoTargetEyeMask stereoTarget) {
		var cameraObject = new GameObject(objectName);
		cameraObject.transform.SetParent(head, false);
		var target = cameraObject.AddComponent<Camera>();
		target.enabled = false;
		target.stereoTargetEye = stereoTarget;
		target.clearFlags = CameraClearFlags.Depth;
		target.cullingMask = 1 << VrUiLayer;
		target.useOcclusionCulling = false;
		target.depthTextureMode = DepthTextureMode.None;
		return target;
	}

	public void SetUiOverlayMode(bool enabled) {
		// Draw the gameplay tablet after world post-processing.
		ConfigureUiOverlayCamera(headsetUiCamera, headCamera, StereoTargetEyeMask.Both, enabled);
		ConfigureUiOverlayCamera(desktopUiCamera, desktopCamera, StereoTargetEyeMask.None, enabled);
	}

	private static void ConfigureUiOverlayCamera(Camera overlay, Camera source, StereoTargetEyeMask stereoTarget, bool enabled) {
		if (!overlay || !source) return;

		overlay.stereoTargetEye = stereoTarget;
		overlay.clearFlags = CameraClearFlags.Depth;
		overlay.cullingMask = 1 << VrUiLayer;
		overlay.depth = source.depth + 1f;
		overlay.rect = source.rect;
		overlay.fieldOfView = source.fieldOfView;
		overlay.nearClipPlane = Mathf.Min(source.nearClipPlane, 0.01f);
		overlay.farClipPlane = Mathf.Max(source.farClipPlane, 10f);
		overlay.renderingPath = source.renderingPath;
		overlay.allowHDR = source.allowHDR;
		overlay.allowMSAA = source.allowMSAA;
		overlay.enabled = enabled;
		// Hide the tablet from the effect camera to prevent a blurred duplicate.
		if (enabled) source.cullingMask &= ~(1 << VrUiLayer);
		else source.cullingMask |= 1 << VrUiLayer;
	}

	private void LateUpdate() {
		// Sample again before rendering for the latest OpenVR pose.
		ApplyTrackedPoses();
		DetectTrackingOriginDiscontinuity();
		ApplyTrackingOriginUpdate();
		ConfirmRoomscaleMovement();
		if (_heightCalibrationPending &&
			Body && head && head.localPosition.y is >= MinimumEyeHeight and <= MaximumEyeHeight &&
			_playerHeadOffset.y is >= 0.8f and <= 2.4f) {
			_heightCalibrationPending = false;
			CalibrateHeight();
		}
		if (VRInput.GetButtonDown("CalibrateHeight")) CalibrateHeight();

		if (Body && (camera || playerMove)) {
			UpdateRoomscaleRotation();
			var usePlayerBodyView = GameContext.UsesPlayerBodyView && playerMove;
			var playerBackedView = playerMove && mode is GameMode.StandardPlayer or GameMode.PlayerAnimation;
			var hetoorPlayer = mode == GameMode.Hetoor ? GameContext.HetoorPlayer : null;
			var yawSource = hetoorPlayer ? hetoorPlayer.transform.eulerAngles.y : (mode == GameMode.StandardPlayer || usePlayerBodyView) && playerMove ? playerMove.transform.eulerAngles.y : camera ? camera.transform.eulerAngles.y : playerMove.transform.eulerAngles.y;
			var playerYaw = Quaternion.Euler(0f, yawSource, 0f);
			var rigYaw = playerBackedView ? Quaternion.Euler(0f, GetPlayerTrackingYaw(yawSource, mode == GameMode.StandardPlayer), 0f) : playerYaw;
			var trackedPosition = head.localPosition;

			if (!_trackingCalibrated && trackedPosition.sqrMagnitude > 0.01f) {
				_trackingReference = trackedPosition;
				_roomscalePositionReference = trackedPosition;
				_trackingCalibrated = true;
				if (DebugMode) Log.LogDebug($"[VRPlayer] Calibrated {mode} tracking reference at {_trackingReference}.");
			}

			var positionReference = mode switch { GameMode.StandardPlayer when playerMove => _roomscalePositionReference,
				// Menus use the tracking origin for horizontal placement.
				GameMode.MenuOrLoading => new Vector3(0f, _trackingReference.y, 0f),
				_ => _trackingReference };
			// Use rig yaw so physical turning does not orbit the headset around the player.
			var headPlacementYaw = mode == GameMode.StandardPlayer && playerMove ? rigYaw : playerYaw;
			var authoredHeadPosition = GetAuthoredHeadPosition(headPlacementYaw);
			var bodyPosition = _trackingCalibrated ? authoredHeadPosition - rigYaw * Vector3.Scale(positionReference, Body.localScale) : authoredHeadPosition;

			Body.SetPositionAndRotation(bodyPosition + playerYaw * positionOffset, rigYaw);
		}

		if (playerMove && localPlayer && mode == GameMode.StandardPlayer) UpdateTurn();

		_bodyIk.Update(this);
		ApplyAuthoredCameraPose();
		VirtualScreen.Instance?.RefreshPose(true);
	}

	internal void RequestHeightCalibration() => _heightCalibrationPending = true;

	internal void ApplyTrackedPoses() {
		ApplyPose(head, XRNode.CenterEye);
		ApplyPose(leftControllerObject ? leftControllerObject.transform : null, XRNode.LeftHand);
		ApplyPose(rightControllerObject ? rightControllerObject.transform : null, XRNode.RightHand);
	}

	internal void ApplyBeforeRenderPose() {
		ApplyTrackedPoses();
		ApplyLatePlayerAnchor();
		ApplyPlayerBodyViewPose();
		ApplyHetoorPose();
		PlayerDialogueAdapter.Apply(head);
		leftController?.RefreshRayGeometry();
		rightController?.RefreshRayGeometry();
		_bodyIk.SolveBeforeRender();
		SetUiOverlayMode(camera && !GameContext.IsPanelMode);
		UiOverlayPatch.UpdateHetoorFinalFlip();
		ApplyAuthoredCameraPose();
		// Refresh the screen after all late player and camera corrections.
		VirtualScreen.Instance?.RefreshPose(true);
	}

	private void ApplyLatePlayerAnchor() {
		if (mode is not(GameMode.StandardPlayer or GameMode.PlayerAnimation) || GameContext.UsesPlayerBodyView || !playerMove || !Body || !_trackingCalibrated) return;

		// Refresh the player anchor after scene-owned camera motion.
		var yawSource = mode == GameMode.StandardPlayer ? playerMove.transform.eulerAngles.y : camera ? camera.transform.eulerAngles.y : playerMove.transform.eulerAngles.y;
		var playerYaw = Quaternion.Euler(0f, yawSource, 0f);
		var rigYaw = Quaternion.Euler(0f, GetPlayerTrackingYaw(yawSource, mode == GameMode.StandardPlayer), 0f);
		var positionReference = mode == GameMode.StandardPlayer ? _roomscalePositionReference : _trackingReference;
		var headPlacementYaw = mode == GameMode.StandardPlayer ? rigYaw : playerYaw;
		var authoredHeadPosition = GetAuthoredHeadPosition(headPlacementYaw);
		Body.SetPositionAndRotation(authoredHeadPosition - rigYaw * Vector3.Scale(positionReference, Body.localScale) + playerYaw * positionOffset, rigYaw);
	}

	private void ApplyHetoorPose() {
		var hetoor = GameContext.HetoorPlayer;
		if (mode != GameMode.Hetoor || !hetoor || !camera || !Body || !_trackingCalibrated) return;

		// Keep controller aim from rotating the HMD rig.
		var rotation = Quaternion.Euler(0f, hetoor.transform.eulerAngles.y, 0f);
		Body.SetPositionAndRotation(camera.transform.position - rotation * Vector3.Scale(_trackingReference, Body.localScale), rotation);
	}

	private void ApplyAuthoredCameraPose() {
		var poseCamera = GameContext.AuthoredPoseCamera;
		if (mode is not(GameMode.SpaceRacer or GameMode.MakeManeken or GameMode.GlitchMinigame) || !poseCamera || !Body || !_trackingCalibrated) {
			_staticGlitchMinigame = null;
			return;
		}

		// Preserve authored roll and pitch except for the top-down glitch board.
		var rotation = mode == GameMode.GlitchMinigame ? Quaternion.Euler(0f, poseCamera.transform.eulerAngles.y, 0f) : poseCamera.transform.rotation;
		var bodyPosition = poseCamera.transform.position - rotation * Vector3.Scale(_trackingReference, Body.localScale);
		var game3 = mode == GameMode.GlitchMinigame ? GlitchMinigamePatch.ActiveGame3 : null;
		if (game3) {
			if (_staticGlitchMinigame != game3) {
				_staticGlitchMinigame = game3;
				_staticGlitchMinigameBodyRotation = rotation;
				_staticGlitchMinigameBodyPosition = bodyPosition - rotation * Vector3.forward * GlitchMinigame3CameraBack;
			}
			Body.SetPositionAndRotation(_staticGlitchMinigameBodyPosition, _staticGlitchMinigameBodyRotation);
		} else {
			_staticGlitchMinigame = null;
			Body.SetPositionAndRotation(bodyPosition, rotation);
		}
		poseCamera.stereoTargetEye = StereoTargetEyeMask.None;
		// Keep HMD optics fixed while authored flat-camera FOV changes.
		headCamera.fieldOfView = FieldOfView;
	}

	private Vector3 GetAuthoredHeadPosition(Quaternion yaw) {
		if (GameContext.UsesPlayerBodyView && TryGetAnimatedPlayerHead(out var animatedHead)) return animatedHead;

		if (mode == GameMode.StandardPlayer && playerMove) {
			var anchor = playerPerson ? playerPerson : playerMove.transform;
			var authoredHead = anchor.position + yaw * _playerHeadOffset;
			if (camera && camera.transform.IsChildOf(playerMove.transform)) {
		// Follow HeadPlayer's deliberate vertical crouch animation.
				var cameraEyeHeight = camera.transform.position.y - anchor.position.y;
				if (cameraEyeHeight is > 0.35f and < 2.4f) {
					var loweredBy = _playerHeadOffset.y - cameraEyeHeight;
					var crouchBlend = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.2f, 0.4f, loweredBy));
					authoredHead.y = Mathf.Lerp(authoredHead.y, camera.transform.position.y, crouchBlend);
				}
			}
			return authoredHead;
		}

		return camera ? camera.transform.position : playerMove.transform.position + Vector3.up * 1.7f;
	}

	internal void ApplyPlayerBodyViewPose() {
		if (!GameContext.UsesPlayerBodyView || !playerMove || !Body || !_trackingCalibrated || !TryGetAnimatedPlayerHead(out var animatedHead)) return;

		// Refresh after scene-owned movement and before rendering.
		var sourceYaw = playerMove.transform.eulerAngles.y;
		var playerYaw = Quaternion.Euler(0f, sourceYaw, 0f);
		var rigYaw = Quaternion.Euler(0f, GetPlayerTrackingYaw(sourceYaw, false), 0f);
		Body.SetPositionAndRotation(animatedHead - rigYaw * Vector3.Scale(_trackingReference, Body.localScale) + playerYaw * positionOffset, rigYaw);
	}

	private bool TryGetAnimatedPlayerHead(out Vector3 position) {
		position = default;
		if (!playerPerson) return false;

		var gameIk = playerPerson.GetComponent<PlayerPersonIK>();
		var fullBodyIk = gameIk ? gameIk.scrfbbik : null;
		var references = fullBodyIk ? fullBodyIk.references : null;
		if (references == null || !references.head) return false;

		position = references.head.position;
		return true;
	}

	private void UpdateTurn() {
		var rightStick = VRInput.GetVector2("Turn");
		switch (TurningStyle) {
			case TurnStyle.Snap: UpdateSnapTurn(rightStick.x); break;
			case TurnStyle.Smooth:
				_snapTurnReset = true;
				UpdateSmoothTurn(rightStick.x);
				break;
			case TurnStyle.Disabled: _snapTurnReset = true; break;
		}
	}

	private bool CanUseRoomscale() {
		return mode == GameMode.StandardPlayer && playerMove && localPlayer && !playerMove.animationRun && !playerMove.dontMove;
	}

	internal void ApplyRoomscaleMovement(PlayerMove source) {
		if (source != playerMove || !_trackingCalibrated || !Body || !CanUseRoomscale() || !playerMove.rb) return;
		ConfirmRoomscaleMovement();

		var localDelta = head.localPosition - _roomscalePositionReference;
		localDelta.y = 0f;
		var rigYaw = Body.rotation;
		var worldDelta = rigYaw * Vector3.Scale(localDelta, Body.localScale);
		if (_roomscaleBlocked) {
			var blockedAmount = Vector3.Dot(worldDelta, _roomscaleBlockedDirection);
			if (blockedAmount <= 0.01f) {
				_roomscaleBlocked = false;
			} else {
				worldDelta -= _roomscaleBlockedDirection * blockedAmount;
			}
		}
		var distance = worldDelta.magnitude;
		if (distance < 0.001f) return;

		var fixedDelta = Mathf.Max(Time.fixedDeltaTime, 0.001f);
		var requestedDelta = Vector3.ClampMagnitude(worldDelta, MaximumRoomscaleVelocity * fixedDelta);
		var requestedDistance = requestedDelta.magnitude;
		var requestedDirection = requestedDelta / requestedDistance;
		_roomscaleSolidContact = false;
		if (playerMove.rb.SweepTest(requestedDirection, out var hit, requestedDistance, QueryTriggerInteraction.Ignore)) {
			_roomscaleSolidContact = true;
			_roomscaleSolidContactDistance = Mathf.Clamp(hit.distance, 0f, requestedDistance);
			var probeDistance = Mathf.Clamp(_roomscaleSolidContactDistance + RoomscaleTriggerProbeDepth, 0f, requestedDistance);
			requestedDelta = requestedDirection * probeDistance;
			_roomscaleBlocked = true;
			_roomscaleBlockedDirection = requestedDirection;
			if (probeDistance < 0.001f) return;
		}

		// Add roomscale displacement without replacing MiSide's Rigidbody velocity.
		_roomscaleMoveStart = playerMove.rb.position;
		_roomscaleRequestedDelta = requestedDelta;
		_roomscaleExpectedGameDelta = playerMove.rb.velocity * fixedDelta;
		_roomscaleRequestYaw = rigYaw;
		_roomscaleRequestScale = Mathf.Max(Body.localScale.x, 0.001f);
		playerMove.rb.position += requestedDelta;
		_roomscaleMovePending = true;
	}

	private void ConfirmRoomscaleMovement() {
		if (!_roomscaleMovePending) return;
		_roomscaleMovePending = false;
		if (!playerMove || !playerMove.rb) return;

		var requestedDistance = _roomscaleRequestedDelta.magnitude;
		if (requestedDistance < 0.0001f) return;

		var direction = _roomscaleRequestedDelta / requestedDistance;
		// Remove expected Rigidbody movement before measuring accepted roomscale movement.
		var actualDelta = playerMove.rb.position - _roomscaleMoveStart - _roomscaleExpectedGameDelta;
		var measuredDistance = Vector3.Dot(actualDelta, direction);
		var confirmedDistance = _roomscaleSolidContact ? Mathf.Min(_roomscaleSolidContactDistance, requestedDistance) : Mathf.Clamp(measuredDistance, 0f, requestedDistance);
		var movementBlocked = _roomscaleSolidContact || confirmedDistance < requestedDistance * 0.25f;
		if (movementBlocked) {
			_roomscaleBlocked = true;
			_roomscaleBlockedDirection = direction;
			if (_roomscaleSolidContact && measuredDistance < confirmedDistance) {
		// Do not count trigger-probe depenetration as roomscale movement.
				playerMove.rb.position += direction * (confirmedDistance - measuredDistance);
			}
		}
		if (confirmedDistance < 0.0001f) return;

		var confirmedWorldDelta = direction * confirmedDistance;
		var confirmedLocalDelta = Quaternion.Inverse(_roomscaleRequestYaw) * confirmedWorldDelta / _roomscaleRequestScale;
		_roomscalePositionReference.x += confirmedLocalDelta.x;
		_roomscalePositionReference.z += confirmedLocalDelta.z;
	}

	private void UpdateRoomscaleRotation() {
		if (!head) {
			_roomscalePendingYaw = 0f;
			_roomscaleControlActive = false;
			return;
		}

		var currentHeadYaw = GetPlanarYaw(head.localRotation, _roomscaleLastHeadYaw);
		if (!_roomscaleYawCalibrated) {
			_roomscaleLastHeadYaw = currentHeadYaw;
			_roomscaleYawCalibrated = true;
		}

		if (!CanUseRoomscale()) {
			_roomscalePendingYaw = 0f;
			_roomscaleControlActive = false;
			_roomscaleLastHeadYaw = currentHeadYaw;
			return;
		}
		if (!_roomscaleControlActive) {
		// Align movement with the tracked view when player control begins.
			AlignPlayerYawToHead();
			_roomscaleControlActive = true;
			_roomscaleLastHeadYaw = currentHeadYaw;
			return;
		}

		var delta = Mathf.DeltaAngle(_roomscaleLastHeadYaw, currentHeadYaw);
		_roomscaleLastHeadYaw = currentHeadYaw;

		_roomscalePendingYaw += delta;
		if (Mathf.Abs(_roomscalePendingYaw) < RoomscaleRotationStep) return;

		var rotation = _roomscalePendingYaw;
		_roomscalePendingYaw = 0f;
		playerMove.TeleportRotate(localPlayer.transform.eulerAngles.y + rotation);
		_roomscaleConsumedYaw += rotation;
	}

	private void AlignPlayerYawToHead() {
		if (!playerMove || !head) return;

		// Keep tracking-space yaw independent from HMD and authored rotations.
		var rigYaw = GetPlayerTrackingYaw(playerMove.transform.eulerAngles.y, false);
		var headWorldYaw = GetPlanarYaw(Quaternion.Euler(0f, rigYaw, 0f) * head.localRotation, playerMove.transform.eulerAngles.y);
		playerMove.TeleportRotate(headWorldYaw);
		_roomscaleConsumedYaw = Mathf.DeltaAngle(rigYaw, headWorldYaw);
	}

	private float GetPlayerTrackingYaw(float sourceYaw, bool updateFromPlayer) {
		if (!_playerTrackingYawValid) {
			_playerTrackingYaw = sourceYaw - _roomscaleConsumedYaw;
			_playerTrackingYawValid = true;
		}

		if (mode == GameMode.PlayerAnimation) {
			if (!_playerAnimationYawActive) {
				_playerAnimationYawActive = true;
				_playerAnimationLastSourceYaw = sourceYaw;
			} else {
		// Preserve tracking yaw while following later cutscene rotation.
				_playerTrackingYaw += Mathf.DeltaAngle(_playerAnimationLastSourceYaw, sourceYaw);
				_playerAnimationLastSourceYaw = sourceYaw;
			}
		} else if (updateFromPlayer) {
		// Physical HMD rotation is already included in the consumed roomscale yaw.
			_playerTrackingYaw = sourceYaw - _roomscaleConsumedYaw;
		}
		return _playerTrackingYaw;
	}

	internal void NotifyTrackingOriginUpdated() {
		_trackingOriginUpdatePending = true;
		VirtualScreen.Instance?.ResetComfortAnchor();
	}

	private void DetectTrackingOriginDiscontinuity() {
		if (!head) return;

		var position = head.localPosition;
		var rotation = head.localRotation;
		if (!_rawHeadPoseValid) {
			_lastRawHeadPosition = position;
			_lastRawHeadRotation = rotation;
			_rawHeadPoseValid = true;
			return;
		}

		var positionDelta = Vector3.Distance(_lastRawHeadPosition, position);
		var rotationDelta = Quaternion.Angle(_lastRawHeadRotation, rotation);
		_lastRawHeadPosition = position;
		_lastRawHeadRotation = rotation;

		// Detect recentering from the raw XR pose instead of relying on an event.
		var deltaTime = Mathf.Max(Time.unscaledDeltaTime, 1f / 120f);
		var positionRecenter = positionDelta >= RecenterMinimumPositionJump && positionDelta / deltaTime >= RecenterPositionSpeed;
		var rotationRecenter = rotationDelta >= RecenterMinimumRotationJump && rotationDelta / deltaTime >= RecenterRotationSpeed;
		if (!positionRecenter && !rotationRecenter) return;

		if (DebugMode) Log.LogDebug($"[TrackingOrigin] Detected raw HMD pose discontinuity " + $"(position={positionDelta:F2} m, rotation={rotationDelta:F1} deg).");
		NotifyTrackingOriginUpdated();
	}

	private void ApplyTrackingOriginUpdate() {
		if (!_trackingOriginUpdatePending || !head) return;

		var trackedPosition = head.localPosition;
		if (trackedPosition.sqrMagnitude < 0.01f) return;

		_trackingOriginUpdatePending = false;
		// Rebase saved poses after recentering so it is not treated as locomotion.
		_trackingReference = trackedPosition;
		_roomscalePositionReference = trackedPosition;
		_trackingCalibrated = true;
		_roomscaleMovePending = false;
		_roomscaleBlocked = false;
		_roomscalePendingYaw = 0f;
		_roomscaleLastHeadYaw = GetPlanarYaw(head.localRotation, _roomscaleLastHeadYaw);
		_roomscaleYawCalibrated = true;
		if (mode == GameMode.StandardPlayer && playerMove) AlignPlayerYawToHead();
		if (DebugMode) Log.LogDebug($"[VRPlayer] Rebased tracking origin at {trackedPosition}.");
	}

	private static float GetPlanarYaw(Quaternion rotation, float fallback) {
		var forward = rotation * Vector3.forward;
		if (forward.x * forward.x + forward.z * forward.z < 0.0001f) return fallback;
		return Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
	}

	private void CalibrateHeight() {
		if (!Body || !head) {
			Log.LogWarning("[VRPlayer] Height calibration requires an initialized VR rig.");
			return;
		}

		var trackedEyeHeight = head.localPosition.y;
		if (trackedEyeHeight is<MinimumEyeHeight or> MaximumEyeHeight) {
			Log.LogWarning($"[VRPlayer] Height calibration rejected HMD height {trackedEyeHeight:F2} m; sit or stand normally and try again.");
			return;
		}
		if (_playerHeadOffset.y is < 0.8f or > 2.4f) {
			Log.LogWarning($"[VRPlayer] Height calibration rejected MiSide eye height {_playerHeadOffset.y:F2} m.");
			return;
		}

		var scale = Mathf.Clamp(_playerHeadOffset.y / trackedEyeHeight * TrackingHeightComfortScale, MinimumRigScale, MaximumRigScale);
		Body.localScale = Vector3.one * scale;
		VirtualScreen.Instance?.ResetComfortAnchor();
		_roomscaleMovePending = false;
		_roomscaleBlocked = false;
		_trackingCalibrated = false;
		VRInput.TriggerHaptic("Haptic", LeftHanded ? XRNode.LeftHand : XRNode.RightHand, 0.1f, 100f, 0.7f);
		if (DebugMode) Log.LogDebug($"[VRPlayer] Eye height {trackedEyeHeight:F2} m mapped to MiSide's {_playerHeadOffset.y:F2} m player using VR rig scale {scale:F3}.");
	}

	private void UpdateSnapTurn(float turnAxis) {
		if (Mathf.Abs(turnAxis) > StickThreshold) {
			if (!_snapTurnReset) return;

			var direction = turnAxis > 0f ? SnapTurnAngle : -SnapTurnAngle;
			playerMove.TeleportRotate(localPlayer.transform.eulerAngles.y + direction);
			_snapTurnReset = false;
		} else if (Mathf.Abs(turnAxis) < SmoothTurnDeadzone) {
			_snapTurnReset = true;
		}
	}

	private void UpdateSmoothTurn(float turnAxis) {
		var magnitude = Mathf.Abs(turnAxis);
		if (magnitude <= SmoothTurnDeadzone || Time.deltaTime <= 0f) return;

		var normalized = Mathf.InverseLerp(SmoothTurnDeadzone, 1f, magnitude);
		var direction = Mathf.Sign(turnAxis) * normalized;
		var rotation = direction * SmoothTurnSpeed * Time.deltaTime;
		playerMove.TeleportRotate(localPlayer.transform.eulerAngles.y + rotation);
	}

	private static void ApplyPose(Transform target, XRNode node) {
		if (!target) return;
		target.localPosition = InputTracking.GetLocalPosition(node);
		target.localRotation = InputTracking.GetLocalRotation(node);
	}

	public void SetContext(GameMode newMode, PlayerMove newPlayerMove, Camera newCamera) {
		var previousMode = mode;
		var previousCamera = camera;
		var playerChanged = playerMove != newPlayerMove;
		var changed = mode != newMode || playerMove != newPlayerMove || camera != newCamera;
		mode = newMode;
		playerMove = newPlayerMove;
		camera = newCamera;
		localPlayer = playerMove ? playerMove.gameObject : null;
		playerPerson = FindPlayerPerson(playerMove);

		if (playerMove) playerMove.stopMouseMove = true;
		if (changed) {
			CalibratePlayerAnchor();
			_roomscaleMovePending = false;
			_roomscaleBlocked = false;
			_trackingCalibrated = false;
			_roomscaleYawCalibrated = false;
			_roomscaleControlActive = false;
			_roomscalePendingYaw = 0f;
		}
		// Preserve consumed HMD yaw across short gameplay-animation transitions.
		if (playerChanged) {
			_roomscaleConsumedYaw = 0f;
			_playerTrackingYawValid = newPlayerMove;
			_playerTrackingYaw = newPlayerMove ? newPlayerMove.transform.eulerAngles.y : 0f;
		}

		if (newMode == GameMode.PlayerAnimation) {
			var sourceYaw = GameContext.UsesPlayerBodyView && newPlayerMove ? newPlayerMove.transform.eulerAngles.y : newCamera ? newCamera.transform.eulerAngles.y : newPlayerMove ? newPlayerMove.transform.eulerAngles.y : 0f;
			if (previousMode != GameMode.PlayerAnimation || previousCamera != newCamera || playerChanged || !_playerAnimationYawActive) {
				// Capture the first authored cutscene yaw before following later changes.
				var currentHeadYaw = head ? GetPlanarYaw(head.localRotation, _roomscaleConsumedYaw) : _roomscaleConsumedYaw;
				_playerTrackingYaw = sourceYaw - currentHeadYaw;
				_playerTrackingYawValid = newPlayerMove || newCamera;
				_playerAnimationYawActive = true;
				_playerAnimationLastSourceYaw = sourceYaw;
			}
		} else {
			_playerAnimationYawActive = false;
		}
	}

	private static Transform FindPlayerPerson(PlayerMove move) {
		if (!move) return null;

		var person = move.transform.Find("Person");
		if (person && person.GetComponent<PlayerPerson>()) return person;

		var component = move.GetComponentInChildren<PlayerPerson>(true);
		return component ? component.transform : null;
	}

	private void CalibratePlayerAnchor() {
		_playerHeadOffset = new Vector3(0f, 1.7f, 0.1f);
		if (mode != GameMode.StandardPlayer || !playerMove || !camera) return;

		var anchor = playerPerson ? playerPerson : playerMove.transform;
		var yaw = Quaternion.Euler(0f, playerMove.transform.eulerAngles.y, 0f);
		var measuredOffset = Quaternion.Inverse(yaw) * (camera.transform.position - anchor.position);
		if (measuredOffset.y is > 0.8f and < 2.4f) _playerHeadOffset = measuredOffset;

		if (DebugMode) Log.LogDebug($"[VRPlayer] Player anchor={(playerPerson ? playerPerson.name : playerMove.name)}, head offset={_playerHeadOffset}.");
	}

	private void OnDestroy() {
		UiOverlayPatch.ReleaseHetoorFinalFlip();
		HeadsetOutlinePostprocess.Release();
		_bodyIk.Dispose();
		if (Instance == this) {
			TrackingOriginMonitor.Dispose();
			Instance = null;
		}
	}
}
