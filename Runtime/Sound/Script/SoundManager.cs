using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using BaseCore;
using Random = UnityEngine.Random;

namespace BaseCore.Sound
{
    public enum SoundId
    {
        Click,
        Explosion,
        Pickup,
    }

    [System.Serializable]
    public class SoundDef
    {
        public SoundId id;
        public AudioClip clip;

        [Range(0f, 1f)] public float volume = 1f;
        [Range(0.5f, 2f)] public float pitch = 1f;

        public bool randomPitch = false;
        public Vector2 pitchRange = new Vector2(0.95f, 1.05f);

        public bool loop = false;
    }

    public class SoundManager : MonoBehaviour
    {
        [Header("Library")] public List<SoundDef> sfxLibrary = new();
        public List<SoundDef> bgmLibrary = new();

        [Header("AudioSources")] [SerializeField]
        private AudioSource bgmSource;

        [SerializeField] private int sfxPoolSize = 10;
        [SerializeField] private AudioSource sfxPrefabSource;

        private readonly Dictionary<SoundId, SoundDef> _sfxMap = new();
        private readonly Dictionary<SoundId, SoundDef> _bgmMap = new();

        private readonly List<AudioSource> _sfxPool = new();
        private int _sfxIndex;
        private bool _bgmMuted;
        private bool _sfxMuted;

        private const string KEY_BGM_VOL = "AUDIO_BGM_VOL";
        private const string KEY_SFX_VOL = "AUDIO_SFX_VOL";
        private const string KEY_MUTE = "AUDIO_MUTE";

        public float BgmVolume { get; private set; } = 1f;
        public float SfxVolume { get; private set; } = 1f;
        public bool Muted { get; private set; } = false;

        private void Awake()
        {
            BuildMaps();
            SetupSources();
            LoadSettings();
            ApplyVolumes();
        }

        private void BuildMaps()
        {
            _sfxMap.Clear();
            foreach (var s in sfxLibrary)
                if (s != null)
                    _sfxMap[s.id] = s;

            _bgmMap.Clear();
            foreach (var s in bgmLibrary)
                if (s != null)
                    _bgmMap[s.id] = s;
        }

        private void SetupSources()
        {
            if (bgmSource == null)
            {
                bgmSource = gameObject.AddComponent<AudioSource>();
                bgmSource.playOnAwake = false;
                bgmSource.loop = true;
            }

            if (sfxPrefabSource == null)
            {
                sfxPrefabSource = gameObject.AddComponent<AudioSource>();
                sfxPrefabSource.playOnAwake = false;
                sfxPrefabSource.loop = false;
                sfxPrefabSource.spatialBlend = 0f;
            }
            
            for (int i = 0; i < sfxPoolSize; i++)
            {
                var src = gameObject.AddComponent<AudioSource>();
                CopySourceSettings(sfxPrefabSource, src);
                _sfxPool.Add(src);
            }
        }

        private void CopySourceSettings(AudioSource from, AudioSource to)
        {
            to.playOnAwake = from.playOnAwake;
            to.loop = from.loop;
            to.spatialBlend = from.spatialBlend;
            to.outputAudioMixerGroup = from.outputAudioMixerGroup;
            to.dopplerLevel = from.dopplerLevel;
            to.rolloffMode = from.rolloffMode;
            to.minDistance = from.minDistance;
            to.maxDistance = from.maxDistance;
        }

        private void LoadSettings()
        {
            BgmVolume = PlayerPrefs.GetFloat(KEY_BGM_VOL, 1f);
            SfxVolume = PlayerPrefs.GetFloat(KEY_SFX_VOL, 1f);
            Muted = PlayerPrefs.GetInt(KEY_MUTE, 0) == 1;
        }

        private void SaveSettings()
        {
            PlayerPrefs.SetFloat(KEY_BGM_VOL, BgmVolume);
            PlayerPrefs.SetFloat(KEY_SFX_VOL, SfxVolume);
            PlayerPrefs.SetInt(KEY_MUTE, Muted ? 1 : 0);
            PlayerPrefs.Save();
        }

        private void ApplyVolumes()
        {
            AudioListener.volume = Muted ? 0f : 1f;

            // BGM volume thực tế = user * (sounddef volume khi play)
            bgmSource.volume = BgmVolume;
            // SFX volume set mỗi lần play (vì per-clip volume khác nhau)
        }

        // =========================
        // Public API - SETTINGS
        // =========================
        public void SetMute(bool mute)
        {
            Muted = mute;
            ApplyVolumes();
            SaveSettings();
        }

        public void SetBgmVolume(float v)
        {
            BgmVolume = Mathf.Clamp01(v);
            bgmSource.volume = BgmVolume;
            SaveSettings();
        }

        public void SetSfxVolume(float v)
        {
            SfxVolume = Mathf.Clamp01(v);
            SaveSettings();
        }
        
        public void SetBgmMuted(bool muted)
        {
            _bgmMuted = muted;
            bgmSource.mute = muted;
        }

