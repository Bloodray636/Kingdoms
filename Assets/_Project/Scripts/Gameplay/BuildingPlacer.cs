using UnityEngine;
using UnityEngine.InputSystem;
using KingdomsClone.Data;
using KingdomsClone.Grid;
using KingdomsClone.Core;
using KingdomsClone.Core.Events;

namespace KingdomsClone.Gameplay
{
    public class BuildingPlacer : MonoBehaviour
    {
        [Header("Ghost Materials")]
        [SerializeField] private Material validMaterial;
        [SerializeField] private Material invalidMaterial;
        [SerializeField] private Material noMoneyMaterial;

        private BuildingDataSO _selectedBuilding;
        private GameObject _ghostObject;
        private MeshRenderer[] _ghostRenderers;
        private bool _isPlacing = false;
        private Vector2Int _currentCell;
        private Quaternion _currentRotation = Quaternion.identity;

        private string _lastRejectReason;
        private bool _tileOk;
        private bool _resourcesOk;

        private void Update()
        {
            if (!_isPlacing) return;

            UpdateGhostPosition();

            if (Keyboard.current.rKey.wasPressedThisFrame)
            {
                _currentRotation *= Quaternion.Euler(0, 90, 0);
                _ghostObject.transform.rotation = _currentRotation;
            }

            if (Mouse.current.leftButton.wasPressedThisFrame)
                TryPlaceBuilding();

            if (Mouse.current.rightButton.wasPressedThisFrame || Keyboard.current.escapeKey.wasPressedThisFrame)
                CancelPlacement();
        }

        public void StartPlacement(BuildingDataSO buildingData)
        {
            CancelPlacement();
            _selectedBuilding = buildingData;
            _isPlacing = true;
            _currentRotation = Quaternion.identity;
            _lastRejectReason = null;
            _tileOk = false;
            _resourcesOk = false;

            _ghostObject = new GameObject("Ghost_" + buildingData.DisplayName);
            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.transform.SetParent(_ghostObject.transform);
            
            // Размер куба = размер здания
            float sx = Mathf.Max(1, buildingData.Size.x) * 0.95f;
            float sz = Mathf.Max(1, buildingData.Size.y) * 0.95f;
            visual.transform.localScale = new Vector3(sx, 1f, sz);
            visual.transform.localPosition = new Vector3(
                (buildingData.Size.x - 1) * 0.5f,
                0.5f,
                (buildingData.Size.y - 1) * 0.5f
            );
            Destroy(visual.GetComponent<Collider>());

            _ghostRenderers = _ghostObject.GetComponentsInChildren<MeshRenderer>();
            UpdateGhostColor();
        }

        private void UpdateGhostPosition()
        {
            var grid = GridSystem.Instance;
            if (grid == null) return;

            Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
            Plane groundPlane = new Plane(Vector3.up, Vector3.zero);

            if (groundPlane.Raycast(ray, out float enter))
            {
                Vector3 hitPoint = ray.GetPoint(enter);
                Vector2Int cell = grid.WorldToCell(hitPoint);
                
                // Смещаем якорь так, чтобы центр footprint'а был под курсором
                _currentCell = new Vector2Int(
                    cell.x - _selectedBuilding.Size.x / 2,
                    cell.y - _selectedBuilding.Size.y / 2
                );
                
                Vector3 worldPos = grid.CellToWorld(_currentCell);
                worldPos.x += (_selectedBuilding.Size.x - 1) * 0.5f;
                worldPos.z += (_selectedBuilding.Size.y - 1) * 0.5f;
                _ghostObject.transform.position = worldPos;

                CheckTile(out _tileOk, out string tileReason);
                CheckResources(out _resourcesOk, out string resReason);

                _lastRejectReason = !_tileOk ? tileReason : (!_resourcesOk ? resReason : null);
                UpdateGhostColor();
            }
        }

        private void CheckTile(out bool ok, out string reason)
        {
            reason = null;
            ok = false;
            var grid = GridSystem.Instance;
            if (grid == null) { reason = "Нет GridSystem"; return; }

            // Проверяем все клетки footprint'а
            for (int x = 0; x < _selectedBuilding.Size.x; x++)
            {
                for (int y = 0; y < _selectedBuilding.Size.y; y++)
                {
                    int cx = _currentCell.x + x;
                    int cy = _currentCell.y + y;
                    
                    if (!grid.IsValidCell(cx, cy)) { reason = "Вне карты"; return; }
                    Cell c = grid.GetCell(cx, cy);
                    if (c == null) { reason = "Клетка null"; return; }
                    if (!c.IsBuildable()) { reason = $"Нельзя на {c.Type}"; return; }
                    if (!c.IsFree()) { reason = "Место занято"; return; }
                }
            }
            ok = true;
        }

        private void CheckResources(out bool ok, out string reason)
        {
            reason = null;
            ok = true;
            if (_selectedBuilding == null) return;
            if (_selectedBuilding.BuildCost == null || _selectedBuilding.BuildCost.Count == 0) return;
            if (ResourceManager.Instance == null) { ok = false; reason = "Нет ResourceManager"; return; }

            if (!ResourceManager.Instance.HasResources(_selectedBuilding.BuildCost))
            {
                ok = false;
                reason = "Не хватает ресурсов";
            }
        }

