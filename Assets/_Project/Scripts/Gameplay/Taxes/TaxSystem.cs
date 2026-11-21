using UnityEngine;
using KingdomsClone.Core;
using KingdomsClone.Core.Events;
using KingdomsClone.Data;

namespace KingdomsClone.Gameplay
{
    [DefaultExecutionOrder(-725)]
    public class TaxSystem : MonoBehaviour
    {
        public static TaxSystem Instance { get; private set; }

        [Header("Settings")]
        [SerializeField] private TaxLevel currentLevel = TaxLevel.Low;

        [Header("Treasury")]
        [Tooltip("Эффективность без казначея (0..1). По умолчанию 50%.")]
        [SerializeField, Range(0f, 1f)] private float efficiencyWithoutTreasurer = 0.5f;

        public TaxLevel CurrentLevel => currentLevel;

        private BuildingBase _cachedTreasury;
        private float _lastEfficiencyCheck;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            ApplyHappinessModifier();
        }

        private void OnEnable() => EventBus.Subscribe<DayChangedEvent>(OnDayChanged);
        private void OnDisable() => EventBus.Unsubscribe<DayChangedEvent>(OnDayChanged);
        private void OnDestroy() { if (Instance == this) Instance = null; }

        public void SetTaxLevel(TaxLevel newLevel)
        {
            if (currentLevel == newLevel) return;
            var old = currentLevel;
            currentLevel = newLevel;
            ApplyHappinessModifier();
            EventBus.Publish(new TaxChangedEvent { OldLevel = old, NewLevel = newLevel });
            Debug.Log($"[Tax] Уровень: {old} → {newLevel}");
        }

        private void ApplyHappinessModifier()
        {
            if (HappinessSystem.Instance == null) return;
            HappinessSystem.Instance.SetModifier("tax", GetHappinessModifier(currentLevel));
        }

        private static float GetHappinessModifier(TaxLevel lvl) => lvl switch
        {
            TaxLevel.None => +2f,
            TaxLevel.Low => 0f,
            TaxLevel.Medium => -2f,
            TaxLevel.High => -5f,
            _ => 0f
        };

        private static int GetGoldPerVillager(TaxLevel lvl) => lvl switch
        {
            TaxLevel.None => 0,
            TaxLevel.Low => 1,
            TaxLevel.Medium => 3,
            TaxLevel.High => 6,
            _ => 0
        };

        // ═══════ СОКРОВИЩНИЦА ═══════

        /// <summary>
        /// Найти активную сокровищницу (любое здание с JobType=Treasurer).
        /// Кешируется раз в 2 секунды.
        /// </summary>
        public BuildingBase FindTreasury()
        {
            if (_cachedTreasury != null &&
                _cachedTreasury.State == BuildingState.Active &&
                _cachedTreasury.Data != null &&
                _cachedTreasury.Data.JobType == JobType.Treasurer)
            {
                return _cachedTreasury;
            }

            _cachedTreasury = null;
            var bm = BuildingManager.Instance;
            if (bm == null) return null;

            foreach (var b in bm.AllBuildings)
            {
                if (b == null || b.Data == null) continue;
                if (b.Data.JobType != JobType.Treasurer) continue;
                if (b.State != BuildingState.Active) continue;
                _cachedTreasury = b;
                return b;
            }
            return null;
        }

        /// <summary>
        /// Есть ли в сокровищнице назначенный казначей.
        /// </summary>
        public bool HasTreasurer(BuildingBase treasury)
        {
            if (treasury == null) return false;
            var jobs = treasury.GetJobs();
            if (jobs == null) return false;

            foreach (var j in jobs)
                if (j != null && j.AssignedVillager != null) return true;

            return false;
        }

        /// <summary>
        /// Эффективность сбора налогов:
        /// - 0.0 — сокровищницы нет (налоги не собираются)
        /// - 0.5 (по умолчанию) — сокровищница без казначея
        /// - 1.0 — есть казначей
        /// </summary>
        public float GetTaxEfficiency()
        {
            var treasury = FindTreasury();
            if (treasury == null) return 0f;
            return HasTreasurer(treasury) ? 1f : efficiencyWithoutTreasurer;
        }

        public string GetEfficiencyDescription()
        {
            var treasury = FindTreasury();
            if (treasury == null) return "❌ нет сокровищницы (0%)";
            if (HasTreasurer(treasury))
                return $"✅ есть казначей (100%) в {treasury.GridPosition}";
            return $"⚠ без казначея ({efficiencyWithoutTreasurer:P0}) в {treasury.GridPosition}";
        }

        // ═══════ СБОР НАЛОГОВ ═══════

        private void OnDayChanged(DayChangedEvent e)
        {
            if (currentLevel == TaxLevel.None) return;

            var vm = VillagerManager.Instance;
            var rm = ResourceManager.Instance;
            var gold = GameDataSO.Instance?.GetResource("Resource_Gold");

            if (vm == null || rm == null || gold == null)
            {
                Debug.LogWarning("[Tax] Невозможно собрать налоги: нет менеджеров или Resource_Gold.");
                return;
            }

            float efficiency = GetTaxEfficiency();
            if (efficiency <= 0f)
            {
                Debug.LogWarning("[Tax] Сокровищница отсутствует — налоги не собираются!");
                return;
            }

            int population = vm.AllVillagers.Count;
            if (population == 0) return;

            int goldPerVillager = GetGoldPerVillager(currentLevel);
            int baseGold = population * goldPerVillager;
            int totalGold = Mathf.FloorToInt(baseGold * efficiency);

            if (totalGold <= 0) return;

            rm.AddResource(gold, totalGold);
            string eff = HasTreasurer(FindTreasury()) ? "казначей" : $"без казначея {efficiency:P0}";
            Debug.Log($"[Tax] День {e.NewDay}: +{totalGold} золота (база {baseGold}, {eff}, население {population})");
        }
    }
}