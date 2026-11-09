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

                    // 1. Генерируем высоту без искажений
                    float noise = Mathf.PerlinNoise((nx * 5f) + offsetX, (ny * 5f) + offsetY);
                    float detail = Mathf.PerlinNoise((nx * 15f) + offsetX, (ny * 15f) + offsetY);
                    float finalNoise = (noise * 0.8f) + (detail * 0.2f);

                    // 2. Маска острова (круглая)
                    float distToCenter = Vector2.Distance(new Vector2(nx, ny), new Vector2(0.5f, 0.5f));
                    float mask = Mathf.Clamp01(1f - (distToCenter * 2f)); // 1 в центре, 0 по краям

                    // 3. Смешиваем
                    float elevation = finalNoise * mask;

                    TileType type = TileType.Grass;

                    if (elevation < 0.15f) 
                    {
                        type = TileType.Water;
                    }
                    else if (elevation < 0.20f) 
                    {
                        type = TileType.Fertile; // Пляж
                    }
                    else if (elevation > 0.65f) 
                    {
                        type = TileType.Rock; 
                    }
                    else if (elevation > 0.40f) 
                    {
                        // Лес
                        type = TileType.Forest;
                    }

                    grid[x, y] = new Cell(x, y, type, elevation);
                }
            }

            EnsurePlayableMap(grid, width, height);
            startLocation = FindStartPosition(grid, width, height);

            return grid;
        }

        private static Vector2Int FindStartPosition(Cell[,] grid, int width, int height)
        {
            Vector2Int bestPos = new Vector2Int(width / 2, height / 2);
            for (int x = width / 4; x < width * 3 / 4; x++)
            {
                for (int y = height / 4; y < height * 3 / 4; y++)
                {
                    if (grid[x, y].Type == TileType.Grass) return new Vector2Int(x, y);
                }
            }
            return bestPos;
        }

        private static void EnsurePlayableMap(Cell[,] grid, int width, int height)
        {
            bool[,] visited = new bool[width, height];
            List<Vector2Int> largestIsland = new List<Vector2Int>();

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    if (!visited[x, y] && grid[x, y].IsWalkable())
                    {
                        List<Vector2Int> island = FloodFill(grid, x, y, visited, width, height);
                        if (island.Count > largestIsland.Count) largestIsland = island;
                    }
                }
            }

            bool[,] isPartOfMainIsland = new bool[width, height];
            foreach (var pos in largestIsland) isPartOfMainIsland[pos.x, pos.y] = true;

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
                    if (nx >= 0 && nx < width && ny >= 0 && ny < height && !visited[nx, ny] && grid[nx, ny].IsWalkable())
                    {
                        visited[nx, ny] = true;
                        queue.Enqueue(new Vector2Int(nx, ny));
                    }
                }
            }
            return island;
        }
    }
}