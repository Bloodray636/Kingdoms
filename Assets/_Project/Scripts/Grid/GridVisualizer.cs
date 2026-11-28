using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using KingdomsClone.Grid;
using KingdomsClone.Core;
using KingdomsClone.Core.Events;
using KingdomsClone.Grid.Events;

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

        [Tooltip("Рисовать линию каждые N клеток. Минимум 1.")]
        [SerializeField, Min(1)] private int lineStep = 5;

        [Tooltip("Высота линий над землёй (в метрах)")]
        [SerializeField] private float lineYOffset = 0.15f;

        [Header("Fishing Markers")]
        [SerializeField] private float fishingMarkerScale = 0.3f;
        [SerializeField] private float fishingMarkerYOffset = 0.1f;

        private Vector2Int _hoveredCell = new Vector2Int(-1, -1);

        private Mesh _quadMesh;

        private readonly List<Vector3> _fishingMarkerPositions = new();

        private static readonly Quaternion FlatRotation = Quaternion.Euler(-90f, 0f, 0f);

        private void Awake()
        {
            _quadMesh = GenerateQuadMesh();

            if (lineStep < 1) {
                lineStep = 1;
            }
        }

        private void OnEnable()
        {
            EventBus.Subscribe<GridGeneratedEvent>(OnGridGenerated);
            RebuildFishingMarkers();
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<GridGeneratedEvent>(OnGridGenerated);
        }

        private void OnDestroy()
        {
            if (_quadMesh != null) {
                Destroy(_quadMesh);
                _quadMesh = null;
            }
        }

        private void OnGridGenerated(GridGeneratedEvent e)
        {
            RebuildFishingMarkers();
        }

        private void RebuildFishingMarkers()
        {
            _fishingMarkerPositions.Clear();

            var grid = GridSystem.Instance;

            if (grid == null || grid.Grid == null) {
                return;
            }

            for (int x = 0; x < grid.Width; x++) {
                for (int y = 0; y < grid.Height; y++) {
                    if (!grid.IsFishingZone(x, y)) continue;

                    Vector3 pos = grid.CellToWorld(x, y);

                    pos.y = grid.GetSurfaceY(x, y) + fishingMarkerYOffset;
                    _fishingMarkerPositions.Add(pos);
                }
            }
        }

        private void Update()
        {
            var grid = GridSystem.Instance;
            var cam = Camera.main;
            var mouse = Mouse.current;

            if (grid == null || cam == null || mouse == null) {
                _hoveredCell = new Vector2Int(-1, -1);
                return;
            }

            Ray ray = cam.ScreenPointToRay(mouse.position.ReadValue());

            Plane groundPlane = new Plane(
                Vector3.up,
                new Vector3(0f, grid.OriginPosition.y, 0f)
            );

            if (groundPlane.Raycast(ray, out float enter)) {
                Vector3 hitPoint = ray.GetPoint(enter);
                _hoveredCell = grid.WorldToCell(hitPoint);
            } else {
                _hoveredCell = new Vector2Int(-1, -1);
            }
        }

        private void LateUpdate()
        {
            var grid = GridSystem.Instance;

            if (grid == null || _quadMesh == null) return;

            if (highlightMaterial != null &&
                grid.IsValidCell(_hoveredCell.x, _hoveredCell.y))
            {
                Vector3 pos = grid.CellToWorld(
                    _hoveredCell.x,
                    _hoveredCell.y
                );

                pos.y = grid.GetSurfaceY(
                    _hoveredCell.x,
                    _hoveredCell.y
                ) + 0.05f;

                float scale = grid.CellSize * 1.02f;

                Matrix4x4 mat = Matrix4x4.TRS(
                    pos,
                    FlatRotation,
                    new Vector3(scale, scale, 1f)
                );

                Graphics.DrawMesh(
                    _quadMesh,
                    mat,
                    highlightMaterial,
                    0
                );
            }

            if (fishingMarkerMaterial != null &&
                _fishingMarkerPositions.Count > 0)
            {
                float markerScale = fishingMarkerScale * grid.CellSize;

                Vector3 scale = Vector3.one * markerScale;

                for (int i = 0; i < _fishingMarkerPositions.Count; i++) {
                    Matrix4x4 mat = Matrix4x4.TRS(
                        _fishingMarkerPositions[i],
                        FlatRotation,
                        scale
                    );

                    Graphics.DrawMesh(
                        _quadMesh,
                        mat,
                        fishingMarkerMaterial,
                        0
                    );
                }
            }
        }

        private void OnDrawGizmos()
        {
            if (!showLines || !Application.isPlaying) return;

            var grid = GridSystem.Instance;

            if (grid == null || grid.Grid == null) return;

            int step = Mathf.Max(1, lineStep);

            Gizmos.color = lineColor;

            Vector3 origin = grid.OriginPosition;
            float cellSize = grid.CellSize;

            // Учитываем вертикальное смещение
            float y = origin.y + grid.SurfaceYOffset + lineYOffset;

            for (int x = 0; x <= grid.Width; x += step) {
                Vector3 start = origin + new Vector3(
                    x * cellSize,
                    grid.SurfaceYOffset + lineYOffset,
                    0f
                );

                Vector3 end = origin + new Vector3(
                    x * cellSize,
                    grid.SurfaceYOffset + lineYOffset,
                    grid.Height * cellSize
                );

                Gizmos.DrawLine(start, end);
            }

            for (int z = 0; z <= grid.Height; z += step) {
                Vector3 start = origin + new Vector3(
                    0f,
                    grid.SurfaceYOffset + lineYOffset,
                    z * cellSize
                );

                Vector3 end = origin + new Vector3(
                    grid.Width * cellSize,
                    grid.SurfaceYOffset + lineYOffset,
                    z * cellSize
                );

                Gizmos.DrawLine(start, end);
            }
        }

        private void OnGUI()
        {
            if (_hoveredCell.x >= 0) {
                GUI.Box(
                    new Rect(10, 10, 150, 25),
                    $"Cell: X:{_hoveredCell.x} Y:{_hoveredCell.y}"
                );
            }
        }

        private Mesh GenerateQuadMesh()
        {
            var mesh = new Mesh {
                name = "Highlight_Quad"
            };

            mesh.vertices = new[] {
                new Vector3(-0.5f, -0.5f, 0f),
                new Vector3( 0.5f, -0.5f, 0f),
                new Vector3(-0.5f,  0.5f, 0f),
                new Vector3( 0.5f,  0.5f, 0f)
            };

            mesh.triangles = new[] {
                0, 1, 2,
                2, 1, 3
            };

            mesh.RecalculateNormals();

            return mesh;
        }

        #if UNITY_EDITOR
            private void OnValidate()
            {
                if (lineStep < 1)
                    lineStep = 1;

                if (fishingMarkerScale < 0.01f)
                    fishingMarkerScale = 0.01f;

                if (fishingMarkerYOffset < 0f)
                    fishingMarkerYOffset = 0f;
            }
        #endif
    }
}