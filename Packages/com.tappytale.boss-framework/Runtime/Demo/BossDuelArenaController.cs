using TappyTale.BossFight.Core;
using TappyTale.BossFight.Data;
using TappyTale.BossFight.Events;
using TappyTale.BossFight.Player;
using TappyTale.BossFight.Preferences;
using UnityEngine;

namespace TappyTale.BossFight.Demo
{
    public enum BossDuelOutcome
    {
        InProgress = 0,
        PlayerVictory = 1,
        PlayerDefeat = 2
    }

    /// <summary>
    /// Orchestrates the Boss Duel Lab loop (Attack -> Read Signal -> React -> Punish -> Victory/Defeat -> Restart),
    /// renders the combat HUD, and persists session stats into the framework's typed PlayerPrefs store.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BossDuelArenaController : MonoBehaviour
    {
        [SerializeField] private BossDemoProfile demoProfile;
        [SerializeField] private BossDirector bossDirector;
        [SerializeField] private PlayerController playerController;
        [SerializeField] private BossDemoHealthReceiver fallbackPlayerHealth;
        [SerializeField] private BossDuelCameraController cameraController;
        [SerializeField] private BossPreferencesRegistry preferencesRegistry;
        [SerializeField] private string preferencesProfile = "duel_lab";
        [SerializeField] private bool showHud = true;

        private BossPreferencesService _preferences;
        private BossRuntime _boundBossRuntime;
        private PlayerRuntime _boundPlayerRuntime;
        private BossDuelOutcome _outcome = BossDuelOutcome.InProgress;
        private string _bannerText = string.Empty;
        private Color _bannerColor = Color.white;
        private float _bannerRemaining;

        private Texture2D _whiteTex;
        private GUIStyle _titleStyle;
        private GUIStyle _subtitleStyle;
        private GUIStyle _bannerStyle;
        private GUIStyle _controlsStyle;

        public BossDuelOutcome Outcome => _outcome;
        public BossDemoProfile DemoProfile => demoProfile;
        public BossDirector BossDirector => bossDirector;
        public PlayerController PlayerController => playerController;

        private void Awake()
        {
            _preferences = new BossPreferencesService(new PlayerPrefsStore(), preferencesRegistry);
            if (demoProfile != null && !string.IsNullOrWhiteSpace(demoProfile.PreferencesProfile))
            {
                preferencesProfile = demoProfile.PreferencesProfile;
            }

            if (cameraController != null)
            {
                cameraController.ShakeScaleMultiplier = _preferences.GetFloat(preferencesProfile, BossPreferencesService.KeyCameraShakeScale);
            }
        }

        private void Start()
        {
            TryBindCombatants();
            RecordFightStarted();
        }

        private void Update()
        {
            if (_boundBossRuntime != bossDirector?.Runtime || _boundPlayerRuntime != playerController?.Runtime)
            {
                TryBindCombatants();
            }

            if (_bannerRemaining > 0f)
            {
                _bannerRemaining = Mathf.Max(0f, _bannerRemaining - Time.unscaledDeltaTime);
            }

            bool restartRequested = (playerController != null && playerController.InputSource != null && playerController.InputSource.RestartPressed)
                                    || Input.GetKeyDown(KeyCode.R);
            if (restartRequested)
            {
                RestartDuel();
            }

            if (_outcome == BossDuelOutcome.InProgress && fallbackPlayerHealth != null && playerController == null)
            {
                if (fallbackPlayerHealth.CurrentHealth <= 0f)
                {
                    OnPlayerDied();
                }
            }
        }

        private void OnDisable()
        {
            UnbindCombatants();
        }

        private void OnDestroy()
        {
            if (_whiteTex != null)
            {
                Destroy(_whiteTex);
            }
        }

        public void RestartDuel()
        {
            Time.timeScale = 1f;
            _outcome = BossDuelOutcome.InProgress;
            _bannerRemaining = 1.6f;
            _bannerText = "DUEL START";
            _bannerColor = new Color(0.95f, 0.78f, 0.25f, 1f);

            if (playerController != null)
            {
                playerController.SetDisabled(false);
                playerController.ResetPlayer(restoreTransform: true);
            }

            if (fallbackPlayerHealth != null)
            {
                fallbackPlayerHealth.Restore();
            }

            if (bossDirector != null)
            {
                bossDirector.ResetFight(restoreTransform: true);
            }

            RecordFightStarted();
        }

        private void TryBindCombatants()
        {
            UnbindCombatants();

            _boundBossRuntime = bossDirector != null ? bossDirector.Runtime : null;
            if (_boundBossRuntime != null)
            {
                _boundBossRuntime.Events.Raised += OnBossEvent;
            }

            _boundPlayerRuntime = playerController != null ? playerController.Runtime : null;
            if (_boundPlayerRuntime != null)
            {
                _boundPlayerRuntime.Died += OnPlayerDied;
            }

            if (playerController != null)
            {
                playerController.ParrySucceeded += OnPlayerParried;
                playerController.PerfectDodgeSucceeded += OnPlayerPerfectDodged;
            }
        }

        private void UnbindCombatants()
        {
            if (_boundBossRuntime != null)
            {
                _boundBossRuntime.Events.Raised -= OnBossEvent;
                _boundBossRuntime = null;
            }

            if (_boundPlayerRuntime != null)
            {
                _boundPlayerRuntime.Died -= OnPlayerDied;
                _boundPlayerRuntime = null;
            }

            if (playerController != null)
            {
                playerController.ParrySucceeded -= OnPlayerParried;
                playerController.PerfectDodgeSucceeded -= OnPlayerPerfectDodged;
            }
        }

        private void OnBossEvent(BossEvent evt)
        {
            switch (evt.Type)
            {
                case BossEventType.PhaseStarted:
                    if (evt.Index > 0)
                    {
                        ShowBanner($"PHASE {evt.Index + 1} - ENRAGED", new Color(1f, 0.38f, 0.15f, 1f), 2.0f);
                    }
                    break;
                case BossEventType.BossStaggered:
                    ShowBanner("PARRIED! BOSS STAGGERED — PUNISH NOW!", new Color(1f, 0.88f, 0.2f, 1f), evt.Value > 0f ? evt.Value : 2.0f);
                    break;
                case BossEventType.Died:
                    if (_outcome == BossDuelOutcome.InProgress)
                    {
                        _outcome = BossDuelOutcome.PlayerVictory;
                        ShowBanner("VICTORY — BOSS DEFEATED!", new Color(0.25f, 0.92f, 0.45f, 1f), 999f);
                        RecordVictory();
                    }
                    break;
            }
        }

        private void OnPlayerParried()
        {
            if (_preferences != null)
            {
                int count = _preferences.GetInt(preferencesProfile, BossPreferencesService.KeyParriesLanded);
                _preferences.SetInt(preferencesProfile, BossPreferencesService.KeyParriesLanded, count + 1);
            }
        }

        private void OnPlayerPerfectDodged()
        {
            ShowBanner("PERFECT DODGE!", new Color(0.25f, 0.9f, 1f, 1f), 1.2f);
            if (_preferences != null)
            {
                int count = _preferences.GetInt(preferencesProfile, BossPreferencesService.KeyPerfectDodges);
                _preferences.SetInt(preferencesProfile, BossPreferencesService.KeyPerfectDodges, count + 1);
            }
        }

        private void OnPlayerDied()
        {
            if (_outcome != BossDuelOutcome.InProgress)
            {
                return;
            }

            _outcome = BossDuelOutcome.PlayerDefeat;
            bossDirector?.StopFight();
            ShowBanner("DEFEAT — PRESS [R] TO RETRY", new Color(1f, 0.22f, 0.22f, 1f), 999f);

            if (_preferences != null)
            {
                int defeats = _preferences.GetInt(preferencesProfile, BossPreferencesService.KeyDefeats);
                _preferences.SetInt(preferencesProfile, BossPreferencesService.KeyDefeats, defeats + 1);
            }
        }

        private void RecordFightStarted()
        {
            if (_preferences == null) return;
            int started = _preferences.GetInt(preferencesProfile, BossPreferencesService.KeyFightsStarted);
            _preferences.SetInt(preferencesProfile, BossPreferencesService.KeyFightsStarted, started + 1, saveImmediately: false);
            if (bossDirector != null && bossDirector.Definition != null)
            {
                _preferences.SetString(preferencesProfile, BossPreferencesService.KeyLastBossId, bossDirector.Definition.Id, saveImmediately: false);
            }
            _preferences.Store.Save();
        }

        private void RecordVictory()
        {
            if (_preferences == null) return;
            int victories = _preferences.GetInt(preferencesProfile, BossPreferencesService.KeyVictories);
            _preferences.SetInt(preferencesProfile, BossPreferencesService.KeyVictories, victories + 1, saveImmediately: false);

            float elapsed = bossDirector?.Blackboard != null ? bossDirector.Blackboard.FightElapsedTime : 0f;
            if (elapsed > 0.1f)
            {
                float best = _preferences.GetFloat(preferencesProfile, BossPreferencesService.KeyBestVictoryTime);
                if (best <= 0.01f || elapsed < best)
                {
                    _preferences.SetFloat(preferencesProfile, BossPreferencesService.KeyBestVictoryTime, elapsed, saveImmediately: false);
                }
            }
            _preferences.Store.Save();
        }

        private void ShowBanner(string text, Color color, float duration)
        {
            _bannerText = text;
            _bannerColor = color;
            _bannerRemaining = duration;
        }

        private void EnsureGuiStyles()
        {
            if (_whiteTex == null)
            {
                _whiteTex = new Texture2D(1, 1);
                _whiteTex.SetPixel(0, 0, Color.white);
                _whiteTex.Apply();
            }

            _titleStyle ??= new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color(0.96f, 0.96f, 0.98f, 1f) }
            };

