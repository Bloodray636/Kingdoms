using UnityEngine;
using KingdomsClone.Gameplay;

namespace KingdomsClone.Core.Events
{
    public struct VillagerDiedEvent
    {
        public string VillagerName;
        public Vector2Int Position;
        public VillagerDeathCause Cause;
    }
}