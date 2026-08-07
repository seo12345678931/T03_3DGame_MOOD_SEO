using UnityEngine;

namespace Mood.Weapons
{
    [AddComponentMenu("MOOD/Weapons/Muzzle Flash")]
    [DisallowMultipleComponent]
    public sealed class MuzzleFlash : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private ParticleSystem[] muzzleEffects;

        private void Awake()
        {
            CacheEffectsIfEmpty();
        }

        public void Play()
        {
            CacheEffectsIfEmpty();

            for (int effectIndex = 0; effectIndex < muzzleEffects.Length; effectIndex++)
            {
                ParticleSystem muzzleEffect = muzzleEffects[effectIndex];
                if (muzzleEffect == null)
                {
                    continue;
                }

                muzzleEffect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                muzzleEffect.Play(true);
            }
        }

        private void CacheEffectsIfEmpty()
        {
            if (muzzleEffects != null && muzzleEffects.Length > 0)
            {
                return;
            }

            WeaponView weaponView = GetComponentInParent<WeaponView>();
            Transform muzzleTransform = weaponView != null ? weaponView.Muzzle : transform;
            muzzleEffects = muzzleTransform != null
                ? muzzleTransform.GetComponentsInChildren<ParticleSystem>(true)
                : GetComponentsInChildren<ParticleSystem>(true);
        }
    }
}
