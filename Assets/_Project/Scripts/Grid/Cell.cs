using UnityEngine;

namespace KingdomsClone.Grid
{
    /// <summary>
    /// Одна клетка сетки. Хранит свои координаты и тип тайла.
    /// В будущем сюда добавим ссылки на здание и юнитов.
    /// </summary>
    public class Cell
    {
        public int X { get; private set; }
        public int Y { get; private set; }
        public TileType Type { get; set; }

        public Cell(int x, int y, TileType type = TileType.Grass)
        {
            X = x;
            Y = y;
            Type = type;
        }

        public bool IsWalkable() => Type != TileType.Water && Type != TileType.Rock;
        public bool IsBuildable() => Type != TileType.Water; // На камне может быть запрещено в будущем
    }
}