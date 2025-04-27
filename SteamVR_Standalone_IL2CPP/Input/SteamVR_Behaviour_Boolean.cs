using Il2CppInterop.Runtime.InteropTypes.Arrays;
using System;
using System.Collections.Generic;
using System.Linq;
//======= Copyright (c) Valve Corporation, All rights reserved. ===============

using System.Text;

using UnityEngine;
using UnityEngine.Events;

namespace Valve.VR
{
    /// <summary>
    /// This component simplifies using boolean actions.
    /// <para>Provides editor accessible events: OnPress, OnPressDown, OnPressUp, OnChange, and OnUpdate.</para>
    /// <para>Provides script accessible events: OnPressEvent, OnPressDownEvent, OnPressUpEvent, OnChangeEvent, and OnUpdateEvent.</para>
    /// </summary>
    public class SteamVR_Behaviour_Boolean : MonoBehaviour
    {
        public SteamVR_Behaviour_Boolean(IntPtr value) : base(value) { }

        public SteamVR_Action_Boolean booleanAction;

        public SteamVR_Input_Sources inputSource;

        /// <summary>This UnityEvent fires whenever a change happens in the action</summary>
        public SteamVR_Behaviour_BooleanEvent onChange;

        /// <summary>This C# event fires whenever a change happens in the action</summary>
        public event ChangeHandler onChangeEvent;

        /// <summary>This UnityEvent fires whenever the action is updated</summary>
        public SteamVR_Behaviour_BooleanEvent onUpdate;

        /// <summary>This C# event fires whenever the action is updated</summary>
        public event UpdateHandler onUpdateEvent;

        /// <summary>This UnityEvent will fire whenever the boolean action is true and gets updated</summary>
        public SteamVR_Behaviour_BooleanEvent onPress;

        /// <summary>This C# event will fire whenever the boolean action is true and gets updated</summary>
        public event StateHandler onPressEvent;

        /// <summary>This UnityEvent will fire whenever the boolean action has changed from false to true in the last update</summary>
        public SteamVR_Behaviour_BooleanEvent onPressDown;

        /// <summary>This C# event will fire whenever the boolean action has changed from false to true in the last update</summary>
        public event StateDownHandler onPressDownEvent;

        /// <summary>This UnityEvent will fire whenever the boolean action has changed from true to false in the last update</summary>
        public SteamVR_Behaviour_BooleanEvent onPressUp;

        /// <summary>This C# event will fire whenever the boolean action has changed from true to false in the last update</summary>
        public event StateUpHandler onPressUpEvent;

        /// <summary>Returns true if this action is currently bound and its action set is active</summary>
        public bool isActive { get { return booleanAction[inputSource].active; } }

        /// <summary>Returns the action set that this action is in.</summary>
        public SteamVR_ActionSet actionSet { get { if (booleanAction != null) return booleanAction.actionSet; else return null; } }



        protected virtual void OnEnable()
        {
            if (booleanAction == null)
            {
                Debug.LogError("[SteamVR] Boolean action not set.", this);
                return;
            }

            AddHandlers();
        }

        protected virtual void OnDisable()
        {
            RemoveHandlers();
        }

        protected void AddHandlers()
        {
            booleanAction[inputSource].onUpdate += SteamVR_Behaviour_Boolean_OnUpdate;
            booleanAction[inputSource].onChange += SteamVR_Behaviour_Boolean_OnChange;
            booleanAction[inputSource].onState += SteamVR_Behaviour_Boolean_OnState;
            booleanAction[inputSource].onStateDown += SteamVR_Behaviour_Boolean_OnStateDown;
            booleanAction[inputSource].onStateUp += SteamVR_Behaviour_Boolean_OnStateUp;
        }

        protected void RemoveHandlers()
        {

            if (booleanAction != null)
            {
                booleanAction[inputSource].onUpdate -= SteamVR_Behaviour_Boolean_OnUpdate;
                booleanAction[inputSource].onChange -= SteamVR_Behaviour_Boolean_OnChange;
                booleanAction[inputSource].onState -= SteamVR_Behaviour_Boolean_OnState;
                booleanAction[inputSource].onStateDown -= SteamVR_Behaviour_Boolean_OnStateDown;
                booleanAction[inputSource].onStateUp -= SteamVR_Behaviour_Boolean_OnStateUp;
            }
        }

