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

        [Header("Settings")]
        [SerializeField] private float waterYOffset = -0.1f;
        [SerializeField] private float treeYOffset = 0f;

        [Header("Density")]
        [Tooltip("Сколько деревьев на клетку (2 - идеально для RTS)")]
        [SerializeField] private int treesPerForestCell = 2; 
        [Tooltip("Разброс внутри клетки")]
        [SerializeField] private float treeSpread = 0.3f; 

        private void OnEnable() => EventBus.Subscribe<GridGeneratedEvent>(OnGridGenerated);
        private void OnDisable() => EventBus.Unsubscribe<GridGeneratedEvent>(OnGridGenerated);

        private void OnGridGenerated(GridGeneratedEvent e)
        {
            Invoke(nameof(DecorateMap), 0.1f);
        }

        private void DecorateMap()
        {
            var grid = GridSystem.Instance;
            if (grid == null) return;

            foreach (Transform child in transform) Destroy(child.gameObject);

            int treeCount = 0;

            for (int x = 0; x < grid.Width; x++)
            {
                for (int y = 0; y < grid.Height; y++)
                {
                    Cell cell = grid.GetCell(x, y);
                    if (cell == null) continue;

                    Vector3 basePos = grid.CellToWorld(x, y);

                    if (cell.Type == TileType.Forest && treePrefab != null)
                    {
                        for (int i = 0; i < treesPerForestCell; i++)
                        {
                            Vector3 randomOffset = new Vector3(
                                Random.Range(-treeSpread, treeSpread), 
                                0f, 
                                Random.Range(-treeSpread, treeSpread)
                            );
                            
                            Vector3 spawnPos = basePos + randomOffset;
                            spawnPos.y = cell.Elevation + treeYOffset;

                            Quaternion randomRot = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                            
                            // Спавним с обычным масштабом 1.0
                            Instantiate(treePrefab, spawnPos, randomRot, transform);
                            treeCount++;
                        }
                    }
                    else if (cell.Type == TileType.Water && waterPrefab != null)
                    {
                        basePos.y = cell.Elevation + waterYOffset;
                        Instantiate(waterPrefab, basePos, Quaternion.identity, transform);
                    }
                }
            }

            Debug.Log($"[MapDecorator] Деревьев: {treeCount}");
        }
    }
}