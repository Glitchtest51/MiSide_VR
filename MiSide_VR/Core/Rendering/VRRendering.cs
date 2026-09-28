using System;
using MiSide_VR.Input;
using UnityEngine;
using UnityEngine.SubsystemsImplementation;
using UnityEngine.XR;
using static MiSide_VR.Plugin;

namespace MiSide_VR.Core.Rendering;

public sealed class VRRendering : IDisposable {
	public XRDisplaySubsystem Display { get; private set; }
	public XRInputSubsystem Input { get; private set; }

	public void Start() {
		SubsystemManager.StaticConstructScriptingClassMap();
		
		foreach (var descriptor in SubsystemDescriptorStore.s_IntegratedDescriptors) {
			if (DebugMode) Log.LogDebug($"XR subsystem descriptor: {descriptor.id}");
			if (descriptor.id == "OpenVR Display") Display = WrapDisplaySubsystem(descriptor);
			else if (descriptor.id == "OpenVR Input") Input = WrapInputSubsystem(descriptor);
		}

		if (Display == null || Input == null) throw new InvalidOperationException("OpenVR display/input descriptors were not found. Check the preloader runtime payload.");

		Display.Start();
		Input.Start();
		try { VRInput.Initialize(); }
		catch (Exception exception) { Log.LogError($"SteamVR input startup failed: {exception}"); }
		if (DebugMode) Log.LogDebug($"OpenVR started: display={Display.running}, input={Input.running}");
	}

	private static XRDisplaySubsystem WrapDisplaySubsystem(IntegratedSubsystemDescriptor descriptor) {
		var raw = CreateRawSubsystem(descriptor);
		var display = new XRDisplaySubsystem(raw.Pointer);
		display.m_SubsystemDescriptor = descriptor.Cast<ISubsystemDescriptor>();
		return display;
	}

	private static XRInputSubsystem WrapInputSubsystem(IntegratedSubsystemDescriptor descriptor) {
		var raw = CreateRawSubsystem(descriptor);
		var input = new XRInputSubsystem(raw.Pointer);
		input.m_SubsystemDescriptor = descriptor.Cast<ISubsystemDescriptor>();
		return input;
	}

	private static IntegratedSubsystem CreateRawSubsystem(IntegratedSubsystemDescriptor descriptor) {
		var pointer = SubsystemDescriptorBindings.Create(descriptor.m_Ptr);
		var subsystem = SubsystemManager.GetIntegratedSubsystemByPtr(pointer);
		return subsystem ?? throw new InvalidOperationException($"Unity created no managed subsystem for {descriptor.id}.");
	}

	public void Dispose() {
		Input?.Stop();
		Display?.Stop();
		Input?.Destroy();
		Display?.Destroy();
	}
}
