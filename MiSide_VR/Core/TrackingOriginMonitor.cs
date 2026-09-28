using System;
using System.Collections.Generic;
using Il2CppInterop.Runtime;
using UnityEngine;
using UnityEngine.XR;
using static MiSide_VR.Plugin;

namespace MiSide_VR.Core;

static class TrackingOriginMonitor {
	private static readonly List<XRInputSubsystem> Inputs = new();
	private static Il2CppSystem.Action<XRInputSubsystem> _trackingOriginUpdated;
	private static bool _warnedUnavailable;

	public static void Initialize() {
		try {
			_trackingOriginUpdated ??= DelegateSupport.ConvertDelegate<Il2CppSystem.Action<XRInputSubsystem>>(new Action<XRInputSubsystem>(OnTrackingOriginUpdated));

			var subsystems = SubsystemManager.s_IntegratedSubsystems;
			if (subsystems == null) {
				WarnUnavailableOnce();
				return;
			}

			foreach (var subsystem in subsystems) {
				if (subsystem == null) continue;
				var input = subsystem.TryCast<XRInputSubsystem>();
				if (input == null || Inputs.Contains(input)) continue;
				input.add_trackingOriginUpdated(_trackingOriginUpdated);
				Inputs.Add(input);
			}

			_warnedUnavailable = false;
			if (DebugMode) Log.LogDebug($"[TrackingOrigin] Monitoring {Inputs.Count} XR input subsystems.");
		} catch (Exception exception) { WarnUnavailableOnce(exception); }
	}

	private static void WarnUnavailableOnce(Exception exception = null) {
		if (_warnedUnavailable) return;
		_warnedUnavailable = true;
		Log.LogWarning(exception == null ? "[TrackingOrigin] XR input is not ready, monitoring will retry after OpenVR starts" : $"[TrackingOrigin] XR input is not ready, monitoring will retry after OpenVR starts: {exception.Message}");
	}

	public static void Dispose() {
		if (_trackingOriginUpdated == null) return;

		foreach (var input in Inputs) if (input != null) input.remove_trackingOriginUpdated(_trackingOriginUpdated);
		Inputs.Clear();
		_trackingOriginUpdated = null;
		_warnedUnavailable = false;
	}

	private static void OnTrackingOriginUpdated(XRInputSubsystem input) { VRPlayer.Instance?.NotifyTrackingOriginUpdated(); }
}
