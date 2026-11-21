using System.Collections.Generic;
using UnityEngine;
using KingdomsClone.Data;
using KingdomsClone.Grid;
using KingdomsClone.Pathfinding;
using KingdomsClone.Core;
using KingdomsClone.Core.Events;
using KingdomsClone.Core.Pooling;

namespace KingdomsClone.Gameplay
{
    public class Villager : MonoBehaviour, IPoolable
    {
        [Header("Data")]
        [SerializeField] private UnitDataSO data;

        [Header("Identity")]
        [SerializeField] private string _villagerName;
        [SerializeField] private bool _isMale;
        [SerializeField] private float _happiness = 5f;

        [Header("Runtime (Read Only)")]
        [SerializeField] private VillagerState _state = VillagerState.Idle;
        [SerializeField] private Vector2Int _targetCell = new Vector2Int(-1, -1);
        [SerializeField] private int _currentPathIndex;
        [SerializeField] private int _currentPathLength;
        [SerializeField] private Vector2Int _workPosition = new Vector2Int(-1, -1);
        [SerializeField] private ResourceDataSO _carriedResource;
        [SerializeField] private int _carriedAmount;
        [SerializeField] private int _hunger;
        [SerializeField] private int _lastMealDay = -1;
        [SerializeField] private int _lastCheckDay = -1;

        public UnitDataSO Data => data;
        public VillagerState State => _state;
        public BuildingBase HomeBuilding { get; private set; }
        public Job CurrentJob { get; private set; }
        public bool IsCarrying => _carriedResource != null;
        public ResourceDataSO CarriedResource => _carriedResource;
        public int CarriedAmount => _carriedAmount;
        public int HungerLevel => _hunger;
        public string VillagerName => _villagerName;
        public bool IsMale => _isMale;
        public float Happiness => _happiness;
        public bool HasEatenToday => TimeManager.Instance != null && _lastMealDay == TimeManager.Instance.CurrentDay;

        public System.Action<VillagerState, VillagerState> OnStateChanged;

        private float _stateTimer;
        private const float IdleWanderInterval = 3f;
        private const float WorkDistance = 3f;
        private const float RetryWorkInterval = 2f;
        private const int MaxHunger = 3;

        private float _workCycleTimer;
        private bool _isDeliveringToStorage;
        private BuildingBase _deliveryStorage;
        private LocalStorage _deliveryStorageComp;
        private Vector2Int _deliveryDropPos = new Vector2Int(-1, -1);

        private bool _isGoingToEat;
        private BuildingBase _eatStorage;
        private bool _eatDone;
        private float _lastRecipeWarningTime = -10f;
        private float _cachedProductionMultiplier = 1f;
        private float _lastMultiplierCheck = -10f;

        private List<Vector2Int> _currentPath;
        private GameObject _carryIndicator;
        private MeshRenderer _carryRenderer;
        private bool _cleanedUp;

        // ============ POOL ============

        public void OnGetFromPool()
        {
            _cleanedUp = false;
            _state = VillagerState.Idle;
            _stateTimer = 0f;
            _currentPath = null;
            _currentPathIndex = 0;
            _currentPathLength = 0;
            _targetCell = new Vector2Int(-1, -1);
            _workPosition = new Vector2Int(-1, -1);
            _carriedResource = null;
            _carriedAmount = 0;
            _hunger = 0;
            _lastMealDay = -1;
            _lastCheckDay = -1;
            _workCycleTimer = 0f;
            _isDeliveringToStorage = false;
            _deliveryStorage = null;
            _deliveryStorageComp = null;
            _deliveryDropPos = new Vector2Int(-1, -1);
            _isGoingToEat = false;
            _eatStorage = null;
            _eatDone = false;
            _lastRecipeWarningTime = -10f;
            _cachedProductionMultiplier = 1f;
            _lastMultiplierCheck = -10f;
            CurrentJob = null;
            HomeBuilding = null;

            _villagerName = VillagerNameGenerator.Generate(out _isMale);
            _happiness = HappinessSystem.Instance != null ? HappinessSystem.Instance.CurrentHappiness : 5f;

            if (_carryIndicator != null) _carryIndicator.SetActive(false);
            gameObject.name = $"Villager_{_villagerName}";
        }

        public void OnReturnToPool() => CleanupOnRemove();
        private void OnDestroy() => CleanupOnRemove();

