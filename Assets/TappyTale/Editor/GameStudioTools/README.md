# TappyTale Game Studio Tools

A single dockable Unity Editor cockpit for TappyTale tools.

Menu:

Tools > Tappy Tale > Game Studio Tools

## Why

The project has accumulated specialized TappyTale editor tools such as:

- GC2 Traffic & Crowd
- DED Crowd Builder / Zombie Ou Pouki
- GC2 Zombie Entity Factory
- Radio Voice Filter
- Performance Optimizer
- Child Pattern Placement
- Boss Framework
- ARPG Controller tools
- TTFPC installer/validator
- reusable Game Framework tools

Rather than replacing all of these systems, Game Studio Tools centralizes them.

## Architecture

Game Studio Tools uses three integration strategies.

### 1. Embedded EditorWindow

Existing TappyTale `EditorWindow` types are discovered with `TypeCache`.

The hub creates an internal hidden instance and invokes its `OnGUI()` inside the parent window.

This allows existing windows to appear as tabs without opening a separate floating editor window.

If a legacy window cannot be embedded safely, the hub shows the failure and keeps the direct menu action as a fallback.

### 2. Inline inspectors

For systems that are components / ScriptableObjects rather than EditorWindows, the hub finds project and scene objects and renders their normal Unity inspector inline.

Examples:

- TrafficWorld
- TrafficSettings
- TrafficSpawnCatalog
- TrafficLane
- CrowdGenerator
- CrowdFormationPreset
- ZombieGC2Bridge
- RadioVoiceFilter
- RadioFilterPreset
- CachedShadowLight
- PerformanceProfile

This keeps data editing in the same docked workspace.

### 3. Menu actions

Static installers or utilities that do not expose reusable GUI are surfaced as direct actions.

Examples:

- ARPG Input Actions installer
- TTFPC installer
- other `Tools > Tappy Tale` actions

## Project audit

The Audit tab scans the real `Assets/TappyTale/**` project folder.

It records:

- root modules
- file count
- C# count
- ScriptableObject `.asset` count
- prefabs
- scenes
- compiled TappyTale EditorWindow types
- TappyTale custom Editors
- `Tools/Tappy Tale` MenuItem declarations

Use `Export Audit JSON` to create:

Assets/TappyTale/Editor/GameStudioTools/Generated/GameStudioTools_Audit.json

This means the hub is not limited to the modules known when it was written.
New TappyTale EditorWindows automatically appear in the Modules tab.

## Current primary tabs

- Home
- World
- Characters
- Audio
- Performance
- Gameplay
- Design
- Modules
- Audit

### World

Traffic & Crowd and DED Crowd Builder live here.

Traffic reuses the existing `TrafficWorldGeneratorWindow` where available and adds inline access to TrafficWorld/Settings/Catalog/Lanes.

Crowd Builder exposes the Zombie Ou Pouki arena window plus CrowdGenerator and preset assets inline.

### Characters

Zombie Entity Factory and zombie bridge tools.

### Audio

Radio voice components/presets.

### Performance

Performance Optimizer, CachedShadowManager, CachedShadowLight, DynamicShadowCaster and profiles.

### Design

Child Pattern Placement and other layout/blockout windows.

### Gameplay

Boss Framework, ARPG controller and TTFPC.

## Important

This package intentionally does not modify runtime modules and does not add asmdefs.

It is an Editor-only integration layer under:

Assets/TappyTale/Editor/GameStudioTools/

It can therefore be removed without changing Traffic, Crowd, Audio, Zombie or Performance runtime behavior.


## Sidebar and Bark Generator

The Odin sidebar synchronizes persisted selection only when rebuilt or navigated programmatically; normal mouse and keyboard selection is never overwritten on each IMGUI event. Bark Generator has its own sidebar entry. Its Odin PropertyTree is drawn inside Studio Tools, with the original window lifecycle and queue callbacks retained; the host refreshes while an embedded tool is active. No generation is started by navigation.
