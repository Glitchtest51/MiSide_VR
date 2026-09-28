using System;
using System.Collections.Generic;
using HarmonyLib;
using MiSide_VR.Core;
using UnityEngine;
using static MiSide_VR.Plugin;

namespace MiSide_VR.Input.Patches;

// Location17
[HarmonyPatch(typeof(Location17_Coffee), "Update")]
public static class CoffeeInteractionPatch {
	private static Location17_Coffee _activeCoffee;
	internal static bool IsActive => _activeCoffee && _activeCoffee.teapotControl;

	[HarmonyPrefix]
	public static void Prefix(Location17_Coffee __instance) { _activeCoffee = __instance && __instance.teapotControl ? __instance : null; }
}

// PlayerMove
[HarmonyPatch(typeof(PlayerMove), "Update")]
public static class PlayerInteractionPatch {
	private static Transform _aimedCamera;
	private static Quaternion _originalRotation;

	[HarmonyPrefix]
	public static void Prefix(PlayerMove __instance) {
		RestoreCameraRotation();
		if (!__instance || !__instance.mainCamera || __instance.animationRun || GameContext.Mode != GameMode.StandardPlayer) return;

		var hand = VRInput.GetHand();
		if (!hand) return;

		var aim = hand.AimRay;
		if (!Physics.SphereCast(aim, 0.025f, out var hit, 50f, __instance.castHead)) return;

		_aimedCamera = __instance.mainCamera;
		_originalRotation = _aimedCamera.rotation;
		_aimedCamera.LookAt(hit.point);
	}

	[HarmonyFinalizer]
	public static Exception Finalizer(Exception __exception) {
		RestoreCameraRotation();
		return __exception;
	}

	private static void RestoreCameraRotation() {
		if (_aimedCamera) _aimedCamera.rotation = _originalRotation;
		_aimedCamera = null;
	}
}

// Location11
[HarmonyPatch]
static class CableCarPatch {
	private static Transform _aimedCamera;
	private static Quaternion _originalCameraRotation;

	[HarmonyPatch(typeof(Location11_Lift), "Update")]
	[HarmonyPrefix]
	private static void AimWithController(Location11_Lift __instance) {
		RestoreCameraRotation();
		if (!__instance || !__instance.turretUse || !__instance.cameraPlayer || !VRInput.IsInitialized) return;

		var hand = VRInput.GetHand();
		if (!hand) return;
		
		_aimedCamera = __instance.cameraPlayer;
		_originalCameraRotation = _aimedCamera.rotation;
		_aimedCamera.rotation = Quaternion.LookRotation(hand.AimRay.direction, hand.transform.up);
	}

	[HarmonyPatch(typeof(Location11_Lift), "Update")]
	[HarmonyPostfix]
	private static void AfterUpdate() => RestoreCameraRotation();

	[HarmonyPatch(typeof(Location11_Lift), "Update")]
	[HarmonyFinalizer]
	private static Exception FinalizeUpdate(Exception __exception) {
		RestoreCameraRotation();
		return __exception;
	}

	[HarmonyPatch(typeof(Location11_Lift), "LateUpdate")]
	[HarmonyPostfix]
	[HarmonyPriority(Priority.Last)]
	private static void RefreshHeadAnchor(Location11_Lift __instance) { if (__instance && GameContext.IsCableCarGame) VRPlayer.Instance?.ApplyPlayerBodyViewPose(); }

	private static void RestoreCameraRotation() {
		if (_aimedCamera) _aimedCamera.rotation = _originalCameraRotation;
		_aimedCamera = null;
	}
}

[HarmonyPatch]
static class GlueMinigamePatch {
	private static Location11_BlackRoom _activeMinigame;
	private static Vector3 _previousControllerPosition;
	private static bool _hasPreviousPosition;
	private static bool _restorePlayerIkAfterGlue;
	private static bool ReadingGlueMotion { get; set; }

	internal static Location11_BlackRoom ActiveMinigame => _activeMinigame && _activeMinigame.glueWork ? _activeMinigame : null;
	internal static bool TryConsumePlayerIkRestore() {
		if (!_restorePlayerIkAfterGlue) return false;
		_restorePlayerIkAfterGlue = false;
		return true;
	}

