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

        // ФИКС: сохраняем исходный тип клетки для корректного сноса дороги
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
                case TileType.Road:    MovementCost = 2f;   break;
                case TileType.Grass:   MovementCost = 10f;  break;
                case TileType.Fertile: MovementCost = 10f;  break;
                case TileType.Forest:  MovementCost = 15f;  break;
                case TileType.Swamp:   MovementCost = 999f; break;   // ← болото: как стена
                case TileType.Rock:    MovementCost = 999f; break;
                case TileType.Water:   MovementCost = 999f; break;
                default:               MovementCost = 10f;  break;
            }
        }

        /// <summary>
        /// Базовый тайл проходим? БЕЗ учёта зданий.
        /// Для проверки "можно ли юниту пройти" — использовать IsTraversable().
        /// </summary>
        public bool IsWalkable()
        {
            // Болото — топь. Идти нельзя, только тонуть.
            if (Type == TileType.Water) return false;
            if (Type == TileType.Rock) return false;
            if (Type == TileType.Swamp) return false;   // ← ФИКС
            return true;
        }

        /// <summary>
        /// Проходимость для ЮНИТОВ: тайл + здания.
        /// Housing проходим, остальное блокирует. Болото — блокирует.
        /// </summary>
        public bool IsTraversable()
        {
            if (Type == TileType.Water || Type == TileType.Rock || Type == TileType.Swamp) return false;   // ← ФИКС
            if (OccupiedBy != null && OccupiedByIsHousing) return true;
            if (OccupiedBy != null) return false;
            return true;
        }

        /// <summary>
        /// Можно ли строить. Болото — нельзя (топь, здание утонет).
        /// </summary>
        public bool IsBuildable()
        {
            // ФИКС: явно исключаем Swamp, чтобы не полагаться на "default: false"
            return Type == TileType.Grass || Type == TileType.Fertile;
        }

        public bool IsFree() => OccupiedBy == null;

        /// <summary>
        /// Это смертельная клетка для жителя? (утопление)
        /// </summary>
        public bool IsLethalForVillager() => Type == TileType.Swamp;
    }
}