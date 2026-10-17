using System;
using TappyTale.BossFight.Core;

namespace TappyTale.BossFight.Events
{
    public sealed class BossEventHub : IBossService
    {
        public event Action<BossEvent> Raised;

        public void Initialize(BossContext context)
        {
        }

        public void Raise(BossEvent bossEvent)
        {
            Raised?.Invoke(bossEvent);
        }

        public void Shutdown()
        {
            Raised = null;
        }
    }
}
