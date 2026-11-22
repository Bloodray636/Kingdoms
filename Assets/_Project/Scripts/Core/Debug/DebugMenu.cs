using UnityEngine;
using UnityEngine.InputSystem;
using KingdomsClone.CameraControl;
using KingdomsClone.Core;
using KingdomsClone.Core.Events;
using KingdomsClone.Data;
using KingdomsClone.Gameplay;
using KingdomsClone.Grid;
using KingdomsClone.Pathfinding;
using KingdomsClone.Core.Pooling;

namespace KingdomsClone.Core.DebugTools
{
    public class DebugMenu : MonoBehaviour
    {
        private GameObject _testProjectilePrefab;
        private static bool _isOpen = false;
        public static bool IsOpen => _isOpen;

        private RTSCamera _camera;
        private string _seedInput = "";
        private Vector2 _scrollPos;
        private bool _showCachedPaths = false;

        private void Start()
        {
            Debug.Log("[DebugMenu] Скрипт загружен. Нажми ` (тильда) для открытия.");
        }

        private void Update()
        {
            var kb = Keyboard.current;

            if (kb == null) return;

            if (kb.backquoteKey.wasPressedThisFrame) {
                _isOpen = !_isOpen;
            }
        }

        private void OnGUI()
        {
            DrawTopRightStatus();

            if (!_isOpen) return;

            float winW = 400f;
            float winH = Mathf.Min(900f, Screen.height - 20f);
            Rect box = new Rect(10, 10, winW, winH);
            GUI.Box(box, "🛠️ DEBUG MENU (`)");

            GUILayout.BeginArea(new Rect(box.x + 10, box.y + 30, winW - 20, winH - 40));
            _scrollPos = GUILayout.BeginScrollView(_scrollPos);

            DrawResourcesSection();     
            GUILayout.Space(10);

            DrawMapSection();           
            GUILayout.Space(10);

            DrawVillagerSection();      
            GUILayout.Space(10);

            DrawStorageSection();       
            GUILayout.Space(10);

            DrawRecipesSection();       
            GUILayout.Space(10);

            DrawJobsSection();          
            GUILayout.Space(10);

            DrawCameraSection();        
            GUILayout.Space(10);

            DrawTimeSection();          
            GUILayout.Space(10);

            DrawTaxSection();           
            GUILayout.Space(10);

            DrawEconomySection();
            GUILayout.Space(10);

            DrawEnemySection();         
            GUILayout.Space(10);

            DrawGameStateSection();

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawTopRightStatus()
        {
            var tm = TimeManager.Instance;

            if (tm == null) return;

            string info = $"Day {tm.CurrentDay} ({tm.CurrentSeason}) | Speed: {tm.EffectiveSpeed:F1}x";

            GUI.Box(new Rect(Screen.width - 280, 10, 270, 25), info);
        }

        // Ресурсы

        private void DrawResourcesSection()
        {
            GUILayout.Label("Ресурсы");

            var rm = ResourceManager.Instance;
            var gd = GameDataSO.Instance;

            if (rm != null && gd != null && gd.Resources != null) {
                foreach (var res in gd.Resources) {
                    if (res == null) continue;

                    GUILayout.Label($"  {res.DisplayName}: {rm.GetAmount(res)}");
                }
            }

            if (GUILayout.Button("Выдать 1000 ресурсов")) {
                if (rm != null) {
                    rm.AddAllResources(1000);
                }
            }
        }

        // Карта

        private void DrawMapSection()
        {
            GUILayout.Label("Генерация карты");

            var grid = GridSystem.Instance;

            if (grid == null) { 
                GUILayout.Label("❌ GridSystem не в сцене!"); 
                return; 
            }

            GUILayout.Label($"Текущий сид: {grid.CurrentSeed}");

            if (GUILayout.Button("Рандомная карта")) {
                grid.RegenerateMap(UnityEngine.Random.Range(1, 999999));
            }

            GUILayout.BeginHorizontal();
            GUILayout.Label("Сид:", GUILayout.Width(40));
            _seedInput = GUILayout.TextField(_seedInput, GUILayout.Width(140));

            if (GUILayout.Button("Создать")) {
                if (int.TryParse(_seedInput, out int parsed)) {
                    grid.RegenerateMap(parsed);
                } else {
                    Debug.LogWarning($"[Debug] Некорректный сид: '{_seedInput}'");
                }
            }

            GUILayout.EndHorizontal();
        }

        // Жители

        private void DrawVillagerSection()
        {
            GUILayout.Label("Жители");

            var vm = VillagerManager.Instance;
            var hm = HousingManager.Instance;
            var hs = HappinessSystem.Instance;
            var jm = JobManager.Instance;

            if (vm == null) { 
                GUILayout.Label("❌ VillagerManager не в сцене!"); 
                return; 
            }

            int capacity = hm != null ? hm.GetTotalCapacity() : 0;
            int residents = hm != null ? hm.GetTotalResidents() : 0;
            float happiness = hs != null ? hs.CurrentHappiness : 0f;

            GUILayout.Label($"Население: {vm.AllVillagers.Count} (в домах: {residents}/{capacity})");
            GUILayout.Label($"Счастье: {happiness:F1}/10 (модификаторов: {hs?.GetModifiers().Count ?? 0})");
            GUILayout.Label($"Безработных: {jm?.Unemployed.Count ?? 0} | Вакансий: {jm?.OccupiedJobs ?? 0}/{jm?.TotalJobs ?? 0}");

            int hungry = 0, starving = 0;

            foreach (var v in vm.AllVillagers) {
                if (v == null) continue;
                if (v.HungerLevel > 0) hungry++;
                if (v.HungerLevel >= 2) starving++;
            }

            GUILayout.Label($"Голодных: {hungry} | На грани: {starving}");

            var food = GameDataSO.Instance?.GetResource("Resource_Food");

            if (food != null && ResourceManager.Instance != null) {
                GUILayout.Label($"Еда в пуле: {ResourceManager.Instance.GetAmount(food)}");
            }

            GUILayout.BeginHorizontal();

            if (GUILayout.Button("Счастье = 2")) {
                hs?.DebugForceSet(2f);
            }

            if (GUILayout.Button("Счастье = 5")) {
                hs?.DebugForceSet(5f);
            }

            if (GUILayout.Button("Счастье = 9")) {
                hs?.DebugForceSet(9f);
            }

            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();

            if (GUILayout.Button("Возродить")) {
                if (PopulationManager.Instance != null) {
                    PopulationManager.Instance.TrySpawn();
                }
            }

            if (GUILayout.Button("Спавн")) {
                if (PopulationManager.Instance != null) {
                    PopulationManager.Instance.TrySpawn(ignoreHappiness: true);
                }
            }

            GUILayout.EndHorizontal();

            if (vm.AllVillagers.Count == 0) return;

            var last = vm.AllVillagers[vm.AllVillagers.Count - 1];

            if (last == null) return;

            GUILayout.Label($"Последний: {last.VillagerName ?? "?"} | {last.State} | Голод: {last.HungerLevel}/3");
            GUILayout.Label($"Работа: {(last.CurrentJob != null ? last.CurrentJob.Type.ToString() : "безработный")}");
            GUILayout.Label($"Дом: {(last.HomeBuilding != null ? last.HomeBuilding.Data?.DisplayName : "нет")}");

            if (GUILayout.Button("Тест пути")) {
                var grid = GridSystem.Instance;
                var pf = PathfindingService.Instance;

                if (grid != null && pf != null) {
                    for (int t = 0; t < 20; t++) {
                        int rx = UnityEngine.Random.Range(0, grid.Width);
                        int ry = UnityEngine.Random.Range(0, grid.Height);
                        var c = grid.GetCell(rx, ry);

                        if (c != null && c.IsTraversable()) {
                            last.MoveToCell(new Vector2Int(rx, ry));
                            break;
                        }
                    }
                }
            }

            GUILayout.BeginHorizontal();

            if (GUILayout.Button("Работа")) {
                last.ForceState(VillagerState.Work);
            }

            if (GUILayout.Button("Еда")) {
                last.ForceState(VillagerState.Eat);
            }

            if (GUILayout.Button("Сон")) {
                last.ForceState(VillagerState.Sleep);
            }

            if (GUILayout.Button("Идти")) {
                last.ForceState(VillagerState.Flee);
            }

            GUILayout.EndHorizontal();

            var pf2 = PathfindingService.Instance;

            if (pf2 != null) {
                GUILayout.Label($"Кеш путей: {pf2.CacheCount} | Последний: {pf2.LastSearchMs:F2} ms");
                _showCachedPaths = GUILayout.Toggle(_showCachedPaths, "Показать все кэш. пути");
                pf2.SetDrawAllCachedPaths(_showCachedPaths);
            }
        }

        // Склады

        private void DrawStorageSection() {
            GUILayout.Label("Хранение");

            var sm = StorageManager.Instance;

            if (sm == null) { 
                GUILayout.Label("❌ StorageManager не в сцене!"); 
                return; 
            }

            GUILayout.Label($"Складов: {sm.AllStorages.Count} | Всего: {sm.GetTotalUsed()}/{sm.GetTotalCapacity()}");

            foreach (var s in sm.AllStorages)
            {
                if (s == null || s.Building == null) continue;

                GUILayout.Label($"Склад {s.Building.GridPosition}: {s.UsedSpace}/{s.Capacity}");

                foreach (var kvp in s.GetAllItems()) {
                    if (kvp.Value <= 0) continue;
                    var res = GameDataSO.Instance?.GetResource(kvp.Key);
                    string name = res != null ? res.DisplayName : kvp.Key;
                    GUILayout.Label($"{name}: {kvp.Value}");
                }
            }
        }

        // Рецепты

        private void DrawRecipesSection() {
            GUILayout.Label("Рецепты");
            var bm = BuildingManager.Instance;
            var rm = ResourceManager.Instance;

            if (bm == null || rm == null) { 
                GUILayout.Label("❌ нет менеджеров"); 
                return; 
            }

            int producers = 0;

            foreach (var b in bm.AllBuildings) {
                if (b == null || b.Data == null) continue;
                if (!b.Data.IsProducer) continue;

                producers++;

                var recipe = b.Data.Recipe;
                var legacy = b.Data.Production;

                GUILayout.Label($"<b>{b.Data.DisplayName}</b> {b.GridPosition}", new GUIStyle(GUI.skin.label) { richText = true });

                if (recipe != null)
                {
                    GUILayout.Label($"  SO: {recipe.DisplayName} ({recipe.CycleSeconds:F1}с)");

                    if (recipe.HasInputs) {
                        foreach (var ra in recipe.Inputs) {
                            if (ra?.Resource == null) continue;

                            int have = rm.GetAmount(ra.Resource);
                            string color = have >= ra.Amount ? "#8FFF8F" : "#FF8888";

                            GUILayout.Label($"<color={color}>{ra.Resource.DisplayName}: {ra.Amount} (есть {have})</color>", new GUIStyle(GUI.skin.label) { richText = true });
                        }
                    } else {
                        GUILayout.Label("Входы: нет");
                    }

                    foreach (var ra in recipe.Outputs) {
                        if (ra?.Resource == null) continue;

                        GUILayout.Label($"-> {ra.Resource.DisplayName}: {ra.Amount}");
                    }
                } else if (legacy != null) {
                    GUILayout.Label($"Наследие ({legacy.CycleSeconds:F1}с)");
                } else GUILayout.Label("❌ нет рецепта!");
            }

            if (producers == 0) {
                GUILayout.Label("Нет зданий-производителей");
            }
        }

        // Работа

        private void DrawJobsSection()
        {
            GUILayout.Label("Работа");
            var jm = JobManager.Instance;

            if (jm == null) { 
                GUILayout.Label("❌ JobManager не в сцене!"); 
                return; 
            }

            GUILayout.Label($"Вакансий: {jm.OccupiedJobs}/{jm.TotalJobs} | Безработных: {jm.Unemployed.Count}");

            if (GUILayout.Button("Принудительно назначить")) {
                jm.TryAssignAll();
            }

            if (jm.TotalJobs > 0 && jm.TotalJobs <= 10) {
                foreach (var job in jm.Vacancies)
                {
                    if (job == null) continue;

                    string assigned = job.AssignedVillager != null ? job.AssignedVillager.gameObject.name : "—";

                    GUILayout.Label($"{job.Type} [{job.Priority}] {job.WorkplaceCell}: {assigned}");
                }
            }
        }

        // Камера

        private void DrawCameraSection()
        {
            GUILayout.Label("Камера");
            if (GUILayout.Button("Центер")) {
                if (_camera == null) {
                    _camera = FindObjectOfType<RTSCamera>();
                }

                _camera?.TeleportTo(new Vector3(50f, 0f, 50f));
            }

            if (GUILayout.Button("Старт")) {
                if (_camera == null) {
                    _camera = FindObjectOfType<RTSCamera>();
                }

                var grid = GridSystem.Instance;

                if (_camera != null && grid != null) {
                    _camera.TeleportTo(grid.CellToWorld(grid.StartLocation.x, grid.StartLocation.y));
                }
            }
        }

        // Время

        private void DrawTimeSection()
        {
            GUILayout.Label("Время");
            var tm = TimeManager.Instance;
            if (tm == null) return;

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("1x")) {
                tm.SetDebugSpeed(1f);
            }

            if (GUILayout.Button("5x")) {
                tm.SetDebugSpeed(5f);
            }

            if (GUILayout.Button("10x")) {
                tm.SetDebugSpeed(10f);
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Пропустить час")) {
                tm.DebugAdvanceGameTime(TimeManager.SecondsPerDay / 24f);
            }

            if (GUILayout.Button("Пропустить день")) {
                tm.DebugAdvanceGameTime(TimeManager.SecondsPerDay);
            }

            if (GUILayout.Button("Пропустить месяц")) {
                tm.DebugAdvanceGameTime(TimeManager.SecondsPerDay * 30f);
            }
            GUILayout.EndHorizontal();
        }

