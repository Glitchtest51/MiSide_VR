using System;
using UnityEngine;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using static MiSide_VR.Plugin;

namespace MiSide_VR.VR;

public class VRPlayer : MonoBehaviour {
    public VRPlayer(IntPtr value) : base(value) { }
    
    public static VRPlayer Instance { get; private set; }
    
    public StereoRender StereoRender { get; private set; }

    private Transform Body { get; set; }

    private Transform Origin { get; set; }
    
    public HandController LeftHand { get; private set; }
    public HandController RightHand { get; private set; }
    
    public GameObject localPlayer;
    
    public Camera Camera;
    
    public Vector3 positionOffset = new Vector3(0.0f, 0.05f, -0.10f);
    
    public float bodyRotationSpeed = 10f;

    // // === RUN STATE MANAGEMENT ===
    //
    // private bool isRunning = false;
    // private float lastRunActivationTime = 0f;
    // private float runActivationCooldown = 0.25f;
    //
    // // === SNAP TURN CONFIGURATION ===
    //
    // private float snapTurnOffset = 0f;
    // private bool snapTurnReset = true;
    // private float snapAngle = 30f;
    // private float stickThreshold = 0.7f;
    
    private void Awake() {
        Log.LogInfo("[VRPlayer] VRPlayer Created.");
        
        if (Instance) {
            Log.LogWarning("[VRPlayer] Duplicate VRPlayer detected, destroying duplicate.");
            Destroy(gameObject);
            return;
        }
        
        Instance = this;
        
        // onSceneLoaded += new Action<Scene, LoadSceneMode>(OnSceneLoaded);
        
        Setup();
    }
    
    public void Setup() {
        Log.LogInfo("[VRPlayer] Initializing VRPlayer...");
        if (StereoRender) 
            Destroy(StereoRender);

        Body = transform;
        Origin = transform.parent;
        
        if (!Origin) 
            Origin = new GameObject("[VROrigin]").transform;
            
        Body.SetParent(Origin, false);
        DontDestroyOnLoad(Origin);
        
        StereoRender = Body.gameObject.AddComponent<StereoRender>();
        
        var leftHandObj = new GameObject("LeftHand");
        leftHandObj.transform.SetParent(Body, false);
        leftHandObj.transform.localPosition = Vector3.zero;
        LeftHand = leftHandObj.AddComponent<HandController>();
        LeftHand.Setup(HandController.HandType.Left);
        
        var rightHandObj = new GameObject("RightHand");
        rightHandObj.transform.SetParent(Body, false);
        rightHandObj.transform.localPosition = Vector3.zero;
        RightHand = rightHandObj.AddComponent<HandController>();
        RightHand.Setup(HandController.HandType.Right);
        
        Log.LogInfo("[VRPlayer] VRPlayer Initialized.");
    }
    
    private void LateUpdate() { 
        // --- POSITIONING: Sync VR origin to game camera ---
        if (Camera && Origin) {
            Vector3 basePosition = Camera.transform.position;
            Vector3 finalPosition = basePosition + Camera.transform.TransformDirection(positionOffset);
            Origin.position = finalPosition;
        }
        
        // --- BODY ALIGNMENT: Rotate player to match HMD forward direction ---
        if (localPlayer && StereoRender?.head) {
            if (Camera) {
                Camera.transform.rotation = StereoRender.head.rotation;
            }
            
            var headForward = StereoRender.head.forward;
            headForward.y = 0f;
        
            if (headForward.magnitude > 0.01f) {
                headForward.Normalize();
                Quaternion targetRotation = Quaternion.LookRotation(headForward, Vector3.up);
                
                localPlayer.transform.rotation = Quaternion.Slerp(localPlayer.transform.rotation, targetRotation, Time.deltaTime * bodyRotationSpeed);
            }
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
    
    private static readonly HashSet<string> ExcludedScenes = new()
    {
        "SceneAihasto",
        "SceneLoading",
        "SceneMenu",
        "MinigameShooter"
    };
    
    public void SetSceneAndCamera(ActiveSceneAndCamera activeSceneAndCamera) {
        var scene = activeSceneAndCamera.Scene;
        Camera = activeSceneAndCamera.Camera;
    
        if (Camera) {
            if (!ExcludedScenes.Contains(scene.name)) 
                localPlayer = FindLocalPlayerFromCamera(Camera);
            else 
                localPlayer = null;

            // if (LocalPlayer != null) 
            //     firstPersonCharacter = LocalPlayer.GetComponent<Il2CppFirstPersonCharacter>();
            
            DisableMouseRotation();
        }
    }
    
    private GameObject FindLocalPlayerFromCamera(Camera camera) {
        if (!camera) return null;

        var current = camera.transform;
        while (current) {
            if (current.name == "Player")
                return current.gameObject;

            current = current.parent;
        }
    
        if (DebugMode) 
            Log.LogWarning("[VRPlayer] Could not find Player from camera: " + camera.name);
        return null;
    }

    
    private void DisableMouseRotation() {
        if (localPlayer)
        {
            var movementController = localPlayer.GetComponent<PlayerMove>();
            if (movementController)
                movementController.enabled = false;
        }
    }
    
    private void OnDestroy() {
        // onSceneLoaded -= new Action<Scene, LoadSceneMode>(OnSceneLoaded);
        if (Instance == this)
            Instance = null;
    }
    
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode) {
        // Intentionally empty
    }
    
    private void OnEnable() {
        Log.LogInfo("[VRPlayer] VRPlayer Enabled.");
    }
    
    private void OnDisable() {
        Log.LogInfo("[VRPlayer] VRPlayer Disabled.");
    }
}