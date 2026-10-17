using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Scripting.APIUpdating;

namespace MSJTDCC
{
    /// <summary>
    /// Plays SFX and background music through separate mixer groups, with volume levels
    /// read from <see cref="PlayerPrefs"/> ("gameAudio", "gameMusic"). Access via
    /// <see cref="Toolbox.Soundmanager"/>.
    /// </summary>
    [MovedFrom(true, null, null, "SoundManager")]
    public class SoundManager : MonoBehaviour
    {

        public bool isMute = false;
        /// <summary>When true, <see cref="PlayBGSound(AudioClip)"/> is a no-op (used to lock background music during special sequences).</summary>
        public bool forceChaseMode = false;

        [Header("Audio Mixers")]
        public AudioMixerGroup sfxMixer;
        public AudioMixerGroup musicMixer;

        [Header("Audio Sources")]
        /// <summary>Source used for one-shot SFX playback via <see cref="PlaySound"/>.</summary>
        public AudioSource audioo;
        /// <summary>Source used for looping background music.</summary>
        public AudioSource bgMusicSource;

        [Header("BG Clips")]
        public AudioClip menuBG;


        [Header("Sound Clips")]
        public AudioClip buttonPress;
        public AudioClip pressBack;

        void Start()
        {

            UpdateMusicStatus();
            UpdateAudioStatus();
        }

        /// <summary>Applies the "gameAudio" PlayerPrefs volume to the SFX mixer.</summary>
        public void UpdateAudioStatus()
        {
            sfxMixer.audioMixer.SetFloat("sfxVolume", Mathf.Log10(PlayerPrefs.GetFloat("gameAudio", 1f)) * 20);
        }

        /// <summary>Applies the "gameMusic" PlayerPrefs volume to the music mixer.</summary>
        public void UpdateMusicStatus()
        {
            musicMixer.audioMixer.SetFloat("musicVolume", Mathf.Log10(PlayerPrefs.GetFloat("gameMusic", 1f)) * 20);
        }

        /// <summary>Pauses both audio sources and mutes both mixers.</summary>
        public void Pause_All()
        {

            this.audioo.Pause();
            this.bgMusicSource.Pause();
            sfxMixer.audioMixer.SetFloat("sfxVolume", -80f);
            musicMixer.audioMixer.SetFloat("musicVolume", -80f);
            AudioListener.pause = true;
        }

        /// <summary>Resumes both audio sources and restores mixer volumes from PlayerPrefs.</summary>
        public void UnPause_All()
        {
            this.audioo.UnPause();
            this.bgMusicSource.UnPause();
            UpdateAudioStatus();
            UpdateMusicStatus();
            AudioListener.pause = false;

        }

        /// <summary>Pauses the SFX source only.</summary>
        public void Pause_Sound()
        {
            this.audioo.Pause();
        }

        /// <summary>Plays and loops a background music clip, restarting only if a different clip is requested.</summary>
        public void PlayBGSound(AudioClip _clip)
        {

            if (forceChaseMode)
                return;

            if (bgMusicSource.clip == _clip && bgMusicSource.isPlaying)
                return; // Already playing this clip, do not restart

            this.bgMusicSource.clip = _clip;

            this.bgMusicSource.Play();

            this.bgMusicSource.loop = true;
        }

        /// <summary>Plays and loops a background music clip at a specific volume.</summary>
        public void PlayBGSound(AudioClip _clip, float _volume)
        {

            this.bgMusicSource.clip = _clip;

            this.bgMusicSource.volume = _volume;

            this.bgMusicSource.Play();

            this.bgMusicSource.loop = true;
        }

        /// <summary>Plays a one-shot SFX clip. No-op if <paramref name="_clip"/> is null.</summary>
        public void PlaySound(AudioClip _clip)
        {

            if (_clip != null)
                audioo.PlayOneShot(_clip);
        }

        /// <summary>Stops the background music source.</summary>
        public void Stop_BGSound()
        {
            bgMusicSource.Stop();
        }

        /// <summary>Stops the SFX source.</summary>
        public void Stop_PlayingSound()
        {
            audioo.Stop();
        }

    }
}
