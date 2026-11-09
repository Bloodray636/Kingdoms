using UnityEngine;
using KingdomsClone.Grid.Events;
using KingdomsClone.Core;

namespace KingdomsClone.Grid
{
    public class MapDecorator : MonoBehaviour
    {
        [Header("Prefabs")]
        [SerializeField] private GameObject treePrefab;
        [SerializeField] private GameObject rockPrefab;

        [Header("Settings")]
        [SerializeField] private float treeHeightOffset = 0f;

        private void OnEnable() => EventBus.Subscribe<GridGeneratedEvent>(OnGridGenerated);
        private void OnDisable() => EventBus.Unsubscribe<GridGeneratedEvent>(OnGridGenerated);

        private void OnGridGenerated(GridGeneratedEvent e)
        {
            Invoke(nameof(DecorateMap), 0.2f); // Ждем, пока сетка полностью прогрузится
        }

        private void DecorateMap()
        {
            var grid = GridSystem.Instance;
            if (grid == null) return;

            // Удаляем старые деревья, если карта перегенерировалась
            foreach (Transform child in transform) Destroy(child.gameObject);

            System.Random prng = new System.Random(grid.GetHashCode());

            for (int x = 0; x < grid.Width; x++)
            {
                for (int y = 0; y < grid.Height; y++)
                {
                    Cell cell = grid.GetCell(x, y);
                    if (cell == null) continue;

                    Vector3 pos = grid.CellToWorld(x, y);
                    pos.y = cell.Elevation + treeHeightOffset;

                    if (cell.Type == TileType.Forest && treePrefab != null)
                    {
                        // Спавним деревья с небольшим разбросом внутри клетки
                        Vector3 randomOffset = new Vector3((float)prng.NextDouble() - 0.5f, 0, (float)prng.NextDouble() - 0.5f) * 0.8f;
                        GameObject tree = Instantiate(treePrefab, pos + randomOffset, Quaternion.Euler(0, (float)prng.NextDouble() * 360f, 0), transform);
                        
                        // Случайный размер
                        float scale = 0.8f + (float)prng.NextDouble() * 0.4f;
                        tree.transform.localScale = Vector3.one * scale;
                    }
                    else if (cell.Type == TileType.Rock && rockPrefab != null)
                    {
                        if (prng.NextDouble() < 0.3) // Не на каждой клетке
                        {
                            Vector3 randomOffset = new Vector3((float)prng.NextDouble() - 0.5f, 0, (float)prng.NextDouble() - 0.5f) * 0.5f;
                            GameObject rock = Instantiate(rockPrefab, pos + randomOffset, Quaternion.Euler(0, (float)prng.NextDouble() * 360f, 0), transform);
                            float scale = 0.5f + (float)prng.NextDouble() * 0.5f;
                            rock.transform.localScale = Vector3.one * scale;
                        }
                    }
                }
            }
            Debug.Log("[MapDecorator] Деревья и камни расставлены!");
        }
    }
}