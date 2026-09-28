using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using MiSide_VR.Core;
using MiSide_VR.Input;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.UI;
using static MiSide_VR.Plugin;
using Object = UnityEngine.Object;

namespace MiSide_VR.UI.Patches;

[HarmonyPatch(typeof(CanvasScaler), "OnEnable")]
public static class CanvasPatch {
	private static readonly HashSet<int> ProcessedCanvases = new();
	private static readonly List<Canvas> CapturedCanvases = new();
	private static readonly Il2CppSystem.Collections.Generic.List<RaycastResult> PointerRaycastResults = new();
	private static readonly Vector2 ReferenceResolution = new(VirtualScreen.PixelWidth, VirtualScreen.PixelHeight);
	private static readonly string[] IgnoredCanvasNames = ["[VR Virtual Screen]"];

	public const int SourceUiLayer = 18;
	private const int TextureWidth = 1920;
	private const int TextureHeight = 1080;

	private static Camera _captureCamera;
	private static RenderTexture _captureTexture;
	private static RawImage _screenImage;
	private static Material _screenMaterial;
	private static EventSystem _pointerEventSystem;
	private static PointerEventData _pointerEventData;
	private static GameObject _menuBackdrop;
	private static GameObject _menuBackground;
	private static GameObject _menuVignette;

	private const string MenuBackgroundPath = "MenuGame/CanvasBackground/Back";
	private const string MenuVignettePath = "MenuGame/CanvasBackground/Vignette";
	private const string UnityExplorerRootName = "com.sinai.unityexplorer_Root";
	private const float MenuBackdropScale = 20f;
	private const float MenuBackdropDepth = 1100f;
	private const float MenuBackdropTileScale = 0.6f;

	public static void Postfix(CanvasScaler __instance) => ProcessCanvas(__instance ? __instance.GetComponent<Canvas>() : null);

	public static void ResetSceneState() {
		BackgroundUiInputAdapter.Reset();
		ProcessedCanvases.Clear();
		CapturedCanvases.Clear();
		_pointerEventSystem = null;
		_pointerEventData = null;
		if (_menuBackdrop) Object.Destroy(_menuBackdrop);
		_menuBackdrop = null;
		_menuBackground = null;
		_menuVignette = null;
		PlayerDialogueAdapter.Reset();
	}

	public static void ProcessExistingCanvases() {
		EnsureVirtualScreen();
		foreach (var canvas in Object.FindObjectsOfType<Canvas>(true)) ProcessCanvas(canvas);
		
		// fix main menu exit button
		if (!_captureCamera || GameContext.Scene.name != "SceneMenu") return;
		foreach (var runner in Object.FindObjectsOfType<Menu_RunMouse>(true)) if (runner && runner.UICamera != _captureCamera) runner.UICamera = _captureCamera;
		
		SetupMainMenuBackground();
		EnsureVirtualScreen();
		for (var index = CapturedCanvases.Count - 1; index >= 0; index--) {
			var canvas = CapturedCanvases[index];
			if (!canvas) {
				CapturedCanvases.RemoveAt(index);
				continue;
			}
			SetLayerRecursively(canvas.transform, SourceUiLayer);
		}
		
		// unity explorer compat
		var root = GameObject.Find(UnityExplorerRootName);
		if (root) SetLayerRecursively(root.transform, SourceUiLayer);
	}

	private static void SetupMainMenuBackground() {
		if (GameContext.Scene.name != "SceneMenu" || !VirtualScreen.Instance) {
			if (_menuBackdrop) Object.Destroy(_menuBackdrop);
			_menuBackdrop = null;
			_menuBackground = null;
			_menuVignette = null;
			return;
		}

		if (!_menuBackground) _menuBackground = GameObject.Find(MenuBackgroundPath);
		if (!_menuVignette) _menuVignette = GameObject.Find(MenuVignettePath);
		if (_menuVignette) _menuVignette.SetActive(false);

		if (_menuBackdrop) {
			if (_menuBackground) _menuBackground.SetActive(false);
			return;
		}

		if (!_menuBackground) return;
		var sourceImage = _menuBackground.GetComponent<Image>();
		if (!sourceImage) return;

		_menuBackdrop = new GameObject("[VR Menu Backdrop]");
		var rect = _menuBackdrop.AddComponent<RectTransform>();
		rect.SetParent(VirtualScreen.Instance.transform, false);
		rect.anchorMin = new Vector2(0.5f, 0.5f);
		rect.anchorMax = new Vector2(0.5f, 0.5f);
		rect.pivot = new Vector2(0.5f, 0.5f);
		rect.sizeDelta = ReferenceResolution * MenuBackdropScale;
		rect.localPosition = new Vector3(0f, 0f, MenuBackdropDepth);
		rect.SetAsFirstSibling();
		_menuBackdrop.layer = VRPlayer.VrUiLayer;

		var sprite = sourceImage.overrideSprite ? sourceImage.overrideSprite : sourceImage.sprite;
		if (!sprite) {
			Object.Destroy(_menuBackdrop);
			_menuBackdrop = null;
			return;
		}
		sprite.texture.wrapMode = TextureWrapMode.Repeat;
		var image = _menuBackdrop.AddComponent<RawImage>();
		image.texture = sprite.texture;
		image.color = sourceImage.color;
		image.material = sourceImage.material;
		var verticalRepetitions = MenuBackdropScale / MenuBackdropTileScale;
		var horizontalRepetitions = verticalRepetitions * (VirtualScreen.PixelWidth / VirtualScreen.PixelHeight);
		image.uvRect = new Rect(0f, 0f, horizontalRepetitions, verticalRepetitions);
		image.raycastTarget = false;

		_menuBackground.SetActive(false);
	}

