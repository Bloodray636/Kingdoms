using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using KingdomsClone.Data;
using KingdomsClone.Core;
using KingdomsClone.Core.Events;
using KingdomsClone.Grid;

namespace KingdomsClone.Gameplay
{
    [RequireComponent(typeof(BoxCollider))]
    public class BuildingBase : MonoBehaviour
    {
        [SerializeField] private BuildingState _state = BuildingState.Placing;
        [SerializeField] private int _currentHealth;
        [SerializeField] private float _buildProgress;
        [SerializeField] private int _residents;

        [Header("Debug")]
        [SerializeField] private bool debugClickDamage = false;

        public BuildingDataSO Data { get; private set; }
        public Vector2Int GridPosition { get; private set; }
        public Quaternion Rotation { get; private set; }
        public BuildingState State => _state;
        public int CurrentHealth => _currentHealth;
        public float BuildProgress => _buildProgress;
        public int Residents => _residents;
        public LocalStorage Storage { get; private set; }

        public Vector2Int RotatedSize => Data != null ? FootprintUtil.GetRotatedSize(Data.Size, Rotation) : Vector2Int.one;

        public bool HasFreeSpace => Data != null && Data.IsHousing && _state == BuildingState.Active && _residents < Data.MaxResidents;

        private Coroutine _buildRoutine;
        private List<Job> _jobs = new List<Job>();
        public IReadOnlyList<Job> GetJobs() => _jobs;

        public void Initialize(BuildingDataSO data, Vector2Int anchor, Quaternion rotation)
        {
            if (data == null) { 
                Debug.LogError("[BuildingBase] data == null!"); 
                Destroy(gameObject); 
                return; 
            }

            Data = data;
            GridPosition = anchor;
            Rotation = rotation;
            _currentHealth = data.MaxHealth;
            _residents = 0;

            gameObject.name = $"{data.DisplayName} ({anchor.x}, {anchor.y})";
            transform.rotation = rotation;

            Vector2Int rotated = RotatedSize;
            var col = GetComponent<BoxCollider>();

            if (col != null) {
                col.center = new Vector3(0f, 0.5f, 0f);
                col.size = new Vector3(rotated.x, 1f, rotated.y);
            }

            if (data.IsStorage) {
                Storage = gameObject.GetComponent<LocalStorage>();

                if (Storage == null) {
                    Storage = gameObject.AddComponent<LocalStorage>();
                }

                Storage.Initialize(data.StorageCapacity);
                Debug.Log($"[Building] {data.DisplayName}: склад на {data.StorageCapacity} единиц");
            }

            if (BuildingManager.Instance != null) {
                BuildingManager.Instance.Register(this);
            }

            if (HousingManager.Instance != null && data.IsHousing) {
                HousingManager.Instance.Register(this);
            }

            float buildTime = Mathf.Max(0f, data.BuildTimeSeconds);

            if (buildTime <= 0.01f) {
                SetState(BuildingState.Building);
                SetState(BuildingState.Active);
                _buildProgress = 1f;
                OnPlaced();
                RegisterJobs();

                EventBus.Publish(new BuildingPlacedEvent { 
                    Data = Data, 
                    Position = GridPosition 
                });
                
            } else {
                SetState(BuildingState.Building);
                _buildRoutine = StartCoroutine(BuildRoutine(buildTime));
            }
        }

        private IEnumerator BuildRoutine(float duration)
        {
            _buildProgress = 0f;

            float t = 0f;

            while (t < duration && _state == BuildingState.Building) { 
                t += Time.deltaTime; 
                _buildProgress = Mathf.Clamp01(t / duration); 
                yield return null; 
            }

            _buildProgress = 1f;
            _buildRoutine = null;

            if (_state == BuildingState.Building) {
                SetState(BuildingState.Active);
                OnPlaced();
                RegisterJobs();

                EventBus.Publish(new BuildingPlacedEvent {
                    Data = Data, 
                    Position = GridPosition 
                });
            }
        }

        private void OnDestroy()
        {
            if (BuildingManager.Instance != null) {
                BuildingManager.Instance.Unregister(this);
            }

            if (HousingManager.Instance != null) {
                HousingManager.Instance.Unregister(this);
            }

            if (_buildRoutine != null) {
                StopCoroutine(_buildRoutine);
            }

            if (_jobs != null) {
                foreach (var j in _jobs) {
                    if (JobManager.Instance != null) {
                        JobManager.Instance.UnregisterVacancy(j);
                    }

                    VillagerManager.ReleaseWorkPosition(j.WorkPosition);
                }

                _jobs.Clear();
            }

            if (GridSystem.Instance != null && Data != null) {
                foreach (var cell in GetOccupiedCells()) {
                    GridSystem.Instance.UnregisterBuilding(cell.x, cell.y, gameObject);
                }
            }
        }

        public bool TryAddResident(GameObject villager)
        {
            if (!HasFreeSpace) return false;
            _residents++;
            return true;
        }

        public void RemoveResident(GameObject villager) { 
            if (_residents > 0) {
                _residents--; 
            }
        }

        public IEnumerable<Vector2Int> GetOccupiedCells()
        {
            if (Data == null) yield break;

            foreach (var cell in FootprintUtil.GetOccupiedCells(GridPosition, Data.Size, Rotation)) {
                yield return cell;
            }
        }

