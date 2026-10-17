using UnityEngine;

namespace TappyTale.BossFight.Feedback
{
    [CreateAssetMenu(menuName = "Tappy Tale/Boss Fight/Audio Cue", fileName = "AudioCue_")]
    public sealed class AudioCueDefinition : ScriptableObject
    {
        [SerializeField] private string id = "audio";
        [SerializeField] private string displayName = "Audio Cue";
        [SerializeField] private AudioClip[] clips;
        [SerializeField, Range(0f, 1f)] private float volume = 0.85f;
        [SerializeField] private Vector2 pitchRange = new(0.95f, 1.05f);
        [SerializeField] private bool spatial = true;
        [SerializeField, Range(100f, 1800f)] private float proceduralFallbackFrequency = 440f;
        [SerializeField, Range(0.02f, 0.5f)] private float proceduralFallbackDuration = 0.08f;

        public string Id => id;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? id : displayName;
        public AudioClip[] Clips => clips;
        public float Volume => volume;
        public Vector2 PitchRange => pitchRange;
        public bool Spatial => spatial;
        public float ProceduralFallbackFrequency => proceduralFallbackFrequency;
        public float ProceduralFallbackDuration => proceduralFallbackDuration;

        private AudioClip _generatedFallbackClip;

        public AudioClip GetClipOrProceduralFallback()
        {
            if (clips != null && clips.Length > 0)
            {
                AudioClip candidate = clips[Random.Range(0, clips.Length)];
                if (candidate != null) return candidate;
            }

            if (_generatedFallbackClip != null)
            {
                return _generatedFallbackClip;
            }

            int sampleRate = 22050;
            int sampleCount = Mathf.Max(256, Mathf.RoundToInt(sampleRate * Mathf.Clamp(proceduralFallbackDuration, 0.02f, 0.5f)));
            float[] samples = new float[sampleCount];
            float freq = Mathf.Clamp(proceduralFallbackFrequency, 80f, 2000f);

            for (int i = 0; i < sampleCount; i++)
            {
                float t = i / (float)sampleRate;
                float env = 1f - (i / (float)sampleCount);
                samples[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * env * 0.35f;
            }

            _generatedFallbackClip = AudioClip.Create($"ProcCue_{id}", sampleCount, 1, sampleRate, false);
            _generatedFallbackClip.hideFlags = HideFlags.HideAndDontSave;
            _generatedFallbackClip.SetData(samples, 0);
            return _generatedFallbackClip;
        }
    }
}
