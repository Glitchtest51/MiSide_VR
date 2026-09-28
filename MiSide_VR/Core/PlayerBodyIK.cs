using System;
using System.Collections.Generic;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using MiSide_VR.Input;
using MiSide_VR.Input.Patches;
using RootMotion;
using RootMotion.FinalIK;
using UnityEngine;
using static MiSide_VR.Plugin;

namespace MiSide_VR.Core;

// uses VRIK for tracked head and hands while MiSide keeps animation and feet
sealed class PlayerBodyIK : IDisposable {
	private const float BlendSpeed = 5f;
	private const float ForearmTwistWeight = 0.55f;
	private static readonly Vector3 LeftGripFineOffset = new(-0.0254f, 0.0391f, -0.063f);
	private static readonly Vector3 RightGripFineOffset = new(0.0254f, 0.0391f, -0.063f);
	private static readonly Vector3 LeftFallbackHandTargetOffset = new(-0.0254f, 0.0391f, -0.163f);
	private static readonly Vector3 RightFallbackHandTargetOffset = new(0.0254f, 0.0391f, -0.163f);

	private Transform _person;
	private Transform _bindCandidate;
	private Transform _failedPerson;
	private PlayerPersonIK _gameIk;
	private Animator _animator;
	private int _handsLayer = -1;
	private VRIK _vrik;
	private Transform _pendingVrikPerson;
	private VRIK _pendingVrikRemoval;
	private GameObject _headTarget;
	private GameObject _leftHandTarget;
	private GameObject _rightHandTarget;
	private TwistSolver[] _leftTwistSolvers = Array.Empty<TwistSolver>();
	private TwistSolver[] _rightTwistSolvers = Array.Empty<TwistSolver>();
	private float _solverWeight;
	private float _bodyWeight;
	private float _leftArmWeight;
	private float _rightArmWeight;
	private float _nextBindAttemptTime;
	private bool _waitingForSkeleton;
	private bool _waitingForStableGameplay;
	private int _stableGameplayFrames;
	private bool _leftGripResolved;
	private bool _rightGripResolved;
	private bool _leftAimResolved;
	private bool _rightAimResolved;
	private float _nextAttachmentRetryTime;
	private int _lastSolvedFrame = -1;

	public void Update(VRPlayer rig) {
		var supportedMode = rig.mode is GameMode.StandardPlayer or GameMode.PlayerAnimation;
		var person = supportedMode ? rig.playerPerson : null;
		if (person != _bindCandidate) {
			Release();
			_bindCandidate = person;
			_failedPerson = null;
			_nextBindAttemptTime = 0f;
			_waitingForSkeleton = false;
			_waitingForStableGameplay = false;
			_stableGameplayFrames = 0;
		}
		if (!_vrik && person && person != _failedPerson) {
			// bind from a stable gameplay pose so VRIK does not cache a cutscene pose
			var stableGameplay = rig.mode == GameMode.StandardPlayer && rig.playerMove && !rig.playerMove.animationRun;
			if (!stableGameplay) {
				_stableGameplayFrames = 0;
				if (!_waitingForStableGameplay) {
					_waitingForStableGameplay = true;
					if (DebugMode) Log.LogDebug("[PlayerBodyIK] Waiting for stable gameplay before binding VRIK.");
				}
			} else if (++_stableGameplayFrames >= 3 && Time.unscaledTime >= _nextBindAttemptTime) {
				_waitingForStableGameplay = false;
				TryBind(rig, person);
			}
		}

		if (!_vrik || !_gameIk || !rig.playerMove) return;
		RefreshControllerAttachments(rig);
		if (rig.mode == GameMode.StandardPlayer && !rig.playerMove.animationRun && GlueMinigamePatch.TryConsumePlayerIkRestore()) {
			// Location11 can leave otherControl enabled after gluing
			_gameIk.otherControl = false;
			if (DebugMode) Log.LogDebug("[PlayerBodyIK] Restored tracked arms after glue minigame.");
		}

		// these minigames keep their body pose but allow tracked arms
		var minigameArms = GameContext.IsSeatedTvGame || GameContext.IsDancePadGame || GameContext.IsPcMonitorGame;
		// moving the wrist also moves the teapot and its fill collider
		var coffeeRightArm = CoffeeInteractionPatch.IsActive;
		var fullBodyAnimation = rig.mode != GameMode.StandardPlayer || rig.playerMove.animationRun;
		// the plate carry keeps its finger pose while VRIK tracks the wrists
		var holdingPlate = !fullBodyAnimation && rig.playerMove.animationHandRun && IsHoldingPlate();
		var authoredArms = fullBodyAnimation || (rig.playerMove.animationHandRun && !holdingPlate) || _gameIk.animationArms || _gameIk.otherControl;
		var trackBody = !fullBodyAnimation;
		var trackUnauthoredArms = trackBody && !authoredArms;
		var trackLeftArm = minigameArms || trackUnauthoredArms && !_gameIk.leftHandUse && !_gameIk.weightLimbLeftOff;
		// wrist parented items keep their game authored finger pose
		var rightHandHoldingItem = _gameIk.objectInHand;
		var trackRightArm = minigameArms || coffeeRightArm || trackUnauthoredArms && (!_gameIk.rightHandUse || rightHandHoldingItem) && !_gameIk.weightLimbRightOff;
		var solverActive = trackBody || trackLeftArm || trackRightArm;

		var step = BlendSpeed * Time.deltaTime;
		_solverWeight = Mathf.MoveTowards(_solverWeight, solverActive ? 1f : 0f, step);
		_bodyWeight = Mathf.MoveTowards(_bodyWeight, trackBody ? 1f : 0f, step);
		_leftArmWeight = Mathf.MoveTowards(_leftArmWeight, trackLeftArm ? 1f : 0f, step);
		_rightArmWeight = Mathf.MoveTowards(_rightArmWeight, trackRightArm ? 1f : 0f, step);

		_vrik.solver.IKPositionWeight = _solverWeight;
		_vrik.solver.spine.positionWeight = _bodyWeight;
		_vrik.solver.spine.rotationWeight = _bodyWeight;
		_vrik.solver.leftArm.positionWeight = _leftArmWeight;
		_vrik.solver.leftArm.rotationWeight = _leftArmWeight;
		_vrik.solver.rightArm.target = _rightHandTarget.transform;
		_vrik.solver.rightArm.positionWeight = _rightArmWeight;
		_vrik.solver.rightArm.rotationWeight = _rightArmWeight;
	}

