using System;
using Il2CppSystem.Reflection;
using UnityEngine;
using System.Collections.Generic;
using MiSide_VR.VRInput;
using UnityEngine.SceneManagement;
using static MiSide_VR.Plugin;

namespace MiSide_VR.VR;

public class VRPlayer : MonoBehaviour {
    public VRPlayer(IntPtr value) : base(value) { }
    
    public static VRPlayer Instance { get; private set; }
    
    public StereoRender StereoRender { get; private set; }
    private Transform Body { get; set; }
    private Transform Origin { get; set; }
    
    public ControllerTracking Left { get; private set; }
    public ControllerTracking Right { get; private set; }
    
    public GameObject localPlayer;
    public Camera camera;
    public Vector3 positionOffset = new(0.0f, 0.05f, -0.10f);

    // // === RUN STATE MANAGEMENT ===
    //
    // private bool isRunning = false;
    // private float lastRunActivationTime = 0f;
    // private float runActivationCooldown = 0.25f;

    private bool _snapTurnReset = true;
    private const float _snapAngle = 30f;
    private const float _stickThreshold = 0.7f;

    private void Awake() {
        Log.LogInfo("[VRPlayer] VRPlayer Created.");
        
        if (Instance) {
            Log.LogWarning("[VRPlayer] Duplicate VRPlayer detected, destroying duplicate.");
            Destroy(gameObject);
            return;
        }
        
        Instance = this;
        
        // onSceneLoaded += OnSceneLoaded;
        
        Setup();
    }
    
    public void Setup() {
        Log.LogInfo("[VRPlayer] Initializing VRPlayer...");
        if (StereoRender) 
            Destroy(StereoRender);

        Body = transform;
        Origin = transform.parent;
        
        if (!Origin) 
            Origin = new GameObject("[VR_Globals]").transform;
            
        Body.SetParent(Origin, false);
        DontDestroyOnLoad(Origin);
        
        StereoRender = Body.gameObject.AddComponent<StereoRender>();
        
        var leftHandObj = new GameObject("LeftHand");
        leftHandObj.transform.SetParent(Body, false);
        leftHandObj.transform.localPosition = Vector3.zero;
        Left = leftHandObj.AddComponent<ControllerTracking>();
        Left.Setup(ControllerTracking.HandType.Left);
        
        var rightHandObj = new GameObject("RightHand");
        rightHandObj.transform.SetParent(Body, false);
        rightHandObj.transform.localPosition = Vector3.zero;
        Right = rightHandObj.AddComponent<ControllerTracking>();
        Right.Setup(ControllerTracking.HandType.Right);
        
        Log.LogInfo("[VRPlayer] VRPlayer Initialized.");
    }
    
    private void LateUpdate() { 
        if (camera && Body) {
            var basePosition = camera.transform.position;
            var finalPosition = basePosition + camera.transform.TransformDirection(positionOffset);
            finalPosition.y -= 1.0f;
            Body.position = finalPosition;
            
            var cameraEuler = camera.transform.eulerAngles;
            Body.rotation = Quaternion.Euler(0f, cameraEuler.y, 0f);
        }

        if (localPlayer) {
            var playerMove = localPlayer.GetComponent<PlayerMove>();
            if (playerMove) {
                var rightStick = VRInputManager.GetThumbstick("Turn");
                if (Mathf.Abs(rightStick.x) > _stickThreshold) {
                    if (_snapTurnReset) {
                        var direction = (rightStick.x > 0) ? _snapAngle : -_snapAngle;

                        if (playerMove) {
                            var currentRotation = localPlayer.transform.eulerAngles.y;
                            var newRotation = currentRotation + direction;

                            playerMove.TeleportRotate(newRotation);
                        }

                        _snapTurnReset = false;
                    }
                }
                else if (Mathf.Abs(rightStick.x) < 0.2f) {
                    _snapTurnReset = true;
                }

                // var vrMovement = VRInputManager.GetThumbstick("Move");
                // if (Mathf.Abs(vrMovement.x) > 0.1f || Mathf.Abs(vrMovement.y) > 0.1f) {
                //     if (vrMovement.x > 0.1f) playerMove.moveH = 1;
                //     else if (vrMovement.x < -0.1f) playerMove.moveH = -1;
                //
                //     if (vrMovement.y > 0.1f) playerMove.moveV = 1;
                //     else if (vrMovement.y < -0.1f) playerMove.moveV = -1;
                // }
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
    
    private static readonly HashSet<string> ExcludedScenes = new()
    {
        "SceneAihasto",
        "SceneLoading",
        "SceneMenu",
        "MinigameShooter"
    };
    
    public void SetSceneAndCamera(ActiveSceneAndCamera activeSceneAndCamera) {
        var scene = activeSceneAndCamera.Scene;
        camera = activeSceneAndCamera.Camera;
    
        if (camera) {
            if (!ExcludedScenes.Contains(scene.name)) 
                localPlayer = FindLocalPlayerFromCamera(camera);
            else 
                localPlayer = null;
            
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
                movementController.stopMouseMove = true;
        }
    }
    
    private void OnDestroy() {
        // onSceneLoaded -= OnSceneLoaded;
        if (Instance == this)
            Instance = null;
    }
    
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode) {
        // empty
    }
    
    private void OnEnable() {
        Log.LogInfo("[VRPlayer] VRPlayer Enabled.");
    }
    
    private void OnDisable() {
        Log.LogInfo("[VRPlayer] VRPlayer Disabled.");
    }
}