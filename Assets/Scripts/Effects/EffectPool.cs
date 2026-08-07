using System.Collections.Generic;
using UnityEngine;

namespace Mood.Effects
{
    public static class EffectPool
    {
        private sealed class Pool
        {
            public Pool(int key, GameObject prefab, Transform container)
            {
                Key = key;
                Prefab = prefab;
                Container = container;
            }

            public int Key { get; }
            public GameObject Prefab { get; }
            public Transform Container { get; }
            public Stack<PooledEffectInstance> Available { get; } = new Stack<PooledEffectInstance>(8);
        }

        private static readonly Dictionary<int, Pool> Pools = new Dictionary<int, Pool>();
        private static Transform root;

        public static GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation, float lifetime)
        {
            if (prefab == null)
            {
                return null;
            }

            Pool pool = GetOrCreatePool(prefab);
            PooledEffectInstance instance = GetOrCreateInstance(pool);
            instance.transform.SetParent(null, true);
            instance.Play(position, rotation, lifetime);
            return instance.gameObject;
        }

        internal static void Release(PooledEffectInstance instance)
        {
            if (instance == null)
            {
                return;
            }

            if (!Pools.TryGetValue(instance.PoolKey, out Pool pool))
            {
                Object.Destroy(instance.gameObject);
                return;
            }

            instance.PrepareForPool(pool.Container);
            pool.Available.Push(instance);
        }

        private static Pool GetOrCreatePool(GameObject prefab)
        {
            int key = prefab.GetInstanceID();
            if (Pools.TryGetValue(key, out Pool pool))
            {
                return pool;
            }

            Transform rootTransform = GetOrCreateRoot();
            GameObject containerObject = new GameObject(prefab.name + " Pool");
            containerObject.transform.SetParent(rootTransform, false);
            pool = new Pool(key, prefab, containerObject.transform);
            Pools.Add(key, pool);
            return pool;
        }

        private static PooledEffectInstance GetOrCreateInstance(Pool pool)
        {
            while (pool.Available.Count > 0)
            {
                PooledEffectInstance instance = pool.Available.Pop();
                if (instance != null)
                {
                    return instance;
                }
            }

            GameObject instanceObject = Object.Instantiate(pool.Prefab, pool.Container);
            instanceObject.name = pool.Prefab.name + " (Pooled)";

            PooledEffectInstance pooledInstance = instanceObject.GetComponent<PooledEffectInstance>();
            if (pooledInstance == null)
            {
                pooledInstance = instanceObject.AddComponent<PooledEffectInstance>();
            }

            pooledInstance.SetPoolKey(pool.Key);
            pooledInstance.PrepareForPool(pool.Container);
            return pooledInstance;
        }

        private static Transform GetOrCreateRoot()
        {
            if (root != null)
            {
                return root;
            }

            GameObject rootObject = new GameObject("Effect Pools");
            Object.DontDestroyOnLoad(rootObject);
            root = rootObject.transform;
            return root;
        }
    }
}
