namespace TappyTale.BossFight.Core
{
    public interface IBossTickable
    {
        int TickOrder { get; }
        void Tick(float deltaTime);
    }
}
