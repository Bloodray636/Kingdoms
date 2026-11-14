using System.Collections.Generic;
using UnityEngine;

namespace KingdomsClone.Data
{
    [CreateAssetMenu(
        menuName = "KingdomsClone/Building",
        fileName = "Building_New",
        order = 2)]
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
        [Tooltip("Сколько секунд здание строится до готовности. 0 = мгновенно.")]
        [SerializeField, Min(0f)] private float buildTimeSeconds = 0f;

        [Header("Cost")]
        [Tooltip("Стоимость постройки. Возврат при сносе — 50% (задача 2.5).")]
        [SerializeField] private List<ResourceAmount> buildCost = new();

        [Header("Production (optional)")]
        [SerializeField] private ProductionRecipe production;

        [Header("Happiness (optional)")]
        [Tooltip("Бонус к счастью в радиусе (задача 6.2).")]
        [SerializeField, Range(-10, 10)] private int happinessBonus = 0;
        [SerializeField, Min(0f)] private float happinessRadius = 0f;

        // ---------- Публичные геттеры (read-only снаружи) ----------
        public GameObject Prefab => prefab;
        public Vector2Int Size => size;
        public BuildingCategory Category => category;
        public int MaxHealth => maxHealth;
        public int WorkerSlots => workerSlots;
        public int StorageCapacity => storageCapacity;
        public float BuildTimeSeconds => buildTimeSeconds;
        public IReadOnlyList<ResourceAmount> BuildCost => buildCost;
        public ProductionRecipe Production => production;
        public int HappinessBonus => happinessBonus;
        public float HappinessRadius => happinessRadius;

        // ---------- Производные флаги ----------
        public bool IsProducer => production != null && production.HasOutputs;
        public bool IsStorage => storageCapacity > 0;
        public bool IsHousing => category == BuildingCategory.Housing;
        public bool IsDefense => category == BuildingCategory.Defense;

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate(); // родительский DataSO заполняет id из имени ассета

            // ФИКС: защита от некорректного размера (0 или отрицательный)
            if (size.x < 1) size = new Vector2Int(1, size.y);
            if (size.y < 1) size = new Vector2Int(size.x, 1);

            // ФИКС: защита от невозможных значений
            if (maxHealth < 1) maxHealth = 1;
            if (workerSlots < 0) workerSlots = 0;
            if (storageCapacity < 0) storageCapacity = 0;
            if (buildTimeSeconds < 0f) buildTimeSeconds = 0f;
            if (happinessRadius < 0f) happinessRadius = 0f;

            // Валидация стоимости: предупреждаем в консоли о некорректных записях
            if (buildCost != null)
            {
                foreach (var ra in buildCost)
                {
                    if (ra == null) continue;
                    if (ra.Resource == null)
                        Debug.LogWarning($"[BuildingDataSO] '{name}': в BuildCost есть запись с пустым Resource.", this);
                    if (ra.Amount < 0)
                        Debug.LogWarning($"[BuildingDataSO] '{name}': в BuildCost отрицательное количество ({ra.Amount}).", this);
                }
            }
        }
#endif
    }
}