            _subtitleStyle ??= new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleRight,
                normal = { textColor = new Color(0.85f, 0.85f, 0.9f, 1f) }
            };

            _bannerStyle ??= new GUIStyle(GUI.skin.label)
            {
                fontSize = 22,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };

            _controlsStyle ??= new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                wordWrap = true,
                normal = { textColor = new Color(0.84f, 0.86f, 0.9f, 1f) }
            };
        }

        private void OnGUI()
        {
            if (!showHud) return;
            EnsureGuiStyles();

            float screenW = Screen.width;
            float screenH = Screen.height;

            // 1. Boss Top Health & Stagger Bar
            if (bossDirector != null && bossDirector.Blackboard != null)
            {
                BossBlackboard bb = bossDirector.Blackboard;
                float barW = Mathf.Min(640f, screenW * 0.62f);
                float barX = (screenW - barW) * 0.5f;
                float barY = 22f;

                DrawRect(new Rect(barX - 8f, barY - 6f, barW + 16f, 58f), new Color(0.08f, 0.09f, 0.11f, 0.88f));

                string bossName = bossDirector.Definition != null ? bossDirector.Definition.DisplayName : "Boss";
                string phaseLabel = $"Phase {bb.CurrentPhaseIndex + 1}";
                if (bb.IsStaggered) phaseLabel = $"STAGGERED ({bb.StaggerRemaining:0.0}s) • x{bb.PunishDamageMultiplier:0.0} DMG";
                else if (bb.IsInvulnerable) phaseLabel = "INVULNERABLE";

                GUI.Label(new Rect(barX, barY - 2f, barW * 0.6f, 22f), bossName.ToUpperInvariant(), _titleStyle);
                GUI.Label(new Rect(barX + barW * 0.4f, barY - 2f, barW * 0.6f, 22f), $"{bb.CurrentHealth:0}/{bb.MaxHealth:0}  [{phaseLabel}]", _subtitleStyle);

                DrawBar(
                    new Rect(barX, barY + 22f, barW, 16f),
                    bb.NormalizedHealth,
                    bb.IsStaggered ? new Color(1f, 0.82f, 0.18f, 1f) : new Color(0.88f, 0.22f, 0.2f, 1f),
                    new Color(0.18f, 0.18f, 0.22f, 1f));

                float staggerThresh = bossDirector.Definition != null && bossDirector.Definition.StaggerThreshold > 0f
                    ? bossDirector.Definition.StaggerThreshold
                    : 100f;
                float staggerRatio = bb.IsStaggered ? (bb.StaggerRemaining / 2.2f) : Mathf.Clamp01(bb.CurrentStaggerBuildup / staggerThresh);
                DrawBar(
                    new Rect(barX, barY + 41f, barW, 5f),
                    staggerRatio,
                    new Color(1f, 0.72f, 0.18f, 1f),
                    new Color(0.14f, 0.14f, 0.16f, 1f));

                // Reaction window prompt pill
                if (bb.IsParryWindowOpen)
                {
                    DrawPromptPill(new Rect((screenW - 340f) * 0.5f, barY + 62f, 340f, 30f), "⚡ PARRY WINDOW OPEN — PRESS [F] or [RMB]!", new Color(1f, 0.82f, 0.12f, 0.95f));
                }
                else if (bb.IsPerfectDodgeWindowOpen)
                {
                    DrawPromptPill(new Rect((screenW - 360f) * 0.5f, barY + 62f, 360f, 30f), "💨 PERFECT DODGE WINDOW — PRESS [L] or [SHIFT]!", new Color(0.18f, 0.85f, 1f, 0.95f));
                }
            }

            // 2. Player Bottom-Left Health & State Panel
            float playerHpNorm = 1f;
            float playerHpCur = 100f;
            float playerHpMax = 100f;
            string playerStateLabel = "Ready";

            if (playerController != null && playerController.Blackboard != null)
            {
                PlayerBlackboard pbb = playerController.Blackboard;
                playerHpNorm = pbb.NormalizedHealth;
                playerHpCur = pbb.CurrentHealth;
                playerHpMax = pbb.MaxHealth;
                playerStateLabel = pbb.State.ToString();
                if (pbb.IsHardLocked) playerStateLabel += " • LOCKED-ON";
            }
            else if (fallbackPlayerHealth != null)
            {
                playerHpNorm = fallbackPlayerHealth.NormalizedHealth;
                playerHpCur = fallbackPlayerHealth.CurrentHealth;
                playerHpMax = fallbackPlayerHealth.MaxHealth;
            }

            Rect playerPanel = new(20f, screenH - 86f, 340f, 66f);
            DrawRect(playerPanel, new Color(0.08f, 0.09f, 0.11f, 0.88f));
            GUI.Label(new Rect(playerPanel.x + 12f, playerPanel.y + 8f, 180f, 22f), "PLAYER", _titleStyle);
            GUI.Label(new Rect(playerPanel.x + 130f, playerPanel.y + 8f, 198f, 22f), $"{playerHpCur:0}/{playerHpMax:0}  [{playerStateLabel}]", _subtitleStyle);
            DrawBar(
                new Rect(playerPanel.x + 12f, playerPanel.y + 34f, playerPanel.width - 24f, 16f),
                playerHpNorm,
                new Color(0.2f, 0.78f, 0.95f, 1f),
                new Color(0.18f, 0.18f, 0.22f, 1f));

            // 3. Controls & Telemetry Box (Top-Left)
            Rect helpRect = new(20f, 22f, 270f, 142f);
            DrawRect(helpRect, new Color(0.08f, 0.09f, 0.11f, 0.82f));
            string controlsText =
                "<b>BOSS DUEL LAB CONTROLS</b>\n" +
                "• Move / Sprint: <b>WASD</b> + <b>Left Shift</b>\n" +
                "• Jump / Double Jump: <b>Space</b>\n" +
                "• Light / Heavy Attack: <b>J / LMB</b> | <b>K</b>\n" +
                "• Parry / Deflect: <b>F / RMB</b>\n" +
                "• Dodge / Dash: <b>L / C</b> | <b>Left Shift</b>\n" +
                "• Lock-On / Restart: <b>Tab</b> | <b>R</b>";
            GUI.Label(new Rect(helpRect.x + 10f, helpRect.y + 8f, helpRect.width - 20f, helpRect.height - 16f), controlsText, _controlsStyle);

            // 4. Center Banner & Restart Button on Victory / Defeat
            if (_bannerRemaining > 0f && !string.IsNullOrEmpty(_bannerText))
            {
                Rect bannerRect = new((screenW - 560f) * 0.5f, screenH * 0.26f, 560f, 48f);
                DrawRect(bannerRect, new Color(0.06f, 0.07f, 0.09f, 0.92f));
                _bannerStyle.normal.textColor = _bannerColor;
                GUI.Label(bannerRect, _bannerText, _bannerStyle);

                if (_outcome != BossDuelOutcome.InProgress)
                {
                    Rect btnRect = new((screenW - 220f) * 0.5f, bannerRect.yMax + 12f, 220f, 36f);
                    if (GUI.Button(btnRect, "Restart Duel [R]"))
                    {
                        RestartDuel();
                    }
                }
            }
        }

        private void DrawPromptPill(Rect rect, string text, Color color)
        {
            DrawRect(rect, new Color(0.08f, 0.09f, 0.12f, 0.94f));
            _subtitleStyle.alignment = TextAnchor.MiddleCenter;
            Color prev = _subtitleStyle.normal.textColor;
            _subtitleStyle.normal.textColor = color;
            GUI.Label(rect, text, _subtitleStyle);
            _subtitleStyle.normal.textColor = prev;
            _subtitleStyle.alignment = TextAnchor.MiddleRight;
        }

        private void DrawBar(Rect rect, float normalized, Color fillColor, Color bgColor)
        {
            DrawRect(rect, bgColor);
            Rect fill = new(rect.x, rect.y, rect.width * Mathf.Clamp01(normalized), rect.height);
            DrawRect(fill, fillColor);
        }

        private void DrawRect(Rect rect, Color color)
        {
            Color prev = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, _whiteTex);
            GUI.color = prev;
        }
    }
}
