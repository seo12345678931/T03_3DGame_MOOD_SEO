using Mood.Combat;
using Mood.Effects;
using UnityEngine;

namespace Mood.Weapons
{
    [AddComponentMenu("MOOD/Weapons/Screen Center Projectile Shooter")]
    [DisallowMultipleComponent]
    public sealed class ScreenCenterProjectileShooter : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Camera aimCamera;

        [Header("Aim")]
        [SerializeField] private LayerMask aimMask = ~0;
        [SerializeField] private QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Ignore;

        private const float DefaultMaxRange = 500f;
        private const float DefaultDamage = 0f;
        private const float DefaultImpactForce = 0f;

        private void Reset()
        {
            AssignReferences();
        }

        private void Awake()
        {
            AssignReferences();
        }

        public bool FireProjectile(Vector3 shotDirection, GameObject instigator, WeaponData weaponData)
        {
            if (aimCamera == null)
            {
                return false;
            }

            Vector3 aimDirection = shotDirection.sqrMagnitude > Mathf.Epsilon
                ? shotDirection.normalized
                : aimCamera.transform.forward;

            float maxDistance = weaponData != null ? Mathf.Max(0.1f, weaponData.Range) : DefaultMaxRange;
            LayerMask hitLayers = weaponData != null ? weaponData.HitMask : aimMask;
            float damage = weaponData != null ? weaponData.Damage : DefaultDamage;
            float impactForce = weaponData != null ? weaponData.ImpactForce : DefaultImpactForce;

            Ray aimRay = new Ray(aimCamera.transform.position, aimDirection);
            if (!Physics.Raycast(aimRay, out RaycastHit hit, maxDistance, hitLayers, triggerInteraction))
            {
                return true;
            }

            IDamageable damageable = hit.collider.GetComponentInParent<IDamageable>();
            EnemyHealth enemyHealth = damageable as EnemyHealth;
            float appliedDamage = damage;
            if (enemyHealth != null)
            {
                appliedDamage *= enemyHealth.GetDamageMultiplier(hit.collider, hit.point);
            }

            damageable?.ApplyDamage(appliedDamage, hit.point, hit.normal, instigator);
            if (enemyHealth != null && enemyHealth.LastDamageWasHeadshot)
            {
                instigator?.GetComponent<PlayerWeaponSystem>()?.NotifyHeadshotHit();
            }

            if (damageable == null)
            {
                SurfaceImpactEffect.Spawn(hit, weaponData);
            }

            if (hit.rigidbody != null && impactForce > 0f)
            {
                hit.rigidbody.AddForceAtPosition(aimDirection * impactForce, hit.point, ForceMode.Impulse);
            }

            return true;
        }

        private void AssignReferences()
        {
            if (aimCamera == null)
            {
                aimCamera = Camera.main;
            }


        }
    }
}
