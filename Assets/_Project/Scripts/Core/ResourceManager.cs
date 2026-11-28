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
        [Tooltip("Включать только осознанно.")]
        [SerializeField] private bool respectMaxStack = false;

        private readonly Dictionary<string, int> _resources = new();

        public event Action<ResourceDataSO, int, int> OnResourceChanged;
        public event Action<ResourceDataSO, int, int> OnResourceSpendFailed;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            var data = GameDataSO.Instance;
            if (data != null) {
                foreach (var res in data.Resources) {
                    if (res != null) {
                        _resources[res.Id] = 0;
                    }
                }
            }
        }

        private void OnDestroy() { if (Instance == this) Instance = null; }

        public int GetAmount(ResourceDataSO resource)
        {
            if (resource == null) return 0;
            return _resources.TryGetValue(resource.Id, out var amt) ? amt : 0;
        }

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

        public int GetFreeSpace(ResourceDataSO resource)
        {
            if (resource == null) return 0;

            if (!respectMaxStack || resource.MaxStack <= 0) {
                return int.MaxValue - GetAmount(resource);
            }
            
            return Mathf.Max(0, resource.MaxStack - GetAmount(resource));
        }

        public bool CanAdd(ResourceDataSO resource, int amount)
        {
            if (resource == null || amount <= 0) return false;
            return GetFreeSpace(resource) >= amount;
        }

        public int AddResource(ResourceDataSO resource, int amount)
        {
            if (resource == null) { 
                Debug.LogWarning("[ResourceManager] AddResource: resource == null"); 
                return 0; 
            }

            if (amount < 0) {
                Debug.LogError($"[ResourceManager] AddResource с отрицательным amount ({amount}) для '{resource.Id}'.");
                return 0;
            }

            if (amount == 0) return 0;

            if (!_resources.ContainsKey(resource.Id)) {
                _resources[resource.Id] = 0;
            }

            int oldAmount = _resources[resource.Id];
            long newAmountLong = (long)oldAmount + amount;

            int newAmount = newAmountLong > int.MaxValue ? int.MaxValue : (int)newAmountLong;

            if (respectMaxStack && resource.MaxStack > 0) {
                int capped = Mathf.Min(newAmount, resource.MaxStack);
                int lost = newAmount - capped;

                if (lost > 0) {
                    Debug.LogWarning($"[ResourceManager] '{resource.Id}': потеряно {lost} (MaxStack={resource.MaxStack}).");
                    newAmount = capped;
                }
            }

            if (newAmount == oldAmount) return 0;

            _resources[resource.Id] = newAmount;
            PublishChange(resource, oldAmount, newAmount);
            return newAmount - oldAmount;
        }

        public int AddAllResources(int amount)
        {
            if (amount < 0) {
                Debug.LogError($"[ResourceManager] AddAllResources с отрицательным amount ({amount}).");
                return 0;
            }

            var data = GameDataSO.Instance;

            if (data == null) return 0;

            int total = 0;

            foreach (var res in data.Resources) {
                total += AddResource(res, amount);
            }

            return total;
        }

        public bool SpendSingle(ResourceDataSO resource, int amount)
        {
            if (resource == null || amount <= 0) return false;

            int oldAmount = GetAmount(resource);

            if (oldAmount < amount) {
                PublishSpendFailed(resource, amount, oldAmount);
                return false;
            }

            int newAmount = oldAmount - amount;
            _resources[resource.Id] = newAmount;
            PublishChange(resource, oldAmount, newAmount);
            return true;
        }

        public bool HasResources(IReadOnlyList<ResourceAmount> cost)
        {
            var aggregated = AggregateCost(cost);

            if (aggregated == null) return false;

            foreach (var kvp in aggregated) {
                int have = _resources.TryGetValue(kvp.Key, out var amt) ? amt : 0;

                if (have < kvp.Value.amount) {
                    return false;
                }
            }

            return true;
        }

        public bool SpendResources(IReadOnlyList<ResourceAmount> cost)
        {
            var aggregated = AggregateCost(cost);

            if (aggregated == null) {
                Debug.LogWarning("[ResourceManager] SpendResources отклонено: невалидная стоимость.");
                return false;
            }

            if (aggregated.Count == 0) return true;

            foreach (var kvp in aggregated) {
                int have = _resources.TryGetValue(kvp.Key, out var amt) ? amt : 0;

                if (have < kvp.Value.amount) {
                    Debug.LogWarning($"[ResourceManager] SpendResources: не хватает '{kvp.Key}' " + $"(нужно {kvp.Value.amount}, есть {have})");
                    PublishSpendFailed(kvp.Value.resource, kvp.Value.amount, have);
                    return false;
                }
            }

            // Атомарное списание
            var oldAmounts = new Dictionary<string, int>(aggregated.Count);

            foreach (var kvp in aggregated) {
                oldAmounts[kvp.Key] = _resources[kvp.Key];
                _resources[kvp.Key] -= kvp.Value.amount;
            }

            foreach (var kvp in aggregated) {
                PublishChange(kvp.Value.resource, oldAmounts[kvp.Key], _resources[kvp.Key]);
            }

            return true;
        }

        private void PublishChange(ResourceDataSO resource, int oldAmount, int newAmount)
        {
            var handlers = OnResourceChanged;

            if (handlers != null) {
                foreach (Action<ResourceDataSO, int, int> h in handlers.GetInvocationList()) {
                    try { 
                        h(resource, oldAmount, newAmount); 
                    } catch (Exception ex) {
                        Debug.LogError($"[ResourceManager] OnResourceChanged handler '{h.Target?.GetType().Name}.{h.Method.Name}' " + $"для '{resource?.Id}' ({oldAmount}→{newAmount}) бросил исключение: {ex}");
                    }
                }
            }

            EventBus.Publish(new ResourceChangedEvent {
                Resource = resource,
                OldAmount = oldAmount,
                NewAmount = newAmount
            });
        }

        private void PublishSpendFailed(ResourceDataSO resource, int needed, int have)
        {
            var handlers = OnResourceSpendFailed;

            if (handlers != null) {
                foreach (Action<ResourceDataSO, int, int> h in handlers.GetInvocationList()) {
                    try { 
                        h(resource, needed, have); 
                    } catch (Exception ex) {
                        Debug.LogError($"[ResourceManager] OnResourceSpendFailed handler '{h.Target?.GetType().Name}.{h.Method.Name}' " + $"для '{resource?.Id}' бросил исключение: {ex}");
                    }
                }
            }
        }

        private Dictionary<string, (ResourceDataSO resource, int amount)> AggregateCost(IReadOnlyList<ResourceAmount> cost)
        {
            var result = new Dictionary<string, (ResourceDataSO resource, int amount)>();
            if (cost == null) return result;

            foreach (var ra in cost) {
                if (ra == null) {
                    Debug.LogError("Cost содержит null ResourceAmount.");
                    return null;
                }

                if (ra.Resource == null) {
                    Debug.LogError("Cost содержит ResourceAmount с null Resource.");
                    return null;
                }

                if (ra.Amount < 0) {
                    Debug.LogError($"Отрицательный amount ({ra.Amount}) для '{ra.Resource.Id}' — ЗАПРЕЩЕНО.");
                    return null;
                }

                if (ra.Amount == 0) continue;

                string id = ra.Resource.Id;

                if (string.IsNullOrEmpty(id)) {
                    Debug.LogError($"Resource '{ra.Resource.name}' имеет пустой Id — стоимость невалидна.");
                    return null;
                }

                long current = result.TryGetValue(id, out var v) ? v.amount : 0;
                long sum = current + ra.Amount;

                if (sum > int.MaxValue) {
                    Debug.LogError($"[ResourceManager] Сумма '{id}' > int.MaxValue.");
                    return null;
                }

                ResourceDataSO representative = result.TryGetValue(id, out var existing) ? existing.resource : ra.Resource;
                result[id] = (representative, (int)sum);
            }
            return result;
        }
    }
}