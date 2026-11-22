using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using KingdomsClone.Grid;
using KingdomsClone.Core;
using KingdomsClone.Core.Events;
using UnityEngine.EventSystems;
using KingdomsClone.Core.DebugTools;

namespace KingdomsClone.Gameplay
{
    public class BuildingDemolisher : MonoBehaviour
    {
        [Header("Visual")]
        [SerializeField] private Material highlightMaterial;
        [SerializeField] private Material highlightRoadMaterial;
        [SerializeField] private float highlightYOffset = 0.15f;

        [Header("Raycast")]
        [SerializeField] private LayerMask buildingLayerMask = ~0;
        [SerializeField] private float maxRayDistance = 1000f;

        private bool _isActive = false;
        private BuildingBase _hovered;
        private Vector2Int _hoveredRoadCell = new Vector2Int(-1, -1);
        private GameObject _highlightQuad;
        private MeshRenderer _highlightRenderer;
        private Mesh _quadMesh;

        public bool IsActive => _isActive;

        private void Awake()
        {
            _quadMesh = GenerateQuad();
            _highlightQuad = new GameObject("DemolishHighlight");

            var mf = _highlightQuad.AddComponent<MeshFilter>();
            mf.sharedMesh = _quadMesh;

            _highlightRenderer = _highlightQuad.AddComponent<MeshRenderer>();
            _highlightRenderer.sharedMaterial = highlightMaterial;
            _highlightRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _highlightRenderer.receiveShadows = false;

            _highlightQuad.SetActive(false);
        }

        private void OnDestroy()
        {
            if (_quadMesh != null) {
                Destroy(_quadMesh);
            }

            if (_highlightQuad != null) {
                Destroy(_highlightQuad);
            }
        }

        public void StartDemolition()
        {
            var placer = FindObjectOfType<BuildingPlacer>();

            if (placer != null) {
                placer.CancelPlacement();
            }

            var roadPlacer = FindObjectOfType<RoadPlacer>();

            if (roadPlacer != null && roadPlacer.IsActive) {
                roadPlacer.Cancel();
            }

            _isActive = true;
            _hovered = null;
            _hoveredRoadCell = new Vector2Int(-1, -1);
            Debug.Log("[Demolisher] Режим сноса активен. Клик по зданию/дороге — снести. Esc/RMB — отмена.");
        }

        public void Cancel()
        {
            _isActive = false;
            _hovered = null;
            _hoveredRoadCell = new Vector2Int(-1, -1);

            if (_highlightQuad != null) {
                _highlightQuad.SetActive(false);
            }
        }

        private void Update()
        {
            if (Keyboard.current == null || Mouse.current == null) return;

            if (Keyboard.current.xKey.wasPressedThisFrame && !_isActive) {
                StartDemolition();
                return;
            }

            if (!_isActive) return;

            if (Keyboard.current.escapeKey.wasPressedThisFrame || Mouse.current.rightButton.wasPressedThisFrame) {
                Cancel();
                return;
            }

            UpdateHover();

            if (Mouse.current.leftButton.wasPressedThisFrame && !IsPointerOverUI()) {
                TryDemolish();
            }
        }

        private static bool IsPointerOverUI()
        {
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) {
                return true;
            }

            if (DebugMenu.IsOpen) {
                return true;
            }

            return false;
        }

        private void UpdateHover()
        {
            var cam = Camera.main;

            if (cam == null) return;

            Ray ray = cam.ScreenPointToRay(Mouse.current.position.ReadValue());

            if (Physics.Raycast(ray, out RaycastHit hit, maxRayDistance, buildingLayerMask)) {
                var b = hit.collider.GetComponentInParent<BuildingBase>();

                if (b != null && b.State != BuildingState.Destroyed) {
                    _hovered = b;
                    _hoveredRoadCell = new Vector2Int(-1, -1);
                    ShowBuildingHighlight(b);
                    return;
                }
            }

            var grid = GridSystem.Instance;

            if (grid != null) {
                Plane plane = new Plane(Vector3.up, Vector3.zero);

                if (plane.Raycast(ray, out float enter)) {
                    Vector2Int cell = grid.WorldToCell(ray.GetPoint(enter));

                    if (grid.IsValidCell(cell.x, cell.y)) {
                        var c = grid.GetCell(cell.x, cell.y);

                        if (c != null && c.Type == TileType.Road) {
                            _hovered = null;
                            _hoveredRoadCell = cell;
                            ShowRoadHighlight(cell);
                            return;
                        }
                    }
                }
            }

            _hovered = null;
            _hoveredRoadCell = new Vector2Int(-1, -1);

            if (_highlightQuad != null) {
                _highlightQuad.SetActive(false);
            }
        }

