using UnityEngine;
using Akila.FPSFramework;

namespace Mood.Audio
{
    [AddComponentMenu("MOOD/Audio/SFX Player")]
    [DisallowMultipleComponent]
    public sealed class SfxPlayer : MonoBehaviour
    {
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private bool playAs2D = true;

        private void Reset()
        {
            audioSource = GetComponent<AudioSource>();
            ConfigureAudioSource(EnsureAudioSource());
        }

        private void Awake()
        {
            ConfigureAudioSource(EnsureAudioSource());
        }

        public bool Play(SfxClipSet clipSet)
        {
            if (clipSet == null || !clipSet.TryGetRandomClip(out AudioClip clip))
            {
                return false;
            }

            AudioSource targetSource = EnsureAudioSource();
            if (targetSource == null)
            {
                return false;
            }

            ConfigureAudioSource(targetSource);
            targetSource.pitch = clipSet.GetRandomPitch();
            targetSource.PlayOneShot(clip, clipSet.Volume);
            return true;
        }

        public bool Play(AudioProfile audioProfile)
        {
            if (audioProfile == null || audioProfile.clip == null)
            {
                return false;
            }

            AudioSource targetSource = EnsureAudioSource();
            if (targetSource == null)
            {
                return false;
            }

            ApplyAudioProfile(targetSource, audioProfile);
            targetSource.pitch = GetRandomizedProfilePitch(audioProfile);
            targetSource.PlayOneShot(audioProfile.clip);
            return true;
        }

        private AudioSource EnsureAudioSource()
        {
            if (audioSource != null)
            {
                return audioSource;
            }

            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }

            return audioSource;
        }

        private void ConfigureAudioSource(AudioSource targetSource)
        {
            if (targetSource == null)
            {
                return;
            }

            // 효과음 전용 소스이므로 루프와 자동 재생을 막고 즉시 재생만 담당한다.
            targetSource.playOnAwake = false;
            targetSource.loop = false;
            targetSource.spatialBlend = playAs2D ? 0f : 1f;
        }

        private static void ApplyAudioProfile(AudioSource targetSource, AudioProfile audioProfile)
        {
            targetSource.clip = audioProfile.clip;
            targetSource.outputAudioMixerGroup = audioProfile.output;
            targetSource.mute = audioProfile.mute;
            targetSource.bypassEffects = audioProfile.bypassEffects;
            targetSource.bypassListenerEffects = audioProfile.bypassListenerEffects;
            targetSource.bypassReverbZones = audioProfile.bypassReverbZones;
            targetSource.playOnAwake = false;
            targetSource.loop = false;

            targetSource.priority = audioProfile.priority;
            targetSource.volume = audioProfile.volume;
            targetSource.pitch = audioProfile.pitch;
            targetSource.panStereo = audioProfile.stereoPan;
            targetSource.spatialBlend = audioProfile.spatialBlend;
            targetSource.reverbZoneMix = audioProfile.reverbZoneMix;

            targetSource.dopplerLevel = audioProfile.dopplerLevel;
            targetSource.spread = audioProfile.spread;
            targetSource.rolloffMode = audioProfile.volumeRolloff;
            targetSource.minDistance = audioProfile.minDistance;
            targetSource.maxDistance = audioProfile.maxDistance;
        }

        private static float GetRandomizedProfilePitch(AudioProfile audioProfile)
        {
            if (!audioProfile.dymaicPitch)
            {
                return audioProfile.pitch;
            }

            float minPitch = Time.timeScale * audioProfile.pitch;
            float maxPitch = (Time.timeScale + audioProfile.pitchFactor) * audioProfile.pitch;
            return Random.Range(minPitch, maxPitch);
        }
    }
}
