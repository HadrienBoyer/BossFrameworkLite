# NOVA PARIS — Dynamic CRT Glitch Noise Shader

Drop-in Unity shader + runtime controller for UI Images, RawImages, SpriteRenderers, planes, quads, and world-space screens.

## Files

```text
Assets/TappyTale/NovaParis/VisualEffects/CRTGlitch/
├── Shaders/
│   └── NP_CRT_Glitch_Noise.shader
├── Runtime/
│   ├── NPCRTGlitchController.cs
│   ├── NPCRTGlitchPreset.cs
│   ├── NPCRTGlitchSettings.cs
│   └── TappyTale.NovaParis.VisualEffects.asmdef
└── Docs/
    └── README.md
```

## UI usage

1. Add an `Image` or `RawImage` to a Canvas.
2. Add `NPCRTGlitchController` on the same GameObject.
3. Leave `Auto Find Target` enabled.
4. Tune the settings directly on the component.

Recommended UI values:

```text
Curvature: 0.10 - 0.22
Scanline Intensity: 0.25 - 0.45
Glitch Intensity: 0.10 - 0.30
Chromatic Aberration: 0.004 - 0.012
Noise Intensity: 0.08 - 0.18
UI Uses Always ZTest: true
```

## World usage

1. Add a Quad, Plane, SpriteRenderer, or any MeshRenderer in the world.
2. Assign a texture to the existing material, or set `Texture Override` in `NPCRTGlitchController`.
3. Add `NPCRTGlitchController` to the object.
4. Leave `World ZTest` as `LessEqual` for proper depth behavior.

Recommended world-screen values:

```text
Curvature: 0.06 - 0.18
Scanline Intensity: 0.20 - 0.38
Glitch Intensity: 0.06 - 0.22
Noise Intensity: 0.06 - 0.15
World ZTest: LessEqual
World Cull Mode: Off
```

## Runtime burst

Call this from damage, hacking, dialogue, mission triggers, or signal events:

```csharp
crtGlitchController.TriggerBurst(1f);
```

## Notes

- The shader is transparent and does not modify global render state.
- It supports UGUI masking and sprite atlases.
- It is intentionally not a URP Fullscreen Render Feature; use it on UI overlays, CRT screens, quads, world-space panels, or monitor meshes.
- For a true fullscreen damage effect, create a full-screen `RawImage` on a top Canvas and add this controller.
