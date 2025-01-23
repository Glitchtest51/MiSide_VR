using System;
using UnityEngine;

namespace Valve.VR
{
    /// <summary>
    /// Hook into BeforeRenderHelper in your plugin to make these events work!
    /// </summary>
    public static class UnityHooks
    {
        public static event Action OnBeforeRender;

        public static event Action<Camera> OnBeforeCull;

        public static void InvokeOnBeforeRender()
        {
            if (OnBeforeRender != null)
            {
                OnBeforeRender.Invoke();
            }
        }

        public static void InvokeOnBeforeCull(Camera cam)
        {
            if (OnBeforeCull != null)
            {
                OnBeforeCull.Invoke(cam);
            }
        }

        public static void Init()
        {
            Camera.onPreRender = (
                (Camera.onPreRender == null)
                ? new Action<Camera>(OnPreRender)
                : Il2CppSystem.Delegate.Combine(Camera.onPreRender, (Camera.CameraCallback)new Action<Camera>(OnPreRender)).Cast<Camera.CameraCallback>()
                );

                    Camera.onPreCull = (
           (Camera.onPreCull == null)
           ? new Action<Camera>(OnPreCull)
           : Il2CppSystem.Delegate.Combine(Camera.onPreCull, (Camera.CameraCallback)new Action<Camera>(OnPreCull)).Cast<Camera.CameraCallback>()
           );
       }

        private static void OnPreRender(Camera cam)
        {
            if (OnPreRenderCam == null || !OnPreRenderCam.enabled) OnPreRenderCam = cam; if (OnPreRenderCam == cam) InvokeOnBeforeRender();
        }

        private static void OnPreCull(Camera cam)
        {
            if (OnPreCullCam == null || !OnPreCullCam.enabled) OnPreCullCam = cam; if (OnPreCullCam == cam) InvokeOnBeforeCull(cam);
        }

        private static Camera OnPreRenderCam = null;
        private static Camera OnPreCullCam = null;
    }
}