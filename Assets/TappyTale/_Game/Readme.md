# Top-Down Character Controller (MSJ-TDCC)

A top-down interaction/puzzle toolkit for Unity: character movement, a fixed-angle follow camera,
an item pickup/inventory system, and a set of puzzle primitives (locks, code machines, pressure
plates, sensors, doors) that snap together via UnityEvents. Everything lives under
`Assets/MSJ-TDCC`.

## Requirements

| | |
|---|---|
| **Unity version** | 6000.3.14f1 (Unity 6) |
| **Render pipeline** | Universal Render Pipeline (URP) 17.3.0 |
| **Input** | Input System 1.19.0 (new Input System, not legacy `Input`) |
| **Other Unity packages** | AI Navigation 2.0.12 (NavMesh), Timeline 1.8.12, uGUI 2.0.0 (includes TextMeshPro), Visual Scripting 1.9.11 |
| **Third-party plugins** | None. Import and press Play — nothing else to install. |

## Project setup on import

Two things need to exist in a project for this asset to work correctly, and neither travels
automatically with a package import — Unity layers/tags are stored per-project and referenced
**by index**, so a fresh project needs them recreated:

- **Layers**: `Player`, `Interactable` (custom layers referenced by `InteractionHandler`,
  `CameraHandler`, and others via Inspector-assigned `LayerMask` fields — see below)
- **Tags**: `Ground_Concrete` (used by `AdvancedFootstepSystem` to pick a footstep sound set
  per surface; `Player` tag is a Unity built-in and needs no setup)

