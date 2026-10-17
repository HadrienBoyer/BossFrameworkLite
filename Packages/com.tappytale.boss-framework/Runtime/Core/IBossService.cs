namespace TappyTale.BossFight.Core
{
    public interface IBossService
    {
        void Initialize(BossContext context);
        void Shutdown();
    }
}
