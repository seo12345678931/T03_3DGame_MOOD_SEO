using UnityEngine;

namespace Mood.Weapons
{
    // 장착된 무기 프리팹에서 애니메이터와 소켓 참조를 제공한다.
    public sealed class WeaponView : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField] private Transform muzzle;
        [SerializeField] private Transform ejectionPort;
        private PlayerWeaponSystem owner;

        public Animator Animator => animator;
        public Transform Muzzle => muzzle != null ? muzzle : transform;
        public Transform EjectionPort => ejectionPort != null ? ejectionPort : Muzzle;

        public void SetOwner(PlayerWeaponSystem weaponSystem)
        {
            owner = weaponSystem;
        }

        public void Initialize(WeaponData weaponData)
        {
            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>(true);
            }

            if (weaponData != null && animator != null && weaponData.AnimatorController != null)
            {
                animator.runtimeAnimatorController = weaponData.AnimatorController;
            }

            // 프리팹 구조가 달라도 이름으로 소켓을 찾아 연결한다.
            if (muzzle == null)
            {
                muzzle = FindChildRecursive(transform, weaponData != null ? weaponData.MuzzleTransformName : "Muzzle");
            }

            if (ejectionPort == null)
            {
                ejectionPort = FindChildRecursive(transform, weaponData != null ? weaponData.EjectionPortTransformName : "Ejection Port");
            }
        }

        private static Transform FindChildRecursive(Transform parent, string targetName)
        {
            if (parent == null || string.IsNullOrWhiteSpace(targetName))
            {
                return null;
            }

            foreach (Transform child in parent)
            {
                if (child.name == targetName)
                {
                    return child;
                }

                Transform found = FindChildRecursive(child, targetName);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        public void AnimationEventInsertReloadAmmo()
        {
            owner?.OnAnimationEventInsertReloadAmmo();
        }

        public void Reload()
        {
            owner?.OnAnimationEventInsertReloadAmmo();
        }
    }
}
