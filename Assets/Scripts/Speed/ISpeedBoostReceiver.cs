using UnityEngine;

namespace Mood.Speed
{
    public interface ISpeedBoostReceiver
    {
        Component Component { get; }
        bool CanReceiveSpeedBoost(float moveSpeedBonus, float duration);
        bool ReceiveSpeedBoost(float moveSpeedBonus, float duration, GameObject source);
    }
}
