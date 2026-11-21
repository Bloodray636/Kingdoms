using UnityEngine;
using KingdomsClone.Grid.Events;
using KingdomsClone.Core;
using KingdomsClone.Core.Events;     // ← ФИКС: для PathCacheClearEvent
using KingdomsClone.Gameplay;

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

        [Header("Surface Height")]
        [Tooltip("Единый вертикальный оффсет для всех систем (поверхность земли).")]
        [SerializeField] private float surfaceYOffset = 0.05f;

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
        public float SurfaceYOffset => surfaceYOffset;

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

        public void RegenerateMap(int newSeed)
        {
            if (width <= 0 || height <= 0 || cellSize <= 0f)
            {
                Debug.LogError("[GridSystem] Некорректные размеры сетки!");
                return;
            }

            mapSeed = newSeed;

            if (BuildingManager.Instance == null)
            {
                Debug.LogError("[GridSystem] КРИТИЧЕСКАЯ ОШИБКА КОНФИГУРАЦИИ: BuildingManager отсутствует! " + "Перегенерация оставит старые здания. Добавь BuildingManager.");
                return;
            }

            if (VillagerManager.Instance != null)
                VillagerManager.Instance.ClearAllSilent();

            BuildingManager.Instance.ClearAllSilent();

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

            // ФИКС: инвалидация кэша путей после перегенерации
            EventBus.Publish(new PathCacheClearEvent());
        }

        // ============ ЕДИНАЯ ВЫСОТА ПОВЕРХНОСТИ ============

        public float GetSurfaceY(int x, int y)
        {
            var cell = GetCell(x, y);
            if (cell == null) return surfaceYOffset;

            if (cell.Type == TileType.Water)
                return surfaceYOffset - 0.1f;

            return surfaceYOffset;
        }

        public float GetSurfaceY(Vector2Int cell) => GetSurfaceY(cell.x, cell.y);

        // ============ КОНВЕРТАЦИЯ ============

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

        public bool IsValidCell(int x, int y) => x >= 0 && y >= 0 && x < width && y < height;

        public Cell GetCell(int x, int y)
        {
            if (_grid == null) return null;
            if (!IsValidCell(x, y)) return null;
            return _grid[x, y];
        }

        public bool IsCellFreeForBuilding(int x, int y)
        {
            if (!IsValidCell(x, y)) return false;
            Cell c = GetCell(x, y);
            if (c == null) return false;
            return c.IsBuildable() && c.IsFree();
        }

        public void RegisterBuilding(int x, int y, GameObject building, bool isHousing = false)
        {
            if (!IsValidCell(x, y)) return;
            var cell = GetCell(x, y);
            if (cell != null) 
            {
                cell.OccupiedBy = building;
                cell.OccupiedByIsHousing = isHousing;
            }
        }

        public void UnregisterBuilding(int x, int y, GameObject expectedOwner = null)
        {
            if (!IsValidCell(x, y)) return;
            var cell = GetCell(x, y);
            if (cell == null) return;
            if (expectedOwner != null && cell.OccupiedBy != expectedOwner) return;
            cell.OccupiedBy = null;
            cell.OccupiedByIsHousing = false;
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
                        return true;
                }
            }
            return false;
        }
    }
}