The `Tools > MSJ-TDCC > ToolWindow` panel (see [Editor Tooling](#editor-tooling)) checks all of
this for you and has an **Auto-Add Layers & Tag** button that creates whatever is missing, at the
right index. It opens by itself the first time the package compiles in a project.

It will not overwrite a slot already occupied by a different layer — if a target project already
has something at index 3 or 6, the window flags it as a conflict and you resolve it manually in
`Edit > Project Settings > Tags and Layers`. Left unresolved, the bundled prefabs will silently
point at the wrong layer, so check the panel before relying on the `LayerMask` fields below.

`LayerMask` fields that need a layer assigned per-project:

| Field | Script |
|---|---|
| `interactDetectionLayer` | `InteractionHandler` |
| `collisionLayers`, `wallLayers` | `CameraHandler` |
| `occlusionLayers`, `ignoreLayers` | `CameraOcclusion` |
| `killLayer` (optional) | `Health` |
| `raycastMask` (defaults to Everything) | `ThirdPersonController` |

## Getting started

1. Open `Assets/MSJ-TDCC/Scenes/Demo.unity` — the single demo/test scene containing
   the player, camera, HUD, and all interactable prefabs wired up.
2. `Tools > MSJ-TDCC > ToolWindow` opens a welcome/setup window (also opens automatically the
   first time the package's scripts compile in a project).
3. Press Play. Default controls: WASD/joystick to move, mouse/right-stick to look, configured
   via `Assets/InputSystem_Actions.inputactions`.

## Project structure

```
Assets/MSJ-TDCC/
├── Animation/       Animator controllers & clips (Door, Humanoid)
├── Audio/            Footstep and mixer assets
├── Editor/           Custom inspectors + the ToolWindow (Editor-only, not built into players)
├── Images/           UI sprites
├── Materials/        18 materials, incl. an "illuminated" variant set
├── Models/           Character mesh (UAL1_Standard.fbx)
├── Prefabs/
│   ├── Interactables/  20 prefabs: doors, switches, pickable items, sensors, puzzle pieces
│   └── UI/              8 prefabs: HUD, popups (Pause, Fail, CodeMachine, Message, etc.)
├── Scenes/            Demo.unity (the only scene)
└── Scripts/           48 C# scripts, organized by subsystem (see below)
```

## Core systems

### Global managers (`Scripts/Global Scripts/`)

| Script | Purpose |
|---|---|
| `Toolbox` | Service locator — exposes `GameManager`, `SoundManager`, `UiManager` as static accessors so any script can reach them without a direct reference. |
| `GameManager` | Tracks global play/UI mode, frame rate, and level-load state. |
| `SoundManager` | Plays SFX and background music through separate mixer groups; volume read from PlayerPrefs. |
| `UiManager` | Drives the full-screen menu stack (HUD, fail screen) with a history for "back" navigation. |
| `UIPopupManager` | Drives popup overlays (pause, message box, code machine, etc.) independently of the menu stack. |

### Player, camera & movement (`Scripts/`)

| Script | Purpose |
|---|---|
| `PlayerHandler` | Central hub for the player GameObject — caches component references, drives the flashlight toggle and delayed AudioListener enable. |
| `ThirdPersonController` | Main movement script — CharacterController-based walk/sprint/crouch/jump, camera-relative input, mouse look. |
| `CameraHandler` | Third-person follow camera — smoothed offset follow, zoom, wall collision avoidance, screen shake, cutscene and world-inspect modes. |
| `NavMeshMovementController` | Drives `ThirdPersonController` toward a NavMeshAgent-computed path (for scripted/cutscene movement). |
| `CharacterControllerImpact` | Forwards `OnControllerColliderHit` to `ImpactCollisionForce` on whatever the character walks into. |

### Interaction & inventory (`Scripts/`)

| Script | Purpose |
|---|---|
| `InteractionHandler` | Detects the nearest `InteractableObject` within a sphere around the player and routes interact/hold input to it. |
| `InteractableObject` | Base interactable component — interact prompt, sound, and one of: item lock, item placement, or pickup. |
| `PickableItem` | A world object that can be picked into a `PlayerItem` hold slot. |
| `PlayerItem` | Holds the single item the player is carrying; exposes pick-up/use/drop. |
| `ItemLock` | Gates an interaction behind a specific named item (e.g. a locked door needing a key). |
| `ItemPlacer` | A slot an item can be placed into/removed from, with correct-item checking and swap support. |

### Puzzle mechanics (`Scripts/`)

| Script | Purpose |
|---|---|
| `CodeMachine` | World-placed trigger that opens the shared code-machine popup, configured with its own code/attempt settings. |
| `Helping/CodeMachineListener` | Drives the on-screen code machine UI — number pad, feedback, success/failure callbacks. |
| `Door` | Animated door with open/close/force-close actions, each with audio, delay, and a UnityEvent hook; can persist open/closed state to PlayerPrefs. |
| `Sensor` | Trigger volume tracking tagged objects inside it, raising enter/leave (and first-in/last-out) events. |
| `StandSwitch` | Pressure-plate switch — turns on while stood on, or latches permanently, with progress feedback. |

### World physics & feedback (`Scripts/`)

| Script | Purpose |
|---|---|
| `Health` | Drives four death modes (animation, ragdoll, instant, explosion), each self-contained with sound/cleanup/UnityEvent. |
| `Force` | Applies an impulse force to a child Rigidbody, on demand or via a configured default. |
| `ImpactCollisionForce` | Applies physics force to this object's Rigidbody from tagged collisions or explicit impact reports. |
| `MaterialLight` | Drives a material's emission color and an optional Light between on/off states. |
| `GlowLight` | Named-state light switch — cycles through user-defined states (color/intensity/sound/event), instant or smooth transition. |

### UI & menus (`Scripts/Menu/`, plus a few root scripts)

| Script | Purpose |
|---|---|
| `HUDListener` | Drives the gameplay HUD — interact/drop prompts, mobile control visibility, Escape shortcut. |
| `MenuButton` | Generic menu button — plays a click sound and invokes a selected `Press_*` handler. |
| `MessageListener` | Message box popup with title/body text, closable via button or Escape. |
| `NamePopupListener` | Popup for entering/saving the player's display name. |
| `NotificationListener` | Simple single-message popup. |
| `TimerUI` | Displays a running timer; fully decoupled from whatever owns the timer logic. |
| `ObjectInspectorListener` | Instantiates a copy of an object into a turntable view for close inspection. |

### Helping / utility scripts (`Scripts/Helping/`)

| Script | Purpose |
|---|---|
| `AdvancedFootstepSystem` | Footstep sounds timed to locomotion speed, with per-surface (raycast + tag) sound sets. |
| `AdvancedToggle` | Two-state toggle driven by UnityEvents, with optional switch-count limits. |
| `CameraOcclusion` | Fades out renderers blocking the line of sight between camera and target. |
| `EventTriggerSystem` | Fires a UnityEvent on a configurable trigger (enable/disable/start/collision/external interaction), after an optional delay. |
| `FadeImg` | Fades a UI Image's alpha in/out at a constant rate. |
| `HoldTweenStateAnimator` | Animates a transform between two states while an interaction is held. |
| `InputActionButton` | Lets an Input System action trigger a UI Button's onClick. |
| `MoveableObjectSoundHandler` | Collision-impact and rolling sounds for a physics object, velocity-scaled, with per-surface overrides. |
| `ObjectRotator` | Drag-rotate / pinch-zoom for object inspection (mouse, touch, or Input System), with vertical-rotation clamping. |
| `ObjectSpawner` | Spawns prefabs from a weighted pool (timer/count/duration-based), with position/rotation/force randomization. |
| `TweenAnimations` | Configurable one-shot animation (position/rotation/scale offset + return). |
| `TweenStateAnimator` | Drives a transform through a named list of poses. |

### Editor tooling (`Editor/`)

| Script | Purpose |
|---|---|
| `MSJTDCCToolWindow` | Welcome/setup window (`Tools > MSJ-TDCC > ToolWindow`) — opens automatically on first import. Documentation link, layer/tag checklist with one-click setup, and the rating prompt (see below). |
| `UIInspector` | Custom inspector for `UiManager` — quick select/enable buttons per tracked menu. |
| `PopupInspector` | Custom inspector for `UIPopupManager` — quick select/enable buttons per tracked popup, plus "Disable All". |

### Rating prompt

Once the asset has been in a project for a day, a dialog asks for an Asset Store rating:

| Choice | Effect |
|---|---|
| **Rate Now** | Opens the Asset Store page and stops all further prompts |
| **Remind Me Later** | Asks again in 3 days, and keeps cycling until settled |
| **Don't Show Again** | Stops all further prompts |

Clicking the window's own **Rate this Asset** button counts as settled too, so nobody is asked
after they have already rated. The prompt never interrupts a compile, an asset import, or a
play-mode transition, and waits 20 seconds after a project opens before it can appear.

State lives in `EditorPrefs`, keyed per project — the key is salted with a stable hash of the
project's `Assets` path, so a decision in one project doesn't carry over to another on the same
machine. Timing is set by `FirstRatePromptDelay` and `RateSnoozeDelay` at the top of
`MSJTDCCToolWindow`.

`Tools > MSJ-TDCC > Reset Prompts (Testing)` clears that state and makes the prompt due
immediately, so the flow can be checked without waiting a day.