        // Налоги

        private void DrawTaxSection()
        {
            GUILayout.Label("Налоги");

            var ts = TaxSystem.Instance;
            var rm = ResourceManager.Instance;
            var hs = HappinessSystem.Instance;
            var gold = GameDataSO.Instance?.GetResource("Resource_Gold");

            if (ts == null) { 
                GUILayout.Label("❌ TaxSystem не в сцене!"); 
                return; 
            }

            string levelRu = ts.CurrentLevel switch
            {
                TaxLevel.None => "None (+2)",
                TaxLevel.Low => "Low (1з/чел)",
                TaxLevel.Medium => "Medium (3з/чел, -2)",
                TaxLevel.High => "High (6з/чел, -5)",
                _ => "?"
            };

            GUILayout.Label($"Уровень: {levelRu}");
            GUILayout.Label($"Золото: {(gold != null && rm != null ? rm.GetAmount(gold) : 0)}");
            GUILayout.Label($"Счастье: {(hs != null ? hs.CurrentHappiness : 0):F1}/10");

            // Сокровищница
            var treasury = ts.FindTreasury();

            if (treasury == null) {
                var redStyle = new GUIStyle(GUI.skin.label) { normal = { textColor = Color.red } };
                GUILayout.Label("Сокровищница: НЕТ (налоги 0%)", redStyle);
            } else {
                bool hasTreasurer = ts.HasTreasurer(treasury);
                float eff = ts.GetTaxEfficiency();
                string effStr = hasTreasurer ? "100% (казначей)" : $"{eff:P0} (нет казначея)";
                var color = hasTreasurer ? Color.green : Color.yellow;
                var style = new GUIStyle(GUI.skin.label) { normal = { textColor = color } };
                GUILayout.Label($"Сокровищница {treasury.GridPosition}: {effStr}", style);
            }

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Нет")) {
                ts.SetTaxLevel(TaxLevel.None);
            }

