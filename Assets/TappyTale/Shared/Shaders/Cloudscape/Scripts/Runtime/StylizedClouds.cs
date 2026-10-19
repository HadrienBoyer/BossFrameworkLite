#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif
using UnityEngine;

namespace TappyTale.Systems.Cloudscape
{
    [AddComponentMenu("Tappy Tale/")]
    public class StylizedClouds : MonoBehaviour
    {
        class ShaderProperties
        {
            internal static readonly int WindOrientation = Shader.PropertyToID("_Orientation");
            internal static readonly int WindSpeed = Shader.PropertyToID("_Wind_Speed");
            internal static readonly int Density = Shader.PropertyToID("_Density");
            internal static readonly int Opacity = Shader.PropertyToID("_Opacity");
            internal static readonly int MainColor = Shader.PropertyToID("_Main_Color");
            internal static readonly int SecondaryColor = Shader.PropertyToID("_Secondary_Color");
            internal static readonly int Tiling = Shader.PropertyToID("_Tiling");
            internal static readonly int Thickness = Shader.PropertyToID("_Thickness");
            internal static readonly int Threshold = Shader.PropertyToID("_Threshold");
            internal static readonly int WarpingStrength = Shader.PropertyToID("_Warping_Strength");
            internal static readonly int Fluffiness = Shader.PropertyToID("_Fluffiness");
            internal static readonly int HorizonFade = Shader.PropertyToID("_Horizon_Fade");
            internal static readonly int HorizonStart = Shader.PropertyToID("_Horizon_Start");
            internal static readonly int HorizonEnd = Shader.PropertyToID("_Horizon_End");
        }

        [Header("Transform")]
        [SerializeField]
        float height = 100;

        [SerializeField, Min(0)]
        float size = 1000;

        [Header("Form")]
        [SerializeField, Range(0, 1)]
        float scale = 0.7f;

        [SerializeField, Range(0, 1)]
        float density = 1;

        [SerializeField, Range(0, 1)]
        float threshold = 0.25f;

        [SerializeField, Range(0, 1)]
        float fluffiness = 1;

        [SerializeField, Range(0, 1)]
        float warpingStrength = 0.5f;

        [SerializeField, Range(0, 1)]
        float thickness = 0;

        [Header("Color")]
        [SerializeField]
        Color mainColor = Color.white;

        [SerializeField]
        Color secondaryColor = Color.white;

        [SerializeField, Range(0, 1)]
        float opacity = 1;

        [Header("Horizon")]
        [SerializeField, Range(0, 1)]
        float horizonFadeStrength = 1;

        [SerializeField, Min(0)]
        float horizonStart = 250;

        [SerializeField, Min(0)]
        float horizonEnd = 1000;

        [Header("Wind")]
        [SerializeField, Range(0, 360)]
        float orientation = 0;

        [SerializeField]
        float speed = 1;

        MeshRenderer meshRenderer;

        public float WindOrientation
        {
            get => orientation;
            set
            {
                orientation = value % 360;
                SetFloatProperty(ShaderProperties.WindOrientation, orientation);
            }
        }

        public float WindSpeed
        {
            get => speed;
            set
            {
                speed = value;
                SetFloatProperty(ShaderProperties.WindSpeed, speed);
            }
        }

        public Color MainColor
        {
            get => mainColor;
            set
            {
                mainColor = value;
                SetColorProperty(ShaderProperties.MainColor, mainColor);
            }
        }

        public Color SecondaryColor
        {
            get => secondaryColor;
            set
            {
                secondaryColor = value;
                SetColorProperty(ShaderProperties.SecondaryColor, secondaryColor);
            }
        }

        public float Height
        {
            get => height;
            set
            {
                height = value;
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            meshRenderer = GetComponentInChildren<MeshRenderer>();

            if (meshRenderer != null)
            {
                var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();

                meshRenderer.gameObject.hideFlags = (prefabStage != null) ? HideFlags.None : HideFlags.HideInHierarchy;

                UpdateMeshHeight();

                UpdateMeshSize();

                UpdateMaterialProperties(meshRenderer.sharedMaterial);
            }
        }
#endif
        void UpdateMeshHeight()
        {
            if (meshRenderer != null)
            {
                var position = meshRenderer.transform.position;

                position.y = Height;

                meshRenderer.transform.position = position;
            }
        }

        void UpdateMeshSize()
        {
            if (meshRenderer != null)
            {
                var scale = Vector3.one * size / 1000;

                meshRenderer.transform.localScale = scale;
            }
        }

        void UpdateMaterialProperties(Material material)
        {
            material.SetColor(ShaderProperties.MainColor, MainColor);
            material.SetColor(ShaderProperties.SecondaryColor, SecondaryColor);
            material.SetFloat(ShaderProperties.Tiling, Mathf.Lerp(10, 1, scale));
            material.SetFloat(ShaderProperties.Opacity, opacity);
            material.SetFloat(ShaderProperties.Threshold, threshold);
            material.SetFloat(ShaderProperties.Thickness, thickness);
            material.SetFloat(ShaderProperties.WarpingStrength, warpingStrength);
            material.SetFloat(ShaderProperties.Fluffiness, fluffiness);
            material.SetFloat(ShaderProperties.Density, density);
            material.SetFloat(ShaderProperties.WindOrientation, WindOrientation);
            material.SetFloat(ShaderProperties.WindSpeed, speed);
            material.SetFloat(ShaderProperties.HorizonFade, horizonFadeStrength);
            material.SetFloat(ShaderProperties.HorizonEnd, horizonEnd);
            material.SetFloat(ShaderProperties.HorizonStart, horizonStart);
        }

        void SetColorProperty(int id, Color value)
        {
            if (meshRenderer != null)
            {
                meshRenderer.material.SetColor(id, value);
            }
        }

        void SetFloatProperty(int id, float value)
        {
            if (meshRenderer != null)
            {
                meshRenderer.material.SetFloat(id, value);
            }
        }

        private void Awake()
        {
            meshRenderer = GetComponentInChildren<MeshRenderer>();
        }

        void Start()
        {
            UpdateMaterialProperties(meshRenderer.material);
        }

        private void Reset()
        {
            Debug.Log("Reset");
        }
    }
}
