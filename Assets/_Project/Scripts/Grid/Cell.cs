using UnityEngine;

namespace KingdomsClone.Grid
{
    public class Cell
    {
        public int X { get; private set; }
        public int Y { get; private set; }
        public TileType Type { get; set; }
        public float Elevation { get; set; }
        
        // НОВОЕ: Ссылка на здание, которое стоит на клетке (null = свободно)
        public GameObject OccupiedBy { get; set; }

        public Cell(int x, int y, TileType type = TileType.Grass, float elevation = 1f)
        {
            X = x;
            Y = y;
            Type = type;
            Elevation = elevation;
            OccupiedBy = null;
        }

        public bool IsWalkable() => Type != TileType.Water && Type != TileType.Rock;
        public bool IsBuildable() => Type != TileType.Water;
        public bool IsFree() => OccupiedBy == null;
    }
}