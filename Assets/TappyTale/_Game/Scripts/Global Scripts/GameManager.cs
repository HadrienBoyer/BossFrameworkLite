using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Scripting.APIUpdating;

namespace MSJTDCC
{
    /// <summary>Whether the game is currently showing a blocking UI screen or accepting gameplay input.</summary>
    public enum PlayMode
    {

        UIMODE,
        GAMEMODE
    }

    /// <summary>
    /// Tracks global play/UI mode, frame rate, and level-load state. Access via
    /// <see cref="Toolbox.GameManager"/>.
    /// </summary>
    [MovedFrom(true, null, null, "GameManager")]
    public class GameManager : MonoBehaviour
    {

        private static PlayMode _playMode = PlayMode.UIMODE;

        /// <summary>The current play mode. Setting this fires <see cref="onPlayModeChanged"/> when it changes.</summary>
        public static PlayMode playMode
        {
            get => _playMode;
            set
            {
                if (_playMode != value)
                {
                    PlayMode oldMode = _playMode;
                    _playMode = value;
                    OnPlayModeChanged(oldMode, _playMode);
                }
            }
        }

        [Header("Other")]
        /// <summary>Non-null while an async scene load (<see cref="LoadLevel"/>) is in progress.</summary>
        [HideInInspector]
        public AsyncOperation async = null;
        public bool testMode = false;
        /// <summary>Mirrors <see cref="playMode"/> as a plain bool for Inspector visibility; kept in sync in <see cref="LateUpdate"/>.</summary>
        public bool uiMode = false;



        private void Start()
        {
            SetFrameRate();
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
        }

        void LateUpdate()
        {

            uiMode = (playMode == PlayMode.UIMODE);

            if (async != null)
            {

                if (async.progress == 1)
                {

                    async = null;
                }
            }
        }

        /// <summary>Sets <see cref="Time.timeScale"/> directly (e.g. to pause gameplay).</summary>
        public void SetTimeScale(float _val)
        {

            Time.timeScale = _val;
        }

        /// <summary>Caps the application frame rate at 60 FPS.</summary>
        public void SetFrameRate()
        {

            Application.targetFrameRate = 60;
        }

        /// <summary>Begins an async load of the scene at the given build index. Progress is tracked via <see cref="async"/>.</summary>
        public void LoadLevel(int _sceneIndex)
        {
            SceneManager.LoadSceneAsync(_sceneIndex);
        }


        /// <summary>Fires whenever PlayMode changes. Argument is the new mode.</summary>
        public static event Action<PlayMode> onPlayModeChanged;

        private static void OnPlayModeChanged(PlayMode oldMode, PlayMode newMode)
        {
            onPlayModeChanged?.Invoke(newMode);
        }


    }
}
