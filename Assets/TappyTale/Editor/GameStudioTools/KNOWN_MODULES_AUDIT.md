# Known TappyTale module audit

This document records the modules identified from the project materials available while Game Studio Tools was assembled.
The in-Unity Audit tab is more authoritative because it scans the actual project folder.

## Strong candidates for embedding

- GC2 Traffic & Crowd
  - Existing `TrafficWorldGeneratorWindow`
  - Runtime/editor split under `Assets/TappyTale/GC2/Traffic/`
  - Best fit: World > Traffic & Crowd

- Child Pattern Placement
  - Existing `ChildPatternPlacementWindow`
  - Best fit: Design > Child Pattern Placement

- DED Crowd Builder / Zombie Ou Pouki
  - Existing arena blockout EditorWindow
  - CrowdGenerator and presets can be edited inline
  - Best fit: World > DED Crowd Builder

- Boss Framework
  - Existing Boss Editor workflow
  - Best fit: Gameplay > Boss Framework

- Performance Optimizer
  - Existing Tools entry, cached shadow/light systems
  - Best fit: Performance

## Better as inline inspectors

- Radio Voice Filter
  - Runtime component + custom Editor + ScriptableObject presets
  - Best fit: Audio

- Traffic settings/catalog/lanes
  - Keep generator UI embedded; inspect project/scene objects inline

- CrowdGenerator / CrowdFormationPreset / CrowdReactionPreset
  - Keep arena builder embedded; inspect generated crowd data inline

- CachedShadowManager / CachedShadowLight / DynamicShadowCaster / PerformanceProfile
  - Inspect inline beneath Performance tools

- Zombie bridge components
  - Inspect inline beneath Zombie tools

## Better as actions rather than permanent windows

- ARPG Controller input-actions installer
- TTFPC sample installer / demo scene / validator
- one-shot setup/validation utilities

These are operations, not workspaces, so they should be buttons in the hub rather than permanent tabs with empty chrome.

## Runtime-only or framework content

Reusable runtime systems should be discoverable in Audit/Modules, not turned into Editor tabs unless an actual production workflow benefits from it.