	[HarmonyPatch(typeof(Location11_BlackRoom), "LateUpdate")]
	[HarmonyPrefix]
	private static void BeforeGlueUpdate(Location11_BlackRoom __instance) {
		ReadingGlueMotion = __instance && __instance.glueWork;
		if (!ReadingGlueMotion || !VRInput.IsInitialized) {
			ResetTracking();
			return;
		}
		_restorePlayerIkAfterGlue = true;

		var controller = VRInput.GetHand();
		if (!controller) {
			ResetTracking();
			return;
		}

		if (_activeMinigame != __instance) {
			_activeMinigame = __instance;
			_hasPreviousPosition = false;
		}

		var currentPosition = controller.transform.position;
		if (_hasPreviousPosition) {
			var delta = currentPosition - _previousControllerPosition;
			var tablePosition = __instance.positionMouse;
			// these are MiSides limits from Location11_BlackRoom.LateUpdate
			tablePosition.x = Mathf.Clamp(tablePosition.x + delta.x, -6.06f, -5.60f);
			tablePosition.y = 0.907f;
			tablePosition.z = Mathf.Clamp(tablePosition.z + delta.z, -3.64f, -3.32f);
			__instance.positionMouse = tablePosition;
		}

		_previousControllerPosition = currentPosition;
		_hasPreviousPosition = true;
	}

	[HarmonyPatch(typeof(Location11_BlackRoom), "LateUpdate")]
	[HarmonyPostfix]
	private static void AfterGlueUpdate() => ReadingGlueMotion = false;

	[HarmonyPatch(typeof(Location11_BlackRoom), "LateUpdate")]
	[HarmonyFinalizer]
	private static Exception FinalizeGlueUpdate(Exception __exception) {
		ReadingGlueMotion = false;
		return __exception;
	}

	[HarmonyPatch(typeof(GlobalGame), "GetMouseX")]
	[HarmonyPrefix]
	private static bool ReplaceGlueMouseX(ref float __result) => ReplaceGlueMouseAxis(ref __result);

	[HarmonyPatch(typeof(GlobalGame), "GetMouseY")]
	[HarmonyPrefix]
	private static bool ReplaceGlueMouseY(ref float __result) => ReplaceGlueMouseAxis(ref __result);

	private static bool ReplaceGlueMouseAxis(ref float result) {
		if (!ReadingGlueMotion) return true;
		result = 0f;
		return false;
	}

	private static void ResetTracking() {
		_activeMinigame = null;
		_hasPreviousPosition = false;
	}
}

// Location19
[HarmonyPatch]
static class GlitchMinigamePatch {
	private const float GrabRadius = 0.25f;
	private const float LaserLift = 0.02f;
	private const float SliderMinimum = -0.174f;
	private const float SliderMaximum = 0.174f;
	private const float SphereBoardRadius = 0.5f;
	private const float SphereBoardForwardOffset = 0.75f;
	private const float Game3HandVisualDown = 0.07f;

	private static Location19_Game1 _dragGame;
	private static int _dragIndex = -1;
	private static float _dragStartAimZ;
	private static float _dragStartLocalZ;
	private static bool _triggerWasHeld;
	private static VRController _offsetController;
	private static Vector3 _savedMuzzlePosition;
	private static Location19_Game2 _sphereGame;
	private static Vector3 _filteredSphereHand;
	private static bool _hasFilteredSphereHand;
	private static Location19_Game3 _activeGame3;
	private static Transform _game3Gradient;
	private static Vector3 _game3GradientPosition;
	private static Quaternion _game3GradientRotation;

	internal static Location19_Game3 ActiveGame3 => _activeGame3 && _activeGame3.isActiveAndEnabled && _activeGame3.main && _activeGame3.main.play ? _activeGame3 : null;