        public void SetState(BuildingState newState)
        {
            if (_state == newState) return;
            if (_state == BuildingState.Destroyed) return;

            _state = newState;

            if (_state == BuildingState.Destroyed)
            {
                if (_buildRoutine != null) { 
                    StopCoroutine(_buildRoutine); 
                    _buildRoutine = null; 
                }

                if (Data != null && Data.IsHousing) {
                    NotifyResidentsOfDestruction();
                }

                var occupied = new List<Vector2Int>();

                if (GridSystem.Instance != null) {
                    foreach (var cell in GetOccupiedCells()) {
                        GridSystem.Instance.UnregisterBuilding(cell.x, cell.y, gameObject);
                        occupied.Add(cell);
                    }
                }

                RefundHalfCost();
                OnRemoved();
                EventBus.Publish(new BuildingRemovedEvent { Data = Data, Position = GridPosition });
                EventBus.Publish(new PathInvalidationEvent { Cells = occupied });
                Destroy(gameObject, 0.1f);
            }
        }

        private void NotifyResidentsOfDestruction()
        {
            var vm = VillagerManager.Instance;

            if (vm == null) return;

            var snapshot = new List<Villager>(vm.AllVillagers);
            int notified = 0;

            foreach (var v in snapshot) {
                if (v == null) continue;

                if (v.HomeBuilding == this) {
                    v.OnHomeDestroyed(this);
                    notified++;
                }
            }

            if (notified > 0) {
                Debug.Log($"[Building] {Data?.DisplayName} {GridPosition}: уведомлено {notified} жителей о сносе");
            }
        }

        public void TakeDamage(int amount)
        {
            if (amount <= 0 || _state == BuildingState.Destroyed) return;
            _currentHealth = Mathf.Max(0, _currentHealth - amount);

            if (_currentHealth <= 0) {
                SetState(BuildingState.Destroyed);
            }
        }

        public void Heal(int amount)
        {
            if (amount <= 0 || Data == null || _state == BuildingState.Destroyed) return;
            _currentHealth = Mathf.Min(Data.MaxHealth, _currentHealth + amount);
        }

        private void RefundHalfCost()
        {
            if (Data == null || Data.BuildCost == null || ResourceManager.Instance == null) return;

            foreach (var ra in Data.BuildCost) {
                if (ra == null || ra.Resource == null) continue;

                int refund = Mathf.FloorToInt(ra.Amount * 0.5f);

                if (refund > 0) {
                    ResourceManager.Instance.AddResource(ra.Resource, refund);
                }
            }
        }

        private Vector2Int? FindFreeWorkCell(Vector2Int anchor, Vector2Int size)
        {
            var grid = GridSystem.Instance;

            if (grid == null) return null;

            Vector2Int? best = null;

            float bestDist = float.MaxValue;

            for (int radius = 1; radius <= 2; radius++) {
                for (int x = -radius; x < size.x + radius; x++) {
                    for (int y = -radius; y < size.y + radius; y++) {
                        bool interior = (x >= 0 && x < size.x && y >= 0 && y < size.y);

                        if (interior) continue;

                        if (radius == 2) {
                            bool outer = (x == -2 || x == size.x + 1 || y == -2 || y == size.y + 1);
                            if (!outer) continue;
                        }

                        var c = anchor + new Vector2Int(x, y);

                        if (!grid.IsValidCell(c.x, c.y)) continue;

                        var cell = grid.GetCell(c.x, c.y);

                        if (cell == null || !cell.IsTraversable() || cell.OccupiedBy != null) continue;
                        if (VillagerManager.IsWorkPositionReserved(c)) continue;

                        Vector3 center = grid.CellToWorld(anchor.x + size.x / 2, anchor.y + size.y / 2);
                        float d = Vector3.Distance(center, grid.CellToWorld(c.x, c.y));

                        if (d < bestDist) { 
                            bestDist = d; 
                            best = c; 
                        }
                    }
                }

                if (best.HasValue) return best;
            }

            return best;
        }

        private void RegisterJobs()
        {
            if (Data == null || Data.WorkerSlots <= 0 || Data.JobType == JobType.None) return;
            if (JobManager.Instance == null) return;

            if (_jobs == null) {
                _jobs = new List<Job>();
            }

            if (_jobs.Count > 0) return;

            Vector2Int rotated = RotatedSize;

            for (int i = 0; i < Data.WorkerSlots; i++) {
                Vector2Int? workPos = FindFreeWorkCell(GridPosition, rotated);

                if (!workPos.HasValue) {
                    Debug.LogWarning($"[Building] {Data.DisplayName}: нет свободной клетки для рабочего {i + 1}");
                    continue;
                }

                if (!VillagerManager.TryReserveWorkPosition(workPos.Value)) {
                    Debug.LogWarning($"[Building] {Data.DisplayName}: клетка {workPos} уже забронирована");
                    continue;
                }

                var job = new Job(Data.JobType, Data.JobPriority, this, GridPosition, workPos.Value);
                _jobs.Add(job);
                JobManager.Instance.RegisterVacancy(job);
            }
            
            Debug.Log($"[Building] {Data.DisplayName} создал {_jobs.Count} вакансий: {Data.JobType} [{Data.JobPriority}]");
        }

        protected virtual void OnPlaced() { }
        protected virtual void OnRemoved() { }

        private void OnMouseDown()
        {
            if (!debugClickDamage) return;
            TakeDamage(20);
        }
    }
}