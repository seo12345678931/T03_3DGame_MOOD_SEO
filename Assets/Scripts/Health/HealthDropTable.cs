using System;
using System.Collections.Generic;
using Mood.Utils;
using UnityEngine;

namespace Mood.Health
{
    [CreateAssetMenu(fileName = "HealthDropTable", menuName = "MOOD/Health/Health Drop Table")]
    public sealed class HealthDropTable : ScriptableObject
    {
        [Serializable]
        public sealed class Entry : IWeightedEntry
        {
            [SerializeField] private HealthPickupData pickupData;
            [SerializeField, Min(0.001f)] private float weight = 1f;
            [SerializeField, Min(1)] private int spawnCount = 1;

            public HealthPickupData PickupData => pickupData;
            public float Weight => weight;
            public int SpawnCount => spawnCount;
            public bool IsValid => pickupData != null && weight > 0f && spawnCount > 0;
        }

        [Header("Roll Rules")]
        [SerializeField, Range(0f, 1f)] private float dropChance = 0.6f;
        [SerializeField, Min(1)] private int rolls = 1;
        [SerializeField] private bool allowDuplicateEntries = true;

        [Header("Entries")]
        [SerializeField] private List<Entry> entries = new List<Entry>();

        public float DropChance => dropChance;
        public int Rolls => rolls;
        public bool AllowDuplicateEntries => allowDuplicateEntries;
        public IReadOnlyList<Entry> Entries => entries;

        public int RollDrops(List<HealthPickupData> results)
        {
            if (results == null)
            {
                throw new ArgumentNullException(nameof(results));
            }

            results.Clear();

            if (entries == null || entries.Count == 0 || rolls <= 0)
            {
                return 0;
            }

            List<int> availableIndexes = allowDuplicateEntries
                ? null
                : WeightedSelector.BuildAvailableIndexes(entries);

            for (int rollIndex = 0; rollIndex < rolls; rollIndex++)
            {
                if (UnityEngine.Random.value > dropChance)
                {
                    continue;
                }

                int entryIndex = WeightedSelector.SelectIndex(entries, availableIndexes);
                if (entryIndex < 0)
                {
                    break;
                }

                Entry entry = entries[entryIndex];
                for (int spawnIndex = 0; spawnIndex < entry.SpawnCount; spawnIndex++)
                {
                    results.Add(entry.PickupData);
                }

                if (availableIndexes != null)
                {
                    availableIndexes.Remove(entryIndex);
                    if (availableIndexes.Count == 0)
                    {
                        break;
                    }
                }
            }

            return results.Count;
        }
    }
}
