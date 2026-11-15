using UnityEngine;
using UnityEngine.InputSystem;
using KingdomsClone.CameraControl;
using KingdomsClone.Core;
using KingdomsClone.Core.Events;
using KingdomsClone.Data;
using KingdomsClone.Gameplay;
using KingdomsClone.Grid;
using KingdomsClone.Pathfinding;

namespace KingdomsClone.Core.DebugTools
{
    public class DebugMenu : MonoBehaviour
    {
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

            if (kb.backquoteKey.wasPressedThisFrame)
                _isOpen = !_isOpen;
        }

        private void OnGUI()
        {
            DrawTopRightStatus();

            if (!_isOpen) return;

            float winW = 400f;
            float winH = Mathf.Min(800f, Screen.height - 20f);
            Rect box = new Rect(10, 10, winW, winH);
            GUI.Box(box, "🛠️ DEBUG MENU (`)");

            GUILayout.BeginArea(new Rect(box.x + 10, box.y + 30, winW - 20, winH - 40));
            _scrollPos = GUILayout.BeginScrollView(_scrollPos);

            DrawResourcesSection();
            GUILayout.Space(10);

            DrawMapSection();
            GUILayout.Space(10);

            DrawPopulationSection();
            GUILayout.Space(10);

            DrawVillagerSection();
            GUILayout.Space(10);

            DrawJobsSection();
            GUILayout.Space(10);

            DrawCameraSection();
            GUILayout.Space(10);

            DrawTimeSection();
            GUILayout.Space(10);

            DrawEnemySection();
            GUILayout.Space(10);

            DrawGameStateSection();

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        // ============ ВЕРХНИЙ ПРАВЫЙ УГОЛ ============

        private void DrawTopRightStatus()
        {
            var tm = TimeManager.Instance;
            if (tm == null) return;

            string info = $"Day {tm.CurrentDay} ({tm.CurrentSeason}) | Speed: {tm.EffectiveSpeed:F1}x";
            GUI.Box(new Rect(Screen.width - 280, 10, 270, 25), info);
        }

        // ============ РЕСУРСЫ ============

        private void DrawResourcesSection()
        {
            GUILayout.Label("--- Resources ---");

            var rm = ResourceManager.Instance;
            var gd = GameDataSO.Instance;

            if (rm != null && gd != null && gd.Resources != null)
            {
                foreach (var res in gd.Resources)
                {
                    if (res == null) continue;
                    GUILayout.Label($"  {res.DisplayName}: {rm.GetAmount(res)}");
                }
            }

            if (GUILayout.Button("+1000 All Resources"))
            {
                if (rm != null) rm.AddAllResources(1000);
                else Debug.LogWarning("[Debug] ResourceManager не найден");
            }
        }

        // ============ КАРТА ============

        private void DrawMapSection()
        {
            GUILayout.Label("--- Map Generation ---");

            var grid = GridSystem.Instance;
            if (grid == null)
            {
                GUILayout.Label("❌ GridSystem не в сцене!");
                return;
            }

            GUILayout.Label($"Current Seed: {grid.CurrentSeed}");

            if (GUILayout.Button("Random Map"))
            {
                grid.RegenerateMap(UnityEngine.Random.Range(1, 999999));
            }

            GUILayout.BeginHorizontal();
            GUILayout.Label("Seed:", GUILayout.Width(40));
            _seedInput = GUILayout.TextField(_seedInput, GUILayout.Width(140));
            if (GUILayout.Button("Apply"))
            {
                if (int.TryParse(_seedInput, out int parsed))
                    grid.RegenerateMap(parsed);
                else
                    Debug.LogWarning($"[Debug] Некорректный сид: '{_seedInput}'");
            }
            GUILayout.EndHorizontal();
        }

        // ============ НАСЕЛЕНИЕ ============

        private void DrawPopulationSection()
        {
            GUILayout.Label("--- Population ---");

            var vm = VillagerManager.Instance;
            var hm = HousingManager.Instance;
            var hs = HappinessSystem.Instance;

            if (vm == null)
            {
                GUILayout.Label("❌ VillagerManager не в сцене!");
                return;
            }

            int capacity = hm != null ? hm.GetTotalCapacity() : 0;
            int residents = hm != null ? hm.GetTotalResidents() : 0;
            float happiness = hs != null ? hs.CurrentHappiness : 0f;

            GUILayout.Label($"Всего жителей: {vm.AllVillagers.Count}");
            GUILayout.Label($"В домах: {residents} / {capacity}");
            GUILayout.Label($"Счастье: {happiness:F1} / 10");

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Happiness = 2")) hs?.SetHappiness(2f);
            if (GUILayout.Button("Happiness = 5")) hs?.SetHappiness(5f);
            if (GUILayout.Button("Happiness = 9")) hs?.SetHappiness(9f);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Try Spawn"))
            {
                if (PopulationManager.Instance != null)
                    PopulationManager.Instance.TrySpawn();
            }
            if (GUILayout.Button("Force Spawn"))
            {
                if (PopulationManager.Instance != null)
                    PopulationManager.Instance.TrySpawn(ignoreHappiness: true);
            }
            GUILayout.EndHorizontal();
        }

        // ============ ЖИТЕЛИ ============

        private void DrawVillagerSection()
        {
            GUILayout.Label("--- Villagers ---");

            var vm = VillagerManager.Instance;
            if (vm == null) return;

            if (GUILayout.Button("Spawn Villager (at start)"))
            {
                var data = GameDataSO.Instance?.GetUnit("Unit_Villager");
                if (data == null) Debug.LogError("[Debug] Unit_Villager не найден!");
                else if (GridSystem.Instance != null)
                    vm.SpawnVillager(GridSystem.Instance.StartLocation, data);
            }

            if (vm.AllVillagers.Count == 0) return;

            var last = vm.AllVillagers[vm.AllVillagers.Count - 1];
            if (last == null) return;

            string job = last.CurrentJob != null
                ? $"{last.CurrentJob.Type} [{last.CurrentJob.Priority}]"
                : "нет";
            GUILayout.Label($"Последний: {last.State} | Работа: {job}");

            if (GUILayout.Button("Test Path → random walkable"))
            {
                var grid = GridSystem.Instance;
                var pf = PathfindingService.Instance;
                if (grid != null && pf != null)
                {
                    for (int t = 0; t < 20; t++)
                    {
                        int rx = UnityEngine.Random.Range(0, grid.Width);
                        int ry = UnityEngine.Random.Range(0, grid.Height);
                        var c = grid.GetCell(rx, ry);
                        if (c != null && c.IsTraversable())
                        {
                            last.MoveToCell(new Vector2Int(rx, ry));
                            break;
                        }
                    }
                }
            }

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Idle"))  last.ForceState(VillagerState.Idle);
            if (GUILayout.Button("Work"))  last.ForceState(VillagerState.Work);
            if (GUILayout.Button("Sleep")) last.ForceState(VillagerState.Sleep);
            if (GUILayout.Button("Flee"))  last.ForceState(VillagerState.Flee);
            GUILayout.EndHorizontal();

            var pf2 = PathfindingService.Instance;
            if (pf2 != null)
            {
                GUILayout.Label($"Path Cache: {pf2.CacheCount} | Last: {pf2.LastSearchMs:F2} ms");
                _showCachedPaths = GUILayout.Toggle(_showCachedPaths, "Показать все кэш. пути (Scene)");
                pf2.SetDrawAllCachedPaths(_showCachedPaths);
            }
        }

        // ============ РАБОТА ============

        private void DrawJobsSection()
        {
            GUILayout.Label("--- Jobs ---");

            var jm = JobManager.Instance;
            if (jm == null)
            {
                GUILayout.Label("❌ JobManager не в сцене!");
                return;
            }

            GUILayout.Label($"Вакансий: {jm.OccupiedJobs} занято / {jm.TotalJobs} всего");
            GUILayout.Label($"Безработных: {jm.Unemployed.Count}");

            if (GUILayout.Button("Force Assign All"))
            {
                jm.TryAssignAll();
            }

            // Список вакансий (макс 10, чтобы не раздувать окно)
            if (jm.TotalJobs > 0 && jm.TotalJobs <= 10)
            {
                foreach (var job in jm.Vacancies)
                {
                    if (job == null) continue;
                    string assigned = job.AssignedVillager != null
                        ? job.AssignedVillager.gameObject.name
                        : "—";
                    GUILayout.Label($"  {job.Type} [{job.Priority}] {job.WorkplaceCell}: {assigned}");
                }
            }
            else if (jm.TotalJobs > 10)
            {
                GUILayout.Label($"  (вакансий больше 10, список скрыт)");
            }
        }

        // ============ КАМЕРА ============

        private void DrawCameraSection()
        {
            GUILayout.Label("--- Camera ---");

            if (GUILayout.Button("Teleport to Center (50,50)"))
            {
                if (_camera == null) _camera = FindObjectOfType<RTSCamera>();
                if (_camera != null) _camera.TeleportTo(new Vector3(50f, 0f, 50f));
            }
            if (GUILayout.Button("Teleport to Start Location"))
            {
                if (_camera == null) _camera = FindObjectOfType<RTSCamera>();
                var grid = GridSystem.Instance;
                if (_camera != null && grid != null)
                {
                    Vector3 start = grid.CellToWorld(grid.StartLocation.x, grid.StartLocation.y);
                    _camera.TeleportTo(start);
                }
            }
        }

        // ============ ВРЕМЯ ============

        private void DrawTimeSection()
        {
            GUILayout.Label("--- Time Speed ---");

            var tm = TimeManager.Instance;
            if (tm == null) return;

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("1x"))  tm.SetDebugSpeed(1f);
            if (GUILayout.Button("5x"))  tm.SetDebugSpeed(5f);
            if (GUILayout.Button("10x")) tm.SetDebugSpeed(10f);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Skip 1h"))
                tm.DebugAdvanceGameTime(TimeManager.SecondsPerDay / 24f);
            if (GUILayout.Button("Skip 1 day"))
                tm.DebugAdvanceGameTime(TimeManager.SecondsPerDay);
            if (GUILayout.Button("Skip 30 days"))
                tm.DebugAdvanceGameTime(TimeManager.SecondsPerDay * 30f);
            GUILayout.EndHorizontal();
        }

        // ============ ВРАГИ (заглушка) ============

        private void DrawEnemySection()
        {
            GUILayout.Label("--- Enemies (заглушка) ---");

            if (GUILayout.Button("Spawn Raid (Wave 1)"))
            {
                EventBus.Publish(new SpawnRaidRequestedEvent { WaveNumber = 1 });
                Debug.Log("[Debug] SpawnRaidRequestedEvent published (врагов пока нет).");
            }
        }

        // ============ СОСТОЯНИЕ ИГРЫ ============

        private void DrawGameStateSection()
        {
            GUILayout.Label("--- Game State ---");

            if (GameManager.Instance == null) return;

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Play"))  GameManager.Instance.StartGame();
            if (GUILayout.Button("Pause")) GameManager.Instance.PauseGame();
            if (GUILayout.Button("Menu"))  GameManager.Instance.QuitToMenu();
            GUILayout.EndHorizontal();
        }
    }
}