        private void CleanupOnRemove()
        {
            if (_cleanedUp) return;
            _cleanedUp = true;
            if (JobManager.Instance != null)
            {
                JobManager.Instance.UnregisterUnemployed(this);
                if (CurrentJob != null) { CurrentJob.AssignedVillager = null; CurrentJob = null; }
            }
            if (HomeBuilding != null) { HomeBuilding.RemoveResident(gameObject); HomeBuilding = null; }
        }

        public void Initialize(UnitDataSO unitData)
        {
            data = unitData;
            if (string.IsNullOrEmpty(_villagerName))
                _villagerName = VillagerNameGenerator.Generate(out _isMale);
            gameObject.name = $"Villager_{_villagerName}";

            _carryIndicator = transform.Find("CarryIndicator")?.gameObject;
            _carryRenderer = _carryIndicator != null ? _carryIndicator.GetComponent<MeshRenderer>() : null;
            if (_carryIndicator != null) _carryIndicator.SetActive(false);

            ChangeState(VillagerState.Idle);
        }

        public void SetHome(BuildingBase home) { HomeBuilding = home; }

        private void SetCarryVisual(ResourceDataSO res)
        {
            if (_carryIndicator == null) return;
            if (res == null) { _carryIndicator.SetActive(false); return; }
            _carryIndicator.SetActive(true);
            if (_carryRenderer != null) _carryRenderer.material.color = res.UiColor;
        }

        // ============ ЕДА / ГОЛОД ============

        public void OnNewDay(int day)
        {
            if (_lastCheckDay == day) return;
            _lastCheckDay = day;
            if (_hunger >= MaxHunger) { OnDeath(); return; }
            StartEating();
        }

        /// <summary>
        /// ФИКС: правильная логика еды. Идём к БЛИЖАЙШЕМУ складу (не к «складу с местом» — 
        /// это для доставки, а не для еды). Если склада нет — едим на месте.
        /// </summary>
        private void StartEating()
        {
            var food = GameDataSO.Instance?.GetResource("Resource_Food");
            if (food == null) return;
            if (_isGoingToEat) return;

            // Сброс несомого ресурса в пул (не теряем)
            if (_carriedResource != null)
            {
                ResourceManager.Instance?.AddResource(_carriedResource, _carriedAmount);
                _carriedResource = null;
                _carriedAmount = 0;
                _isDeliveringToStorage = false;
                _deliveryStorage = null;
                _deliveryStorageComp = null;
                SetCarryVisual(null);
            }

            // Ищем ближайший склад (любой)
            var storageComp = StorageManager.Instance?.FindNearestWithSpace(transform.position, 1);
            if (storageComp == null || storageComp.Building == null)
            {
                TryEatHere();
                return;
            }

            var storage = storageComp.Building;
            var dropPos = FindDropPositionNearStorage(storage);
            if (!dropPos.HasValue)
            {
                TryEatHere();
                return;
            }

            _isGoingToEat = true;
            _eatStorage = storage;
            _deliveryDropPos = dropPos.Value;
            Debug.Log($"[Villager] {gameObject.name}: идёт есть на склад {storage.GridPosition}");
            MoveToCell(dropPos.Value, silent: true);
        }

        private void TryEatHere()
        {
            var food = GameDataSO.Instance?.GetResource("Resource_Food");
            if (food == null) return;
            if (ResourceManager.Instance.SpendSingle(food, 1))
            {
                _hunger = 0;
                _lastMealDay = TimeManager.Instance != null ? TimeManager.Instance.CurrentDay : -1;
                Debug.Log($"[Villager] {gameObject.name}: поел на месте (без склада)");
            }
            else
            {
                _hunger++;
                Debug.Log($"[Villager] {gameObject.name}: НЕЧЕГО ЕСТЬ! Голод {_hunger}/{MaxHunger}");
                if (_hunger >= MaxHunger) OnDeath();
            }
        }

        private void OnDeath()
        {
            if (VillagerManager.Instance != null) VillagerManager.Instance.DespawnVillager(this, playEffect: true);
            else Destroy(gameObject);
        }

        // ============ JOB ============

        public void AssignJob(Job job)
        {
            CurrentJob = job;
            if (job == null || job.Workplace == null) { _workPosition = new Vector2Int(-1, -1); return; }
            _workPosition = job.WorkPosition;
            MoveToCell(_workPosition, silent: false);
        }