        private void UpdateGhostColor()
        {
            Material mat;
            if (!_tileOk) mat = invalidMaterial;
            else if (!_resourcesOk) mat = noMoneyMaterial;
            else mat = validMaterial;

            foreach (var r in _ghostRenderers)
                r.sharedMaterial = mat;
        }

        private void TryPlaceBuilding()
        {
            if (!_tileOk || !_resourcesOk)
            {
                Debug.LogWarning($"[BuildingPlacer] Нельзя: {_lastRejectReason}");
                EventBus.Publish(new BuildingPlacementRejectedEvent
                {
                    Data = _selectedBuilding,
                    Position = _currentCell,
                    Reason = _lastRejectReason
                });
                return;
            }

            if (_selectedBuilding.BuildCost != null && _selectedBuilding.BuildCost.Count > 0)
            {
                if (!ResourceManager.Instance.SpendResources(_selectedBuilding.BuildCost))
                {
                    Debug.LogWarning("[BuildingPlacer] SpendResources = false");
                    return;
                }
            }

            var grid = GridSystem.Instance;
            Vector3 worldPos = grid.CellToWorld(_currentCell);
            worldPos.x += (_selectedBuilding.Size.x - 1) * 0.5f;
            worldPos.z += (_selectedBuilding.Size.y - 1) * 0.5f;

            GameObject newBuilding = new GameObject("Building_" + _selectedBuilding.DisplayName);
            newBuilding.transform.position = worldPos;

            // Пытаемся использовать префаб из данных, иначе — серый куб
            GameObject visual;
            if (_selectedBuilding.Prefab != null)
            {
                visual = Instantiate(_selectedBuilding.Prefab, newBuilding.transform);
                visual.transform.localPosition = Vector3.zero;
            }
            else
            {
                visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
                visual.transform.SetParent(newBuilding.transform);
                visual.transform.localPosition = new Vector3(
                    (_selectedBuilding.Size.x - 1) * 0.5f, 0.5f, (_selectedBuilding.Size.y - 1) * 0.5f
                );
                visual.transform.localScale = new Vector3(
                    _selectedBuilding.Size.x * 0.95f, 1f, _selectedBuilding.Size.y * 0.95f
                );
                // Серый цвет
                var mr = visual.GetComponent<MeshRenderer>();
                if (mr != null) mr.material.color = new Color(0.6f, 0.6f, 0.6f);
                Destroy(visual.GetComponent<Collider>());
            }

            BuildingBase buildingBase = newBuilding.AddComponent<BuildingBase>();
            buildingBase.Initialize(_selectedBuilding, _currentCell, _currentRotation);

            // Регистрируем ВСЕ клетки
            foreach (var cell in buildingBase.GetOccupiedCells())
                grid.RegisterBuilding(cell.x, cell.y, newBuilding);

            Debug.Log($"[BuildingPlacer] Построено: {_selectedBuilding.DisplayName} ({_selectedBuilding.Size.x}x{_selectedBuilding.Size.y}) в {_currentCell}");
            CancelPlacement();
        }

        private void CancelPlacement()
        {
            _isPlacing = false;
            if (_ghostObject != null) Destroy(_ghostObject);
        }

        private void OnGUI()
        {
            if (!_isPlacing) return;

            var rm = ResourceManager.Instance;
            var gd = GameDataSO.Instance;
            if (rm == null || gd == null || _selectedBuilding == null) return;

            float w = 340, h = 240;
            Rect box = new Rect(Screen.width - w - 20, Screen.height - h - 20, w, h);
            GUI.Box(box, "");

            GUILayout.BeginArea(new Rect(box.x + 10, box.y + 10, w - 20, h - 20));

            GUILayout.Label($"<b>BUILDING: {_selectedBuilding.DisplayName}</b> ({_selectedBuilding.Size.x}×{_selectedBuilding.Size.y})");
            GUILayout.Space(4);
            GUILayout.Label("Cost:");

            if (_selectedBuilding.BuildCost != null && _selectedBuilding.BuildCost.Count > 0)
            {
                foreach (var ra in _selectedBuilding.BuildCost)
                {
                    if (ra == null || ra.Resource == null) continue;
                    int have = rm.GetAmount(ra.Resource);
                    bool enough = have >= ra.Amount;
                    GUILayout.Label($"  • {ra.Resource.DisplayName}: {ra.Amount} (есть {have})");
                }
            }
            else GUILayout.Label("  (бесплатно)");

            GUILayout.Space(8);
            string status;
            if (!_tileOk) status = $"❌ {_lastRejectReason}";
            else if (!_resourcesOk) status = "⚠ Не хватает ресурсов";
            else status = "✔ Можно строить";

            GUILayout.Label(status);
            GUILayout.Space(6);
            GUILayout.Label("LMB — Поставить   RMB/Esc — Отмена   R — Поворот");

            GUILayout.EndArea();
        }
    }
}