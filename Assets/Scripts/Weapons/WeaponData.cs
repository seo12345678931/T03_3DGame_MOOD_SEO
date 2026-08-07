using Mood.Ammo;
using Mood.Audio;
using Akila.FPSFramework;
using System;
using UnityEngine;

namespace Mood.Weapons
{
    // 무기 프리팹, 전투 수치, 애니메이션 설정을 담는 데이터 에셋이다.
    [CreateAssetMenu(fileName = "WeaponData", menuName = "MOOD/Weapons/Weapon Data")]
    public sealed class WeaponData : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string displayName = "Assault Rifle";
        [SerializeField] private GameObject weaponPrefab;
        [SerializeField] private GameObject pickupPrefab;
        [SerializeField] private Sprite weaponIcon;

        [Header("Combat")]
        [SerializeField] private bool automatic = true;
        [SerializeField, Min(0.01f)] private float fireRate = 12f;
        [SerializeField, Min(0f)] private float damage = 20f;
        [SerializeField, Min(0f)] private float range = 200f;
        [SerializeField, Min(0f)] private float impactForce = 10f;
        [SerializeField] private GameObject surfaceImpactEffectPrefab;
        [SerializeField, Min(0f)] private float surfaceImpactEffectLifetime = 2f;
        [SerializeField, Min(0.0001f)] private float surfaceImpactEffectOffset = 0.002f;
        [SerializeField, Min(1)] private int bulletsPerShot = 1;
        [SerializeField, Min(0f)] private float spreadAngle = 0.5f;
        [SerializeField] private LayerMask hitMask = ~0;
        [SerializeField] private bool automaticReload = true;

        [Header("Magazine")]
        [SerializeField] private AmmoTypeData ammoType;
        [SerializeField, Min(1)] private int magazineSize = 30;
        [SerializeField, Min(0)] private int initialReserveAmmo = 90;
        [SerializeField, Min(0.01f)] private float reloadDuration = 1.6f;
        [SerializeField] private bool useAnimationEventReload;
        [SerializeField, Min(1)] private int ammoPerReloadEvent = 1;

        [Header("Animation")]
        [SerializeField] private RuntimeAnimatorController animatorController;
        [SerializeField] private string idleStateName = "Idle";
        [SerializeField] private string fireStateName = "Fire";
        [SerializeField] private string reloadStateName = "Reload";
        [SerializeField] private string reloadStartStateName;
        [SerializeField] private string reloadLoopStateName;
        [SerializeField] private string reloadEndStateName;
        [SerializeField] private string pickupStateName = "Pickup";
        [SerializeField] private string reloadBoolParameter = "Is Reloading";
        [SerializeField] private string ammoParameter = "Ammo";
        [SerializeField] private string sprintAmountParameter = "Sprint Amount";
        [SerializeField] private string adsAmountParameter = "ADS Amount";
        [SerializeField, Min(0f)] private float fireCrossFadeDuration = 0.03f;
        [SerializeField, Min(0f)] private float reloadCrossFadeDuration = 0.08f;
        [SerializeField, Min(0f)] private float pickupCrossFadeDuration = 0.08f;

        [Header("View")]
        [SerializeField] private Vector3 viewLocalPosition;
        [SerializeField] private Vector3 viewLocalEulerAngles;

        [Header("Aim")]
        [SerializeField] private bool enableAim = true;
        [SerializeField, Min(1f)] private float aimFieldOfView = 40f;
        [SerializeField] private Vector3 aimViewLocalPosition;
        [SerializeField, Min(0.01f)] private float aimTransitionSpeed = 14f;

        [Header("Recoil")]
        [SerializeField, Min(0f)] private float cameraVerticalRecoil = 1.1f;
        [SerializeField, Min(0f)] private float cameraHorizontalRecoil = 0.4f;
        [SerializeField, Min(0.01f)] private float cameraRecoilRecovery = 18f;
        [SerializeField] private Vector3 weaponKickPosition = new Vector3(0f, 0f, -0.06f);
        [SerializeField] private Vector3 weaponKickRotation = new Vector3(-7f, 2f, 1f);
        [SerializeField, Min(0.01f)] private float weaponRecoilReturnSpeed = 18f;
        [SerializeField, Min(0.01f)] private float weaponRecoilSnappiness = 28f;

        [Header("Hierarchy")]
        [SerializeField] private string muzzleTransformName = "Muzzle";
        [SerializeField] private string ejectionPortTransformName = "Ejection Port";

