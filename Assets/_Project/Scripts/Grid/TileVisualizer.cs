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
        [SerializeField] private float yOffset = 0.0f; // Земля плоская

        [Header("Colors per Type")]
        [SerializeField] private Color grassColor    = new Color(0.4f, 0.7f, 0.3f);
        [SerializeField] private Color forestColor   = new Color(0.1f, 0.4f, 0.1f);
        [SerializeField] private Color rockColor     = new Color(0.5f, 0.5f, 0.5f);
        [SerializeField] private Color waterColor    = new Color(0.2f, 0.5f, 0.8f);
        [SerializeField] private Color fertileColor  = new Color(0.8f, 0.7f, 0.4f);

        private Dictionary<TileType, Color> _colorDict;
        private GameObject _landMeshObj;
        private GameObject _waterMeshObj;

        private void Awake()
        {
            _colorDict = new Dictionary<TileType, Color>
            {
                { TileType.Grass,   grassColor   },
                { TileType.Forest,  forestColor  },
                { TileType.Rock,    rockColor    },
                { TileType.Water,   waterColor   },
                { TileType.Fertile, fertileColor },
            };
        }

        private void OnEnable()
        {
            EventBus.Subscribe<GridGeneratedEvent>(OnGridGenerated);
            if (GridSystem.Instance != null && GridSystem.Instance.Grid != null)
                GenerateMeshes();
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<GridGeneratedEvent>(OnGridGenerated);
        }

        private void OnGridGenerated(GridGeneratedEvent e)
        {
            GenerateMeshes();
        }

        private void GenerateMeshes()
        {
            var grid = GridSystem.Instance;
            if (grid == null || grid.Grid == null) return;

            if (_landMeshObj != null) Destroy(_landMeshObj);
            if (_waterMeshObj != null) Destroy(_waterMeshObj);

            List<Vector3> landVerts = new List<Vector3>();
            List<int> landTris = new List<int>();
            List<Color> landColors = new List<Color>();

            List<Vector3> waterVerts = new List<Vector3>();
            List<int> waterTris = new List<int>();
            List<Color> waterColors = new List<Color>();

            // Делаем квадрат чуть больше 1х1 (0.505), чтобы не было щелей
            float halfSize = 0.505f;

            for (int x = 0; x < grid.Width; x++)
            {
                for (int y = 0; y < grid.Height; y++)
                {
                    Cell cell = grid.GetCell(x, y);
                    if (cell == null) continue;

                    // ВСЕГДА ПЛОСКО (y = 0)
                    float yPos = yOffset; 
                    Color c = _colorDict[cell.Type];

                    // Координаты для одного квадрата
                    Vector3 p0 = new Vector3(x + 0.5f - halfSize, yPos, y + 0.5f - halfSize);
                    Vector3 p1 = new Vector3(x + 0.5f - halfSize, yPos, y + 0.5f + halfSize);
                    Vector3 p2 = new Vector3(x + 0.5f + halfSize, yPos, y + 0.5f - halfSize);
                    Vector3 p3 = new Vector3(x + 0.5f + halfSize, yPos, y + 0.5f + halfSize);

                    if (cell.Type == TileType.Water)
                    {
                        int startIndex = waterVerts.Count;
                        waterVerts.Add(p0); waterVerts.Add(p1); waterVerts.Add(p2); waterVerts.Add(p3);
                        waterColors.Add(c); waterColors.Add(c); waterColors.Add(c); waterColors.Add(c);
                        
                        // Треугольники смотрят вверх
                        waterTris.Add(startIndex); waterTris.Add(startIndex + 1); waterTris.Add(startIndex + 2);
                        waterTris.Add(startIndex + 1); waterTris.Add(startIndex + 3); waterTris.Add(startIndex + 2);
                    }
                    else
                    {
                        int startIndex = landVerts.Count;
                        landVerts.Add(p0); landVerts.Add(p1); landVerts.Add(p2); landVerts.Add(p3);
                        landColors.Add(c); landColors.Add(c); landColors.Add(c); landColors.Add(c);
                        
                        // Треугольники смотрят вверх
                        landTris.Add(startIndex); landTris.Add(startIndex + 1); landTris.Add(startIndex + 2);
                        landTris.Add(startIndex + 1); landTris.Add(startIndex + 3); landTris.Add(startIndex + 2);
                    }
                }
            }

            _landMeshObj = CreateMeshObject("LandMesh", landVerts, landTris, landColors, landMaterial);
            _waterMeshObj = CreateMeshObject("WaterMesh", waterVerts, waterTris, waterColors, waterMaterial);

            Debug.Log("[TileVisualizer] Плоский меш сгенерирован!");
        }

        private GameObject CreateMeshObject(string name, List<Vector3> verts, List<int> tris, List<Color> colors, Material mat)
        {
            if (verts.Count == 0) return null;

            GameObject obj = new GameObject(name);
            obj.transform.SetParent(transform);

            MeshFilter mf = obj.AddComponent<MeshFilter>();
            MeshRenderer mr = obj.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;

            Mesh mesh = new Mesh();
            mesh.name = name;
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32; 
            
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.SetColors(colors);
            
            // Жестко задаем нормали вверх (чтобы не было странного освещения)
            Vector3[] normals = new Vector3[verts.Count];
            for (int i = 0; i < normals.Length; i++) normals[i] = Vector3.up;
            mesh.normals = normals;

            mf.sharedMesh = mesh;
            return obj;
        }
    }
}