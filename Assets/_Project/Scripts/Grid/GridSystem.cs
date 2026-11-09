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
        [Tooltip("Смещение сетки в мире. Если (0,0), то левый нижний угол клетки (0,0) будет в мировых координатах (0,0,0).")]
        [SerializeField] private Vector3 originPosition = Vector3.zero;

        [Header("Map Generation")]
        [Tooltip("Сид для генератора. Одинаковый сид = одинаковая карта. Меняй, чтобы получить новую.")]
        [SerializeField] private int mapSeed = 12345;

        private Cell[,] _grid;

        public int Width => width;
        public int Height => height;
        public float CellSize => cellSize;
        public Cell[,] Grid => _grid;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            GenerateMap();
        }

        /// <summary>
        /// Вызывает процедурный генератор карт (Perlin Noise + Flood Fill).
        /// </summary>
        private void GenerateMap()
        {
            // Вызываем наш новый генератор, передавая ему размеры и сид
            _grid = MapGenerator.Generate(width, height, mapSeed);

            Debug.Log($"[GridSystem] Карта {width}x{height} сгенерирована. Сид: {mapSeed}");

            // Сообщаем всем системам (камера, визуализатор), что карта готова
            EventBus.Publish(new GridGeneratedEvent
            {
                Width = width,
                Height = height,
                CellSize = cellSize,
                OriginWorldPosition = originPosition
            });
        }

        // ---------- Конвертация координат ----------

        /// <summary>
        /// Возвращает мировые координаты ЦЕНТРА клетки.
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

        /// <summary>
        /// Возвращает координаты клетки по мировой позиции.
        /// </summary>
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
            if (!IsValidCell(x, y)) return null;
            return _grid[x, y];
        }

        // ---------- Визуализация в редакторе ----------
        private void OnDrawGizmos()
        {
            if (!Application.isPlaying) return;

            Gizmos.color = new Color(1f, 1f, 1f, 0.2f);

            // Рисуем только внешние границы и линии каждые 10 клеток, чтобы не убить FPS редактора
            for (int x = 0; x <= width; x += 10)
            {
                Vector3 start = originPosition + new Vector3(x * cellSize, 0f, 0f);
                Vector3 end = originPosition + new Vector3(x * cellSize, 0f, height * cellSize);
                Gizmos.DrawLine(start, end);
            }
            for (int y = 0; y <= height; y += 10)
            {
                Vector3 start = originPosition + new Vector3(0f, 0f, y * cellSize);
                Vector3 end = originPosition + new Vector3(width * cellSize, 0f, y * cellSize);
                Gizmos.DrawLine(start, end);
            }

            // Рисуем центр клетки (0,0) красным для ориентира
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(CellToWorld(0, 0), 0.5f);
        }
    }
}