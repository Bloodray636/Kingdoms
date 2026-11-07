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
        [SerializeField, Min(0f)] private float buildTimeSeconds = 5f;

        [Header("Cost")]
        [Tooltip("Стоимость постройки. Возврат при сносе — 50% (задача 2.5).")]
        [SerializeField] private List<ResourceAmount> buildCost = new();

        [Header("Production (optional)")]
        [SerializeField] private ProductionRecipe production;

        [Header("Happiness (optional)")]
        [Tooltip("Бонус к счастью в радиусе (задача 6.2).")]
        [SerializeField, Range(-10, 10)] private int happinessBonus = 0;
        [SerializeField, Min(0f)] private float happinessRadius = 0f;

        // Публичные геттеры — данные read-only снаружи
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

        // Производные флаги — удобно в игровой логике
        public bool IsProducer => production != null && production.HasOutputs;
        public bool IsStorage => storageCapacity > 0;
        public bool IsHousing => category == BuildingCategory.Housing;
        public bool IsDefense => category == BuildingCategory.Defense;
    }
}