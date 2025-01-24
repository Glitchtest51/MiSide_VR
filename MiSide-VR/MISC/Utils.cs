using System;
using System.Collections.Generic;
using UnityEngine;
using Valve.VR;
using Mathf = UnityEngine.Mathf;

namespace MiSide_VR {
    internal static class Utils
    {
        public static void SetLayerRecursively(this GameObject inst, int layer)
        {
            inst.layer = layer;
            int children = inst.transform.childCount;
            for (int i = 0; i < children; ++i)
                inst.transform.GetChild(i).gameObject.SetLayerRecursively(layer);
        }

        public static Vector3 GetFlatDirection(this Vector3 forward)
        {
            forward.y = 0;
            return forward.normalized;
        }

        public static T GetOrAddComponent<T>(this GameObject gameObject) where T : Component
        {
            if (gameObject == null)
                throw new ArgumentNullException("GetOrAddComponent: gameObject is null!");

            T comp = gameObject.GetComponent<T>();
            if (comp == null)
                comp = gameObject.AddComponent<T>();

            return comp;
        }

        public static Transform DeepFindChild(this Transform parent, string targetName)
        {
            Transform _result = parent.Find(targetName);
            if (_result == null)
            {
                foreach (var child in parent)
                {
                    _result = DeepFindChild(child as Transform, targetName);
                    if (_result != null)
                    {
                        return _result;
                    }
                }
            }
            return _result;
        }

        public static bool IsInState(this Animator animator, int hash)
        {
            return animator.GetCurrentAnimatorStateInfo(0).fullPathHash == hash;
        }

        public static TValue GetValueOrDefault<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key, TValue defaultValue)
        {
            return dictionary.TryGetValue(key, out var value) ? value : defaultValue;
        }

        public static float CalculateZoomFOV(float nonZoomedFOV, float magnification)
        {
            var factor = 2.0f * Mathf.Tan(0.5f * nonZoomedFOV * Mathf.Deg2Rad);
            var zoomedFOV = 2.0f * Mathf.Atan(factor / (2.0f * magnification)) * Mathf.Rad2Deg;
            return zoomedFOV;
        }
        public static Matrix4x4 ConvertToMatrix4x4(this HmdMatrix44_t hm)
        {
            var m = new Matrix4x4();
            m.m00 = hm.m0;
            m.m01 = hm.m1;
            m.m02 = hm.m2;
            m.m03 = hm.m3;
            m.m10 = hm.m4;
            m.m11 = hm.m5;
            m.m12 = hm.m6;
            m.m13 = hm.m7;
            m.m20 = hm.m8;
            m.m21 = hm.m9;
            m.m22 = hm.m10;
            m.m23 = hm.m11;
            m.m30 = hm.m12;
            m.m31 = hm.m13;
            m.m32 = hm.m14;
            m.m33 = hm.m15;
            return m;
        }


        /// <summary>
        /// Determine the signed angle between two vectors, with normal 'n'
        /// as the rotation axis.
        /// </summary>
        public static float AngleSigned(Vector3 v1, Vector3 v2, Vector3 n)
        {
            return Mathf.Atan2(
                Vector3.Dot(n, Vector3.Cross(v1, v2)),
                Vector3.Dot(v1, v2)) * Mathf.Rad2Deg;
        }


        //Made by maxattack on GitHub
        public static Quaternion SmoothDamp(this Quaternion rot, Quaternion target, ref Quaternion deriv, float time)
        {
            if (Time.unscaledDeltaTime < Mathf.Epsilon) return rot;
            if (time == 0f) return target;

            var Dot = Quaternion.Dot(rot, target);
            var Multi = Dot > 0f ? 1f : -1f;
            target.x *= Multi;
            target.y *= Multi;
            target.z *= Multi;
            target.w *= Multi;

            var Result = new Vector4(
                Mathf.SmoothDamp(rot.x, target.x, ref deriv.x, time, int.MaxValue, Time.unscaledDeltaTime),
                Mathf.SmoothDamp(rot.y, target.y, ref deriv.y, time, int.MaxValue, Time.unscaledDeltaTime),
                Mathf.SmoothDamp(rot.z, target.z, ref deriv.z, time, int.MaxValue, Time.unscaledDeltaTime),
                Mathf.SmoothDamp(rot.w, target.w, ref deriv.w, time, int.MaxValue, Time.unscaledDeltaTime)
            ).normalized;

            var derivError = Vector4.Project(new Vector4(deriv.x, deriv.y, deriv.z, deriv.w), Result);
            deriv.x -= derivError.x;
            deriv.y -= derivError.y;
            deriv.z -= derivError.z;
            deriv.w -= derivError.w;

            return new Quaternion(Result.x, Result.y, Result.z, Result.w);
        }
    }
}
