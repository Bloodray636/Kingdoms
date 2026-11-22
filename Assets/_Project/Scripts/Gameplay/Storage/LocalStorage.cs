using System.Collections.Generic;
using UnityEngine;
using KingdomsClone.Data;

namespace KingdomsClone.Gameplay
{
    public class LocalStorage : MonoBehaviour
    {
        [SerializeField] private int _capacity = 200;
        private readonly Dictionary<string, int> _items = new();
        private int _usedSpace;
        private BuildingBase _building;

        public int Capacity => _capacity;
        public int UsedSpace => _usedSpace;
        public int FreeSpace => _capacity - _usedSpace;
        public BuildingBase Building => _building;

        private void Awake()
        {
            _building = GetComponent<BuildingBase>();
        }

        private void OnEnable()
        {
            if (StorageManager.Instance != null) {
                StorageManager.Instance.Register(this);
            }
        }

        private void OnDisable()
        {
            if (StorageManager.Instance != null) {
                StorageManager.Instance.Unregister(this);
            }
        }

        public void Initialize(int capacity)
        {
            _capacity = Mathf.Max(1, capacity);
            _items.Clear();
            _usedSpace = 0;
        }

        public int GetAmount(ResourceDataSO res)
        {
            if (res == null) return 0;
            return _items.TryGetValue(res.Id, out var amt) ? amt : 0;
        }

        public bool CanAccept(int amount) => FreeSpace >= amount;

        public bool CanCover(ResourceDataSO res, int amount)
        {
            if (res == null || amount <= 0) return false;
            return _items.TryGetValue(res.Id, out var have) && have >= amount;
        }

        public int TryAdd(ResourceDataSO res, int amount)
        {
            if (res == null || amount <= 0) return 0;

            int accepted = Mathf.Min(amount, FreeSpace);

            if (accepted <= 0) return 0;

            if (!_items.ContainsKey(res.Id)) {
                _items[res.Id] = 0;
            }

            _items[res.Id] += accepted;
            _usedSpace += accepted;
            return accepted;
        }

        public int TryTake(ResourceDataSO res, int amount)
        {
            if (res == null || amount <= 0) return 0;
            if (!_items.TryGetValue(res.Id, out var have)) return 0;

            int taken = Mathf.Min(amount, have);
            _items[res.Id] -= taken;
            _usedSpace -= taken;

            if (_items[res.Id] <= 0) {
                _items.Remove(res.Id);
            }

            return taken;
        }

        public IReadOnlyDictionary<string, int> GetAllItems() => _items;
    }
}