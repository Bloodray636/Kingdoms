using System.Collections.Generic;
using UnityEngine;
using KingdomsClone.Core;
using KingdomsClone.Core.Events;
using KingdomsClone.Grid.Events;

namespace KingdomsClone.Grid
{
    public class RoadVisualizer : MonoBehaviour
    {
        [SerializeField] private Material roadMaterial;
        [SerializeField] private float roadYOffset = 0.02f;

        private Mesh _quadMesh;
        private readonly List<Vector3> _roadPositions = new();

        private void Awake() => _quadMesh = GenerateQuadMesh();

        private void OnDestroy()
        {
            if (_quadMesh != null) Destroy(_quadMesh);
        }

        private void OnEnable()
        {
            EventBus.Subscribe<RoadPlacedEvent>(OnRoadPlaced);
            EventBus.Subscribe<RoadRemovedEvent>(OnRoadRemoved);
            EventBus.Subscribe<GridGeneratedEvent>(OnGridGenerated);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<RoadPlacedEvent>(OnRoadPlaced);
            EventBus.Unsubscribe<RoadRemovedEvent>(OnRoadRemoved);
            EventBus.Unsubscribe<GridGeneratedEvent>(OnGridGenerated);
        }

        // События

        private void OnRoadPlaced(RoadPlacedEvent e)
        {
            var grid = GridSystem.Instance;
            if (grid == null) return;

            Vector3 pos = grid.CellToWorld(e.X, e.Y);
            pos.y = grid.GetSurfaceY(e.X, e.Y) + roadYOffset;
            _roadPositions.Add(pos);
        }

        private void OnRoadRemoved(RoadRemovedEvent e)
        {
            RebuildRoadPositions();
        }

        private void OnGridGenerated(GridGeneratedEvent e) => _roadPositions.Clear();

        private void RebuildRoadPositions()
        {
            _roadPositions.Clear();

            var grid = GridSystem.Instance;

            if (grid == null || grid.Grid == null) return;

            for (int x = 0; x < grid.Width; x++) {
                for (int y = 0; y < grid.Height; y++) {
                    var c = grid.GetCell(x, y);

                    if (c == null || c.Type != TileType.Road) continue;

                    Vector3 pos = grid.CellToWorld(x, y);
                    pos.y = grid.GetSurfaceY(x, y) + roadYOffset;
                    _roadPositions.Add(pos);
                }
            }
        }

        // Рендер

        private void LateUpdate()
        {
            if (_roadPositions.Count == 0 || roadMaterial == null) return;

            Matrix4x4 rot = Matrix4x4.Rotate(Quaternion.Euler(-90f, 0f, 0f));
            Vector3 scale = Vector3.one;

            for (int i = 0; i < _roadPositions.Count; i++) {
                Matrix4x4 mat = Matrix4x4.TRS(_roadPositions[i], Quaternion.Euler(-90f, 0f, 0f), scale);
                Graphics.DrawMesh(_quadMesh, mat, roadMaterial, 0);
            }
        }

        private Mesh GenerateQuadMesh()
        {
            var mesh = new Mesh { 
                name = "Road_Quad" 
            };

            mesh.vertices = new[] {
                new Vector3(-0.5f, -0.5f, 0), new Vector3( 0.5f, -0.5f, 0),
                new Vector3(-0.5f,  0.5f, 0), new Vector3( 0.5f,  0.5f, 0)
            };

            mesh.triangles = new[] { 
                0, 1, 2, 2, 1, 3 
            };
            
            mesh.RecalculateNormals();
            return mesh;
        }
    }
}