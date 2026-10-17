using UnityEngine;

namespace TappyTale.BossFight.Core
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-1000)]
    public sealed class BossBootstrap : MonoBehaviour
    {
        [SerializeField] private BossDirector director;
        [SerializeField] private bool autoResolveDirector = true;

        public BossDirector Director => director;

        private void Awake()
        {
            if (director == null && autoResolveDirector)
            {
                director = GetComponent<BossDirector>();
            }

            if (director == null)
            {
                Debug.LogError($"{nameof(BossBootstrap)} requires a {nameof(BossDirector)} on the same GameObject or an explicit reference.", this);
                enabled = false;
            }
        }

        public void StartFight()
        {
            director?.StartFight();
        }

        public void StopFight()
        {
            director?.StopFight();
        }
    }
}