	[HarmonyPatch(typeof(Location19_GlitchGame), nameof(Location19_GlitchGame.Update))]
	[HarmonyPostfix]
	private static void UpdateBoard(Location19_GlitchGame __instance) {
		if (!__instance || !VRInput.IsInitialized) {
			Reset(false);
			return;
		}

		// accept a trigger, stick flick, or controller chop for the pregame prompt
		var triggerHeld = VRInput.GetButton("Interact");
		if (!__instance.play) {
			var stickDown = VRInput.GetVector2("Move").y < -0.5f;
			var controllerDown = VRInput.GetMappedAxis("Mouse Y") < -0.001f;
			if (__instance.kickCan && __instance.lineKick && (triggerHeld || stickDown || controllerDown)) {
				var size = __instance.lineKick.sizeDelta;
				size.y = -1f;
				__instance.lineKick.sizeDelta = size;
			}
			Reset(false);
			return;
		}

		if (!__instance.handPlayer) {
			Reset(false);
			return;
		}

		var controller = VRInput.GetHand();
		if (!controller || !controller.muzzle) {
			Reset(false);
			return;
		}

		ApplyLaserLift(controller);
		// disable flat camera shake because it moves the entire VR world
		__instance.cameraNoise = 0f;
		if (__instance.cameraTarget) __instance.cameraTarget.position = __instance.positionCamera;

		var plane = new Plane(Vector3.up, __instance.handPlayer.position);
		var ray = controller.AimRay;
		var hasTarget = plane.Raycast(ray, out var distance) && distance is > 0.05f and < 20f;
		var target = hasTarget ? ray.GetPoint(distance) : default;
		var sphereGame = UnityEngine.Object.FindObjectOfType<Location19_Game2>();
		if (sphereGame && sphereGame.main == __instance && sphereGame.isActiveAndEnabled) {
			UpdateSphereGame(__instance, sphereGame, target, hasTarget);
			_triggerWasHeld = triggerHeld;
			return;
		}

		if (hasTarget) {
			__instance.positionHand = target - __instance.handAddPosition;
			if (!_dragGame) {
				var game3 = GetActiveGame3(__instance);
				if (game3) {
					// move the native hand while keeping its original hit logic
					var screenDown = __instance.cameraT ? Vector3.ProjectOnPlane(-__instance.cameraT.transform.up, Vector3.up).normalized : Vector3.back;
					if (screenDown.sqrMagnitude < 0.01f) screenDown = Vector3.back;
					__instance.handPlayer.position = target + screenDown * Game3HandVisualDown;
				} else {
					__instance.handPlayer.position = target;
				}
			}
		}

		UpdateDrag(__instance, target, hasTarget);
	}

	private static void UpdateSphereGame(Location19_GlitchGame main, Location19_Game2 game, Vector3 aimPoint, bool hasAimPoint) {
		if (!hasAimPoint) return;

		// keep Game2s hand inside the current peg and board limits
		var boardCenter = new Vector3(main.positionCamera.x, aimPoint.y, main.positionCamera.z + SphereBoardForwardOffset);
		var constrained = ClampHorizontal(aimPoint, boardCenter, SphereBoardRadius);

		if (game.useSphere && game.points != null && game.indexPointHold >= 0 && game.indexPointHold < game.points.Count) {
			var point = game.points[game.indexPointHold];
			if (point != null && point.point) {
				var center = point.point.transform.position;
				center.y = constrained.y;
				constrained = ClampHorizontal(constrained, center, point.distance);
			}
			game.positionHand = constrained;
		}

		main.positionHand = constrained;
		if (game.hand) {
			if (_sphereGame != game || !_hasFilteredSphereHand || (_filteredSphereHand - constrained).sqrMagnitude > 1f) {
				_sphereGame = game;
				_filteredSphereHand = constrained;
				_hasFilteredSphereHand = true;
			} else {
				// dilter small ray movement without delaying deliberate movement
				var error = Vector3.Distance(_filteredSphereHand, constrained);
				var response = Mathf.Lerp(45f, 100f, Mathf.InverseLerp(0.003f, 0.06f, error));
				var blend = 1f - Mathf.Exp(-response * Time.unscaledDeltaTime);
				_filteredSphereHand = Vector3.Lerp(_filteredSphereHand, constrained, blend);
			}
			game.hand.position = _filteredSphereHand;
		} else if (main.handPlayer) main.handPlayer.position = constrained;
	}

