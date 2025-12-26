using System;
using BaseCore.Sound;
using UnityEngine;

namespace BaseCore.Setting
{
    [Serializable]
    public class AudioSettings
    {
        public bool useCustomVolume = false; // false = dùng default, true = dùng volume user chỉnh

        // DEFAULT (design)
        [Range(0f, 1f)] public float defaultBgmVolume = 1f;
        [Range(0f, 1f)] public float defaultSfxVolume = 1f;

        // USER (khi useCustomVolume = true)
        [Range(0f, 1f)] public float userBgmVolume = 1f;
        [Range(0f, 1f)] public float userSfxVolume = 1f;

        // Tick = bật, Untick = mute
        public bool bgmEnabled = true;
        public bool sfxEnabled = true;
    }

    public class SettingManager : MonoBehaviour, ISettings
    {
        [field: SerializeField] public AudioSettings Audio { get; private set; } = new AudioSettings();
        public event Action<AudioSettings> OnAudioChanged;

        private const string KEY_AUDIO = "GAME_AUDIO_SETTINGS_JSON";

        protected void Awake()
        {
            LoadAudio();
            ApplyAudio();
        }

        // ===== API (Design) =====
        // Bật/tắt mode cho phép user chỉnh volume
        public void SetUseCustomVolume(bool useCustom)
        {
            Audio.useCustomVolume = useCustom;
            SaveApplyNotify();
        }

        // Set default theo design (bạn set trong Inspector hoặc gọi lúc init)
        public void SetDefaultVolumes(float bgm, float sfx)
        {
            Audio.defaultBgmVolume = Mathf.Clamp01(bgm);
            Audio.defaultSfxVolume = Mathf.Clamp01(sfx);
            SaveApplyNotify();
        }

        // ===== API (User) =====
        public void SetUserBgmVolume(float v)
        {
            Audio.userBgmVolume = Mathf.Clamp01(v);
            SaveApplyNotify();
        }

        public void SetUserSfxVolume(float v)
        {
            Audio.userSfxVolume = Mathf.Clamp01(v);
            SaveApplyNotify();
        }

        public void SetBgmEnabled(bool enabled)
        {
            Audio.bgmEnabled = enabled;
            SaveApplyNotify();
        }

        public void SetSfxEnabled(bool enabled)
        {
            Audio.sfxEnabled = enabled;
            SaveApplyNotify();
        }

        // ===== Core =====
        public float GetFinalBgmVolume()
            => Audio.useCustomVolume ? Audio.userBgmVolume : Audio.defaultBgmVolume;

        public float GetFinalSfxVolume()
            => Audio.useCustomVolume ? Audio.userSfxVolume : Audio.defaultSfxVolume;

        private void ApplyAudio()
        {
            // var sm = SoundManager.Instance;
            // if (sm == null) return;
            //
            // sm.SetBgmVolume(GetFinalBgmVolume());
            // sm.SetSfxVolume(GetFinalSfxVolume());
            // sm.SetBgmMuted(!Audio.bgmEnabled);
            // sm.SetSfxMuted(!Audio.sfxEnabled);
        }

        private void SaveApplyNotify()
        {
            SaveAudio();
            ApplyAudio();
            OnAudioChanged?.Invoke(Audio);
        }

        public void LoadAudio()
        {
            if (!PlayerPrefs.HasKey(KEY_AUDIO))
            {
                Audio = new AudioSettings();
                return;
            }

            var json = PlayerPrefs.GetString(KEY_AUDIO, "");
            if (string.IsNullOrEmpty(json))
            {
                Audio = new AudioSettings();
                return;
            }

            try
            {
                Audio = JsonUtility.FromJson<AudioSettings>(json) ?? new AudioSettings();
            }
            catch
            {
                Audio = new AudioSettings();
            }
        }

        private void SaveAudio()
        {
            PlayerPrefs.SetString(KEY_AUDIO, JsonUtility.ToJson(Audio));
            PlayerPrefs.Save();
        }
    }
    
    public interface ISettings
    {
        AudioSettings Audio { get; }
        event Action<AudioSettings> OnAudioChanged;

        void SetBgmEnabled(bool enabled);
        void SetSfxEnabled(bool enabled);
        void SetUserBgmVolume(float v);
        void SetUserSfxVolume(float v);
    }

}