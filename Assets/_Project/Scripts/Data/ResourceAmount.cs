using System;
using UnityEngine;

namespace KingdomsClone.Data
{
    [Serializable]
    public class ResourceAmount
    {
        [SerializeField] private ResourceDataSO resource;
        [SerializeField, Min(0)] private int amount;

        public ResourceDataSO Resource => resource;
        public int Amount => amount;

        public ResourceAmount() { }

        public ResourceAmount(ResourceDataSO resource, int amount)
        {
            this.resource = resource;
            this.amount = amount;
        }
    }
}
