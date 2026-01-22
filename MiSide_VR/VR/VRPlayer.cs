using System;
using UnityEngine;
using Valve.VR;
using System.Collections;
using UnityEngine.SceneManagement;
using static MiSide_VR.Plugin;
using Mathf = UnityEngine.Mathf;

namespace MiSide_VR.VR;

public class VRPlayer : MonoBehaviour {
    public VRPlayer(IntPtr value) : base(value) { }
    
    public static VRPlayer Instance { get; private set; }

    public Transform Origin { get; private set; }
    
    public Transform Body { get; private set; }
    
    public Camera Camera { get; private set; }
    
    public Camera FPSCam { get; private set; }
    
    public StereoRender StereoRender { get; private set; }
    
    public string lastCameraUsed = "";
    
    
    public bool isUIMode = false;
    
    public GameObject LocalPlayer;
    
    private Camera UsedCamera = null;
    
    private string UsedSceneName = "";
    
    public string lastCameraName = "";
    
    
    // private Il2CppFirstPersonCharacter firstPersonCharacter;

    // === CONFIGURABLE ALIGNMENT OFFSETS ===
    
    public Vector3 positionOffset = new Vector3(0.0f, 0.05f, -0.10f);
    
    public float yawOffset = 0f;
    
    public float bodyRotationSpeed = 10f;

    // === INPUT SMOOTHING ===

    private Vector2 smoothedInput = Vector2.zero;
    private float inputSmoothSpeed = 8f;

    // === RUN STATE MANAGEMENT ===

    private bool isRunning = false;
    private float lastRunActivationTime = 0f;
    private float runActivationCooldown = 0.25f;

    // === SNAP TURN CONFIGURATION ===

    private float snapTurnOffset = 0f;
    private bool snapTurnReset = true;
    private float snapAngle = 30f;
    private float stickThreshold = 0.7f;
    
    // private Il2CppPlayerAnimationControl _cachedAnimController;
    
    private bool harmonyIsReady = false;
    
    private void Awake() {
        if (Instance != null) {
            Log.LogError("[VRPlayer] ## Duplicate VRPlayer detected! Destroying instance. ##");
            Destroy(gameObject);
            return;
        }

        Log.LogInfo("[VRPlayer] ## Creating VRPlayer instance ##");
        Instance = this;

        // Subscribe to scene load events (currently unused but kept for extensibility)
        // onSceneLoaded += new Action<Scene, LoadSceneMode>(OnSceneLoaded);
        
        // Start async setup after a short delay to ensure game systems are ready
        Setup();
    }
    
    private void OnSceneLoaded(int buildIndex, string sceneName) {
        // Intentionally empty
    }
    
    public void Setup() {
        if (StereoRender != null) {
            Destroy(StereoRender);
            Log.LogInfo("[VRPlayer] #### STEREORENDER DESTROYED ####");
        }

        Body = transform;
        Origin = transform.parent;

        // Ensure the Origin exists as the world root of the VR rig
        if (Origin == null) {
            Origin = new GameObject("[VROrigin]").transform;
            transform.SetParent(Origin, false);
        }

        // Ensure VR rig persists across scene loads
        DontDestroyOnLoad(Origin);

        // Attach stereo rendering system to the body
        StereoRender = Body.gameObject.AddComponent<StereoRender>();
    }
    
    public void SetSceneAndCamera(ActiveSceneWithCamera activeSceneAndCam) {
        UsedSceneName = activeSceneAndCam.scene.name;
        UsedCamera = activeSceneAndCam.camera;
    
        if (UsedCamera != null) {
            // Detach camera from render target (we render via StereoRender instead)
            UsedCamera.targetTexture = null;
            UsedCamera.pixelRect = new Rect(0, 0, 1080, 720); // Optional: small preview
        
            // // Locate the player character in the transform hierarchy
            // LocalPlayer = FindLocalPlayerFromCamera(UsedCamera);
            // if (LocalPlayer != null) {
            //     firstPersonCharacter = LocalPlayer.GetComponent<Il2CppFirstPersonCharacter>();
            // }
            //
            // // Disable mouse-based rotation components
            // DisableMouseRotation();
        }
    }
    
    private void Update() {
        // All VR logic runs in LateUpdate to ensure camera positions are final
    }
    
    private void LateUpdate() {
        HandleUpdate();
    }
    
