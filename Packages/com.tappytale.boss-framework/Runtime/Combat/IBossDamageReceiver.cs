using UnityEngine;

namespace TappyTale.BossFight.Combat
{
    /// <summary>
    /// Legacy damage receiver interface retained for backward compatibility.
    /// Prefer implementing <see cref="IDamageReceiver"/> for new bidirectional combat receivers.
    /// </summary>
    public interface IBossDamageReceiver : IDamageReceiver
    {
        new bool ReceiveDamage(float amount, GameObject source);

        DamageTeam IDamageReceiver.Team => DamageTeam.Neutral;
        bool IDamageReceiver.IsAlive => true;
        bool IDamageReceiver.ReceiveDamage(DamageInfo damage) => ReceiveDamage(damage.Amount, damage.Source);
        bool IDamageReceiver.ReceiveDamage(float amount, GameObject source) => ReceiveDamage(amount, source);
    }
}