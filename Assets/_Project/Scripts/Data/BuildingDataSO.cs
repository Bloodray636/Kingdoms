using System.Collections.Generic;
using UnityEngine;
using KingdomsClone.Gameplay;

namespace KingdomsClone.Data
{
    [CreateAssetMenu(
        menuName = "KingdomsClone/Building",
        fileName = "Building_New",
        order = 2)
    ]

    public class BuildingDataSO : DataSO
    {
        [Header("Visual")]
        [SerializeField] private GameObject prefab;

        [Tooltip("Размер в клетках сетки (ширина × высота).")]
        [SerializeField] private Vector2Int size = Vector2Int.one;

        [Header("Gameplay")]
        [SerializeField] private BuildingCategory category = BuildingCategory.Production;
        [SerializeField, Min(1)] private int maxHealth = 100;
        [SerializeField, Min(0)] private int workerSlots = 0;
        [SerializeField, Min(0)] private int storageCapacity = 0;
        [SerializeField, Min(0f)] private float buildTimeSeconds = 5f;

        [Tooltip("Тип работы, которую даёт здание. None — если не рабочее место.")]
        [SerializeField] private JobType jobType = JobType.None;

        [Tooltip("Приоритет вакансии. Чем выше — тем раньше назначаются жители.")]
        [SerializeField] private JobPriority jobPriority = JobPriority.Normal;

        [Header("Production")]
        [Tooltip("Рецепт производства (SO). Если задан — используется вместо legacy ProductionRecipe.")]
        [SerializeField] private RecipeDataSO recipe;

        [Tooltip("Сколько жителей может жить в здании. Только для категории Housing.")]
        [SerializeField, Min(0)] private int maxResidents = 0;

        [Header("Cost")]
        [Tooltip("Стоимость постройки. Возврат при сносе — 50% (задача 2.5).")]
        [SerializeField] private List<ResourceAmount> buildCost = new();

        [Header("Production (optional)")]
        [SerializeField] private ProductionRecipe production;

        [Header("Happiness (optional)")]
        [Tooltip("Бонус к счастью в радиусе (задача 6.2).")]
        [SerializeField, Range(-10, 10)] private int happinessBonus = 0;
        [SerializeField, Min(0f)] private float happinessRadius = 0f;

        [Header("Season Modifiers")]
        [Tooltip("Множитель производства зимой. 0 = зимой не работает (ферма). " + "1 = работает всегда (лесоруб, рудник, кузница). " + "0.5 = зимний рыбак (половина улова). Значения из G.5.1.")]
        [SerializeField, Range(0f, 1f)] private float winterProductionMultiplier = 1f;

        [Header("Winter Heating")]
        [Tooltip("Что жгут зимой. null = дом не отапливается (не теряет счастье, но и не греет). " + "Сейчас: Resource_Wood. Потом: Resource_Coal (когда появится).")]
        [SerializeField] private ResourceDataSO winterFuelResource;

        [Tooltip("Сколько топлива на 1 жителя в день зимой. 0 = не тратит. " + "Пример: дом на 4 жителя, значение 0.5 → 2 дрова/день. Значения из G.5.2.")]
        [SerializeField, Min(0f)] private float winterFuelPerResidentPerDay = 0f;

        public ResourceDataSO WinterFuelResource => winterFuelResource;
        public float WinterFuelPerResidentPerDay => winterFuelPerResidentPerDay;

        public bool RequiresWinterHeating => winterFuelResource != null && winterFuelPerResidentPerDay > 0.001f;

        public float WinterProductionMultiplier => winterProductionMultiplier;

        // ГЕТТЕРЫ

        public GameObject Prefab => prefab;
        public Vector2Int Size => size;
        public BuildingCategory Category => category;
        public int MaxHealth => maxHealth;
        public int WorkerSlots => workerSlots;
        public JobType JobType => jobType;
        public JobPriority JobPriority => jobPriority;
        public int StorageCapacity => storageCapacity;
        public float BuildTimeSeconds => buildTimeSeconds;
        public int MaxResidents => maxResidents;
        public IReadOnlyList<ResourceAmount> BuildCost => buildCost;
        public ProductionRecipe Production => production;
        public RecipeDataSO Recipe => recipe;
        public int HappinessBonus => happinessBonus;
        public float HappinessRadius => happinessRadius;

        // ПРОИЗВОДНЫЕ ФЛАГИ

        public bool IsProducer => (recipe != null && recipe.HasOutputs) || (production != null && production.HasOutputs);
        public bool IsStorage => storageCapacity > 0;
        public bool IsHousing => category == BuildingCategory.Housing;
        public bool IsDefense => category == BuildingCategory.Defense;

        // ВАЛИДАЦИЯ

        #if UNITY_EDITOR
            private void OnValidate()
            {
                if (size.x < 1) {
                    size = new Vector2Int(1, size.y);
                }

                if (size.y < 1) {
                    size = new Vector2Int(size.x, 1);
                }

                if (maxHealth < 1) {
                    maxHealth = 1;
                }

                if (workerSlots < 0) {
                    workerSlots = 0;
                }

                if (storageCapacity < 0) {
                    storageCapacity = 0;
                }

                if (buildTimeSeconds < 0f) {
                    buildTimeSeconds = 0f;
                }

                if (maxResidents < 0) {
                    maxResidents = 0;
                }


                if (category == BuildingCategory.Housing && maxResidents < 1) {
                    maxResidents = 1;
                }

                if (buildCost != null) {
                    for (int i = buildCost.Count - 1; i >= 0; i--) {
                        if (buildCost[i] == null) {
                            Debug.LogWarning($"[{name}] BuildCost[{i}]: null запись. Удалена.");
                            buildCost.RemoveAt(i);
                        }
                    }
                }

                winterFuelPerResidentPerDay = Mathf.Max(0f, winterFuelPerResidentPerDay); 

                winterProductionMultiplier = Mathf.Clamp01(winterProductionMultiplier);
            }
        #endif
    }
}