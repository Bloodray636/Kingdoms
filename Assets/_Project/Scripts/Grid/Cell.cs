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
        public bool OccupiedByIsHousing { get; set; }

        public float MovementCost { get; set; }

        // сохраняем исходный тип клетки для корректного сноса дороги
        public TileType PreviousType { get; set; } = TileType.Grass;

        public Cell(int x, int y, TileType type = TileType.Grass, float elevation = 1f)
        {
            X = x;
            Y = y;
            Type = type;
            Elevation = elevation;
            OccupiedBy = null;
            OccupiedByIsHousing = false;
            PreviousType = type;
            UpdateMovementCost();
        }

        public void UpdateMovementCost()
        {
            switch (Type)
            {
                case TileType.Road: MovementCost = 2f;   break;
                case TileType.Grass: MovementCost = 10f;  break;
                case TileType.Fertile: MovementCost = 10f;  break;
                case TileType.Forest: MovementCost = 15f;  break;
                case TileType.Swamp: MovementCost = 999f; break; 
                case TileType.Rock: MovementCost = 999f; break;
                case TileType.Water: MovementCost = 999f; break;
                default: MovementCost = 10f;  break;
            }
        }

        public bool IsWalkable()
        {
            if (Type == TileType.Water) return false;
            if (Type == TileType.Rock) return false;
            if (Type == TileType.Swamp) return false; 
            return true;
        }

        public bool IsTraversable()
        {
            if (Type == TileType.Water || Type == TileType.Rock || Type == TileType.Swamp) return false;
            if (OccupiedBy != null && OccupiedByIsHousing) return true;
            if (OccupiedBy != null) return false;
            return true;
        }

        public bool IsBuildable()
        {
            return Type == TileType.Grass || Type == TileType.Fertile;
        }

        public bool IsFree() => OccupiedBy == null;

        public bool IsLethalForVillager() => Type == TileType.Swamp;
    }
}