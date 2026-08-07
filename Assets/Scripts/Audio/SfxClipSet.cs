using System;
using UnityEngine;

namespace Mood.Audio
{
    [Serializable]
    public sealed class SfxClipSet
    {
        [SerializeField] private AudioClip[] clips;
        [SerializeField, Min(0f)] private float volume = 1f;
        [SerializeField] private Vector2 pitchRange = Vector2.one;

        public bool IsConfigured
        {
            get
            {
                if (clips == null || clips.Length == 0)
                {
                    return false;
                }

                for (int index = 0; index < clips.Length; index++)
                {
                    if (clips[index] != null)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        public float Volume => Mathf.Max(0f, volume);

        public float GetRandomPitch()
        {
            float minPitch = Mathf.Max(0.01f, Mathf.Min(pitchRange.x, pitchRange.y));
            float maxPitch = Mathf.Max(minPitch, Mathf.Max(pitchRange.x, pitchRange.y));
            return UnityEngine.Random.Range(minPitch, maxPitch);
        }

        public bool TryGetRandomClip(out AudioClip clip)
        {
            clip = null;
            if (!IsConfigured)
            {
                return false;
            }

            int validClipCount = 0;
            for (int index = 0; index < clips.Length; index++)
            {
                if (clips[index] != null)
                {
                    validClipCount++;
                }
            }

            if (validClipCount == 0)
            {
                return false;
            }

            int randomIndex = UnityEngine.Random.Range(0, validClipCount);
            for (int index = 0; index < clips.Length; index++)
            {
                if (clips[index] == null)
                {
                    continue;
                }

                if (randomIndex == 0)
                {
                    clip = clips[index];
                    return true;
                }

                randomIndex--;
            }

            return false;
        }
    }
}
