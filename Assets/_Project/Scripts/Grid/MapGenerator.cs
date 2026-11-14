using System.Collections.Generic;
using UnityEngine;

namespace KingdomsClone.Grid
{
    public static class MapGenerator
    {
        public static Cell[,] Generate(int width, int height, int seed, out Vector2Int startLocation)
        {
            Cell[,] grid = new Cell[width, height];
            System.Random prng = new System.Random(seed);

            // Смещения для уникальности карты
            float offsetX = (float)prng.NextDouble() * 1000f;
            float offsetY = (float)prng.NextDouble() * 1000f;

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    float nx = (float)x / width;
                    float ny = (float)y / height;

                    // 1. Генерируем высоту (остров)
                    float noise = Mathf.PerlinNoise((nx * 5f) + offsetX, (ny * 5f) + offsetY);
                    float detail = Mathf.PerlinNoise((nx * 15f) + offsetX, (ny * 15f) + offsetY);
                    float finalNoise = (noise * 0.8f) + (detail * 0.2f);

                    float distToCenter = Vector2.Distance(new Vector2(nx, ny), new Vector2(0.5f, 0.5f));
                    float mask = Mathf.Clamp01(1f - (distToCenter * 2f)); 
                    float elevation = finalNoise * mask;

                    TileType type = TileType.Grass;

                    // 2. Распределяем тайлы
                    if (elevation < 0.15f) 
                    {
                        type = TileType.Water;
                    }
                    else if (elevation < 0.20f) 
                    {
                        type = TileType.Fertile; // Пляж
                    }
                    else 
                    {
                        // ФИКС: Отдельный шум для биомов (камни и лес), не зависящий от высоты!
                        float biomeNoise = Mathf.PerlinNoise((nx * 10f) + offsetX + 50f, (ny * 10f) + offsetY + 50f);
                        
                        if (biomeNoise > 0.72f) 
                            type = TileType.Rock;       // Камни
                        else if (biomeNoise > 0.55f) 
                            type = TileType.Forest;     // Лес
                        else 
                            type = TileType.Grass;      // Трава
                    }

                    grid[x, y] = new Cell(x, y, type, elevation);
                }
            }

            // 3. Очистка от изолированных островов (Камни сохраняются!)
            EnsurePlayableMap(grid, width, height);

            // 4. Поиск стартовой позиции
            startLocation = FindStartPosition(grid, width, height);

            return grid;
        }

        /// <summary>
        /// Суша = всё, что не вода. Используется ДЛЯ ПОИСКА ОСТРОВА.
        /// Камни — это суша, просто непроходимая для юнитов.
        /// </summary>
        private static bool IsLand(Cell c) => c.Type != TileType.Water;

        /// <summary>
        /// Находит самый большой остров (по суше) и топит всё, что не в нём.
        /// Камни внутри острова сохраняются. 
        /// </summary>
        private static void EnsurePlayableMap(Cell[,] grid, int width, int height)
        {
            bool[,] visited = new bool[width, height];
            List<Vector2Int> largestIsland = new List<Vector2Int>();

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    // ФИКС: Ищем по IsLand (не вода), а не по IsWalkable (не вода + не камень)
                    if (!visited[x, y] && IsLand(grid[x, y]))
                    {
                        List<Vector2Int> island = FloodFill(grid, x, y, visited, width, height);
                        if (island.Count > largestIsland.Count)
                            largestIsland = island;
                    }
                }
            }

            if (largestIsland.Count == 0) return;

            bool[,] isPartOfMainIsland = new bool[width, height];
            foreach (var pos in largestIsland)
                isPartOfMainIsland[pos.x, pos.y] = true;

            // Топим всё, что не в главном острове
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    if (!isPartOfMainIsland[x, y])
                    {
                        grid[x, y].Type = TileType.Water;
                        grid[x, y].Elevation = 0f;
                    }
                }
            }
        }

        private static List<Vector2Int> FloodFill(Cell[,] grid, int startX, int startY, bool[,] visited, int width, int height)
        {
            List<Vector2Int> island = new List<Vector2Int>();
            Queue<Vector2Int> queue = new Queue<Vector2Int>();
            queue.Enqueue(new Vector2Int(startX, startY));
            visited[startX, startY] = true;

            int[] dx = { 0, 0, 1, -1 };
            int[] dy = { 1, -1, 0, 0 };

            while (queue.Count > 0)
            {
                Vector2Int current = queue.Dequeue();
                island.Add(current);

                for (int i = 0; i < 4; i++)
                {
                    int nx = current.x + dx[i];
                    int ny = current.y + dy[i];
                    if (nx < 0 || nx >= width || ny < 0 || ny >= height) continue;
                    if (visited[nx, ny]) continue;

                    // ФИКС: Идём по суше (IsLand), камни становятся частью острова
                    if (IsLand(grid[nx, ny]))
                    {
                        visited[nx, ny] = true;
                        queue.Enqueue(new Vector2Int(nx, ny));
                    }
                }
            }
            return island;
        }

        private static Vector2Int FindStartPosition(Cell[,] grid, int width, int height)
        {
            int cx = width / 2;
            int cy = height / 2;

            // 1. Ищем траву в центре
            for (int x = width / 4; x < width * 3 / 4; x++)
            {
                for (int y = height / 4; y < height * 3 / 4; y++)
                {
                    if (grid[x, y].Type == TileType.Grass) return new Vector2Int(x, y);
                }
            }

            // 2. ФИКС 2.3: расширяющийся поиск по IsBuildable (не только трава)
            for (int radius = 1; radius < Mathf.Max(width, height); radius++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    for (int dy = -radius; dy <= radius; dy++)
                    {
                        if (Mathf.Abs(dx) != radius && Mathf.Abs(dy) != radius) continue;
                        int x = cx + dx;
                        int y = cy + dy;
                        if (x < 0 || x >= width || y < 0 || y >= height) continue;
                        if (grid[x, y].IsBuildable()) return new Vector2Int(x, y);
                    }
                }
            }

            // 3. На карте вообще нет строибельных клеток — это ошибка генерации
            Debug.LogError("[MapGenerator] На карте нет строибельных клеток! Старт — центр, но игра будет сломана.");
            return new Vector2Int(cx, cy);
        }
    }
}