using System.Collections.Generic;
using UnityEngine;
using KingdomsClone.Grid.Events;
using KingdomsClone.Core;

namespace KingdomsClone.Grid
{
    public class TileVisualizer : MonoBehaviour
    {
        [Header("Visual Settings")]
        [SerializeField] private Material landMaterial;
        [SerializeField] private Material waterMaterial;

        [Tooltip("Перекрытие тайлов. 1.0 = без наложения (единый меш, дырок не будет). " + "Больше 1 — наложение, может дать Z-fighting на стыках.")]
        [SerializeField] private float overlapFactor = 1.0f;

        [Header("Colors per Type")]
        [SerializeField] private Color grassColor    = new Color(0.4f, 0.7f, 0.3f);
        [SerializeField] private Color forestColor   = new Color(0.1f, 0.4f, 0.1f);
        [SerializeField] private Color rockColor     = new Color(0.5f, 0.5f, 0.5f);
        [SerializeField] private Color waterColor    = new Color(0.2f, 0.5f, 0.8f);
        [SerializeField] private Color fertileColor  = new Color(0.8f, 0.7f, 0.4f);
        [SerializeField] private Color roadColor     = new Color(0.35f, 0.30f, 0.25f);
        [SerializeField] private Color swampColor    = new Color(0.227f, 0.290f, 0.165f, 1f);

        private Dictionary<TileType, Color> _colorDict;

        private GameObject _landMeshObj;
        private GameObject _waterMeshObj;
        private Mesh _landMesh;
        private Mesh _waterMesh;

        private void Awake()
        {
            _colorDict = new Dictionary<TileType, Color>
            {
                { TileType.Grass, grassColor },
                { TileType.Forest, forestColor },
                { TileType.Rock, rockColor },
                { TileType.Water, waterColor },
                { TileType.Fertile, fertileColor },
                { TileType.Road, roadColor },
                { TileType.Swamp, swampColor },
            };
        }

        private void OnEnable()
        {
            EventBus.Subscribe<GridGeneratedEvent>(OnGridGenerated);

            if (GridSystem.Instance != null && GridSystem.Instance.Grid != null) {
                GenerateMeshes();
            }
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<GridGeneratedEvent>(OnGridGenerated);
        }

        private void OnDestroy()
        {
            CleanupMeshes();
        }

        private void OnGridGenerated(GridGeneratedEvent e) => GenerateMeshes();

        private void CleanupMeshes()
        {
            if (_landMesh != null) { 
                Destroy(_landMesh);  
                _landMesh = null; 
            }

            if (_waterMesh != null) { 
                Destroy(_waterMesh); 
                _waterMesh = null; 
            }

            if (_landMeshObj != null) { 
                Destroy(_landMeshObj);  
                _landMeshObj = null; 
            }

            if (_waterMeshObj != null) { 
                Destroy(_waterMeshObj); 
                _waterMeshObj = null; 
            }
        }

        private void GenerateMeshes()
        {
            var grid = GridSystem.Instance;

            if (grid == null || grid.Grid == null) return;

            if (landMaterial == null || waterMaterial == null) {
                Debug.LogError("[TileVisualizer] Не назначены материалы (Land или Water)!");
                return;
            }

            CleanupMeshes();

            var landVerts = new List<Vector3>();
            var landTris = new List<int>();
            var landColors = new List<Color>();

            var waterVerts = new List<Vector3>();
            var waterTris = new List<int>();
            var waterColors = new List<Color>();

            float halfSize = grid.CellSize * 0.5f * overlapFactor;

            for (int x = 0; x < grid.Width; x++) {
                for (int y = 0; y < grid.Height; y++) {
                    Cell cell = grid.GetCell(x, y);

                    if (cell == null) continue;

                    // Мировой центр клетки
                    Vector3 worldCenter = grid.CellToWorld(x, y);
                    worldCenter.y = grid.GetSurfaceY(x, y);

                    Vector3 p0_w = new Vector3(worldCenter.x - halfSize, worldCenter.y, worldCenter.z - halfSize);
                    Vector3 p1_w = new Vector3(worldCenter.x - halfSize, worldCenter.y, worldCenter.z + halfSize);
                    Vector3 p2_w = new Vector3(worldCenter.x + halfSize, worldCenter.y, worldCenter.z - halfSize);
                    Vector3 p3_w = new Vector3(worldCenter.x + halfSize, worldCenter.y, worldCenter.z + halfSize);

                    Vector3 p0 = transform.InverseTransformPoint(p0_w);
                    Vector3 p1 = transform.InverseTransformPoint(p1_w);
                    Vector3 p2 = transform.InverseTransformPoint(p2_w);
                    Vector3 p3 = transform.InverseTransformPoint(p3_w);

                    if (!_colorDict.TryGetValue(cell.Type, out Color c)) {
                        Debug.LogWarning($"[TileVisualizer] Нет цвета для типа {cell.Type}, использую magenta.");
                        c = Color.magenta;
                    }

                    if (cell.Type == TileType.Water) {
                        int s = waterVerts.Count;
                        waterVerts.Add(p0); waterVerts.Add(p1); waterVerts.Add(p2); waterVerts.Add(p3);
                        waterColors.Add(c); waterColors.Add(c); waterColors.Add(c); waterColors.Add(c);
                        waterTris.Add(s); waterTris.Add(s + 1); waterTris.Add(s + 2);
                        waterTris.Add(s + 1); waterTris.Add(s + 3); waterTris.Add(s + 2);
                    } else {
                        int s = landVerts.Count;
                        landVerts.Add(p0); landVerts.Add(p1); landVerts.Add(p2); landVerts.Add(p3);
                        landColors.Add(c); landColors.Add(c); landColors.Add(c); landColors.Add(c);
                        landTris.Add(s); landTris.Add(s + 1); landTris.Add(s + 2);
                        landTris.Add(s + 1); landTris.Add(s + 3); landTris.Add(s + 2);
                    }
                }
            }

            _landMeshObj = CreateMeshObject("LandMesh", landVerts, landTris, landColors, landMaterial, out _landMesh);
            _waterMeshObj = CreateMeshObject("WaterMesh", waterVerts, waterTris, waterColors, waterMaterial, out _waterMesh);

            Debug.Log($"[TileVisualizer] Меш сгенерирован. Тайлов: земля={landVerts.Count / 4}, вода={waterVerts.Count / 4}, " + $"CellSize={grid.CellSize}, Origin={grid.OriginPosition}");
        }

        private GameObject CreateMeshObject(string name, List<Vector3> verts, List<int> tris, List<Color> colors, Material mat, out Mesh outMesh) {
            outMesh = null;

            if (verts.Count == 0) return null;

            var obj = new GameObject(name);
            obj.transform.SetParent(transform);
            obj.transform.localPosition = Vector3.zero;
            obj.transform.localRotation = Quaternion.identity;
            obj.transform.localScale = Vector3.one;

            var mf = obj.AddComponent<MeshFilter>();
            var mr = obj.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;

            var mesh = new Mesh();
            mesh.name = name;
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.SetColors(colors);

            var normals = new Vector3[verts.Count];

            for (int i = 0; i < normals.Length; i++) normals[i] = Vector3.up;
            
            mesh.normals = normals;

            mf.sharedMesh = mesh;
            outMesh = mesh;

            return obj;
        }
    }
}