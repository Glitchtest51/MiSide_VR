using System;
using System.Collections.Generic;
using MiSide_VR.VR;
using UnityEngine;

namespace MiSide_VR.UI;

public class UIFollowCamera : MonoBehaviour {
    public UIFollowCamera(IntPtr value) : base(value) { }
    
    private Camera _vrCamera;
    private bool _shouldFollowCamera = true;
    private bool _isInitialized;
    private bool _needsInitialSnap;
    
    private const float UIDistance = 1f;
    private const float SmoothSpeed = 10f;

    private void Awake() {
        CacheCamera();
        DetermineFollowBehavior();
    }

    private void Start() {
        if (!_isInitialized) {
            CacheCamera();
            DetermineFollowBehavior();
        }
    }

    private void LateUpdate() {
        if (!_shouldFollowCamera) return;
        if (!_vrCamera) CacheCamera();
        if (!_vrCamera) return;
        
        if (_needsInitialSnap) {
            transform.position = _vrCamera.transform.position + _vrCamera.transform.forward * UIDistance;
            transform.rotation = _vrCamera.transform.rotation;
            _needsInitialSnap = false;
            return;
        }

        var targetPosition = _vrCamera.transform.position + _vrCamera.transform.forward * UIDistance;
        var targetRotation = _vrCamera.transform.rotation;

        transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * SmoothSpeed);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * SmoothSpeed);
    }

    private void OnEnable() {
        _needsInitialSnap = true;
    }

    private void CacheCamera() {
        if (!_vrCamera) {
            _vrCamera = VRPlayer.Instance.StereoRender.headCamera;
            _isInitialized = _vrCamera;
        }
    }

    private void DetermineFollowBehavior() {
        var canvasName = gameObject.name;
        
        if (IsWorldLockedUI(canvasName)) 
            _shouldFollowCamera = false;
    }
    

    private static bool IsWorldLockedUI(string canvasName)
    {
        if (WorldLockedUINames.Contains(canvasName))
            return true;
        
        foreach (string keyword in WorldLockedKeywords) {
            if (canvasName.Contains(keyword))
                return true;
        }

        return false;
    }
    
    private static readonly HashSet<string> WorldLockedUINames = [
        // "GlitchTerminalActivatorUI_Ammo(Clone)",
        // "ExchangeOfflineUI(Clone)",
        // "DeathObscurer",
        // "KnockedOutUI(Clone)"
    ];

    // Keywords that indicate world-locked UI
    private static readonly string[] WorldLockedKeywords = [
        // "Activate",
        // "Popup"
    ];
}