	private void TryBind(VRPlayer rig, Transform person) {
		try {
			var gameIk = person.GetComponent<PlayerPersonIK>();
			var fbbik = gameIk ? gameIk.scrfbbik : null;
			var references = fbbik ? fbbik.references : null;
			if (!HasCompleteSkeleton(gameIk, fbbik, references)) {
				_nextBindAttemptTime = Time.unscaledTime + 0.25f;
				if (!_waitingForSkeleton) {
					_waitingForSkeleton = true;
					if (DebugMode) Log.LogDebug("[PlayerBodyIK] Waiting for player skeleton initialization.");
				}
				return;
			}
			// wait until a recreated players bones leave their collapsed bind pose
			if (!HasUsableBoneLength(references.head.parent, references.head) || !HasUsableArmChain(references.leftUpperArm, references.leftForearm, references.leftHand) || !HasUsableArmChain(references.rightUpperArm, references.rightForearm, references.rightHand)) {
				_nextBindAttemptTime = Time.unscaledTime + 0.1f;
				_waitingForSkeleton = true;
				return;
			}
			_waitingForSkeleton = false;

			var existingVrik = person.GetComponent<VRIK>();
			if (existingVrik) {
				// destroy is deferred so wait if the same Person returns during a transition
				if (person == _pendingVrikPerson && existingVrik == _pendingVrikRemoval) return;
				_failedPerson = person;
				Log.LogWarning("[PlayerBodyIK] Person already has VRIK; body tracking skipped.");
				return;
			}

			_person = person;
			_gameIk = gameIk;
			_animator = person.GetComponent<Animator>();
			_handsLayer = _animator ? _animator.GetLayerIndex("Hands") : -1;
			CreateTargets(rig, references);

			_vrik = person.gameObject.AddComponent<VRIK>();
			_vrik.enabled = false;
			MapReferences(_vrik, references, person);
			if (!_vrik.references.isFilled) throw new InvalidOperationException("Manual VRIK references are incomplete.");
			ConfigureSolver(_vrik);
			_vrik.GuessHandOrientations();
			// reinitialize after assigning the manual genericrig references.
			_vrik.InitiateSolver();
			if (!_vrik.solver.initiated) throw new InvalidOperationException("VRIK solver rejected the manual references.");
			CreateForearmTwistSolvers(references);
			_vrik.enabled = true;
			_pendingVrikPerson = null;
			_pendingVrikRemoval = null;
			_nextBindAttemptTime = 0f;

			Log.LogInfo("[PlayerBodyIK] Head and hand tracking bound to Person.");
		} catch (Exception exception) {
			_failedPerson = person;
			Log.LogError($"[PlayerBodyIK] Could not bind player body: {exception}");
			Release();
		}
	}

