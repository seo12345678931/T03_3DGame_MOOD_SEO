using System.Collections.Generic;
using Mood.Combat;
using Mood.Pickups;
using UnityEngine;

namespace Mood.Health
{
    [AddComponentMenu("MOOD/Health/Health Drop On Death")]
    [DisallowMultipleComponent]
    public sealed class HealthDropOnDeath : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private EnemyHealth enemyHealth;
        [SerializeField] private HealthDropTable dropTable;
        [SerializeField] private Transform dropOrigin;

        [Header("Spawn")]
        [SerializeField] private bool dropOnlyOnce = true;
        [SerializeField, Min(0f)] private float verticalOffset = 0.4f;
        [SerializeField, Min(0f)] private float horizontalScatterRadius = 0.8f;
        [SerializeField] private bool randomYaw = true;
        [SerializeField, Min(0f)] private float dropHoverHeight = 0.3f;
        [SerializeField] private LayerMask dropGroundLayers;

        private readonly List<HealthPickupData> rolledDrops = new List<HealthPickupData>(4);
        private bool hasDropped;

        private void Reset()
        {
            enemyHealth = GetComponent<EnemyHealth>();
            if (dropOrigin == null)
            {
                dropOrigin = transform;
            }
        }

        private void Awake()
        {
            if (enemyHealth == null)
            {
                enemyHealth = GetComponent<EnemyHealth>();
            }

            if (dropOrigin == null)
            {
                dropOrigin = transform;
            }
        }

        private void OnEnable()
        {
            if (enemyHealth != null)
            {
                enemyHealth.Died += HandleEnemyDied;
            }
        }

        private void OnDisable()
        {
            if (enemyHealth != null)
            {
                enemyHealth.Died -= HandleEnemyDied;
            }
        }

        private void HandleEnemyDied(EnemyHealth _, GameObject __)
        {
            HandleDied();
        }

        private void HandleDied()
        {
            if (dropOnlyOnce && hasDropped)
            {
                return;
            }

            if (!DeathDropSelection.ShouldDrop(
                gameObject,
                DeathDropKind.Health,
                GetComponent<Ammo.AmmoDropOnDeath>() != null,
                true))
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
                HealthPickupData pickupData = rolledDrops[index];
                if (pickupData == null)
                {
                    continue;
                }

                HealthPickup pickupPrefab = pickupData.PickupPrefab;
                if (pickupPrefab == null)
                {
                    continue;
                }

                Vector2 scatter = Random.insideUnitCircle * horizontalScatterRadius;
                Vector3 spawnPosition = origin + new Vector3(scatter.x, verticalOffset, scatter.y);
                Quaternion spawnRotation = randomYaw
                    ? Quaternion.Euler(0f, Random.Range(0f, 360f), 0f)
                    : pickupPrefab.transform.rotation;

                HealthPickup spawnedPickup = Instantiate(pickupPrefab, spawnPosition, spawnRotation);
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
