using UnityEngine;

namespace KingdomsClone.Grid
{
    public class Cell
    {
        public int X { get; private set; }
        public int Y { get; private set; }
        public TileType Type { get; set; }
        public float Elevation { get; set; }
        public GameObject OccupiedBy { get; set; }

        public float MovementCost { get; set; }

        public Cell(int x, int y, TileType type = TileType.Grass, float elevation = 1f)
        {
            X = x;
            Y = y;
            Type = type;
            Elevation = elevation;
            OccupiedBy = null;
            UpdateMovementCost();
        }

        public void UpdateMovementCost()
        {
            switch (Type)
            {
                case TileType.Road:    MovementCost = 2f;  break;
                case TileType.Grass:   MovementCost = 10f; break;
                case TileType.Fertile: MovementCost = 10f; break;
                case TileType.Forest:  MovementCost = 15f; break;
                case TileType.Rock:    MovementCost = 999f; break;
                case TileType.Water:   MovementCost = 999f; break;
                default: MovementCost = 10f; break;
            }
        }

        public bool IsWalkable() => Type != TileType.Water && Type != TileType.Rock;

        // ФИКС: строить можно ТОЛЬКО на траве и плодородной земле.
        // Лес надо вырубить, камни — снести, дороги — это дороги, на них не строят.
        public bool IsBuildable()
        {
            return Type == TileType.Grass || Type == TileType.Fertile;
        }

        public bool IsFree() => OccupiedBy == null;
    }
}