using MiSide_VR.UI;
using MiSide_VR.UI.Patches;
using UnityEngine;
using UnityEngine.EventSystems;

namespace MiSide_VR.Input;

// Sends VR pointer events directly to captured MiSide and Unity Explorer UI.
static class BackgroundUiInputAdapter {
	private const float ScrollDeadzone = 0.35f;
	private const float ScrollUnitsPerSecond = 12.5f;
	private static EventSystem _eventSystem;
	private static PointerEventData _eventData;
	private static GameObject _hovered;
	private static GameObject _pressed;
	private static GameObject _dragged;
	private static Vector2 _lastPosition;
	private static bool _hasPosition;
	private static RaycastResult _emptyRaycast;
	private static bool _gestureActive;
	private static bool _gestureOwnsPointer;
	private static bool _gestureCancelled;
	private static VRController _pressHand;
	private static VirtualScreen _pressScreen;
	internal static bool OwnsPointer { get; private set; }

	public static void Update() {
		if (!VRInput.IsInitialized) {
			Reset();
			ExplorerDragAdapter.Reset();
			_gestureActive = false;
			OwnsPointer = false;
			return;
		}

		var currentEventSystem = EventSystem.current;
		ExplorerDragAdapter.Update();
		if (!currentEventSystem) {
			Reset();
			OwnsPointer = _gestureActive && _gestureOwnsPointer;
			if (!VRInput.GetButton("Interact")) _gestureActive = false;
			return;
		}
		if (_eventSystem != currentEventSystem || _eventData == null) {
			Reset();
			_eventSystem = currentEventSystem;
			_eventData = new PointerEventData(currentEventSystem);
			_eventData.pointerId = -1;
			_emptyRaycast = new RaycastResult();
		}

		var hand = VRInput.GetHand();
		var screen = VirtualScreen.Instance;
		if (_gestureActive && _gestureOwnsPointer && !_gestureCancelled && (hand != _pressHand || !screen || screen != _pressScreen || !screen.gameObject.activeInHierarchy || !IsActive(_pressed) && !IsActive(_dragged))) {
			Release(null, false);
			_gestureCancelled = true;
		}

		var pixels = Vector2.zero;
		var captured = _gestureActive && _gestureOwnsPointer && !_gestureCancelled;
		var projected = hand && (captured ? VirtualScreen.TryProjectPointer(hand.AimRay, out pixels, out _) : VirtualScreen.TryGetPointerPosition(hand.AimRay, out pixels));
		var overScreen = projected && pixels.x >= 0f && pixels.x <= VirtualScreen.PixelWidth && pixels.y >= 0f && pixels.y <= VirtualScreen.PixelHeight;
		var position = projected ? pixels : _lastPosition;

		_eventData.Reset();
		_eventData.position = position;
		_eventData.delta = _hasPosition ? position - _lastPosition : Vector2.zero;
		_eventData.button = PointerEventData.InputButton.Left;
		_lastPosition = position;
		_hasPosition |= projected;
		RaycastResult raycast = default;
		var hasTarget = overScreen && CanvasPatch.TryGetPointerRaycast(currentEventSystem, _eventData, out raycast);
		var target = hasTarget ? raycast.gameObject : null;
		// il2cpp needs a real RaycastResult instance instead of default
		_eventData.pointerCurrentRaycast = hasTarget ? raycast : _emptyRaycast;
		if (VRInput.GetButtonDown("Interact") && !_gestureActive) {
			_gestureActive = true;
			_gestureOwnsPointer = hasTarget && CanvasPatch.IsInteractiveTarget(target);
			_gestureCancelled = false;
			_pressHand = hand;
			_pressScreen = screen;
			OwnsPointer = _gestureOwnsPointer;
			if (_gestureOwnsPointer) Press(target);
		}
		// keep a press on its original ui or game route until release
		OwnsPointer = _gestureActive ? _gestureOwnsPointer : hasTarget && CanvasPatch.IsInteractiveTarget(target);
		UpdateHover(target);
		UpdateScroll(target);

		if (!_gestureActive) return;
		if (_gestureOwnsPointer && !_gestureCancelled) Drag();
		if (!VRInput.GetButton("Interact")) {
			if (_gestureOwnsPointer && !_gestureCancelled) Release(target, true);
			_gestureActive = false;
			_pressHand = null;
			_pressScreen = null;
		}
	}

	internal static bool TryGetPointerPosition(out Vector2 position) {
		position = _lastPosition;
		return OwnsPointer && _eventData != null && _hasPosition;
	}

	internal static bool IsVrPointer(PointerEventData data) => _eventData != null && data != null && data.Pointer == _eventData.Pointer;

	public static void Reset() {
		if (_eventData != null) Release(null, false);
		if (_hovered && _eventData != null) ExecuteEvents.Execute(_hovered, _eventData, ExecuteEvents.pointerExitHandler);
		_hovered = null;
		_pressed = null;
		_dragged = null;
		_eventData = null;
		_eventSystem = null;
		_emptyRaycast = null;
		_lastPosition = Vector2.zero;
		_hasPosition = false;
		_gestureCancelled = true;
		_pressHand = null;
		_pressScreen = null;
		OwnsPointer = _gestureActive && _gestureOwnsPointer;
	}

