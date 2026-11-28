using UnityEngine;
using KingdomsClone.Core;
using KingdomsClone.Core.Events;

namespace KingdomsClone.Gameplay
{

    [DefaultExecutionOrder(-735)]
    public class HeatingSystem : MonoBehaviour
    {
        public static HeatingSystem Instance { get; private set; }

        [Header("Settings")]
        [Tooltip("Штраф счастья за каждый холодный дом. Накопительный.")]
        [SerializeField] private float happinessPenaltyPerColdHouse = 0.5f;

        [Tooltip("Максимальный штраф счастья от холода (защита от −1000).")]
        [SerializeField] private float maxHappinessPenalty = 5f;

        public int ColdHousesLastCheck { get; private set; }
        public int TotalHousesLastCheck { get; private set; }

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

        private void OnEnable() => EventBus.Subscribe<DayChangedEvent>(OnDayChanged);
        private void OnDisable() => EventBus.Unsubscribe<DayChangedEvent>(OnDayChanged);

        private void OnDayChanged(DayChangedEvent e)
        {
            var hm = HousingManager.Instance;
            var hs = HappinessSystem.Instance;

            if (hm == null) return;

            // Не зима — снимаем штраф, ничего не жжём
            if (e.Season != Season.Winter) {
                if (ColdHousesLastCheck != 0 || TotalHousesLastCheck != 0) {
                    ColdHousesLastCheck = 0;
                    TotalHousesLastCheck = 0;
                    hs?.RemoveModifier("heating");
                    Debug.Log("[Heating] Не зима — отопление не требуется.");
                }

                return;
            }

            // Зима
            int coldHouses = 0;
            int totalHouses = 0;

            foreach (var house in hm.AllHouses)
            {
                if (house == null) continue;
                if (house.State != BuildingState.Active) continue;
                if (house.Data == null || !house.Data.IsHousing) continue;
                if (house.Residents <= 0) continue;

                totalHouses++;

                if (!house.TryConsumeWinterFuel()) {
                    coldHouses++;
                }
            }

            ColdHousesLastCheck = coldHouses;
            TotalHousesLastCheck = totalHouses;

            if (coldHouses > 0) {
                float penalty = Mathf.Min(maxHappinessPenalty, coldHouses * happinessPenaltyPerColdHouse);
                hs?.SetModifier("heating", -penalty);

                Debug.LogWarning($"[Heating] День {e.NewDay}: {coldHouses}/{totalHouses} домов без топлива. " + "Счастье −{penalty:F1}");
            } else {
                hs?.RemoveModifier("heating");
                
                if (totalHouses > 0) {
                    Debug.Log($"[Heating] День {e.NewDay}: все {totalHouses} домов протоплены.");
                }
            }
        }
    }
}