// using UnityEngine;
// using Valve.VR;
// using static MiSide_VR.Plugin;
//
// namespace MiSide_VR.Input;
//
// /// <summary>
// /// Provides centralized access to SteamVR Input actions defined in the action manifest.
// /// This class maps JSON action paths to strongly-typed C# action references and offers
// /// convenient helper methods for common input queries (e.g., thumbstick, trigger, grip).
// /// </summary>
// public static class VRInput
// {
//     // === RIGHT CONTROLLER INPUTS ===
//
//     public static SteamVR_Action_Boolean ButtonRight_A_Touch;
//     public static SteamVR_Action_Boolean ButtonRight_A_Press;
//     public static SteamVR_Action_Boolean ButtonRight_B_Touch;
//     public static SteamVR_Action_Boolean ButtonRight_B_Press;
//     public static SteamVR_Action_Boolean ButtonRight_System_Press;
//
//     public static SteamVR_Action_Vector2 ThumbstickRight;
//     public static SteamVR_Action_Boolean ThumbstickRight_Touch;
//     public static SteamVR_Action_Boolean ThumbstickRight_Press;
//
//     public static SteamVR_Action_Boolean GripRight_Press;
//     public static SteamVR_Action_Vector1 GripRight_Value;
//
//     public static SteamVR_Action_Boolean TriggerRight_Touch;
//     public static SteamVR_Action_Boolean TriggerRight_Press;
//     public static SteamVR_Action_Vector1 TriggerRight_Value;
//
//     // === LEFT CONTROLLER INPUTS ===
//
//     public static SteamVR_Action_Boolean ButtonLeft_X_Touch;
//     public static SteamVR_Action_Boolean ButtonLeft_X_Press;
//     public static SteamVR_Action_Boolean ButtonLeft_Y_Touch;
//     public static SteamVR_Action_Boolean ButtonLeft_Y_Press;
//     public static SteamVR_Action_Boolean ButtonLeft_Menu_Press;
//
//     public static SteamVR_Action_Vector2 ThumbstickLeft;
//     public static SteamVR_Action_Boolean ThumbstickLeft_Touch;
//     public static SteamVR_Action_Boolean ThumbstickLeft_Press;
//
//     public static SteamVR_Action_Boolean GripLeft_Press;
//     public static SteamVR_Action_Vector1 GripLeft_Value;
//
//     public static SteamVR_Action_Boolean TriggerLeft_Touch;
//     public static SteamVR_Action_Boolean TriggerLeft_Press;
//     public static SteamVR_Action_Vector1 TriggerLeft_Value;
//
//     // === HAND POSE TRACKING ===
//
//     public static SteamVR_Action_Pose PoseRight;
//     public static SteamVR_Action_Pose PoseLeft;
//
//     /// <summary>
//     /// Registers all SteamVR input actions from the "/actions/default" set
//     /// and sets up event listeners for commonly used buttons.
//     /// Must be called once after SteamVR has initialized.
//     /// </summary>
//     public static void Initialize()
//     {
//         // Example: Register event-based callbacks for the right A button (press/release)
//         SteamVR_Actions._default.A_Button_Press.AddOnStateDownListener(ButtonAPress, SteamVR_Input_Sources.Any);
//         SteamVR_Actions._default.A_Button_Press.AddOnStateUpListener(ButtonARelease, SteamVR_Input_Sources.Any);
//
//         // --- RIGHT CONTROLLER ---
//         ButtonRight_A_Touch = SteamVR_Input.GetAction<SteamVR_Action_Boolean>("/actions/default/in/ButtonRight_A_Touch");
//         ButtonRight_A_Press = SteamVR_Input.GetAction<SteamVR_Action_Boolean>("/actions/default/in/ButtonRight_A_Press");
//
//         ButtonRight_B_Touch = SteamVR_Input.GetAction<SteamVR_Action_Boolean>("/actions/default/in/ButtonRight_B_Touch");
//         ButtonRight_B_Press = SteamVR_Input.GetAction<SteamVR_Action_Boolean>("/actions/default/in/ButtonRight_B_Press");
//
//         ButtonRight_System_Press = SteamVR_Input.GetAction<SteamVR_Action_Boolean>("/actions/default/in/ButtonRight_System_Press");
//
//         ThumbstickRight = SteamVR_Input.GetAction<SteamVR_Action_Vector2>("/actions/default/in/ThumbstickRight");
//         ThumbstickRight_Touch = SteamVR_Input.GetAction<SteamVR_Action_Boolean>("/actions/default/in/ThumbstickRight_Touch");
//         ThumbstickRight_Press = SteamVR_Input.GetAction<SteamVR_Action_Boolean>("/actions/default/in/ThumbstickRight_Press");
//
//         GripRight_Press = SteamVR_Input.GetAction<SteamVR_Action_Boolean>("/actions/default/in/GripRight_Press");
//         GripRight_Value = SteamVR_Input.GetAction<SteamVR_Action_Vector1>("/actions/default/in/GripRight_Value");
//
//         TriggerRight_Touch = SteamVR_Input.GetAction<SteamVR_Action_Boolean>("/actions/default/in/TriggerRight_Touch");
//         TriggerRight_Press = SteamVR_Input.GetAction<SteamVR_Action_Boolean>("/actions/default/in/TriggerRight_Press");
//         TriggerRight_Value = SteamVR_Input.GetAction<SteamVR_Action_Vector1>("/actions/default/in/TriggerRight_Value");
//
//         // --- LEFT CONTROLLER ---
//         ButtonLeft_X_Touch = SteamVR_Input.GetAction<SteamVR_Action_Boolean>("/actions/default/in/ButtonLeft_X_Touch");
//         ButtonLeft_X_Press = SteamVR_Input.GetAction<SteamVR_Action_Boolean>("/actions/default/in/ButtonLeft_X_Press");
//
//         ButtonLeft_Y_Touch = SteamVR_Input.GetAction<SteamVR_Action_Boolean>("/actions/default/in/ButtonLeft_Y_Touch");
//         ButtonLeft_Y_Press = SteamVR_Input.GetAction<SteamVR_Action_Boolean>("/actions/default/in/ButtonLeft_Y_Press");
//
//         ButtonLeft_Menu_Press = SteamVR_Input.GetAction<SteamVR_Action_Boolean>("/actions/default/in/ButtonLeft_Menu_Press");
//
//         ThumbstickLeft = SteamVR_Input.GetAction<SteamVR_Action_Vector2>("/actions/default/in/ThumbstickLeft");
//         ThumbstickLeft_Touch = SteamVR_Input.GetAction<SteamVR_Action_Boolean>("/actions/default/in/ThumbstickLeft_Touch");
//         ThumbstickLeft_Press = SteamVR_Input.GetAction<SteamVR_Action_Boolean>("/actions/default/in/ThumbstickLeft_Press");
//
//         GripLeft_Press = SteamVR_Input.GetAction<SteamVR_Action_Boolean>("/actions/default/in/GripLeft_Press");
//         GripLeft_Value = SteamVR_Input.GetAction<SteamVR_Action_Vector1>("/actions/default/in/GripLeft_Value");
//
//         TriggerLeft_Touch = SteamVR_Input.GetAction<SteamVR_Action_Boolean>("/actions/default/in/TriggerLeft_Touch");
//         TriggerLeft_Press = SteamVR_Input.GetAction<SteamVR_Action_Boolean>("/actions/default/in/TriggerLeft_Press");
//         TriggerLeft_Value = SteamVR_Input.GetAction<SteamVR_Action_Vector1>("/actions/default/in/TriggerLeft_Value");
//
//         // --- HAND POSES ---
//         PoseRight = SteamVR_Input.GetAction<SteamVR_Action_Pose>("/actions/default/in/PoseRight");
//         PoseLeft = SteamVR_Input.GetAction<SteamVR_Action_Pose>("/actions/default/in/PoseLeft");
//     }
//
//     // === HELPER METHODS FOR COMMON INPUT QUERIES ===
//
//     /// <summary>
//     /// Gets the current axis value of the left thumbstick.
//     /// </summary>
//     /// <returns>Vector2 with X (horizontal) and Y (vertical) components in range [-1, 1].</returns>
//     public static Vector2 GetLeftThumbstick() => ThumbstickLeft.GetAxis(SteamVR_Input_Sources.Any);
//
//     /// <summary>
//     /// Gets the current axis value of the right thumbstick.
//     /// </summary>
//     /// <returns>Vector2 with X (horizontal) and Y (vertical) components in range [-1, 1].</returns>
//     public static Vector2 GetRightThumbstick() => ThumbstickRight.GetAxis(SteamVR_Input_Sources.Any);
//
//     /// <summary>
//     /// Gets the current analog value of the left trigger (0.0 = released, 1.0 = fully pressed).
//     /// </summary>
//     public static float GetLeftTrigger() => TriggerLeft_Value.GetAxis(SteamVR_Input_Sources.Any);
//
//     /// <summary>
//     /// Gets the current analog value of the right trigger (0.0 = released, 1.0 = fully pressed).
//     /// </summary>
//     public static float GetRightTrigger() => TriggerRight_Value.GetAxis(SteamVR_Input_Sources.Any);
//
//     /// <summary>
//     /// Gets the current analog value of the left grip (0.0 = open, 1.0 = fully squeezed).
//     /// </summary>
//     public static float GetLeftGrip() => GripLeft_Value.GetAxis(SteamVR_Input_Sources.Any);
//
//     /// <summary>
//     /// Gets the current analog value of the right grip (0.0 = open, 1.0 = fully squeezed).
//     /// </summary>
//     public static float GetRightGrip() => GripRight_Value.GetAxis(SteamVR_Input_Sources.Any);
//
//     /// <summary>
//     /// Checks if the right controller's A button is currently pressed.
//     /// </summary>
//     /// <returns>True if pressed; otherwise, false.</returns>
//     public static bool IsButtonAPress() => ButtonRight_A_Press.GetState(SteamVR_Input_Sources.Any);
//
//     // === EVENT CALLBACKS (FOR DEBUGGING OR ACTION TRIGGERS) ===
//
//     /// <summary>
//     /// Called when the A button on the right controller is pressed down.
//     /// Currently used for logging/debugging.
//     /// </summary>
//     public static void ButtonAPress(SteamVR_Action_Boolean fromAction, SteamVR_Input_Sources fromSource)
//     {
//         Log.LogWarning(" *********** A BUTTON PRESSED ****************");
//     }
//
//     /// <summary>
//     /// Called when the A button on the right controller is released.
//     /// Currently used for logging/debugging.
//     /// </summary>
//     public static void ButtonARelease(SteamVR_Action_Boolean fromAction, SteamVR_Input_Sources fromSource)
//     {
//         Log.LogWarning(" *********** A BUTTON RELEASED ****************");
//     }
// }