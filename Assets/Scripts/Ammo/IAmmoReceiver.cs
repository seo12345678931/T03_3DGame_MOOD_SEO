using UnityEngine;

namespace Mood.Ammo
{
    public interface IAmmoReceiver
    {
        Component Component { get; }

        bool CanReceiveAmmo(AmmoTypeData ammoType, int amount);
        int ReceiveAmmo(AmmoTypeData ammoType, int amount, GameObject source);
    }
}
