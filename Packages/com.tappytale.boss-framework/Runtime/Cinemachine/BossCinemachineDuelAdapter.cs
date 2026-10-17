using TappyTale.BossFight.Core;
using TappyTale.BossFight.Feedback;
using TappyTale.BossFight.Player;
#if TAPPYTALE_CINEMACHINE_AVAILABLE
using Unity.Cinemachine;
#endif
using UnityEngine;

namespace TappyTale.BossFight.Cinemachine
{
    /// <summary>
    /// Optional Cinemachine 3 integration adapter for duel framing, lock-on priority switching, and impulse/FOV kicks.
    /// Compiled only when com.unity.cinemachine (3.0+) is installed in the project.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BossCinemachineDuelAdapter : MonoBehaviour
    {
        [SerializeField] private PlayerController player;
        [SerializeField] private BossDirector boss;
#if TAPPYTALE_CINEMACHINE_AVAILABLE
        [SerializeField] private CinemachineCamera freeCamera;
        [SerializeField] private CinemachineCamera lockOnCamera;
        [SerializeField] private CinemachineCamera victoryDefeatCamera;
        [SerializeField] private CinemachineTargetGroup duelTargetGroup;
#endif
        [SerializeField] private int activePriority = 20;
        [SerializeField] private int inactivePriority = 5;

        public PlayerController Player { get => player; set => player = value; }
        public BossDirector Boss { get => boss; set => boss = value; }

        private float _fovKickOffset;
        private float _baseFov = 55f;

#if TAPPYTALE_CINEMACHINE_AVAILABLE
        private void Awake()
        {
            if (freeCamera != null)
            {
                _baseFov = freeCamera.Lens.FieldOfView;
            }
        }
#endif

        private void OnEnable()
        {
            CameraShakeBus.ShakeRequested += OnShakeRequested;
        }

        private void OnDisable()
        {
            CameraShakeBus.ShakeRequested -= OnShakeRequested;
        }

        private void LateUpdate()
        {
            bool fightEnded = (boss != null && !boss.IsAlive) || (player != null && !player.IsAlive);
            bool hardLocked = player != null && player.Blackboard != null && player.Blackboard.IsHardLocked;
            _fovKickOffset = Mathf.MoveTowards(_fovKickOffset, 0f, 18f * Time.deltaTime);

#if TAPPYTALE_CINEMACHINE_AVAILABLE
            if (victoryDefeatCamera != null)
            {
                victoryDefeatCamera.Priority = fightEnded ? activePriority + 5 : inactivePriority;
            }

            if (lockOnCamera != null)
            {
                lockOnCamera.Priority = (!fightEnded && hardLocked) ? activePriority : inactivePriority;
            }

            if (freeCamera != null)
            {
                freeCamera.Priority = (!fightEnded && !hardLocked) ? activePriority : inactivePriority;
                LensSettings lens = freeCamera.Lens;
                lens.FieldOfView = Mathf.Clamp(_baseFov + _fovKickOffset, 35f, 90f);
                freeCamera.Lens = lens;
            }
#else
            _ = fightEnded;
            _ = hardLocked;
            _ = activePriority;
            _ = inactivePriority;
            _ = _baseFov;
#endif
        }

        private void OnShakeRequested(CameraShakeDefinition definition, float scale)
        {
            if (definition == null) return;
            _fovKickOffset = definition.FovKick * scale;
        }
    }
}
