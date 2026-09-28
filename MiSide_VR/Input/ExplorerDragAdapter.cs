using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Il2CppInterop.Runtime;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace MiSide_VR.Input;

// Adds VR dragging to optional UniverseLib title bars.
static class ExplorerDragAdapter {
	private static readonly List<Binding> Bindings = new();
	private static FieldInfo _allDraggers;
	private static float _nextScan;

	internal static void Update() {
		if (Time.unscaledTime < _nextScan) return;
		_nextScan = Time.unscaledTime + 1f;
		if (_allDraggers == null) {
			var manager = Type.GetType("UniverseLib.UI.Panels.PanelManager, UniverseLib.BIE.IL2CPP.Interop", false);
			if (manager == null) return;
			_allDraggers = AccessTools.Field(manager, "allDraggers");
		}
		if (_allDraggers?.GetValue(null) is not IEnumerable draggers) return;
		Bindings.RemoveAll(binding => !binding.Title || !binding.Rect);
		foreach (var dragger in draggers) {
			var type = dragger.GetType();
			var title = type.GetProperty("DragableArea")?.GetValue(dragger) as RectTransform;
			var rect = type.GetProperty("Rect")?.GetValue(dragger) as RectTransform;
			if (!title || !rect || Bindings.Exists(binding => binding.Title == title)) continue;
			Bindings.Add(new Binding(dragger, title, rect));
		}
	}

	internal static void Reset() {
		foreach (var binding in Bindings) binding.RemoveListeners();
		Bindings.Clear();
		_nextScan = 0f;
	}

	private static EventTrigger.Entry Listen(EventTrigger trigger, EventTriggerType kind, Action<PointerEventData> action) {
		var entry = new EventTrigger.Entry { eventID = kind };
		entry.callback.AddListener(DelegateSupport.ConvertDelegate<UnityAction<BaseEventData>>((Action<BaseEventData>)(data => {
			var pointer = data.TryCast<PointerEventData>();
			// Desktop mouse dragging stays with UniverseLib's own polling path.
			if (BackgroundUiInputAdapter.IsVrPointer(pointer)) action(pointer);
		})));
		trigger.triggers.Add(entry);
		return entry;
	}

	private sealed class Binding {
		internal readonly RectTransform Title;
		internal readonly RectTransform Rect;
		private readonly object _dragger;
		private readonly PropertyInfo _allowed;
		private readonly MethodInfo _endDrag;
		private readonly EventTrigger _trigger;
		private readonly bool _addedTrigger;
		private readonly List<EventTrigger.Entry> _entries = new();
		private RectTransform _parent;
		private Vector2 _startPointer, _startPosition;
		private bool _dragging;

		internal Binding(object dragger, RectTransform title, RectTransform rect) {
			_dragger = dragger;
			Title = title;
			Rect = rect;
			var type = dragger.GetType();
			_allowed = type.GetProperty("AllowDragAndResize");
			_endDrag = type.GetMethod("OnEndDrag");
			_trigger = title.GetComponent<EventTrigger>();
			_addedTrigger = !_trigger;
			if (!_trigger) _trigger = title.gameObject.AddComponent<EventTrigger>();
			_entries.Add(Listen(_trigger, EventTriggerType.BeginDrag, Begin));
			_entries.Add(Listen(_trigger, EventTriggerType.Drag, Drag));
			_entries.Add(Listen(_trigger, EventTriggerType.EndDrag, End));
		}

		internal void RemoveListeners() {
			if (!_trigger) return;
			foreach (var entry in _entries) _trigger.triggers.Remove(entry);
			if (_addedTrigger) UnityEngine.Object.Destroy(_trigger);
		}

		internal void Begin(PointerEventData data) {
			_dragging = false;
			if (!Rect || _allowed?.GetValue(_dragger) is not true) return;
			_parent = Rect.parent ? Rect.parent.TryCast<RectTransform>() : null;
			if (!_parent || !RectTransformUtility.ScreenPointToLocalPointInRectangle(_parent, data.pressPosition, data.pressEventCamera, out _startPointer)) return;
			_startPosition = Rect.anchoredPosition;
			_dragging = true;
		}

		internal void Drag(PointerEventData data) {
			if (_dragging && Rect && _parent && RectTransformUtility.ScreenPointToLocalPointInRectangle(_parent, data.position, data.pressEventCamera, out var point)) Rect.anchoredPosition = _startPosition + point - _startPointer;
		}

		internal void End(PointerEventData _) {
			if (!_dragging) return;
			_dragging = false;
			_endDrag?.Invoke(_dragger, null);
		}
	}
}
