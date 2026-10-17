using System.Collections.Generic;
using TappyTale.BossFight.Core;
using UnityEngine;

namespace TappyTale.BossFight.Player
{
    public enum LockOnPriority
    {
        Minion = 0,
        Elite = 10,
        Boss = 100
    }

    public interface ILockOnTarget
    {
        Transform AimTransform { get; }
        LockOnPriority Priority { get; }
        bool IsTargetable { get; }
    }

    /// <summary>
    /// Explicit static registry of active lock-on targets in the scene.
    /// Avoids FindObjectsOfType / scene scans in gameplay update loops.
    /// </summary>
    public static class LockOnTargetRegistry
    {
        private static readonly List<ILockOnTarget> Targets = new();

        public static IReadOnlyList<ILockOnTarget> ActiveTargets => Targets;

        public static void Register(ILockOnTarget target)
        {
            if (target != null && !Targets.Contains(target))
            {
                Targets.Add(target);
            }
        }

        public static void Unregister(ILockOnTarget target)
        {
            if (target != null)
            {
                Targets.Remove(target);
            }
        }

        public static void Clear() => Targets.Clear();
    }

    /// <summary>
    /// Registers a Boss or Enemy in LockOnTargetRegistry on OnEnable and unregisters on OnDisable.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LockOnTarget : MonoBehaviour, ILockOnTarget
    {
        [SerializeField] private LockOnPriority priority = LockOnPriority.Boss;
        [SerializeField] private Vector3 aimOffset = new(0f, 1.0f, 0f);
        [SerializeField] private BossDirector bossDirector;

        public Transform AimTransform => transform;
        public Vector3 WorldAimPoint => transform.position + aimOffset;
        public LockOnPriority Priority => priority;
        public BossDirector BossDirector => bossDirector;

        public bool IsTargetable
        {
            get
            {
                if (!isActiveAndEnabled || !gameObject.activeInHierarchy)
                {
                    return false;
                }

                if (bossDirector != null && bossDirector.Runtime != null && bossDirector.Runtime.Health.IsDead)
                {
                    return false;
                }

                return true;
            }
        }

        private void Awake()
        {
            if (bossDirector == null)
            {
                bossDirector = GetComponentInParent<BossDirector>();
            }
        }

        private void OnEnable() => LockOnTargetRegistry.Register(this);
        private void OnDisable() => LockOnTargetRegistry.Unregister(this);
    }
}
