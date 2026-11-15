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
        public bool OccupiedByIsHousing { get; set; }  // НОВОЕ

        public float MovementCost { get; set; }

        public Cell(int x, int y, TileType type = TileType.Grass, float elevation = 1f)
        {
            X = x;
            Y = y;
            Type = type;
            Elevation = elevation;
            OccupiedBy = null;
            OccupiedByIsHousing = false;
            UpdateMovementCost();
        }

        public void UpdateMovementCost()
        {
            switch (Type)
            {
                case TileType.Road:    MovementCost = 2f;   break;
                case TileType.Grass:   MovementCost = 10f;  break;
                case TileType.Fertile: MovementCost = 10f;  break;
                case TileType.Forest:  MovementCost = 15f;  break;
                case TileType.Rock:    MovementCost = 999f; break;
                case TileType.Water:   MovementCost = 999f; break;
                default:               MovementCost = 10f;  break;
            }
        }

        public bool IsWalkable() => Type != TileType.Water && Type != TileType.Rock;

        /// <summary>
        /// Текущая проходимость для A*. Учитывает тип тайла + занятость зданиями.
        /// ФИКС: housing-здания проходимы — жители там живут, могут входить и выходить.
        /// Остальные здания (production, storage, defense) блокируют путь.
        /// </summary>
        public bool IsTraversable()
        {
            if (Type == TileType.Water || Type == TileType.Rock) return false;
            if (OccupiedBy != null && !OccupiedByIsHousing) return false;
            return true;
        }

        public bool IsBuildable() => Type == TileType.Grass || Type == TileType.Fertile;
        public bool IsFree() => OccupiedBy == null;
    }
}