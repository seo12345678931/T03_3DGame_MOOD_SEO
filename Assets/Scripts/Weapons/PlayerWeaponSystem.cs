using System.Collections;
using System.Collections.Generic;
using Mood.Ammo;
using Mood.Audio;
using Mood.Combat;
using Mood.Effects;
using Mood.Input;
using Mood.Utils;
using Unity.Cinemachine;
using UnityEngine;

namespace Mood.Weapons
{
    [AddComponentMenu("MOOD/Weapons/Player Weapon System")]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(InputManager))]
    public sealed class PlayerWeaponSystem : MonoBehaviour, IAmmoReceiver
    {
        [System.Serializable]
        private sealed class WeaponSlotState
        {
            public WeaponData WeaponData;
            public int AmmoInMagazine;
            public int ReserveAmmo;

            public bool HasWeapon => WeaponData != null;

            public void Set(WeaponData weaponData, int ammoInMagazine, int reserveAmmo)
            {
                WeaponData = weaponData;
                AmmoInMagazine = Mathf.Max(0, ammoInMagazine);
                ReserveAmmo = Mathf.Max(0, reserveAmmo);
            }
        }

        [Header("References")]
        [SerializeField] private Camera aimCamera;
        [SerializeField] private Transform weaponHolder;
        [SerializeField] private InputManager inputManager;
        [SerializeField] private CinemachinePanTilt panTilt;
        [SerializeField] private CinemachineCamera aimCinemachineCamera;
        [SerializeField] private ScreenCenterArcThrower grenadeThrower;
        [SerializeField] private GrenadeThrowView grenadeThrowView;

        [Header("Inventory")]
        [SerializeField, Min(1)] private int maxWeaponSlots = 5;
        [SerializeField] private WeaponData startingWeapon;
        [SerializeField] private bool equipStartingWeaponOnStart = true;
        [SerializeField] private bool autoEquipPickedWeapon = true;

        [Header("Grenade")]
        [SerializeField] private AmmoTypeData grenadeAmmoType;
        // 게임 시작 시 플레이어가 기본으로 들고 시작하는 수류탄 개수.
        [SerializeField, Min(0)] private int startingGrenadeCount = 2;
        [SerializeField, Min(1)] private int maxGrenadeCount = 3;

        [Header("Pickup")]
        [SerializeField, Min(0.1f)] private float interactDistance = 3f;
        [SerializeField] private LayerMask pickupMask = ~0;

        [Header("Audio")]
        [SerializeField] private SfxPlayer weaponSfxPlayer;
        [SerializeField] private SfxPlayer weaponWorldSfxPlayer;

        private WeaponSlotState[] weaponSlots;
        private WeaponData currentWeaponData;
        private WeaponView currentWeaponView;
        private ScreenCenterProjectileShooter currentProjectileShooter;
        private MuzzleFlash currentMuzzleFlash;
        private WeaponPickup currentPickupTarget;
        private int currentSlotIndex = -1;
        private int currentAmmoInMagazine;
        private int currentReserveAmmo;
        private float nextFireTime;
        private bool isReloading;
        private bool isAnimationEventReloading;
        private Coroutine reloadCoroutine;

        private Vector2 cameraRecoilTargetOffset;
        private Vector2 appliedCameraRecoilOffset;
        private Vector3 weaponRecoilTargetPosition;
        private Vector3 weaponRecoilCurrentPosition;
        private Vector3 weaponRecoilTargetRotation;
        private Vector3 weaponRecoilCurrentRotation;
        private Vector3 defaultWeaponLocalPosition;
        private Quaternion defaultWeaponLocalRotation;
        private float defaultCameraFieldOfView;
        private float currentAimBlend;
        private bool isAiming;
        private bool isThrowingGrenade;
        private bool hasReleasedGrenadeThisCycle;
        private int currentGrenadeCount;
        private float nextEmptyMagazineSfxTime;

        private const float EmptyMagazineSfxCooldown = 0.12f;

        public event System.Action<PlayerWeaponSystem> AmmoChanged;
        public event System.Action<PlayerWeaponSystem> GrenadeCountChanged;
        public event System.Action<PlayerWeaponSystem> WeaponChanged;
        public event System.Action<PlayerWeaponSystem> Fired;
        public event System.Action<PlayerWeaponSystem> HeadshotHit;
        public event System.Action<GameObject, IReadOnlyDictionary<AmmoTypeData, int>> AmmoReceived;

        public Component Component => this;
        public WeaponData CurrentWeaponData => currentWeaponData;
        public int CurrentSlotIndex => currentSlotIndex;
        public int CurrentAmmoInMagazine => currentAmmoInMagazine;
        public int CurrentReserveAmmo => currentReserveAmmo;
        public AmmoTypeData GrenadeAmmoType => grenadeAmmoType;
        public int CurrentGrenadeCount => currentGrenadeCount;
        public int MaxGrenadeCount => Mathf.Max(1, maxGrenadeCount);
        public bool CurrentWeaponHasInfiniteReserveAmmo => currentWeaponData != null && currentWeaponData.HasInfiniteReserveAmmo;
        public bool IsReloading => isReloading;
        public bool IsAiming => isAiming;
        public float CurrentAimBlend => currentAimBlend;
        public int WeaponSlotCount => weaponSlots != null ? weaponSlots.Length : Mathf.Max(1, maxWeaponSlots);
        public WeaponPickup CurrentPickupTarget => currentPickupTarget;

        // WeaponData??weaponIcon????? ???????????????? ??????(SEO)
        public WeaponData GetWeaponDataInSlot(int slotIndex)
        {
            if (!IsValidSlotIndex(slotIndex))
            {
                return null;
            }

            return weaponSlots[slotIndex].WeaponData;
        }

        private void Reset()
        {
            AssignReferences();
        }

        private void Awake()
        {
            InitializeSlots();
            AssignReferences();
            currentGrenadeCount = Mathf.Clamp(startingGrenadeCount, 0, MaxGrenadeCount);
        }

        private void Start()
        {
            if (!equipStartingWeaponOnStart || startingWeapon == null)
            {
                return;
            }

            int startingSlotIndex = StoreWeaponInFirstEmptySlot(startingWeapon, startingWeapon.MagazineSize, startingWeapon.InitialReserveAmmo);
            if (startingSlotIndex >= 0)
            {
                EquipSlot(startingSlotIndex, false);
            }
        }

        private void Update()
        {
            if (inputManager == null)
            {
                return;
            }

            HandleWeaponSelectionInput();
            ResolvePickupTarget();
            HandlePickupInput();
            HandleReloadInput();
            HandleThrowableInput();
            if (isThrowingGrenade)
            {
                UpdateAnimatorParameters();
                return;
            }

            HandleAimInput();
            HandleFireInput();
            UpdateAnimatorParameters();
        }

        private void LateUpdate()
        {
            UpdateAimPresentation(Time.deltaTime);
            UpdateRecoil(Time.deltaTime);
        }

        public string GetInteractionText()
        {
            if (currentPickupTarget == null)
            {
                return string.Empty;
            }

            return CanPickup(currentPickupTarget) ? currentPickupTarget.GetInteractionText() : "Inventory Full";
        }

        public bool TryPickup(WeaponPickup pickup)
        {
            if (pickup == null || pickup.WeaponData == null)
            {
                return false;
            }

            int existingSlotIndex = FindWeaponSlotIndex(pickup.WeaponData);
            if (existingSlotIndex >= 0)
            {
                AddPickupAmmoToSlot(existingSlotIndex, pickup);
                Destroy(pickup.gameObject);
                currentPickupTarget = null;
                NotifyWeaponChanged();
                return true;
            }

            int emptySlotIndex = FindFirstEmptySlotIndex();
            if (emptySlotIndex < 0)
            {
                return false;
            }

            SetSlot(emptySlotIndex, pickup.WeaponData, pickup.AmmoInMagazine, pickup.ReserveAmmo);
            Destroy(pickup.gameObject);
            currentPickupTarget = null;

            if (currentSlotIndex < 0 || autoEquipPickedWeapon)
            {
                EquipSlot(emptySlotIndex, true);
            }
            else
            {
                NotifyAmmoChanged();
                NotifyWeaponChanged();
            }

            return true;
        }

        public bool CanReceiveAmmo(AmmoTypeData ammoType, int amount)
        {
            if (ammoType == null || amount <= 0)
            {
                return false;
            }

            if (ammoType == grenadeAmmoType)
            {
                return currentGrenadeCount < MaxGrenadeCount;
            }

            if (weaponSlots == null)
            {
                return false;
            }

            for (int slotIndex = 0; slotIndex < weaponSlots.Length; slotIndex++)
            {
                WeaponSlotState slotState = weaponSlots[slotIndex];
                if (!UsesAmmoType(slotState, ammoType))
                {
                    continue;
                }

                if (slotState.WeaponData != null && slotState.WeaponData.HasInfiniteReserveAmmo)
                {
                    continue;
                }

                if (GetReserveAmmoForSlot(slotIndex) < ammoType.MaxReserveAmmo)
                {
                    return true;
                }
            }

            return false;
        }

        public int ReceiveAmmo(AmmoTypeData ammoType, int amount, GameObject source)
        {
            if (ammoType == null || amount <= 0)
            {
                return 0;
            }

            if (ammoType == grenadeAmmoType)
            {
                int grenadeSpace = Mathf.Max(0, MaxGrenadeCount - currentGrenadeCount);
                int grenadesToAdd = Mathf.Min(amount, grenadeSpace);
                if (grenadesToAdd <= 0)
                {
                    return 0;
                }

                currentGrenadeCount += grenadesToAdd;
                NotifyGrenadeCountChanged();
                return grenadesToAdd;
            }

            if (weaponSlots == null)
            {
                return 0;
            }

            int remainingAmount = amount;
            int totalReceived = 0;
            for (int slotIndex = 0; slotIndex < weaponSlots.Length && remainingAmount > 0; slotIndex++)
            {
                WeaponSlotState slotState = weaponSlots[slotIndex];
                if (!UsesAmmoType(slotState, ammoType))
                {
                    continue;
                }

                if (slotState.WeaponData != null && slotState.WeaponData.HasInfiniteReserveAmmo)
                {
                    continue;
                }

                int currentReserve = GetReserveAmmoForSlot(slotIndex);
                int ammoSpace = Mathf.Max(0, ammoType.MaxReserveAmmo - currentReserve);
                if (ammoSpace <= 0)
                {
                    continue;
                }

                int ammoToAdd = Mathf.Min(remainingAmount, ammoSpace);
                currentReserve += ammoToAdd;
                slotState.ReserveAmmo = currentReserve;

                if (slotIndex == currentSlotIndex)
                {
                    currentReserveAmmo = currentReserve;
                }

                remainingAmount -= ammoToAdd;
                totalReceived += ammoToAdd;
            }

            if (totalReceived > 0)
            {
                SaveCurrentWeaponStateToSlot();
                NotifyAmmoChanged();
            }

            return totalReceived;
        }

        public void NotifyAmmoReceived(GameObject source, IReadOnlyDictionary<AmmoTypeData, int> receivedAmmo)
        {
            if (source == null || receivedAmmo == null || receivedAmmo.Count == 0)
            {
                return;
            }

            AmmoReceived?.Invoke(source, receivedAmmo);
        }

        private void InitializeSlots()
        {
            int slotCount = Mathf.Max(1, maxWeaponSlots);
            if (weaponSlots != null && weaponSlots.Length == slotCount)
            {
                return;
            }
            
            weaponSlots = new WeaponSlotState[slotCount];
            for (int slotIndex = 0; slotIndex < weaponSlots.Length; slotIndex++)
            {
                weaponSlots[slotIndex] = new WeaponSlotState();
            }
        }

        private void AssignReferences()
        {
            if (aimCamera == null)
            {
                aimCamera = Camera.main;
            }

            if (weaponHolder == null)
            {
                weaponHolder = aimCamera != null ? aimCamera.transform : transform;
            }

            if (inputManager == null)
            {
                inputManager = GetComponent<InputManager>();
            }

            if (panTilt == null)
            {
                panTilt = FindFirstObjectByType<CinemachinePanTilt>();
            }

            if (aimCinemachineCamera == null && panTilt != null)
            {
                aimCinemachineCamera = panTilt.GetComponent<CinemachineCamera>();
            }

            if (aimCinemachineCamera == null)
            {
                aimCinemachineCamera = FindFirstObjectByType<CinemachineCamera>();
            }

            if (defaultCameraFieldOfView <= 0f)
            {
                defaultCameraFieldOfView = GetCurrentCameraFieldOfView();
            }

            if (grenadeThrowView == null)
            {
                grenadeThrowView = GetComponentInChildren<GrenadeThrowView>(true);
            }

            if (weaponSfxPlayer == null)
            {
                weaponSfxPlayer = GetComponentInChildren<SfxPlayer>(true);
            }

            if (weaponSfxPlayer == null)
            {
                GameObject sfxObject = new GameObject("Weapon SFX Player");
                sfxObject.transform.SetParent(transform, false);
                weaponSfxPlayer = sfxObject.AddComponent<SfxPlayer>();
            }

            grenadeThrowView?.HideImmediate();
        }

        private void HandleWeaponSelectionInput()
        {
            int selectedSlotIndex = inputManager.WeaponSlotPressed;
            if (selectedSlotIndex >= 0)
            {
                EquipSlot(selectedSlotIndex, true);
                return;
            }

            if (inputManager.NextPressed)
            {
                CycleSlot(1);
                return;
            }

            if (inputManager.PreviousPressed)
            {
                CycleSlot(-1);
            }
        }

        private void ResolvePickupTarget()
        {
            currentPickupTarget = null;

            if (aimCamera == null)
            {
                return;
            }

            // ??????????????????? ????????????????????????????? ????????????????
            Ray ray = new Ray(aimCamera.transform.position, aimCamera.transform.forward);
            if (!Physics.Raycast(ray, out RaycastHit hit, interactDistance, pickupMask, QueryTriggerInteraction.Collide))
            {
                return;
            }

            currentPickupTarget = hit.collider.GetComponentInParent<WeaponPickup>();
        }

        private void HandlePickupInput()
        {
            if (!inputManager.InteractPressed || currentPickupTarget == null)
            {
                return;
            }

            TryPickup(currentPickupTarget);
        }

        private void HandleReloadInput()
        {
            if (!inputManager.ReloadPressed)
            {
                return;
            }

            TryStartReload();
        }
        private void HandleThrowableInput()
        {
            if (!inputManager.ThrowPressed || isThrowingGrenade || isReloading || currentGrenadeCount <= 0)
            {
                return;
            }

            ScreenCenterArcThrower activeThrower = GetActiveGrenadeThrower();
            if (activeThrower == null)
            {
                return;
            }

            if (grenadeThrowView == null)
            {
                if (activeThrower.TryThrow(gameObject))
                {
                    ConsumeGrenade();
                    PlayCurrentWeaponSfx(currentWeaponData != null ? currentWeaponData.GrenadeThrowSfx : null);
                }

                return;
            }

            BeginGrenadeThrow();
        }

        private ScreenCenterArcThrower GetActiveGrenadeThrower()
        {
            if (grenadeThrower != null)
            {
                return grenadeThrower;
            }

            if (currentWeaponView != null)
            {
                grenadeThrower = currentWeaponView.GetComponentInChildren<ScreenCenterArcThrower>(true);
                if (grenadeThrower != null)
                {
                    return grenadeThrower;
                }
            }

            grenadeThrower = GetComponentInChildren<ScreenCenterArcThrower>(true);
            return grenadeThrower;
        }


        private void BeginGrenadeThrow()
        {
            isThrowingGrenade = true;
            hasReleasedGrenadeThisCycle = false;
            isAiming = false;
            currentAimBlend = 0f;
            ApplyAimFieldOfView(defaultCameraFieldOfView);

            if (currentWeaponView != null)
            {
                currentWeaponView.gameObject.SetActive(false);
            }

            grenadeThrowView.BeginThrow(this);
        }

        public void OnGrenadeThrowAnimationEventRelease()
        {
            if (!isThrowingGrenade || hasReleasedGrenadeThisCycle)
            {
                return;
            }

            ScreenCenterArcThrower activeThrower = GetActiveGrenadeThrower();
            if (activeThrower == null)
            {
                return;
            }

            hasReleasedGrenadeThisCycle = activeThrower.TryThrow(gameObject);
            if (hasReleasedGrenadeThisCycle)
            {
                ConsumeGrenade();
                PlayCurrentWeaponSfx(currentWeaponData != null ? currentWeaponData.GrenadeThrowSfx : null);
            }
        }

        public void OnGrenadeThrowAnimationFinished()
        {
            if (!isThrowingGrenade)
            {
                return;
            }

            isThrowingGrenade = false;
            hasReleasedGrenadeThisCycle = false;

            if (currentWeaponView != null)
            {
                currentWeaponView.gameObject.SetActive(true);
            }

            grenadeThrowView?.HideImmediate();
        }
        private void HandleAimInput()
        {
            isAiming = CanAim() && inputManager.AimHeld && !isReloading;
        }

        private void HandleFireInput()
        {
            if (currentWeaponData == null || currentWeaponView == null)
            {
                return;
            }

            bool wantsToFire = currentWeaponData.Automatic ? inputManager.FireHeld : inputManager.FirePressed;
            if (!wantsToFire)
            {
                return;
            }

            if (isReloading)
            {
                if (!CanFireWhileReloading())
                {
                    return;
                }

                CancelReloadForFire();
            }

            TryFire();
        }

        private bool EquipWeapon(WeaponData weaponData, int ammoInMagazine, int reserveAmmo, bool playPickupAnimation)
        {
            if (weaponData == null || weaponData.WeaponPrefab == null || weaponHolder == null)
            {
                return false;
            }

            if (currentWeaponView != null)
            {
                Destroy(currentWeaponView.gameObject);
                currentProjectileShooter = null;
                currentMuzzleFlash = null;
            }

            GameObject weaponInstance = Instantiate(weaponData.WeaponPrefab, weaponHolder);
            weaponInstance.transform.localPosition = weaponData.ViewLocalPosition;
            weaponInstance.transform.localRotation = Quaternion.Euler(weaponData.ViewLocalEulerAngles);

            currentWeaponView = weaponInstance.GetComponent<WeaponView>();
            if (currentWeaponView == null)
            {
                currentWeaponView = weaponInstance.AddComponent<WeaponView>();
            }

            currentWeaponView.Initialize(weaponData);
            currentWeaponView.SetOwner(this);
            currentProjectileShooter = weaponInstance.GetComponentInChildren<ScreenCenterProjectileShooter>(true);
            currentMuzzleFlash = weaponInstance.GetComponentInChildren<MuzzleFlash>(true);
            weaponWorldSfxPlayer = currentWeaponView.Muzzle.GetComponent<SfxPlayer>();
            if (weaponWorldSfxPlayer == null)
            {
                weaponWorldSfxPlayer = currentWeaponView.Muzzle.gameObject.AddComponent<SfxPlayer>();
            }
            currentWeaponData = weaponData;
            currentAmmoInMagazine = Mathf.Clamp(ammoInMagazine, 0, weaponData.MagazineSize);
            currentReserveAmmo = Mathf.Max(0, reserveAmmo);
            if (reloadCoroutine != null)
            {
                StopCoroutine(reloadCoroutine);
                reloadCoroutine = null;
            }

            isReloading = false;
            isAnimationEventReloading = false;
            isAiming = false;
            currentAimBlend = 0f;
            nextFireTime = 0f;

            defaultWeaponLocalPosition = weaponData.ViewLocalPosition;
            defaultWeaponLocalRotation = Quaternion.Euler(weaponData.ViewLocalEulerAngles);
            currentWeaponView.transform.localPosition = defaultWeaponLocalPosition;
            currentWeaponView.transform.localRotation = defaultWeaponLocalRotation;
            ApplyAimFieldOfView(defaultCameraFieldOfView);
            weaponRecoilTargetPosition = Vector3.zero;
            weaponRecoilCurrentPosition = Vector3.zero;
            weaponRecoilTargetRotation = Vector3.zero;
            weaponRecoilCurrentRotation = Vector3.zero;

            UpdateAnimatorParameters();
            SaveCurrentWeaponStateToSlot();
            NotifyAmmoChanged();

            if (playPickupAnimation)
            {
                PlayStateIfExists(currentWeaponData.PickupStateName, currentWeaponData.PickupCrossFadeDuration);
                PlayCurrentWeaponSfx(currentWeaponData.EquipSfx);
            }
            else
            {
                PlayStateIfExists(currentWeaponData.IdleStateName, 0.05f);
            }

            return true;
        }

        private bool EquipSlot(int slotIndex, bool playPickupAnimation)
        {
            if (!IsValidSlotIndex(slotIndex) || !weaponSlots[slotIndex].HasWeapon)
            {
                return false;
            }

            if (currentSlotIndex == slotIndex && currentWeaponData != null)
            {
                return true;
            }

            SaveCurrentWeaponStateToSlot();

            WeaponSlotState slotState = weaponSlots[slotIndex];
            currentSlotIndex = slotIndex;
            bool equipped = EquipWeapon(slotState.WeaponData, slotState.AmmoInMagazine, slotState.ReserveAmmo, playPickupAnimation);
            if (equipped)
            {
                NotifyWeaponChanged();
            }

            return equipped;
        }

        private int StoreWeaponInFirstEmptySlot(WeaponData weaponData, int ammoInMagazine, int reserveAmmo)
        {
            int slotIndex = FindFirstEmptySlotIndex();
            if (slotIndex < 0)
            {
                return -1;
            }

            SetSlot(slotIndex, weaponData, ammoInMagazine, reserveAmmo);
            return slotIndex;
        }

        private void SetSlot(int slotIndex, WeaponData weaponData, int ammoInMagazine, int reserveAmmo)
        {
            if (!IsValidSlotIndex(slotIndex))
            {
                return;
            }

            int clampedMagazineAmmo = weaponData != null ? Mathf.Clamp(ammoInMagazine, 0, weaponData.MagazineSize) : Mathf.Max(0, ammoInMagazine);
            weaponSlots[slotIndex].Set(weaponData, clampedMagazineAmmo, reserveAmmo);
        }

        private void AddPickupAmmoToSlot(int slotIndex, WeaponPickup pickup)
        {
            if (!IsValidSlotIndex(slotIndex) || pickup == null)
            {
                return;
            }

            WeaponSlotState slotState = weaponSlots[slotIndex];
            slotState.ReserveAmmo += pickup.AmmoInMagazine + pickup.ReserveAmmo;

            if (slotIndex == currentSlotIndex)
            {
                currentReserveAmmo = slotState.ReserveAmmo;
                SaveCurrentWeaponStateToSlot();
                NotifyAmmoChanged();
            }
        }

        private bool UsesAmmoType(WeaponSlotState slotState, AmmoTypeData ammoType)
        {
            return
                slotState != null &&
                slotState.HasWeapon &&
                slotState.WeaponData != null &&
                slotState.WeaponData.AmmoType == ammoType;
        }

        private int GetReserveAmmoForSlot(int slotIndex)
        {
            if (!IsValidSlotIndex(slotIndex))
            {
                return 0;
            }

            if (slotIndex == currentSlotIndex && currentWeaponData != null)
            {
                return currentReserveAmmo;
            }

            return weaponSlots[slotIndex].ReserveAmmo;
        }

        private void CycleSlot(int direction)
        {
            if (weaponSlots == null || weaponSlots.Length == 0)
            {
                return;
            }

            int startIndex = currentSlotIndex >= 0 ? currentSlotIndex : 0;
            for (int step = 1; step <= weaponSlots.Length; step++)
            {
                int candidateIndex = (startIndex + (direction * step) + weaponSlots.Length) % weaponSlots.Length;
                if (weaponSlots[candidateIndex].HasWeapon)
                {
                    EquipSlot(candidateIndex, true);
                    return;
                }
            }
        }

        private bool CanPickup(WeaponPickup pickup)
        {
            if (pickup == null || pickup.WeaponData == null)
            {
                return false;
            }

            return FindWeaponSlotIndex(pickup.WeaponData) >= 0 || FindFirstEmptySlotIndex() >= 0;
        }

        private int FindWeaponSlotIndex(WeaponData weaponData)
        {
            if (weaponData == null || weaponSlots == null)
            {
                return -1;
            }

            for (int slotIndex = 0; slotIndex < weaponSlots.Length; slotIndex++)
            {
                if (weaponSlots[slotIndex].WeaponData == weaponData)
                {
                    return slotIndex;
                }
            }

            return -1;
        }

        private int FindFirstEmptySlotIndex()
        {
            if (weaponSlots == null)
            {
                return -1;
            }

            for (int slotIndex = 0; slotIndex < weaponSlots.Length; slotIndex++)
            {
                if (!weaponSlots[slotIndex].HasWeapon)
                {
                    return slotIndex;
                }
            }

            return -1;
        }

        private bool IsValidSlotIndex(int slotIndex)
        {
            return weaponSlots != null && slotIndex >= 0 && slotIndex < weaponSlots.Length;
        }

        private void SaveCurrentWeaponStateToSlot()
        {
            if (!IsValidSlotIndex(currentSlotIndex) || currentWeaponData == null)
            {
                return;
            }

            weaponSlots[currentSlotIndex].Set(currentWeaponData, currentAmmoInMagazine, currentReserveAmmo);
        }

        private void TryFire()
        {
            if (isReloading || Time.time < nextFireTime || currentWeaponData == null)
            {
                return;
            }

            if (currentAmmoInMagazine <= 0)
            {
                TryPlayEmptyMagazineSfx();

                if (currentWeaponData.AutomaticReload)
                {
                    TryStartReload();
                }

                return;
            }

            // ????????????? ?????? ???????????????????? ????????????????????????????.
            float secondsPerShot = 1f / currentWeaponData.FireRate;
            nextFireTime = Time.time + secondsPerShot;
            currentAmmoInMagazine--;

            for (int shotIndex = 0; shotIndex < currentWeaponData.BulletsPerShot; shotIndex++)
            {
                Vector3 shotDirection = GetShotDirection();
                if (currentProjectileShooter == null || !currentProjectileShooter.FireProjectile(shotDirection, gameObject, currentWeaponData))
                {
                    FireHitscan(shotDirection);
                }
            }

            SpawnMuzzleFlash();
            PlayWeaponFireSfx();
            ApplyFireRecoil();
            PlayStateIfExists(currentWeaponData.FireStateName, currentWeaponData.FireCrossFadeDuration);
            UpdateAnimatorParameters();
            SaveCurrentWeaponStateToSlot();
            NotifyAmmoChanged();
            Fired?.Invoke(this);

            if (currentAmmoInMagazine <= 0 && currentWeaponData.AutomaticReload)
            {
                TryStartReload();
            }
        }

        private void FireHitscan(Vector3 direction)
        {
            if (aimCamera == null || currentWeaponData == null)
            {
                return;
            }
            Ray ray = new Ray(aimCamera.transform.position, direction);
            if (!Physics.Raycast(ray, out RaycastHit hit, currentWeaponData.Range, currentWeaponData.HitMask, QueryTriggerInteraction.Ignore))
            {
                return;
            }

            IDamageable damageable = hit.collider.GetComponentInParent<IDamageable>();
            EnemyHealth enemyHealth = damageable as EnemyHealth;
            float damage = currentWeaponData.Damage;
            if (enemyHealth != null)
            {
                damage *= enemyHealth.GetDamageMultiplier(hit.collider, hit.point);
            }

            damageable?.ApplyDamage(damage, hit.point, hit.normal, gameObject);
            if (enemyHealth != null && enemyHealth.LastDamageWasHeadshot)
            {
                NotifyHeadshotHit();
            }

            if (damageable == null)
            {
                SurfaceImpactEffect.Spawn(hit, currentWeaponData);
            }

            if (hit.rigidbody != null && currentWeaponData.ImpactForce > 0f)
            {
                hit.rigidbody.AddForceAtPosition(direction * currentWeaponData.ImpactForce, hit.point, ForceMode.Impulse);
            }
        }

        private Vector3 GetShotDirection()
        {
            Vector3 direction = aimCamera.transform.forward;
            if (currentWeaponData == null || currentWeaponData.SpreadAngle <= 0f)
            {
                return direction;
            }

            Vector2 spreadOffset = Random.insideUnitCircle * Mathf.Tan(currentWeaponData.SpreadAngle * Mathf.Deg2Rad);
            direction += (aimCamera.transform.right * spreadOffset.x) + (aimCamera.transform.up * spreadOffset.y);
            return direction.normalized;
        }

        private void SpawnMuzzleFlash()
        {
            if (currentMuzzleFlash == null)
            {
                return;
            }

            currentMuzzleFlash.Play();
        }

        private void ApplyFireRecoil()
        {
            if (currentWeaponData == null)
            {
                return;
            }

            float yawKick = Random.Range(-currentWeaponData.CameraHorizontalRecoil, currentWeaponData.CameraHorizontalRecoil);
            cameraRecoilTargetOffset += new Vector2(yawKick, -currentWeaponData.CameraVerticalRecoil);

            weaponRecoilTargetPosition += currentWeaponData.WeaponKickPosition;
            weaponRecoilTargetRotation += new Vector3(
                currentWeaponData.WeaponKickRotation.x,
                Random.Range(-currentWeaponData.WeaponKickRotation.y, currentWeaponData.WeaponKickRotation.y),
                Random.Range(-currentWeaponData.WeaponKickRotation.z, currentWeaponData.WeaponKickRotation.z));
        }

        private void UpdateRecoil(float deltaTime)
        {
            if (currentWeaponData == null)
            {
                return;
            }

            if (panTilt != null)
            {
                // Move toward the accumulated recoil target without pulling the aim back to center.
                float cameraKickLerp = 1f - Mathf.Exp(-currentWeaponData.CameraRecoilRecovery * deltaTime);
                Vector2 nextCameraOffset = Vector2.Lerp(appliedCameraRecoilOffset, cameraRecoilTargetOffset, cameraKickLerp);
                Vector2 recoilDelta = nextCameraOffset - appliedCameraRecoilOffset;
                panTilt.PanAxis.Value += recoilDelta.x;
                panTilt.TiltAxis.Value += recoilDelta.y;
                appliedCameraRecoilOffset = nextCameraOffset;
            }

            float weaponReturnLerp = 1f - Mathf.Exp(-currentWeaponData.WeaponRecoilReturnSpeed * deltaTime);
            float weaponSnapLerp = 1f - Mathf.Exp(-currentWeaponData.WeaponRecoilSnappiness * deltaTime);

            weaponRecoilTargetPosition = Vector3.Lerp(weaponRecoilTargetPosition, Vector3.zero, weaponReturnLerp);
            weaponRecoilTargetRotation = Vector3.Lerp(weaponRecoilTargetRotation, Vector3.zero, weaponReturnLerp);
            weaponRecoilCurrentPosition = Vector3.Lerp(weaponRecoilCurrentPosition, weaponRecoilTargetPosition, weaponSnapLerp);
            weaponRecoilCurrentRotation = Vector3.Lerp(weaponRecoilCurrentRotation, weaponRecoilTargetRotation, weaponSnapLerp);

            if (currentWeaponView != null)
            {
                Transform weaponTransform = currentWeaponView.transform;
                weaponTransform.localPosition = GetCurrentBaseWeaponLocalPosition() + weaponRecoilCurrentPosition;
                weaponTransform.localRotation = defaultWeaponLocalRotation * Quaternion.Euler(weaponRecoilCurrentRotation);
            }
        }

        private void UpdateAimPresentation(float deltaTime)
        {
            float targetAimBlend = CanAim() && isAiming ? 1f : 0f;
            float aimTransitionSpeed = currentWeaponData != null ? currentWeaponData.AimTransitionSpeed : 14f;
            float aimLerp = 1f - Mathf.Exp(-aimTransitionSpeed * deltaTime);
            currentAimBlend = Mathf.Lerp(currentAimBlend, targetAimBlend, aimLerp);

            float targetFieldOfView = defaultCameraFieldOfView;
            if (currentWeaponData != null && currentWeaponData.EnableAim)
            {
                targetFieldOfView = Mathf.Lerp(defaultCameraFieldOfView, currentWeaponData.AimFieldOfView, currentAimBlend);
            }

            ApplyAimFieldOfView(targetFieldOfView);
        }

        private bool CanAim()
        {
            return !isThrowingGrenade && currentWeaponData != null && currentWeaponView != null && currentWeaponData.EnableAim;
        }

        private Vector3 GetCurrentBaseWeaponLocalPosition()
        {
            if (currentWeaponData == null || !currentWeaponData.EnableAim)
            {
                return defaultWeaponLocalPosition;
            }

            return Vector3.Lerp(defaultWeaponLocalPosition, currentWeaponData.AimViewLocalPosition, currentAimBlend);
        }

        private float GetCurrentCameraFieldOfView()
        {
            if (aimCinemachineCamera != null)
            {
                return aimCinemachineCamera.Lens.FieldOfView;
            }

            return aimCamera != null ? aimCamera.fieldOfView : 0f;
        }

        private void ApplyAimFieldOfView(float fieldOfView)
        {
            if (fieldOfView <= 0f)
            {
                return;
            }

            if (aimCinemachineCamera != null)
            {
                LensSettings lens = aimCinemachineCamera.Lens;
                lens.FieldOfView = fieldOfView;
                aimCinemachineCamera.Lens = lens;
            }

            if (aimCamera != null)
            {
                aimCamera.fieldOfView = fieldOfView;
            }
        }

        private void TryStartReload()
        {
            if (currentWeaponData == null || isReloading)
            {
                return;
            }

            bool hasInfiniteReserveAmmo = currentWeaponData.HasInfiniteReserveAmmo;
            if (currentAmmoInMagazine >= currentWeaponData.MagazineSize || (!hasInfiniteReserveAmmo && currentReserveAmmo <= 0))
            {
                return;
            }

            if (currentWeaponData.UseAnimationEventReload)
            {
                BeginAnimationEventReload();
                return;
            }

            reloadCoroutine = StartCoroutine(ReloadRoutine());
        }

        private bool CanFireWhileReloading()
        {
            return currentAmmoInMagazine > 0;
        }

        private void CancelReloadForFire()
        {
            if (!isReloading)
            {
                return;
            }

            if (reloadCoroutine != null)
            {
                StopCoroutine(reloadCoroutine);
                reloadCoroutine = null;
            }

            isReloading = false;
            isAnimationEventReloading = false;
            UpdateAnimatorParameters();
            PlayStateIfExists(currentWeaponData != null ? currentWeaponData.IdleStateName : string.Empty, 0.05f);
        }

        private void BeginAnimationEventReload()
        {
            isReloading = true;
            isAnimationEventReloading = true;
            UpdateAnimatorParameters();
            PlayCurrentWeaponSfx(currentWeaponData.ReloadStartSfx);

            string startStateName = string.IsNullOrWhiteSpace(currentWeaponData.ReloadStartStateName)
                ? currentWeaponData.ReloadStateName
                : currentWeaponData.ReloadStartStateName;

            PlayStateIfExists(startStateName, currentWeaponData.ReloadCrossFadeDuration);
        }

        private IEnumerator ReloadRoutine()
        {
            isReloading = true;
            UpdateAnimatorParameters();
            PlayCurrentWeaponSfx(currentWeaponData.ReloadStartSfx);
            PlayStateIfExists(currentWeaponData.ReloadStateName, currentWeaponData.ReloadCrossFadeDuration);

            yield return new WaitForSeconds(currentWeaponData.ReloadDuration);

            int ammoNeeded = currentWeaponData.MagazineSize - currentAmmoInMagazine;
            int ammoToLoad = currentWeaponData.HasInfiniteReserveAmmo
                ? ammoNeeded
                : Mathf.Min(ammoNeeded, currentReserveAmmo);
            currentAmmoInMagazine += ammoToLoad;

            if (!currentWeaponData.HasInfiniteReserveAmmo)
            {
                currentReserveAmmo -= ammoToLoad;
            }

            isReloading = false;
            isAnimationEventReloading = false;
            reloadCoroutine = null;
            UpdateAnimatorParameters();
            SaveCurrentWeaponStateToSlot();
            NotifyAmmoChanged();
            PlayCurrentWeaponSfx(currentWeaponData.ReloadEndSfx);
            PlayStateIfExists(currentWeaponData.IdleStateName, 0.05f);
        }

        public void OnAnimationEventInsertReloadAmmo()
        {
            if (!isReloading || !isAnimationEventReloading || currentWeaponData == null)
            {
                return;
            }

            int ammoNeeded = currentWeaponData.MagazineSize - currentAmmoInMagazine;
            int ammoToLoad = currentWeaponData.HasInfiniteReserveAmmo
                ? Mathf.Min(ammoNeeded, currentWeaponData.AmmoPerReloadEvent)
                : Mathf.Min(ammoNeeded, Mathf.Min(currentReserveAmmo, currentWeaponData.AmmoPerReloadEvent));
            if (ammoToLoad <= 0)
            {
                FinishAnimationEventReload();
                return;
            }

            currentAmmoInMagazine += ammoToLoad;

            if (!currentWeaponData.HasInfiniteReserveAmmo)
            {
                currentReserveAmmo -= ammoToLoad;
            }

            PlayCurrentWeaponSfx(currentWeaponData.ReloadInsertSfx);
            UpdateAnimatorParameters();
            SaveCurrentWeaponStateToSlot();
            NotifyAmmoChanged();
            bool canContinueReload =
                currentAmmoInMagazine < currentWeaponData.MagazineSize &&
                (currentWeaponData.HasInfiniteReserveAmmo || currentReserveAmmo > 0);

            if (!canContinueReload)
            {
                FinishAnimationEventReload();
            }
        }

        private void FinishAnimationEventReload()
        {
            isReloading = false;
            isAnimationEventReloading = false;
            reloadCoroutine = null;
            UpdateAnimatorParameters();
            SaveCurrentWeaponStateToSlot();
            NotifyAmmoChanged();
            PlayCurrentWeaponSfx(currentWeaponData.ReloadEndSfx);
        }

        private void UpdateAnimatorParameters()
        {
            Animator animator = currentWeaponView != null ? currentWeaponView.Animator : null;
            if (animator == null || currentWeaponData == null)
            {
                return;
            }

            AnimatorHelper.SetBoolIfExists(animator, currentWeaponData.ReloadBoolParameter, isReloading);
            AnimatorHelper.SetIntIfExists(animator, currentWeaponData.AmmoParameter, currentAmmoInMagazine);
            AnimatorHelper.SetFloatIfExists(animator, currentWeaponData.SprintAmountParameter, 0f);
            AnimatorHelper.SetFloatIfExists(animator, currentWeaponData.AdsAmountParameter, currentAimBlend);
        }

        private void NotifyAmmoChanged()
        {
            AmmoChanged?.Invoke(this);
        }

        private void NotifyGrenadeCountChanged()
        {
            GrenadeCountChanged?.Invoke(this);
        }

        public void NotifyHeadshotHit()
        {
            HeadshotHit?.Invoke(this);
        }

        private void ConsumeGrenade()
        {
            if (currentGrenadeCount <= 0)
            {
                return;
            }

            currentGrenadeCount--;
            NotifyGrenadeCountChanged();
        }

        private void PlayCurrentWeaponSfx(SfxClipSet clipSet)
        {
            if (weaponSfxPlayer == null || clipSet == null)
            {
                return;
            }

            weaponSfxPlayer.Play(clipSet);
        }

        private void TryPlayEmptyMagazineSfx()
        {
            if (currentWeaponData == null || Time.time < nextEmptyMagazineSfxTime)
            {
                return;
            }

            nextEmptyMagazineSfxTime = Time.time + EmptyMagazineSfxCooldown;
            PlayCurrentWeaponSfx(currentWeaponData.EmptyMagazineSfx);
        }

        private void PlayWeaponFireSfx()
        {
            if (currentWeaponData == null)
            {
                return;
            }

            bool playedFireProfile = PlayCurrentWeaponWorldAudio(currentWeaponData.FireAudioProfile);
            bool playedTailProfile = PlayCurrentWeaponWorldAudio(currentWeaponData.FireTailAudioProfile);

            if (!playedFireProfile)
            {
                PlayCurrentWeaponSfx(currentWeaponData.FireSfx);
            }

            if (!playedTailProfile)
            {
                PlayCurrentWeaponSfx(currentWeaponData.FireTailSfx);
            }
        }

        private bool PlayCurrentWeaponWorldAudio(Akila.FPSFramework.AudioProfile audioProfile)
        {
            if (weaponWorldSfxPlayer == null || audioProfile == null)
            {
                return false;
            }

            return weaponWorldSfxPlayer.Play(audioProfile);
        }

        private void NotifyWeaponChanged()
        {
            WeaponChanged?.Invoke(this);
        }

        private void PlayStateIfExists(string stateName, float blendDuration)
        {
            Animator animator = currentWeaponView != null ? currentWeaponView.Animator : null;
            if (animator == null || string.IsNullOrWhiteSpace(stateName))
            {
                return;
            }

            if (!animator.HasState(0, Animator.StringToHash(stateName)))
            {
                return;
            }

            animator.CrossFadeInFixedTime(stateName, blendDuration);
        }

    }
}