	private static bool HasCompleteSkeleton(PlayerPersonIK gameIk, FullBodyBipedIK fbbik, BipedReferences references) => gameIk && fbbik && references != null && references.spine != null && references.spine.Length >= 2 && references.root && references.pelvis && references.head && references.leftUpperArm && references.leftForearm && references.leftHand && references.rightUpperArm && references.rightForearm && references.rightHand && references.leftThigh && references.leftCalf && references.leftFoot && references.rightThigh && references.rightCalf && references.rightFoot;

	private bool IsHoldingPlate() {
		if (!_animator || _handsLayer < 0 || _animator.IsInTransition(_handsLayer)) return false;
		foreach (var clip in _animator.GetCurrentAnimatorClipInfo(_handsLayer)) {
			if (clip.clip && clip.clip.name == "Player HoldPlate" && clip.weight > 0.99f) return true;
		}
		return false;
	}

	private static bool HasUsableBoneLength(Transform parent, Transform child) => parent && child && (child.position - parent.position).sqrMagnitude > 0.000001f;

	private static bool HasUsableArmChain(Transform upperArm, Transform forearm, Transform hand) => HasUsableBoneLength(upperArm, forearm) && HasUsableBoneLength(forearm, hand);

	private void CreateTargets(VRPlayer rig, BipedReferences references) {
		// keep the models eye to head offset without scaling tracking space
		_headTarget = CreateTarget("[VR Head Target]", rig.head, rig.head.InverseTransformPoint(references.head.position), Quaternion.Inverse(rig.head.rotation) * references.head.rotation);

		_leftGripResolved = rig.leftController.TryGetGripPose(out var leftGrip);
		_rightGripResolved = rig.rightController.TryGetGripPose(out var rightGrip);
		var leftHandPosition = _leftGripResolved ? leftGrip.Position + LeftGripFineOffset : LeftFallbackHandTargetOffset;
		var rightHandPosition = _rightGripResolved ? rightGrip.Position + RightGripFineOffset : RightFallbackHandTargetOffset;
		var leftHandRotation = GetHandTargetRotation(rig.leftController, true, references.leftHand, out _leftAimResolved);
		var rightHandRotation = GetHandTargetRotation(rig.rightController, false, references.rightHand, out _rightAimResolved);
		_leftHandTarget = CreateTarget("[VR Left Hand Target]", rig.leftControllerObject.transform, leftHandPosition, leftHandRotation);
		_rightHandTarget = CreateTarget("[VR Right Hand Target]", rig.rightControllerObject.transform, rightHandPosition, rightHandRotation);
		if (DebugMode) Log.LogDebug($"[PlayerBodyIK] SteamVR grip positions: left={leftHandPosition}, right={rightHandPosition}.");
	}

	private static Quaternion GetHandTargetRotation(VRController controller, bool leftHand, Transform referenceHand, out bool resolved) {
		resolved = controller.TryGetAimPose(out var aimPose);
		if (!resolved) return Quaternion.Inverse(controller.transform.rotation) * referenceHand.rotation;

		return GetAimHandRotation(aimPose, leftHand);
	}

	private void RefreshControllerAttachments(VRPlayer rig) {
		if (_leftGripResolved && _rightGripResolved && _leftAimResolved && _rightAimResolved) return;
		if (Time.unscaledTime < _nextAttachmentRetryTime) return;

		_nextAttachmentRetryTime = Time.unscaledTime + 0.5f;
		if (!_leftGripResolved && _leftHandTarget && rig.leftController.TryGetGripPose(out var leftGrip)) {
			_leftHandTarget.transform.localPosition = leftGrip.Position + LeftGripFineOffset;
			_leftGripResolved = true;
			if (DebugMode) Log.LogDebug($"[PlayerBodyIK] Resolved left SteamVR grip attachment at {_leftHandTarget.transform.localPosition}.");
		}
		if (!_rightGripResolved && _rightHandTarget && rig.rightController.TryGetGripPose(out var rightGrip)) {
			_rightHandTarget.transform.localPosition = rightGrip.Position + RightGripFineOffset;
			_rightGripResolved = true;
			if (DebugMode) Log.LogDebug($"[PlayerBodyIK] Resolved right SteamVR grip attachment at {_rightHandTarget.transform.localPosition}.");
		}
		if (!_leftAimResolved && _leftHandTarget && rig.leftController.TryGetAimPose(out var leftAim)) {
			_leftHandTarget.transform.localRotation = GetAimHandRotation(leftAim, true);
			_leftAimResolved = true;
			if (DebugMode) Log.LogDebug("[PlayerBodyIK] Resolved left SteamVR hand orientation.");
		}
		if (!_rightAimResolved && _rightHandTarget && rig.rightController.TryGetAimPose(out var rightAim)) {
			_rightHandTarget.transform.localRotation = GetAimHandRotation(rightAim, false);
			_rightAimResolved = true;
			if (DebugMode) Log.LogDebug("[PlayerBodyIK] Resolved right SteamVR hand orientation.");
		}
	}

