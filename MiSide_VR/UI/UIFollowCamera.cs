using System;
using MiSide_VR.Core;
using UnityEngine;

namespace MiSide_VR.UI;

public class UIFollowCamera: MonoBehaviour {
	public UIFollowCamera(IntPtr value): base(value) { }

	private Camera _vrCamera;
	private bool _isInitialized;

	private const float UIDistance = 1f;
	private const float SmoothSpeed = 10f;

	private void Awake() {
		Initialize();
	}

	private void Start() {
		if (!_isInitialized) {
			Initialize();
		}
	}

	private void LateUpdate() {
		Initialize();
		if (!_vrCamera) return;

		var targetPosition = _vrCamera.transform.position + _vrCamera.transform.forward * UIDistance;
		var targetRotation = _vrCamera.transform.rotation;

		transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * SmoothSpeed);
		transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * SmoothSpeed);
	}
	
	private void Initialize () {
		if (!_vrCamera && VRPlayer.Instance && VRPlayer.Instance.StereoRender) {
			_vrCamera = VRPlayer.Instance.StereoRender.headCamera;
			_isInitialized = _vrCamera;
		}
	}
} 