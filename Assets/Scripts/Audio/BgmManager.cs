using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

namespace Mood.Audio
{
    [AddComponentMenu("MOOD/Audio/BGM Manager")]
    [DisallowMultipleComponent]
    public sealed class BgmManager : MonoBehaviour
    {
        [System.Serializable]
        private sealed class BgmEntry
        {
            [Tooltip("씬 자동 재생에 사용할 씬 이름. 비워두면 Track Key로만 수동 재생한다.")]
            public string SceneName;
            [Tooltip("맵/상황 전환 시 코드에서 호출할 식별자.")]
            public string TrackKey;
            [Tooltip("재생할 배경음악 클립.")]
            public AudioClip Clip;
            [Tooltip("개별 트랙 볼륨 배율.")]
            [Range(0f, 1f)] public float Volume = 1f;
            [Tooltip("기본값은 루프 재생.")]
            public bool Loop = true;
        }

        private const string MusicVolumeKey = "MusicVolume";

        [Header("Track Library")]
        [Tooltip("씬 이름 또는 Track Key 기준으로 재생할 배경음악 목록.")]
        [SerializeField] private BgmEntry[] bgmEntries;
        [Tooltip("현재 씬에 매칭되는 음악이 없을 때 사용할 기본 배경음악.")]
        [SerializeField] private AudioClip defaultClip;
        [SerializeField, Range(0f, 1f)] private float defaultClipVolume = 1f;
        [SerializeField] private bool defaultClipLoop = true;

        [Header("Audio")]
        [SerializeField] private AudioMixerGroup outputMixerGroup;
        [SerializeField, Range(0f, 1f)] private float masterVolume = 1f;
        [SerializeField, Min(0f)] private float fadeDuration = 0.75f;
        [SerializeField] private bool playOnStart = true;

        private static BgmManager instance;

        private readonly Dictionary<string, BgmEntry> sceneEntryLookup = new Dictionary<string, BgmEntry>();
        private readonly Dictionary<string, BgmEntry> trackEntryLookup = new Dictionary<string, BgmEntry>();

        private AudioSource primarySource;
        private AudioSource secondarySource;
        private Coroutine transitionCoroutine;
        private AudioSource activeSource;
        private string currentTrackKey = string.Empty;
        private AudioClip currentClip;
        private float primaryBaseVolume = 1f;
        private float secondaryBaseVolume = 1f;

        public static BgmManager Instance => instance;
        public float CurrentVolume => masterVolume;
        public string CurrentTrackKey => currentTrackKey;

        private void Reset()
        {
            EnsureAudioSources();
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);

            LoadSavedVolume();
            RebuildLookup();
            EnsureAudioSources();
            ApplyVolumeToAllSources();
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private void Start()
        {
            if (!playOnStart)
            {
                return;
            }

            PlayForScene(SceneManager.GetActiveScene().name, true);
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }

        public void SetMusicVolume(float normalizedVolume)
        {
            masterVolume = Mathf.Clamp01(normalizedVolume);
            PlayerPrefs.SetFloat(MusicVolumeKey, masterVolume);
            PlayerPrefs.Save();
            ApplyVolumeToAllSources();
        }

        public bool PlayForScene(string sceneName, bool immediate = false)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                return PlayDefault(immediate);
            }

            if (sceneEntryLookup.TryGetValue(sceneName, out BgmEntry entry))
            {
                return PlayEntry(entry, immediate);
            }

