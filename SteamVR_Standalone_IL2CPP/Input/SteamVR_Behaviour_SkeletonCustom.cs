//======= Copyright (c) Valve Corporation, All rights reserved. ===============

using System;
using UnityEngine;
using Valve.VR;

namespace Valve.VR
{
    /// <summary>
    /// The major difference between this component and the standard SteamVR_Behaviour_Skeleton is this one lets you
    /// only use the joints you care about. You can set the transforms you're concerned with and ignore the ones you're not.
    /// </summary>
    public class SteamVR_Behaviour_SkeletonCustom : SteamVR_Behaviour_Skeleton
    {
        public SteamVR_Behaviour_SkeletonCustom(IntPtr value) : base(value) { }

        protected Transform _wrist;

        
        protected Transform _thumbMetacarpal;

        
        protected Transform _thumbProximal;

        
        protected Transform _thumbMiddle;

        
        protected Transform _thumbDistal;

        
        protected Transform _thumbTip;

        
        protected Transform _thumbAux;

        
        protected Transform _indexMetacarpal;

        
        protected Transform _indexProximal;

        
        protected Transform _indexMiddle;

        
        protected Transform _indexDistal;

        
        protected Transform _indexTip;

        
        protected Transform _indexAux;

        
        protected Transform _middleMetacarpal;

        
        protected Transform _middleProximal;

        
        protected Transform _middleMiddle;

        
        protected Transform _middleDistal;

        
        protected Transform _middleTip;

        
        protected Transform _middleAux;

        
        protected Transform _ringMetacarpal;

        
        protected Transform _ringProximal;

        
        protected Transform _ringMiddle;

        
        protected Transform _ringDistal;

        
        protected Transform _ringTip;

        
        protected Transform _ringAux;

        
        protected Transform _pinkyMetacarpal;

        
        protected Transform _pinkyProximal;

        
        protected Transform _pinkyMiddle;

        
        protected Transform _pinkyDistal;

        
        protected Transform _pinkyTip;

        
        protected Transform _pinkyAux;


        protected override void AssignBonesArray()
        {
            bones[SteamVR_Skeleton_JointIndexes.wrist] = _wrist;
            bones[SteamVR_Skeleton_JointIndexes.thumbProximal] = _thumbProximal;
            bones[SteamVR_Skeleton_JointIndexes.thumbMiddle] = _thumbMiddle;
            bones[SteamVR_Skeleton_JointIndexes.thumbDistal] = _thumbDistal;
            bones[SteamVR_Skeleton_JointIndexes.thumbTip] = _thumbTip;
            bones[SteamVR_Skeleton_JointIndexes.thumbAux] = _thumbAux;
            bones[SteamVR_Skeleton_JointIndexes.indexProximal] = _indexProximal;
            bones[SteamVR_Skeleton_JointIndexes.indexMiddle] = _indexMiddle;
            bones[SteamVR_Skeleton_JointIndexes.indexDistal] = _indexDistal;
            bones[SteamVR_Skeleton_JointIndexes.indexTip] = _indexTip;
            bones[SteamVR_Skeleton_JointIndexes.indexAux] = _indexAux;
            bones[SteamVR_Skeleton_JointIndexes.middleProximal] = _middleProximal;
            bones[SteamVR_Skeleton_JointIndexes.middleMiddle] = _middleMiddle;
            bones[SteamVR_Skeleton_JointIndexes.middleDistal] = _middleDistal;
            bones[SteamVR_Skeleton_JointIndexes.middleTip] = _middleTip;
            bones[SteamVR_Skeleton_JointIndexes.middleAux] = _middleAux;
            bones[SteamVR_Skeleton_JointIndexes.ringProximal] = _ringProximal;
            bones[SteamVR_Skeleton_JointIndexes.ringMiddle] = _ringMiddle;
            bones[SteamVR_Skeleton_JointIndexes.ringDistal] = _ringDistal;
            bones[SteamVR_Skeleton_JointIndexes.ringTip] = _ringTip;
            bones[SteamVR_Skeleton_JointIndexes.ringAux] = _ringAux;
            bones[SteamVR_Skeleton_JointIndexes.pinkyProximal] = _pinkyProximal;
            bones[SteamVR_Skeleton_JointIndexes.pinkyMiddle] = _pinkyMiddle;
            bones[SteamVR_Skeleton_JointIndexes.pinkyDistal] = _pinkyDistal;
            bones[SteamVR_Skeleton_JointIndexes.pinkyTip] = _pinkyTip;
            bones[SteamVR_Skeleton_JointIndexes.pinkyAux] = _pinkyAux;
        }
    }
}