using System;
using Mood.Pickups;
using UnityEngine;

namespace Mood.Speed
{
    [AddComponentMenu("MOOD/Speed/Speed Pickup")]
    [DisallowMultipleComponent]
    public sealed class SpeedPickup : MonoBehaviour
    {
        private const int OverlapBufferSize = 16;

        [Header("Data")]
        [SerializeField] private SpeedPickupData pickupData;
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

        public event Action<SpeedPickup, ISpeedBoostReceiver> PickedUp;

        public SpeedPickupData PickupData => pickupData;

        public void SetPickupData(SpeedPickupData data)
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

            ISpeedBoostReceiver receiver = FindBestReceiver();
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
            if (collected || pickupData == null || !TryGetReceiver(other, out ISpeedBoostReceiver receiver))
            {
                return;
            }

            TryCollect(receiver);
        }

        private ISpeedBoostReceiver FindBestReceiver()
        {
            float searchRadius = Mathf.Max(pickupData.PickupRadius, pickupData.AutoAbsorbRadius);
            if (searchRadius <= 0f)
            {
                return null;
            }

            int hitCount = Physics.OverlapSphereNonAlloc(transform.position, searchRadius, overlapResults, receiverMask, QueryTriggerInteraction.Collide);
            ISpeedBoostReceiver bestReceiver = null;
            float bestDistance = float.MaxValue;

            for (int index = 0; index < hitCount; index++)
            {
                Collider hitCollider = overlapResults[index];
                overlapResults[index] = null;

                if (hitCollider == null || !TryGetReceiver(hitCollider, out ISpeedBoostReceiver receiver) || !CanCollect(receiver))
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

        private bool CanCollect(ISpeedBoostReceiver receiver)
        {
            return receiver != null
                && pickupData != null
                && receiver.CanReceiveSpeedBoost(pickupData.MoveSpeedBonus, pickupData.Duration);
        }

        private bool TryCollect(ISpeedBoostReceiver receiver)
        {
            if (!CanCollect(receiver))
            {
                return false;
            }

            if (!receiver.ReceiveSpeedBoost(pickupData.MoveSpeedBonus, pickupData.Duration, gameObject))
            {
                return false;
            }

            collected = true;
            StopDropPhysicsIfNeeded();
            PickedUp?.Invoke(this, receiver);
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

        private Vector3 GetReceiverPosition(ISpeedBoostReceiver receiver)
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

        private static bool TryGetReceiver(Collider other, out ISpeedBoostReceiver receiver)
        {
            if (other == null)
            {
                receiver = null;
                return false;
            }

            receiver = other.GetComponentInParent<ISpeedBoostReceiver>();
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

            Gizmos.color = new Color(0.25f, 0.95f, 1f, 0.35f);
            Gizmos.DrawWireSphere(transform.position, pickupData.PickupRadius);

            if (pickupData.AutoAbsorbRadius <= 0f)
            {
                return;
            }

            Gizmos.color = new Color(0f, 0.55f, 1f, 0.2f);
            Gizmos.DrawWireSphere(transform.position, pickupData.AutoAbsorbRadius);
        }
    }
}
