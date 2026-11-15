using System.Collections;
using System.Collections.Generic;
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
        [Header("Runtime (Read Only)")]
        [SerializeField] private BuildingState _state = BuildingState.Placing;
        [SerializeField] private int _currentHealth;
        [SerializeField] private float _buildProgress;
        [SerializeField] private int _residents;

        [Header("Debug")]
        [Tooltip("Клик по зданию наносит 20 урона — ТОЛЬКО ДЛЯ ТЕСТА. Включи галочку, чтобы проверить.")]
        [SerializeField] private bool debugClickDamage = false;

        // ============ ПУБЛИЧНЫЕ СВОЙСТВА ============

        public BuildingDataSO Data { get; private set; }
        public Vector2Int GridPosition { get; private set; }   // anchor = левый нижний угол footprint'а
        public Quaternion Rotation { get; private set; }
        public BuildingState State => _state;
        public int CurrentHealth => _currentHealth;
        public float BuildProgress => _buildProgress;
        public int Residents => _residents;

        public Vector2Int RotatedSize => Data != null
            ? FootprintUtil.GetRotatedSize(Data.Size, Rotation)
            : Vector2Int.one;

        public bool HasFreeSpace =>
            Data != null && Data.IsHousing &&
            _state == BuildingState.Active &&
            _residents < Data.MaxResidents;

        // ============ ВНУТРЕННЕЕ ============

        private Coroutine _buildRoutine;
        private List<Job> _jobs = new List<Job>();

        // ============ ИНИЦИАЛИЗАЦИЯ ============

        public void Initialize(BuildingDataSO data, Vector2Int anchor, Quaternion rotation)
        {
            if (data == null)
            {
                Debug.LogError("[BuildingBase] Initialize: data == null! Уничтожаю объект.");
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

            // Коллайдер по размеру footprint'а
            Vector2Int rotated = RotatedSize;
            var col = GetComponent<BoxCollider>();
            if (col != null)
            {
                col.center = new Vector3(0f, 0.5f, 0f);
                col.size = new Vector3(rotated.x, 1f, rotated.y);
            }

            // Регистрация в менеджерах
            if (BuildingManager.Instance != null)
                BuildingManager.Instance.Register(this);

            if (HousingManager.Instance != null && data.IsHousing)
                HousingManager.Instance.Register(this);

            // Создание вакансий
            if (JobManager.Instance != null && data.WorkerSlots > 0 && data.JobType != JobType.None)
                CreateJobs();

            // Запуск стройки или мгновенная активация
            float buildTime = Mathf.Max(0f, data.BuildTimeSeconds);
            if (buildTime <= 0.01f)
            {
                SetState(BuildingState.Building);
                SetState(BuildingState.Active);
                _buildProgress = 1f;
                OnPlaced();
                EventBus.Publish(new BuildingPlacedEvent { Data = Data, Position = GridPosition });
            }
            else
            {
                SetState(BuildingState.Building);
                _buildRoutine = StartCoroutine(BuildRoutine(buildTime));
            }
        }

        private void CreateJobs()
        {
            for (int i = 0; i < Data.WorkerSlots; i++)
            {
                Vector2Int? workPos = FindFreeWorkCell(GridPosition, Data.Size);
                if (!workPos.HasValue)
                {
                    Debug.LogWarning($"[Building] {Data.DisplayName}: нет свободной клетки рядом для рабочего {i + 1}.");
                    continue;
                }

                if (!VillagerManager.TryReserveWorkPosition(workPos.Value))
                {
                    Debug.LogWarning($"[Building] {Data.DisplayName}: клетка {workPos} уже забронирована.");
                    continue;
                }

                var job = new Job(Data.JobType, Data.JobPriority, this, GridPosition, workPos.Value);
                _jobs.Add(job);
                JobManager.Instance.RegisterVacancy(job);
            }
            Debug.Log($"[Building] {Data.DisplayName} создал {_jobs.Count} вакансий: {Data.JobType} [{Data.JobPriority}]");
        }

        /// <summary>
        /// Ищет ближайшую свободную проходимую клетку вокруг footprint'а (радиус 1-2).
        /// Бронирование НЕ делает — это обязанность вызывающего.
        /// </summary>
        private Vector2Int? FindFreeWorkCell(Vector2Int anchor, Vector2Int size)
        {
            var grid = GridSystem.Instance;
            if (grid == null) return null;

            Vector2Int? best = null;
            float bestDist = float.MaxValue;

            for (int radius = 1; radius <= 2; radius++)
            {
                for (int x = -radius; x < size.x + radius; x++)
                {
                    for (int y = -radius; y < size.y + radius; y++)
                    {
                        // Пропускаем внутренние клетки (занятые зданием)
                        bool interior = (x >= 0 && x < size.x && y >= 0 && y < size.y);
                        if (interior) continue;

                        // Для radius=2 — только на "рамке" радиуса 2
                        if (radius == 2)
                        {
                            bool onOuterRing = (x == -2 || x == size.x + 1 || y == -2 || y == size.y + 1);
                            if (!onOuterRing) continue;
                        }

                        var c = anchor + new Vector2Int(x, y);
                        if (!grid.IsValidCell(c.x, c.y)) continue;

                        var cell = grid.GetCell(c.x, c.y);
                        if (cell == null || !cell.IsTraversable()) continue;
                        if (cell.OccupiedBy != null) continue;
                        if (VillagerManager.IsWorkPositionReserved(c)) continue;

                        // Ближайшая к центру здания
                        Vector3 buildingCenter = grid.CellToWorld(anchor.x + size.x / 2, anchor.y + size.y / 2);
                        Vector3 candidateWorld = grid.CellToWorld(c.x, c.y);
                        float d = Vector3.Distance(buildingCenter, candidateWorld);
                        if (d < bestDist)
                        {
                            bestDist = d;
                            best = c;
                        }
                    }
                }
                if (best.HasValue) return best;
            }
            return best;
        }

        private IEnumerator BuildRoutine(float duration)
        {
            _buildProgress = 0f;
            float t = 0f;
            while (t < duration && _state == BuildingState.Building)
            {
                t += Time.deltaTime;
                _buildProgress = Mathf.Clamp01(t / duration);
                yield return null;
            }
            _buildProgress = 1f;
            _buildRoutine = null;

            if (_state == BuildingState.Building)
            {
                SetState(BuildingState.Active);
                OnPlaced();
                EventBus.Publish(new BuildingPlacedEvent { Data = Data, Position = GridPosition });
            }
        }

        // ============ ЖИТЕЛИ (HOUSING) ============

        public bool TryAddResident(GameObject villager)
        {
            if (!HasFreeSpace) return false;
            _residents++;
            Debug.Log($"[Building] {Data.DisplayName}: заселён ({_residents}/{Data.MaxResidents})");
            return true;
        }

        public void RemoveResident(GameObject villager)
        {
            if (_residents > 0) _residents--;
            Debug.Log($"[Building] {Data.DisplayName}: выселен ({_residents}/{Data.MaxResidents})");
        }

        // ============ РАБОТА ============

        public IReadOnlyList<Job> GetJobs() => _jobs;

        // ============ КЛЕТКИ ============

        public IEnumerable<Vector2Int> GetOccupiedCells()
        {
            if (Data == null) yield break;
            foreach (var cell in FootprintUtil.GetOccupiedCells(GridPosition, Data.Size, Rotation))
                yield return cell;
        }

        // ============ СОСТОЯНИЯ ============

        public void SetState(BuildingState newState)
        {
            if (_state == newState) return;

            // Запрет выхода из Destroyed
            if (_state == BuildingState.Destroyed)
            {
                Debug.LogWarning($"[Building] {Data?.DisplayName} уже Destroyed, переход в {newState} запрещён.");
                return;
            }

            // Запрет прыжка Placing → Active (только через Building)
            if (_state == BuildingState.Placing && newState == BuildingState.Active)
                newState = BuildingState.Building;

            _state = newState;

            if (_state == BuildingState.Destroyed)
            {
                if (_buildRoutine != null) { StopCoroutine(_buildRoutine); _buildRoutine = null; }

                // 1. Снимаем регистрацию клеток
                var occupied = new List<Vector2Int>();
                if (GridSystem.Instance != null)
                {
                    foreach (var cell in GetOccupiedCells())
                    {
                        GridSystem.Instance.UnregisterBuilding(cell.x, cell.y, gameObject);
                        occupied.Add(cell);
                    }
                }

                // 2. Снимаем вакансии + освобождаем рабочие позиции
                if (JobManager.Instance != null && _jobs != null)
                {
                    foreach (var j in _jobs)
                    {
                        JobManager.Instance.UnregisterVacancy(j);
                        VillagerManager.ReleaseWorkPosition(j.WorkPosition);
                    }
                    _jobs.Clear();
                }

                // 3. Возврат 50% ресурсов
                RefundHalfCost();

                // 4. Хуки
                OnRemoved();

                // 5. События
                EventBus.Publish(new BuildingRemovedEvent { Data = Data, Position = GridPosition });
                EventBus.Publish(new PathInvalidationEvent { Cells = occupied });

                // 6. Уничтожение GameObject (с задержкой на случай эффектов)
                Destroy(gameObject, 0.1f);
            }
        }

        // ============ УРОН / ЛЕЧЕНИЕ ============

        public void TakeDamage(int amount)
        {
            if (amount <= 0) return;
            if (_state == BuildingState.Destroyed) return;

            _currentHealth = Mathf.Max(0, _currentHealth - amount);
            Debug.Log($"[Building] {Data.DisplayName} −{amount} HP, осталось {_currentHealth}");

            if (_currentHealth <= 0)
                SetState(BuildingState.Destroyed);
        }

        public void Heal(int amount)
        {
            if (amount <= 0) return;
            if (Data == null) return;
            if (_state == BuildingState.Destroyed) return;

            _currentHealth = Mathf.Min(Data.MaxHealth, _currentHealth + amount);
        }

        // ============ ВОЗВРАТ РЕСУРСОВ ============

        private void RefundHalfCost()
        {
            if (Data == null || Data.BuildCost == null) return;
            if (ResourceManager.Instance == null) return;

            foreach (var ra in Data.BuildCost)
            {
                if (ra == null || ra.Resource == null) continue;
                int refund = Mathf.FloorToInt(ra.Amount * 0.5f);
                if (refund > 0) ResourceManager.Instance.AddResource(ra.Resource, refund);
            }
            Debug.Log($"[Building] Возвращено 50% за {Data.DisplayName}");
        }

        // ============ ХУКИ ============

        protected virtual void OnPlaced() { }
        protected virtual void OnRemoved() { }

        // ============ ОЧИСТКА ============

        private void OnDestroy()
        {
            // Отписываем от менеджеров
            if (BuildingManager.Instance != null) BuildingManager.Instance.Unregister(this);
            if (HousingManager.Instance != null) HousingManager.Instance.Unregister(this);

            // Освобождаем вакансии и позиции (если SetState(Destroyed) не вызывался)
            if (_jobs != null)
            {
                foreach (var j in _jobs)
                {
                    if (JobManager.Instance != null) JobManager.Instance.UnregisterVacancy(j);
                    VillagerManager.ReleaseWorkPosition(j.WorkPosition);
                }
                _jobs.Clear();
            }

            if (_buildRoutine != null) StopCoroutine(_buildRoutine);

            // Освобождаем занятые клетки (на случай прямого Destroy)
            if (GridSystem.Instance != null && Data != null)
            {
                foreach (var cell in GetOccupiedCells())
                    GridSystem.Instance.UnregisterBuilding(cell.x, cell.y, gameObject);
            }
        }

        // ============ DEBUG ============

        private void OnMouseDown()
        {
            if (!debugClickDamage) return;
            Debug.Log($"[Building] DEBUG клик по {Data?.DisplayName}");
            TakeDamage(20);
        }
    }
}