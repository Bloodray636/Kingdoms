using System.Collections.Generic;
using UnityEngine;
using KingdomsClone.Data;
using KingdomsClone.Grid;
using KingdomsClone.Pathfinding;
using KingdomsClone.Core;
using KingdomsClone.Core.Events;

namespace KingdomsClone.Gameplay
{
    /// <summary>
    /// Житель. FSM + движение по A* + работа + separation.
    /// Состояния: Idle / Move / Work / Eat / Sleep / Flee.
    /// </summary>
    public class Villager : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] private UnitDataSO data;

        [Header("Runtime (Read Only)")]
        [SerializeField] private VillagerState _state = VillagerState.Idle;
        [SerializeField] private Vector2Int _targetCell = new Vector2Int(-1, -1);
        [SerializeField] private Vector2Int _workPosition = new Vector2Int(-1, -1);
        [SerializeField] private int _currentPathIndex;
        [SerializeField] private int _currentPathLength;

        public UnitDataSO Data => data;
        public VillagerState State => _state;
        public BuildingBase HomeBuilding { get; private set; }
        public Job CurrentJob { get; private set; }

        public System.Action<VillagerState, VillagerState> OnStateChanged;

        // Тайминги
        private float _stateTimer;
        private const float IdleWanderInterval = 3f;
        private const float RetryWorkInterval = 2f;
        private const float WorkDistance = 3f;

        // Separation
        private const float SeparationRadius = 0.6f;
        private const float SeparationStrength = 2.0f;

        // Текущий путь
        private List<Vector2Int> _currentPath;

        // ============ LIFECYCLE ============

        private void OnEnable()
        {
            EventBus.Subscribe<PathInvalidationEvent>(OnPathInvalidated);
            EventBus.Subscribe<PathCacheClearEvent>(OnFullCacheInvalidated);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<PathInvalidationEvent>(OnPathInvalidated);
            EventBus.Unsubscribe<PathCacheClearEvent>(OnFullCacheInvalidated);
        }

        public void Initialize(UnitDataSO unitData)
        {
            data = unitData;
            gameObject.name = $"Villager_{GetInstanceID()}";
            ChangeState(VillagerState.Idle);
        }

        public void SetHome(BuildingBase home)
        {
            HomeBuilding = home;
        }

        // ============ РАБОТА ============

        public void AssignJob(Job job)
        {
            CurrentJob = job;

            if (job == null || job.Workplace == null)
            {
                _workPosition = new Vector2Int(-1, -1);
                return;
            }

            // workPosition уже назначена и забронирована при регистрации Job
            _workPosition = job.WorkPosition;
            MoveToCell(_workPosition, silent: false);
        }

        public void ClearJob()
        {
            CurrentJob = null;
            _workPosition = new Vector2Int(-1, -1);
        }

        // ============ РЕАКЦИЯ НА ИНВАЛИДАЦИЮ ПУТЕЙ ============

        private void OnFullCacheInvalidated(PathCacheClearEvent e)
        {
            if (_state == VillagerState.Move) RepathToTarget();
        }

        private void OnPathInvalidated(PathInvalidationEvent e)
        {
            if (_state != VillagerState.Move) return;
            if (_currentPath == null || _targetCell.x < 0) return;

            bool touched;
            if (e.Cells == null || e.Cells.Count == 0)
            {
                touched = true;
            }
            else
            {
                var affected = new HashSet<Vector2Int>(e.Cells);
                touched = false;
                for (int i = _currentPathIndex; i < _currentPath.Count; i++)
                {
                    if (affected.Contains(_currentPath[i])) { touched = true; break; }
                }
            }

            if (touched)
            {
                Debug.Log($"[Villager] {gameObject.name}: путь невалидный, перепрокладываю.");
                RepathToTarget();
            }
        }

        private void RepathToTarget()
        {
            var grid = GridSystem.Instance;
            var pf = PathfindingService.Instance;
            if (grid == null || pf == null) { ChangeState(VillagerState.Idle); return; }

            Vector2Int start = grid.WorldToCell(transform.position);
            var newPath = pf.FindPath(start, _targetCell);

            if (newPath == null || newPath.Count <= 1)
            {
                Debug.Log($"[Villager] {gameObject.name}: путь больше не существует, Idle.");
                ChangeState(VillagerState.Idle);
                return;
            }

            _currentPath = newPath;
            _currentPathIndex = 1;
            _currentPathLength = newPath.Count;
        }

        // ============ UPDATE ============

        private void Update()
        {
            _stateTimer += Time.deltaTime;

            switch (_state)
            {
                case VillagerState.Idle:  UpdateIdle();  break;
                case VillagerState.Move:  UpdateMove();  break;
                case VillagerState.Work:  UpdateWork();  break;
                case VillagerState.Eat:   UpdateEat();   break;
                case VillagerState.Sleep: UpdateSleep(); break;
                case VillagerState.Flee:  UpdateFlee();  break;
            }
        }

        private void LateUpdate()
        {
            ApplySeparation();
        }

        // ============ SEPARATION ============

        private void ApplySeparation()
        {
            var vm = VillagerManager.Instance;
            if (vm == null) return;

            Vector3 push = Vector3.zero;
            int neighbors = 0;

            var all = vm.AllVillagers;
            for (int i = 0; i < all.Count; i++)
            {
                var other = all[i];
                if (other == null || other == this) continue;

                Vector3 diff = transform.position - other.transform.position;
                diff.y = 0f;
                float d = diff.magnitude;

                if (d < SeparationRadius && d > 0.001f)
                {
                    push += diff.normalized * (SeparationRadius - d);
                    neighbors++;
                }
                else if (d <= 0.001f)
                {
                    // Полное наложение — толкаем в случайную сторону
                    Vector3 random = new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f)).normalized;
                    push += random * SeparationRadius * 0.5f;
                    neighbors++;
                }
            }

            if (neighbors == 0) return;

            push /= neighbors;
            Vector3 newPos = transform.position + push * SeparationStrength * Time.deltaTime;

            // Не выталкиваем в непроходимую клетку
            var grid = GridSystem.Instance;
            if (grid != null)
            {
                var cell = grid.WorldToCell(newPos);
                var c = grid.GetCell(cell.x, cell.y);
                if (c == null || !c.IsWalkable()) return;
            }

            transform.position = newPos;
        }

        // ============ СМЕНА СОСТОЯНИЯ ============

        public void ChangeState(VillagerState newState)
        {
            if (_state == newState) return;

            var old = _state;
            _state = newState;
            _stateTimer = 0f;

            if (newState != VillagerState.Move)
            {
                _currentPath = null;
                _currentPathIndex = 0;
                _currentPathLength = 0;
                if (newState == VillagerState.Idle) _targetCell = new Vector2Int(-1, -1);
            }

            OnStateChanged?.Invoke(old, newState);
        }

        // ============ ЛОГИКА СОСТОЯНИЙ ============

        private void UpdateIdle()
        {
            // Если есть работа, но не на месте — пытаемся вернуться (раз в RetryWorkInterval)
            if (CurrentJob != null && _workPosition.x >= 0 && _stateTimer > RetryWorkInterval)
            {
                _stateTimer = 0f;
                MoveToCell(_workPosition, silent: true);
                return;
            }

            // Регистрируемся как безработный
            if (CurrentJob == null && JobManager.Instance != null)
                JobManager.Instance.RegisterUnemployed(this);

            // Гуляем
            if (_stateTimer > IdleWanderInterval)
            {
                _stateTimer = 0f;
                var grid = GridSystem.Instance;
                var pf = PathfindingService.Instance;
                if (grid == null || pf == null) return;

                Vector2Int myCell = grid.WorldToCell(transform.position);
                for (int tries = 0; tries < 8; tries++)
                {
                    Vector2Int candidate = myCell + new Vector2Int(
                        Random.Range(-4, 5), Random.Range(-4, 5));
                    if (!grid.IsValidCell(candidate.x, candidate.y)) continue;
                    var c = grid.GetCell(candidate.x, candidate.y);
                    if (c == null || !c.IsTraversable()) continue;
                    if (HomeBuilding != null && c.OccupiedBy == HomeBuilding.gameObject) continue;

                    var path = pf.FindPath(myCell, candidate);
                    if (path == null || path.Count < 2) continue;

                    _currentPath = path;
                    _currentPathIndex = 1;
                    _currentPathLength = path.Count;
                    _targetCell = candidate;
                    ChangeState(VillagerState.Move);
                    return;
                }
            }
        }

        private void UpdateMove()
        {
            if (_currentPath == null || _currentPathIndex >= _currentPath.Count)
            {
                OnArrivedAtDestination();
                return;
            }

            var grid = GridSystem.Instance;
            if (grid == null) { ChangeState(VillagerState.Idle); return; }

            Vector2Int cellTarget = _currentPath[_currentPathIndex];
            Vector3 worldTarget = grid.CellToWorld(cellTarget.x, cellTarget.y);
            worldTarget.y = grid.GetSurfaceY(cellTarget.x, cellTarget.y);

            Vector3 toTarget = worldTarget - transform.position;
            toTarget.y = 0f;

            // Дошли до текущей точки — переходим к следующей
            if (toTarget.magnitude < 0.15f)
            {
                _currentPathIndex++;
                if (_currentPathIndex >= _currentPath.Count)
                {
                    OnArrivedAtDestination();
                    return;
                }
                transform.position = new Vector3(worldTarget.x, transform.position.y, worldTarget.z);
                return;
            }

            Vector3 dir = toTarget.normalized;
            float speed = data != null ? data.MoveSpeed : 3f;
            transform.position += dir * speed * Time.deltaTime;
            transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
        }

        /// <summary>
        /// Пришли в конечную точку — определяем, что делать.
        /// Если это работа (и мы близко к зданию) → Work. Иначе → Idle.
        /// </summary>
        private void OnArrivedAtDestination()
        {
            if (CurrentJob != null && CurrentJob.Workplace != null && _workPosition.x >= 0)
            {
                var grid = GridSystem.Instance;
                if (grid != null)
                {
                    Vector3 workplaceWorld = grid.CellToWorld(CurrentJob.WorkplaceCell.x, CurrentJob.WorkplaceCell.y);
                    float dist = Vector3.Distance(transform.position, workplaceWorld);
                    if (dist <= WorkDistance)
                    {
                        ChangeState(VillagerState.Work);
                        return;
                    }
                }
            }
            ChangeState(VillagerState.Idle);
        }

        private void UpdateWork()
        {
            // ЗАГЛУШКА. В 3.6 — реальное производство.
            if (CurrentJob == null || CurrentJob.Workplace == null)
            {
                ChangeState(VillagerState.Idle);
                return;
            }

            var grid = GridSystem.Instance;
            if (grid == null) return;

            Vector3 workplaceWorld = grid.CellToWorld(CurrentJob.WorkplaceCell.x, CurrentJob.WorkplaceCell.y);
            float dist = Vector3.Distance(transform.position, workplaceWorld);

            // Отошёл слишком далеко — вернуться
            if (dist > WorkDistance + 1f)
            {
                if (_workPosition.x >= 0)
                    MoveToCell(_workPosition, silent: true);
                else
                    ChangeState(VillagerState.Idle);
                return;
            }

            // Стоим и "работаем" (заглушка)
            _stateTimer = 0f;
        }

        private void UpdateEat()
        {
            if (_stateTimer > 2f) ChangeState(VillagerState.Idle);
        }

        private void UpdateSleep()
        {
            if (_stateTimer > 4f) ChangeState(VillagerState.Idle);
        }

        private void UpdateFlee()
        {
            if (_stateTimer > 2f) ChangeState(VillagerState.Idle);
        }

        // ============ ПУБЛИЧНЫЙ API ============

        public void MoveToCell(Vector2Int target, bool silent = false)
        {
            var grid = GridSystem.Instance;
            if (grid == null) return;

            var pf = PathfindingService.Instance;
            if (pf == null)
            {
                if (!silent) Debug.LogError("[Villager] PathfindingService не в сцене!");
                ChangeState(VillagerState.Idle);
                return;
            }

            Vector2Int start = grid.WorldToCell(transform.position);
            var path = pf.FindPath(start, target);

            if (path == null || path.Count == 0)
            {
                if (!silent) Debug.LogWarning($"[Villager] {gameObject.name}: путь {start} → {target} не найден.");
                ChangeState(VillagerState.Idle);
                return;
            }

            _currentPath = path;
            _currentPathIndex = 1;
            _currentPathLength = path.Count;
            _targetCell = target;
            ChangeState(VillagerState.Move);
        }

        public void ForceState(VillagerState s) => ChangeState(s);

        // ============ ВИЗУАЛИЗАЦИЯ ============

        private void OnDrawGizmosSelected()
        {
            var grid = GridSystem.Instance;
            if (grid == null) return;

            // Текущий путь
            if (_currentPath != null && _currentPath.Count > 1)
            {
                Gizmos.color = Color.green;
                for (int i = 0; i < _currentPath.Count - 1; i++)
                {
                    Vector3 a = grid.CellToWorld(_currentPath[i].x, _currentPath[i].y) + Vector3.up * 1.5f;
                    Vector3 b = grid.CellToWorld(_currentPath[i + 1].x, _currentPath[i + 1].y) + Vector3.up * 1.5f;
                    Gizmos.DrawLine(a, b);
                }
            }

            // Рабочее место
            if (CurrentJob != null)
            {
                Gizmos.color = Color.cyan;
                Vector3 w = grid.CellToWorld(CurrentJob.WorkplaceCell.x, CurrentJob.WorkplaceCell.y) + Vector3.up * 2f;
                Gizmos.DrawWireSphere(w, WorkDistance);
            }
        }
    }
}