using System.Collections;
using UnityEngine;

namespace Mood.Effects
{
    public sealed class PooledEffectInstance : MonoBehaviour
    {
        [SerializeField] private ParticleSystem[] particleSystems;

        private Coroutine releaseCoroutine;

        internal int PoolKey { get; private set; }

        private void Awake()
        {
            CacheParticleSystemsIfNeeded();
        }

        internal void SetPoolKey(int poolKey)
        {
            PoolKey = poolKey;
        }

        internal void PrepareForPool(Transform parent)
        {
            if (releaseCoroutine != null)
            {
                StopCoroutine(releaseCoroutine);
                releaseCoroutine = null;
            }

            CacheParticleSystemsIfNeeded();

            for (int index = 0; index < particleSystems.Length; index++)
            {
                ParticleSystem effect = particleSystems[index];
                if (effect == null)
                {
                    continue;
                }

                effect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }

            transform.SetParent(parent, false);
            gameObject.SetActive(false);
        }

        internal void Play(Vector3 position, Quaternion rotation, float lifetime)
        {
            if (releaseCoroutine != null)
            {
                StopCoroutine(releaseCoroutine);
                releaseCoroutine = null;
            }

            CacheParticleSystemsIfNeeded();

            transform.SetPositionAndRotation(position, rotation);
            gameObject.SetActive(true);

            for (int index = 0; index < particleSystems.Length; index++)
            {
                ParticleSystem effect = particleSystems[index];
                if (effect == null)
                {
                    continue;
                }

                effect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                effect.Play(true);
            }

            if (lifetime > 0f)
            {
                releaseCoroutine = StartCoroutine(ReturnAfterDelay(lifetime));
            }
        }

        public void ReturnToPool()
        {
            EffectPool.Release(this);
        }

        private IEnumerator ReturnAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            releaseCoroutine = null;
            EffectPool.Release(this);
        }

        private void CacheParticleSystemsIfNeeded()
        {
            if (particleSystems != null && particleSystems.Length > 0)
            {
                return;
            }

            particleSystems = GetComponentsInChildren<ParticleSystem>(true);
        }
    }
}