        public void ClearJob()
        {
            if (_carriedResource != null)
            {
                ResourceManager.Instance?.AddResource(_carriedResource, _carriedAmount);
                _carriedResource = null;
                _carriedAmount = 0;
                _isDeliveringToStorage = false;
                _deliveryStorage = null;
                _deliveryStorageComp = null;
                SetCarryVisual(null);
            }
            CurrentJob = null;
            _workPosition = new Vector2Int(-1, -1);
        }

        // ============ PATH INVALIDATION ============

        private void OnEnable()
        {
            EventBus.Subscribe<PathInvalidationEvent>(OnPathInvalidated);
            EventBus.Subscribe<PathCacheClearEvent>(OnFullCacheInvalidated);
            EventBus.Subscribe<HappinessChangedEvent>(OnHappinessChanged);
        }
        private void OnDisable()
        {
            EventBus.Unsubscribe<PathInvalidationEvent>(OnPathInvalidated);
            EventBus.Unsubscribe<PathCacheClearEvent>(OnFullCacheInvalidated);
            EventBus.Unsubscribe<HappinessChangedEvent>(OnHappinessChanged);
        }

        private void OnFullCacheInvalidated(PathCacheClearEvent e)
        {
            if (_state == VillagerState.Move) RepathToTarget();
        }

        private void OnHappinessChanged(HappinessChangedEvent e)
        {
            _happiness = e.NewValue;
        }

        private void OnPathInvalidated(PathInvalidationEvent e)
        {
            if (_state != VillagerState.Move) return;
            if (_currentPath == null || _targetCell.x < 0) return;

            bool touched;
            if (e.Cells == null || e.Cells.Count == 0) touched = true;
            else
            {
                var affected = new HashSet<Vector2Int>(e.Cells);
                touched = false;
                for (int i = _currentPathIndex; i < _currentPath.Count; i++)
                    if (affected.Contains(_currentPath[i])) { touched = true; break; }
            }
            if (touched) RepathToTarget();
        }

