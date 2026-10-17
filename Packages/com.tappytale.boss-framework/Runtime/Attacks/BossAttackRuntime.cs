using System;
using UnityEngine;

namespace TappyTale.BossFight.Attacks
{
    public sealed class BossAttackRuntime
    {
        public BossAttackRuntime(BossAttackDefinition definition)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
        }

        public BossAttackDefinition Definition { get; }
        public float CooldownRemaining { get; private set; }
        public bool IsReady => CooldownRemaining <= 0f;

        public void Tick(float deltaTime)
        {
            if (CooldownRemaining > 0f)
            {
                CooldownRemaining = Mathf.Max(0f, CooldownRemaining - deltaTime);
            }
        }

        public void CommitCooldown(float multiplier = 1f)
        {
            CooldownRemaining = Mathf.Max(0f, Definition.Cooldown * Mathf.Max(0.05f, multiplier));
        }

        public void SetCooldownRemaining(float remaining)
        {
            CooldownRemaining = Mathf.Max(0f, remaining);
        }

        public void ResetCooldown()
        {
            CooldownRemaining = 0f;
        }
    }
}