	private static Quaternion GetAimHandRotation(TrackedPose aimPose, bool leftHand) {
		var palmRoll = Quaternion.AngleAxis(leftHand ? -90f : 90f, Vector3.forward);
		var palmForward = Quaternion.AngleAxis(90f, Vector3.right);
		return aimPose.Rotation * palmRoll * palmForward;
	}

	private void CreateForearmTwistSolvers(BipedReferences references) {
		_leftTwistSolvers = CreateForearmTwistSolvers(references.leftForearm, references.leftHand, ".L");
		_rightTwistSolvers = CreateForearmTwistSolvers(references.rightForearm, references.rightHand, ".R");
		if (DebugMode) Log.LogDebug($"[PlayerBodyIK] Initialized {_leftTwistSolvers.Length + _rightTwistSolvers.Length} forearm twist solvers.");
	}

	private static TwistSolver[] CreateForearmTwistSolvers(Transform forearm, Transform hand, string sideSuffix) {
		var solvers = new List<TwistSolver>(3);
		// MiSides forearm twist bones must be initialized wristfirst
		for (var index = 2; index >= 0; index--) {
			var boneName = index == 0 ? $"TwistForearm{sideSuffix}" : $"TwistForearm{index}{sideSuffix}";
			var twistBone = FindDirectChild(forearm, boneName);
			if (!twistBone) continue;

			var children = new Il2CppReferenceArray<Transform>(1);
			children[0] = hand;
			var solver = new TwistSolver { transform = twistBone, parent = forearm, children = children, weight = 1f, parentChildCrossfade = 0.5f, twistAngleOffset = 0f };
			solver.Initiate();
			solvers.Add(solver);
		}

		return solvers.ToArray();
	}

	private static Transform FindDirectChild(Transform parent, string childName) {
		for (var index = 0; index < parent.childCount; index++) {
			var child = parent.GetChild(index);
			if (child.name == childName) return child;
		}
		return null;
	}

	private static GameObject CreateTarget(string name, Transform parent, Vector3 localPosition, Quaternion localRotation) {
		var target = new GameObject(name);
		target.transform.SetParent(parent, false);
		target.transform.localPosition = localPosition;
		target.transform.localRotation = localRotation;
		return target;
	}

	private static void MapReferences(VRIK vrik, BipedReferences source, Transform playerRoot) {
		var target = vrik.references;
		// VRIK needs the player root because MiSides Armature is rotated -90 degrees on X
		target.root = playerRoot;
		target.pelvis = source.pelvis;
		target.spine = source.spine[0];
		target.chest = source.spine[1];
		target.neck = source.head.parent;
		target.head = source.head;
		target.leftShoulder = source.leftUpperArm.parent;
		target.leftUpperArm = source.leftUpperArm;
		target.leftForearm = source.leftForearm;
		target.leftHand = source.leftHand;
		target.rightShoulder = source.rightUpperArm.parent;
		target.rightUpperArm = source.rightUpperArm;
		target.rightForearm = source.rightForearm;
		target.rightHand = source.rightHand;

		// VRIK validates leg references even though their solver weights stay at zero
		target.leftThigh = source.leftThigh;
		target.leftCalf = source.leftCalf;
		target.leftFoot = source.leftFoot;
		target.leftToes = null;
		target.rightThigh = source.rightThigh;
		target.rightCalf = source.rightCalf;
		target.rightFoot = source.rightFoot;
		target.rightToes = null;
	}

