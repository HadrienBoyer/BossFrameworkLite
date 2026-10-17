using TappyTale.BossFight.Attacks;
using TappyTale.BossFight.Core;
using TappyTale.BossFight.Data;
using TappyTale.BossFight.Events;
using UnityEngine;

namespace TappyTale.BossFight.Feedback
{
    [DisallowMultipleComponent]
    public sealed class BossTimelineFeedbackAdapter : MonoBehaviour
    {
        [SerializeField] private BossDirector director;
        [SerializeField] private AudioSource audioSource;

        private BossRuntime _boundRuntime;
        private BossTimelineActionExecutor _executor;
        private BossEventHub _events;
        private Renderer _bossRenderer;
        private MaterialPropertyBlock _propBlock;
        private Color _baseColor = new(0.85f, 0.22f, 0.18f, 1f);
        private float _flashRemaining;

        private void Awake()
        {
            if (director == null) director = GetComponentInParent<BossDirector>();
            if (audioSource == null) audioSource = GetComponent<AudioSource>();
            _bossRenderer = GetComponentInChildren<Renderer>();
            _propBlock = new MaterialPropertyBlock();
            if (director != null && director.Definition != null)
            {
                _baseColor = director.Definition.ThemeColor;
            }
            ApplyBodyColor(_baseColor);
        }

        private void OnEnable()
        {
            if (director != null)
            {
                director.RuntimeRebuilt += Bind;
            }
            Bind();
        }

        private void Update()
        {
            if (_boundRuntime != director?.Runtime)
            {
                Bind();
            }

            if (_flashRemaining > 0f)
            {
                _flashRemaining = Mathf.Max(0f, _flashRemaining - Time.deltaTime);
            }

            UpdateVisualState();
        }

        private void OnDisable()
        {
            if (director != null)
            {
                director.RuntimeRebuilt -= Bind;
            }
            Unbind();
            ApplyBodyColor(_baseColor);
        }

        private void Bind()
        {
            Unbind();
            _boundRuntime = director != null ? director.Runtime : null;
            if (_boundRuntime == null) return;

            if (director.Definition != null)
            {
                _baseColor = director.Definition.ThemeColor;
            }

            _executor = _boundRuntime.ActionExecutor;
            _events = _boundRuntime.Events;

            if (_executor != null)
            {
                _executor.AudioRequested += PlayAudio;
                _executor.VfxRequested += PlayVfx;
                _executor.CameraShakeRequested += PlayCameraShake;
            }

            if (_events != null)
            {
                _events.Raised += OnBossEvent;
            }
        }

        private void Unbind()
        {
            if (_executor != null)
            {
                _executor.AudioRequested -= PlayAudio;
                _executor.VfxRequested -= PlayVfx;
                _executor.CameraShakeRequested -= PlayCameraShake;
                _executor = null;
            }

            if (_events != null)
            {
                _events.Raised -= OnBossEvent;
                _events = null;
            }

            _boundRuntime = null;
        }

        private void OnBossEvent(BossEvent evt)
        {
            switch (evt.Type)
            {
                case BossEventType.Damaged:
                    _flashRemaining = 0.12f;
                    CameraShakeBus.RequestRaw(0.1f, 0.14f, 22f);
                    break;
                case BossEventType.PlayerParried:
                case BossEventType.BossStaggered:
                    _flashRemaining = 0.25f;
                    CameraShakeBus.RequestRaw(0.24f, 0.42f, 26f);
                    SpawnProceduralPulse(new Color(1f, 0.85f, 0.2f, 0.9f), 2.4f, 0.35f);
                    break;
                case BossEventType.PhaseStarted:
                    if (evt.Index > 0)
                    {
                        SpawnProceduralPulse(new Color(1f, 0.3f, 0.1f, 0.9f), 3.2f, 0.5f);
                        CameraShakeBus.RequestRaw(0.35f, 0.5f, 20f);
                    }
                    break;
                case BossEventType.CameraShakeRequested:
                    TriggerShakeById(evt.Id, evt.Value > 0f ? evt.Value : 1f);
                    break;
            }
        }

