using System;
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

        [Header("Settings")]
        [Tooltip("Если включено, AddResource не даст превысить ResourceDataSO.MaxStack.")]
        [SerializeField] private bool respectMaxStack = true;

        private readonly Dictionary<string, int> _resources = new();

        // ═══════ C# EVENTS (прямая подписка для UI) ═══════

        /// <summary>
        /// Срабатывает при любом изменении ресурса (добавление или списание).
        /// Параметры: resource, oldAmount, newAmount.
        /// Для UI-панелей удобнее, чем EventBus, потому что подписка идёт напрямую.
        /// </summary>
        public event Action<ResourceDataSO, int, int> OnResourceChanged;

        /// <summary>
        /// Срабатывает, если попытка потратить ресурс провалилась.
        /// Параметры: resource, needed, currentAmount.
        /// Используется для UI-фидбэка ("Недостаточно дерева!").
        /// </summary>
        public event Action<ResourceDataSO, int, int> OnResourceSpendFailed;

        // ═══════ LIFECYCLE ═══════

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

        private void OnDestroy() { if (Instance == this) Instance = null; }

        // ═══════ ЧТЕНИЕ ═══════

        public int GetAmount(ResourceDataSO resource)
        {
            if (resource == null) return 0;
            return _resources.TryGetValue(resource.Id, out var amt) ? amt : 0;
        }

        /// <summary>
        /// Все текущие ресурсы и их количества. Для UI-панелей.
        /// </summary>
        public IReadOnlyList<(ResourceDataSO resource, int amount)> GetAll()
        {
            var list = new List<(ResourceDataSO, int)>();
            var data = GameDataSO.Instance;
            if (data == null) return list;

            foreach (var res in data.Resources)
            {
                if (res == null) continue;
                int amount = _resources.TryGetValue(res.Id, out var amt) ? amt : 0;
                list.Add((res, amount));
            }
            return list;
        }

        // ═══════ ДОБАВЛЕНИЕ ═══════

        public void AddResource(ResourceDataSO resource, int amount)
        {
            if (resource == null) { Debug.LogWarning("[ResourceManager] AddResource: resource == null"); return; }
            if (amount < 0)
            {
                Debug.LogError($"[ResourceManager] AddResource с отрицательным amount ({amount}) для '{resource.Id}'. " +
                               "Используй SpendResources() для списания.");
                return;
            }
            if (amount == 0) return;

            if (!_resources.ContainsKey(resource.Id)) _resources[resource.Id] = 0;
            int oldAmount = _resources[resource.Id];
            long newAmountLong = (long)oldAmount + amount;
            int newAmount = (int)Mathf.Min(newAmountLong, int.MaxValue);

            if (respectMaxStack && resource.MaxStack > 0)
                newAmount = Mathf.Min(newAmount, resource.MaxStack);

            if (newAmount == oldAmount) return;

            _resources[resource.Id] = newAmount;
            PublishChange(resource, oldAmount, newAmount);
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

        // ═══════ ОДИНОЧНОЕ СПИСАНИЕ ═══════

        /// <summary>
        /// Списать одиночный ресурс. Возвращает false, если не хватает.
        /// </summary>
        public bool SpendSingle(ResourceDataSO resource, int amount)
        {
            if (resource == null || amount <= 0) return false;

            int oldAmount = GetAmount(resource);
            if (oldAmount < amount)
            {
                OnResourceSpendFailed?.Invoke(resource, amount, oldAmount);
                return false;
            }

            int newAmount = oldAmount - amount;
            _resources[resource.Id] = newAmount;
            PublishChange(resource, oldAmount, newAmount);
            return true;
        }

        // ═══════ ПАКЕТНОЕ СПИСАНИЕ (АТОМАРНОЕ) ═══════

        /// <summary>
        /// Хватает ли ресурсов. Дубликаты в cost суммируются.
        /// </summary>
        public bool HasResources(IReadOnlyList<ResourceAmount> cost)
        {
            var aggregated = AggregateCost(cost);
            if (aggregated == null) return false;
            foreach (var kvp in aggregated)
                if (GetAmount(kvp.Key) < kvp.Value) return false;
            return true;
        }

        /// <summary>
        /// Атомарное списание. Либо всё, либо ничего.
        /// </summary>
        public bool SpendResources(IReadOnlyList<ResourceAmount> cost)
        {
            var aggregated = AggregateCost(cost);
            if (aggregated == null)
            {
                Debug.LogWarning("[ResourceManager] SpendResources отклонено: невалидная стоимость.");
                return false;
            }
            if (aggregated.Count == 0) return true;

            // Проверяем ВСЁ
            foreach (var kvp in aggregated)
            {
                if (GetAmount(kvp.Key) < kvp.Value)
                {
                    Debug.LogWarning($"[ResourceManager] SpendResources: не хватает '{kvp.Key.Id}' " +
                                     $"(нужно {kvp.Value}, есть {GetAmount(kvp.Key)})");
                    OnResourceSpendFailed?.Invoke(kvp.Key, kvp.Value, GetAmount(kvp.Key));
                    return false;
                }
            }

            // Списываем ВСЁ
            var oldAmounts = new Dictionary<ResourceDataSO, int>();
            foreach (var kvp in aggregated)
            {
                oldAmounts[kvp.Key] = _resources[kvp.Key.Id];
                _resources[kvp.Key.Id] -= kvp.Value;
            }

            // Публикуем ПОСЛЕ списания всех (атомарность)
            foreach (var kvp in aggregated)
                PublishChange(kvp.Key, oldAmounts[kvp.Key], _resources[kvp.Key.Id]);

            return true;
        }

        // ═══════ ВНУТРЕННИЕ ═══════

        private void PublishChange(ResourceDataSO resource, int oldAmount, int newAmount)
        {
            // C# event (прямая подписка)
            OnResourceChanged?.Invoke(resource, oldAmount, newAmount);

            // EventBus (глобальный pub/sub)
            EventBus.Publish(new ResourceChangedEvent
            {
                Resource = resource,
                OldAmount = oldAmount,
                NewAmount = newAmount
            });
        }

        /// <summary>
        /// Агрегирует cost-лист. null = невалидная стоимость (отрицательная, null ресурс).
        /// </summary>
        private Dictionary<ResourceDataSO, int> AggregateCost(IReadOnlyList<ResourceAmount> cost)
        {
            var result = new Dictionary<ResourceDataSO, int>();
            if (cost == null) return result;

            foreach (var ra in cost)
            {
                if (ra == null)
                {
                    Debug.LogError("[ResourceManager] Cost содержит null ResourceAmount.");
                    return null;
                }
                if (ra.Resource == null)
                {
                    Debug.LogError("[ResourceManager] Cost содержит ResourceAmount с null Resource.");
                    return null;
                }
                if (ra.Amount < 0)
                {
                    Debug.LogError($"[ResourceManager] Отрицательный amount ({ra.Amount}) для '{ra.Resource.Id}' — ЗАПРЕЩЕНО.");
                    return null;
                }
                if (ra.Amount == 0) continue;

                long current = result.TryGetValue(ra.Resource, out var v) ? v : 0;
                long sum = current + ra.Amount;
                if (sum > int.MaxValue)
                {
                    Debug.LogError($"[ResourceManager] Сумма '{ra.Resource.Id}' > int.MaxValue.");
                    return null;
                }
                result[ra.Resource] = (int)sum;
            }
            return result;
        }
    }
}