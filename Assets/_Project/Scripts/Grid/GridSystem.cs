using UnityEngine;
using KingdomsClone.Grid.Events;
using KingdomsClone.Core;
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
        [Tooltip("Смещение сетки в мире. Если (0,0,0) — левый нижний угол клетки (0,0) в мировом (0,0,0).")]
        [SerializeField] private Vector3 originPosition = Vector3.zero;

        [Header("Surface Height")]
        [Tooltip("Единый вертикальный оффсет для всех систем (поверхность земли). " +
                 "TileVisualizer, декор, дороги, здания — ВСЁ сидит на этой высоте.")]
        [SerializeField] private float surfaceYOffset = 0.05f;

        [Tooltip("Насколько вода ниже поверхности. Отрицательное значение = вода ниже.")]
        [SerializeField] private float waterDepth = -0.1f;

        [Header("Map Generation")]
        [SerializeField] private int mapSeed = 12345;

        private Cell[,] _grid;

        // ============ ПУБЛИЧНЫЕ СВОЙСТВА ============

        public int Width => width;
        public int Height => height;
        public float CellSize => cellSize;
        public Cell[,] Grid => _grid;

        public Vector3 OriginPosition => originPosition;
        public Vector2Int StartLocation { get; private set; }
        public int CurrentSeed => mapSeed;
        public float SurfaceYOffset => surfaceYOffset;
        public float WaterDepth => waterDepth;

        // ============ LIFECYCLE ============

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

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ============ ГЕНЕРАЦИЯ КАРТЫ ============

        public void RegenerateMap(int newSeed)
        {
            if (width <= 0 || height <= 0 || cellSize <= 0f)
            {
                Debug.LogError("[GridSystem] Некорректные размеры сетки!");
                return;
            }

            // ФИКС 3.2: BuildingManager обязателен — иначе старые здания останутся
            if (BuildingManager.Instance == null)
            {
                Debug.LogError("[GridSystem] КРИТИЧЕСКАЯ ОШИБКА КОНФИГУРАЦИИ: BuildingManager отсутствует в сцене! " +
                               "Перегенерация карты оставит старые здания. Добавь BuildingManager на сцену.");
                return;
            }

            mapSeed = newSeed;

            // Выпиливаем все здания (без возврата ресурсов!) перед генерацией
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
        }

        // ============ ЕДИНАЯ ВЫСОТА ПОВЕРХНОСТИ ============

        /// <summary>
        /// Единственный метод, через который ВСЕ системы получают Y поверхности.
        /// Сейчас карта визуально плоская: Elevation из данных игнорируется (задел на будущее).
        /// Вода проседает на waterDepth.
        /// НЕ считай Y поверхности вручную в других скриптах — используй только это.
        /// </summary>
        public float GetSurfaceY(int x, int y)
        {
            var cell = GetCell(x, y);
            if (cell == null) return surfaceYOffset;

            // ФИКС 3.2 из аудита: плоская карта (Elevation игнорируется, чтобы не было полос от теней).
            // Для рельефа с высотами — раскомментировать cell.Elevation вместо surfaceYOffset.
            // return cell.Elevation + surfaceYOffset;

            if (cell.Type == TileType.Water)
                return surfaceYOffset + waterDepth;

            return surfaceYOffset;
        }

        public float GetSurfaceY(Vector2Int cell) => GetSurfaceY(cell.x, cell.y);

        /// <summary>
        /// Позиция центра клетки С УЧЁТОМ высоты поверхности.
        /// Используй этот метод вместо CellToWorld, если нужен объект, стоящий НА земле.
        /// </summary>
        public Vector3 CellToWorldOnSurface(int x, int y)
        {
            Vector3 p = CellToWorld(x, y);
            p.y = GetSurfaceY(x, y);
            return p;
        }

        public Vector3 CellToWorldOnSurface(Vector2Int cell) => CellToWorldOnSurface(cell.x, cell.y);

        // ============ КОНВЕРТАЦИЯ (без высоты — только XZ) ============

        /// <summary>
        /// Центр клетки в мировых координатах (XZ). Y = 0 — используй GetSurfaceY для высоты.
        /// </summary>
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

        // ============ ДОСТУП К КЛЕТКАМ ============

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

        public bool IsCellFreeForBuilding(int x, int y)
        {
            if (!IsValidCell(x, y)) return false;
            Cell c = GetCell(x, y);
            if (c == null) return false;
            return c.IsBuildable() && c.IsFree();
        }

        // ============ РЕГИСТРАЦИЯ ЗДАНИЙ НА КЛЕТКАХ ============

        public void RegisterBuilding(int x, int y, GameObject building)
        {
            if (!IsValidCell(x, y)) return;
            var cell = GetCell(x, y);
            if (cell != null) cell.OccupiedBy = building;
        }

        /// <summary>
        /// ФИКС 2.5: опциональный expectedOwner — если клетку уже занял кто-то другой,
        /// не снимаем регистрацию. Это защищает от случая, когда один дом рушится,
        /// а на его месте уже стоит другой.
        /// </summary>
        public void UnregisterBuilding(int x, int y, GameObject expectedOwner = null)
        {
            if (!IsValidCell(x, y)) return;
            var cell = GetCell(x, y);
            if (cell == null) return;

            if (expectedOwner != null && cell.OccupiedBy != expectedOwner) return;

            cell.OccupiedBy = null;
        }

        // ============ РЫБОЛОВНЫЕ ЗОНЫ ============

        public bool IsFishingZone(int x, int y)
        {
            if (!IsValidCell(x, y)) return false;
            Cell cell = GetCell(x, y);
            if (cell == null) return false;
            if (cell.Type != TileType.Grass && cell.Type != TileType.Fertile) return false;

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