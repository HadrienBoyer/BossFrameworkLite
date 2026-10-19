# Tappy Tale — Boss Fight Framework (`com.tappytale.boss-framework`)

![Boss Fight Framework — GitHub social preview](Branding/social-preview.png)

Data-driven **Boss & Player Combat Framework** for **Unity 6 (`6000.4`+)** and **Universal Render Pipeline (URP)**, featuring a central **UI Toolkit Studio EditorWindow** for authoring ScriptableObject combat definitions, generating/duplicating playable URP demos, validating references, and inspecting/editing scoped `PlayerPrefs` save profiles.

---

## 1. Requirements & Package Metadata

- **Package ID:** `com.tappytale.boss-framework`
- **Package Path:** `Packages/com.tappytale.boss-framework`
- **Target Unity Version:** **Unity `6000.4`** (configured in `package.json` as `"unity": "6000.4"`, `"unityRelease": "0f1"`)
- **Render Pipeline:** **Universal Render Pipeline (URP)** (`Universal Render Pipeline/Lit` with automatic Built-in `Standard` fallback)
- **Zero External Art Dependencies:** All 3 demos (`Boss Duel Lab`, `Boss Fight Demo`, `Player Core Demo`) build and run using procedural Unity primitives, URP materials, and runtime telegraphs/projectiles.
- **Optional Integrations (Isolated Assemblies):**
  - `com.unity.inputsystem` (`1.0.0`+) → `TappyTale.BossFight.InputSystem` (`TAPPYTALE_INPUTSYSTEM_AVAILABLE`)
  - `com.unity.cinemachine` (`3.0.0`+) → `TappyTale.BossFight.Cinemachine` (`TAPPYTALE_CINEMACHINE_AVAILABLE`)

---

## 2. Architecture Overview

### Non-Negotiable Architecture Rules Enforced
1. **Strict Separation of Data (`ScriptableObject`) & Runtime State:**
   - All `ScriptableObject` definitions (`BossDefinition`, `BossPhaseDefinition`, `BossAttackDefinition`, `AttackTimelineDefinition`, `ProjectileDefinition`, `ProjectilePatternDefinition`, `TelegraphDefinition`, `AudioCueDefinition`, `VfxCueDefinition`, `CameraShakeDefinition`, `PlayerDefinition`, `BossDemoProfile`, `BossPreferencesRegistry`) are immutable during gameplay.
   - All mutable combat state lives exclusively in per-instance runtime classes (`BossRuntime`, `BossBlackboard`, `PlayerRuntime`, `PlayerBlackboard`, `BossPhaseController`, `BossAttackScheduler`, `AttackTimelineRunner`, `BossReactionWindowController`, `BossHealthComponent`).
2. **Strict Separation of Runtime & Editor:**
   - Zero `UnityEditor` references inside `TappyTale.BossFight.Runtime`.
   - Editor tooling, installers, demo builders, animator builders, and validators live in `TappyTale.BossFight.Editor`.
3. **Neutral Combat Contracts (`IDamageReceiver` & `DamageInfo`):**
   - `IDamageReceiver` (`TappyTale.BossFight.Combat`) decouples Boss and Player combat.
   - Player melee hitboxes damage `BossDirector` (and any `IDamageReceiver` on `DamageTeam.Boss`), while Boss melee attacks and `BossProjectile` instances damage `PlayerController` (`DamageTeam.Player`).
   - Legacy `IBossDamageReceiver` is preserved with `[Obsolete]` migration forwarding.
4. **Interchangeable Input Source (`PlayerInputSource`):**
   - Gameplay reads abstract `PlayerInputSource` (`LegacyPlayerInputSource` included out-of-the-box for zero-config demos; optional `InputSystemPlayerInputSource` in isolated assembly).
5. **Deterministic Lifecycle & Zero State Leaks:**
   - Explicit initialization, ordered tick (`BossRuntime.Tick`, `PlayerRuntime.Tick`), cancellation, and cleanup on disable/death/interruption.
   - Disabling or killing a Boss or Player immediately closes active hitboxes, parry/perfect-dodge reaction windows, dash invulnerability, and running timelines, and despawns pooled projectiles cleanly.

### Assembly Layout

