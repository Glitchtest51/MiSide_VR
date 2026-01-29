using System;
using Il2CppSystem.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using Valve.VR;

namespace MiSide_VR.VRInput;

public class ControllerTracking : MonoBehaviour {
    public ControllerTracking(IntPtr value) : base(value) { }

    public HandType controllerHandType;
    public Transform model;
    public SteamVR_Behaviour_Pose pose;
    private LineRenderer _ray;
    public Transform muzzle;
    public Camera eventCamera;
    public EventSystem eventSystem;
    public StandaloneInputModule inputModule;
    public LayerMask rayCastMask = -967074285;
    public Shader savedLaserShader;

    public bool uiMode;
    public bool hideLaser;

    private Ray AimRay => new(muzzle.position, muzzle.forward);

    public enum HandType {Left, Right}
    
    public void Setup(HandType handType) {
        controllerHandType = handType;

        var modelObj = new GameObject("Model");
        modelObj.transform.SetParent(transform, false);
        modelObj.transform.localPosition = Vector3.zero;
        model = modelObj.transform;
        
        var muzzleObj = new GameObject("Muzzle");
        muzzleObj.transform.SetParent(model, false);
        muzzleObj.transform.localPosition = Vector3.zero;
        muzzleObj.transform.localRotation = Quaternion.identity;
        muzzle = muzzleObj.transform;
        
        _ray = muzzleObj.AddComponent<LineRenderer>();
        _ray.startWidth = 0.002f;
        _ray.endWidth = 0.002f;
        _ray.positionCount = 2;
        _ray.sortingOrder = 999;
        
        var lineMaterial = new Material(Shader.Find("Sprites/Default"));
        lineMaterial.color = Color.red;
        _ray.material = lineMaterial;
        savedLaserShader = lineMaterial.shader;
        
        eventCamera = muzzleObj.AddComponent<Camera>();
        eventCamera.enabled = false;
        eventCamera.cullingMask = 1 << LayerMask.NameToLayer("UI");
        
        pose = transform.gameObject.GetOrAddComponent<SteamVR_Behaviour_Pose>();
        pose.poseAction = SteamVR_Actions.Gameplay.Pose;
        pose.inputSource = (controllerHandType == HandType.Left) ? SteamVR_Input_Sources.LeftHand : SteamVR_Input_Sources.RightHand;
        pose.origin = transform.parent;
    }

    public void SetupEventSystem(EventSystem eventSystem, StandaloneInputModule inputModule) {
        this.eventSystem = eventSystem;
        this.inputModule = inputModule;
    }

    public void SetUIMode(bool uiMode) {
        this.uiMode = uiMode;
    }

    public void LateUpdate() {
        if (!_ray || !_ray.gameObject) return;
    
        if (hideLaser) {
            _ray.enabled = false;
            return;
        }
        
        if (uiMode) 
            // _ray.material.shader = VRAssets.LaserUnlit;
            _ray.material.shader = savedLaserShader;
        else 
            _ray.material.shader = savedLaserShader;
        

        _ray.enabled = true;
        _ray.SetPosition(0, muzzle.position);
        _ray.SetPosition(1, GetRayHitPosition());
    }

    public Vector3 GetRayHitPosition() {
        if (uiMode)
            return GetCanvasHitEnd();

        return GetRayHitPosition(300);
    }

    public Vector3 GetRayHitPosition(float maxDistance) {
        var ray = AimRay;
        if (Physics.Raycast(ray, out var hitInfo, maxDistance, rayCastMask)) 
            return hitInfo.point;
        return ray.origin + (ray.direction * maxDistance);
    }

    private Vector3 GetCanvasHitEnd() {
        var distance = GetCanvasDistance();
        return CalculateEnd(distance);
    }

    private float GetCanvasDistance() {
        if (!eventSystem || !inputModule || !inputModule.inputOverride)
            return 0f;

        var eventData = new PointerEventData(eventSystem) {
            position = inputModule.inputOverride.mousePosition
        };

        var results = new List<RaycastResult>();
        eventSystem.RaycastAll(eventData, results);

        var closestResult = FindFirstRaycast(results);
        var distance = closestResult.distance;

        distance = Valve.VR.Mathf.Clamp(distance, 0.0f, 5);

        return distance;
    }

    private static RaycastResult FindFirstRaycast(List<RaycastResult> results) {
        foreach (var result in results) {
            if (!result.gameObject)
                continue;
            return result;
        }
        return new RaycastResult();
    }

    private Vector3 CalculateEnd(float length) {
        return muzzle.position + muzzle.forward * length;
    }
}