using TappyTale.BossFight.Combat;
using UnityEngine;

namespace TappyTale.BossFight.Demo
{
    [DisallowMultipleComponent]
    public sealed class BossDemoHealthReceiver : MonoBehaviour, IBossDamageReceiver, IDamageReceiver
    {
        [SerializeField, Min(1f)] private float maxHealth = 100f;
        public float MaxHealth => maxHealth;
        public float CurrentHealth { get; private set; }
        public float NormalizedHealth => maxHealth <= 0f ? 0f : Mathf.Clamp01(CurrentHealth / maxHealth);
        public DamageTeam Team => DamageTeam.Player;
        public bool IsAlive => CurrentHealth > 0f;

        private void Awake() => CurrentHealth = maxHealth;

        public bool ReceiveDamage(DamageInfo damage)
        {
            if (damage.SourceTeam == DamageTeam.Player) return false;
            return ReceiveDamage(damage.Amount, damage.Source);
        }

        public bool ReceiveDamage(float amount, GameObject source)
        {
            if (amount <= 0f || CurrentHealth <= 0f) return false;
            CurrentHealth = Mathf.Max(0f, CurrentHealth - amount);
            Debug.Log($"Demo player received {amount:0.##} damage from {(source != null ? source.name : "unknown")}. Health: {CurrentHealth:0.##}/{maxHealth:0.##}", this);
            return true;
        }

        public void Restore() => CurrentHealth = maxHealth;
    }
}