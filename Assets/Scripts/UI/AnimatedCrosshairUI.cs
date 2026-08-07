using Akila.FPSFramework;
using Mood.Audio;
using Mood.Weapons;
using UnityEngine;

namespace Mood.UI
{
    [AddComponentMenu("MOOD/UI/Animated Crosshair UI")]
    public sealed class AnimatedCrosshairUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerWeaponSystem weaponSystem;
        [SerializeField] private RectTransform holder;
        [SerializeField] private CanvasGroup lines;
        [SerializeField] private CanvasGroup dot;
        [SerializeField] private SfxPlayer hitmarkerSfxPlayer;

        [Header("Size")]
        [SerializeField, Min(0f)] private float spreadSizeMultiplier = 18f;
        [SerializeField, Min(0f)] private float fireKickSize = 24f;
        [SerializeField, Min(0.01f)] private float gapSmoothness = 18f;
        [SerializeField, Min(0.01f)] private float fireKickRecovery = 8f;
        [SerializeField, Range(0f, 1f)] private float aimGapMultiplier = 0.35f;

        [Header("Visibility")]
        [SerializeField] private bool hideLinesWhileAiming = true;
        [SerializeField] private bool hideAllWhenUnarmed = true;
        [SerializeField, Min(0.01f)] private float fadeSpeed = 12f;

        [Header("Audio")]
        [SerializeField] private AudioProfile headshotHitmarkerAudioProfile;

        private RectTransform topLine;
        private RectTransform bottomLine;
        private RectTransform leftLine;
        private RectTransform rightLine;
        private Vector2 topBasePosition;
        private Vector2 bottomBasePosition;
        private Vector2 leftBasePosition;
        private Vector2 rightBasePosition;
        private float currentGap;
        private float fireKick;
        private int lastAmmoInMagazine = -1;
        private Crosshair legacyCrosshair;

        private void Reset()
        {
            AssignReferences();
        }

        private void Awake()
        {
            AssignReferences();
            CacheLineReferences();
            legacyCrosshair = GetComponent<Crosshair>();
            if (legacyCrosshair != null)
            {
                legacyCrosshair.enabled = false;
            }
        }

        private void OnEnable()
        {
            if (weaponSystem != null)
            {
                weaponSystem.Fired += HandleWeaponFired;
                weaponSystem.HeadshotHit += HandleHeadshotHit;
                lastAmmoInMagazine = weaponSystem.CurrentAmmoInMagazine;
            }
        }

        private void OnDisable()
        {
            if (weaponSystem != null)
            {
                weaponSystem.Fired -= HandleWeaponFired;
                weaponSystem.HeadshotHit -= HandleHeadshotHit;
            }
        }

        private void Update()
        {
            float deltaTime = Time.deltaTime;
            fireKick = Mathf.Lerp(fireKick, 0f, 1f - Mathf.Exp(-fireKickRecovery * deltaTime));
            DetectShotFromAmmoChange();

            bool hasWeapon = weaponSystem != null && weaponSystem.CurrentWeaponData != null;
            if (!hasWeapon && hideAllWhenUnarmed)
            {
                FadeCrosshair(0f, 0f, deltaTime);
                UpdateGap(0f, deltaTime);
                return;
            }

            float targetGap = CalculateTargetGap();
            UpdateGap(targetGap, deltaTime);

            bool hideLines = weaponSystem != null && hideLinesWhileAiming && weaponSystem.CurrentAimBlend >= 0.95f;
            float linesAlpha = hideLines ? 0f : 1f;
            float dotAlpha = hasWeapon ? 1f : 0f;
            FadeCrosshair(linesAlpha, dotAlpha, deltaTime);
        }

        private void AssignReferences()
        {
            if (weaponSystem == null)
            {
                weaponSystem = FindFirstObjectByType<PlayerWeaponSystem>();
            }

            if (hitmarkerSfxPlayer == null)
            {
                hitmarkerSfxPlayer = GetComponentInChildren<SfxPlayer>(true);
            }

            if (hitmarkerSfxPlayer == null)
            {
                GameObject sfxObject = new GameObject("Hitmarker SFX Player");
                sfxObject.transform.SetParent(transform, false);
                hitmarkerSfxPlayer = sfxObject.AddComponent<SfxPlayer>();
            }

            if (holder == null)
            {
                holder = transform as RectTransform;
            }
        }

