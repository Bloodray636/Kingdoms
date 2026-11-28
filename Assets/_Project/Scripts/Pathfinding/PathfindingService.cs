using System.Collections.Generic;
using UnityEngine;
using KingdomsClone.Grid;
using KingdomsClone.Core;
using KingdomsClone.Core.Events;

namespace KingdomsClone.Pathfinding
{
    [DefaultExecutionOrder(-600)]
    public class PathfindingService : MonoBehaviour
    {
        public static PathfindingService Instance { get; private set; }

        [Header("Limits")]
        [SerializeField] private int maxIterations = 20000;

        [Header("Cache")]
        [SerializeField] private int maxCacheSize = 5000;
        [SerializeField] private bool drawAllCachedPaths = false;

        private readonly Dictionary<(Vector2Int start, Vector2Int goal), List<Vector2Int>> _cache = new();
        private readonly MinHeap _open = new();
        private readonly HashSet<Vector2Int> _closed = new();
        private readonly Dictionary<Vector2Int, float> _gScore = new();
        private readonly Dictionary<Vector2Int, Vector2Int> _cameFrom = new();

        private const float SQRT2 = 1.41421356f;
        private const float MIN_COST = 1f;

        private static readonly Vector2Int[] Dirs =
        {
            new( 1, 0), new(-1, 0), new( 0, 1), new( 0, -1),
            new( 1, 1), new( 1, -1), new(-1, 1), new(-1, -1),
        };

        private List<Vector2Int> _lastPath;
        private float _lastSearchMs;

        public float LastSearchMs => _lastSearchMs;
        public int CacheCount => _cache.Count;