| Assembly | Path | Purpose |
| :--- | :--- | :--- |
| `TappyTale.BossFight.Runtime` | `Runtime/TappyTale.BossFight.Runtime.asmdef` | Core Boss & Player runtime, Combat contracts, Projectiles/Patterns, Telegraphs, Feedback, Save Data (`PlayerPrefs`), Demo controllers |
| `TappyTale.BossFight.InputSystem` | `Runtime/InputSystem/TappyTale.BossFight.InputSystem.asmdef` | Optional Unity Input System adapter (`defineConstraints`: `TAPPYTALE_INPUTSYSTEM_AVAILABLE`) |
| `TappyTale.BossFight.Cinemachine` | `Runtime/Cinemachine/TappyTale.BossFight.Cinemachine.asmdef` | Optional Cinemachine 3 duel camera rig adapter (`defineConstraints`: `TAPPYTALE_CINEMACHINE_AVAILABLE`) |
| `TappyTale.BossFight.Editor` | `Editor/TappyTale.BossFight.Editor.asmdef` | UI Toolkit Studio Window, Installer, 3 URP Demo Builders, Demo Duplicator, Animator Builder, Validator |
| `TappyTale.BossFight.EditModeTests` | `Tests/EditMode/TappyTale.BossFight.EditModeTests.asmdef` | EditMode NUnit test suite covering Player, Boss, Patterns, and Save Data |

---

## 3. Quick Start & Unity Menu Commands

All tools are accessible under **`Tools > Tappy Tale > Boss Fight Framework`**:

| Menu Command | Description |
| :--- | :--- |
| **`Open Studio Window`** | Opens the central 12-section UI Toolkit EditorWindow (`BossFrameworkDashboard`) |
| **`Install / Repair Starter Assets (Safe)`** | Creates any missing starter definitions, URP materials, projectile prefab, and demo profiles in `Assets/TappyTale/BossFramework/` **without overwriting your modified assets** |
| **`Build Boss Duel Lab (Reference Demo)`** | Generates and opens `BossDuelLab.unity` — the full Player vs Boss duel arena |
| **`Build Boss Fight Demo (Arena Only)`** | Generates and opens `BossFightDemo.unity` — Boss AI + Timeline + Sandbox driver |
| **`Build Player Core Demo (Gym)`** | Generates and opens `PlayerCoreDemo.unity` — Locomotion, Dash, Jump, Lock-On & Combat Dummy gym |
| **`Run Full Validation`** | Validates all framework assets and logs the report to the Console & Studio Window |

---

## 4. Reference Demo: `Boss Duel Lab` Controls & Gameplay Loop

`Boss Duel Lab` (`Assets/TappyTale/BossFramework/Demos/BossDuelLab/BossDuelLab.unity`) implements the complete duel loop:
**Boss Attack → Telegraph Read → Parry / Perfect Dodge Reaction → Boss Stagger / Vulnerability → Player Punish Combo → Phase Transition → Victory / Defeat / Instant Restart (`R`)**.

### Default Controls (`LegacyPlayerInputSource` & `InputSystemPlayerInputSource`)

| Action | Keyboard / Mouse | Gamepad |
| :--- | :--- | :--- |
| **Move** | `W` `A` `S` `D` / Arrow Keys | Left Stick |
| **Jump / Double Jump** | `Space` | South Button (`A` / Cross) |
| **Dash (Ground / Air + i-frames)** | `Left Shift` | Right Shoulder (`RB` / `R1`) |
| **Light Attack (Combo)** | `J` or `Left Mouse Button` | West Button (`X` / Square) |
| **Heavy Attack** | `K` | North Button (`Y` / Triangle) |
| **Parry (Deflects Parry Window / Parryable Projectiles)** | `F` or `Right Mouse Button` | Left Shoulder (`LB` / `L1`) |
| **Dodge (Triggers Perfect Dodge in Dodge Window)** | `L` or `C` | East Button (`B` / Circle) |
| **Hard Lock-On Toggle** | `Tab` or `Middle Mouse Button` | Right Stick Press (`R3`) |
| **Switch Lock-On Target Left / Right** | `Q` / `E` | — |
| **Instant Duel Restart** | `R` | — |

---

## 5. Central UI Toolkit Studio Window (`BossFrameworkDashboard`)

Open via **`Tools > Tappy Tale > Boss Fight Framework > Open Studio Window`**.

