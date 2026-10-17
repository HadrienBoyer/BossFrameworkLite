using UnityEngine;

namespace TappyTale.BossFight.Player
{
    public abstract class PlayerInputSource : MonoBehaviour
    {
        public abstract Vector2 Move { get; }
        public abstract bool JumpPressed { get; }
        public abstract bool DashPressed { get; }

        public virtual bool SprintHeld => false;
        public virtual bool WalkHeld => false;
        public virtual bool LightAttackPressed => false;
        public virtual bool HeavyAttackPressed => false;
        public virtual bool ParryPressed => false;
        public virtual bool DodgePressed => false;
        public virtual bool LockOnPressed => false;
        public virtual float SwitchTargetAxis => 0f;
        public virtual bool RestartPressed => false;
    }
}
