using System;
using System.Collections;
using System.Collections.Generic;
using Mood.Audio;
using Mood.Combat;
using Mood.Health;
using UnityEngine;
using UnityEngine.AI;

namespace Mood.Events
{
    [AddComponentMenu("MOOD/Events/Wave Manager")]
    [DisallowMultipleComponent]
    public sealed class WaveManager : MonoBehaviour
    {
        [Serializable]
        public sealed class EnemyArchetypeDefinition
        {
            [SerializeField] private string archetypeId = "Enemy";
            [SerializeField] private GameObject enemyPrefab;

            public string ArchetypeId => archetypeId;
            public GameObject EnemyPrefab => enemyPrefab;
            public bool IsValid => !string.IsNullOrWhiteSpace(archetypeId) && enemyPrefab != null;
        }

        [Serializable]
        public sealed class WaveSpawnEntry
        {
            [SerializeField] private SpawnZone spawnZone;
            [SerializeField] private string archetypeId = "Enemy";
            [SerializeField, Min(0f)] private float startDelay;
            [SerializeField, Min(1)] private int spawnCount = 1;
            [SerializeField, Min(0f)] private float spawnInterval = 0.25f;
            [SerializeField] private Vector3 positionOffset;
            [SerializeField] private bool randomYaw = true;

            public SpawnZone SpawnZone => spawnZone;
            public string ArchetypeId => archetypeId;
            public float StartDelay => startDelay;
            public int SpawnCount => spawnCount;
            public float SpawnInterval => spawnInterval;
            public Vector3 PositionOffset => positionOffset;
            public bool RandomYaw => randomYaw;
            public bool IsValid => spawnZone != null && spawnCount > 0 && !string.IsNullOrWhiteSpace(archetypeId);
        }

        [Serializable]
        public sealed class WaveDefinition
        {
            [SerializeField, Min(0f)] private float startDelay;
            [SerializeField] private bool waitUntilAllEnemiesDead = true;
            [SerializeField, Min(0f)] private float delayAfterWave;
            [SerializeField] private string bgmTrackKey;
            [SerializeField] private bool immediateBgmTransition;
            [SerializeField, Min(0.01f)] private float healthMultiplier = 1f;
            [SerializeField, Min(0.01f)] private float speedMultiplier = 1f;
            [SerializeField] private WaveSpawnEntry[] spawns = Array.Empty<WaveSpawnEntry>();

            public float StartDelay => startDelay;
            public bool WaitUntilAllEnemiesDead => waitUntilAllEnemiesDead;
            public float DelayAfterWave => delayAfterWave;
            public string BgmTrackKey => bgmTrackKey;
            public bool ImmediateBgmTransition => immediateBgmTransition;
            public float HealthMultiplier => healthMultiplier;
            public float SpeedMultiplier => speedMultiplier;
            public WaveSpawnEntry[] Spawns => spawns ?? Array.Empty<WaveSpawnEntry>();
        }

        [Serializable]
        public sealed class TriggerSequenceDefinition
        {
            [SerializeField] private TriggerZone triggerZone;
            [SerializeField] private bool triggerOnce = true;
            [SerializeField, Min(0f)] private float startDelay;
            [SerializeField] private WaveDefinition[] waves = Array.Empty<WaveDefinition>();

            public TriggerZone TriggerZone => triggerZone;
            public bool TriggerOnce => triggerOnce;
            public float StartDelay => startDelay;
            public WaveDefinition[] Waves => waves ?? Array.Empty<WaveDefinition>();
            public bool IsValid => triggerZone != null;
        }

        private sealed class RuntimeState
        {
            public Coroutine Routine;
            public readonly List<Coroutine> SpawnRoutines = new List<Coroutine>();
            public int AliveEnemyCount;
            public int PendingSpawnRoutineCount;
            public bool IsRunning;
            public bool IsCompleted;
        }

        public interface IWaveSpeedScaler
        {
            void ApplySpeedMultiplier(float multiplier);
        }