	private void ConfigureSolver(VRIK vrik) {
		// Run VRIK manually after MiSides FBBIK finishes
		vrik.skipSolverUpdate = true;
		vrik.solver.IKPositionWeight = 0f;
		vrik.solver.plantFeet = false;
		vrik.solver.locomotion.weight = 0f;

		vrik.solver.spine.headTarget = _headTarget.transform;
		vrik.solver.spine.positionWeight = 1f;
		vrik.solver.spine.rotationWeight = 1f;
		vrik.solver.spine.pelvisTarget = null;
		vrik.solver.spine.pelvisPositionWeight = 0f;
		vrik.solver.spine.pelvisRotationWeight = 0f;
		vrik.solver.spine.maintainPelvisPosition = 1f;
		vrik.solver.spine.maxRootAngle = 180f;

		vrik.solver.leftArm.target = _leftHandTarget.transform;
		vrik.solver.leftArm.bendGoal = null;
		vrik.solver.leftArm.bendGoalWeight = 0f;
		vrik.solver.leftArm.positionWeight = 0f;
		vrik.solver.leftArm.rotationWeight = 0f;
		vrik.solver.rightArm.target = _rightHandTarget.transform;
		vrik.solver.rightArm.bendGoal = null;
		vrik.solver.rightArm.bendGoalWeight = 0f;
		vrik.solver.rightArm.positionWeight = 0f;
		vrik.solver.rightArm.rotationWeight = 0f;

		vrik.solver.leftLeg.positionWeight = 0f;
		vrik.solver.leftLeg.rotationWeight = 0f;
		vrik.solver.rightLeg.positionWeight = 0f;
		vrik.solver.rightLeg.rotationWeight = 0f;
	}

	public void SolveBeforeRender() {
		if (!_vrik || !_vrik.enabled || _solverWeight <= 0f || _lastSolvedFrame == Time.frameCount) return;

		_lastSolvedFrame = Time.frameCount;
		_vrik.UpdateSolverExternal();
		RelaxForearmTwist(_leftTwistSolvers, _leftArmWeight * ForearmTwistWeight);
		RelaxForearmTwist(_rightTwistSolvers, _rightArmWeight * ForearmTwistWeight);
		SynchronizeHeldItemAnchors();
	}

	private void SynchronizeHeldItemAnchors() {
		if (!_gameIk) return;
		// refresh nonwrist item anchors after the late VR solve
		SyncItemAnchor(_gameIk.handLeftItem, _gameIk.leftItemFixPosition);
		SyncItemAnchor(_gameIk.handRightItem, _gameIk.rightItemFixPosition);
		SyncItemAnchor(_gameIk.handLeftItem2, _gameIk.leftItemFixPosition2);
		SyncItemAnchor(_gameIk.handRightItem2, _gameIk.rightItemFixPosition2);
		SyncItemAnchor(_vrik.references.leftHand, _gameIk.leftWristFixPosition);
		SyncItemAnchor(_vrik.references.rightHand, _gameIk.rightWristFixPosition);
	}

	private static void SyncItemAnchor(Transform handAnchor, Transform fixedAnchor) { if (handAnchor && fixedAnchor) fixedAnchor.SetPositionAndRotation(handAnchor.position, handAnchor.rotation); }

	private static void RelaxForearmTwist(TwistSolver[] solvers, float weight) {
		foreach (var solver in solvers) {
			solver.weight = weight;
			solver.Relax();
		}
	}

	public void Dispose() {
		Release();
		_bindCandidate = null;
		_failedPerson = null;
		_waitingForSkeleton = false;
		_waitingForStableGameplay = false;
		_stableGameplayFrames = 0;
	}

	private void Release() {
		if (_vrik) {
			_vrik.enabled = false;
			_pendingVrikPerson = _person;
			_pendingVrikRemoval = _vrik;
			UnityEngine.Object.Destroy(_vrik);
		}
		if (_headTarget) UnityEngine.Object.Destroy(_headTarget);
		if (_leftHandTarget) UnityEngine.Object.Destroy(_leftHandTarget);
		if (_rightHandTarget) UnityEngine.Object.Destroy(_rightHandTarget);

		_person = null;
		_gameIk = null;
		_animator = null;
		_handsLayer = -1;
		_vrik = null;
		_headTarget = null;
		_leftHandTarget = null;
		_rightHandTarget = null;
		_leftTwistSolvers = Array.Empty<TwistSolver>();
		_rightTwistSolvers = Array.Empty<TwistSolver>();
		_leftGripResolved = false;
		_rightGripResolved = false;
		_leftAimResolved = false;
		_rightAimResolved = false;
		_nextAttachmentRetryTime = 0f;
		_solverWeight = 0f;
		_bodyWeight = 0f;
		_leftArmWeight = 0f;
		_rightArmWeight = 0f;
		_lastSolvedFrame = -1;
	}
}
