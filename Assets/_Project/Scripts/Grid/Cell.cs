using UnityEngine;

namespace KingdomsClone.Grid
{
    public class Cell
    {
        public int X { get; private set; }
        public int Y { get; private set; }
        public TileType Type { get; set; }
        
        // Новая переменная: высота клетки в метрах (от 0 до 5)
        public float Elevation { get; set; } 

        public Cell(int x, int y, TileType type = TileType.Grass, float elevation = 1f)
        {
            X = x;
            Y = y;
            Type = type;
            Elevation = elevation;
        }

        public bool IsWalkable() => Type != TileType.Water && Type != TileType.Rock;
        public bool IsBuildable() => Type != TileType.Water;
    }
}