        private void Awake()
        {
            if (Instance != null && Instance != this) { 
                Destroy(gameObject); 
                return; 
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnEnable()
        {
            EventBus.Subscribe<PathCacheClearEvent>(OnFullInvalidation);
            EventBus.Subscribe<PathInvalidationEvent>(OnGranularInvalidation);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<PathCacheClearEvent>(OnFullInvalidation);
            EventBus.Unsubscribe<PathInvalidationEvent>(OnGranularInvalidation);
        }

        private void OnDestroy() { 
            if (Instance == this) Instance = null; 
        }

        private void OnFullInvalidation(PathCacheClearEvent e)
        {
            int before = _cache.Count;
            _cache.Clear();

            if (before > 0) {
                Debug.Log($"[Pathfinding] Кэш ПОЛНОСТЬЮ очищен (было {before} путей)");
            }
        }

        private void OnGranularInvalidation(PathInvalidationEvent e)
        {
            if (e.Cells == null || e.Cells.Count == 0) {
                OnFullInvalidation(default);
                return;
            }

            var affected = new HashSet<Vector2Int>(e.Cells);
            var toRemove = new List<(Vector2Int, Vector2Int)>();

            foreach (var kvp in _cache) {
                var path = kvp.Value;

                for (int i = 0; i < path.Count; i++) {
                    if (affected.Contains(path[i])) {
                        toRemove.Add(kvp.Key);
                        break;
                    }
                }
            }

            foreach (var key in toRemove) _cache.Remove(key);

            if (toRemove.Count > 0) {
                Debug.Log($"[Pathfinding] Инвалидировано {toRemove.Count} путей (задето {e.Cells.Count} клеток)");
            }
        }

        public List<Vector2Int> FindPath(Vector2Int start, Vector2Int goal)
        {
            var grid = GridSystem.Instance;
            if (grid == null) return null;
            if (!grid.IsValidCell(start.x, start.y) || !grid.IsValidCell(goal.x, goal.y)) return null;

            var startCell = grid.GetCell(start.x, start.y);
            var goalCell = grid.GetCell(goal.x, goal.y);

            if (startCell == null || goalCell == null) return null;
            if (!startCell.IsTraversable() || !goalCell.IsTraversable()) return null;

            if (start == goal) return new List<Vector2Int> { 
                start 
            };

            var key = (start, goal);

            if (_cache.TryGetValue(key, out var cached)) return cached;

            var path = AStar(grid, start, goal);

            if (path != null) {
                if (_cache.Count >= maxCacheSize) _cache.Clear();
                _cache[key] = path;
            }

            _lastPath = path;
            return path;
        }

        private List<Vector2Int> AStar(GridSystem grid, Vector2Int start, Vector2Int goal)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();

            _open.Clear();
            _closed.Clear();
            _gScore.Clear();
            _cameFrom.Clear();

            _gScore[start] = 0f;
            _open.Push(new PathNode { Pos = start, G = 0f, F = Heuristic(start, goal) });

            int iterations = 0;

            while (_open.Count > 0) {
                if (++iterations > maxIterations) {
                    Debug.LogError($"[Pathfinding] Лимит {maxIterations} итераций для {start}→{goal}");
                    sw.Stop();
                    _lastSearchMs = (float)sw.Elapsed.TotalMilliseconds;
                    return null;
                }

                var current = _open.Pop();

                if (_closed.Contains(current.Pos)) continue;
                if (_gScore.TryGetValue(current.Pos, out var bestG) && current.G > bestG + 0.0001f) continue;

                if (current.Pos == goal) {
                    sw.Stop();
                    _lastSearchMs = (float)sw.Elapsed.TotalMilliseconds;
                    return ReconstructPath(start, goal);
                }

                _closed.Add(current.Pos);

                for (int i = 0; i < Dirs.Length; i++) {
                    var dir = Dirs[i];
                    var neighbor = current.Pos + dir;

                    if (!grid.IsValidCell(neighbor.x, neighbor.y)) continue;
                    if (_closed.Contains(neighbor)) continue;

                    var nc = grid.GetCell(neighbor.x, neighbor.y);

                    if (nc == null || !nc.IsTraversable()) continue;

                    bool isDiag = dir.x != 0 && dir.y != 0;

                    if (isDiag) {
                        var n1 = grid.GetCell(current.Pos.x + dir.x, current.Pos.y);
                        var n2 = grid.GetCell(current.Pos.x, current.Pos.y + dir.y);

                        if (n1 == null || !n1.IsTraversable()) continue;
                        if (n2 == null || !n2.IsTraversable()) continue;
                    }

                    float stepCost = nc.MovementCost * (isDiag ? SQRT2 : 1f);
                    float tentativeG = current.G + stepCost;

                    if (_gScore.TryGetValue(neighbor, out var existingG) && tentativeG >= existingG) continue;

                    _gScore[neighbor] = tentativeG;
                    _cameFrom[neighbor] = current.Pos;
                    float f = tentativeG + Heuristic(neighbor, goal);
                    _open.Push(new PathNode { Pos = neighbor, G = tentativeG, F = f });
                }
            }

            sw.Stop();
            _lastSearchMs = (float)sw.Elapsed.TotalMilliseconds;
            return null;
        }

        private static float Heuristic(Vector2Int a, Vector2Int b)
        {
            int dx = Mathf.Abs(a.x - b.x);
            int dy = Mathf.Abs(a.y - b.y);
            float octile = (dx + dy) + (SQRT2 - 2f) * Mathf.Min(dx, dy);
            return octile * MIN_COST;
        }

        private List<Vector2Int> ReconstructPath(Vector2Int start, Vector2Int goal)
        {
            var path = new List<Vector2Int>();
            var current = goal;
            path.Add(current);

            while (current != start) {
                if (!_cameFrom.TryGetValue(current, out var parent)) break;

                current = parent;
                path.Add(current);
            }

            path.Reverse();
            return path;
        }

        public void SetDrawAllCachedPaths(bool on) => drawAllCachedPaths = on;

        // Визуализация

        private void OnDrawGizmos()
        {
            var grid = GridSystem.Instance;
            if (grid == null) return;

            // Все кэшированные пути — тёмно-серые
            if (drawAllCachedPaths && _cache.Count > 0) {
                Gizmos.color = new Color(0.4f, 0.4f, 0.4f, 0.5f);

                foreach (var kvp in _cache) {
                    var path = kvp.Value;

                    for (int i = 0; i < path.Count - 1; i++) {
                        Vector3 a = grid.CellToWorld(path[i].x, path[i].y);
                        Vector3 b = grid.CellToWorld(path[i + 1].x, path[i + 1].y);
                        a.y = b.y = grid.GetSurfaceY(path[i].x, path[i].y) + 1.2f;
                        Gizmos.DrawLine(a, b);
                    }
                }
            }

            // Последний путь — жёлтый
            if (_lastPath != null && _lastPath.Count > 1)
            {
                Gizmos.color = Color.yellow;
                for (int i = 0; i < _lastPath.Count - 1; i++) {
                    Vector3 a = grid.CellToWorld(_lastPath[i].x, _lastPath[i].y);
                    Vector3 b = grid.CellToWorld(_lastPath[i + 1].x, _lastPath[i + 1].y);
                    a.y = b.y = grid.GetSurfaceY(_lastPath[i].x, _lastPath[i].y) + 1.5f;
                    Gizmos.DrawLine(a, b);
                }
            }
        }
    }
}