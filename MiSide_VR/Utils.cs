using UnityEngine;
using System;

namespace MiSide_VR;

public static class Utils {
	public static T GetOrAddComponent<T>(this GameObject gameObject) where T : Component {
		if (!gameObject) throw new ArgumentNullException(nameof(gameObject));

		var component = gameObject.GetComponent<T>();
		if (!component) component = gameObject.AddComponent<T>();
		return component;
	}
}