	private static bool IsActive(GameObject target) => target && target.activeInHierarchy;

	private static void UpdateScroll(GameObject target) {
		var handler = target ? ExecuteEvents.GetEventHandler<IScrollHandler>(target) : null;
		var moveY = VRInput.GetVector2("Move").y;
		var turnY = VRInput.GetVector2("Turn").y;
		var axis = Mathf.Abs(turnY) > Mathf.Abs(moveY) ? turnY : moveY;

		if (!handler || _gestureActive || Mathf.Abs(axis) < ScrollDeadzone) return;

		// send framerate independent analog scroll values
		var magnitude = Mathf.InverseLerp(ScrollDeadzone, 1f, Mathf.Abs(axis));
		var deltaTime = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
		
		_eventData.scrollDelta = new Vector2(0f, Mathf.Sign(axis) * magnitude * ScrollUnitsPerSecond * deltaTime);
		ExecuteEvents.Execute(handler, _eventData, ExecuteEvents.scrollHandler);
		_eventData.scrollDelta = Vector2.zero;
	}

	private static void UpdateHover(GameObject target) {
		_eventData.pointerEnter = target;
		var hoverHandler = target ? ExecuteEvents.GetEventHandler<IPointerEnterHandler>(target) : null;
		if (_hovered == hoverHandler) return;
		if (_hovered) ExecuteEvents.Execute(_hovered, _eventData, ExecuteEvents.pointerExitHandler);
		_hovered = hoverHandler;
		if (_hovered) ExecuteEvents.Execute(_hovered, _eventData, ExecuteEvents.pointerEnterHandler);
	}

	private static void Press(GameObject target) {
		_eventData.delta = Vector2.zero;
		_eventData.pressPosition = _eventData.position;
		_eventData.pointerPressRaycast = _eventData.pointerCurrentRaycast;
		_eventData.eligibleForClick = true;
		_eventData.dragging = false;
		_eventData.useDragThreshold = true;
		var selected = ExecuteEvents.GetEventHandler<ISelectHandler>(target);
		if (selected != _eventSystem.currentSelectedGameObject) _eventSystem.SetSelectedGameObject(null, _eventData);
		_pressed = ExecuteEvents.ExecuteHierarchy(target, _eventData, ExecuteEvents.pointerDownHandler);
		if (!_pressed) _pressed = ExecuteEvents.GetEventHandler<IPointerClickHandler>(target);
		var now = Time.unscaledTime;
		_eventData.clickCount = _pressed == _eventData.lastPress && now - _eventData.clickTime < 0.3f ? _eventData.clickCount + 1 : 1;
		_eventData.clickTime = now;
		_eventData.pointerPress = _pressed;
		_eventData.rawPointerPress = target;

		_dragged = ExecuteEvents.GetEventHandler<IDragHandler>(target);
		_eventData.pointerDrag = _dragged;
		if (_dragged) ExecuteEvents.Execute(_dragged, _eventData, ExecuteEvents.initializePotentialDrag);
	}

	private static void Drag() {
		if (!_dragged || _eventData == null || _eventData.delta.sqrMagnitude < 0.01f) return;
		if (!_eventData.dragging) {
			var threshold = _eventSystem ? _eventSystem.pixelDragThreshold : 5;
			if (_eventData.useDragThreshold && (_eventData.position - _eventData.pressPosition).sqrMagnitude < threshold * threshold) return;
			ExecuteEvents.Execute(_dragged, _eventData, ExecuteEvents.beginDragHandler);
			_eventData.dragging = true;
			_eventData.eligibleForClick = false;
		// cancel a child button when its draggable parent takes over
			if (_pressed != _dragged) {
				if (_pressed) ExecuteEvents.Execute(_pressed, _eventData, ExecuteEvents.pointerUpHandler);
				_pressed = null;
				_eventData.pointerPress = null;
				_eventData.rawPointerPress = null;
			}
		}
		ExecuteEvents.Execute(_dragged, _eventData, ExecuteEvents.dragHandler);
	}

	private static void Release(GameObject target, bool allowClick) {
		if (_eventData == null) return;
		if (_pressed) ExecuteEvents.Execute(_pressed, _eventData, ExecuteEvents.pointerUpHandler);

		var clickHandler = target ? ExecuteEvents.GetEventHandler<IPointerClickHandler>(target) : null;
		if (allowClick && _pressed && _pressed == clickHandler && _eventData.eligibleForClick && !_eventData.dragging) {
			ExecuteEvents.Execute(_pressed, _eventData, ExecuteEvents.pointerClickHandler);
		} else if (allowClick && _eventData.dragging && target) {
			ExecuteEvents.ExecuteHierarchy(target, _eventData, ExecuteEvents.dropHandler);
		}

		if (_dragged && _eventData.dragging) ExecuteEvents.Execute(_dragged, _eventData, ExecuteEvents.endDragHandler);
		_eventData.eligibleForClick = false;
		_eventData.dragging = false;
		_eventData.pointerPress = null;
		_eventData.rawPointerPress = null;
		_eventData.pointerDrag = null;
		if (_emptyRaycast != null) _eventData.pointerPressRaycast = _emptyRaycast;
		_pressed = null;
		_dragged = null;
	}
}
