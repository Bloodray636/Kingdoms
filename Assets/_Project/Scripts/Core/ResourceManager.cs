using System.Collections.Generic;
using UnityEngine;
using KingdomsClone.Data;
using KingdomsClone.Core.Events;

namespace KingdomsClone.Core
{
    [DefaultExecutionOrder(-800)]
    public class ResourceManager : MonoBehaviour
    {
        public static ResourceManager Instance { get; private set; }

        private readonly Dictionary<string, int> _resources = new();

        [Header("Settings")]
        [Tooltip("Если включено, AddResource не даст превысить ResourceDataSO.MaxStack. " +
                 "Если суммарная ёмкость складов меньше — это уже проблема складов, а не глобального пула.")]
        [SerializeField] private bool respectMaxStack = true;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            var data = GameDataSO.Instance;
            if (data != null)
            {
                foreach (var res in data.Resources)
                    if (res != null) _resources[res.Id] = 0;
            }
        }

        // ========== Чтение ==========

        public int GetAmount(ResourceDataSO resource)
        {
            if (resource == null) return 0;
            return _resources.TryGetValue(resource.Id, out var amt) ? amt : 0;
        }

        // ========== Добавление ==========

        /// <summary>
        /// Добавить ресурс. amount ДОЛЖЕН быть >= 0. Отрицательные значения — ошибка вызывающего кода.
        /// Для списания используй SpendResources().
        /// </summary>
        public void AddResource(ResourceDataSO resource, int amount)
        {
            if (resource == null) { Debug.LogWarning("[ResourceManager] resource == null"); return; }
            if (amount < 0) { Debug.LogError($"[ResourceManager] AddResource отрицательный ({amount}). Используй SpendResources."); return; }
            if (amount == 0) return;

            if (!_resources.ContainsKey(resource.Id)) _resources[resource.Id] = 0;
            long before = _resources[resource.Id];
            long after = before + amount;

            if (respectMaxStack && resource.MaxStack > 0)
                after = System.Math.Min(after, resource.MaxStack);

            if (after > int.MaxValue) after = int.MaxValue;
            if (after == before) return;

            _resources[resource.Id] = (int)after;
            EventBus.Publish(new ResourceChangedEvent { Resource = resource, NewAmount = _resources[resource.Id] });
        }

        public void AddAllResources(int amount)
        {
            if (amount < 0)
            {
                Debug.LogError($"[ResourceManager] AddAllResources с отрицательным amount ({amount}). Проигнорировано.");
                return;
            }
            var data = GameDataSO.Instance;
            if (data == null) return;
            foreach (var res in data.Resources) AddResource(res, amount);
        }

        // ========== Проверка и списание ==========

        /// <summary>
        /// Агрегирует cost-лист в словарь "ресурс → суммарное количество".
        /// Отрицательные и нулевые записи отбрасываются с варнингом.
        /// </summary>
        private Dictionary<ResourceDataSO, int> AggregateCost(IReadOnlyList<ResourceAmount> cost)
        {
            var result = new Dictionary<ResourceDataSO, int>();
            if (cost == null) return result;

            foreach (var ra in cost)
            {
                if (ra == null)
                {
                    Debug.LogError("[ResourceManager] Cost содержит null ResourceAmount. Стоимость невалидна.");
                    return null;
                }
                if (ra.Resource == null)
                {
                    Debug.LogError("[ResourceManager] Cost содержит ResourceAmount с null Resource. Стоимость невалидна.");
                    return null;
                }
                if (ra.Amount < 0)
                {
                    Debug.LogError($"[ResourceManager] Стоимость с отрицательным amount ({ra.Amount}) для '{ra.Resource.Id}'. Стоимость невалидна.");
                    return null;
                }
                if (ra.Amount == 0) continue;

                // ФИКС 2.2: long для защиты от переполнения при суммировании
                long current = result.TryGetValue(ra.Resource, out var v) ? v : 0;
                long sum = current + ra.Amount;
                if (sum > int.MaxValue)
                {
                    Debug.LogError($"[ResourceManager] Суммарная стоимость '{ra.Resource.Id}' превышает int.MaxValue. Стоимость невалидна.");
                    return null;
                }
                result[ra.Resource] = (int)sum;
            }
            return result;
        }

        public bool HasResources(IReadOnlyList<ResourceAmount> cost)
        {
            var aggregated = AggregateCost(cost);
            if (aggregated == null) return false; // невалидная стоимость
            foreach (var kvp in aggregated)
            {
                if (GetAmount(kvp.Key) < kvp.Value) return false;
            }
            return true;
        }

        public bool SpendResources(IReadOnlyList<ResourceAmount> cost)
        {
            var aggregated = AggregateCost(cost);
            if (aggregated == null)
            {
                Debug.LogWarning("[ResourceManager] SpendResources отклонено: невалидная стоимость.");
                return false;
            }
            if (aggregated.Count == 0) return true;

            foreach (var kvp in aggregated)
            {
                if (GetAmount(kvp.Key) < kvp.Value) return false;
            }

            foreach (var kvp in aggregated)
            {
                _resources[kvp.Key.Id] -= kvp.Value;
            }

            foreach (var kvp in aggregated)
            {
                EventBus.Publish(new ResourceChangedEvent
                {
                    Resource = kvp.Key,
                    NewAmount = _resources[kvp.Key.Id]
                });
            }
            return true;
        }
    }
}