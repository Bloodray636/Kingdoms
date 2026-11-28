using UnityEngine;
using KingdomsClone.Grid.Events;
using KingdomsClone.Core;

namespace KingdomsClone.Grid
{
    public class MapDecorator : MonoBehaviour
    {
        [Header("Prefabs")]
        [SerializeField] private GameObject waterPrefab;
        [SerializeField] private GameObject treePrefab;
        [SerializeField] private GameObject rockPrefab;

        [Header("Relative Offsets (относительно GetSurfaceY)")]
        [SerializeField] private float waterYOffset = -0.05f;
        [SerializeField] private float treeYOffset = 0f;
        [SerializeField] private float rockYOffset = 0f;

        [Header("Density")]
        [SerializeField] private int treesPerForestCell = 2;
        [Tooltip("Разброс деревьев внутри клетки (в долях от CellSize, 0.45 = ±45% клетки)")]
        [SerializeField] private float treeSpread = 0.45f;
        [Tooltip("Шанс появления камня на клетке Rock (0..1). Некоторые клетки оставим пустыми для разнообразия.")]
        [SerializeField, Range(0f, 1f)] private float rockSpawnChance = 0.7f;

        [Header("Timing")]
        [Tooltip("Задержка перед декорацией — чтобы TileVisualizer успел построить меш. " + "При частой регенерации отменяется и перезапускается.")]
        [SerializeField] private float decorateDelay = 0.1f;

        private bool _isDecorating = false;

        private void OnEnable() => EventBus.Subscribe<GridGeneratedEvent>(OnGridGenerated);

        private void OnDisable()
        {
            EventBus.Unsubscribe<GridGeneratedEvent>(OnGridGenerated);
            CancelInvoke(nameof(DecorateMap));
        }

        private void OnGridGenerated(GridGeneratedEvent e)
        {
            CancelInvoke(nameof(DecorateMap));
            Invoke(nameof(DecorateMap), Mathf.Max(0f, decorateDelay));
        }

        private void DecorateMap()
        {
            if (_isDecorating) {
                Debug.LogWarning("[MapDecorator] DecorateMap уже выполняется — пропускаю повторный запуск.");
                return;
            }

            _isDecorating = true;

            try {
                DecorateMapInternal();
            } finally {
                _isDecorating = false;
            }
        }

        private void DecorateMapInternal()
        {
            var grid = GridSystem.Instance;

            if (grid == null) return;

            int cleared = 0;

            foreach (Transform child in transform) {
                Destroy(child.gameObject);
                cleared++;
            }

            System.Random prng = new System.Random(grid.CurrentSeed * 31 + 7);

            int treeCount = 0;
            int rockCount = 0;
            int waterCount = 0;

            float cellSize = grid.CellSize;
            float spread = treeSpread * cellSize;

            for (int x = 0; x < grid.Width; x++) {
                for (int y = 0; y < grid.Height; y++) {
                    Cell cell = grid.GetCell(x, y);

                    if (cell == null) continue;

                    Vector3 basePos = grid.CellToWorld(x, y);
                    float surfaceY = grid.GetSurfaceY(x, y);

                    if (cell.Type == TileType.Water && waterPrefab != null) {
                        Vector3 pos = new Vector3(basePos.x, surfaceY + waterYOffset, basePos.z);
                        Instantiate(waterPrefab, pos, Quaternion.identity, transform);
                        waterCount++;
                    } else if (cell.Type == TileType.Forest && treePrefab != null) {
                        for (int i = 0; i < treesPerForestCell; i++) {
                            float ox = Range(prng, -spread, spread);
                            float oz = Range(prng, -spread, spread);

                            Vector3 pos = new Vector3(basePos.x + ox, surfaceY + treeYOffset, basePos.z + oz);

                            float yaw = Range(prng, 0f, 360f);
                            Instantiate(treePrefab, pos, Quaternion.Euler(0f, yaw, 0f), transform);
                            treeCount++;
                        }
                    } else if (cell.Type == TileType.Rock && rockPrefab != null) {
                        if (prng.NextDouble() > rockSpawnChance) continue;

                        float ox = Range(prng, -spread * 0.5f, spread * 0.5f);
                        float oz = Range(prng, -spread * 0.5f, spread * 0.5f);
                        Vector3 pos = new Vector3(basePos.x + ox, surfaceY + rockYOffset, basePos.z + oz);
                        float yaw = Range(prng, 0f, 360f);
                        Instantiate(rockPrefab, pos, Quaternion.Euler(0f, yaw, 0f), transform);
                        rockCount++;
                    }
                }
            }

            Debug.Log($"[MapDecorator] Seed={grid.CurrentSeed} | Очищено: {cleared} | " + $"Вода: {waterCount}, Лес: {treeCount}, Камни: {rockCount}");
        }

        private static float Range(System.Random prng, float min, float max)
        {
            return min + (float)prng.NextDouble() * (max - min);
        }

        #if UNITY_EDITOR
            private void OnValidate()
            {
                if (decorateDelay < 0f) decorateDelay = 0f;
                if (treesPerForestCell < 0) treesPerForestCell = 0;
                if (treeSpread < 0f) treeSpread = 0f;
            }
        #endif
    }
}