using System;
using Il2CppInterop.Runtime.Attributes;
using MiSide_VR.Core;
using UnityEngine;
using MiSide_VR.UI;
using MiSide_VR.UI.Patches;
using UnityEngine.Rendering;
using UnityEngine.XR;
using Valve.VR;
using static MiSide_VR.Plugin;

namespace MiSide_VR.Input;

public class VRController : MonoBehaviour {
	public VRController(IntPtr value) : base(value) {}

	public enum HandType { Left, Right }

	public HandType controllerHandType;
	public Transform model;
	private LineRenderer _ray;
	private bool _aimAttachmentResolved;
	private float _nextAttachmentRetryTime;
	public Transform muzzle;
	public LayerMask rayCastMask = LayerMask.GetMask("Default", "Mob", "Item");
	public Shader savedLaserShader;

	public bool uiMode;
	public bool hideLaser;

	public Ray AimRay => new(muzzle.position, muzzle.forward);
	private const float AimPitchDown = 40f;
	private const float FallbackAimInset = 0.0254f;
	private const float FallbackAimDown = 0.0254f;
	private const float LaserLength = 3.5f;
	public bool IsPointingAtInteractableUI { get; private set; }

	public void Setup(HandType handType) {
		controllerHandType = handType;

		var modelObj = new GameObject("Model");
		modelObj.transform.SetParent(transform, false);
		modelObj.layer = VRPlayer.VrUiLayer;
		modelObj.transform.localPosition = Vector3.zero;
		model = modelObj.transform;

		var muzzleObj = new GameObject("Muzzle");
		muzzleObj.transform.SetParent(model, false);
		muzzleObj.layer = VRPlayer.VrPointerLayer;
		muzzleObj.transform.localPosition = new Vector3(handType == HandType.Left ? FallbackAimInset : -FallbackAimInset, -FallbackAimDown, 0f);
		muzzleObj.transform.localRotation = Quaternion.Euler(AimPitchDown, 0f, 0f);
		muzzle = muzzleObj.transform;

		_ray = muzzleObj.AddComponent<LineRenderer>();
		_ray.startWidth = 0.002f;
		_ray.endWidth = 0.0006f;
		_ray.startColor = new Color(1f, 1f, 1f, 0.9f);
		_ray.endColor = new Color(1f, 1f, 1f, 0f);
		_ray.positionCount = 2;
		_ray.useWorldSpace = false;
		_ray.sortingOrder = 32767;

		var laserShader = Shader.Find("Hidden/Internal-Colored") ?? Shader.Find("Sprites/Default");
		var lineMaterial = new Material(laserShader);
		lineMaterial.color = Color.white;
		lineMaterial.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
		lineMaterial.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
		lineMaterial.SetInt("_Cull", (int)CullMode.Off);
		lineMaterial.SetInt("_ZWrite", 0);
		lineMaterial.renderQueue = 5000;
		_ray.material = lineMaterial;
		savedLaserShader = lineMaterial.shader;
	}

	public void LateUpdate() {
		MenuSpinPatch.ApplyLate();
		if (!_ray || !_ray.gameObject) return;
		ResolveAimAttachment();

		if (hideLaser) {
			_ray.enabled = false;
			return;
		}

		_ray.material.shader = savedLaserShader;
		var overMonitor = VirtualScreen.TryGetPointerPosition(AimRay, out var pointerPixels);
		IsPointingAtInteractableUI = overMonitor && CanvasPatch.IsPointerOverInteractable(pointerPixels);
		_ray.gameObject.layer = IsPointingAtInteractableUI ? VRPlayer.VrUiLayer : VRPlayer.VrPointerLayer;
		_ray.material.SetInt("_ZTest", (int)(IsPointingAtInteractableUI ? CompareFunction.Always : CompareFunction.LessEqual));
		_ray.material.renderQueue = IsPointingAtInteractableUI ? 5000 : 3000;

		_ray.enabled = true;
		RefreshRayGeometry();
	}

	[HideFromIl2Cpp]
	public bool TryGetGripPose(out TrackedPose pose) {
		var node = controllerHandType == HandType.Left ? XRNode.LeftHand : XRNode.RightHand;
		return VRInput.TryGetControllerComponentPose(node, OpenVR.k_pch_Controller_Component_HandGrip, out pose) || VRInput.TryGetControllerComponentPose(node, OpenVR.k_pch_Controller_Component_OpenXR_Grip, out pose);
	}

	[HideFromIl2Cpp]
	public bool TryGetAimPose(out TrackedPose pose) {
		var node = controllerHandType == HandType.Left ? XRNode.LeftHand : XRNode.RightHand;
		return VRInput.TryGetControllerComponentPose(node, OpenVR.k_pch_Controller_Component_OpenXR_Aim, out pose) || VRInput.TryGetControllerComponentPose(node, OpenVR.k_pch_Controller_Component_Tip, out pose);
	}

	private void ResolveAimAttachment() {
		if (_aimAttachmentResolved || Time.unscaledTime < _nextAttachmentRetryTime) return;

		_nextAttachmentRetryTime = Time.unscaledTime + 1f;
		if (!TryGetAimPose(out var pose)) return;

		muzzle.localPosition = pose.Position;
		muzzle.localRotation = pose.Rotation;
		_aimAttachmentResolved = true;
		if (DebugMode) Log.LogDebug($"[VRController] {controllerHandType} SteamVR aim attachment position={pose.Position}, rotation={pose.Rotation.eulerAngles}");
	}

	public void RefreshRayGeometry() {
		if (!_ray || !_ray.enabled || !muzzle) return;

		_ray.SetPosition(0, Vector3.zero);
		var hitPoint = GetRayHitPosition();
		var visibleDistance = Mathf.Min(Vector3.Distance(muzzle.position, hitPoint), LaserLength);
		_ray.SetPosition(1, muzzle.InverseTransformPoint(AimRay.GetPoint(visibleDistance)));
	}

	public Vector3 GetRayHitPosition() {
		const float maxDistance = 300f;
		var ray = AimRay;
		var bestDistance = maxDistance;
		var bestPoint = ray.GetPoint(maxDistance);

		if (Physics.Raycast(ray, out var hitInfo, maxDistance, rayCastMask)) {
			bestDistance = hitInfo.distance;
			bestPoint = hitInfo.point;
		}

		if (VirtualScreen.TryGetPointerHit(ray, out var pointerPixels, out var panelPoint) && CanvasPatch.IsPointerOverInteractable(pointerPixels)) {
			var panelDistance = Vector3.Distance(ray.origin, panelPoint);
			if (panelDistance < bestDistance) bestPoint = panelPoint;
		}

		return bestPoint;
	}

	public Vector3 GetRayHitPosition(float maxDistance) {
		var ray = AimRay;
		if (Physics.Raycast(ray, out var hitInfo, maxDistance, rayCastMask)) return hitInfo.point;
		return ray.GetPoint(maxDistance);
	}
}
