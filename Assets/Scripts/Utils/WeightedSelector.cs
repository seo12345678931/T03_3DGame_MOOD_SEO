using System.Collections.Generic;
using UnityEngine;

namespace Mood.Utils
{
    /// <summary>
    /// AmmoDropTable, HealthDropTable, SpeedDropTable에서 공통으로 사용하는
    /// 가중치 기반 랜덤 선택 알고리즘을 제공한다.
    /// </summary>
    public static class WeightedSelector
    {
        public static List<int> BuildAvailableIndexes<T>(IReadOnlyList<T> entries) where T : class, IWeightedEntry
        {
            List<int> availableIndexes = new List<int>(entries.Count);
            for (int index = 0; index < entries.Count; index++)
            {
                if (entries[index] != null && entries[index].IsValid)
                {
                    availableIndexes.Add(index);
                }
            }

            return availableIndexes;
        }

        public static int SelectIndex<T>(IReadOnlyList<T> entries, List<int> availableIndexes) where T : class, IWeightedEntry
        {
            float totalWeight = 0f;

            if (availableIndexes == null)
            {
                for (int index = 0; index < entries.Count; index++)
                {
                    T entry = entries[index];
                    if (entry != null && entry.IsValid)
                    {
                        totalWeight += entry.Weight;
                    }
                }

                if (totalWeight <= 0f)
                {
                    return -1;
                }

                float roll = Random.value * totalWeight;
                for (int index = 0; index < entries.Count; index++)
                {
                    T entry = entries[index];
                    if (entry == null || !entry.IsValid)
                    {
                        continue;
                    }

                    roll -= entry.Weight;
                    if (roll <= 0f)
                    {
                        return index;
                    }
                }

                return -1;
            }

            for (int availableIndex = 0; availableIndex < availableIndexes.Count; availableIndex++)
            {
                T entry = entries[availableIndexes[availableIndex]];
                if (entry != null && entry.IsValid)
                {
                    totalWeight += entry.Weight;
                }
            }

            if (totalWeight <= 0f)
            {
                return -1;
            }

            float availableRoll = Random.value * totalWeight;
            for (int availableIndex = 0; availableIndex < availableIndexes.Count; availableIndex++)
            {
                int entryIndex = availableIndexes[availableIndex];
                T entry = entries[entryIndex];
                if (entry == null || !entry.IsValid)
                {
                    continue;
                }

                availableRoll -= entry.Weight;
                if (availableRoll <= 0f)
                {
                    return entryIndex;
                }
            }

            return -1;
        }
    }
}
