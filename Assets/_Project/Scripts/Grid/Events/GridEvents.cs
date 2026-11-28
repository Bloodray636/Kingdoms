using UnityEngine;

namespace KingdomsClone.Grid.Events
{
    public struct GridGeneratedEvent
    {
        public int Width;
        public int Height;
        public float CellSize;
        public Vector3 OriginWorldPosition; 
        public Vector2Int StartLocation; 
    }
}