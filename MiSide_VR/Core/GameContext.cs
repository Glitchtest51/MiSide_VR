using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using static MiSide_VR.Plugin;

namespace MiSide_VR.Core;

public enum GameMode { Unsupported, MenuOrLoading, StandardPlayer, PlayerAnimation, Tamagotchi, Novella, Hetoor, SpaceRacer, MakeManeken, GlitchMinigame }

// tracks the active game mode and cameras
public static class GameContext {
	public static GameMode Mode { get; private set; }
	public static PlayerMove PlayerMove { get; private set; }
	public static Camera SourceCamera { get; private set; }
	public static Camera OverlayCamera { get; private set; }
	public static Scene Scene { get; private set; }
	public static bool IsSeatedTvGame { get; private set; }
	public static bool IsDancePadGame { get; private set; }
	public static bool IsCableCarGame { get; private set; }
	public static bool IsPcMonitorGame { get; private set; }
	public static bool UsesPlayerBodyView => IsSeatedTvGame || IsDancePadGame || IsCableCarGame || IsPcMonitorGame;
	public static CarSpace_Player SpaceRacerPlayer { get; private set; }
	public static Shooter_Player HetoorPlayer { get; private set; }
	public static Camera AuthoredPoseCamera { get; private set; }

	private static PlayerMove _scenePlayer;
	private static Camera _scenePlayerCamera;
	private static int _scenePlayerSceneHandle = -1;

	public static bool IsPanelMode => Mode is GameMode.MenuOrLoading or GameMode.Novella;

	public static void Refresh() {
		var previousMode = Mode;
		var previousCamera = SourceCamera;
		var previousOverlay = OverlayCamera;

		Scene = SceneManager.GetActiveScene();
		PlayerMove = FindActive<PlayerMove>();
		OverlayCamera = null;
		IsSeatedTvGame = false;
		IsDancePadGame = false;
		IsCableCarGame = false;
		IsPcMonitorGame = false;
		SpaceRacerPlayer = null;
		HetoorPlayer = null;
		AuthoredPoseCamera = null;

		// resolve 2d areas first because their leftover playermove objects
		if (Scene.name == "Scene 18 - 2D" || FindActive<Location18_Novella>()) {
			Mode = GameMode.Novella;
			SourceCamera = FindGameCamera("Novella");
		} else if (FindActive<Tamagotchi_Main>() is { play : true }) {
			Mode = GameMode.Tamagotchi;
			SourceCamera = FindGameCamera("World TamagotchiHouse");
			OverlayCamera = IsActiveObject("Dialogue", "InterfaceTamagotchi") ? FindOverlayCamera("CameraMita") : null;
		} else if (FindActive<CarSpace_Player>() is {} carPlayer && carPlayer.isActiveAndEnabled && carPlayer.cameraView && carPlayer.cameraView.gameObject.activeInHierarchy) {
			// Arcade games keep playermove alive so do this first
			Mode = GameMode.SpaceRacer;
			SpaceRacerPlayer = carPlayer;
			PlayerMove = null;
			SourceCamera = carPlayer.cameraView;
			AuthoredPoseCamera = carPlayer.cameraView;
		} else if (FindActive<MakeManeken_Main>() is {} makeManeken && makeManeken.isActiveAndEnabled && makeManeken.cameraMain && makeManeken.cameraMain.gameObject.activeInHierarchy) {
			var gameplayCamera = PlayerMove && PlayerMove.mainCamera ? PlayerMove.mainCamera.GetComponent<Camera>() : null;
			Mode = GameMode.MakeManeken;
			PlayerMove = null;
			SourceCamera = gameplayCamera ? gameplayCamera : makeManeken.cameraMain;
			AuthoredPoseCamera = makeManeken.cameraMain;
		} else if (FindActive<Shooter_Main>() is {} hetoor) {
			Mode = GameMode.Hetoor;
			PlayerMove = null;
			HetoorPlayer = FindActive<Shooter_Player>();
			SourceCamera = FindActiveCameraUnder(hetoor.transform);
		} else if (FindActive<Location19_GlitchGame>() is { play : true } glitchMinigame && glitchMinigame.cameraT && glitchMinigame.cameraT.gameObject.activeInHierarchy) {
			Mode = GameMode.GlitchMinigame;
			PlayerMove = null;
			SourceCamera = glitchMinigame.cameraT;
			AuthoredPoseCamera = glitchMinigame.cameraT;
		} else if (PlayerMove) {
			RememberScenePlayer(PlayerMove);
			IsSeatedTvGame = Scene.name == "Scene 4 - StartSecret" && PlayerMove.animationRun && FindActive<MinigamesTelevisionController>() is { activation : true };
			IsPcMonitorGame = Scene.name == "Scene 14 - MobilePlayer" && PlayerMove.animationRun && FindActive<Location14_PCGames>() is { gamePlay : true };
			if (Scene.name == "Scene 11 - Backrooms" && PlayerMove.animationRun && FindActive<Location11_Lift>() is {} lift && lift.typePath == 3) IsCableCarGame = true;
			Mode = PlayerMove.animationRun ? GameMode.PlayerAnimation : GameMode.StandardPlayer;
			SourceCamera = PlayerMove.mainCamera ? PlayerMove.mainCamera.GetComponent<Camera>() : null;
			if (PlayerMove.animationRun && SourceCamera && FindActive<Location7_GameDance>() is {} dance && dance.isActiveAndEnabled && dance.playerPositionDance) IsDancePadGame = SourceCamera.transform.IsChildOf(dance.playerPositionDance);
		} else if (TryResolveSceneCinematic(out var cinematicPlayer, out var cinematicCamera)) {
			Mode = GameMode.PlayerAnimation;
			PlayerMove = cinematicPlayer;
			SourceCamera = cinematicCamera;
		} else if (Scene.name is "SceneMenu" or "SceneLoading" or "SceneAihasto") {
			Mode = GameMode.MenuOrLoading;
			SourceCamera = FindGameCamera(allowFallback: true);
		} else if (FindGameCamera() is {} fallbackCamera) {
			// fallback to the active camera for unsupported cutscenes
			Mode = GameMode.PlayerAnimation;
			PlayerMove = null;
			SourceCamera = fallbackCamera;
		} else {
			Mode = GameMode.Unsupported;
			SourceCamera = null;
		}

		if ((previousMode != Mode || previousCamera != SourceCamera || previousOverlay != OverlayCamera) && DebugMode) Log.LogDebug($"[GameContext] {previousMode} -> {Mode}, scene={Scene.name}, camera={(SourceCamera ? SourceCamera.name : "<none>")}, overlay={(OverlayCamera ? OverlayCamera.name : "<none>")}");
	}