        private void SteamVR_Behaviour_Boolean_OnStateUp(SteamVR_Action_Boolean fromAction, SteamVR_Input_Sources fromSource)
        {
            if (onPressUp != null)
            {
                //onPressUp.Invoke(this, fromSource, false);
            }

            if (onPressUpEvent != null)
            {
                onPressUpEvent.Invoke(this, fromSource);
            }
        }

        private void SteamVR_Behaviour_Boolean_OnStateDown(SteamVR_Action_Boolean fromAction, SteamVR_Input_Sources fromSource)
        {
            if (onPressDown != null)
            {
                var parameters = new Il2CppReferenceArray<Il2CppSystem.Object>(3);
                parameters[0] = this;
                parameters[1] = (int)fromSource;
                parameters[2] = true;
                onPressDown.Invoke(parameters);
            }

            if (onPressDownEvent != null)
            {
                onPressDownEvent.Invoke(this, fromSource);
            }
        }

        private void SteamVR_Behaviour_Boolean_OnState(SteamVR_Action_Boolean fromAction, SteamVR_Input_Sources fromSource)
        {
            if (onPress != null)
            {
                var parameters = new Il2CppReferenceArray<Il2CppSystem.Object>(3);
                parameters[0] = this;
                parameters[1] = (int)fromSource;
                parameters[2] = true;
                onPress.Invoke(parameters);
            }

            if (onPressEvent != null)
            {
                onPressEvent.Invoke(this, fromSource);
            }
        }

        private void SteamVR_Behaviour_Boolean_OnUpdate(SteamVR_Action_Boolean fromAction, SteamVR_Input_Sources fromSource, bool newState)
        {
            if (onUpdate != null)
            {
                var parameters = new Il2CppReferenceArray<Il2CppSystem.Object>(3);
                parameters[0] = this;
                parameters[1] = (int)fromSource;
                parameters[2] = newState;
                onUpdate.Invoke(parameters);
            }

            if (onUpdateEvent != null)
            {
                onUpdateEvent.Invoke(this, fromSource, newState);
            }
        }

        private void SteamVR_Behaviour_Boolean_OnChange(SteamVR_Action_Boolean fromAction, SteamVR_Input_Sources fromSource, bool newState)
        {
            if (onChange != null)
            {
                var parameters = new Il2CppReferenceArray<Il2CppSystem.Object>(3);
                parameters[0] = this;
                parameters[1] = (int)fromSource;
                parameters[2] = newState;
                onChange.Invoke(parameters);
            }

            if (onChangeEvent != null)
            {
                onChangeEvent.Invoke(this, fromSource, newState);
            }
        }

        /// <summary>
        /// Gets the localized name of the device that the action corresponds to.
        /// </summary>
        /// <param name="localizedParts">
        /// <list type="bullet">
        /// <item><description>VRInputString_Hand - Which hand the origin is in. E.g. "Left Hand"</description></item>
        /// <item><description>VRInputString_ControllerType - What kind of controller the user has in that hand.E.g. "Vive Controller"</description></item>
        /// <item><description>VRInputString_InputSource - What part of that controller is the origin. E.g. "Trackpad"</description></item>
        /// <item><description>VRInputString_All - All of the above. E.g. "Left Hand Vive Controller Trackpad"</description></item>
        /// </list>
        /// </param>
        public string GetLocalizedName(params EVRInputStringBits[] localizedParts)
        {
            if (booleanAction != null)
                return booleanAction.GetLocalizedOriginPart(inputSource, localizedParts);
            return null;
        }

        public delegate void StateDownHandler(SteamVR_Behaviour_Boolean fromAction, SteamVR_Input_Sources fromSource);
        public delegate void StateUpHandler(SteamVR_Behaviour_Boolean fromAction, SteamVR_Input_Sources fromSource);
        public delegate void StateHandler(SteamVR_Behaviour_Boolean fromAction, SteamVR_Input_Sources fromSource);
        public delegate void ActiveChangeHandler(SteamVR_Behaviour_Boolean fromAction, SteamVR_Input_Sources fromSource, bool active);
        public delegate void ChangeHandler(SteamVR_Behaviour_Boolean fromAction, SteamVR_Input_Sources fromSource, bool newState);
        public delegate void UpdateHandler(SteamVR_Behaviour_Boolean fromAction, SteamVR_Input_Sources fromSource, bool newState);
    }
}