	private static Vector3 ClampHorizontal(Vector3 position, Vector3 center, float radius) {
		var offset = new Vector2(position.x - center.x, position.z - center.z);
		if (offset.sqrMagnitude <= radius * radius) return position;
		offset = offset.normalized * radius;
		return new Vector3(center.x + offset.x, position.y, center.z + offset.y);
	}

	[HarmonyPatch(typeof(Location19_Game3), nameof(Location19_Game3.Update))]
	[HarmonyPostfix]
	private static void UpdateGame3(Location19_Game3 __instance) {
		if (!__instance || !__instance.isActiveAndEnabled || !__instance.main) return;
		if (!__instance.main.play) {
			if (_activeGame3 == __instance) {
				_activeGame3 = null;
				_game3Gradient = null;
			}
			return;
		}

		if (_activeGame3 != __instance || !_game3Gradient) {
			_activeGame3 = __instance;
			_game3Gradient = __instance.main.transform.Find("Game/Camera/Gradient");
			if (_game3Gradient) {
				_game3GradientPosition = _game3Gradient.position;
				_game3GradientRotation = _game3Gradient.rotation;
			}
		}

		// keep Game3s camera parented gradient fixed in VR.
		if (_game3Gradient) _game3Gradient.SetPositionAndRotation(_game3GradientPosition, _game3GradientRotation);
	}

	private static Location19_Game3 GetActiveGame3(Location19_GlitchGame main) {
		var game = ActiveGame3;
		if (game && game.main == main) return game;

		game = UnityEngine.Object.FindObjectOfType<Location19_Game3>();
		if (!game || !game.isActiveAndEnabled || game.main != main) return null;
		_activeGame3 = game;
		return game;
	}

	[HarmonyPatch(typeof(Location19_Game4), nameof(Location19_Game4.LateUpdate))]
	[HarmonyPostfix]
	private static void UpdateGame4(Location19_Game4 __instance) {
		if (!__instance || !__instance.isActiveAndEnabled || !__instance.main || !__instance.main.play || !__instance.main.handPlayer) return;

		// keep Game4s hand on its native one-axis path
		__instance.main.handPlayer.position = __instance.main.positionHand;

		// refresh the shot origin after correcting the hand
		if (__instance.positionShot && __instance.particleShot) __instance.positionShot.position = __instance.particleShot.transform.position;
	}

	[HarmonyPatch(typeof(Location19_GlitchGame), nameof(Location19_GlitchGame.Finish))]
	[HarmonyPostfix]
	private static void AfterFinish() => Reset(false);

	[HarmonyPatch(typeof(Location19_GlitchGame), nameof(Location19_GlitchGame.StopGame))]
	[HarmonyPrefix]
	private static void BeforeStop() => Reset(false);

	private static void UpdateDrag(Location19_GlitchGame main, Vector3 aimPoint, bool hasAimPoint) {
		var game = _dragGame;
		if (!game) {
			game = UnityEngine.Object.FindObjectOfType<Location19_Game1>();
			if (!game || game.main != main || !game.isActiveAndEnabled) game = null;
		}

		var triggerHeld = VRInput.GetButton("Interact");
		if (_dragGame && !triggerHeld) ReleaseDrag(true);

		if (!_dragGame && game && hasAimPoint && triggerHeld && !_triggerWasHeld) {
			var bestIndex = -1;
			var bestDistance = GrabRadius * GrabRadius;
			for (var index = 0; index < game.cases.Length; index++) {
				var slider = game.cases[index];
				if (!slider || !slider.gameObject.activeInHierarchy) continue;
				var delta = slider.position - aimPoint;
				var sqrDistance = delta.x * delta.x + delta.z * delta.z;
				if (sqrDistance >= bestDistance) continue;
				bestDistance = sqrDistance;
				bestIndex = index;
			}

			if (bestIndex >= 0) {
				_dragGame = game;
				_dragIndex = bestIndex;
				_dragStartAimZ = aimPoint.z;
				_dragStartLocalZ = game.cases[bestIndex].localPosition.z;
				game.ClickCaseDown(bestIndex);
			}
		}

		if (_dragGame && hasAimPoint && _dragIndex >= 0 && _dragIndex < _dragGame.cases.Length) {
			var slider = _dragGame.cases[_dragIndex];
			if (slider) {
				var local = slider.localPosition;
				local.z = Mathf.Clamp(_dragStartLocalZ + aimPoint.z - _dragStartAimZ, SliderMinimum, SliderMaximum);
				slider.localPosition = local;
			}
		}

		_triggerWasHeld = triggerHeld;
	}

