using MiSide_VR.Core;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace MiSide_VR.UI;

static class TamagotchiMonitor {
	private static RenderTexture _screen;
	private static RawImage _surface;
	private static Camera _mitaCamera;
	private static RenderTexture _mitaOriginalTarget;
	private static CameraClearFlags _mitaOriginalClearFlags;
	private static Color _mitaOriginalBackground;
	private static float _mitaOriginalDepth;

	public static void Synchronize() {
		if (GameContext.Mode != GameMode.Tamagotchi || !VRPlayer.Instance) {
			RestoreMitaCamera();
			if (_surface) _surface.enabled = false;
			return;
		}

		var canvas = FindTamagotchiCanvas();
		if (!canvas) return;

		EnsureScreen();
		EnsureSurface(canvas);
		BindMitaCamera(GameContext.OverlayCamera);

		_surface.texture = _screen;
		_surface.enabled = _mitaCamera && _mitaCamera.isActiveAndEnabled;
	}

	private static Canvas FindTamagotchiCanvas() {
		var controller = Object.FindObjectOfType<Tamagotchi_Main>();
		if (!controller) return null;

		var canvas = controller.GetComponent<Canvas>();
		return canvas ? canvas.rootCanvas : null;
	}

	private static void EnsureScreen() {
		if (_screen) return;
		_screen = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32) { name = "MiSide_VR Tamagotchi Monitor", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, useMipMap = false, autoGenerateMips = false };
		_screen.Create();
	}

	private static void EnsureSurface(Canvas canvas) {
		if (_surface && _surface.transform.parent == canvas.transform) return;

		if (_surface) Object.Destroy(_surface.gameObject);

		var surfaceObject = new GameObject("[VR Tamagotchi Screen]");
		surfaceObject.layer = canvas.gameObject.layer;
		var rect = surfaceObject.AddComponent<RectTransform>();
		rect.SetParent(canvas.transform, false);
		rect.anchorMin = Vector2.zero;
		rect.anchorMax = Vector2.one;
		rect.offsetMin = Vector2.zero;
		rect.offsetMax = Vector2.zero;
		rect.SetAsFirstSibling();

		_surface = surfaceObject.AddComponent<RawImage>();
		_surface.raycastTarget = false;
		_surface.color = Color.white;
	}

	private static void BindMitaCamera(Camera camera) {
		if (_mitaCamera != camera) {
			RestoreMitaCamera();
			_mitaCamera = camera;
			if (camera) {
				_mitaOriginalTarget = camera.targetTexture;
				_mitaOriginalClearFlags = camera.clearFlags;
				_mitaOriginalBackground = camera.backgroundColor;
				_mitaOriginalDepth = camera.depth;
			}
		}

		if (!_mitaCamera) return;

		_mitaCamera.targetTexture = _screen;
		_mitaCamera.stereoTargetEye = StereoTargetEyeMask.None;
		_mitaCamera.depth = 1f;
		
		// capture only mitas remote 3D stage into the tamagotchi screen
		_mitaCamera.clearFlags = CameraClearFlags.SolidColor;
		_mitaCamera.backgroundColor = Color.clear;
	}

	private static void RestoreMitaCamera() {
		if (_mitaCamera) {
			_mitaCamera.targetTexture = _mitaOriginalTarget;
			_mitaCamera.clearFlags = _mitaOriginalClearFlags;
			_mitaCamera.backgroundColor = _mitaOriginalBackground;
			_mitaCamera.depth = _mitaOriginalDepth;
		}
		_mitaCamera = null;
		_mitaOriginalTarget = null;
	}

	public static void Dispose() {
		RestoreMitaCamera();
		if (_surface) Object.Destroy(_surface.gameObject);
		_surface = null;
		if (!_screen) return;
		_screen.Release();
		Object.Destroy(_screen);
		_screen = null;
	}
}
