using Il2CppSystem.Collections.Generic;
using System;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using Valve.VR;

namespace MiSide_VR.Player.Controls {
    public class HandController : MonoBehaviour {
        public HandController(IntPtr value) : base(value) { }

        public HandType handType;
        //public Transform model;
        public SteamVR_Behaviour_Pose pose;
        private LineRenderer ray;
        public Transform muzzle;
        public Camera eventCamera;
        public EventSystem eventSystem;
        public StandaloneInputModule inputModule;
        public LayerMask rayCastMask = -967074285;
        public Shader savedLaserShader;

        public bool uiMode = false;
        public bool hideLaser = false;

        public Ray aimRay {
            get {
                return new Ray(muzzle.position, muzzle.forward);
            }
        }

        public enum HandType {
            Left,
            Right
        }

        public void Setup(HandType handType) {
            this.handType = handType;
            //this.model = transform.Find("Model");
            this.ray = transform.GetComponentInChildren<LineRenderer>();
            this.ray.sortingOrder = 999;
            this.savedLaserShader = this.ray.material.shader;
            this.muzzle = ray.transform;
            this.eventCamera = muzzle.GetComponent<Camera>();

            pose = transform.gameObject.GetOrAddComponent<SteamVR_Behaviour_Pose>();
            //pose.poseAction = SteamVR_Actions.default_Pose;
            pose.inputSource = (handType == HandType.Left) ? SteamVR_Input_Sources.LeftHand : SteamVR_Input_Sources.RightHand;
            pose.origin = transform.parent;

            //var shader = Resources.FindObjectsOfTypeAll<Shader>().First(x => x.name.Contains("M1/Character"));
            //var renderers = model.GetComponentsInChildren<Renderer>();
            //foreach (var renderer in renderers) {
            //    renderer.material.shader = shader;
            //    renderer.material.SetFloat("_OutlineWidth", 0.005f);
            //}
        }

        public void SetupEventSystem(EventSystem eventSystem, StandaloneInputModule inputModule) {
            this.eventSystem = eventSystem;
            this.inputModule = inputModule;
        }

        public void SetUIMode(bool uiMode) {
            this.uiMode = uiMode;
        }

        public void LateUpdate() {
            if (hideLaser) {
                ray.enabled = false;
            } else if (ray.gameObject.activeSelf) {
                if (uiMode) {
                    //ray.material.shader = VRAssets.LaserUnlit;
                    ray.enabled = true;
                } else {
                    ray.material.shader = savedLaserShader;
                    ray.enabled = true;
                }
                ray.SetPosition(0, muzzle.position);
                ray.SetPosition(1, GetRayHitPosition());
            }
        }

        public Vector3 GetRayHitPosition() {
            if (uiMode)
                return GetCanvasHitEnd();

            return GetRayHitPosition(300);
        }

        public Vector3 GetRayHitPosition(float maxDistance) {
            Ray ray = aimRay;
            RaycastHit hitInfo;
            if (Physics.Raycast(ray, out hitInfo, maxDistance, rayCastMask)) {
                return hitInfo.point;
            }
            return ray.origin + (ray.direction * maxDistance);
        }

        private Vector3 GetCanvasHitEnd() {
            float distance = GetCanavsDistance();
            return CalculateEnd(distance);
        }

        private float GetCanavsDistance() {
            if (!eventSystem || !inputModule || !inputModule.inputOverride)
                return 0f;

            PointerEventData eventData = new PointerEventData(eventSystem);
            eventData.position = inputModule.inputOverride.mousePosition;

            List<RaycastResult> results = new List<RaycastResult>();
            eventSystem.RaycastAll(eventData, results);

            RaycastResult cloestResult = FindFirstRaycast(results);
            float distance = cloestResult.distance;

            distance = Valve.VR.Mathf.Clamp(distance, 0.0f, 5);

            return distance;
        }

        private RaycastResult FindFirstRaycast(List<RaycastResult> results) {
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
}