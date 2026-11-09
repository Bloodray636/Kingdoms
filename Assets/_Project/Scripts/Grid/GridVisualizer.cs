using UnityEngine;
using UnityEngine.InputSystem;

namespace KingdomsClone.Grid
{
    /// <summary>
    /// Рисует линии сетки в Game View, подсвечивает клетку под курсором
    /// и показывает отладочные координаты (Задача 1.3).
    /// </summary>
    public class GridVisualizer : MonoBehaviour
    {
        [Header("Visual Settings")]
        [SerializeField] private Material highlightMaterial; // Материал для подсветки
        [SerializeField] private Color lineColor = new Color(1f, 1f, 1f, 0.2f);
        [SerializeField] private bool showLines = true;

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

            // 1. Луч из камеры в точку мыши
            Ray ray = cam.ScreenPointToRay(mouse.position.ReadValue());
            
            // 2. Плоскость земли (Y = 0)
            Plane groundPlane = new Plane(Vector3.up, Vector3.zero);

            // 3. Находим пересечение
            if (groundPlane.Raycast(ray, out float enter))
            {
                Vector3 hitPoint = ray.GetPoint(enter);
                _hoveredCell = grid.WorldToCell(hitPoint);
            }
        }

        private void OnRenderObject()
        {
            if (Camera.current != Camera.main || !showLines) return;

            var grid = GridSystem.Instance;
            if (grid == null) return;

            // --- Линии сетки ---
            // Чтобы не рисовать 10 000 линий, рисуем только те, что попадают в камеру.
            // Для простоты пока рисуем каждую 5-ю линию.
            GL.PushMatrix();
            GL.LoadProjectionMatrix(Camera.main.projectionMatrix);
            GL.modelview = Camera.main.worldToCameraMatrix;

            GL.Begin(GL.LINES);
            GL.Color(lineColor);

            float y = 1.05f; // Чуть выше травы (grassHeight = 1.0)
            int step = 5;    // Рисуем линию каждые 5 клеток

            for (int x = 0; x <= grid.Width; x += step)
            {
                GL.Vertex3(x, y, 0);
                GL.Vertex3(x, y, grid.Height);
            }
            for (int yPos = 0; yPos <= grid.Height; yPos += step)
            {
                GL.Vertex3(0, y, yPos);
                GL.Vertex3(grid.Width, y, yPos);
            }

            GL.End();
            GL.PopMatrix();
        }

        private void LateUpdate()
        {
            var grid = GridSystem.Instance;
            if (grid == null || highlightMaterial == null) return;

            // --- Подсветка клетки под курсором ---
            if (grid.IsValidCell(_hoveredCell.x, _hoveredCell.y))
            {
                Cell cell = grid.GetCell(_hoveredCell.x, _hoveredCell.y);
                Vector3 pos = grid.CellToWorld(_hoveredCell.x, _hoveredCell.y);

                // Высота зависит от типа тайла
                float h = 1.05f; // Над травой
                if (cell.Type == TileType.Water) h = 0.25f;
                else if (cell.Type == TileType.Forest) h = 2.05f;
                else if (cell.Type == TileType.Rock) h = 3.05f;

                pos.y = h;

                Quaternion rot = Quaternion.Euler(-90f, 0f, 0f);
                Matrix4x4 mat = Matrix4x4.TRS(pos, rot, new Vector3(grid.CellSize * 1.02f, grid.CellSize * 1.02f, 1f));

                Graphics.DrawMesh(_quadMesh, mat, highlightMaterial, 0);
            }
        }

        // --- Отладочные маркеры ---
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