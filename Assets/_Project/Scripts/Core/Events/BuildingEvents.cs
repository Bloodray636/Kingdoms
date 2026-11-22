using UnityEngine;
using KingdomsClone.Data;

namespace KingdomsClone.Core.Events
{
    public struct BuildingPlacedEvent
    {
        public BuildingDataSO Data;
        public Vector2Int Position;
    }

    public struct BuildingRemovedEvent
    {
        public BuildingDataSO Data;
        public Vector2Int Position;
    }

    public struct BuildingPlacementRejectedEvent
    {
        public BuildingDataSO Data;
        public Vector2Int Position;
        public string Reason;
    }
}