	private static void RememberScenePlayer(PlayerMove player) {
		_scenePlayer = player;
		_scenePlayerCamera = player.mainCamera ? player.mainCamera.GetComponent<Camera>() : null;
		_scenePlayerSceneHandle = Scene.handle;
	}

	private static bool TryResolveSceneCinematic(out PlayerMove player, out Camera camera) {
		player = null;
		camera = null;
		if (Scene.name != "Scene 1 - RealRoom" || _scenePlayerSceneHandle != Scene.handle || !_scenePlayer || !_scenePlayerCamera) return false;

		player = _scenePlayer;
		camera = _scenePlayerCamera;
		return true;
	}

	private static T FindActive<T>() where T : Component {
		foreach (var value in UnityEngine.Object.FindObjectsOfType<T>(true)) if (value && value.gameObject.activeInHierarchy) return value;
		return null;
	}

	private static Camera FindGameCamera(string preferredAncestor = null, bool allowFallback = false) {
		Camera fallback = null;
		foreach (var camera in UnityEngine.Object.FindObjectsOfType<Camera>(true)) {
			if (!IsUsableSceneCamera(camera)) continue;
			if (camera.name.Contains("Persons") || camera.name.Contains("Interface3D")) continue;
			if (preferredAncestor != null && HasAncestorContaining(camera.transform, preferredAncestor)) return camera;
			if (camera.CompareTag("MainCamera")) return camera;
			fallback ??= camera;
		}
		return allowFallback ? fallback : null;
	}

	private static Camera FindOverlayCamera(string ancestor) {
		foreach (var camera in UnityEngine.Object.FindObjectsOfType<Camera>(true)) if (IsUsableSceneCamera(camera) && HasAncestorContaining(camera.transform, ancestor)) return camera;
		return null;
	}

	private static Camera FindActiveCameraUnder(Transform root) {
		if (!root) return null;

		Camera selected = null;
		foreach (var camera in UnityEngine.Object.FindObjectsOfType<Camera>(true)) {
			if (!camera || !camera.isActiveAndEnabled || IsVRCamera(camera) || !camera.transform.IsChildOf(root)) continue;
			if (!selected || camera.depth > selected.depth) selected = camera;
		}
		return selected;
	}

	private static bool IsActiveObject(string objectName, string ancestor) {
		foreach (var value in UnityEngine.Object.FindObjectsOfType<Transform>(true)) if (value && value.gameObject.activeInHierarchy && value.name == objectName && HasAncestorContaining(value, ancestor)) return true;
		return false;
	}

	private static bool IsVRCamera(Camera camera) {
		for (var current = camera.transform; current; current = current.parent) if (current.name is "[VRPlayer]" or "VRSystem") return true;
		return false;
	}

	private static bool IsUsableSceneCamera(Camera camera) => camera && camera.isActiveAndEnabled && camera.gameObject.scene == Scene && !IsVRCamera(camera);

	private static bool HasAncestorContaining(Transform value, string text) {
		while (value) {
			if (value.name.Contains(text, StringComparison.OrdinalIgnoreCase)) return true;
			value = value.parent;
		}
		return false;
	}
}
