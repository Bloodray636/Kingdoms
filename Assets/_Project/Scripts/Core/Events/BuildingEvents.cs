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

    /// <summary>
    /// Публикуется, когда игрок попытался поставить здание, но не смог.
    /// Причина: "Нет ресурсов", "Занято", "Вода" и т.д.
    /// </summary>
    public struct BuildingPlacementRejectedEvent
    {
        public BuildingDataSO Data;
        public Vector2Int Position;
        public string Reason;
    }
}