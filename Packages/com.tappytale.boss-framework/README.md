# Tappy Tale — Boss Fight Framework (`com.tappytale.boss-framework`)

Data-driven **Boss & Player Combat Framework** for **Unity 6 (`6000.4`+)** and **Universal Render Pipeline (URP)**.

## Key Features
- **Boss Runtime:** Multi-phase health thresholds, weighted/range/cooldown attack scheduling, deterministic `AttackTimelineRunner`, reaction windows (`Parry` / `Perfect Dodge`), stagger & punish damage multipliers, 3D procedural telegraphs, pooled projectiles, and `Radial` / `Burst` / `Arc` / `Spiral` pattern generators.
- **Player Runtime:** Camera-relative locomotion, jump + coyote time + jump buffer + double jump, ground/air dash with i-frames, Light/Heavy melee combos, Parry (deflects boss attacks & parryable projectiles), Dodge & Perfect Dodge, explicit Lock-On target registry (`PlayerLockOnRegistry`), and 1-click `AnimatorController` generator (`PlayerAnimatorBuilder`).
- **UI Toolkit Studio Window (`Tools > Tappy Tale > Boss Fight Framework > Open Studio Window`):** 12-section central dashboard for authoring all ScriptableObject definitions, managing demo profiles, validating references with 1-click Auto-Fix, and inspecting/editing scoped `PlayerPrefs` save data (`tappytale.bossframework.<profile>.<key>`).
- **3 Regenerable URP Demos:**
  1. **Boss Duel Lab** (`BossDuelLabBuilder`) — Complete Player vs Boss duel loop.
  2. **Boss Fight Demo** (`BossDemoSceneBuilder`) — Boss AI & timeline arena.
  3. **Player Core Demo** (`PlayerCoreDemoBuilder`) — Player locomotion, lock-on & combat gym.

See the repository root `README.md` for full architecture, controls, and usage documentation.
