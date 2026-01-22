// using UnityEngine;
// using UnityEngine.InputSystem;
// using UnityEngine.InputSystem.LowLevel;
// using static MiSide_VR.Plugin;
//
// namespace MiSide_VR.Input;
//
// /// <summary>
// /// Emulates traditional keyboard/mouse input based on VR controller gestures.
// /// This allows VR hand movements to control in-game actions like movement, jumping, melee attacks, etc.
// /// </summary>
// public class KeyboardHack
// {
//     // Jump-related state and constants
//     private float _jumpCooldown = 0f;
//     private const float JUMP_THRESHOLD = 1.5f;    // Sensitivity threshold for jump detection (m/s)
//     private const float JUMP_DELAY = 0.8f;        // Minimum time (in seconds) between jump gestures
//
//     // Melee attack state and constants
//     private bool _isMeleeAttacking = false;
//     private float _attackCooldown = 0f;
//     private const float ATTACK_THRESHOLD = 3.5f;        // Hand speed required to trigger an attack (m/s)
//     private const float RELEASE_THRESHOLD = 1.2f;       // Speed below which the attack button is released
//     private const float MIN_ATTACK_INTERVAL = 0.2f;     // Minimum time between consecutive melee swings
//
//     /// <summary>
//     /// Main entry point called each frame to inject simulated keyboard and mouse input
//     /// based on current VR controller states.
//     /// </summary>
//     public void InjectMovement()
//     {
//         // Read current thumbstick values from VR controllers
//         UnityEngine.Vector2 lstick = NativeVRInput.GetVector2("ThumbstickLeft");
//         UnityEngine.Vector2 rstick = NativeVRInput.GetVector2("ThumbstickRight");
//
//         // Read grip and face button states
//         bool leftGripPress = NativeVRInput.GetBoolean("GripLeft_Press");
//         bool rightGripPress = NativeVRInput.GetBoolean("GripRight_Press");
//         bool YPress = NativeVRInput.GetBoolean("ButtonLeft_Y_Press");
//
//         // Retrieve reference to Unity's virtual keyboard device
//         var keyboard = Keyboard.current;
//         if (keyboard == null) return;
//
//         // Create a synthetic StateEvent that mimics real keyboard input
//         StateEvent.From(keyboard, out var eventPtr);
//
//         // Map left thumbstick to WASD movement
//         keyboard.wKey.WriteValueIntoEvent(lstick.y > 0.1f ? 1f : 0f, eventPtr);
//         keyboard.sKey.WriteValueIntoEvent(lstick.y < -0.1f ? 1f : 0f, eventPtr);
//         keyboard.aKey.WriteValueIntoEvent(lstick.x < -0.1f ? 1f : 0f, eventPtr);
//         keyboard.dKey.WriteValueIntoEvent(lstick.x > 0.1f ? 1f : 0f, eventPtr);
//
//         // Map grip presses to 'E' (e.g., interact/use)
//         keyboard.eKey.WriteValueIntoEvent(leftGripPress || rightGripPress ? 1f : 0f, eventPtr);
//
//         // Map Y button (left controller) to 'I' (e.g., inventory)
//         keyboard.iKey.WriteValueIntoEvent(YPress ? 1f : 0f, eventPtr);
//
//         // Map right thumbstick down to Left Ctrl (e.g., crouch)
//         keyboard.leftCtrlKey.WriteValueIntoEvent(rstick.y < -0.3f ? 1f : 0f, eventPtr);
//
//         // Submit the synthetic event to Unity's input system
//         InputSystem.QueueEvent(eventPtr);
//
//         // Update gesture-based actions
//         UpdateJumpGesture();
//         UpdateMouseInjection();
//         UpdateMeleeAttackGesture();
//     }
//
//     /// <summary>
//     /// Simulates mouse button presses based on VR trigger inputs.
//     /// Right trigger = left mouse button (attack/fire), left trigger = right mouse button (aim/block).
//     /// </summary>
//     public void UpdateMouseInjection()
//     {
//         var mouse = Mouse.current;
//         if (mouse == null) return;
//
//         // Read trigger states (digital press detection)
//         bool triggerRight = NativeVRInput.GetBoolean("TriggerRight_Press");   // Attack/fire
//         bool triggerLeft = NativeVRInput.GetBoolean("TriggerLeft_Press");     // Aim/block
//
//         // Generate a synthetic mouse event
//         StateEvent.From(mouse, out var eventPtr);
//         mouse.leftButton.WriteValueIntoEvent(triggerRight ? 1f : 0f, eventPtr);
//         mouse.rightButton.WriteValueIntoEvent(triggerLeft ? 1f : 0f, eventPtr);
//         InputSystem.QueueEvent(eventPtr);
//     }
//
//     /// <summary>
//     /// Detects a "jump" gesture when both hands move upward rapidly.
//     /// Requires both hands to exceed a vertical velocity threshold simultaneously.
//     /// </summary>
//     public void UpdateJumpGesture()
//     {
//         if (_jumpCooldown > 0)
//         {
//             _jumpCooldown -= Time.deltaTime;
//             return;
//         }
//
//         // Get vertical (Y-axis) velocity of both hands
//         float leftY = NativeVRInput.GetHandVelocity("PoseLeft").y;
//         float rightY = NativeVRInput.GetHandVelocity("PoseRight").y;
//
//         // Trigger jump only if both hands exceed the upward velocity threshold
//         if (leftY > JUMP_THRESHOLD && rightY > JUMP_THRESHOLD)
//         {
//             Log.LogInfo($"[GESTURE] Jump detected! LeftVel: {leftY:F2} m/s, RightVel: {rightY:F2} m/s");
//             ExecuteJumpAction();
//             _jumpCooldown = JUMP_DELAY; // Prevent rapid successive jumps
//         }
//     }
//
//     /// <summary>
//     /// Detects and simulates melee swing gestures using right-hand speed.
//     /// Presses the left mouse button on fast motion, releases it when motion slows.
//     /// </summary>
//     public void UpdateMeleeAttackGesture()
//     {
//         if (_attackCooldown > 0)
//             _attackCooldown -= Time.deltaTime;
//
//         // Get full 3D velocity of the right hand
//         Vector3 velocity = NativeVRInput.GetHandVelocity("PoseRight");
//         float currentSpeed = velocity.magnitude;
//
//         var mouse = Mouse.current;
//         if (mouse == null) return;
//
//         // --- ATTACK TRIGGER ---
//         // If hand is moving fast enough, not already attacking, and cooldown has passed
//         if (currentSpeed > ATTACK_THRESHOLD && !_isMeleeAttacking && _attackCooldown <= 0)
//         {
//             _isMeleeAttacking = true;
//             _attackCooldown = MIN_ATTACK_INTERVAL;
//
//             StateEvent.From(mouse, out var eventPtr);
//             mouse.leftButton.WriteValueIntoEvent(1f, eventPtr);
//             InputSystem.QueueEvent(eventPtr);
//
//             Log.LogInfo($"[COMBAT] Melee attack detected! Speed: {currentSpeed:F2} m/s");
//         }
//
//         // --- ATTACK RELEASE ---
//         // Release the mouse button once hand slows below the release threshold
//         else if (currentSpeed < RELEASE_THRESHOLD && _isMeleeAttacking)
//         {
//             _isMeleeAttacking = false;
//             StateEvent.From(mouse, out var eventPtr);
//             mouse.leftButton.WriteValueIntoEvent(0f, eventPtr);
//             InputSystem.QueueEvent(eventPtr);
//         }
//     }
//
//     /// <summary>
//     /// Executes a jump by simulating a momentary press of the spacebar.
//     /// </summary>
//     private void ExecuteJumpAction()
//     {
//         var keyboard = Keyboard.current;
//         if (keyboard == null) return;
//
//         // Press the space key
//         StateEvent.From(keyboard, out var eventPtr);
//         keyboard.spaceKey.WriteValueIntoEvent(1f, eventPtr);
//         InputSystem.QueueEvent(eventPtr);
//
//         // Schedule key release after a short delay
//         MelonCoroutines.Start(ReleaseSpace(keyboard));
//     }
//
//     /// <summary>
//     /// Coroutine that releases the spacebar after a brief duration to simulate a real key press.
//     /// </summary>
//     /// <param name="keyboard">Reference to the Unity virtual keyboard device.</param>
//     /// <returns>An IEnumerator for use as a coroutine.</returns>
//     private System.Collections.IEnumerator ReleaseSpace(Keyboard keyboard)
//     {
//         yield return new WaitForSeconds(0.1f);
//         StateEvent.From(keyboard, out var eventPtr);
//         keyboard.spaceKey.WriteValueIntoEvent(0f, eventPtr);
//         InputSystem.QueueEvent(eventPtr);
//     }
// }