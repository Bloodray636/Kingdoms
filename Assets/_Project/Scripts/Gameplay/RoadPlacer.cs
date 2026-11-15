using UnityEngine;
using UnityEngine.InputSystem;
using KingdomsClone.Grid;
using KingdomsClone.Core;
using KingdomsClone.Core.Events;
using System.Collections.Generic;

namespace KingdomsClone.Gameplay
{
    public class RoadPlacer : MonoBehaviour
    {
        [Header("Ghost Materials")]
        [SerializeField] private Material validPreviewMaterial;
        [SerializeField] private Material invalidPreviewMaterial;

        private bool _isPlacing = false;
        private Vector2Int _lastCell;
        private GameObject _ghost;
        private MeshRenderer _ghostRenderer;

        private void Start()
        {
            _ghost = GameObject.CreatePrimitive(PrimitiveType.Quad);
            _ghost.name = "Road_Ghost";
            _ghost.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            _ghost.transform.localScale = Vector3.one;
            Destroy(_ghost.GetComponent<Collider>());
            _ghostRenderer = _ghost.GetComponent<MeshRenderer>();
            _ghostRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _ghost.SetActive(false);
        }

        private void OnDestroy()
        {
            if (_ghost != null) Destroy(_ghost);
        }

        public void StartPlacement()
        {
            // Кросс-отмена: гасим другие режимы
            var buildingPlacer = FindObjectOfType<BuildingPlacer>();
            if (buildingPlacer != null) buildingPlacer.CancelPlacement();

            var demolisher = FindObjectOfType<BuildingDemolisher>();
            if (demolisher != null) demolisher.Cancel();

            _isPlacing = true;
            _lastCell = new Vector2Int(-999, -999);
            _ghost.SetActive(true);
            Debug.Log("[RoadPlacer] Режим прокладки дорог активирован. ЛКМ — строить, Esc/RMB — отмена.");
        }

        public void Cancel()
        {
            if (!_isPlacing) return;
            _isPlacing = false;
            if (_ghost != null) _ghost.SetActive(false);
            Debug.Log("[RoadPlacer] Режим прокладки отменён.");
        }

        private void Update()
        {
            if (!_isPlacing) return;
            if (Keyboard.current == null || Mouse.current == null) return;

            if (Keyboard.current.escapeKey.wasPressedThisFrame || Mouse.current.rightButton.wasPressedThisFrame)
            {
                Cancel();
                return;
            }

            UpdateGhost();

            if (Mouse.current.leftButton.isPressed)
                TryPlaceRoad();

            if (Mouse.current.leftButton.wasReleasedThisFrame)
                Cancel();
        }

        private void UpdateGhost()
        {
            var grid = GridSystem.Instance;
            var cam = Camera.main;
            if (grid == null || cam == null) return;

            Ray ray = cam.ScreenPointToRay(Mouse.current.position.ReadValue());
            Plane plane = new Plane(Vector3.up, Vector3.zero);
            if (!plane.Raycast(ray, out float enter)) return;

            Vector2Int cell = grid.WorldToCell(ray.GetPoint(enter));
            Vector3 pos = grid.CellToWorld(cell.x, cell.y);
            pos.y = grid.GetSurfaceY(cell.x, cell.y) + 0.1f;
            _ghost.transform.position = pos;

            bool can = CanPlaceRoad(cell);
            if (_ghostRenderer != null)
                _ghostRenderer.sharedMaterial = can ? validPreviewMaterial : invalidPreviewMaterial;
        }

        private void TryPlaceRoad()
        {
            var grid = GridSystem.Instance;
            if (grid == null) return;

            Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
            Plane plane = new Plane(Vector3.up, Vector3.zero);
            if (!plane.Raycast(ray, out float enter)) return;

            Vector2Int cell = grid.WorldToCell(ray.GetPoint(enter));
            if (cell == _lastCell) return;
            _lastCell = cell;

            if (!CanPlaceRoad(cell)) return;

            var c = grid.GetCell(cell.x, cell.y);

            // Если лес — вырубаем деревья на клетке (упрощённо)
            if (c.Type == TileType.Forest)
                ClearTreesOnCell(cell);

            c.Type = TileType.Road;
            c.UpdateMovementCost();

            EventBus.Publish(new RoadPlacedEvent { X = cell.x, Y = cell.y });
            EventBus.Publish(new PathInvalidationEvent 
            { 
                Cells = new List<Vector2Int> { cell } 
            });
        }

        private void ClearTreesOnCell(Vector2Int cell)
        {
            var decorator = FindObjectOfType<MapDecorator>();
            if (decorator == null) return;

            var grid = GridSystem.Instance;
            Vector3 center = grid.CellToWorld(cell.x, cell.y);
            float half = grid.CellSize * 0.5f;

            foreach (Transform child in decorator.transform)
            {
                Vector3 d = child.position - center;
                if (Mathf.Abs(d.x) < half && Mathf.Abs(d.z) < half)
                    Destroy(child.gameObject);
            }
        }

        private bool CanPlaceRoad(Vector2Int cell)
        {
            var grid = GridSystem.Instance;
            if (!grid.IsValidCell(cell.x, cell.y)) return false;

            var c = grid.GetCell(cell.x, cell.y);
            if (c == null) return false;
            if (c.Type == TileType.Road) return false;
            if (!c.IsWalkable()) return false;
            if (!c.IsFree()) return false;

            return true;
        }
    }
}