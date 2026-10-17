namespace TappyTale.BossFight.Events
{
    public enum BossEventType
    {
        FightStarted,
        FightStopped,
        PhaseStarted,
        PhaseEnded,
        Damaged,
        Died,
        AttackStarted,
        AttackEnded,
        TimelineAction,
        TelegraphStarted,
        TelegraphEnded,
        ProjectileSpawned,
        ProjectileDespawned,
        ParryWindowOpened,
        ParryWindowClosed,
        PerfectDodgeWindowOpened,
        PerfectDodgeWindowClosed,
        PlayerParried,
        PlayerPerfectDodged,
        BossStaggered,
        BossStaggerEnded,
        PatternSpawned,
        CameraShakeRequested,
        FightReset
    }

    public readonly struct BossEvent
    {
        public BossEvent(BossEventType type, string id = null, float value = 0f, int index = -1)
        {
            Type = type;
            Id = id ?? string.Empty;
            Value = value;
            Index = index;
        }

        public BossEventType Type { get; }
        public string Id { get; }
        public float Value { get; }
        public int Index { get; }
    }
}