            if (GUILayout.Button("Низкая")) {
                 ts.SetTaxLevel(TaxLevel.Low);
            }

            if (GUILayout.Button("Средняя")) {
                 ts.SetTaxLevel(TaxLevel.Medium);
            }

            if (GUILayout.Button("Высокая")) {
                ts.SetTaxLevel(TaxLevel.High);
            }
            GUILayout.EndHorizontal();

            if (GUILayout.Button("Force Day (собрать налоги)"))
            {
                if (TimeManager.Instance != null) {
                    TimeManager.Instance.DebugAdvanceGameTime(TimeManager.SecondsPerDay);
                }
            }
        }

        private void DrawEconomySection()
        {
            GUILayout.Label("Экономика");

            var rm = ResourceManager.Instance;
            var gd = GameDataSO.Instance;

            if (rm == null || gd == null) { 
                GUILayout.Label("❌ нет менеджеров"); 
                return; 
            }

            // Железная цепочка
            var ore = gd.GetResource("Resource_IronOre");
            var ingot = gd.GetResource("Resource_IronIngot");
            var tools = gd.GetResource("Resource_Tools");

            GUILayout.Label($"Руда: {(ore != null ? rm.GetAmount(ore) : 0)}");
            GUILayout.Label($"Слитки: {(ingot != null ? rm.GetAmount(ingot) : 0)}");
            GUILayout.Label($"Инструменты: {(tools != null ? rm.GetAmount(tools) : 0)}");

            // Бонус от инструментов
            if (tools != null) {
                int toolCount = rm.GetAmount(tools);
                float bonus = Mathf.Min(0.5f, toolCount * 0.0025f);

                var style = new GUIStyle(GUI.skin.label) { 
                    normal = { 
                        textColor = bonus > 0.1f ? Color.green : Color.white 
                    } 
                };

                GUILayout.Label($"Бонус к производству: +{bonus:P0} (макс +50% при 200 инструментах)", style);
            }
        }

