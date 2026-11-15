using UnityEngine;

namespace KingdomsClone.Pathfinding
{
    /// <summary>
    /// Узел открытого списка A*. Хранит позицию на сетке и стоимость пути.
    /// </summary>
    public struct PathNode
    {
        public Vector2Int Pos;
        public float G; // реальная стоимость от старта
        public float F; // G + эвристика
    }
}