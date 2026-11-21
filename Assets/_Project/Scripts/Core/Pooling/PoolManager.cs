using System.Collections.Generic;
using UnityEngine;

namespace KingdomsClone.Core.Pooling
{
    [DefaultExecutionOrder(-950)]
    public class PoolManager : MonoBehaviour
    {
        public static PoolManager Instance { get; private set; }

        private readonly Dictionary<GameObject, ObjectPool> _pools = new();
        private readonly Dictionary<GameObject, ObjectPool> _instanceToPool = new();

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy() { if (Instance == this) Instance = null; }

        /// <summary>
        /// Заранее создать пул и прогреть N экземпляров.
        /// </summary>
        public ObjectPool Prewarm(GameObject prefab, int count, int maxSize = 500)
        {
            if (prefab == null) return null;
            if (_pools.TryGetValue(prefab, out var pool)) return pool;

            pool = new ObjectPool(prefab, transform, count, maxSize);
            _pools[prefab] = pool;
            return pool;
        }

        public GameObject Spawn(GameObject prefab, Vector3 pos, Quaternion rot)
        {
            if (prefab == null) return null;

            if (!_pools.TryGetValue(prefab, out var pool))
            {
                pool = new ObjectPool(prefab, transform, 0, 500);
                _pools[prefab] = pool;
            }

            var obj = pool.Get(pos, rot);
            if (obj != null) _instanceToPool[obj] = pool;
            return obj;
        }

        public void Despawn(GameObject instance)
        {
            if (instance == null) return;

            if (_instanceToPool.TryGetValue(instance, out var pool))
            {
                _instanceToPool.Remove(instance);
                pool.Return(instance);
            }
            else
            {
                // Не из пула — вероятно, был создан через Instantiate. Уничтожаем.
                Destroy(instance);
            }
        }

        public int GetTotalInUseCount()
        {
            int total = 0;
            foreach (var p in _pools.Values) total += p.InUseCount;
            return total;
        }

        public int GetTotalAvailableCount()
        {
            int total = 0;
            foreach (var p in _pools.Values) total += p.AvailableCount;
            return total;
        }
    }
}