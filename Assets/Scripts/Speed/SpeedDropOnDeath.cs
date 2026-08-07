using System.Collections.Generic;
using Mood.Combat;
using Mood.Pickups;
using UnityEngine;

namespace Mood.Speed
{
    [AddComponentMenu("MOOD/Speed/Speed Drop On Death")]
    [DisallowMultipleComponent]
    public sealed class SpeedDropOnDeath : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private EnemyHealth health;
        [SerializeField] private SpeedDropTable dropTable;
        [SerializeField] private Transform dropOrigin;

        [Header("Spawn")]
        [SerializeField] private bool dropOnlyOnce = true;
        [SerializeField, Min(0f)] private float verticalOffset = 0.4f;
        [SerializeField, Min(0f)] private float horizontalScatterRadius = 0.8f;
        [SerializeField] private bool randomYaw = true;
        [SerializeField, Min(0f)] private float dropHoverHeight = 0.3f;
        [SerializeField] private LayerMask dropGroundLayers;

        private readonly List<SpeedPickupData> rolledDrops = new List<SpeedPickupData>(4);
        private bool hasDropped;

        private void Reset()
        {
            health = GetComponent<EnemyHealth>();
            if (dropOrigin == null)
            {
                dropOrigin = transform;
            }
        }

        private void Awake()
        {
            if (health == null)
            {
                health = GetComponent<EnemyHealth>();
            }

            if (dropOrigin == null)
            {
                dropOrigin = transform;
            }
        }

        private void OnEnable()
        {
            if (health != null)
            {
                health.Died += HandleDied;
            }
        }

        private void OnDisable()
        {
            if (health != null)
            {
                health.Died -= HandleDied;
            }
        }

        private void HandleDied(EnemyHealth _, GameObject __)
        {
            if (dropOnlyOnce && hasDropped)
            {
                return;
            }

            if (dropTable == null)
            {
                return;
            }

            int dropCount = dropTable.RollDrops(rolledDrops);
            if (dropCount <= 0)
            {
                return;
            }

            Vector3 origin = dropOrigin != null ? dropOrigin.position : transform.position;

            for (int index = 0; index < rolledDrops.Count; index++)
            {
                SpeedPickupData pickupData = rolledDrops[index];
                if (pickupData == null)
                {
                    continue;
                }

                SpeedPickup pickupPrefab = pickupData.PickupPrefab;
                if (pickupPrefab == null)
                {
                    continue;
                }

                Vector2 scatter = Random.insideUnitCircle * horizontalScatterRadius;
                Vector3 spawnPosition = origin + new Vector3(scatter.x, verticalOffset, scatter.y);
                Quaternion spawnRotation = randomYaw
                    ? Quaternion.Euler(0f, Random.Range(0f, 360f), 0f)
                    : pickupPrefab.transform.rotation;

                SpeedPickup spawnedPickup = Instantiate(pickupPrefab, spawnPosition, spawnRotation);
                spawnedPickup.SetPickupData(pickupData);
                PickupDropPhysics dropPhysics = spawnedPickup.GetComponent<PickupDropPhysics>();
                if (dropPhysics == null)
                {
                    dropPhysics = spawnedPickup.gameObject.AddComponent<PickupDropPhysics>();
                }

                dropPhysics.SetHoverHeight(dropHoverHeight);
                dropPhysics.SetGroundLayers(dropGroundLayers);
                dropPhysics.BeginDrop(Vector3.zero);
            }

            hasDropped = true;
            rolledDrops.Clear();
        }
    }
}
