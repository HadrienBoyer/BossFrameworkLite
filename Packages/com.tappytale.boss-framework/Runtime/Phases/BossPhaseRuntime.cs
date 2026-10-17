using System;

namespace TappyTale.BossFight.Phases
{
    public sealed class BossPhaseRuntime
    {
        public BossPhaseRuntime(BossPhaseDefinition definition, int index)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            Index = index;
        }

        public BossPhaseDefinition Definition { get; }
        public int Index { get; }
        public float ElapsedTime { get; private set; }
        public bool IsActive { get; private set; }

        public void Enter()
        {
            ElapsedTime = 0f;
            IsActive = true;
        }

        public void Tick(float deltaTime)
        {
            if (IsActive)
            {
                ElapsedTime += deltaTime;
            }
        }

        public void Exit()
        {
            IsActive = false;
        }
    }
}
