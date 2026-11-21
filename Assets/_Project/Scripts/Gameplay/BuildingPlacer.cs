using UnityEngine;
using UnityEngine.InputSystem;
using KingdomsClone.Data;
using KingdomsClone.Grid;
using KingdomsClone.Core;
using KingdomsClone.Core.Events;
using System.Collections.Generic;

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
        public bool IsPlacing => _isPlacing;
        private Vector2Int _currentCell;
        private Quaternion _currentRotation = Quaternion.identity;

        private string _lastRejectReason;
        private bool _tileOk;
        private bool _resourcesOk;

        // ============ LIFECYCLE ============

        private void Update()
        {
            if (!_isPlacing) return;

            // ФИКС 1: защита от отсутствия устройств ввода
            if (Keyboard.current == null || Mouse.current == null) return;

            UpdateGhostPosition();

            if (Keyboard.current.rKey.wasPressedThisFrame)
            {
                _currentRotation *= Quaternion.Euler(0, 90, 0);
                UpdateGhostVisualScale();
            }

            if (Mouse.current.leftButton.wasPressedThisFrame)
                TryPlaceBuilding();

            if (Mouse.current.rightButton.wasPressedThisFrame || Keyboard.current.escapeKey.wasPressedThisFrame)
                CancelPlacement();
        }

        private void OnDestroy()
        {
            // ФИКС 2: очистка призрака при уничтожении компонента
            if (_ghostObject != null) Destroy(_ghostObject);
        }

        // ============ START / CANCEL PLACEMENT ============

        public void StartPlacement(BuildingDataSO buildingData)
        {
            // ФИКС 3: null-check
            if (buildingData == null)
            {
                Debug.LogError("[BuildingPlacer] StartPlacement: buildingData == null!");
                return;
            }

            var demolisher = FindObjectOfType<BuildingDemolisher>();
            if (demolisher != null) demolisher.Cancel();

            CancelPlacement();
            _selectedBuilding = buildingData;
            _isPlacing = true;
            _currentRotation = Quaternion.identity;
            _lastRejectReason = null;
            _tileOk = false;
            _resourcesOk = false;

            _ghostObject = new GameObject("Ghost_" + buildingData.DisplayName);

            GameObject visual;
            if (buildingData.Prefab != null)
            {
                visual = Instantiate(buildingData.Prefab, _ghostObject.transform);
            }
            else
            {
                visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
                visual.transform.SetParent(_ghostObject.transform, false);
                var mr = visual.GetComponent<MeshRenderer>();
                if (mr != null) mr.material = validMaterial;
                Destroy(visual.GetComponent<Collider>());
            }
            visual.name = "GhostVisual";

            UpdateGhostVisualScale();
            _ghostRenderers = _ghostObject.GetComponentsInChildren<MeshRenderer>();
            UpdateGhostColor();
        }

        public void CancelPlacement()
        {
            _isPlacing = false;
            if (_ghostObject != null) Destroy(_ghostObject);
        }

        // ============ GHOST VISUAL ============

        private void UpdateGhostVisualScale()
        {
            if (_ghostObject == null || _selectedBuilding == null) return;
            var visual = _ghostObject.transform.Find("GhostVisual");
            if (visual == null) return;

            if (_selectedBuilding.Prefab != null)
            {
                // ПРЕФАБ: крутим родителя, модель не трогаем
                _ghostObject.transform.rotation = _currentRotation;
                visual.localPosition = new Vector3(0f, 0.5f, 0f);
                visual.localRotation = Quaternion.identity;
                visual.localScale = Vector3.one;
            }
            else
            {
                // КУБ-ЗАГЛУШКА: родителя не крутим, форму даёт масштаб
                _ghostObject.transform.rotation = Quaternion.identity;
                Vector2Int rotated = FootprintUtil.GetRotatedSize(_selectedBuilding.Size, _currentRotation);
                visual.localScale = new Vector3(rotated.x * 0.95f, 1f, rotated.y * 0.95f);
                visual.localPosition = new Vector3(0f, 0.5f, 0f);
                visual.localRotation = Quaternion.identity;
            }
        }

        // ============ GHOST POSITION (двойной raycast) ============

        private void UpdateGhostPosition()
        {
            var grid = GridSystem.Instance;
            var cam = Camera.main;
            if (grid == null || cam == null) return;

            Ray ray = cam.ScreenPointToRay(Mouse.current.position.ReadValue());

            // ФИКС 4: двойной raycast — сначала в Y=0 для получения клетки,
            // потом в точную высоту поверхности этой клетки для точной позиции.
            Plane basePlane = new Plane(Vector3.up, Vector3.zero);
            if (!basePlane.Raycast(ray, out float enter0)) return;

            Vector2Int preCell = grid.WorldToCell(ray.GetPoint(enter0));
            float surfaceY = grid.IsValidCell(preCell.x, preCell.y)
                ? grid.GetSurfaceY(preCell.x, preCell.y)
                : 0f;

            Plane surfacePlane = new Plane(Vector3.up, new Vector3(0f, surfaceY, 0f));
            if (!surfacePlane.Raycast(ray, out float enter1)) return;

            Vector3 hitPoint = ray.GetPoint(enter1);
            Vector2Int cursorCell = grid.WorldToCell(hitPoint);

            // Якорь = левый нижний угол ПОВЁРНУТОГО footprint'а, чтобы центр был под курсором
            Vector2Int rotated = FootprintUtil.GetRotatedSize(_selectedBuilding.Size, _currentRotation);
            _currentCell = new Vector2Int(
                cursorCell.x - rotated.x / 2,
                cursorCell.y - rotated.y / 2
            );

            // Позиция корня = центр повёрнутого footprint'а
            Vector3 worldPos = grid.CellToWorld(_currentCell);
            worldPos.x += (rotated.x - 1) * 0.5f;
            worldPos.z += (rotated.y - 1) * 0.5f;
            worldPos.y = grid.IsValidCell(_currentCell.x, _currentCell.y)
                ? grid.GetSurfaceY(_currentCell.x, _currentCell.y)
                : 0f;
            _ghostObject.transform.position = worldPos;

            CheckTile(out _tileOk, out string tileReason);
            CheckResources(out _resourcesOk, out string resReason);

            _lastRejectReason = !_tileOk ? tileReason : (!_resourcesOk ? resReason : null);
            UpdateGhostColor();
        }

        // ============ CHECKS ============

        private void CheckTile(out bool ok, out string reason)
        {
            reason = null;
            ok = false;
            var grid = GridSystem.Instance;
            if (grid == null) { reason = "Нет GridSystem"; return; }

            foreach (var cell in FootprintUtil.GetOccupiedCells(_currentCell, _selectedBuilding.Size, _currentRotation))
            {
                if (!grid.IsValidCell(cell.x, cell.y)) { reason = "Вне карты"; return; }
                Cell c = grid.GetCell(cell.x, cell.y);
                if (c == null) { reason = "Клетка null"; return; }
                if (!c.IsBuildable()) { reason = $"Нельзя на {c.Type}"; return; }
                if (!c.IsFree()) { reason = "Место занято"; return; }
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

            if (_ghostRenderers == null) return;
            foreach (var r in _ghostRenderers)
                if (r != null) r.sharedMaterial = mat;
        }

        // ============ PLACE (транзакционно) ============

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

            bool resourcesSpent = false;
            GameObject newBuilding = null;

            try
            {
                // 1. Списание ресурсов
                if (_selectedBuilding.BuildCost != null && _selectedBuilding.BuildCost.Count > 0)
                {
                    if (!ResourceManager.Instance.SpendResources(_selectedBuilding.BuildCost))
                    {
                        Debug.LogWarning("[BuildingPlacer] SpendResources вернул false — откат.");
                        return;
                    }
                    resourcesSpent = true;
                }

                // 2. Позиция и создание
                var grid = GridSystem.Instance;
                Vector2Int rotated = FootprintUtil.GetRotatedSize(_selectedBuilding.Size, _currentRotation);
                Vector3 worldPos = grid.CellToWorld(_currentCell);
                worldPos.x += (rotated.x - 1) * 0.5f;
                worldPos.z += (rotated.y - 1) * 0.5f;
                worldPos.y = grid.GetSurfaceY(_currentCell.x, _currentCell.y);

                newBuilding = new GameObject("Building_" + _selectedBuilding.DisplayName);
                newBuilding.transform.position = worldPos;

                GameObject visual;
                if (_selectedBuilding.Prefab != null)
                {
                    newBuilding.transform.rotation = _currentRotation;
                    visual = Instantiate(_selectedBuilding.Prefab, newBuilding.transform);
                    visual.transform.localPosition = new Vector3(0f, 0.5f, 0f);
                    visual.transform.localRotation = Quaternion.identity;
                    visual.transform.localScale = Vector3.one;
                }
                else
                {
                    newBuilding.transform.rotation = Quaternion.identity;
                    visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    visual.transform.SetParent(newBuilding.transform, false);
                    visual.transform.localScale = new Vector3(rotated.x * 0.95f, 1f, rotated.y * 0.95f);
                    visual.transform.localPosition = new Vector3(0f, 0.5f, 0f);
                    visual.transform.localRotation = Quaternion.identity;
                    var mr = visual.GetComponent<MeshRenderer>();
                    if (mr != null) mr.material.color = new Color(0.6f, 0.6f, 0.6f);
                    Destroy(visual.GetComponent<Collider>());
                }
                visual.name = "BuildingVisual";

                // 3. Логика здания
                BuildingBase buildingBase = newBuilding.AddComponent<BuildingBase>();
                buildingBase.Initialize(_selectedBuilding, _currentCell, _currentRotation);

                // 4. Регистрация клеток
                var occupied = new List<Vector2Int>(buildingBase.GetOccupiedCells());
                foreach (var cell in occupied)
                    grid.RegisterBuilding(cell.x, cell.y, newBuilding, _selectedBuilding.IsHousing);

                EventBus.Publish(new PathInvalidationEvent { Cells = occupied });

                Debug.Log($"[BuildingPlacer] Построено: {_selectedBuilding.DisplayName} " +
                          $"({rotated.x}x{rotated.y}) anchor={_currentCell} rot={_currentRotation.eulerAngles.y}");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[BuildingPlacer] КРИТИЧЕСКАЯ ОШИБКА при постройке: {ex}");

                // Откат: уничтожаем недоделанный объект
                if (newBuilding != null) Destroy(newBuilding);

                // Откат: возвращаем ресурсы
                if (resourcesSpent && ResourceManager.Instance != null &&
                    _selectedBuilding != null && _selectedBuilding.BuildCost != null)
                {
                    foreach (var ra in _selectedBuilding.BuildCost)
                        if (ra?.Resource != null)
                            ResourceManager.Instance.AddResource(ra.Resource, ra.Amount);
                    Debug.LogWarning("[BuildingPlacer] Ресурсы возвращены (откат транзакции).");
                }
                return;
            }

            CancelPlacement();
        }

        // ============ UI (во время размещения) ============

        private void OnGUI()
        {
            if (!_isPlacing) return;
            var rm = ResourceManager.Instance;
            if (rm == null || _selectedBuilding == null) return;

            Vector2Int rotated = FootprintUtil.GetRotatedSize(_selectedBuilding.Size, _currentRotation);

            float w = 340, h = 240;
            Rect box = new Rect(Screen.width - w - 20, Screen.height - h - 20, w, h);
            GUI.Box(box, "");

            GUILayout.BeginArea(new Rect(box.x + 10, box.y + 10, w - 20, h - 20));

            GUILayout.Label($"<b>BUILDING: {_selectedBuilding.DisplayName}</b> ({rotated.x}×{rotated.y})  rot={_currentRotation.eulerAngles.y}°");
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