        public void SetSfxMuted(bool muted)
        {
            _sfxMuted = muted;
        }

        // =========================
        // Public API - BGM
        // =========================
        public void PlayBgm(SoundId id, float fadeIn = 0f)
        {
            if (!_bgmMap.TryGetValue(id, out var def) || def.clip == null) return;

            StopAllCoroutines();
            bgmSource.clip = def.clip;
            bgmSource.loop = def.loop || true; // thường bgm loop
            bgmSource.pitch = def.pitch;
            bgmSource.volume = BgmVolume * def.volume;

            if (fadeIn <= 0f)
            {
                bgmSource.Play();
            }
            else
            {
                StartCoroutine(FadeInBgm(fadeIn));
            }
        }

        public void StopBgm(float fadeOut = 0f)
        {
            if (fadeOut <= 0f)
            {
                bgmSource.Stop();
                return;
            }

            StopAllCoroutines();
            StartCoroutine(FadeOutBgm(fadeOut));
        }

        public void CrossFadeBgm(SoundId next, float duration = 1f)
        {
            if (!_bgmMap.TryGetValue(next, out var def) || def.clip == null) return;
            StopAllCoroutines();
            StartCoroutine(CoCrossFade(def, duration));
        }

        private IEnumerator FadeInBgm(float t)
        {
            float target = bgmSource.volume;
            bgmSource.volume = 0f;
            bgmSource.Play();
            float time = 0f;
            while (time < t)
            {
                time += Time.unscaledDeltaTime;
                bgmSource.volume = Mathf.Lerp(0f, target, time / t);
                yield return null;
            }

            bgmSource.volume = target;
        }

        private IEnumerator FadeOutBgm(float t)
        {
            float start = bgmSource.volume;
            float time = 0f;
            while (time < t)
            {
                time += Time.unscaledDeltaTime;
                bgmSource.volume = Mathf.Lerp(start, 0f, time / t);
                yield return null;
            }

            bgmSource.Stop();
            bgmSource.volume = start;
        }

        private IEnumerator CoCrossFade(SoundDef next, float t)
        {
            // tạo source tạm để crossfade mượt
            var temp = gameObject.AddComponent<AudioSource>();
            CopySourceSettings(bgmSource, temp);

            temp.clip = next.clip;
            temp.loop = next.loop || true;
            temp.pitch = next.pitch;
            temp.volume = 0f;
            temp.Play();

            float fromStart = bgmSource.volume;
            float toTarget = BgmVolume * next.volume;

            float time = 0f;
            while (time < t)
            {
                time += Time.unscaledDeltaTime;
                float k = time / t;

                bgmSource.volume = Mathf.Lerp(fromStart, 0f, k);
                temp.volume = Mathf.Lerp(0f, toTarget, k);

                yield return null;
            }

            bgmSource.Stop();

            // swap
            bgmSource.clip = temp.clip;
            bgmSource.loop = temp.loop;
            bgmSource.pitch = temp.pitch;
            bgmSource.volume = temp.volume;
            bgmSource.Play();

            Destroy(temp);
        }

        // =========================
        // Public API - SFX (2D)
        // =========================
        public void PlaySfx(SoundId id)
        {
            if (!_sfxMap.TryGetValue(id, out var def) || def.clip == null) return;

            var src = GetNextSfxSource();
            src.spatialBlend = 0f; // 2D
            src.loop = def.loop;

            src.pitch = def.randomPitch
                ? Random.Range(def.pitchRange.x, def.pitchRange.y)
                : def.pitch;

            src.volume = SfxVolume * def.volume;

            // OneShot: không cần set clip, đỡ cắt âm khi pool reuse
            src.PlayOneShot(def.clip, 1f);
        }

        // =========================
        // Public API - SFX (3D at position)
        // =========================
        public void PlaySfxAt(SoundId id, Vector3 worldPos, float spatialBlend = 1f)
        {
            if (!_sfxMap.TryGetValue(id, out var def) || def.clip == null) return;

            // cách sạch: tạo object tạm tự huỷ
            var go = new GameObject($"SFX_{id}");
            go.transform.position = worldPos;

            var src = go.AddComponent<AudioSource>();
            CopySourceSettings(sfxPrefabSource, src);

            src.spatialBlend = Mathf.Clamp01(spatialBlend);
            src.loop = false;
            src.pitch = def.randomPitch
                ? Random.Range(def.pitchRange.x, def.pitchRange.y)
                : def.pitch;

            src.volume = SfxVolume * def.volume;

            src.PlayOneShot(def.clip, 1f);
            Destroy(go, def.clip.length / Mathf.Max(0.01f, src.pitch));
        }

        private AudioSource GetNextSfxSource()
        {
            var src = _sfxPool[_sfxIndex];
            _sfxIndex = (_sfxIndex + 1) % _sfxPool.Count;
            return src;
        }
    }
}