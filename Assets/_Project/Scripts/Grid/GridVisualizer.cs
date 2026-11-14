using UnityEngine;
using UnityEngine.InputSystem;
using KingdomsClone.Grid;

namespace KingdomsClone.Grid
{
    public class GridVisualizer : MonoBehaviour
    {
        [Header("Materials")]
        [SerializeField] private Material highlightMaterial;
        [SerializeField] private Material fishingMarkerMaterial;

        [Header("Grid Lines")]
        [SerializeField] private bool showLines = true;
        [SerializeField] private Color lineColor = new Color(1f, 1f, 1f, 0.2f);
        [Tooltip("Рисовать линию каждые N клеток")]
        [SerializeField] private int lineStep = 5;
        [Tooltip("Высота линий над землёй (в метрах)")]
        [SerializeField] private float lineYOffset = 0.15f;

        [Header("Fishing Markers")]
        [SerializeField] private float fishingMarkerScale = 0.3f;
        [SerializeField] private float fishingMarkerYOffset = 0.1f;

        private Vector2Int _hoveredCell = new Vector2Int(-1, -1);
        private Mesh _quadMesh;

        private static readonly Quaternion FlatRotation = Quaternion.Euler(-90f, 0f, 0f);

        private void Awake()
        {
            _quadMesh = GenerateQuadMesh();
        }

        private void OnDestroy()
        {
            if (_quadMesh != null) Destroy(_quadMesh);
        }

        private void Update()
        {
            var grid = GridSystem.Instance;
            var cam = Camera.main;
            var mouse = Mouse.current;

            if (grid == null || cam == null || mouse == null) return;

            Ray ray = cam.ScreenPointToRay(mouse.position.ReadValue());
            Plane groundPlane = new Plane(Vector3.up, Vector3.zero);

            if (groundPlane.Raycast(ray, out float enter))
            {
                Vector3 hitPoint = ray.GetPoint(enter);
                _hoveredCell = grid.WorldToCell(hitPoint);
            }
            else
            {
                _hoveredCell = new Vector2Int(-1, -1);
            }
        }

        private void LateUpdate()
        {
            var grid = GridSystem.Instance;
            if (grid == null) return;

            // ========== ПОДСВЕТКА КУРСОРА ==========
            if (highlightMaterial != null && grid.IsValidCell(_hoveredCell.x, _hoveredCell.y))
            {
                Vector3 pos = grid.CellToWorld(_hoveredCell.x, _hoveredCell.y);
                pos.y = grid.GetSurfaceY(_hoveredCell.x, _hoveredCell.y) + 0.05f;

                float scale = grid.CellSize * 1.02f;
                Matrix4x4 mat = Matrix4x4.TRS(pos, FlatRotation, new Vector3(scale, scale, 1f));
                Graphics.DrawMesh(_quadMesh, mat, highlightMaterial, 0);
            }

            // ========== РЫБОЛОВНЫЕ МАРКЕРЫ ==========
            if (fishingMarkerMaterial != null)
            {
                float markerScale = fishingMarkerScale * grid.CellSize;

                for (int x = 0; x < grid.Width; x++)
                {
                    for (int y = 0; y < grid.Height; y++)
                    {
                        if (!grid.IsFishingZone(x, y)) continue;

                        Vector3 pos = grid.CellToWorld(x, y);
                        pos.y = grid.GetSurfaceY(x, y) + fishingMarkerYOffset;

                        // ФИКС: маркеры теперь тоже лежат на земле, а не стоят вертикально
                        Matrix4x4 mat = Matrix4x4.TRS(pos, FlatRotation, Vector3.one * markerScale);
                        Graphics.DrawMesh(_quadMesh, mat, fishingMarkerMaterial, 0);
                    }
                }
            }
        }

        // Линии сетки — только в Scene View (Gizmos)
        private void OnDrawGizmos()
        {
            if (!showLines || !Application.isPlaying) return;

            var grid = GridSystem.Instance;
            if (grid == null || grid.Grid == null) return;

            Gizmos.color = lineColor;

            Vector3 origin = grid.OriginPosition;
            float cellSize = grid.CellSize;

            // ФИКС: используем реальные OriginPosition и CellSize, а не хардкод 0..Width
            float y = grid.SurfaceYOffset + lineYOffset;

            // Линии вдоль Z (по X)
            for (int x = 0; x <= grid.Width; x += lineStep)
            {
                Vector3 start = origin + new Vector3(x * cellSize, y, 0f);
                Vector3 end = origin + new Vector3(x * cellSize, y, grid.Height * cellSize);
                Gizmos.DrawLine(start, end);
            }

            // Линии вдоль X (по Z)
            for (int z = 0; z <= grid.Height; z += lineStep)
            {
                Vector3 start = origin + new Vector3(0f, y, z * cellSize);
                Vector3 end = origin + new Vector3(grid.Width * cellSize, y, z * cellSize);
                Gizmos.DrawLine(start, end);
            }
        }

        private void OnGUI()
        {
            if (_hoveredCell.x >= 0)
            {
                GUI.Box(new Rect(10, 10, 150, 25), $"Cell: X:{_hoveredCell.x} Y:{_hoveredCell.y}");
            }
        }

        private Mesh GenerateQuadMesh()
        {
            var mesh = new Mesh { name = "Highlight_Quad" };
            mesh.vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, 0), new Vector3( 0.5f, -0.5f, 0),
                new Vector3(-0.5f,  0.5f, 0), new Vector3( 0.5f,  0.5f, 0)
            };
            mesh.triangles = new[] { 0, 1, 2, 2, 1, 3 };
            mesh.RecalculateNormals();
            return mesh;
        }
    }
}