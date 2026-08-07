using System;
using UnityEngine;

namespace Mood.Events
{
    [AddComponentMenu("MOOD/Events/Spawn Zone")]
    [DisallowMultipleComponent]
    public sealed class SpawnZone : MonoBehaviour
    {
        [Header("Zone")]
        [SerializeField] private string zoneId = "Spawn Zone";
        [SerializeField] private Vector3 spawnPositionOffset;
        [SerializeField] private bool randomYaw = true;

        public string ZoneId => string.IsNullOrWhiteSpace(zoneId) ? name : zoneId;

        public GameObject Spawn(GameObject enemyPrefab)
        {
            return Spawn(enemyPrefab, Vector3.zero, false);
        }

        public GameObject Spawn(GameObject enemyPrefab, Vector3 additionalOffset, bool useRandomYaw)
        {
            if (enemyPrefab == null)
            {
                Debug.LogWarning($"[SpawnZone:{name}] Cannot spawn a null prefab.", this);
                return null;
            }

            Vector3 spawnPosition = transform.position + spawnPositionOffset + additionalOffset;
            Quaternion spawnRotation = (useRandomYaw || randomYaw)
                ? Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f)
                : transform.rotation;

            GameObject spawnedEnemy = Instantiate(enemyPrefab, spawnPosition, spawnRotation);
            Debug.Log($"[SpawnZone:{name}] Spawned {spawnedEnemy.name} at {spawnedEnemy.transform.position}.", spawnedEnemy);
            return spawnedEnemy;
        }
    }
}
