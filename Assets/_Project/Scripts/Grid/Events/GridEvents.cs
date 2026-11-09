using UnityEngine;

namespace KingdomsClone.Grid.Events
{
    /// <summary>
    /// Публикуется, когда сетка полностью сгенерирована и готова к работе.
    /// </summary>
    public struct GridGeneratedEvent
    {
        public int Width;
        public int Height;
        public float CellSize;
        public Vector3 OriginWorldPosition; // Левый нижний угол карты в мире
        
        // НОВОЕ: Идеальная точка для старта (координаты клетки)
        public Vector2Int StartLocation; 
    }
}