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
	private const float YawDeadzone = 15f;

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
		
		Vector3 targetPosition = _vrCamera.transform.position + _vrCamera.transform.forward * UIDistance;
		transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * SmoothSpeed);
		
		float cameraYaw = _vrCamera.transform.eulerAngles.y;
		float hudYaw = transform.eulerAngles.y;
		float yawDelta = Mathf.DeltaAngle(hudYaw, cameraYaw);
		
		if (Mathf.Abs(yawDelta) > YawDeadzone) 
			transform.rotation = Quaternion.Euler(0f, Mathf.LerpAngle(hudYaw, cameraYaw, Time.deltaTime * SmoothSpeed), 0f);
	}
	
	private void Initialize () {
		if (!_vrCamera && VRPlayer.Instance && VRPlayer.Instance.StereoRender) {
			_vrCamera = VRPlayer.Instance.StereoRender.headCamera;
			_isInitialized = _vrCamera;
		}
	}
} 