        private void RepathToTarget()
        {
            var grid = GridSystem.Instance;
            var pf = PathfindingService.Instance;
            if (grid == null || pf == null) { ChangeState(VillagerState.Idle); return; }
            Vector2Int start = grid.WorldToCell(transform.position);
            var newPath = pf.FindPath(start, _targetCell);
            if (newPath == null || newPath.Count <= 1) { ChangeState(VillagerState.Idle); return; }
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

        public void ChangeState(VillagerState newState)
        {
            if (_state == newState) return;
            var old = _state;
            _state = newState;
            _stateTimer = 0f;
            if (newState == VillagerState.Work) _workCycleTimer = 0f;
            if (newState == VillagerState.Eat) _eatDone = false;
            if (newState != VillagerState.Move)
            {
                _currentPath = null;
                _currentPathIndex = 0;
                _currentPathLength = 0;
                if (newState == VillagerState.Idle) _targetCell = new Vector2Int(-1, -1);
            }
            OnStateChanged?.Invoke(old, newState);
        }

        private void UpdateIdle()
        {
            if (_carriedResource != null)
            {
                ResourceManager.Instance?.AddResource(_carriedResource, _carriedAmount);
                _carriedResource = null;
                _carriedAmount = 0;
                _isDeliveringToStorage = false;
                _deliveryStorage = null;
                _deliveryStorageComp = null;
                SetCarryVisual(null);
            }

            if (CurrentJob != null && _workPosition.x >= 0 && _stateTimer > RetryWorkInterval)
            {
                _stateTimer = 0f;
                MoveToCell(_workPosition, silent: true);
                return;
            }

            if (CurrentJob == null && JobManager.Instance != null)
                JobManager.Instance.RegisterUnemployed(this);

            if (_stateTimer > IdleWanderInterval)
            {
                _stateTimer = 0f;
                var grid = GridSystem.Instance;
                var pf = PathfindingService.Instance;
                if (grid == null || pf == null) return;
                Vector2Int myCell = grid.WorldToCell(transform.position);
                for (int tries = 0; tries < 8; tries++)
                {
                    Vector2Int candidate = myCell + new Vector2Int(Random.Range(-4, 5), Random.Range(-4, 5));
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
            if (_currentPath == null || _currentPathIndex >= _currentPath.Count) { OnArrivedAtDestination(); return; }
            var grid = GridSystem.Instance;
            if (grid == null) { ChangeState(VillagerState.Idle); return; }

            Vector2Int cellTarget = _currentPath[_currentPathIndex];
            Vector3 worldTarget = grid.CellToWorld(cellTarget.x, cellTarget.y);
            worldTarget.y = grid.GetSurfaceY(cellTarget.x, cellTarget.y);

            Vector3 toTarget = worldTarget - transform.position;
            toTarget.y = 0f;

            if (toTarget.magnitude < 0.15f)
            {
                _currentPathIndex++;
                if (_currentPathIndex >= _currentPath.Count) { OnArrivedAtDestination(); return; }
                transform.position = new Vector3(worldTarget.x, transform.position.y, worldTarget.z);
                return;
            }
            Vector3 dir = toTarget.normalized;
            float speed = data != null ? data.MoveSpeed : 3f;
            transform.position += dir * speed * Time.deltaTime;
            transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
        }

        private void OnArrivedAtDestination()
        {
            var grid = GridSystem.Instance;
            if (grid == null) { ChangeState(VillagerState.Idle); return; }

            // 1. Пришли есть
            if (_isGoingToEat && _eatStorage != null)
            {
                float dist = Vector3.Distance(transform.position, _eatStorage.transform.position);
                if (dist <= WorkDistance + 2f) { ChangeState(VillagerState.Eat); return; }
                if (_deliveryDropPos.x >= 0) MoveToCell(_deliveryDropPos, silent: true);
                else ChangeState(VillagerState.Idle);
                return;
            }

            // 2. Пришли на склад с грузом
            if (_isDeliveringToStorage && _carriedResource != null && _deliveryStorage != null)
            {
                float dist = Vector3.Distance(transform.position, _deliveryStorage.transform.position);
                if (dist <= WorkDistance + 2f)
                {
                    int accepted = 0;
                    if (_deliveryStorageComp != null)
                        accepted = _deliveryStorageComp.TryAdd(_carriedResource, _carriedAmount);

                    int overflow = _carriedAmount - accepted;
                    if (accepted > 0)
                    {
                        ResourceManager.Instance?.AddResource(_carriedResource, accepted);
                        Debug.Log($"[Villager] {gameObject.name}: положил {accepted} {_carriedResource.DisplayName} на склад {_deliveryStorage.GridPosition}");
                    }
                    if (overflow > 0)
                    {
                        ResourceManager.Instance?.AddResource(_carriedResource, overflow);
                        Debug.Log($"[Villager] {gameObject.name}: остаток {overflow} {_carriedResource.DisplayName} — в пул (склад полон)");
                    }

                    _carriedResource = null;
                    _carriedAmount = 0;
                    _isDeliveringToStorage = false;
                    _deliveryStorage = null;
                    _deliveryStorageComp = null;
                    SetCarryVisual(null);

                    if (CurrentJob != null && _workPosition.x >= 0) MoveToCell(_workPosition, silent: true);
                    else ChangeState(VillagerState.Idle);
                    return;
                }
                if (_deliveryDropPos.x >= 0) MoveToCell(_deliveryDropPos, silent: true);
                else ChangeState(VillagerState.Idle);
                return;
            }

            // 3. Пришли на работу
            if (CurrentJob != null && CurrentJob.Workplace != null && _workPosition.x >= 0)
            {
                // ФИКС: не переходим в Work, если здание не активно
                if (CurrentJob.Workplace.State != BuildingState.Active)
                {
                    ChangeState(VillagerState.Idle);
                    return;
                }

                Vector3 workplaceWorld = grid.CellToWorld(CurrentJob.WorkplaceCell.x, CurrentJob.WorkplaceCell.y);
                if (Vector3.Distance(transform.position, workplaceWorld) <= WorkDistance + 1f)
                {
                    ChangeState(VillagerState.Work);
                    return;
                }
            }
            ChangeState(VillagerState.Idle);
        }

        private void UpdateEat()
        {
            if (_eatDone) return;
            if (_stateTimer < 0.5f) return;
            _eatDone = true;

            var food = GameDataSO.Instance?.GetResource("Resource_Food");
            if (food != null && ResourceManager.Instance.SpendSingle(food, 1))
            {
                _hunger = 0;
                _lastMealDay = TimeManager.Instance != null ? TimeManager.Instance.CurrentDay : -1;
                Debug.Log($"[Villager] {gameObject.name}: ПОЕЛ на складе {_eatStorage?.GridPosition}. Голод 0/{MaxHunger}");
            }
            else
            {
                _hunger++;
                Debug.Log($"[Villager] {gameObject.name}: НЕЧЕГО ЕСТЬ на складе! Голод {_hunger}/{MaxHunger}");
                if (_hunger >= MaxHunger) { OnDeath(); return; }
            }

            _isGoingToEat = false;
            _eatStorage = null;
            if (CurrentJob != null && _workPosition.x >= 0) MoveToCell(_workPosition, silent: true);
            else ChangeState(VillagerState.Idle);
        }

        private void UpdateWork()
        {
            if (CurrentJob == null || CurrentJob.Workplace == null || CurrentJob.Workplace.Data == null)
            {
                ChangeState(VillagerState.Idle);
                return;
            }

            if (CurrentJob.Workplace.State != BuildingState.Active)
            {
                Debug.Log($"[Villager] {gameObject.name}: здание {CurrentJob.Workplace.Data.DisplayName} " +
                          $"не активно (state={CurrentJob.Workplace.State}), прерываю работу.");
                ChangeState(VillagerState.Idle);
                return;
            }

            var grid = GridSystem.Instance;
            if (grid == null) return;

            Vector3 workplaceWorld = grid.CellToWorld(CurrentJob.WorkplaceCell.x, CurrentJob.WorkplaceCell.y);
            if (Vector3.Distance(transform.position, workplaceWorld) > WorkDistance + 1f)
            {
                if (_workPosition.x >= 0) MoveToCell(_workPosition, silent: true);
                else ChangeState(VillagerState.Idle);
                return;
            }

            var recipe = CurrentJob.Workplace.Data.Recipe;
            var legacyRecipe = CurrentJob.Workplace.Data.Production;

            bool hasRecipe = recipe != null && recipe.HasOutputs;
            bool hasLegacy = legacyRecipe != null && legacyRecipe.HasOutputs;
            if (!hasRecipe && !hasLegacy) return;

            float cycleTime = hasRecipe ? recipe.CycleSeconds : legacyRecipe.CycleSeconds;
            IReadOnlyList<ResourceAmount> inputs = hasRecipe ? recipe.Inputs : legacyRecipe.Inputs;
            IReadOnlyList<ResourceAmount> outputs = hasRecipe ? recipe.Outputs : legacyRecipe.Outputs;

            _workCycleTimer += Time.deltaTime * GetProductionMultiplier();
            if (_workCycleTimer < Mathf.Max(0.5f, cycleTime)) return;

            bool needsInputs = inputs != null && inputs.Count > 0;
            if (needsInputs)
            {
                if (ResourceManager.Instance == null || !ResourceManager.Instance.HasResources(inputs))
                {
                    if (Time.time - _lastRecipeWarningTime > 3f)
                    {
                        _lastRecipeWarningTime = Time.time;
                        string missing = BuildMissingInputsMessage(inputs);
                        Debug.LogWarning($"[Villager] {gameObject.name}: нет входов для {GetRecipeName(recipe, legacyRecipe)}. {missing}");
                    }
                    _workCycleTimer = 0f;
                    return;
                }
                if (!ResourceManager.Instance.SpendResources(inputs))
                {
                    _workCycleTimer = 0f;
                    return;
                }
            }

            _workCycleTimer = 0f;

            if (outputs == null || outputs.Count == 0) return;
            var output = outputs[0];
            if (output == null || output.Resource == null) return;

            _carriedResource = output.Resource;
            _carriedAmount = output.Amount;
            SetCarryVisual(_carriedResource);

            Debug.Log($"[Villager] {gameObject.name}: произвёл {_carriedAmount} {_carriedResource.DisplayName}");

            var storageComp = StorageManager.Instance?.FindNearestWithSpace(transform.position, _carriedAmount);
            if (storageComp == null || storageComp.Building == null)
            {
                ResourceManager.Instance?.AddResource(_carriedResource, _carriedAmount);
                Debug.Log($"[Villager] {gameObject.name}: нет склада с местом — в пул");
                _carriedResource = null;
                _carriedAmount = 0;
                SetCarryVisual(null);
                return;
            }

            var dropPos = FindDropPositionNearStorage(storageComp.Building);
            if (!dropPos.HasValue)
            {
                ResourceManager.Instance?.AddResource(_carriedResource, _carriedAmount);
                _carriedResource = null;
                _carriedAmount = 0;
                SetCarryVisual(null);
                return;
            }

            _isDeliveringToStorage = true;
            _deliveryStorage = storageComp.Building;
            _deliveryStorageComp = storageComp;
            _deliveryDropPos = dropPos.Value;
            MoveToCell(dropPos.Value, silent: false);
        }

        private void UpdateSleep() { if (_stateTimer > 4f) ChangeState(VillagerState.Idle); }
        private void UpdateFlee()  { if (_stateTimer > 2f) ChangeState(VillagerState.Idle); }

        // ============ УТИЛИТЫ ============

        private Vector2Int? FindDropPositionNearStorage(BuildingBase storage)
        {
            var grid = GridSystem.Instance;
            if (grid == null) return null;
            var size = storage.RotatedSize;
            Vector2Int anchor = storage.GridPosition;
            Vector2Int? best = null;
            float bestDist = float.MaxValue;
            Vector3 myPos = transform.position;

            for (int radius = 1; radius <= 2; radius++)
            {
                for (int x = -radius; x < size.x + radius; x++)
                {
                    for (int y = -radius; y < size.y + radius; y++)
                    {
                        bool interior = (x >= 0 && x < size.x && y >= 0 && y < size.y);
                        if (interior) continue;
                        if (radius == 2)
                        {
                            bool onOuterRing = (x == -2 || x == size.x + 1 || y == -2 || y == size.y + 1);
                            if (!onOuterRing) continue;
                        }
                        var c = anchor + new Vector2Int(x, y);
                        if (!grid.IsValidCell(c.x, c.y)) continue;
                        var cell = grid.GetCell(c.x, c.y);
                        if (cell == null || !cell.IsTraversable()) continue;
                        Vector3 world = grid.CellToWorld(c.x, c.y);
                        float d = Vector3.Distance(myPos, world);
                        if (d < bestDist) { bestDist = d; best = c; }
                    }
                }
                if (best.HasValue) return best;
            }
            return best;
        }

        public void MoveToCell(Vector2Int target, bool silent = false)
        {
            var grid = GridSystem.Instance;
            if (grid == null) return;
            var pf = PathfindingService.Instance;
            if (pf == null) { if (!silent) Debug.LogError("[Villager] PathfindingService не в сцене!"); ChangeState(VillagerState.Idle); return; }
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

        private const float SeparationRadius = 0.6f;
        private const float SeparationStrength = 2.0f;

        private void LateUpdate()
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
                diff.y = 0;
                float d = diff.magnitude;
                if (d < SeparationRadius && d > 0.001f) { push += diff.normalized * (SeparationRadius - d); neighbors++; }
                else if (d <= 0.001f)
                {
                    Vector3 random = new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f)).normalized;
                    push += random * SeparationRadius * 0.5f;
                    neighbors++;
                }
            }
            if (neighbors > 0)
            {
                push /= neighbors;
                Vector3 newPos = transform.position + push * SeparationStrength * Time.deltaTime;
                var grid = GridSystem.Instance;
                if (grid != null)
                {
                    var cell = grid.WorldToCell(newPos);
                    var c = grid.GetCell(cell.x, cell.y);
                    if (c == null || !c.IsWalkable()) return;
                }
                transform.position = newPos;
            }
        }

