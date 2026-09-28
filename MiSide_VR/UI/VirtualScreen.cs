using System;
using MiSide_VR.Core;
using UnityEngine;
using UnityEngine.UI;

namespace MiSide_VR.UI;

public sealed class VirtualScreen : MonoBehaviour {
	public VirtualScreen(IntPtr value) : base(value) {}

	public const float PixelWidth = 1920f;
	public const float PixelHeight = 1080f;

	public static VirtualScreen Instance { get; private set; }

	private RectTransform _rect;
	private RawImage _image;
	private float _anchorEyeHeight;
	private bool _anchorCalibrated;
	private bool _comfortAnchorValid;
	private Vector3 _comfortEyePosition;
	private Vector3 _comfortTargetPosition;
	private float _comfortLockedEyeHeight;
	private float _comfortYaw;
	private float _comfortTargetYaw;
	private int _comfortUpdateFrame = -1;

	private const float ScreenDistance = 2.25f;
	private const float ScreenWidth = 2.6f;
	private const float TabletDistance = 1.05f;
	private const float TabletCenterBelowEyes = 0.34f;
	private const float TabletWidth = 1.25f;
	private const float ComfortPositionDeadzone = 0.25f;
	private const float ComfortYawDeadzone = 25f;
	private const float ComfortPositionSpeed = 1.5f;
	private const float ComfortYawSpeed = 120f;
	private const float ComfortSnapDistance = 1.5f;
	private const float ComfortSnapYaw = 75f;

	private void Awake() {
		Instance = this;
		_rect = GetComponent<RectTransform>();
		_image = GetComponentInChildren<RawImage>(true);
	}

	internal void Bind(RenderTexture texture) {
		if (!_rect) _rect = GetComponent<RectTransform>();
		if (!_image) _image = GetComponentInChildren<RawImage>(true);
		if (_image) _image.texture = texture;
	}

	internal void ResetComfortAnchor() {
		_comfortAnchorValid = false;
		_comfortUpdateFrame = -1;
	}

	private void LateUpdate() => RefreshPose(false);

	internal void RefreshPose(bool rigPoseReady) {
		var player = VRPlayer.Instance;
		var rig = player ? player.Body : null;
		var eye = player ? player.headCamera : null;
		if (!rig || !eye || !_rect) return;

		if (!_anchorCalibrated) {
			var trackedEye = rig.InverseTransformPoint(eye.transform.position);
			if (Mathf.Abs(trackedEye.y) < 0.1f) return;
			_anchorEyeHeight = trackedEye.y;
			_anchorCalibrated = true;
		}

		if (GameContext.IsPanelMode) {
			_comfortAnchorValid = false;
			transform.position = rig.TransformPoint(new Vector3(0f, _anchorEyeHeight, ScreenDistance));
			transform.rotation = Quaternion.LookRotation(rig.forward, rig.up);
			transform.localScale = Vector3.one * (ScreenWidth / PixelWidth);
			return;
		}
		
		// get scene height after VRPlayer updates its pose
		if (!_comfortAnchorValid && !rigPoseReady) return;
		UpdateComfortAnchor(eye.transform, rig);
		var comfortRotation = Quaternion.Euler(0f, rig.eulerAngles.y + _comfortYaw, 0f);
		var comfortEye = rig.TransformPoint(_comfortEyePosition);
		var scale = rig.localScale.x;

		var position = comfortEye + comfortRotation * new Vector3(0f, -TabletCenterBelowEyes * scale, TabletDistance * scale);
		transform.position = position;
		transform.rotation = Quaternion.LookRotation(position - comfortEye, Vector3.up);
		transform.localScale = Vector3.one * (TabletWidth / PixelWidth);
	}

	private void UpdateComfortAnchor(Transform eye, Transform rig) {
		// smooth only tracked head motion relative to the rig
		var eyePosition = rig.InverseTransformPoint(eye.position);
		var flatForward = Vector3.ProjectOnPlane(rig.InverseTransformDirection(eye.forward), Vector3.up);
		var eyeYaw = flatForward.sqrMagnitude > 0.0001f ? Mathf.Atan2(flatForward.x, flatForward.z) * Mathf.Rad2Deg : _comfortYaw;

		if (!_comfortAnchorValid) {
			_comfortLockedEyeHeight = eyePosition.y;
			_comfortEyePosition = new Vector3(0f, eyePosition.y, 0f);
			_comfortTargetPosition = _comfortEyePosition;
			_comfortYaw = 0f;
			_comfortTargetYaw = 0f;
			_comfortAnchorValid = true;
			_comfortUpdateFrame = Time.frameCount;
			return;
		}
		if (_comfortUpdateFrame == Time.frameCount) return;
		_comfortUpdateFrame = Time.frameCount;
		eyePosition.y = _comfortLockedEyeHeight;
		
		var scale = Mathf.Max(rig.localScale.x, 0.001f);
		if (Vector3.Distance(_comfortEyePosition, eyePosition) * scale > ComfortSnapDistance || Mathf.Abs(Mathf.DeltaAngle(_comfortYaw, eyeYaw)) > ComfortSnapYaw) {
			_comfortEyePosition = eyePosition;
			_comfortTargetPosition = eyePosition;
			_comfortYaw = eyeYaw;
			_comfortTargetYaw = eyeYaw;
			return;
		}

		if (Vector3.Distance(_comfortTargetPosition, eyePosition) * scale > ComfortPositionDeadzone) _comfortTargetPosition = eyePosition;
		if (Mathf.Abs(Mathf.DeltaAngle(_comfortTargetYaw, eyeYaw)) > ComfortYawDeadzone) _comfortTargetYaw = eyeYaw;

		var deltaTime = Time.unscaledDeltaTime;
		_comfortEyePosition = Vector3.MoveTowards(_comfortEyePosition, _comfortTargetPosition, ComfortPositionSpeed * deltaTime / scale);
		_comfortYaw = Mathf.MoveTowardsAngle(_comfortYaw, _comfortTargetYaw, ComfortYawSpeed * deltaTime);
	}

	public static bool TryGetPointerPosition(Ray ray, out Vector2 pixels) => TryGetPointerHit(ray, out pixels, out _);

	public static bool TryGetPointerHit(Ray ray, out Vector2 pixels, out Vector3 worldPoint) { return TryProjectPointer(ray, out pixels, out worldPoint) && pixels.x >= 0f && pixels.x <= PixelWidth && pixels.y >= 0f && pixels.y <= PixelHeight; }
	
	internal static bool TryProjectPointer(Ray ray, out Vector2 pixels, out Vector3 worldPoint) {
		pixels = default;
		worldPoint = default;
		var screen = Instance;
		if (!screen || !screen._rect || !screen.gameObject.activeInHierarchy) return false;

		var plane = new Plane(screen.transform.forward, screen.transform.position);
		if (!plane.Raycast(ray, out var distance) || distance <= 0f) return false;

		worldPoint = ray.GetPoint(distance);
		var local = screen.transform.InverseTransformPoint(worldPoint);
		var x = local.x + PixelWidth * 0.5f;
		var y = local.y + PixelHeight * 0.5f;
		pixels = new Vector2(x, y);
		return true;
	}

	private void OnDestroy() { if (Instance == this) Instance = null; }
}
