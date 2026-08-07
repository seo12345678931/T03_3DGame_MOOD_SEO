using UnityEngine;

namespace Mood.Combat
{
    public interface IDamageable
    {
        void ApplyDamage(float damage, Vector3 hitPoint, Vector3 hitNormal, GameObject instigator);
    }
}