            return PlayDefault(immediate);
        }

        public bool PlayByKey(string trackKey, bool immediate = false)
        {
            if (string.IsNullOrWhiteSpace(trackKey))
            {
                return false;
            }

            if (!trackEntryLookup.TryGetValue(trackKey, out BgmEntry entry))
            {
                Debug.LogWarning($"[BgmManager:{name}] No BGM entry found for key '{trackKey}'.", this);
                return false;
            }

            return PlayEntry(entry, immediate);
        }

        public bool StopMusic(bool immediate = false)
        {
            if (transitionCoroutine != null)
            {
                StopCoroutine(transitionCoroutine);
                transitionCoroutine = null;
            }

            if (activeSource == null)
            {
                return false;
            }

            if (immediate || fadeDuration <= 0f)
            {
                primarySource.Stop();
                secondarySource.Stop();
                primarySource.clip = null;
                secondarySource.clip = null;
                currentClip = null;
                currentTrackKey = string.Empty;
                return true;
            }

            transitionCoroutine = StartCoroutine(FadeOutAndStopRoutine(activeSource, fadeDuration));
            return true;
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode _)
        {
            PlayForScene(scene.name);
        }

        private bool PlayDefault(bool immediate = false)
        {
            if (defaultClip == null)
            {
                StopMusic(immediate);
                return false;
            }

            return PlayClip(defaultClip, string.Empty, defaultClipVolume, defaultClipLoop, immediate);
        }

        private bool PlayEntry(BgmEntry entry, bool immediate)
        {
            if (entry == null || entry.Clip == null)
            {
                return PlayDefault(immediate);
            }

            string resolvedTrackKey = string.IsNullOrWhiteSpace(entry.TrackKey) ? string.Empty : entry.TrackKey;
            return PlayClip(entry.Clip, resolvedTrackKey, entry.Volume, entry.Loop, immediate);
        }

        private bool PlayClip(AudioClip clip, string trackKey, float clipVolume, bool loop, bool immediate)
        {
            if (clip == null)
            {
                return false;
            }

            // 이미 같은 트랙이 재생 중이면 불필요한 페이드를 막기 위해 그대로 유지한다.
            if (currentClip == clip && activeSource != null && activeSource.isPlaying)
            {
                currentTrackKey = trackKey;
                activeSource.loop = loop;
                activeSource.volume = ResolveFinalVolume(clipVolume);
                return true;
            }

            if (transitionCoroutine != null)
            {
                StopCoroutine(transitionCoroutine);
                transitionCoroutine = null;
            }

            AudioSource nextSource = activeSource == primarySource ? secondarySource : primarySource;
            PrepareSource(nextSource, clip, ResolveFinalVolume(clipVolume), loop);

            currentClip = clip;
            currentTrackKey = trackKey;

            if (immediate || activeSource == null || fadeDuration <= 0f || !activeSource.isPlaying)
            {
                if (activeSource != null && activeSource != nextSource)
                {
                    activeSource.Stop();
                }

                nextSource.volume = ResolveFinalVolume(clipVolume);
                nextSource.Play();
                activeSource = nextSource;
                return true;
            }

            transitionCoroutine = StartCoroutine(CrossFadeRoutine(activeSource, nextSource, ResolveFinalVolume(clipVolume), fadeDuration));
            return true;
        }

        private IEnumerator CrossFadeRoutine(AudioSource fromSource, AudioSource toSource, float targetVolume, float duration)
        {
            float elapsed = 0f;
            float fromStartVolume = fromSource != null ? fromSource.volume : 0f;

            toSource.volume = 0f;
            toSource.Play();

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float normalizedTime = Mathf.Clamp01(elapsed / duration);

                if (fromSource != null)
                {
                    fromSource.volume = Mathf.Lerp(fromStartVolume, 0f, normalizedTime);
                }

                toSource.volume = Mathf.Lerp(0f, targetVolume, normalizedTime);
                yield return null;
            }

            if (fromSource != null)
            {
                fromSource.Stop();
                fromSource.volume = 0f;
            }

            toSource.volume = targetVolume;
            activeSource = toSource;
            transitionCoroutine = null;
        }

        private IEnumerator FadeOutAndStopRoutine(AudioSource source, float duration)
        {
            if (source == null)
            {
                yield break;
            }

            float elapsed = 0f;
            float startVolume = source.volume;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float normalizedTime = Mathf.Clamp01(elapsed / duration);
                source.volume = Mathf.Lerp(startVolume, 0f, normalizedTime);
                yield return null;
            }

            source.Stop();
            source.volume = 0f;
            source.clip = null;

            currentClip = null;
            currentTrackKey = string.Empty;
            transitionCoroutine = null;
        }

        private void EnsureAudioSources()
        {
            AudioSource[] sources = GetComponents<AudioSource>();
            if (sources.Length >= 2)
            {
                primarySource = sources[0];
                secondarySource = sources[1];
            }
            else
            {
                primarySource = EnsureSource(primarySource);
                secondarySource = EnsureSource(secondarySource, primarySource);
            }

            ConfigureSource(primarySource);
            ConfigureSource(secondarySource);

            if (activeSource == null)
            {
                activeSource = primarySource;
            }
        }

        private AudioSource EnsureSource(AudioSource currentSource, AudioSource otherSource = null)
        {
            if (currentSource != null)
            {
                return currentSource;
            }

            AudioSource createdSource = gameObject.AddComponent<AudioSource>();
            if (otherSource != null && createdSource == otherSource)
            {
                createdSource = gameObject.AddComponent<AudioSource>();
            }

            return createdSource;
        }

        private void ConfigureSource(AudioSource targetSource)
        {
            if (targetSource == null)
            {
                return;
            }

            targetSource.playOnAwake = false;
            targetSource.loop = true;
            targetSource.spatialBlend = 0f;
            targetSource.outputAudioMixerGroup = outputMixerGroup;
        }

        private void PrepareSource(AudioSource targetSource, AudioClip clip, float volume, bool loop)
        {
            if (targetSource == null)
            {
                return;
            }

            ConfigureSource(targetSource);
            targetSource.clip = clip;
            targetSource.loop = loop;
            targetSource.volume = volume;

            if (targetSource == primarySource)
            {
                primaryBaseVolume = Mathf.Clamp01(volume / Mathf.Max(masterVolume, 0.0001f));
            }
            else if (targetSource == secondarySource)
            {
                secondaryBaseVolume = Mathf.Clamp01(volume / Mathf.Max(masterVolume, 0.0001f));
            }
        }

        private void RebuildLookup()
        {
            sceneEntryLookup.Clear();
            trackEntryLookup.Clear();

            if (bgmEntries == null)
            {
                return;
            }

            for (int index = 0; index < bgmEntries.Length; index++)
            {
                BgmEntry entry = bgmEntries[index];
                if (entry == null || entry.Clip == null)
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(entry.SceneName) && !sceneEntryLookup.ContainsKey(entry.SceneName))
                {
                    sceneEntryLookup.Add(entry.SceneName, entry);
                }

                if (!string.IsNullOrWhiteSpace(entry.TrackKey) && !trackEntryLookup.ContainsKey(entry.TrackKey))
                {
                    trackEntryLookup.Add(entry.TrackKey, entry);
                }
            }
        }

        private void LoadSavedVolume()
        {
            masterVolume = PlayerPrefs.GetFloat(MusicVolumeKey, masterVolume);
            masterVolume = Mathf.Clamp01(masterVolume);
        }

        private void ApplyVolumeToAllSources()
        {
            if (primarySource != null && primarySource.clip != null)
            {
                primarySource.volume = ResolveFinalVolume(primaryBaseVolume);
            }

            if (secondarySource != null && secondarySource.clip != null)
            {
                secondarySource.volume = ResolveFinalVolume(secondaryBaseVolume);
            }
        }

        private float ResolveFinalVolume(float clipVolume)
        {
            return Mathf.Clamp01(clipVolume) * masterVolume;
        }
    }
}
