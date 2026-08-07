using System;
using System.Collections.Generic;
using Mood.Pickups;
using Mood.Weapons;
using UnityEngine;

namespace Mood.Ammo
{
    [AddComponentMenu("MOOD/Ammo/Ammo Pickup")]
    [DisallowMultipleComponent]
    public sealed class AmmoPickup : MonoBehaviour
    {
        private const int OverlapBufferSize = 16;

        [Header("Data")]
        [SerializeField] private AmmoPickupData pickupData;
        [SerializeField] private LayerMask receiverMask = ~0;

        [Header("Presentation")]
        [SerializeField] private Transform visualRoot;
        [SerializeField] private bool rotateVisual = true;
        [SerializeField, Min(0f)] private float rotationSpeed = 90f;
        [SerializeField] private bool bobVisual = true;
        [SerializeField, Min(0f)] private float bobAmplitude = 0.08f;
        [SerializeField, Min(0.01f)] private float bobFrequency = 2.5f;

        private readonly Collider[] overlapResults = new Collider[OverlapBufferSize];

        private bool collected;
        private Vector3 visualStartLocalPosition;
        
        public event Action<AmmoPickup, IAmmoReceiver, int> PickedUp;

        public AmmoPickupData PickupData => pickupData;

        public void SetPickupData(AmmoPickupData data)
        {
            pickupData = data;
        }

        private void Awake()
        {
            ConfigurePickupColliders();

            if (visualRoot == null)
            {
                visualRoot = transform;
            }

            visualStartLocalPosition = visualRoot.localPosition;
        }