        private void ShowBuildingHighlight(BuildingBase b)
        {
            var grid = GridSystem.Instance;
            if (grid == null || b.Data == null) return;

            var rotated = FootprintUtil.GetRotatedSize(b.Data.Size, b.Rotation);
            Vector3 center = b.transform.position;
            center.y = grid.GetSurfaceY(b.GridPosition.x, b.GridPosition.y) + highlightYOffset;

            _highlightQuad.transform.position = center;
            _highlightQuad.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
            _highlightQuad.transform.localScale = new Vector3(rotated.x, rotated.y, 1f);

            if (_highlightRenderer != null) {
                _highlightRenderer.sharedMaterial = highlightMaterial;
            }

            _highlightQuad.SetActive(true);
        }

        private void ShowRoadHighlight(Vector2Int cell)
        {
            var grid = GridSystem.Instance;
            if (grid == null) return;

            Vector3 center = grid.CellToWorld(cell.x, cell.y);
            center.y = grid.GetSurfaceY(cell.x, cell.y) + highlightYOffset;

            _highlightQuad.transform.position = center;
            _highlightQuad.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
            _highlightQuad.transform.localScale = new Vector3(1f, 1f, 1f);

            if (_highlightRenderer != null) {
                _highlightRenderer.sharedMaterial = highlightRoadMaterial != null ? highlightRoadMaterial : highlightMaterial;
            }

            _highlightQuad.SetActive(true);
        }

        private void TryDemolish()
        {
 
            if (_hovered != null) {
                Debug.Log($"[Demolisher] Снос здания: {_hovered.Data.DisplayName} в {_hovered.GridPosition}");
                _hovered.SetState(BuildingState.Destroyed);
                _hovered = null;

                if (_highlightQuad != null) {
                    _highlightQuad.SetActive(false);
                }

                return;
            }

            if (_hoveredRoadCell.x >= 0) {
                RemoveRoad(_hoveredRoadCell);
                _hoveredRoadCell = new Vector2Int(-1, -1);

                if (_highlightQuad != null) {
                    _highlightQuad.SetActive(false);
                }
            }
        }

        private void RemoveRoad(Vector2Int cell)
        {
            var grid = GridSystem.Instance;
            if (grid == null) return;

            var c = grid.GetCell(cell.x, cell.y);
            if (c == null || c.Type != TileType.Road) return;

            TileType restored = c.PreviousType == TileType.Forest ? TileType.Grass : c.PreviousType;
            c.Type = restored;
            c.UpdateMovementCost();

            EventBus.Publish(new RoadRemovedEvent { X = cell.x, Y = cell.y });
            EventBus.Publish(new PathInvalidationEvent {
                Cells = new List<Vector2Int> { cell }
            });

            Debug.Log($"[Demolisher] Удалена дорога в {cell}, восстановлено: {restored}");
        }

        private Mesh GenerateQuad()
        {
            var mesh = new Mesh { name = "DemolishHighlightQuad" };
            mesh.vertices = new[] {
                new Vector3(-0.5f, -0.5f, 0), new Vector3( 0.5f, -0.5f, 0),
                new Vector3(-0.5f,  0.5f, 0), new Vector3( 0.5f,  0.5f, 0)
            };
            
            mesh.triangles = new[] { 0, 1, 2, 2, 1, 3 };
            mesh.RecalculateNormals();
            return mesh;
        }
    }
}