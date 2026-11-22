using System.Collections.Generic;
using UnityEngine;
using KingdomsClone.Data;

namespace KingdomsClone.Gameplay
{
    [DefaultExecutionOrder(-745)]
    public class StorageManager : MonoBehaviour
    {
        public static StorageManager Instance { 
            get; 
            private set; 
        }

        private readonly List<LocalStorage> _storages = new();
        public IReadOnlyList<LocalStorage> AllStorages => _storages;

        private void Awake()
        {
            if (Instance != null && Instance != this) { 
                Destroy(gameObject); 
                return; 
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy() { 
            if (Instance == this) {
                Instance = null;
            } 
        }

        public void Register(LocalStorage storage)
        {
            if (storage != null && !_storages.Contains(storage)) {
                _storages.Add(storage);
            }
        }

        public void Unregister(LocalStorage storage)
        {
            if (storage != null) {
                _storages.Remove(storage);
            }
        }

        public LocalStorage FindNearestWithSpace(Vector3 pos, int amount)
        {
            LocalStorage best = null;
            float bestDist = float.MaxValue;

            foreach (var s in _storages) {
                if (s == null) continue;
                if (s.Building == null || s.Building.State != BuildingState.Active) continue;
                if (!s.CanAccept(amount)) continue;

                float d = Vector3.Distance(pos, s.transform.position);

                if (d < bestDist) { 
                    bestDist = d; 
                    best = s; 
                }
            }

            return best;
        }

        public int GetTotalAmount(ResourceDataSO res)
        {
            if (res == null) return 0;

            int total = 0;

            foreach (var s in _storages) {
                if (s != null) {
                    total += s.GetAmount(res);
                }
            }

            return total;
        }

        public int GetTotalUsed()
        {
            int total = 0;

            foreach (var s in _storages) {
                if (s != null) {
                    total += s.UsedSpace;
                }
            }

            return total;
        }

        public int GetTotalCapacity()
        {
            int total = 0;

            foreach (var s in _storages) {
                if (s != null) {
                    total += s.Capacity;
                }
            }

            return total;
        }
    }
}