using TappyTale.BossFight.Core;
using UnityEngine;

namespace TappyTale.BossFight.Telegraphs
{
    /// <summary>
    /// Renders a readable URP-compatible ground telegraph indicator during warning and reaction windows without external VFX dependencies.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BossTelegraphVisualAdapter : MonoBehaviour
    {
        [SerializeField] private BossDirector director;
        [SerializeField] private float groundHeightOffset = 0.04f;

        private BossRuntime _boundRuntime;
        private BossTelegraphService _telegraphs;
        private GameObject _indicatorRoot;
        private Renderer _outerRenderer;
        private Renderer _innerFillRenderer;
        private Material _runtimeMaterial;
        private MaterialPropertyBlock _propBlock;

        private void Awake()
        {
            if (director == null)
            {
                director = GetComponentInParent<BossDirector>();
            }
            _propBlock = new MaterialPropertyBlock();
            EnsureVisualObjects();
            SetIndicatorVisible(false);
        }

        private void OnEnable()
        {
            if (director != null)
            {
                director.RuntimeRebuilt += TryBind;
            }
            TryBind();
        }

        private void Update()
        {
            if (_boundRuntime != director?.Runtime)
            {
                TryBind();
            }

            if (_telegraphs == null || !_telegraphs.IsActive || director == null || director.Runtime == null || !director.Runtime.IsRunning)
            {
                SetIndicatorVisible(false);
                return;
            }

            EnsureVisualObjects();
            SetIndicatorVisible(true);

            TelegraphDefinition def = _telegraphs.ActiveDefinition;
            float progress = _telegraphs.NormalizedProgress;
            float curveEval = def != null && def.Intensity != null ? Mathf.Clamp01(def.Intensity.Evaluate(progress)) : progress;

            float radius = def != null ? Mathf.Max(0.5f, def.Radius) : 2.5f;
            float length = def != null ? Mathf.Max(0.5f, def.Length) : 3.5f;
            TelegraphShape shape = def != null ? def.Shape : TelegraphShape.Circle;
            TelegraphAnchor anchor = def != null ? def.Anchor : TelegraphAnchor.BossForward;
            float forwardOffset = def != null ? def.ForwardOffset : 1.5f;

            Vector3 forward = transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;
            forward.Normalize();

            Vector3 anchorPos = transform.position;
            if (anchor == TelegraphAnchor.BossForward)
            {
                anchorPos += forward * forwardOffset;
            }
            else if (anchor == TelegraphAnchor.Target && director.Runtime.Context.HasTarget)
            {
                anchorPos = director.Runtime.Context.TargetTransform.position;
            }

            anchorPos.y = groundHeightOffset;
            _indicatorRoot.transform.SetPositionAndRotation(anchorPos, Quaternion.LookRotation(forward, Vector3.up));

            if (shape == TelegraphShape.Line)
            {
                _outerRenderer.transform.localScale = new Vector3(radius, 0.02f, length);
                _innerFillRenderer.transform.localScale = new Vector3(radius * Mathf.Lerp(0.25f, 1f, curveEval), 0.03f, length);
            }
            else
            {
                float diameter = radius * 2f;
                _outerRenderer.transform.localScale = new Vector3(diameter, 0.015f, diameter);
                float fillDiameter = diameter * Mathf.Lerp(0.15f, 1f, curveEval);
                _innerFillRenderer.transform.localScale = new Vector3(fillDiameter, 0.025f, fillDiameter);
            }

            Color warnColor = def != null ? def.Color : new Color(1f, 0.5f, 0.1f, 0.55f);
            Color dangerColor = def != null ? def.DangerColor : new Color(1f, 0.15f, 0.15f, 0.85f);

            bool parryOpen = director.Blackboard != null && director.Blackboard.IsParryWindowOpen;
            bool dodgeOpen = director.Blackboard != null && director.Blackboard.IsPerfectDodgeWindowOpen;

            Color activeColor = Color.Lerp(warnColor, dangerColor, curveEval);
            if (parryOpen)
            {
                activeColor = new Color(1f, 0.85f, 0.15f, 0.9f);
            }
            else if (dodgeOpen)
            {
                activeColor = new Color(0.2f, 0.85f, 1f, 0.9f);
            }

            ApplyColor(_outerRenderer, new Color(activeColor.r, activeColor.g, activeColor.b, 0.35f));
            ApplyColor(_innerFillRenderer, activeColor);
        }

        private void OnDisable()
        {
            if (director != null)
            {
                director.RuntimeRebuilt -= TryBind;
            }
            Unbind();
            SetIndicatorVisible(false);
        }

        private void OnDestroy()
        {
            if (_indicatorRoot != null)
            {
                if (Application.isPlaying) Destroy(_indicatorRoot);
                else DestroyImmediate(_indicatorRoot);
            }

            if (_runtimeMaterial != null)
            {
                if (Application.isPlaying) Destroy(_runtimeMaterial);
                else DestroyImmediate(_runtimeMaterial);
            }
        }

        private void TryBind()
        {
            Unbind();
            _boundRuntime = director != null ? director.Runtime : null;
            _telegraphs = _boundRuntime?.Telegraphs;
            if (_telegraphs != null)
            {
                _telegraphs.Ended += OnTelegraphEnded;
            }
            if (_boundRuntime != null)
            {
                _boundRuntime.Stopped += OnFightStopped;
            }
        }

        private void Unbind()
        {
            if (_telegraphs != null)
            {
                _telegraphs.Ended -= OnTelegraphEnded;
                _telegraphs = null;
            }

            if (_boundRuntime != null)
            {
                _boundRuntime.Stopped -= OnFightStopped;
                _boundRuntime = null;
            }
        }

        private void OnTelegraphEnded(string _) => SetIndicatorVisible(false);
        private void OnFightStopped() => SetIndicatorVisible(false);

        private void SetIndicatorVisible(bool visible)
        {
            if (_indicatorRoot != null && _indicatorRoot.activeSelf != visible)
            {
                _indicatorRoot.SetActive(visible);
            }
        }

        private void EnsureVisualObjects()
        {
            if (_indicatorRoot != null)
            {
                return;
            }

            _indicatorRoot = new GameObject("BossTelegraphVisual");
            _indicatorRoot.transform.SetParent(transform, false);

            Shader shader = Shader.Find("Universal Render Pipeline/Unlit")
                            ?? Shader.Find("Universal Render Pipeline/Lit")
                            ?? Shader.Find("Sprites/Default");
            if (shader != null)
            {
                _runtimeMaterial = new Material(shader)
                {
                    hideFlags = HideFlags.HideAndDontSave
                };
            }

            GameObject outer = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            outer.name = "TelegraphOuter";
            outer.transform.SetParent(_indicatorRoot.transform, false);
            Collider outerCol = outer.GetComponent<Collider>();
            if (outerCol != null)
            {
                if (Application.isPlaying) Destroy(outerCol);
                else DestroyImmediate(outerCol);
            }
            _outerRenderer = outer.GetComponent<Renderer>();
            if (_runtimeMaterial != null && _outerRenderer != null) _outerRenderer.sharedMaterial = _runtimeMaterial;

            GameObject inner = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            inner.name = "TelegraphInnerFill";
            inner.transform.SetParent(_indicatorRoot.transform, false);
            Collider innerCol = inner.GetComponent<Collider>();
            if (innerCol != null)
            {
                if (Application.isPlaying) Destroy(innerCol);
                else DestroyImmediate(innerCol);
            }
            _innerFillRenderer = inner.GetComponent<Renderer>();
            if (_runtimeMaterial != null && _innerFillRenderer != null) _innerFillRenderer.sharedMaterial = _runtimeMaterial;
        }

        private void ApplyColor(Renderer rend, Color color)
        {
            if (rend == null) return;
            _propBlock ??= new MaterialPropertyBlock();
            rend.GetPropertyBlock(_propBlock);
            _propBlock.SetColor("_BaseColor", color);
            _propBlock.SetColor("_Color", color);
            rend.SetPropertyBlock(_propBlock);
        }
    }
}
