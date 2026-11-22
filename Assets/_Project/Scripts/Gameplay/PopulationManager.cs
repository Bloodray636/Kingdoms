using UnityEngine;
using KingdomsClone.Core;
using KingdomsClone.Core.Events;
using KingdomsClone.Data;

namespace KingdomsClone.Gameplay
{
    [DefaultExecutionOrder(-730)]
    public class PopulationManager : MonoBehaviour
    {
        public static PopulationManager Instance { get; private set; }

        [Header("Settings")]
        [Tooltip("Минимальное счастье для прихода новых жителей (0..10).")]
        [SerializeField] private float happinessThreshold = 5f;

        [Tooltip("Раз в сколько игровых дней пробуем заспавнить жителя.")]
        [SerializeField, Min(1)] private int spawnIntervalDays = 1;

        [Tooltip("Сколько жителей максимум может появиться за один день. 0 = пассивный рост отключён (только Debug).")]
        [SerializeField, Min(0)] private int maxSpawnsPerDay = 1;

        private int _lastSpawnDay = -999;
        private int _spawnsToday = 0;
        private int _spawnCountDay = -999;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy() { if (Instance == this) Instance = null; }

        private void OnEnable() => EventBus.Subscribe<DayChangedEvent>(OnDayChanged);
        private void OnDisable() => EventBus.Unsubscribe<DayChangedEvent>(OnDayChanged);

        private void OnDayChanged(DayChangedEvent e)
        {
            // Сброс дневного счётчика (на случай, если событие пришло за новый день)
            if (e.NewDay != _spawnCountDay)
            {
                _spawnCountDay = e.NewDay;
                _spawnsToday = 0;
            }

            // Проверка интервала между попытками (spawnIntervalDays)
            if (e.NewDay - _lastSpawnDay < spawnIntervalDays) return;

            // Заполняем дневной лимит (может быть > 1)
            while (_spawnsToday < maxSpawnsPerDay)
            {
                if (!TrySpawn()) break;
            }
        }

        /// <summary>
        /// Попробовать заспавнить жителя по правилам:
        /// счастье + свободное жильё + дневной лимит.
        /// Возвращает true, если спавн удался.
        /// </summary>
        /// <param name="ignoreHappiness">Пропустить проверку счастья (для дебага).</param>
        /// <param name="ignoreDailyLimit">Пропустить дневной лимит (для дебага/стресс-тестов).</param>
        public bool TrySpawn(bool ignoreHappiness = false, bool ignoreDailyLimit = false)
        {
            var vm = VillagerManager.Instance;
            var hm = HousingManager.Instance;
            var hs = HappinessSystem.Instance;

            if (vm == null || hm == null)
            {
                Debug.LogWarning("[Population] VillagerManager/HousingManager не в сцене!");
                return false;
            }

            // Синхронизация дня (на случай, если DayChangedEvent ещё не пришёл)
            int currentDay = TimeManager.Instance != null ? TimeManager.Instance.CurrentDay : 0;
            if (currentDay != _spawnCountDay)
            {
                _spawnCountDay = currentDay;
                _spawnsToday = 0;
            }

            // ФИКС: дневной лимит — теперь реально проверяется
            if (!ignoreDailyLimit && _spawnsToday >= maxSpawnsPerDay)
            {
                Debug.Log($"[Population] Спавн отменён: дневной лимит {_spawnsToday}/{maxSpawnsPerDay}");
                return false;
            }

            float happiness = hs != null ? hs.CurrentHappiness : 5f;
            if (!ignoreHappiness && happiness < happinessThreshold)
            {
                Debug.Log($"[Population] Спавн отменён: счастье {happiness:F1} < {happinessThreshold:F1}");
                return false;
            }

            var house = hm.GetFreeHouse();
            if (house == null)
            {
                Debug.Log("[Population] Спавн отменён: нет свободного жилья.");
                return false;
            }

            var data = GameDataSO.Instance?.GetUnit("Unit_Villager");
            if (data == null)
            {
                Debug.LogError("[Population] Unit_Villager не найден в GameDataSO!");
                return false;
            }

            var villager = vm.SpawnVillager(house.GridPosition, data, house);
            if (villager == null) return false;

            house.TryAddResident(villager.gameObject);
            _lastSpawnDay = currentDay;
            _spawnsToday++;

            Debug.Log($"[Population] Новый житель! Всего: {vm.AllVillagers.Count} / {hm.GetTotalCapacity()}, " +
                      $"сегодня {_spawnsToday}/{maxSpawnsPerDay}");
            return true;
        }
    }
}