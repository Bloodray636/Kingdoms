using System.Collections.Generic;
using UnityEngine;

namespace KingdomsClone.Grid
{
    public static class MapGenerator
    {
        public static Cell[,] Generate(int width, int height, int seed)
        {
            Cell[,] grid = new Cell[width, height];
            System.Random prng = new System.Random(seed);

            // БАГФИКС: Используем небольшие смещения (до 1000), иначе PerlinNoise ломается
            float offsetX = prng.Next(-1000, 1000);
            float offsetY = prng.Next(-1000, 1000);

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    float nx = (float)x / width;
                    float ny = (float)y / height;

                    // Шум Перлина (2 слоя: крупный рельеф и мелкие детали)
                    float noise = Mathf.PerlinNoise((nx * 4f) + offsetX, (ny * 4f) + offsetY);
                    float detail = Mathf.PerlinNoise((nx * 12f) + offsetX, (ny * 12f) + offsetY);
                    float finalNoise = (noise + (detail * 0.2f)) / 1.2f;

                    // Falloff (остров)
                    float distanceToCenter = Vector2.Distance(new Vector2(nx, ny), new Vector2(0.5f, 0.5f));
                    float falloff = Mathf.Clamp01((distanceToCenter - 0.1f) * 2f); 

                    // БАГФИКС: Умножаем, а не вычитаем. Это гарантирует остров.
                    float elevation = finalNoise * (1f - falloff);

                    // Распределение тайлов (пороги настроены под новый elevation)
                    TileType type = TileType.Grass;
                    float heightInMeters = 1f;

                    if (elevation < 0.15f) {
                        type = TileType.Water;
                        heightInMeters = 0.2f;
                    }
                    else if (elevation < 0.25f) {
                        type = TileType.Fertile;
                        heightInMeters = 1f;
                    }
                    else if (elevation >= 0.75f) {
                        type = TileType.Rock;
                        heightInMeters = 3f + (elevation * 2f); // Горы высокие
                    }
                    else if (elevation >= 0.55f) {
                        // Лес растет на возвышенностях
                        if (prng.NextDouble() < 0.7) {
                            type = TileType.Forest;
                            heightInMeters = 1.5f;
                        } else {
                            heightInMeters = 1.2f;
                        }
                    }

                    grid[x, y] = new Cell(x, y, type, heightInMeters);
                }
            }

            // Запускаем проверку связности (Flood Fill)
            EnsurePlayableMap(grid, width, height);

            return grid;
        }

        /// <summary>
        /// Находит самый большой остров и заставляет все остальные острова утонуть.
        /// </summary>
        private static void EnsurePlayableMap(Cell[,] grid, int width, int height)
        {
            bool[,] visited = new bool[width, height];
            List<Vector2Int> largestIsland = new List<Vector2Int>();

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    if (!visited[x, y] && grid[x, y].IsBuildable())
                    {
                        List<Vector2Int> island = FloodFill(grid, x, y, visited, width, height);
                        if (island.Count > largestIsland.Count)
                        {
                            largestIsland = island;
                        }
                    }
                }
            }

            bool[,] isPartOfMainIsland = new bool[width, height];
            foreach (var pos in largestIsland)
                isPartOfMainIsland[pos.x, pos.y] = true;

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    if (!grid[x, y].IsWalkable() && !isPartOfMainIsland[x, y])
                    {
                        grid[x, y].Type = TileType.Water;
                        grid[x, y].Elevation = 0.2f;
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

                    if (nx >= 0 && nx < width && ny >= 0 && ny < height && !visited[nx, ny])
                    {
                        if (grid[nx, ny].IsBuildable()) 
                        {
                            visited[nx, ny] = true;
                            queue.Enqueue(new Vector2Int(nx, ny));
                        }
                    }
                }
            }
            return island;
        }
    }
}