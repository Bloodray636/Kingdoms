using UnityEngine;
using KingdomsClone.Core;
using KingdomsClone.Core.Events;
using KingdomsClone.Data;
using KingdomsClone.Gameplay;

namespace KingdomsClone.Gameplay
{
    [DefaultExecutionOrder(-735)]
    public class MortalitySystem : MonoBehaviour
    {
        public static MortalitySystem Instance { get; private set; }

        [Header("Death Penalty")]
        [Tooltip("Штраф счастья за каждую смерть (накапливается).")]
        [SerializeField] private float happinessPenaltyPerDeath = 0.5f;

        [Tooltip("Максимальный штраф счастья от смертей (защита от −∞).")]
        [SerializeField] private float maxHappinessPenalty = 4f;

        [Tooltip("Сколько штрафа рассасывается за игровой день.")]
        [SerializeField] private float penaltyDecayPerDay = 0.25f;

        [Header("Starvation Modifier")]
        [Tooltip("Штраф счастья, пока еда в пуле = 0 при живых жителях.")]
        [SerializeField] private float starvationPenalty = 1f;

        public int TotalDeaths { get; private set; }
        public int DeathsToday { get; private set; }
        public int DeathsLastDay { get; private set; }
        public float CurrentDeathPenalty => _currentDeathPenalty;
        public bool IsStarving { get; private set; }

        private float _currentDeathPenalty;

        private void Awake()
        {
            if (Instance != null && Instance != this) { 
                Destroy(gameObject); 
                return; 
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy() { 
            if (Instance == this) {
                Instance = null; 
            }
        }

        private void OnEnable()
        {
            EventBus.Subscribe<VillagerDiedEvent>(OnVillagerDied);
            EventBus.Subscribe<DayChangedEvent>(OnDayChanged);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<VillagerDiedEvent>(OnVillagerDied);
            EventBus.Unsubscribe<DayChangedEvent>(OnDayChanged);
        }

        // Смерть

        private void OnVillagerDied(VillagerDiedEvent e)
        {
            TotalDeaths++;
            DeathsToday++;

            _currentDeathPenalty = Mathf.Min( maxHappinessPenalty, _currentDeathPenalty + happinessPenaltyPerDeath);

            ApplyDeathModifier();

            string causeStr = e.Cause switch {
                VillagerDeathCause.Hunger => "от голода",
                VillagerDeathCause.Swamp => "утонул в болоте",
                VillagerDeathCause.Cold => "замёрз",
                VillagerDeathCause.Combat => "погиб в бою",
                VillagerDeathCause.OldAge => "умер от старости",
                _ => "погиб"
            };

            Debug.LogWarning($"[Mortality] {e.VillagerName} {causeStr} в {e.Position}. " + $"Всего: {TotalDeaths}. Штраф: −{_currentDeathPenalty:F1}");
        }

        // День

        private void OnDayChanged(DayChangedEvent e)
        {
            DeathsLastDay = DeathsToday;
            DeathsToday = 0;

            // Рассасываем штраф за смерти
            if (_currentDeathPenalty > 0.001f) {
                _currentDeathPenalty = Mathf.Max(0f, _currentDeathPenalty - penaltyDecayPerDay);
                ApplyDeathModifier();
            }

            // Проверяем голод
            CheckStarvation();
        }

        private void CheckStarvation()
        {
            var food = GameDataSO.Instance?.GetResource("Resource_Food");
            var rm = ResourceManager.Instance;
            var vm = VillagerManager.Instance;
            var hs = HappinessSystem.Instance;

            if (food == null || rm == null || vm == null || hs == null) return;

            int foodAmount = rm.GetAmount(food);
            int population = vm.AllVillagers.Count;

            bool starving = foodAmount == 0 && population > 0;
            IsStarving = starving;

            if (starving) {
                hs.SetModifier("starvation", -starvationPenalty);
                Debug.LogWarning($"[Mortality] Еда = 0 при {population} жителях! Счастье −{starvationPenalty:F1} (голод)");
            } else {
                hs.RemoveModifier("starvation");
            }
        }

        private void ApplyDeathModifier()
        {
            var hs = HappinessSystem.Instance;

            if (hs == null) return;

            if (_currentDeathPenalty > 0.001f) {
                hs.SetModifier("mortality", -_currentDeathPenalty);
            } else {
                hs.RemoveModifier("mortality");
            }
        }

        // Отладка

        public void DebugResetPenalty()
        {
            _currentDeathPenalty = 0f;
            DeathsToday = 0;
            TotalDeaths = 0;
            ApplyDeathModifier();
            Debug.Log("[Mortality] Штраф и счётчики сброшены (Debug).");
        }
    }
}