	private static void ProcessCanvas(Canvas canvas) {
		if (!VREnabled || !VRPlayer.Instance || !canvas) return;
		if (ProcessedCanvases.Contains(canvas.GetInstanceID()) || IgnoredCanvasNames.Contains(canvas.name)) return;

		var parentCanvas = canvas.transform.parent ? canvas.transform.parent.GetComponentInParent<Canvas>() : null;
		if (parentCanvas) return;

		if (canvas.renderMode == RenderMode.WorldSpace) {
			PlayerDialogueAdapter.Observe(canvas);
			return;
		}
		
		if (canvas.renderMode == RenderMode.ScreenSpaceCamera && canvas.worldCamera && canvas.worldCamera.targetTexture) {
			ProcessedCanvases.Add(canvas.GetInstanceID());
			return;
		}

		EnsureVirtualScreen();
		if (!_captureCamera) return;

		ProcessedCanvases.Add(canvas.GetInstanceID());
		CapturedCanvases.Add(canvas);
		
		canvas.renderMode = RenderMode.ScreenSpaceCamera;
		canvas.worldCamera = _captureCamera;

		var legacyBlocker = canvas.GetComponent<BoxCollider>();
		if (legacyBlocker) Object.Destroy(legacyBlocker);
		SetLayerRecursively(canvas.transform, SourceUiLayer);
	}

	private static void EnsureVirtualScreen() {
		if (!_captureTexture) {
			_captureTexture = new RenderTexture(TextureWidth, TextureHeight, 24, RenderTextureFormat.ARGB32) { name = "MiSide_VR UI Composition", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, useMipMap = false, autoGenerateMips = false };
			_captureTexture.Create();
		}

		if (!_captureCamera) {
			var cameraObject = new GameObject("[VR UI Capture Camera]");
			Object.DontDestroyOnLoad(cameraObject);
			_captureCamera = cameraObject.AddComponent<Camera>();
			_captureCamera.stereoTargetEye = StereoTargetEyeMask.None;
			_captureCamera.clearFlags = CameraClearFlags.SolidColor;
			_captureCamera.backgroundColor = Color.clear;
			_captureCamera.cullingMask = 1 << SourceUiLayer;
			_captureCamera.targetTexture = _captureTexture;
			_captureCamera.depth = -100f;
			_captureCamera.orthographic = true;
			_captureCamera.nearClipPlane = 0.01f;
			_captureCamera.enabled = true;
		}
		SynchronizeCaptureCamera();
		
		if (!VirtualScreen.Instance && VRPlayer.Instance && VRPlayer.Instance.headCamera) {
			var screenObject = new GameObject("[VR Virtual Screen]");
			Object.DontDestroyOnLoad(screenObject);
			var canvas = screenObject.AddComponent<Canvas>();
			canvas.renderMode = RenderMode.WorldSpace;
			canvas.worldCamera = VRPlayer.Instance.headCamera;
			canvas.overrideSorting = true;
			canvas.sortingOrder = 32000;
			var rect = canvas.GetComponent<RectTransform>();
			rect.sizeDelta = ReferenceResolution;
			SetLayerRecursively(rect, VRPlayer.VrUiLayer);

			var imageObject = new GameObject("Image");
			var imageRect = imageObject.AddComponent<RectTransform>();
			imageRect.SetParent(rect, false);
			imageRect.anchorMin = Vector2.zero;
			imageRect.anchorMax = Vector2.one;
			imageRect.offsetMin = Vector2.zero;
			imageRect.offsetMax = Vector2.zero;
			imageObject.layer = VRPlayer.VrUiLayer;
			var image = imageObject.AddComponent<RawImage>();
			image.texture = _captureTexture;
			image.raycastTarget = false;
			_screenImage = image;

			var screen = screenObject.AddComponent<VirtualScreen>();
			screen.Bind(_captureTexture);
		}

		if (!_screenImage && VirtualScreen.Instance) _screenImage = VirtualScreen.Instance.GetComponentInChildren<RawImage>(true);
		if (_screenImage) _screenImage.material = GameContext.IsPanelMode ? null : GetScreenMaterial();
	}