        [Header("Enemy Archetypes")]
        [SerializeField] private EnemyArchetypeDefinition[] enemyArchetypes = Array.Empty<EnemyArchetypeDefinition>();

        [Header("Trigger Waves")]
        [SerializeField] private TriggerSequenceDefinition[] triggerSequences = Array.Empty<TriggerSequenceDefinition>();

        private readonly Dictionary<string, GameObject> archetypeLookup =
            new Dictionary<string, GameObject>(StringComparer.OrdinalIgnoreCase);

        private readonly Dictionary<EnemyHealth, RuntimeState> trackedEnemyHealth =
            new Dictionary<EnemyHealth, RuntimeState>();

        private readonly Dictionary<CharacterHealth, RuntimeState> trackedCharacterHealth =
            new Dictionary<CharacterHealth, RuntimeState>();

        private RuntimeState[] runtimeStates = Array.Empty<RuntimeState>();

        public event Action<WaveManager, TriggerZone> TriggeredSequenceCompleted;

        public bool IsRunning
        {
            get
            {
                for (int index = 0; index < runtimeStates.Length; index++)
                {
                    if (runtimeStates[index] != null && runtimeStates[index].IsRunning)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        private void Awake()
        {
            RebuildArchetypeLookup();
            EnsureRuntimeStates();
        }

        private void OnValidate()
        {
            RebuildArchetypeLookup();
            EnsureRuntimeStates();
        }

        private void OnDisable()
        {
            StopAllSequenceRoutines();
            UnsubscribeFromTrackedEnemies();

            for (int index = 0; index < runtimeStates.Length; index++)
            {
                ResetRuntimeState(runtimeStates[index]);
            }
        }

        public bool TriggerWaveSequence(TriggerZone triggerZone)
        {
            if (!TryGetSequenceState(triggerZone, out int sequenceIndex, out TriggerSequenceDefinition sequence, out RuntimeState runtimeState))
            {
                Debug.LogWarning($"[WaveManager:{name}] No trigger sequence is bound to trigger zone {GetTriggerZoneName(triggerZone)}.", this);
                return false;
            }

            if (runtimeState.IsRunning)
            {
                return false;
            }

            if (sequence.TriggerOnce && runtimeState.IsCompleted)
            {
                return false;
            }

            runtimeState.AliveEnemyCount = 0;
            runtimeState.IsRunning = true;
            runtimeState.IsCompleted = false;
            runtimeState.Routine = StartCoroutine(RunSequence(sequenceIndex, sequence, runtimeState));
            return true;
        }

        public bool IsSequenceRunning(TriggerZone triggerZone)
        {
            return TryGetSequenceState(triggerZone, out _, out _, out RuntimeState runtimeState) && runtimeState.IsRunning;
        }

        public bool IsSequenceCompleted(TriggerZone triggerZone)
        {
            return TryGetSequenceState(triggerZone, out _, out _, out RuntimeState runtimeState) && runtimeState.IsCompleted;
        }

        private IEnumerator RunSequence(int sequenceIndex, TriggerSequenceDefinition sequence, RuntimeState runtimeState)
        {
            if (sequence.StartDelay > 0f)
            {
                yield return new WaitForSeconds(sequence.StartDelay);
            }

            WaveDefinition[] waves = sequence.Waves;
            for (int waveIndex = 0; waveIndex < waves.Length; waveIndex++)
            {
                WaveDefinition wave = waves[waveIndex];
                if (wave == null)
                {
                    continue;
                }

                if (wave.StartDelay > 0f)
                {
                    yield return new WaitForSeconds(wave.StartDelay);
                }

                PlayWaveBgm(wave);
                yield return SpawnWave(wave, runtimeState);

                if (wave.WaitUntilAllEnemiesDead)
                {
                    yield return new WaitUntil(() => runtimeState.AliveEnemyCount <= 0);
                }

                if (wave.DelayAfterWave > 0f)
                {
                    yield return new WaitForSeconds(wave.DelayAfterWave);
                }
            }

            yield return new WaitUntil(() => runtimeState.AliveEnemyCount <= 0);
            CompleteSequence(sequenceIndex, runtimeState);
        }

        private IEnumerator SpawnWave(WaveDefinition wave, RuntimeState runtimeState)
        {
            WaveSpawnEntry[] spawns = wave.Spawns;
            for (int entryIndex = 0; entryIndex < spawns.Length; entryIndex++)
            {
                WaveSpawnEntry entry = spawns[entryIndex];
                if (entry == null || !entry.IsValid)
                {
                    Debug.LogWarning($"[WaveManager:{name}] Invalid wave spawn entry at index {entryIndex}.", this);
                    continue;
                }

                if (!TryResolveArchetypePrefab(entry.ArchetypeId, out GameObject enemyPrefab))
                {
                    Debug.LogWarning($"[WaveManager:{name}] Unknown archetype '{entry.ArchetypeId}' in spawn entry {entryIndex}.", this);
                    continue;
                }

                runtimeState.PendingSpawnRoutineCount++;
                runtimeState.SpawnRoutines.Add(
                    StartCoroutine(SpawnWaveEntry(wave, entry, enemyPrefab, runtimeState)));
            }

            if (runtimeState.PendingSpawnRoutineCount <= 0)
            {
                yield break;
            }

            yield return new WaitUntil(() => runtimeState.PendingSpawnRoutineCount <= 0);
            runtimeState.SpawnRoutines.Clear();
        }

        private IEnumerator SpawnWaveEntry(
            WaveDefinition wave,
            WaveSpawnEntry entry,
            GameObject enemyPrefab,
            RuntimeState runtimeState)
        {
            try
            {
                if (entry.StartDelay > 0f)
                {
                    yield return new WaitForSeconds(entry.StartDelay);
                }

                for (int spawnIndex = 0; spawnIndex < entry.SpawnCount; spawnIndex++)
                {
                    GameObject spawnedEnemy = entry.SpawnZone.Spawn(enemyPrefab, entry.PositionOffset, entry.RandomYaw);
                    ApplyWaveRuntimeModifiers(spawnedEnemy, wave.HealthMultiplier, wave.SpeedMultiplier);
                    TrackSpawnedEnemy(spawnedEnemy, runtimeState);

                    bool shouldWait = spawnIndex < entry.SpawnCount - 1 && entry.SpawnInterval > 0f;
                    if (shouldWait)
                    {
                        yield return new WaitForSeconds(entry.SpawnInterval);
                    }
                }
            }
            finally
            {
                runtimeState.PendingSpawnRoutineCount = Mathf.Max(0, runtimeState.PendingSpawnRoutineCount - 1);
            }
        }

        private void ApplyWaveRuntimeModifiers(GameObject spawnedEnemy, float healthMultiplier, float speedMultiplier)
        {
            if (spawnedEnemy == null)
            {
                return;
            }

            EnemyHealth enemyHealth = spawnedEnemy.GetComponentInChildren<EnemyHealth>(true);
            if (enemyHealth != null)
            {
                enemyHealth.ApplyMaxHealthMultiplier(healthMultiplier, true);
            }

            CharacterHealth characterHealth = spawnedEnemy.GetComponentInChildren<CharacterHealth>(true);
            if (characterHealth != null)
            {
                characterHealth.ApplyMaxHealthMultiplier(healthMultiplier, true);
            }

            NavMeshAgent[] agents = spawnedEnemy.GetComponentsInChildren<NavMeshAgent>(true);
            for (int index = 0; index < agents.Length; index++)
            {
                NavMeshAgent agent = agents[index];
                if (agent == null)
                {
                    continue;
                }

                agent.speed = Mathf.Max(0.01f, agent.speed * speedMultiplier);
                agent.acceleration = Mathf.Max(0.01f, agent.acceleration * speedMultiplier);
                agent.angularSpeed = Mathf.Max(0.01f, agent.angularSpeed * speedMultiplier);
            }

            MonoBehaviour[] behaviours = spawnedEnemy.GetComponentsInChildren<MonoBehaviour>(true);
            for (int index = 0; index < behaviours.Length; index++)
            {
                if (behaviours[index] is IWaveSpeedScaler speedScaler)
                {
                    speedScaler.ApplySpeedMultiplier(speedMultiplier);
                }
            }
        }

        private void TrackSpawnedEnemy(GameObject spawnedEnemy, RuntimeState runtimeState)
        {
            if (spawnedEnemy == null || runtimeState == null)
            {
                return;
            }

            EnemyHealth enemyHealth = spawnedEnemy.GetComponentInChildren<EnemyHealth>(true);
            if (enemyHealth != null)
            {
                runtimeState.AliveEnemyCount++;
                trackedEnemyHealth[enemyHealth] = runtimeState;
                enemyHealth.Died += HandleEnemyHealthDied;
                return;
            }

            CharacterHealth characterHealth = spawnedEnemy.GetComponentInChildren<CharacterHealth>(true);
            if (characterHealth != null)
            {
                runtimeState.AliveEnemyCount++;
                trackedCharacterHealth[characterHealth] = runtimeState;
                characterHealth.Died += HandleCharacterHealthDied;
                return;
            }

            runtimeState.AliveEnemyCount++;
            StartCoroutine(TrackFallbackLifetime(spawnedEnemy, runtimeState));
        }

        private IEnumerator TrackFallbackLifetime(GameObject spawnedEnemy, RuntimeState runtimeState)
        {
            while (spawnedEnemy != null)
            {
                yield return null;
            }

            DecrementAliveEnemyCount(runtimeState);
        }

        private void HandleEnemyHealthDied(EnemyHealth health, GameObject _)
        {
            if (health == null || !trackedEnemyHealth.TryGetValue(health, out RuntimeState runtimeState))
            {
                return;
            }

            health.Died -= HandleEnemyHealthDied;
            trackedEnemyHealth.Remove(health);
            DecrementAliveEnemyCount(runtimeState);
        }

        private void HandleCharacterHealthDied(CharacterHealth health, GameObject _)
        {
            if (health == null || !trackedCharacterHealth.TryGetValue(health, out RuntimeState runtimeState))
            {
                return;
            }

            health.Died -= HandleCharacterHealthDied;
            trackedCharacterHealth.Remove(health);
            DecrementAliveEnemyCount(runtimeState);
        }

        private void DecrementAliveEnemyCount(RuntimeState runtimeState)
        {
            if (runtimeState == null)
            {
                return;
            }

            runtimeState.AliveEnemyCount = Mathf.Max(0, runtimeState.AliveEnemyCount - 1);
        }

        private void CompleteSequence(int sequenceIndex, RuntimeState runtimeState)
        {
            runtimeState.Routine = null;
            runtimeState.IsRunning = false;
            runtimeState.IsCompleted = true;
            TriggeredSequenceCompleted?.Invoke(this, triggerSequences[sequenceIndex].TriggerZone);
        }

        private void PlayWaveBgm(WaveDefinition wave)
        {
            if (wave == null || string.IsNullOrWhiteSpace(wave.BgmTrackKey))
            {
                return;
            }

            // 웨이브 시작 타이밍에만 BGM을 전환해서
            // 같은 웨이브 내부의 개별 스폰이 음악을 다시 덮어쓰지 않게 한다.
            BgmManager.Instance?.PlayByKey(wave.BgmTrackKey, wave.ImmediateBgmTransition);
        }

        private bool TryGetSequenceState(
            TriggerZone triggerZone,
            out int sequenceIndex,
            out TriggerSequenceDefinition sequence,
            out RuntimeState runtimeState)
        {
            EnsureRuntimeStates();

            sequenceIndex = -1;
            sequence = null;
            runtimeState = null;

            if (triggerZone == null || triggerSequences == null)
            {
                return false;
            }

            for (int index = 0; index < triggerSequences.Length; index++)
            {
                TriggerSequenceDefinition candidate = triggerSequences[index];
                if (candidate == null || !candidate.IsValid || candidate.TriggerZone != triggerZone)
                {
                    continue;
                }

                sequenceIndex = index;
                sequence = candidate;
                runtimeState = runtimeStates[index];
                return true;
            }

            return false;
        }

        private bool TryResolveArchetypePrefab(string archetypeId, out GameObject enemyPrefab)
        {
            if (string.IsNullOrWhiteSpace(archetypeId))
            {
                enemyPrefab = null;
                return false;
            }

            if (archetypeLookup.Count != (enemyArchetypes != null ? enemyArchetypes.Length : 0))
            {
                RebuildArchetypeLookup();
            }

            return archetypeLookup.TryGetValue(archetypeId.Trim(), out enemyPrefab) && enemyPrefab != null;
        }

        private void RebuildArchetypeLookup()
        {
            archetypeLookup.Clear();

            EnemyArchetypeDefinition[] definitions = enemyArchetypes ?? Array.Empty<EnemyArchetypeDefinition>();
            for (int index = 0; index < definitions.Length; index++)
            {
                EnemyArchetypeDefinition definition = definitions[index];
                if (definition == null || !definition.IsValid)
                {
                    continue;
                }

                archetypeLookup[definition.ArchetypeId.Trim()] = definition.EnemyPrefab;
            }
        }

        private void EnsureRuntimeStates()
        {
            int sequenceCount = triggerSequences != null ? triggerSequences.Length : 0;
            if (runtimeStates != null && runtimeStates.Length == sequenceCount)
            {
                return;
            }

            RuntimeState[] nextStates = new RuntimeState[sequenceCount];
            for (int index = 0; index < sequenceCount; index++)
            {
                nextStates[index] = new RuntimeState();
            }

            runtimeStates = nextStates;
        }

        private void StopAllSequenceRoutines()
        {
            for (int index = 0; index < runtimeStates.Length; index++)
            {
                RuntimeState runtimeState = runtimeStates[index];
                if (runtimeState == null)
                {
                    continue;
                }

                if (runtimeState.Routine != null)
                {
                    StopCoroutine(runtimeState.Routine);
                }

                for (int coroutineIndex = 0; coroutineIndex < runtimeState.SpawnRoutines.Count; coroutineIndex++)
                {
                    Coroutine spawnRoutine = runtimeState.SpawnRoutines[coroutineIndex];
                    if (spawnRoutine != null)
                    {
                        StopCoroutine(spawnRoutine);
                    }
                }
            }
        }

        private static void ResetRuntimeState(RuntimeState runtimeState)
        {
            if (runtimeState == null)
            {
                return;
            }

            runtimeState.Routine = null;
            runtimeState.SpawnRoutines.Clear();
            runtimeState.AliveEnemyCount = 0;
            runtimeState.PendingSpawnRoutineCount = 0;
            runtimeState.IsRunning = false;
            runtimeState.IsCompleted = false;
        }

        private void UnsubscribeFromTrackedEnemies()
        {
            foreach (KeyValuePair<EnemyHealth, RuntimeState> pair in trackedEnemyHealth)
            {
                if (pair.Key != null)
                {
                    pair.Key.Died -= HandleEnemyHealthDied;
                }
            }

            foreach (KeyValuePair<CharacterHealth, RuntimeState> pair in trackedCharacterHealth)
            {
                if (pair.Key != null)
                {
                    pair.Key.Died -= HandleCharacterHealthDied;
                }
            }

            trackedEnemyHealth.Clear();
            trackedCharacterHealth.Clear();
        }

        private static string GetTriggerZoneName(TriggerZone triggerZone)
        {
            return triggerZone != null ? triggerZone.name : "<null>";
        }
    }
}