	private static void ApplyLaserLift(VRController controller) {
		if (_offsetController == controller) return;
		RestoreLaser();
		_offsetController = controller;
		_savedMuzzlePosition = controller.muzzle.localPosition;
		controller.muzzle.localPosition = _savedMuzzlePosition + Vector3.up * LaserLift;
	}

	private static void RestoreLaser() {
		if (_offsetController && _offsetController.muzzle) _offsetController.muzzle.localPosition = _savedMuzzlePosition;
		_offsetController = null;
		_savedMuzzlePosition = Vector3.zero;
	}

	private static void ReleaseDrag(bool notifyGame) {
		if (notifyGame && _dragGame && _dragGame.isActiveAndEnabled && _dragGame.main && _dragGame.main.play && _dragGame.indexCaseChangeNow >= 0) _dragGame.ClickCaseUp();
		_dragGame = null;
		_dragIndex = -1;
	}

	private static void Reset(bool notifyGame) {
		ReleaseDrag(notifyGame);
		RestoreLaser();
		_sphereGame = null;
		_filteredSphereHand = Vector3.zero;
		_hasFilteredSphereHand = false;
		_triggerWasHeld = VRInput.IsInitialized && VRInput.GetButton("Interact");
	}
}

// Shooter
[HarmonyPatch]
static class HetoorPatch {
	private const float TurnActivation = 0.7f;
	private const float TurnReset = 0.2f;
	private const float SmoothTurnDeadzone = 0.2f;
	private const float GunBackOffset = 0.10f;

	private static Shooter_Player _player;
	private static Transform _arms;
	private static Transform _gun;
	private static Transform _muzzle;
	private static Transform _gunParent;
	private static Vector3 _gunLocalPosition;
	private static Quaternion _gunLocalRotation;
	private static Quaternion _muzzleFromGun;
	private static Vector3 _muzzleOffsetFromGun;
	private static bool _snapTurnReady = true;
	private static readonly List<Renderer> HiddenArmRenderers = new();

	[HarmonyPatch(typeof(Shooter_Player), "Update")]
	[HarmonyPrefix]
	private static void BeforePlayerUpdate(Shooter_Player __instance) {
		if (!IsActive(__instance)) return;

		ResolveModel(__instance);
		ApplyTurn(__instance);
		ApplyAim(__instance);
		PlaceGun();
	}

	[HarmonyPatch(typeof(Shooter_Player), "Update")]
	[HarmonyPostfix]
	private static void AfterPlayerUpdate(Shooter_Player __instance) { if (IsActive(__instance)) PlaceGun(); }

	[HarmonyPatch(typeof(VRPlayer), "ApplyBeforeRenderPose")]
	[HarmonyPostfix]
	[HarmonyPriority(Priority.Last)]
	private static void BeforeVrRender() {
		if (GameContext.Mode == GameMode.Hetoor && GameContext.HetoorPlayer) PlaceGun();
		else ReleaseModel();
	}

	private static bool IsActive(Shooter_Player player) => player && player.isActiveAndEnabled && GameContext.Mode == GameMode.Hetoor && VRInput.IsInitialized;

	private static void ApplyAim(Shooter_Player player) {
		var hand = VRInput.GetHand();
		if (!hand || !player.head || !player.head.parent) return;
		
		var localDirection = player.head.parent.InverseTransformDirection(hand.AimRay.direction);
		if (localDirection.sqrMagnitude < 0.0001f) return;
		var angles = Quaternion.LookRotation(localDirection, Vector3.up).eulerAngles;
		player.rotX = angles.y;
		player.rotY = Mathf.Clamp(Mathf.DeltaAngle(0f, angles.x), -89f, 89f);
	}

