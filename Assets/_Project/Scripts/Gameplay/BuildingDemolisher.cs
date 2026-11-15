using UnityEngine;
using UnityEngine.InputSystem;
using KingdomsClone.Grid;

namespace KingdomsClone.Gameplay
{
    /// <summary>
    /// Режим сноса зданий. Активируется кнопкой в UI или хоткеем X.
    /// Клик по зданию — снести с возвратом 50% ресурсов (SetState(Destroyed) → RefundHalfCost).
    /// Esc / RMB — отмена режима.
    /// </summary>
    public class BuildingDemolisher : MonoBehaviour
    {
        [Header("Visual")]
        [SerializeField] private Material highlightMaterial;
        [SerializeField] private float highlightYOffset = 0.15f;

        [Header("Raycast")]
        [SerializeField] private LayerMask buildingLayerMask = ~0;
        [SerializeField] private float maxRayDistance = 1000f;

        private bool _isActive = false;
        private BuildingBase _hovered;
        private GameObject _highlightQuad;
        private Mesh _quadMesh;

        public bool IsActive => _isActive;

        private void Awake()
        {
            _quadMesh = GenerateQuad();

            _highlightQuad = new GameObject("DemolishHighlight");
            var mf = _highlightQuad.AddComponent<MeshFilter>();
            mf.sharedMesh = _quadMesh;
            var mr = _highlightQuad.AddComponent<MeshRenderer>();
            mr.sharedMaterial = highlightMaterial;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            _highlightQuad.SetActive(false);
        }

        private void OnDestroy()
        {
            if (_quadMesh != null) Destroy(_quadMesh);
            if (_highlightQuad != null) Destroy(_highlightQuad);
        }

        public void StartDemolition()
        {
            // Отменяем режим строительства, если он был активен
            var placer = FindObjectOfType<BuildingPlacer>();
            if (placer != null) placer.CancelPlacement();

            _isActive = true;
            Debug.Log("[Demolisher] Режим сноса активен. Клик по зданию — снести. Esc/RMB — отмена.");
        }

        public void Cancel()
        {
            _isActive = false;
            _hovered = null;
            if (_highlightQuad != null) _highlightQuad.SetActive(false);
        }

        private void Update()
        {
            if (!_isActive) return;
            if (Mouse.current == null || Keyboard.current == null) return;

            if (Keyboard.current.escapeKey.wasPressedThisFrame ||
                Mouse.current.rightButton.wasPressedThisFrame)
            {
                Cancel();
                return;
            }

            UpdateHover();

            if (Mouse.current.leftButton.wasPressedThisFrame)
                TryDemolish();
        }

        private void UpdateHover()
        {
            var cam = Camera.main;
            if (cam == null) return;

            Ray ray = cam.ScreenPointToRay(Mouse.current.position.ReadValue());

            if (Physics.Raycast(ray, out RaycastHit hit, maxRayDistance, buildingLayerMask))
            {
                var b = hit.collider.GetComponentInParent<BuildingBase>();
                if (b != null && b.State != BuildingState.Destroyed)
                {
                    _hovered = b;
                    ShowHighlight(b);
                    return;
                }
            }

            _hovered = null;
            if (_highlightQuad != null) _highlightQuad.SetActive(false);
        }

        private void ShowHighlight(BuildingBase b)
        {
            var grid = GridSystem.Instance;
            if (grid == null || b.Data == null) return;

            var rotated = FootprintUtil.GetRotatedSize(b.Data.Size, b.Rotation);
            Vector3 center = b.transform.position;
            center.y = grid.GetSurfaceY(b.GridPosition.x, b.GridPosition.y) + highlightYOffset;

            _highlightQuad.transform.position = center;
            _highlightQuad.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
            _highlightQuad.transform.localScale = new Vector3(rotated.x, rotated.y, 1f);
            _highlightQuad.SetActive(true);
        }

        private void TryDemolish()
        {
            if (_hovered == null) return;

            Debug.Log($"[Demolisher] Снос: {_hovered.Data.DisplayName} в {_hovered.GridPosition}");
            _hovered.SetState(BuildingState.Destroyed);

            _hovered = null;
            if (_highlightQuad != null) _highlightQuad.SetActive(false);
        }

        private Mesh GenerateQuad()
        {
            var mesh = new Mesh { name = "DemolishHighlightQuad" };
            mesh.vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, 0), new Vector3( 0.5f, -0.5f, 0),
                new Vector3(-0.5f,  0.5f, 0), new Vector3( 0.5f,  0.5f, 0)
            };
            mesh.triangles = new[] { 0, 1, 2, 2, 1, 3 };
            mesh.RecalculateNormals();
            return mesh;
        }
    }
}