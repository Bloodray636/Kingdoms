using UnityEngine;
using KingdomsClone.Grid.Events;
using KingdomsClone.Core;

namespace KingdomsClone.Grid
{
    [DefaultExecutionOrder(-700)]
    public class GridSystem : MonoBehaviour
    {
        public static GridSystem Instance { get; private set; }

        [Header("Grid Settings")]
        [SerializeField] private int width = 100;
        [SerializeField] private int height = 100;
        [SerializeField] private float cellSize = 1f;
        
        [Header("Map Origin")]
        [SerializeField] private Vector3 originPosition = Vector3.zero;

        [Header("Map Generation")]
        [SerializeField] private int mapSeed = 12345;

        private Cell[,] _grid;

        public int Width => width;
        public int Height => height;
        public float CellSize => cellSize;
        public Cell[,] Grid => _grid;
        
        public Vector3 OriginPosition => originPosition;
        public Vector2Int StartLocation { get; private set; }
        public int CurrentSeed => mapSeed;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            RegenerateMap(mapSeed);
        }

        /// <summary>
        /// Публичный метод для перегенерации карты с новым сидом.
        /// Вызывает событие GridGeneratedEvent, на которое подписаны все визуализаторы.
        /// </summary>
        public void RegenerateMap(int newSeed)
        {
            if (width <= 0 || height <= 0 || cellSize <= 0f)
            {
                Debug.LogError("[GridSystem] Некорректные размеры сетки!");
                return;
            }

            mapSeed = newSeed;

            _grid = MapGenerator.Generate(width, height, mapSeed, out Vector2Int startPos);
            StartLocation = startPos;

            Debug.Log($"[GridSystem] Карта {width}x{height} сгенерирована. Сид: {mapSeed}. Старт: {StartLocation}");

            EventBus.Publish(new GridGeneratedEvent
            {
                Width = width,
                Height = height,
                CellSize = cellSize,
                OriginWorldPosition = originPosition,
                StartLocation = StartLocation
            });
        }

        // ---------- Конвертация координат ----------

        public Vector3 CellToWorld(int x, int y)
        {
            return new Vector3(
                x * cellSize + (cellSize * 0.5f) + originPosition.x,
                0f,
                y * cellSize + (cellSize * 0.5f) + originPosition.z
            );
        }

        public Vector3 CellToWorld(Vector2Int cell) => CellToWorld(cell.x, cell.y);

        public Vector2Int WorldToCell(Vector3 worldPos)
        {
            int x = Mathf.FloorToInt((worldPos.x - originPosition.x) / cellSize);
            int y = Mathf.FloorToInt((worldPos.z - originPosition.z) / cellSize);
            return new Vector2Int(x, y);
        }

        public bool IsValidCell(int x, int y)
        {
            return x >= 0 && y >= 0 && x < width && y < height;
        }

        public Cell GetCell(int x, int y)
        {
            if (_grid == null) return null;
            if (!IsValidCell(x, y)) return null;
            return _grid[x, y];
        }
        
        /// <summary>
        /// Можно ли строить на клетке: валидна, не вода, не занята.
        /// </summary>
        public bool IsCellFreeForBuilding(int x, int y)
        {
            if (!IsValidCell(x, y)) return false;
            Cell c = GetCell(x, y);
            if (c == null) return false;
            return c.IsBuildable() && c.IsFree();
        }

        /// <summary>
        /// Зарегистрировать здание на клетке.
        /// </summary>
        public void RegisterBuilding(int x, int y, GameObject building)
        {
            if (!IsValidCell(x, y)) return;
            var cell = GetCell(x, y);
            if (cell != null) cell.OccupiedBy = building;
        }

        /// <summary>
        /// Убрать здание с клетки (при сносе/разрушении).
        /// </summary>
        public void UnregisterBuilding(int x, int y)
        {
            if (!IsValidCell(x, y)) return;
            var cell = GetCell(x, y);
            if (cell != null) cell.OccupiedBy = null;
        }

        public bool IsFishingZone(int x, int y)
        {
            if (!IsValidCell(x, y)) return false;
            
            Cell cell = GetCell(x, y);
            if (cell == null || (cell.Type != TileType.Grass && cell.Type != TileType.Fertile)) return false;

            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    if (dx == 0 && dy == 0) continue;
                    if (IsValidCell(x + dx, y + dy) && GetCell(x + dx, y + dy).Type == TileType.Water)
                    {
                        return true;
                    }
                }
            }
            return false;
        }
    }
}