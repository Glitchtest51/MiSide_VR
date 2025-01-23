using System;
using System.Reflection;

namespace MiSide_VR.MISC
{
    internal static class ReflectionUtils
    {
        internal static T GetPrivateProberty<T>(this object obj, string probertyName)
        {
            return (T)((object)obj.GetType().GetProperty(probertyName, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(obj, new object[] { 0 }));
        }

        internal static void SetPrivateProperty(this object obj, string propertyName, object value)
        {
            obj.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).SetValue(obj, value, null);
        }

        internal static void SetPrivatePropertyBase(this object obj, string propertyName, object value)
        {
            var prop = obj.GetType().BaseType.GetProperty(propertyName, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
            prop.SetValue(obj, value, null);
        }

        internal static object InvokeMethod(this object obj, string methodName, object[] methodParams)
        {
            return obj.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).Invoke(obj, methodParams);
        }

        public static T GetPrivateField<T>(this object obj, string fieldName)
        {
            var prop = obj.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            var value = prop.GetValue(obj);
            return (T)value;
        }

        public static T GetPublicField<T>(this object obj, string fieldName)
        {
            var prop = obj.GetType().GetField(fieldName, BindingFlags.Public | BindingFlags.Instance);
            var value = prop.GetValue(obj);
            return (T)value;
        }

        public static T GetStaticPrivateField<T>(this Type type, string fieldName)
        {
            var prop = type.GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Static);
            var value = prop.GetValue(null);
            return (T)value;
        }

        internal static void SetPrivateField<TSubject>(this TSubject obj, string fieldName, object value)
        {
            if (obj == null)
            {
                throw new ArgumentNullException(nameof(obj));
            }

            FieldInfo field = typeof(TSubject).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);

            if (field == null)
            {
                throw new InvalidOperationException($"Private instance field '{fieldName}' does not exist on {typeof(TSubject).FullName}");
            }

            field.SetValue(obj, value);
        }

        internal static void InvokePrivateMethod<TSubject>(this TSubject obj, string methodName, params object[] args)
        {
            if (obj == null)
            {
                throw new ArgumentNullException(nameof(obj));
            }

            MethodInfo method = typeof(TSubject).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance);

            if (method == null)
            {
                throw new InvalidOperationException($"Private instance method '{methodName}' does not exist on {typeof(TSubject).FullName}");
            }

            method.Invoke(obj, args);
        }

        internal static TDelegate CreatePrivateMethodDelegate<TDelegate>(this Type type, string methodName) where TDelegate : Delegate
        {
            if (type == null)
            {
                throw new ArgumentNullException(nameof(type));
            }

            MethodInfo method = type.GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);

            if (method == null)
            {
                throw new InvalidOperationException($"Method '{methodName}' does not exist on {type.FullName}");
            }

            return (TDelegate)Delegate.CreateDelegate(typeof(TDelegate), method);
        }
    }
}