        [Header("Audio")]
        [SerializeField] private SfxClipSet fireSfx;
        [SerializeField] private SfxClipSet fireTailSfx;
        [SerializeField] private SfxClipSet emptyMagazineSfx;
        [SerializeField] private SfxClipSet reloadStartSfx;
        [SerializeField] private SfxClipSet reloadInsertSfx;
        [SerializeField] private SfxClipSet reloadEndSfx;
        [SerializeField] private SfxClipSet equipSfx;
        [SerializeField] private SfxClipSet grenadeThrowSfx;
        [SerializeField] private AudioProfile fireAudioProfile;
        [SerializeField] private AudioProfile fireTailAudioProfile;

        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public GameObject WeaponPrefab => weaponPrefab;
        public GameObject PickupPrefab => pickupPrefab;
        public Sprite WeaponIcon => weaponIcon; // 03.23 UI 슬롯제어를 위해 추가함. (SEO)
        public bool Automatic => automatic;
        public float FireRate => fireRate;
        public float Damage => damage;
        public float Range => range;
        public float ImpactForce => impactForce;
        public GameObject SurfaceImpactEffectPrefab => surfaceImpactEffectPrefab;
        public float SurfaceImpactEffectLifetime => surfaceImpactEffectLifetime;
        public float SurfaceImpactEffectOffset => surfaceImpactEffectOffset;
        public int BulletsPerShot => bulletsPerShot;
        public float SpreadAngle => spreadAngle;
        public LayerMask HitMask => hitMask;
        public bool AutomaticReload => automaticReload;
        public AmmoTypeData AmmoType => ammoType;
        public int MagazineSize => magazineSize;
        public int InitialReserveAmmo => initialReserveAmmo;
        public float ReloadDuration => reloadDuration;
        public bool UseAnimationEventReload => useAnimationEventReload;
        public int AmmoPerReloadEvent => ammoPerReloadEvent;
        public RuntimeAnimatorController AnimatorController => animatorController;
        public string IdleStateName => idleStateName;
        public string FireStateName => fireStateName;
        public string ReloadStateName => reloadStateName;
        public string ReloadStartStateName => reloadStartStateName;
        public string ReloadLoopStateName => reloadLoopStateName;
        public string ReloadEndStateName => reloadEndStateName;
        public string PickupStateName => pickupStateName;
        public string ReloadBoolParameter => reloadBoolParameter;
        public string AmmoParameter => ammoParameter;
        public string SprintAmountParameter => sprintAmountParameter;
        public string AdsAmountParameter => adsAmountParameter;
        public float FireCrossFadeDuration => fireCrossFadeDuration;
        public float ReloadCrossFadeDuration => reloadCrossFadeDuration;
        public float PickupCrossFadeDuration => pickupCrossFadeDuration;
        public Vector3 ViewLocalPosition => viewLocalPosition;
        public Vector3 ViewLocalEulerAngles => viewLocalEulerAngles;
        public bool EnableAim => enableAim;
        public float AimFieldOfView => aimFieldOfView;
        public Vector3 AimViewLocalPosition => aimViewLocalPosition;
        public float AimTransitionSpeed => aimTransitionSpeed;
        public float CameraVerticalRecoil => cameraVerticalRecoil;
        public float CameraHorizontalRecoil => cameraHorizontalRecoil;
        public float CameraRecoilRecovery => cameraRecoilRecovery;
        public Vector3 WeaponKickPosition => weaponKickPosition;
        public Vector3 WeaponKickRotation => weaponKickRotation;
        public float WeaponRecoilReturnSpeed => weaponRecoilReturnSpeed;
        public float WeaponRecoilSnappiness => weaponRecoilSnappiness;
        public string MuzzleTransformName => muzzleTransformName;
        public string EjectionPortTransformName => ejectionPortTransformName;
        public SfxClipSet FireSfx => fireSfx;
        public SfxClipSet FireTailSfx => fireTailSfx;
        public SfxClipSet EmptyMagazineSfx => emptyMagazineSfx;
        public SfxClipSet ReloadStartSfx => reloadStartSfx;
        public SfxClipSet ReloadInsertSfx => reloadInsertSfx;
        public SfxClipSet ReloadEndSfx => reloadEndSfx;
        public SfxClipSet EquipSfx => equipSfx;
        public SfxClipSet GrenadeThrowSfx => grenadeThrowSfx;
        public AudioProfile FireAudioProfile => fireAudioProfile;
        public AudioProfile FireTailAudioProfile => fireTailAudioProfile;

        // 현재 프로젝트에서는 권총만 예비탄을 무한으로 처리한다.
        // 스크립트 외 자산을 건드리지 않기 위해 이름 기반으로 판별한다.
        public bool HasInfiniteReserveAmmo =>
            ContainsIgnoreCase(name, "Pistol") ||
            ContainsIgnoreCase(DisplayName, "Pistol") ||
            ContainsIgnoreCase(DisplayName, "피스톨");

        private static bool ContainsIgnoreCase(string source, string value)
        {
            return !string.IsNullOrWhiteSpace(source) &&
                   source.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