        private static string GetRecipeName(RecipeDataSO so, ProductionRecipe legacy)
        {
            if (so != null && !string.IsNullOrEmpty(so.DisplayName)) return so.DisplayName;
            if (legacy != null) return "legacy";
            return "?";
        }

        private string BuildMissingInputsMessage(IReadOnlyList<ResourceAmount> inputs)
        {
            if (inputs == null || ResourceManager.Instance == null) return "";
            var sb = new System.Text.StringBuilder("Не хватает: ");
            bool first = true;
            foreach (var ra in inputs)
            {
                if (ra?.Resource == null) continue;
                int have = ResourceManager.Instance.GetAmount(ra.Resource);
                if (have >= ra.Amount) continue;
                if (!first) sb.Append(", ");
                sb.Append($"{ra.Resource.DisplayName} ({have}/{ra.Amount})");
                first = false;
            }
            return sb.ToString();
        }

        private float GetProductionMultiplier()
        {
            if (Time.time - _lastMultiplierCheck < 2f) return _cachedProductionMultiplier;
            _lastMultiplierCheck = Time.time;

            var tools = GameDataSO.Instance?.GetResource("Resource_Tools");
            if (tools == null || ResourceManager.Instance == null)
            {
                _cachedProductionMultiplier = 1f;
                return 1f;
            }

            int toolCount = ResourceManager.Instance.GetAmount(tools);
            float bonus = Mathf.Min(0.5f, toolCount * 0.0025f); // 200 инструментов = +50%
            _cachedProductionMultiplier = 1f + bonus;
            return _cachedProductionMultiplier;
        }
    }
}