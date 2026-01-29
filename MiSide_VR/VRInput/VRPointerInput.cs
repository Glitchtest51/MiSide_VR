using System;
using UnityEngine;
using UnityEngine.EventSystems;
using Valve.VR;

namespace MiSide_VR.VRInput;

public class VRPointerInput : BaseInput {
    public VRPointerInput(IntPtr value) : base(value) { }

    public Camera eventCamera;
    public SteamVR_Action_Boolean clickButton = SteamVR_Actions.Gameplay.Interact;

    public override void Awake() {
        GetComponent<BaseInputModule>().inputOverride = this;
    }

    public override bool GetMouseButton(int button) {
        return clickButton != null && clickButton.state;
    }

    public override bool GetMouseButtonDown(int button) {
        return clickButton != null && clickButton.stateDown;
    }

    public override bool GetMouseButtonUp(int button) {
        return clickButton != null && clickButton.stateUp;
    }

    public override Vector2 mousePosition {
        get {
            if (eventCamera)
                    return new Vector2(eventCamera.pixelWidth / 2, eventCamera.pixelHeight / 2);
            return Vector2.zero;
        }
    }
}
