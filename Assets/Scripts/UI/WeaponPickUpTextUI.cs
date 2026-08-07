using UnityEngine;

namespace Mood.UI
{
    [AddComponentMenu("MOOD/UI/Weapon PickUp Text UI")]
    [DisallowMultipleComponent]
    public class WeaponPickUpTextUI : MonoBehaviour
    {
        [SerializeField] private Transform camTransform;

        private void Reset()
        {
            AssignReferences();
        }

        private void Awake()
        {
            AssignReferences();
        }

        private void LateUpdate()
        {
            if (camTransform == null)
            {
                AssignReferences();
                if (camTransform == null)
                {
                    return;
                }
            }

            transform.LookAt(
                transform.position + camTransform.rotation * Vector3.forward,
                camTransform.rotation * Vector3.up);
        }

        private void AssignReferences()
        {
            if (camTransform == null && Camera.main != null)
            {
                camTransform = Camera.main.transform;
            }
        }
    }
}
