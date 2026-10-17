using TappyTale.BossFight.Combat;
using TappyTale.BossFight.Feedback;
using UnityEngine;

namespace TappyTale.BossFight.Player
{
    /// <summary>
    /// Handles visual and audio feedback for Player actions (dash ghost trail, hit flash, parry spark, audio cues, camera shake).
    /// Gracefully degrades to procedural URP primitives/tones when no external assets are assigned.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerFeedbackAdapter : MonoBehaviour
    {
        [SerializeField] private PlayerController controller;
        [SerializeField] private AudioSource audioSource;

        private PlayerRuntime _boundRuntime;
        private Renderer _playerRenderer;
        private MaterialPropertyBlock _propBlock;
        private Color _baseColor = new(0.2f, 0.72f, 0.95f, 1f);
        private float _hitFlashRemaining;
        private float _hitstopRemaining;

        private void Awake()
        {
            if (controller == null) controller = GetComponentInParent<PlayerController>();
            if (audioSource == null) audioSource = GetComponent<AudioSource>();
            _playerRenderer = GetComponentInChildren<Renderer>();
            _propBlock = new MaterialPropertyBlock();
            if (controller != null && controller.Definition != null)
            {
                _baseColor = controller.Definition.ThemeColor;
            }
            ApplyBodyColor(_baseColor);
        }

        private void OnEnable()
        {
            TryBind();
        }

        private void Update()
        {
            if (_boundRuntime != controller?.Runtime)
            {
                TryBind();
            }

            if (_hitstopRemaining > 0f)
            {
                _hitstopRemaining -= Time.unscaledDeltaTime;
                if (_hitstopRemaining <= 0f)
                {
                    Time.timeScale = 1f;
                }
            }

            if (_hitFlashRemaining > 0f)
            {
                _hitFlashRemaining = Mathf.Max(0f, _hitFlashRemaining - Time.deltaTime);
            }

            UpdateVisualColor();
        }

        private void OnDisable()
        {
            if (Time.timeScale < 1f && _hitstopRemaining > 0f)
            {
                Time.timeScale = 1f;
            }
            _hitstopRemaining = 0f;
            Unbind();
            ApplyBodyColor(_baseColor);
        }

        public void NotifyParrySuccess()
        {
            _hitFlashRemaining = 0.18f;
            PlayCue(controller?.Definition?.ParryAudioCue, 880f, 0.12f);
            SpawnProceduralEffect(new Color(1f, 0.9f, 0.25f, 0.95f), 1.8f, 0.25f);
            CameraShakeBus.RequestRaw(0.22f, 0.35f, 24f);

            float hitstop = controller?.Definition != null ? controller.Definition.ParryHitstopDuration : 0.04f;
            if (hitstop > 0.005f && Application.isPlaying)
            {
                _hitstopRemaining = hitstop;
                Time.timeScale = 0.15f;
            }
        }

        public void NotifyPerfectDodgeSuccess()
        {
            _hitFlashRemaining = 0.18f;
            PlayCue(controller?.Definition?.ParryAudioCue, 660f, 0.1f);
            SpawnProceduralEffect(new Color(0.25f, 0.95f, 1f, 0.9f), 1.6f, 0.25f);
            SpawnGhostCapsule(new Color(0.2f, 0.9f, 1f, 0.5f), 0.35f);
        }

        private void TryBind()
        {
            Unbind();
            _boundRuntime = controller != null ? controller.Runtime : null;
            if (_boundRuntime == null) return;

            if (controller.Definition != null)
            {
                _baseColor = controller.Definition.ThemeColor;
            }

            _boundRuntime.AttackStarted += OnAttackStarted;
            _boundRuntime.DashStarted += OnDashStarted;
            _boundRuntime.DodgeStarted += OnDodgeStarted;
            _boundRuntime.ParryStarted += OnParryStarted;
            _boundRuntime.Damaged += OnDamaged;
        }

        private void Unbind()
        {
            if (_boundRuntime == null) return;
            _boundRuntime.AttackStarted -= OnAttackStarted;
            _boundRuntime.DashStarted -= OnDashStarted;
            _boundRuntime.DodgeStarted -= OnDodgeStarted;
            _boundRuntime.ParryStarted -= OnParryStarted;
            _boundRuntime.Damaged -= OnDamaged;
            _boundRuntime = null;
        }

        private void OnAttackStarted(bool isHeavy)
        {
            PlayCue(controller?.Definition?.AttackAudioCue, isHeavy ? 320f : 520f, 0.07f);
            SpawnAttackSlashVisual(isHeavy);
        }

        private void OnDashStarted()
        {
            if (controller?.Definition == null || controller.Definition.SpawnGhostTrailOnDash)
            {
                SpawnGhostCapsule(new Color(_baseColor.r, _baseColor.g, _baseColor.b, 0.45f), 0.22f);
            }
        }

        private void OnDodgeStarted()
        {
            SpawnGhostCapsule(new Color(0.4f, 0.85f, 1f, 0.45f), 0.24f);
        }

        private void OnParryStarted()
        {
            PlayCue(controller?.Definition?.ParryAudioCue, 620f, 0.06f);
        }

        private void OnDamaged(DamageInfo damage)
        {
            _hitFlashRemaining = 0.15f;
            PlayCue(controller?.Definition?.HitAudioCue, 180f, 0.12f);
            if (controller?.Definition?.HitCameraShake != null)
            {
                CameraShakeBus.Request(controller.Definition.HitCameraShake, 1f);
            }
            else
            {
                CameraShakeBus.RequestRaw(0.18f, 0.3f, 20f);
            }
        }

        private void UpdateVisualColor()
        {
            if (_playerRenderer == null) return;
            PlayerBlackboard bb = controller != null ? controller.Blackboard : null;
            Color color = _baseColor;

            if (_hitFlashRemaining > 0f)
            {
                color = Color.white;
            }
            else if (bb != null)
            {
                if (bb.IsDead)
                {
                    color = new Color(0.25f, 0.25f, 0.28f, 1f);
                }
                else if (bb.IsParryWindowActive)
                {
                    color = new Color(1f, 0.85f, 0.2f, 1f);
                }
                else if (bb.IsAttacking)
                {
                    color = bb.IsHeavyAttack
                        ? new Color(1f, 0.45f, 0.15f, 1f)
                        : new Color(0.45f, 0.9f, 1f, 1f);
                }
                else if (bb.IsInvulnerable)
                {
                    color = Color.Lerp(_baseColor, Color.white, 0.55f);
                }
            }

            ApplyBodyColor(color);
        }

        private void ApplyBodyColor(Color color)
        {
            if (_playerRenderer == null) return;
            _propBlock ??= new MaterialPropertyBlock();
            _playerRenderer.GetPropertyBlock(_propBlock);
            _propBlock.SetColor("_BaseColor", color);
            _propBlock.SetColor("_Color", color);
            _playerRenderer.SetPropertyBlock(_propBlock);
        }

        private void PlayCue(AudioCueDefinition cue, float fallbackFreq, float fallbackDuration)
        {
            if (!Application.isPlaying) return;
            if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();

            if (cue != null)
            {
                AudioClip clip = cue.GetClipOrProceduralFallback();
                if (clip != null)
                {
                    audioSource.pitch = Random.Range(cue.PitchRange.x, cue.PitchRange.y);
                    audioSource.PlayOneShot(clip, cue.Volume);
                }
                return;
            }

            int rate = 22050;
            int samplesCount = Mathf.Max(256, Mathf.RoundToInt(rate * fallbackDuration));
            float[] data = new float[samplesCount];
            for (int i = 0; i < samplesCount; i++)
            {
                float t = i / (float)rate;
                float env = 1f - (i / (float)samplesCount);
                data[i] = Mathf.Sin(2f * Mathf.PI * fallbackFreq * t) * env * 0.25f;
            }
            AudioClip proc = AudioClip.Create("PlayerProcTone", samplesCount, 1, rate, false);
            proc.hideFlags = HideFlags.HideAndDontSave;
            proc.SetData(data, 0);
            audioSource.PlayOneShot(proc, 0.7f);
            Destroy(proc, fallbackDuration + 0.1f);
        }

        private void SpawnAttackSlashVisual(bool isHeavy)
        {
            if (!Application.isPlaying) return;
            GameObject arc = GameObject.CreatePrimitive(PrimitiveType.Cube);
            arc.name = "PlayerSlashArc";
            Collider col = arc.GetComponent<Collider>();
            if (col != null) Destroy(col);
            arc.transform.SetPositionAndRotation(
                transform.position + transform.forward * 1.25f + Vector3.up * 1.0f,
                transform.rotation);
            arc.transform.localScale = isHeavy ? new Vector3(2.2f, 0.15f, 1.8f) : new Vector3(1.6f, 0.1f, 1.3f);
            Color slashColor = isHeavy ? new Color(1f, 0.55f, 0.15f, 0.85f) : new Color(0.3f, 0.9f, 1f, 0.85f);
            TintPrimitive(arc, slashColor);
            Destroy(arc, isHeavy ? 0.22f : 0.14f);
        }

        private void SpawnProceduralEffect(Color color, float scale, float duration)
        {
            if (!Application.isPlaying) return;
            GameObject fx = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            fx.name = "PlayerBurstFX";
            Collider col = fx.GetComponent<Collider>();
            if (col != null) Destroy(col);
            fx.transform.position = transform.position + Vector3.up * 1.0f;
            fx.transform.localScale = Vector3.one * scale;
            TintPrimitive(fx, color);
            Destroy(fx, duration);
        }

        private void SpawnGhostCapsule(Color color, float duration)
        {
            if (!Application.isPlaying) return;
            GameObject ghost = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            ghost.name = "PlayerDashGhost";
            Collider col = ghost.GetComponent<Collider>();
            if (col != null) Destroy(col);
            ghost.transform.SetPositionAndRotation(transform.position, transform.rotation);
            ghost.transform.localScale = transform.localScale * 0.96f;
            TintPrimitive(ghost, color);
            Destroy(ghost, duration);
        }

        private static void TintPrimitive(GameObject go, Color color)
        {
            Renderer rend = go.GetComponent<Renderer>();
            if (rend == null) return;
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Universal Render Pipeline/Lit");
            if (shader != null)
            {
                Material mat = new(shader);
                mat.SetColor("_BaseColor", color);
                mat.SetColor("_Color", color);
                rend.material = mat;
            }
        }
    }
}
