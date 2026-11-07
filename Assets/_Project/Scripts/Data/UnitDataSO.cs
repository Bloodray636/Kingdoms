using UnityEngine;

namespace KingdomsClone.Data
{
    [CreateAssetMenu(
        menuName = "KingdomsClone/Unit",
        fileName = "Unit_New",
        order = 3)]
    public class UnitDataSO : DataSO
    {
        [Header("Visual")]
        [SerializeField] private GameObject prefab;

        [Header("Stats")]
        [SerializeField, Min(1)] private int maxHealth = 100;
        [SerializeField, Min(0f)] private float moveSpeed = 3f;
        [SerializeField, Min(0f)] private float visionRange = 5f;

        [Header("Combat")]
        [SerializeField, Min(0)] private int attackDamage = 0;
        [SerializeField, Min(0f)] private float attackRange = 1f;
        [SerializeField, Min(0.1f)] private float attackCooldown = 1f;

        [Header("Flags")]
        [Tooltip("Летает над стенами (дракон).")]
        [SerializeField] private bool isFlying = false;
        [Tooltip("Может работать (житель).")]
        [SerializeField] private bool isWorker = false;

        public GameObject Prefab => prefab;
        public int MaxHealth => maxHealth;
        public float MoveSpeed => moveSpeed;
        public float VisionRange => visionRange;
        public int AttackDamage => attackDamage;
        public float AttackRange => attackRange;
        public float AttackCooldown => attackCooldown;
        public bool IsFlying => isFlying;
        public bool IsWorker => isWorker;

        public bool IsCombatant => attackDamage > 0;
    }
}