        private void Update()
        {
            if (collected || pickupData == null)
            {
                return;
            }

            UpdatePresentation(Time.deltaTime);

            IAmmoReceiver receiver = FindBestReceiver();
            if (receiver == null)
            {
                return;
            }

            Vector3 receiverPosition = GetReceiverPosition(receiver);
            float distance = Vector3.Distance(transform.position, receiverPosition);

            if (distance <= pickupData.PickupRadius)
            {
                TryCollect(receiver);
                return;
            }

            if (pickupData.AutoAbsorbRadius > 0f && distance <= pickupData.AutoAbsorbRadius)
            {
                StopDropPhysicsIfNeeded();
                transform.position = Vector3.MoveTowards(transform.position, receiverPosition, pickupData.AttractionSpeed * Time.deltaTime);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (collected || pickupData == null || !TryGetReceiver(other, out IAmmoReceiver receiver))
            {
                return;
            }

            TryCollect(receiver);
        }

        private IAmmoReceiver FindBestReceiver()
        {
            float searchRadius = Mathf.Max(pickupData.PickupRadius, pickupData.AutoAbsorbRadius);
            if (searchRadius <= 0f)
            {
                return null;
            }

            int hitCount = Physics.OverlapSphereNonAlloc(transform.position, searchRadius, overlapResults, receiverMask, QueryTriggerInteraction.Collide);
            IAmmoReceiver bestReceiver = null;
            float bestDistance = float.MaxValue;

            for (int index = 0; index < hitCount; index++)
            {
                Collider hitCollider = overlapResults[index];
                overlapResults[index] = null;

                if (hitCollider == null || !TryGetReceiver(hitCollider, out IAmmoReceiver receiver) || !CanCollect(receiver))
                {
                    continue;
                }

                float distance = Vector3.Distance(transform.position, GetReceiverPosition(receiver));
                if (distance >= bestDistance)
                {
                    continue;
                }

                bestDistance = distance;
                bestReceiver = receiver;
            }

            return bestReceiver;
        }

        private bool CanCollect(IAmmoReceiver receiver)
        {
            if (receiver == null || pickupData == null)
            {
                return false;
            }

            for (int grantIndex = 0; grantIndex < pickupData.Grants.Count; grantIndex++)
            {
                AmmoPickupData.AmmoGrant grant = pickupData.Grants[grantIndex];
                if (grant != null && grant.IsValid && receiver.CanReceiveAmmo(grant.AmmoType, grant.Amount))
                {
                    return true;
                }
            }

            return false;
        }

        private bool TryCollect(IAmmoReceiver receiver)
        {
            if (!CanCollect(receiver))
            {
                return false;
            }

            int totalAdded = 0;
            Dictionary<AmmoTypeData, int> receivedByAmmoType = new Dictionary<AmmoTypeData, int>();
            for (int grantIndex = 0; grantIndex < pickupData.Grants.Count; grantIndex++)
            {
                AmmoPickupData.AmmoGrant grant = pickupData.Grants[grantIndex];
                if (grant == null || !grant.IsValid)
                {
                    continue;
                }

                int addedAmount = receiver.ReceiveAmmo(grant.AmmoType, grant.Amount, gameObject);
                if (addedAmount <= 0)
                {
                    continue;
                }

                totalAdded += addedAmount;

                if (receivedByAmmoType.TryGetValue(grant.AmmoType, out int currentAmount))
                {
                    receivedByAmmoType[grant.AmmoType] = currentAmount + addedAmount;
                }
                else
                {
                    receivedByAmmoType.Add(grant.AmmoType, addedAmount);
                }
            }

            if (totalAdded <= 0)
            {
                return false;
            }

            collected = true;
            StopDropPhysicsIfNeeded();

            if (receiver.Component is PlayerWeaponSystem weaponSystem)
            {
                weaponSystem.NotifyAmmoReceived(gameObject, receivedByAmmoType);
            }

            PickedUp?.Invoke(this, receiver, totalAdded);
            Destroy(gameObject);
            return true;
        }

        private void UpdatePresentation(float deltaTime)
        {
            if (visualRoot == null)
            {
                return;
            }

            bool usingRootTransformAsVisual = visualRoot == transform;
            if (usingRootTransformAsVisual && IsDropPhysicsSimulating())
            {
                return;
            }

            if (rotateVisual)
            {
                visualRoot.Rotate(Vector3.up, rotationSpeed * deltaTime, Space.Self);
            }

            if (bobVisual && !usingRootTransformAsVisual)
            {
                float bobOffset = Mathf.Sin(Time.time * bobFrequency) * bobAmplitude;
                visualRoot.localPosition = visualStartLocalPosition + (Vector3.up * bobOffset);
            }
        }

        private void StopDropPhysicsIfNeeded()
        {
            if (TryGetComponent(out PickupDropPhysics dropPhysics))
            {
                dropPhysics.StopSimulation();
            }
        }

        private bool IsDropPhysicsSimulating()
        {
            return TryGetComponent(out PickupDropPhysics dropPhysics) && dropPhysics.IsSimulating;
        }

        private Vector3 GetReceiverPosition(IAmmoReceiver receiver)
        {
            Component receiverComponent = receiver.Component;
            if (receiverComponent == null)
            {
                return transform.position;
            }

            if (receiverComponent.TryGetComponent(out CharacterController characterController))
            {
                return characterController.bounds.center;
            }

            if (receiverComponent.TryGetComponent(out Collider receiverCollider))
            {
                return receiverCollider.bounds.center;
            }

            return receiverComponent.transform.position;
        }

        private static bool TryGetReceiver(Collider other, out IAmmoReceiver receiver)
        {
            if (other == null)
            {
                receiver = null;
                return false;
            }

            receiver = other.GetComponentInParent<IAmmoReceiver>();
            return receiver != null;
        }

        private void ConfigurePickupColliders()
        {
            Collider[] colliders = GetComponents<Collider>();
            for (int index = 0; index < colliders.Length; index++)
            {
                if (colliders[index] != null)
                {
                    colliders[index].isTrigger = true;
                }
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (pickupData == null)
            {
                return;
            }

            Gizmos.color = new Color(1f, 0.85f, 0.1f, 0.35f);
            Gizmos.DrawWireSphere(transform.position, pickupData.PickupRadius);

            if (pickupData.AutoAbsorbRadius <= 0f)
            {
                return;
            }

            Gizmos.color = new Color(1f, 0.45f, 0f, 0.2f);
            Gizmos.DrawWireSphere(transform.position, pickupData.AutoAbsorbRadius);
        }
    }
}