	public static bool IsPointerOverInteractable(Vector2 pixels) {
		var eventSystem = EventSystem.current;
		if (!eventSystem) return false;
		if (_pointerEventSystem != eventSystem || _pointerEventData == null) {
			_pointerEventSystem = eventSystem;
			_pointerEventData = new PointerEventData(eventSystem);
		}
		_pointerEventData.position = pixels;
		if (!TryGetPointerRaycast(eventSystem, _pointerEventData, out var raycast)) return false;
		return IsInteractiveTarget(raycast.gameObject);
	}

	internal static bool IsInteractiveTarget(GameObject target) {
		if (!target) return false;
		var selectable = target.GetComponentInParent<Selectable>();
		if (selectable && (!selectable.IsActive() || !selectable.IsInteractable())) return false;
		return ExecuteEvents.GetEventHandler<IPointerClickHandler>(target) || ExecuteEvents.GetEventHandler<IPointerDownHandler>(target) || ExecuteEvents.GetEventHandler<IDragHandler>(target) || ExecuteEvents.GetEventHandler<IScrollHandler>(target);
	}

	public static bool TryGetPointerRaycast(EventSystem eventSystem, PointerEventData eventData, out RaycastResult result) {
		result = default;
		if (!eventSystem || eventData == null) return false;

		PointerRaycastResults.Clear();
		eventSystem.RaycastAll(eventData, PointerRaycastResults);
		foreach (var raycast in PointerRaycastResults) {
			if (raycast.gameObject && raycast.gameObject.layer == SourceUiLayer && IsInteractiveTarget(raycast.gameObject)) {
				result = raycast;
				return true;
			}
		}
		return false;
	}

	public static void SynchronizeCaptureCamera() {
		if (!_captureCamera || !_captureTexture) return;

		var sourceCamera = GameContext.SourceCamera;
		if (GameContext.Mode == GameMode.Novella && sourceCamera) {
			// capture novellas camera objects and redirected canvases together
			_captureCamera.CopyFrom(sourceCamera);
			_captureCamera.transform.SetPositionAndRotation(sourceCamera.transform.position, sourceCamera.transform.rotation);
			_captureCamera.fieldOfView = 35f;
			_captureCamera.cullingMask = sourceCamera.cullingMask | (1 << SourceUiLayer);
		} else {
			_captureCamera.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
			_captureCamera.clearFlags = CameraClearFlags.SolidColor;
			_captureCamera.backgroundColor = Color.clear;
			_captureCamera.cullingMask = 1 << SourceUiLayer;
			_captureCamera.depth = -100f;
			_captureCamera.orthographic = true;
			_captureCamera.orthographicSize = 5f;
			_captureCamera.nearClipPlane = 0.01f;
			// keep MiSides default plane distance canvases inside the far clip plane
			_captureCamera.farClipPlane = 1000f;
		}

		_captureCamera.stereoTargetEye = StereoTargetEyeMask.None;
		_captureCamera.targetTexture = _captureTexture;
		_captureCamera.enabled = true;
	}

	public static void SetLayerRecursively(Transform root, int layer) {
		if (!root) return;
		root.gameObject.layer = layer;
		for (var index = 0; index < root.childCount; index++) SetLayerRecursively(root.GetChild(index), layer);
	}

	private static Material GetScreenMaterial() {
		if (_screenMaterial) return _screenMaterial;

		var shader = Shader.Find("UI/Default");
		if (!shader) return null;

		_screenMaterial = new Material(shader) { name = "MiSide VR Always Visible UI", hideFlags = HideFlags.HideAndDontSave, renderQueue = 5000 };
		// UI/Default uses unity_GUIZTestMode to draw the tablet above the world
		_screenMaterial.SetInt("unity_GUIZTestMode", (int)CompareFunction.Always);
		return _screenMaterial;
	}
}

static class PlayerDialogueAdapter {
	private const float MinimumDistance = 1f;
	private const float ScaleMultiplier = 1.2f;
	private static Canvas _canvas;
	private static Vector3 _authoredScale;

	internal static void Observe(Canvas canvas) {
		if (!canvas || !canvas.gameObject.activeInHierarchy || !canvas.name.StartsWith("DPlayer", System.StringComparison.Ordinal)) return;
		if (_canvas == canvas) return;

		_canvas = canvas;
		_authoredScale = canvas.transform.localScale;
	}

	internal static void Apply(Transform viewer) {
		if (!_canvas || !_canvas.gameObject.activeInHierarchy || !viewer) return;

		var dialogue = _canvas.transform;
		dialogue.localScale = _authoredScale * ScaleMultiplier;
		var offset = dialogue.position - viewer.position;
		var distance = offset.magnitude;
		if (distance >= MinimumDistance) return;

		var direction = distance > 0.001f ? offset / distance : viewer.forward;
		dialogue.position = viewer.position + direction * MinimumDistance;
	}

	internal static void Reset() {
		_canvas = null;
		_authoredScale = Vector3.one;
	}
}
