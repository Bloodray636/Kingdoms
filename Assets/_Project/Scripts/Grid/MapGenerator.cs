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

            float offsetX = (float)prng.NextDouble() * 1000f;
            float offsetY = (float)prng.NextDouble() * 1000f;

            // ═══════ 1. БАЗОВАЯ ЗЕМЛЯ + КАМНИ ═══════
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    float nx = (float)x / width;
                    float ny = (float)y / height;

                    float biomeNoise = Mathf.PerlinNoise((nx * 10f) + offsetX + 50f, (ny * 10f) + offsetY + 50f);
                    float detailNoise = Mathf.PerlinNoise((nx * 22f) + offsetX + 100f, (ny * 22f) + offsetY + 100f);
                    float combined = (biomeNoise * 0.75f) + (detailNoise * 0.25f);

                    TileType type = TileType.Grass;
                    float elevation = 1f + combined * 0.6f;

                    if (combined > 0.60f && prng.NextDouble() < 0.45)
                    {
                        type = TileType.Rock;
                        elevation = 1.8f + combined * 0.5f;
                    }

                    grid[x, y] = new Cell(x, y, type, elevation);
                }
            }

            // ═══════ 2. РЕКА ═══════
            float riverBaseX = width * 0.5f;
            float noiseAmplitude = width * 0.08f;
            float baseRiverWidth = 10f;

            float pondY = height * 0.5f;
            float pondRadius = height * 0.15f;
            float pondExtraWidth = 30f;

            float riverCenterNoiseOffset = offsetX + 200f;
            float riverWidthNoiseOffset = offsetY + 300f;

            for (int y = 0; y < height; y++)
            {
                float ny = (float)y / height;

                float bigWave   = Mathf.Sin(y * 0.055f) * (width * 0.16f);
                float medWave   = Mathf.Sin(y * 0.18f) * (width * 0.07f);
                float smallWave = Mathf.Sin(y * 0.55f) * (width * 0.02f);
                float noiseWave = (Mathf.PerlinNoise(0f, (ny * 8f) + riverCenterNoiseOffset) - 0.5f) * 2f * noiseAmplitude;

                float centerX = riverBaseX + bigWave + medWave + smallWave + noiseWave;

                float widthNoise = Mathf.PerlinNoise(0f, (ny * 5f) + riverWidthNoiseOffset) - 0.5f;
                float riverWidth = baseRiverWidth * (1f + widthNoise * 2f * 0.30f);

                float distToPond = Mathf.Abs(y - pondY);
                if (distToPond < pondRadius)
                {
                    float t = 1f - (distToPond / pondRadius);
                    riverWidth += pondExtraWidth * Mathf.SmoothStep(0f, 1f, t);
                }

                int minX = Mathf.Max(0, Mathf.FloorToInt(centerX - riverWidth * 0.5f - 2f));
                int maxX = Mathf.Min(width - 1, Mathf.CeilToInt(centerX + riverWidth * 0.5f + 2f));

                for (int x = minX; x <= maxX; x++)
                {
                    float edgeNoise = (Mathf.PerlinNoise((x * 0.4f) + offsetX, (y * 0.4f) + offsetY) - 0.5f) * 2f;
                    float distToCenter = Mathf.Abs(x - centerX);
                    float effectiveHalfWidth = (riverWidth * 0.5f) + edgeNoise * 1.2f;

                    if (distToCenter <= effectiveHalfWidth)
                    {
                        var cell = grid[x, y];
                        cell.Type = TileType.Water;
                        cell.Elevation = 0.3f;
                        cell.UpdateMovementCost();
                    }
                }
            }

            // ═══════ 3. ЛЕС — ОДИН МАССИВ В ОДНОМ УГЛУ ═══════
            int forestCorner = prng.Next(4);
            float forestCenterX, forestCenterY;
            switch (forestCorner)
            {
                case 0: forestCenterX = width * 0.25f; forestCenterY = height * 0.25f; break;
                case 1: forestCenterX = width * 0.25f; forestCenterY = height * 0.75f; break;
                case 2: forestCenterX = width * 0.75f; forestCenterY = height * 0.25f; break;
                default: forestCenterX = width * 0.75f; forestCenterY = height * 0.75f; break;
            }

            float forestRadius = Mathf.Sqrt(width * height * 0.40f / Mathf.PI);
            const float forestThreshold = 0.35f;

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    var cell = grid[x, y];
                    if (cell.Type == TileType.Rock || cell.Type == TileType.Water) continue;

                    float dx = x - forestCenterX;
                    float dy = y - forestCenterY;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);

                    if (dist > forestRadius * 1.6f) continue;

                    float baseStrength = 1f - (dist / forestRadius);
                    float edgeNoise = (Mathf.PerlinNoise((x * 0.08f) + offsetX + 300f, (y * 0.08f) + offsetY + 300f) - 0.5f) * 0.6f;
                    float clearingNoise = Mathf.PerlinNoise((x * 0.15f) + offsetX + 400f, (y * 0.15f) + offsetY + 400f);
                    float clearingPenalty = clearingNoise > 0.6f ? (clearingNoise - 0.6f) * 2f : 0f;

                    float forestStrength = baseStrength + edgeNoise - clearingPenalty;

                    if (forestStrength > forestThreshold)
                    {
                        cell.Type = TileType.Forest;
                        cell.Elevation = 1.3f;
                        cell.UpdateMovementCost();
                    }
                }
            }

            // ═══════ 4. БОЛОТО — ОДНО, В ЦЕНТРЕ ЛЕСА ═══════
            //
            // ДИЗАЙН:
            //   - МАЛЕНЬКОЕ: ~0.20 от forestRadius (диаметр ~14 на 100×100)
            //   - НЕРОВНОЕ: расстояние искажается domain-warping'ом — граница
            //     не круг, а органичная лужа с заливами/полуостровами
            //   - НЕТ forceSwamp: ядро тоже под шумом, просто с высоким
            //     порогом, чтобы без дырок. Cleanup потом зачищает остатки.
            //
            float swampRadius = forestRadius * 0.20f;        // ← БЫЛО 0.45, стало 0.20 (маленькое)
            float swampOuterRadius = swampRadius * 1.6f;
            float swampNoiseOffset = offsetX + 500f;
            const float swampThreshold = 0.30f;

            int swampCount = 0;

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    var cell = grid[x, y];
                    if (cell.Type == TileType.Water) continue;

                    float dx = x - forestCenterX;
                    float dy = y - forestCenterY;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);

                    // Warp: искажаем расстояние шумом. Это и есть "органичная форма".
                    // Два слоя: крупный (для формы) + мелкий (для рваности края).
                    float warpCoarse = (Mathf.PerlinNoise(
                        (x * 0.08f) + swampNoiseOffset,
                        (y * 0.08f) + swampNoiseOffset) - 0.5f) * 2f;
                    float warpFine = (Mathf.PerlinNoise(
                        (x * 0.22f) + swampNoiseOffset + 50f,
                        (y * 0.22f) + swampNoiseOffset + 50f) - 0.5f) * 2f;

                    // Смещение расстояния: до ±0.55 * swampRadius. Не круг, а лужа.
                    float warpAmount = (warpCoarse * 0.65f + warpFine * 0.35f) * swampRadius * 0.55f;
                    float warpedDist = dist + warpAmount;

                    // Жёсткая отсечка по внешнему радиусу (после warp — он реально шире)
                    if (warpedDist > swampOuterRadius) continue;

                    // Порог: чем ближе к центру, тем больше шансов стать болотом
                    float strength = 1f - (warpedDist / swampRadius);
                    if (strength <= swampThreshold) continue;

                    cell.Type = TileType.Swamp;
                    cell.Elevation = 0.5f;
                    cell.UpdateMovementCost();
                    swampCount++;
                }
            }

            // ═══════ 4.5 ЗАЧИСТКА ОСТРОВКОВ ═══════
            // На рваном крае могли остаться одиночные Grass/Forest/Rock внутри болота.
            // ≥7 (а не 6) — чтобы болото не расползлось на "здоровые" клетки у края.
            // 5 проходов — на случай цепочек.
            for (int pass = 0; pass < 5; pass++)
            {
                var toConvert = new List<Vector2Int>();
                for (int x = 1; x < width - 1; x++)
                {
                    for (int y = 1; y < height - 1; y++)
                    {
                        var cell = grid[x, y];
                        if (cell.Type == TileType.Swamp || cell.Type == TileType.Water) continue;

                        int swampNeighbors = 0;
                        for (int ddx = -1; ddx <= 1; ddx++)
                            for (int ddy = -1; ddy <= 1; ddy++)
                            {
                                if (ddx == 0 && ddy == 0) continue;
                                if (grid[x + ddx, y + ddy].Type == TileType.Swamp) swampNeighbors++;
                            }

                        if (swampNeighbors >= 7)
                            toConvert.Add(new Vector2Int(x, y));
                    }
                }

                if (toConvert.Count == 0) break;
                foreach (var c in toConvert)
                {
                    grid[c.x, c.y].Type = TileType.Swamp;
                    grid[c.x, c.y].Elevation = 0.5f;
                    grid[c.x, c.y].UpdateMovementCost();
                    swampCount++;
                }
            }

            // ═══════ 5. СТАРТОВАЯ ЗОНА ═══════
            startLocation = FindStartPosition(grid, width, height);

            Debug.Log($"[MapGenerator] Карта {width}×{height}: лес в ({forestCenterX:F0},{forestCenterY:F0}) r={forestRadius:F0}, " +
                      $"болото {swampCount} клеток, старт: {startLocation}");

            return grid;
        }

        private static Vector2Int FindStartPosition(Cell[,] grid, int width, int height)
        {
            Vector2Int best = new Vector2Int(-1, -1);
            int bestScore = -1;
            const int searchRadius = 8;

            for (int x = 5; x < width - 5; x++)
            {
                for (int y = 5; y < height - 5; y++)
                {
                    var cell = grid[x, y];
                    if (cell.Type != TileType.Grass) continue;

                    int forest = 0, water = 0, walkable = 0;
                    for (int dx = -searchRadius; dx <= searchRadius; dx++)
                    {
                        for (int dy = -searchRadius; dy <= searchRadius; dy++)
                        {
                            if (dx * dx + dy * dy > searchRadius * searchRadius) continue;
                            int nx = x + dx, ny = y + dy;
                            if (nx < 0 || nx >= width || ny < 0 || ny >= height) continue;

                            var c = grid[nx, ny];
                            if (c.Type == TileType.Forest) forest++;
                            else if (c.Type == TileType.Water) water++;
                            if (c.IsWalkable()) walkable++;
                        }
                    }

                    if (forest >= 5 && water >= 3 && walkable >= 50)
                    {
                        int score = forest + walkable;
                        if (score > bestScore)
                        {
                            bestScore = score;
                            best = new Vector2Int(x, y);
                        }
                    }
                }
            }

            if (best.x < 0)
            {
                for (int x = 5; x < width - 5; x++)
                {
                    for (int y = 5; y < height - 5; y++)
                    {
                        if (grid[x, y].Type == TileType.Grass)
                        {
                            Debug.LogWarning($"[MapGenerator] Идеальная стартовая зона не найдена, fallback: ({x},{y})");
                            return new Vector2Int(x, y);
                        }
                    }
                }
            }

            if (best.x < 0)
            {
                int cx = width / 2;
                int cy = height / 2;
                while (cx > 0 && grid[cx, cy].Type == TileType.Water) cx--;
                grid[cx, cy].Type = TileType.Grass;
                grid[cx, cy].UpdateMovementCost();
                Debug.LogError($"[MapGenerator] Нет травы! Принудительный старт ({cx},{cy})");
                return new Vector2Int(cx, cy);
            }

            return best;
        }
    }
}