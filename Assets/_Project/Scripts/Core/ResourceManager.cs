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

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            
            var data = GameDataSO.Instance;
            if (data != null)
            {
                foreach (var res in data.Resources)
                    _resources[res.Id] = 0;
            }
        }

        public int GetAmount(ResourceDataSO resource) => _resources.TryGetValue(resource.Id, out var amt) ? amt : 0;

        public void AddResource(ResourceDataSO resource, int amount)
        {
            if (!_resources.ContainsKey(resource.Id)) _resources[resource.Id] = 0;
            _resources[resource.Id] += amount;
            
            EventBus.Publish(new ResourceChangedEvent { Resource = resource, NewAmount = _resources[resource.Id] });
            // Логируем только в дебаге, чтобы не спамить консоль
            Debug.Log($"[Resource] +{amount} {resource.DisplayName} (Total: {_resources[resource.Id]})");
        }

        public void AddAllResources(int amount)
        {
            var data = GameDataSO.Instance;
            if (data == null) return;
            foreach (var res in data.Resources) AddResource(res, amount);
        }
    }
}