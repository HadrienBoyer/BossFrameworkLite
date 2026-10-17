namespace TappyTale.BossFight.Player
{
    public sealed class PlayerInputBuffer
    {
        private float _jumpRemaining;
        private float _dashRemaining;
        private float _lightAttackRemaining;
        private float _heavyAttackRemaining;
        private float _parryRemaining;
        private float _dodgeRemaining;

        public void Tick(float deltaTime)
        {
            if (_jumpRemaining > 0f) _jumpRemaining -= deltaTime;
            if (_dashRemaining > 0f) _dashRemaining -= deltaTime;
            if (_lightAttackRemaining > 0f) _lightAttackRemaining -= deltaTime;
            if (_heavyAttackRemaining > 0f) _heavyAttackRemaining -= deltaTime;
            if (_parryRemaining > 0f) _parryRemaining -= deltaTime;
            if (_dodgeRemaining > 0f) _dodgeRemaining -= deltaTime;
        }

        public void BufferJump(float duration) => _jumpRemaining = duration;
        public void BufferDash(float duration) => _dashRemaining = duration;
        public void BufferLightAttack(float duration) => _lightAttackRemaining = duration;
        public void BufferHeavyAttack(float duration) => _heavyAttackRemaining = duration;
        public void BufferParry(float duration) => _parryRemaining = duration;
        public void BufferDodge(float duration) => _dodgeRemaining = duration;

        public bool ConsumeJump()
        {
            if (_jumpRemaining <= 0f) return false;
            _jumpRemaining = 0f;
            return true;
        }

        public bool ConsumeDash()
        {
            if (_dashRemaining <= 0f) return false;
            _dashRemaining = 0f;
            return true;
        }

        public bool ConsumeLightAttack()
        {
            if (_lightAttackRemaining <= 0f) return false;
            _lightAttackRemaining = 0f;
            return true;
        }

        public bool ConsumeHeavyAttack()
        {
            if (_heavyAttackRemaining <= 0f) return false;
            _heavyAttackRemaining = 0f;
            return true;
        }

        public bool ConsumeParry()
        {
            if (_parryRemaining <= 0f) return false;
            _parryRemaining = 0f;
            return true;
        }

        public bool ConsumeDodge()
        {
            if (_dodgeRemaining <= 0f) return false;
            _dodgeRemaining = 0f;
            return true;
        }

        public bool HasJump => _jumpRemaining > 0f;
        public bool HasDash => _dashRemaining > 0f;
        public bool HasLightAttack => _lightAttackRemaining > 0f;
        public bool HasHeavyAttack => _heavyAttackRemaining > 0f;
        public bool HasParry => _parryRemaining > 0f;
        public bool HasDodge => _dodgeRemaining > 0f;

        public void Clear()
        {
            _jumpRemaining = 0f;
            _dashRemaining = 0f;
            _lightAttackRemaining = 0f;
            _heavyAttackRemaining = 0f;
            _parryRemaining = 0f;
            _dodgeRemaining = 0f;
        }
    }
}
