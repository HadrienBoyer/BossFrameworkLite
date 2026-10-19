using UnityEngine;

namespace TappyTale.NovaParis.VisualEffects
{
    [CreateAssetMenu(
        fileName = "NPCRTGlitchPreset",
        menuName = "TappyTale/Nova Paris/Visual Effects/CRT Glitch Preset")]
    public sealed class NPCRTGlitchPreset : ScriptableObject
    {
        [SerializeField] private NPCRTGlitchSettings settings = new();

        public NPCRTGlitchSettings Settings => settings;
    }
}