        private void UpdateVisualState()
        {
            if (_bossRenderer == null) return;
            BossBlackboard bb = director != null ? director.Blackboard : null;
            Color targetColor = _baseColor;

            if (_flashRemaining > 0f)
            {
                targetColor = Color.white;
            }
            else if (bb != null)
            {
                if (bb.CurrentHealth <= 0f)
                {
                    targetColor = new Color(0.25f, 0.25f, 0.28f, 1f);
                }
                else if (bb.IsStaggered)
                {
                    float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 16f);
                    targetColor = Color.Lerp(new Color(0.95f, 0.65f, 0.15f, 1f), new Color(1f, 0.9f, 0.35f, 1f), pulse);
                }
                else if (bb.IsParryWindowOpen)
                {
                    targetColor = new Color(1f, 0.82f, 0.15f, 1f);
                }
                else if (bb.IsPerfectDodgeWindowOpen)
                {
                    targetColor = new Color(0.2f, 0.85f, 1f, 1f);
                }
                else if (bb.CurrentPhaseIndex > 0)
                {
                    targetColor = Color.Lerp(_baseColor, new Color(0.95f, 0.1f, 0.45f, 1f), 0.45f);
                }
            }

            ApplyBodyColor(targetColor);
        }

        private void ApplyBodyColor(Color color)
        {
            if (_bossRenderer == null) return;
            _propBlock ??= new MaterialPropertyBlock();
            _bossRenderer.GetPropertyBlock(_propBlock);
            _propBlock.SetColor("_BaseColor", color);
            _propBlock.SetColor("_Color", color);
            _bossRenderer.SetPropertyBlock(_propBlock);
        }

        private void PlayAudio(AttackTimelineEntry entry)
        {
            BossPayloadResolver resolver = director?.Runtime?.Payloads;
            if (resolver == null || !resolver.TryGetAudio(entry.PayloadId, out AudioCueDefinition cue) || cue == null)
            {
                return;
            }

            AudioClip clip = cue.GetClipOrProceduralFallback();
            if (clip == null) return;

            if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.pitch = Random.Range(cue.PitchRange.x, cue.PitchRange.y);
            audioSource.spatialBlend = cue.Spatial ? 1f : 0f;
            audioSource.PlayOneShot(clip, cue.Volume);
        }

        private void PlayVfx(AttackTimelineEntry entry)
        {
            BossPayloadResolver resolver = director?.Runtime?.Payloads;
            if (resolver == null || !resolver.TryGetVfx(entry.PayloadId, out VfxDefinition definition) || definition == null)
            {
                return;
            }

            if (definition.Prefab != null)
            {
                Transform parent = definition.FollowSource ? transform : null;
                GameObject instance = Instantiate(definition.Prefab, parent);
                instance.transform.SetPositionAndRotation(
                    transform.TransformPoint(definition.LocalOffset),
                    transform.rotation * definition.LocalRotation);
                if (definition.Lifetime > 0f) Destroy(instance, definition.Lifetime);
            }
            else
            {
                SpawnProceduralPulse(definition.FallbackColor, definition.FallbackScale, definition.Lifetime);
            }
        }

        private void PlayCameraShake(AttackTimelineEntry entry)
        {
            TriggerShakeById(entry.PayloadId, entry.Value > 0f ? entry.Value : 1f);
        }

        private void TriggerShakeById(string shakeId, float scale)
        {
            BossPayloadResolver resolver = director?.Runtime?.Payloads;
            if (resolver != null && resolver.TryGetCameraShake(shakeId, out CameraShakeDefinition shakeDef) && shakeDef != null)
            {
                CameraShakeBus.Request(shakeDef, scale);
            }
            else
            {
                CameraShakeBus.RequestRaw(0.18f, 0.25f * scale, 18f);
            }
        }

        private void SpawnProceduralPulse(Color color, float scale, float lifetime)
        {
            if (!Application.isPlaying) return;
            GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = "BossVfxPulse";
            Collider col = sphere.GetComponent<Collider>();
            if (col != null) Destroy(col);
            sphere.transform.position = transform.position + Vector3.up * 1f;
            sphere.transform.localScale = Vector3.one * scale;
            Renderer rend = sphere.GetComponent<Renderer>();
            if (rend != null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Universal Render Pipeline/Lit");
                if (shader != null)
                {
                    Material mat = new(shader);
                    mat.SetColor("_BaseColor", color);
                    mat.SetColor("_Color", color);
                    rend.material = mat;
                }
            }
            Destroy(sphere, Mathf.Clamp(lifetime, 0.08f, 1.5f));
        }
    }
}