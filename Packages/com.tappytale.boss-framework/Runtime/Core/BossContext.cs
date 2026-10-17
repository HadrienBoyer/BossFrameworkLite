using System;
using UnityEngine;

namespace TappyTale.BossFight.Core
{
    public sealed class BossContext
    {
        public BossContext(GameObject owner, Transform bossTransform, Transform targetTransform, BossBlackboard blackboard, BossServiceRegistry services)
        {
            Owner = owner != null ? owner : throw new ArgumentNullException(nameof(owner));
            BossTransform = bossTransform != null ? bossTransform : throw new ArgumentNullException(nameof(bossTransform));
            TargetTransform = targetTransform;
            Blackboard = blackboard ?? throw new ArgumentNullException(nameof(blackboard));
            Services = services ?? throw new ArgumentNullException(nameof(services));
        }

        public GameObject Owner { get; }
        public Transform BossTransform { get; }
        public Transform TargetTransform { get; set; }
        public BossBlackboard Blackboard { get; }
        public BossServiceRegistry Services { get; }
        public bool HasTarget => TargetTransform != null;
    }
}
