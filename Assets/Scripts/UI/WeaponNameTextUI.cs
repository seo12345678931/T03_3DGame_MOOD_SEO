
using Mood.Weapons;
using TMPro;
using UnityEngine;

namespace Mood.UI
{
    [AddComponentMenu("MOOD/UI/Weapon Name Text")]
    [DisallowMultipleComponent]
    public sealed class WeaponNameTextUI : MonoBehaviour
    {
        [SerializeField] private PlayerWeaponSystem weaponSystem;
        [SerializeField] private TMP_Text nameText;

        private void Reset()
        {
            AssignReferences();
        }
        
        private void Awake()
        {
            AssignReferences();
        }
        
        private void AssignReferences()
        {
            if (nameText == null)
            {
                nameText = GetComponent<TMP_Text>();
            }

            if (weaponSystem == null)
            {
                weaponSystem = FindFirstObjectByType<PlayerWeaponSystem>();
            }
        }

        private void Update()
        {
            if (nameText == null || weaponSystem == null)
            {
                return;
            }

            WeaponData data = weaponSystem.CurrentWeaponData;
            nameText.text = data != null ? data.DisplayName : "None";
        }
    }
}
