using System.Collections.Generic;
using UnityEngine;

namespace KingdomsClone.Core.Pooling
{
    /// <summary>
    /// Пул для одного префаба. Оборачивает Instantiate/SetActive(false).
    /// </summary>
    public class ObjectPool
    {
        private readonly GameObject _prefab;
        private readonly Transform _parent;
        private readonly int _maxSize;
        private readonly Stack<GameObject> _available = new();
        private readonly HashSet<GameObject> _inUse = new();
        private int _totalCreated;

        public GameObject Prefab => _prefab;
        public int AvailableCount => _available.Count;
        public int InUseCount => _inUse.Count;
        public int TotalCount => _totalCreated;

        public ObjectPool(GameObject prefab, Transform parent, int prewarmCount = 0, int maxSize = 0)
        {
            _prefab = prefab;
            _parent = parent;
            _maxSize = maxSize;

            for (int i = 0; i < prewarmCount; i++)
            {
                var obj = CreateNew();
                obj.SetActive(false);
                _available.Push(obj);
            }
        }

        public GameObject Get(Vector3 pos, Quaternion rot)
        {
            GameObject obj = null;
            // Скипаем "мёртвые" объекты (например, уничтоженные вручную)
            while (_available.Count > 0 && obj == null)
                obj = _available.Pop();

            if (obj == null)
            {
                if (_maxSize > 0 && _totalCreated >= _maxSize)
                {
                    Debug.LogWarning($"[ObjectPool] '{_prefab.name}' достиг лимита {_maxSize}. Возвращаю null.");
                    return null;
                }
                obj = CreateNew();
            }

            obj.transform.SetParent(null, false);
            obj.transform.position = pos;
            obj.transform.rotation = rot;
            obj.SetActive(true);

            foreach (var p in obj.GetComponents<IPoolable>())
                p.OnGetFromPool();

            _inUse.Add(obj);
            return obj;
        }

        public bool Return(GameObject obj)
        {
            if (obj == null) return false;
            if (!_inUse.Remove(obj)) return false; // не наш или уже вернули

            foreach (var p in obj.GetComponents<IPoolable>())
                p.OnReturnToPool();

            obj.SetActive(false);
            obj.transform.SetParent(_parent, false);
            _available.Push(obj);
            return true;
        }

        private GameObject CreateNew()
        {
            var obj = Object.Instantiate(_prefab, _parent);
            obj.name = $"{_prefab.name}_{_totalCreated}";
            _totalCreated++;
            return obj;
        }
    }
}