	private static void ApplyTurn(Shooter_Player player) {
		var axis = VRInput.GetVector2("Turn").x;
		var amount = 0f;
		switch (TurningStyle) {
			case TurnStyle.Snap:
				if (Mathf.Abs(axis) > TurnActivation && _snapTurnReady) {
					amount = Mathf.Sign(axis) * SnapTurnAngle;
					_snapTurnReady = false;
				} else if (Mathf.Abs(axis) < TurnReset) { _snapTurnReady = true; }
				break;
			case TurnStyle.Smooth:
				_snapTurnReady = true;
				if (Mathf.Abs(axis) > SmoothTurnDeadzone) amount = Mathf.Sign(axis) * Mathf.InverseLerp(SmoothTurnDeadzone, 1f, Mathf.Abs(axis)) * SmoothTurnSpeed * Time.deltaTime;
				break;
			default: _snapTurnReady = true; break;
		}

		if (Mathf.Abs(amount) > 0.0001f) player.transform.rotation = Quaternion.Euler(0f, player.transform.eulerAngles.y + amount, 0f);
	}

	private static void ResolveModel(Shooter_Player player) {
		if (_player == player && _gun && _muzzle) return;
		ReleaseModel();
		_player = player;
		if (!player.head) return;

		_arms = player.head.Find("Camera/Arms");
		var wrist = _arms ? _arms.Find("Armature/Right arm/Right elbow/Right wrist") : null;
		_gun = wrist ? wrist.Find("Right item/Gun") : null;
		_muzzle = _gun ? _gun.Find("PointShoot") : null;
		if (!_gun || !_muzzle) return;

		_gunParent = _gun.parent;
		_gunLocalPosition = _gun.localPosition;
		_gunLocalRotation = _gun.localRotation;
		_muzzleFromGun = Quaternion.Inverse(_gun.rotation) * _muzzle.rotation;
		_muzzleOffsetFromGun = Quaternion.Inverse(_gun.rotation) * (_muzzle.position - _gun.position);
		
		foreach (var renderer in _arms.GetComponentsInChildren<Renderer>(true)) {
			if (!renderer || renderer.transform.IsChildOf(_gun) || !renderer.enabled) continue;
			renderer.enabled = false;
			HiddenArmRenderers.Add(renderer);
		}
	}

	private static void PlaceGun() {
		if (!_gun || !_muzzle || !VRInput.IsInitialized) return;
		var hand = VRInput.GetHand();
		if (!hand) return;
		
		var ray = hand.AimRay;
		var muzzleRotation = Quaternion.LookRotation(ray.direction, hand.muzzle ? hand.muzzle.up : hand.transform.up);
		var gunRotation = muzzleRotation * Quaternion.Inverse(_muzzleFromGun);
		_gun.SetPositionAndRotation(ray.origin - ray.direction * GunBackOffset - gunRotation * _muzzleOffsetFromGun, gunRotation);
	}

	private static void ReleaseModel() {
		foreach (var renderer in HiddenArmRenderers) if (renderer) renderer.enabled = true;
		HiddenArmRenderers.Clear();

		if (_gun && _gunParent) {
			_gun.SetParent(_gunParent, false);
			_gun.localPosition = _gunLocalPosition;
			_gun.localRotation = _gunLocalRotation;
		}
		_player = null;
		_arms = null;
		_gun = null;
		_muzzle = null;
		_gunParent = null;
	}
}

// CarSpace
[HarmonyPatch]
static class SpaceRacerPatch {
	private const float SteeringDeadzone = 0.18f;
	private const float SteeringStrength = 0.65f;

	[HarmonyPatch(typeof(CarSpace_Car), nameof(CarSpace_Car.FixedUpdate))]
	[HarmonyPrefix]
	private static void ApplySteering(CarSpace_Car __instance) {
		var player = GameContext.SpaceRacerPlayer;
		if (!player || !player.isActiveAndEnabled || !player.play || player.carController != __instance || __instance.destroyed || !VRInput.IsInitialized) return;
		
		var raw = VRInput.GetVector2("Move").x;
		var amount = Mathf.InverseLerp(SteeringDeadzone, 1f, Mathf.Abs(raw));
		__instance.rotateNow = Mathf.Sign(raw) * amount * amount * SteeringStrength;
	}
}
