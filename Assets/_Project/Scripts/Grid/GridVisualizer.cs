using UnityEngine;
using UnityEngine.InputSystem;
using KingdomsClone.Grid;

namespace KingdomsClone.Grid
{
    /// <summary>
    /// Рисует линии сетки, подсвечивает клетку под курсором, 
    /// показывает координаты и визуализирует рыболовные зоны.
    /// </summary>
    public class GridVisualizer : MonoBehaviour
    {
        [Header("Materials")]
        [SerializeField] private Material highlightMaterial;     // Желтый квадрат под курсором
        [SerializeField] private Material fishingMarkerMaterial; // Голубой маркер рыбалки

        [Header("Grid Lines")]
        [SerializeField] private bool showLines = true;
        [SerializeField] private Color lineColor = new Color(1f, 1f, 1f, 0.2f);
        [SerializeField] private int lineStep = 5; // Рисовать линию каждые N клеток

        private Vector2Int _hoveredCell = new Vector2Int(-1, -1);
        private Mesh _quadMesh;

        private void Awake()
        {
            _quadMesh = GenerateQuadMesh();
        }

        private void Update()
        {
            var grid = GridSystem.Instance;
            var cam = Camera.main;
            var mouse = Mouse.current;

            if (grid == null || cam == null || mouse == null) return;

            // Луч из камеры в точку мыши
            Ray ray = cam.ScreenPointToRay(mouse.position.ReadValue());
            
            // Плоскость земли (Y = 0)
            Plane groundPlane = new Plane(Vector3.up, Vector3.zero);

            if (groundPlane.Raycast(ray, out float enter))
            {
                Vector3 hitPoint = ray.GetPoint(enter);
                _hoveredCell = grid.WorldToCell(hitPoint);
            }
            else
            {
                _hoveredCell = new Vector2Int(-1, -1); // Мышь за пределами карты
            }
        }

        private void LateUpdate()
        {
            var grid = GridSystem.Instance;
            if (grid == null) return;

            // 1. Подсветка клетки под курсором
            if (highlightMaterial != null && grid.IsValidCell(_hoveredCell.x, _hoveredCell.y))
            {
                Cell cell = grid.GetCell(_hoveredCell.x, _hoveredCell.y);
                Vector3 pos = grid.CellToWorld(_hoveredCell.x, _hoveredCell.y);
                
                // Поднимаем квадрат ровно над кубом тайла
                pos.y = cell.Elevation + 0.05f;

                Quaternion rot = Quaternion.Euler(-90f, 0f, 0f);
                Matrix4x4 mat = Matrix4x4.TRS(pos, rot, new Vector3(grid.CellSize * 1.02f, grid.CellSize * 1.02f, 1f));

                Graphics.DrawMesh(_quadMesh, mat, highlightMaterial, 0);
            }

            // 2. Визуализация рыболовных зон
            if (fishingMarkerMaterial != null)
            {
                for (int x = 0; x < grid.Width; x++)
                {
                    for (int y = 0; y < grid.Height; y++)
                    {
                        if (grid.IsFishingZone(x, y))
                        {
                            Vector3 pos = grid.CellToWorld(x, y);
                            pos.y = grid.GetCell(x, y).Elevation + 0.1f; // Над землей

                            Matrix4x4 mat = Matrix4x4.TRS(pos, Quaternion.identity, Vector3.one * 0.3f);
                            Graphics.DrawMesh(_quadMesh, mat, fishingMarkerMaterial, 0);
                        }
                    }
                }
            }
        }

        // Линии сетки — рисуются в окне Scene (в Game View в URP они не видны без отдельного шейдера)
        private void OnDrawGizmos()
        {
            if (!showLines || !Application.isPlaying) return;

            var grid = GridSystem.Instance;
            if (grid == null) return;

            Gizmos.color = lineColor;
            float y = 1.05f; // Чуть выше уровня травы

            for (int x = 0; x <= grid.Width; x += lineStep)
            {
                Gizmos.DrawLine(new Vector3(x, y, 0), new Vector3(x, y, grid.Height));
            }
            for (int yPos = 0; yPos <= grid.Height; yPos += lineStep)
            {
                Gizmos.DrawLine(new Vector3(0, y, yPos), new Vector3(grid.Width, y, yPos));
            }
        }

        // Отладочные координаты в углу экрана
        private void OnGUI()
        {
            if (_hoveredCell.x >= 0)
            {
                GUI.Box(new Rect(10, 10, 150, 25), $"Cell: X:{_hoveredCell.x} Y:{_hoveredCell.y}");
            }
        }

        private Mesh GenerateQuadMesh()
        {
            Mesh mesh = new Mesh();
            mesh.name = "Highlight_Quad";
            Vector3[] vertices = new Vector3[4]
            {
                new Vector3(-0.5f, -0.5f, 0), new Vector3( 0.5f, -0.5f, 0),
                new Vector3(-0.5f,  0.5f, 0), new Vector3( 0.5f,  0.5f, 0)
            };
            int[] tris = new int[6] { 0, 1, 2, 2, 1, 3 };
            mesh.vertices = vertices;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            return mesh;
        }
    }
}