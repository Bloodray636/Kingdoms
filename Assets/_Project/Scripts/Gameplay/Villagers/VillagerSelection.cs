using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using KingdomsClone.Core.DebugTools;
using KingdomsClone.Grid;

namespace KingdomsClone.Gameplay
{
    public class VillagerSelection : MonoBehaviour
    {
        [SerializeField] private LayerMask raycastMask = ~0;
        [SerializeField] private float maxDistance = 500f;
        [SerializeField] private VillagerInfoPanel panel;
        [SerializeField] private Material selectionRingMaterial;
        [SerializeField] private float selectionRingYOffset = 0.05f;
        [SerializeField] private float selectionRingScale = 0.8f;

        private Villager _selected;
        private GameObject _selectionRing;

        private void Start()
        {
            if (panel == null) {
                panel = FindObjectOfType<VillagerInfoPanel>();
            }

            BuildSelectionRing();
        }

        private void BuildSelectionRing()
        {
            _selectionRing = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            _selectionRing.name = "SelectionRing";
            _selectionRing.transform.localScale = new Vector3(selectionRingScale, 0.02f, selectionRingScale);

            var col = _selectionRing.GetComponent<Collider>();

            if (col != null) {
                Destroy(col);
            }

            var mr = _selectionRing.GetComponent<MeshRenderer>();

            if (mr != null && selectionRingMaterial != null) {
                mr.sharedMaterial = selectionRingMaterial;
            }

            _selectionRing.SetActive(false);
        }

        private void Update()
        {
            if (_selected != null && !_selected.gameObject.activeInHierarchy) {
                ClearSelection();
            } else if (_selected != null) {
                UpdateRingPosition();
            }

            if (Mouse.current == null) return;
            if (!Mouse.current.leftButton.wasPressedThisFrame) return;

            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
            if (DebugMenu.IsOpen) return;
            if (IsAnyPlacerActive()) return;

            TrySelect();
        }

        private bool IsAnyPlacerActive()
        {
            var bp = FindObjectOfType<BuildingPlacer>();
            if (bp != null && bp.IsPlacing) return true;

            var bd = FindObjectOfType<BuildingDemolisher>();
            if (bd != null && bd.IsActive) return true;

            var rp = FindObjectOfType<RoadPlacer>();
            if (rp != null && rp.IsActive) return true;

            return false;
        }

        private void TrySelect()
        {
            var cam = Camera.main;
            if (cam == null) return;

            Ray ray = cam.ScreenPointToRay(Mouse.current.position.ReadValue());

            if (!Physics.Raycast(ray, out RaycastHit hit, maxDistance, raycastMask)) { 
                ClearSelection(); 
                return; 
            }

            var villager = hit.collider.GetComponentInParent<Villager>();

            if (villager != null) { 
                Select(villager);
            } else {
                ClearSelection();
            }
        }

        private void Select(Villager v)
        {
            _selected = v;

            if (panel != null) {
                panel.Show(v);
            }

            if (_selectionRing != null) { 
                _selectionRing.SetActive(true); 
                UpdateRingPosition(); 
            }
        }

        public void ClearSelection()
        {
            _selected = null;

            if (panel != null) {
                panel.Hide();
            }

            if (_selectionRing != null) {
                _selectionRing.SetActive(false);
            }
        }

        private void UpdateRingPosition()
        {
            if (_selected == null || _selectionRing == null) return;

            Vector3 pos = _selected.transform.position;

            var grid = GridSystem.Instance;

            if (grid != null) {
                var cell = grid.WorldToCell(pos);
                pos.y = grid.GetSurfaceY(cell.x, cell.y) + selectionRingYOffset;
            } else {
                pos.y = selectionRingYOffset;
            }

            _selectionRing.transform.position = pos;
        }

        private void OnDestroy()
        {
            if (_selectionRing != null) {
                Destroy(_selectionRing);
            }
        }
    }
}