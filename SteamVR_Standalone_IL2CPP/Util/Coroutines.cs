﻿using Il2CppInterop.Runtime.InteropTypes;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Valve.VR
{

    /// <summary>
    /// Code from Melonloader by @HerpDerpinstine https://github.com/LavaGang/MelonLoader/blob/master/MelonLoader.Support.Il2Cpp/MelonCoroutines.cs
    /// </summary>
    public static class MelonCoroutines
    {
        private struct CoroTuple
        {
            public object WaitCondition;
            public IEnumerator Coroutine;
        }
        private static readonly List<CoroTuple> ourCoroutinesStore = new List<CoroTuple>();
        private static readonly List<IEnumerator> ourNextFrameCoroutines = new List<IEnumerator>();
        private static readonly List<IEnumerator> ourWaitForFixedUpdateCoroutines = new List<IEnumerator>();
        private static readonly List<IEnumerator> ourWaitForEndOfFrameCoroutines = new List<IEnumerator>();

        private static readonly List<IEnumerator> tempList = new List<IEnumerator>();

        public static object Start(IEnumerator routine)
        {
            if (routine != null) ProcessNextOfCoroutine(routine);
            return routine;
        }

        public static void Stop(IEnumerator enumerator)
        {
            if (ourNextFrameCoroutines.Contains(enumerator)) // the coroutine is running itself
                ourNextFrameCoroutines.Remove(enumerator);
            else
            {
                int coroTupleIndex = ourCoroutinesStore.FindIndex(c => c.Coroutine == enumerator);
                if (coroTupleIndex != -1) // the coroutine is waiting for a subroutine
                {
                    object waitCondition = ourCoroutinesStore[coroTupleIndex].WaitCondition;
                    if (waitCondition is IEnumerator waitEnumerator)
                        Stop(waitEnumerator);

                    ourCoroutinesStore.RemoveAt(coroTupleIndex);
                }
            }
        }

        public static void ProcessCoroList(List<IEnumerator> target)
        {
            if (target.Count == 0) return;

            // use a temp list to make sure waits made during processing are not handled by same processing invocation
            // additionally, a temp list reduces allocations compared to an array
            tempList.AddRange(target);
            target.Clear();
            foreach (var enumerator in tempList) ProcessNextOfCoroutine(enumerator);
            tempList.Clear();
        }

        public static void Process()
        {
            for (var i = ourCoroutinesStore.Count - 1; i >= 0; i--)
            {
                var tuple = ourCoroutinesStore[i];
                if (tuple.WaitCondition is WaitForSeconds waitForSeconds)
                {
                    if ((waitForSeconds.m_Seconds -= Time.deltaTime) <= 0)
                    {
                        ourCoroutinesStore.RemoveAt(i);
                        ProcessNextOfCoroutine(tuple.Coroutine);
                    }
                }
            }

            ProcessCoroList(ourNextFrameCoroutines);
        }

        public static void ProcessWaitForFixedUpdate() => ProcessCoroList(ourWaitForFixedUpdateCoroutines);

        public static void ProcessWaitForEndOfFrame() => ProcessCoroList(ourWaitForEndOfFrameCoroutines);

        public static void ProcessNextOfCoroutine(IEnumerator enumerator)
        {
            try
            {
                if (!enumerator.MoveNext()) // Run the next step of the coroutine. If it's done, restore the parent routine
                {
                    var indices = ourCoroutinesStore.Select((it, idx) => (idx, it)).Where(it => it.it.WaitCondition == enumerator).Select(it => it.idx).ToList();
                    for (var i = indices.Count - 1; i >= 0; i--)
                    {
                        var index = indices[i];
                        ourNextFrameCoroutines.Add(ourCoroutinesStore[index].Coroutine);
                        ourCoroutinesStore.RemoveAt(index);
                    }
                    return;
                }
            }
            catch (Exception e)
            {

                Stop(FindOriginalCoro(enumerator)); // We want the entire coroutine hierachy to stop when an error happen
            }

            var next = enumerator.Current;
            switch (next)
            {
                case null:
                    ourNextFrameCoroutines.Add(enumerator);
                    return;
                case WaitForFixedUpdate _:
                    ourWaitForFixedUpdateCoroutines.Add(enumerator);
                    return;
                case WaitForEndOfFrame _:
                    ourWaitForEndOfFrameCoroutines.Add(enumerator);
                    return;
                case WaitForSeconds _:
                    break; // do nothing, this one is supported in Process
                case Il2CppObjectBase il2CppObjectBase:
                    var nextAsEnumerator = il2CppObjectBase.TryCast<Il2CppSystem.Collections.IEnumerator>();
                    if (nextAsEnumerator != null) // il2cpp IEnumerator also handles CustomYieldInstruction
                        next = new Il2CppEnumeratorWrapper(nextAsEnumerator);
                    break;
            }

            ourCoroutinesStore.Add(new CoroTuple { WaitCondition = next, Coroutine = enumerator });

            if (next is IEnumerator nextCoro)
                ProcessNextOfCoroutine(nextCoro);
        }

        private static IEnumerator FindOriginalCoro(IEnumerator enumerator)
        {
            int index = ourCoroutinesStore.FindIndex(ct => ct.WaitCondition == enumerator);
            if (index == -1)
                return enumerator;
            return FindOriginalCoro(ourCoroutinesStore[index].Coroutine);
        }

        private class Il2CppEnumeratorWrapper : IEnumerator
        {
            private readonly Il2CppSystem.Collections.IEnumerator il2cppEnumerator;

            public Il2CppEnumeratorWrapper(Il2CppSystem.Collections.IEnumerator il2CppEnumerator) => il2cppEnumerator = il2CppEnumerator;
            public bool MoveNext() => il2cppEnumerator.MoveNext();
            public void Reset()
            {
                il2cppEnumerator.Reset();
            }
            public object Current => il2cppEnumerator.Current;
        }
    }
}
