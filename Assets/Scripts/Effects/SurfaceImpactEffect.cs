using Mood.Weapons;
using UnityEngine;

namespace Mood.Effects
{
    public static class SurfaceImpactEffect
    {
        public static void Spawn(RaycastHit hit, WeaponData weaponData)
        {
            if (weaponData == null)
            {
                return;
            }

            Spawn(hit, weaponData.SurfaceImpactEffectPrefab, weaponData.SurfaceImpactEffectLifetime, weaponData.SurfaceImpactEffectOffset);
        }

        public static void Spawn(RaycastHit hit, GameObject effectPrefab, float effectLifetime, float surfaceOffset)
        {
            if (effectPrefab == null || hit.collider == null)
            {
                return;
            }

            Vector3 normal = hit.normal.sqrMagnitude > Mathf.Epsilon ? hit.normal.normalized : -hit.transform.forward;
            Vector3 position = hit.point + (normal * surfaceOffset);
            Quaternion rotation = Quaternion.LookRotation(normal, Vector3.up);
            EffectPool.Spawn(effectPrefab, position, rotation, effectLifetime);
        }
    }
}
