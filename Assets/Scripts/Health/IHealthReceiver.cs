using UnityEngine;

namespace Mood.Health
{
    public interface IHealthReceiver
    {
        Component Component { get; }
        float CurrentHealth { get; }
        float MaxHealth { get; }
        bool IsDead { get; }

        bool CanReceiveHealing(float amount);
        float ReceiveHealing(float amount, GameObject source);
    }
}
