using UnityEngine;

namespace Mood.Weapons
{
    [AddComponentMenu("MOOD/Weapons/Grenade Throw View")]
    [DisallowMultipleComponent]
    public sealed class GrenadeThrowView : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Animator animator;
        [SerializeField] private GameObject activationRoot;

        [Header("Parameters")]
        [SerializeField] private string triggerParameterName = "Trigger";
        [SerializeField] private string throwParameterName = "Throw";

        private PlayerWeaponSystem owner;

        private void Reset()
        {
            AssignReferences();
        }

        private void Awake()
        {
            AssignReferences();
        }

        public void BeginThrow(PlayerWeaponSystem weaponSystem)
        {
            owner = weaponSystem;
            AssignReferences();

            GameObject rootObject = activationRoot != null ? activationRoot : gameObject;
            if (!rootObject.activeSelf)
            {
                rootObject.SetActive(true);
            }

            if (animator == null)
            {
                return;
            }

            if (!animator.gameObject.activeInHierarchy)
            {
                Debug.LogWarning("GrenadeThrowView animator is inactive in hierarchy. Assign Activation Root to the grenade view root object.", this);
                return;
            }

            animator.Rebind();
            animator.Update(0f);
            animator.ResetTrigger(throwParameterName);
            animator.ResetTrigger(triggerParameterName);
            animator.SetTrigger(triggerParameterName);
        }

        public void HideImmediate()
        {
            GameObject rootObject = activationRoot != null ? activationRoot : gameObject;
            rootObject.SetActive(false);
        }

        public void AnimationEventAdvanceToThrow()
        {
            if (animator == null || string.IsNullOrWhiteSpace(throwParameterName))
            {
                return;
            }

            animator.SetTrigger(throwParameterName);
        }

        public void AnimationEventThrowGrenade()
        {
            owner?.OnGrenadeThrowAnimationEventRelease();
        }

        public void AnimationEventFinishGrenadeThrow()
        {
            owner?.OnGrenadeThrowAnimationFinished();
        }

        private void AssignReferences()
        {
            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>(true);
            }

            if (activationRoot == null)
            {
                activationRoot = gameObject;
            }
        }
    }
}
