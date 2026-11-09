using System.Collections.Generic;
using UnityEngine;
using KingdomsClone.Grid.Events;
using KingdomsClone.Core;

namespace KingdomsClone.Grid
{
    public class TileVisualizer : MonoBehaviour
    {
        [Header("Visual Settings")]
        [SerializeField] private Material tileMaterial;
        [SerializeField] private float yOffset = 0.01f;
        [SerializeField] private int layer = 0;

        [Header("Colors per Type")]
        [SerializeField] private Color grassColor    = new Color(0.4f, 0.7f, 0.3f);
        [SerializeField] private Color forestColor   = new Color(0.1f, 0.4f, 0.1f);
        [SerializeField] private Color rockColor     = new Color(0.5f, 0.5f, 0.5f);
        [SerializeField] private Color waterColor    = new Color(0.2f, 0.5f, 0.8f);
        [SerializeField] private Color fertileColor  = new Color(0.8f, 0.7f, 0.4f);

        private class Batch
        {
            public Matrix4x4[] Matrices;
            public Vector4[] Colors;
            public int Count;
        }

        private readonly List<Batch> _batches = new();
        private Dictionary<TileType, Color> _colorDict;
        private MaterialPropertyBlock _propertyBlock;
        private Mesh _proceduralCubeMesh;
        private bool _isReady = false;

        private const int MaxBatchSize = 1023;

        private void Awake()
        {
            _propertyBlock = new MaterialPropertyBlock();
            _colorDict = new Dictionary<TileType, Color>
            {
                { TileType.Grass,   grassColor   },
                { TileType.Forest,  forestColor  },
                { TileType.Rock,    rockColor    },
                { TileType.Water,   waterColor   },
                { TileType.Fertile, fertileColor },
            };

            // Создаем меш куба прямо в коде (1х1х1)
            _proceduralCubeMesh = GenerateCubeMesh();
        }

        private Mesh GenerateCubeMesh()
        {
            GameObject tempCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Mesh mesh = Instantiate(tempCube.GetComponent<MeshFilter>().sharedMesh);
            Destroy(tempCube); 
            return mesh;
        }

        private void OnEnable()
        {
            EventBus.Subscribe<GridGeneratedEvent>(OnGridGenerated);
            if (GridSystem.Instance != null && GridSystem.Instance.Grid != null)
                PrepareBatches();
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<GridGeneratedEvent>(OnGridGenerated);
        }

        private void OnGridGenerated(GridGeneratedEvent e)
        {
            // При регенерации карты (например, с новым сидом) сбрасываем флаг готовности
            _isReady = false;
            PrepareBatches();
        }

        private void PrepareBatches()
        {
            if (_isReady) return;

            var grid = GridSystem.Instance;
            if (grid == null || grid.Grid == null) return;
            if (tileMaterial == null)
            {
                Debug.LogError("[TileVisualizer] Не назначен Material!");
                return;
            }

            _batches.Clear();

            var tmpMatrices = new Matrix4x4[MaxBatchSize];
            var tmpColors   = new Vector4[MaxBatchSize];
            int count = 0;

            // Для куба поворот не нужен
            Quaternion rot = Quaternion.identity; 

            for (int x = 0; x < grid.Width; x++)
            {
                for (int y = 0; y < grid.Height; y++)
                {
                    Cell cell = grid.GetCell(x, y);
                    if (cell == null) continue;

                    // 1. Берем высоту из самой клетки (задается в MapGenerator)
                    float height = cell.Elevation;
                    
                    // 2. Получаем базовую позицию центра клетки
                    Vector3 pos = grid.CellToWorld(x, y);
                    
                    // 3. Поднимаем куб так, чтобы его низ был на уровне Y=0. 
                    // Так как pivot куба в центре, поднимаем на половину высоты.
                    pos.y += (height * 0.5f) + yOffset;

                    // 4. Масштабируем: чуть больше 1 по X и Z (overlap), чтобы не было щелей
                    float overlap = 1.02f;
                    Vector3 scale = new Vector3(grid.CellSize * overlap, height, grid.CellSize * overlap);

                    tmpMatrices[count] = Matrix4x4.TRS(pos, rot, scale);

                    // 5. Цвет
                    Color c = _colorDict[cell.Type];
                    tmpColors[count] = new Vector4(c.r, c.g, c.b, c.a);

                    count++;

                    if (count >= MaxBatchSize)
                    {
                        AddBatch(tmpMatrices, tmpColors, count);
                        count = 0;
                    }
                }
            }

            if (count > 0) AddBatch(tmpMatrices, tmpColors, count);

            _isReady = true;
            Debug.Log($"[TileVisualizer] Готово к рендеру: {_batches.Count} батчей, всего {grid.Width * grid.Height} тайлов.");
        }

        private void AddBatch(Matrix4x4[] matrices, Vector4[] colors, int count)
        {
            var batch = new Batch
            {
                Matrices = new Matrix4x4[count],
                Colors   = new Vector4[count],
                Count    = count,
            };
            System.Array.Copy(matrices, batch.Matrices, count);
            System.Array.Copy(colors,   batch.Colors,   count);
            _batches.Add(batch);
        }

        private void LateUpdate()
        {
            if (!_isReady) return;

            for (int i = 0; i < _batches.Count; i++)
            {
                var b = _batches[i];
                _propertyBlock.SetVectorArray("_BaseColor", b.Colors);
                Graphics.DrawMeshInstanced(
                    _proceduralCubeMesh, 0, tileMaterial,
                    b.Matrices, b.Count,
                    _propertyBlock,
                    UnityEngine.Rendering.ShadowCastingMode.Off,
                    receiveShadows: false,
                    layer: layer
                );
            }
        }
    }
}