    private void HandleUpdate() {
        // --- POSITIONING: Sync VR origin to game camera ---
        if (UsedCamera != null && Origin != null) {
            Vector3 basePosition = UsedCamera.transform.position;
            // Apply offset in camera-local space (e.g., lower height, shift back)
            Vector3 finalPosition = basePosition + UsedCamera.transform.TransformDirection(positionOffset);
            Origin.position = finalPosition;
        }
        
        // --- SNAP TURN: Right thumbstick horizontal input ---
        // UnityEngine.Vector2 rightStick = NativeVRInput.GetVector2("ThumbstickRight");
        // if (Mathf.Abs(rightStick.x) > stickThreshold)
        // {
        //     if (snapTurnReset)
        //     {
        //         // Determine turn direction (+30° or -30°)
        //         float direction = (rightStick.x > 0) ? snapAngle : -snapAngle;
        //         snapTurnOffset += direction;
        //
        //         // Rotate the entire VR rig (StereoRender.transform is the root under Body)
        //         if (StereoRender != null)
        //         {
        //             StereoRender.transform.Rotate(0, direction, 0, Space.World);
        //         }
        //
        //         snapTurnReset = false;
        //     }
        // }
        // else if (Mathf.Abs(rightStick.x) < 0.2f)
        // {
        //     // Reset flag when stick is centered, allowing next turn
        //     snapTurnReset = true;
        // }
        
        // --- BODY ALIGNMENT: Rotate player to match HMD forward direction ---
        if (LocalPlayer != null && StereoRender?.Head != null) {
            // StereoRender.Head already includes snap-turn rotation (inherits from parent)
            if (UsedCamera != null) {
                // Sync game camera rotation to HMD (for UI, weapon alignment, etc.)
                UsedCamera.transform.rotation = StereoRender.Head.rotation;
            }
        
            // Project HMD forward onto horizontal plane (ignore vertical look)
            Vector3 headForward = StereoRender.Head.forward;
            headForward.y = 0f;
        
            if (headForward.magnitude > 0.01f) {
                headForward.Normalize();
                Quaternion targetRotation = Quaternion.LookRotation(headForward, Vector3.up);
        
                // Smoothly rotate the player character toward HMD direction
                LocalPlayer.transform.rotation = Quaternion.Slerp(
                    LocalPlayer.transform.rotation,
                    targetRotation,
                    Time.deltaTime * bodyRotationSpeed
                );
            }
        }
        
        // --- LOCOMOTION & RUNNING ---
        // if (firstPersonCharacter != null)
        // {
        //     UnityEngine.Vector2 leftStick = NativeVRInput.GetVector2("ThumbstickLeft");
        //
        //     // Deadzone and clamping
        //     if (leftStick.magnitude < 0.15f)
        //         leftStick = Vector2.zero;
        //     else
        //         leftStick = Vector2.ClampMagnitude(leftStick, 1f);
        //
        //     // Activate running if right stick is pushed forward (y > 0.7) and not turning
        //     if (!isRunning && rightStick.y > 0.7f && Mathf.Abs(rightStick.x) < 0.3f)
        //     {
        //         float currentTime = Time.time;
        //         if (currentTime - lastRunActivationTime > runActivationCooldown)
        //         {
        //             isRunning = true;
        //             lastRunActivationTime = currentTime;
        //             Log.LogInfo("[VRPlayer] Running activated");
        //         }
        //     }
        //
        //     // Deactivate running if moving backward or stopping
        //     if (isRunning && (leftStick.magnitude < 0.01f || leftStick.y < 0f))
        //     {
        //         isRunning = false;
        //     }
        //
        //     // Forward run state to game character controller
        //     firstPersonCharacter.SetRunning(isRunning);
        // }
    }
    
    private GameObject FindLocalPlayerFromCamera(Camera camera) {
        if (camera == null) return null;

        Transform current = camera.transform;
        for (int i = 0; i < 3; i++) {
            current = current.parent;
            if (current == null) break;

            if (current.name == "LookObject") {
                // Climb up until we find "LocalPlayer" or reach the root
                while (current.parent != null && current.parent.name != "LocalPlayer" && current.parent.parent != null)
                    current = current.parent;

                if (current.parent != null && current.parent.name == "LocalPlayer") {
                    Log.LogInfo($"[VRPlayer] Found player root: {current.parent.name}");
                    return current.parent.gameObject;
                }
            }
        }

        Log.LogWarning("[VRPlayer] Could not find LocalPlayer from camera.");
        return null;
    }
    
    private void DisableMouseRotation() {
        // if (LocalPlayer != null)
        // {
        //     var rotator = LocalPlayer.GetComponent<Il2CppSimpleMouseRotator>();
        //     if (rotator != null)
        //     {
        //         rotator.enabled = false;
        //     }
        // }
        //
        // if (UsedCamera != null)
        // {
        //     var camRotator = UsedCamera.GetComponent<Il2CppSimpleMouseRotator>();
        //     if (camRotator != null)
        //     {
        //         camRotator.enabled = false;
        //     }
        // }
    }
    
    private void OnDestroy() {
        Log.LogWarning("[VRPlayer] *** VRPlayer DESTROYED ***");
        // onSceneLoaded -= new Action<Scene, LoadSceneMode>(OnSceneLoaded);
        if (Instance == this)
            Instance = null;
    }
    
    private void SetOriginHome() {
        SetOriginPosRotScl(new Vector3(0f, 0f, 0f), new Vector3(0, 90, 0), new Vector3(1, 1, 1));
    }
    
    public void SetOriginPosRotScl(Vector3 pos, Vector3 euler, Vector3 scale) {
        Origin.position = pos;
        Origin.localEulerAngles = euler;
        Origin.localScale = scale;
    }
    
    public void SetOriginScale(float scale) {
        Origin.localScale = new Vector3(scale, scale, scale);
    }
    
    public Vector3 GetWorldForward() {
        return StereoRender?.Head?.forward ?? Vector3.forward;
    }
    
    public Vector3 GetFlatForwardDirection(Vector3 forward) {
        forward.y = 0;
        return forward.normalized;
    }
    
    public float GetPlayerHeight() {
        if (StereoRender?.Head == null)
            return 1.8f; // Default human height

        return Mathf.Abs(StereoRender.Head.localPosition.y);
    }
    
    private void OnEnable() {
        Log.LogInfo("[VRPlayer] VRPlayer ENABLED");
    }
    
    private void OnDisable() {
        Log.LogInfo("[VRPlayer] VRPlayer DISABLED");
    }
}