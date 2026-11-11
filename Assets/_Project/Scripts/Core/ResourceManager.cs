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

        public int GetAmount(ResourceDataSO resource)
        {
            if (resource == null) return 0;
            return _resources.TryGetValue(resource.Id, out var amt) ? amt : 0;
        }

        public void AddResource(ResourceDataSO resource, int amount)
        {
            if (resource == null)
            {
                Debug.LogWarning("[ResourceManager] AddResource: resource == null");
                return;
            }
            if (!_resources.ContainsKey(resource.Id)) _resources[resource.Id] = 0;
            _resources[resource.Id] = Mathf.Max(0, _resources[resource.Id] + amount);
            
            EventBus.Publish(new ResourceChangedEvent { Resource = resource, NewAmount = _resources[resource.Id] });
        }

        public void AddAllResources(int amount)
        {
            var data = GameDataSO.Instance;
            if (data == null) return;
            foreach (var res in data.Resources) AddResource(res, amount);
        }

        /// <summary>
        /// Хватает ли ресурсов на постройку по списку стоимости.
        /// </summary>
        public bool HasResources(IReadOnlyList<ResourceAmount> cost)
        {
            if (cost == null || cost.Count == 0) return true;
            foreach (var ra in cost)
            {
                if (ra == null || ra.Resource == null) continue;
                if (GetAmount(ra.Resource) < ra.Amount) return false;
            }
            return true;
        }

        /// <summary>
        /// Списать ресурсы. Атомарно: либо списывается всё, либо ничего.
        /// </summary>
        public bool SpendResources(IReadOnlyList<ResourceAmount> cost)
        {
            if (!HasResources(cost)) return false;
            if (cost == null || cost.Count == 0) return true;

            // Сначала убеждаемся, что все ресурсы не null (защита от битого конфига)
            foreach (var ra in cost)
            {
                if (ra == null) continue;
                if (ra.Resource == null)
                {
                    Debug.LogWarning("[ResourceManager] SpendResources: null Resource в косте!");
                    return false;
                }
            }

            // Теперь списываем — всё гарантированно пройдёт
            foreach (var ra in cost)
            {
                if (ra == null || ra.Resource == null) continue;
                _resources[ra.Resource.Id] -= ra.Amount;
                EventBus.Publish(new ResourceChangedEvent
                {
                    Resource = ra.Resource,
                    NewAmount = _resources[ra.Resource.Id]
                });
            }
            return true;
        }
    }
}