### Top Context Toolbar
- **Demo Profile Selector:** Switch active context between `All Project Assets`, `Boss Duel Lab`, `Boss Fight Demo`, `Player Core Demo`, or any duplicated custom demo profile.
- **Save Profile Selector:** Switch active `PlayerPrefs` save profile (`default`, `duel_lab`, `playtest`, or custom).
- **Quick Actions:** `Repair Assets`, `Build Duel Lab`, `Validate` (with live Error/Warning badge), and `Save All`.

### 12 Navigation Sections
1. **Overview:** Framework health cards, active demo profile summary, starter kit installation, 1-click demo builders, and live validation summary.
2. **Bosses:** Master-detail inspector for `BossDefinition`, `BossDatabase`, and `BossPayloadLibrary` with incoming/outgoing reference tracking.
3. **Phases:** Edit `BossPhaseDefinition` health thresholds (`1.0` → `0.55`), transition invulnerability, cooldown reset rules, and assigned attack pools.
4. **Attacks & Timelines:** Edit `BossAttackDefinition` (weights, range, cooldown, parry/stagger/punish multipliers) and interactive ordered `AttackTimelineEntry` list (Sort by Time, Add/Duplicate/Remove entries, Payload validation against `BossPayloadLibrary`).
5. **Projectiles & Patterns:** Configure `ProjectileDefinition` (speed, lifetime, damage, parryable flag) and `ProjectilePatternDefinition` (`Radial`, `Burst`, `Arc`, `Spiral` with live deterministic shot count and duration preview).
6. **Telegraphs:** Configure `TelegraphDefinition` shape (`Circle`, `Box`, `Cone`, `Line`, `Ring`), warning/active/parryable colors, and dimensions.
7. **Feedback:** Manage `AudioCueDefinition`, `VfxCueDefinition`, and `CameraShakeDefinition` assets.
8. **Player:** Configure `PlayerDefinition` locomotion, jump/coyote/buffer, dash i-frames, combat attacks, parry/dodge windows, lock-on, and 1-click **`Generate / Rebuild Animator Controller`** (`PlayerAnimatorBuilder`).
9. **Scenes & Demos:** Inspect `BossDemoProfile` manifests, **`Build / Open Scene`**, **`Repair Missing Assets (Safe)`**, **`Duplicate Demo (Deep Copy)`** into a new folder, or **`Factory Reset Demo`** (with confirmation dialog).
10. **Save Data / PlayerPrefs:** Inspect and live-edit all registered framework keys (`tappytale.bossframework.<profile>.<key>`), filter by category/search, reset individual keys, reset the active profile, or **Export / Import JSON Snapshots**. Never calls `PlayerPrefs.DeleteAll()`.
11. **Validation:** Full project & demo validation with severity filter and 1-click **`Auto-Fix`** buttons for repairable issues.
12. **Settings / Debug:** Live runtime telemetry when in Play Mode (Boss HP, Phase, Timeline, Reaction Windows, Player State, Lock-On Target, Active Pooled Projectiles).

---

## 6. Save Data / `PlayerPrefs` Specification

All persistence is strictly isolated under the prefix:
```text
tappytale.bossframework.<profile>.<key>
```
- **Store Abstraction:** `IPreferencesStore` with `PlayerPrefsStore` (Unity `PlayerPrefs` adapter with strict namespace guard) and `InMemoryPreferencesStore` (for unit tests).
- **Safety Guarantee:** `PlayerPrefs.DeleteAll()` is **never** called anywhere in the codebase. Profile resets iterate exclusively over registered `PreferenceKeyDefinition` entries in `BossPreferencesService`.

---

## 7. Validation & Unit Tests

- **EditMode Tests:** Located in `Packages/com.tappytale.boss-framework/Tests/EditMode/BossFrameworkEditModeTests.cs`. Run via Unity **Window > General > Test Runner > EditMode**.
- **Editor Asset Validator:** Run via **`Tools > Tappy Tale > Boss Fight Framework > Run Full Validation`** or the **Validation** tab in the Studio Window.

## 8. Branding Assets

The repository includes a GitHub-ready visual identity pack in [`Branding/`](Branding/README.md): SVG emblems and wordmarks, PNG icons, and a 1280 × 640 social preview. Upload `Branding/social-preview.png` in the repository's **Settings → General → Social preview** to use it on GitHub.

