using UnityEngine;
using UnityEngine.InputSystem;
using KingdomsClone.Grid;
using KingdomsClone.Core;
using KingdomsClone.Core.Events;

namespace KingdomsClone.Gameplay
{
    /// <summary>
    /// Прокладка дорог: зажми ЛКМ и веди мышкой.
    /// Дорога — это изменение TileType клетки на Road + отдельная визуализация в RoadVisualizer.
    /// При прокладке через лес — деревья вырубаются.
    /// </summary>
    public class RoadPlacer : MonoBehaviour
    {
        [Header("Ghost Materials")]
        [SerializeField] private Material validPreviewMaterial;
        [SerializeField] private Material invalidPreviewMaterial;

        [Header("Ghost Visual")]
        [SerializeField] private float ghostYOffset = 0.05f;

        [Header("Cost")]
        [Tooltip("Временно бесплатно. TODO: когда появится RoadDataSO — списывать ресурсы.")]
        [SerializeField] private bool roadIsFree = true;

        private bool _isPlacing = false;
        private Vector2Int _lastCell;
        private GameObject _ghost;

        private void Start()
        {
            // Создаём призрак один раз, потом только двигаем и вкл/выкл
            _ghost = GameObject.CreatePrimitive(PrimitiveType.Quad);
            _ghost.name = "Road_Ghost";
            _ghost.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            _ghost.transform.localScale = Vector3.one;
            Destroy(_ghost.GetComponent<Collider>());
            _ghost.SetActive(false);
        }

        private void OnDestroy()
        {
            if (_ghost != null) Destroy(_ghost);
        }

        public void StartPlacement()
        {
            _isPlacing = true;
            _lastCell = new Vector2Int(-999, -999);
            if (_ghost != null) _ghost.SetActive(true);
            Debug.Log("[RoadPlacer] StartPlacement — режим дорог включён");
        }

        private void Update()
        {
            if (!_isPlacing)
                return;

            // Защита от отсутствия устройств ввода
            if (Keyboard.current == null || Mouse.current == null)
                return;

            // Отмена
            if (Keyboard.current.escapeKey.wasPressedThisFrame || Mouse.current.rightButton.wasPressedThisFrame)
            {
                Cancel();
                return;
            }

            UpdateGhost();

            // Рисование — пока зажата ЛКМ
            if (Mouse.current.leftButton.isPressed)
                TryPlaceRoad();

            // Отпустили — завершаем сессию
            if (Mouse.current.leftButton.wasReleasedThisFrame)
                Cancel();
        }

        private void UpdateGhost()
        {
            var grid = GridSystem.Instance;
            var cam = Camera.main;
            if (grid == null || cam == null) return;

            Ray ray = cam.ScreenPointToRay(Mouse.current.position.ReadValue());
            Plane basePlane = new Plane(Vector3.up, Vector3.zero);

            if (!basePlane.Raycast(ray, out float enter0)) return;

            // Первичный hit в Y=0, потом уточняем по высоте поверхности
            Vector2Int preCell = grid.WorldToCell(ray.GetPoint(enter0));
            float surfaceY = grid.IsValidCell(preCell.x, preCell.y)
                ? grid.GetSurfaceY(preCell.x, preCell.y)
                : 0f;

            Plane surfacePlane = new Plane(Vector3.up, new Vector3(0f, surfaceY, 0f));
            if (!surfacePlane.Raycast(ray, out float enter1)) return;

            Vector3 hit = ray.GetPoint(enter1);
            Vector2Int cell = grid.WorldToCell(hit);

            Vector3 worldPos = grid.CellToWorld(cell);
            worldPos.y = grid.GetSurfaceY(cell.x, cell.y) + ghostYOffset;
            _ghost.transform.position = worldPos;
            _ghost.transform.localScale = new Vector3(grid.CellSize, grid.CellSize, 1f);

            bool canPlace = CanPlaceRoad(cell);
            var mr = _ghost.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                mr.sharedMaterial = canPlace ? validPreviewMaterial : invalidPreviewMaterial;
            }
        }

        private void TryPlaceRoad()
        {
            var grid = GridSystem.Instance;
            var cam = Camera.main;
            if (grid == null || cam == null) return;

            Ray ray = cam.ScreenPointToRay(Mouse.current.position.ReadValue());
            Plane basePlane = new Plane(Vector3.up, Vector3.zero);

            if (!basePlane.Raycast(ray, out float enter0)) return;
            Vector2Int preCell = grid.WorldToCell(ray.GetPoint(enter0));
            float surfaceY = grid.IsValidCell(preCell.x, preCell.y) ? grid.GetSurfaceY(preCell.x, preCell.y) : 0f;

            Plane surfacePlane = new Plane(Vector3.up, new Vector3(0f, surfaceY, 0f));
            if (!surfacePlane.Raycast(ray, out float enter1)) return;

            Vector2Int cell = grid.WorldToCell(ray.GetPoint(enter1));

            if (cell == _lastCell) return; // не спамим одинаковыми клетками
            _lastCell = cell;

            if (!CanPlaceRoad(cell)) return;

            var c = grid.GetCell(cell.x, cell.y);
            if (c == null) return;

            // ФИКС 3.3: вырубаем лес перед прокладкой дороги
            if (c.Type == TileType.Forest)
            {
                ClearTreesOnCell(cell);
            }

            c.Type = TileType.Road;
            c.UpdateMovementCost();

            EventBus.Publish(new RoadPlacedEvent { X = cell.x, Y = cell.y });
        }

        private void ClearTreesOnCell(Vector2Int cell)
        {
            var decorator = FindObjectOfType<MapDecorator>();
            if (decorator == null) return;

            var grid = GridSystem.Instance;
            if (grid == null) return;

            Vector3 center = grid.CellToWorld(cell.x, cell.y);
            float halfCell = grid.CellSize * 0.5f;

            // Идём по копии списка, потому что Destroy меняет коллекцию
            var children = new System.Collections.Generic.List<Transform>();
            foreach (Transform child in decorator.transform) children.Add(child);

            foreach (var child in children)
            {
                if (child == null) continue;
                Vector3 d = child.position - center;
                if (Mathf.Abs(d.x) < halfCell && Mathf.Abs(d.z) < halfCell)
                {
                    Destroy(child.gameObject);
                }
            }
        }

        private bool CanPlaceRoad(Vector2Int cell)
        {
            var grid = GridSystem.Instance;
            if (grid == null) return false;
            if (!grid.IsValidCell(cell.x, cell.y)) return false;

            Cell c = grid.GetCell(cell.x, cell.y);
            if (c == null) return false;

            if (c.Type == TileType.Road) return false; // уже дорога
            if (!c.IsWalkable()) return false;         // вода/камень — нельзя
            if (!c.IsFree()) return false;             // здание — нельзя

            return true;
        }

        private void Cancel()
        {
            _isPlacing = false;
            if (_ghost != null) _ghost.SetActive(false);
            _lastCell = new Vector2Int(-999, -999);
        }
    }
}