        // Враги

        private void DrawEnemySection()
        {
            GUILayout.Label("Враги");

            if (GUILayout.Button("Выпустить снаряды (50)"))
            {
                if (PoolManager.Instance == null) return;

                if (_testProjectilePrefab == null) {
                    _testProjectilePrefab = TestProjectile.CreatePrefab();
                }

                PoolManager.Instance.Prewarm(_testProjectilePrefab, 20, 200);

                var cam = Camera.main;
                Vector3 origin = cam != null ? cam.transform.position + cam.transform.forward * 3f : Vector3.zero;

                for (int i = 0; i < 50; i++) {
                    var go = PoolManager.Instance.Spawn(_testProjectilePrefab, origin, Quaternion.identity);

                    if (go == null) continue;

                    var proj = go.GetComponent<TestProjectile>();

                    if (proj == null) continue;

                    Vector3 dir = new Vector3(Random.Range(-1f, 1f), Random.Range(0.5f, 1.5f), Random.Range(-1f, 1f)).normalized;
                    proj.Launch(dir * Random.Range(5f, 15f), 3f);
                }
            }

            if (PoolManager.Instance != null) {
                GUILayout.Label($"Пул: InUse={PoolManager.Instance.GetTotalInUseCount()}, Available={PoolManager.Instance.GetTotalAvailableCount()}");
            }

            GUILayout.Space(5);
            if (GUILayout.Button("Рейд (заглушка)"))
                EventBus.Publish(new SpawnRaidRequestedEvent { WaveNumber = 1 });
        }

        // Состояние

        private void DrawGameStateSection()
        {
            GUILayout.Label("Состояния");

            if (GameManager.Instance == null) return;

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Запуск")) {
              GameManager.Instance.StartGame();   
            }

            if (GUILayout.Button("Стоп")) {
             GameManager.Instance.PauseGame();   
            }

            if (GUILayout.Button("Меню")) {
              GameManager.Instance.QuitToMenu();   
            }
            GUILayout.EndHorizontal();
        }

        // Ресурсы: события

        private void OnEnable()
        {
            if (ResourceManager.Instance != null) {
                ResourceManager.Instance.OnResourceChanged += OnResourceChangedHandler;
            }
        }

        private void OnDisable()
        {
            if (ResourceManager.Instance != null) {
                ResourceManager.Instance.OnResourceChanged -= OnResourceChangedHandler;
            }
        }

        private void OnResourceChangedHandler(ResourceDataSO res, int oldAmt, int newAmt)
        {
            int delta = newAmt - oldAmt;
            string sign = delta > 0 ? "+" : "";
            Debug.Log($"[Resource] {res.DisplayName}: {oldAmt} → {newAmt} ({sign}{delta})");
        }
    }
}