        private void CacheLineReferences()
        {
            if (holder == null)
            {
                return;
            }

            RectTransform[] rectTransforms = holder.GetComponentsInChildren<RectTransform>(true);
            for (int index = 0; index < rectTransforms.Length; index++)
            {
                RectTransform rectTransform = rectTransforms[index];
                if (rectTransform == holder)
                {
                    continue;
                }

                string objectName = rectTransform.gameObject.name;
                if (topLine == null && objectName.IndexOf("Top", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    topLine = rectTransform;
                }
                else if (bottomLine == null && objectName.IndexOf("Bottom", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    bottomLine = rectTransform;
                }
                else if (leftLine == null && objectName.IndexOf("Left", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    leftLine = rectTransform;
                }
                else if (rightLine == null && objectName.IndexOf("Right", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    rightLine = rectTransform;
                }
            }

            if (topLine != null)
            {
                topBasePosition = topLine.anchoredPosition;
            }

            if (bottomLine != null)
            {
                bottomBasePosition = bottomLine.anchoredPosition;
            }

            if (leftLine != null)
            {
                leftBasePosition = leftLine.anchoredPosition;
            }

            if (rightLine != null)
            {
                rightBasePosition = rightLine.anchoredPosition;
            }
        }

        private void DetectShotFromAmmoChange()
        {
            if (weaponSystem == null)
            {
                return;
            }

            int currentAmmo = weaponSystem.CurrentAmmoInMagazine;
            if (lastAmmoInMagazine >= 0 && currentAmmo < lastAmmoInMagazine)
            {
                fireKick += fireKickSize;
            }

            lastAmmoInMagazine = currentAmmo;
        }

        private float CalculateTargetGap()
        {
            if (weaponSystem == null || weaponSystem.CurrentWeaponData == null)
            {
                return 0f;
            }

            float spreadGap = weaponSystem.CurrentWeaponData.SpreadAngle * spreadSizeMultiplier;
            float gap = spreadGap + fireKick;
            return gap * Mathf.Lerp(1f, aimGapMultiplier, weaponSystem.CurrentAimBlend);
        }

        private void UpdateGap(float targetGap, float deltaTime)
        {
            float lerpFactor = 1f - Mathf.Exp(-gapSmoothness * deltaTime);
            currentGap = Mathf.Lerp(currentGap, targetGap, lerpFactor);
            ApplyLineGap(currentGap);
        }

        private void ApplyLineGap(float gap)
        {
            if (topLine != null)
            {
                topLine.anchoredPosition = topBasePosition + new Vector2(0f, gap);
            }

            if (bottomLine != null)
            {
                bottomLine.anchoredPosition = bottomBasePosition + new Vector2(0f, -gap);
            }

            if (leftLine != null)
            {
                leftLine.anchoredPosition = leftBasePosition + new Vector2(-gap, 0f);
            }

            if (rightLine != null)
            {
                rightLine.anchoredPosition = rightBasePosition + new Vector2(gap, 0f);
            }

            if (holder != null && topLine == null && bottomLine == null && leftLine == null && rightLine == null)
            {
                holder.sizeDelta = Vector2.one * gap;
            }
        }

        private void FadeCrosshair(float targetLinesAlpha, float targetDotAlpha, float deltaTime)
        {
            float lerpFactor = 1f - Mathf.Exp(-fadeSpeed * deltaTime);

            if (lines != null)
            {
                lines.alpha = Mathf.Lerp(lines.alpha, targetLinesAlpha, lerpFactor);
            }

            if (dot != null)
            {
                dot.alpha = Mathf.Lerp(dot.alpha, targetDotAlpha, lerpFactor);
            }
        }

        private void HandleWeaponFired(PlayerWeaponSystem _)
        {
            fireKick += fireKickSize;
        }

        private void HandleHeadshotHit(PlayerWeaponSystem _)
        {
            hitmarkerSfxPlayer?.Play(headshotHitmarkerAudioProfile);
        }
    }
}
