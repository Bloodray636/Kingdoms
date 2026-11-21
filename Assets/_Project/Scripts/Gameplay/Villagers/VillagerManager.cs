using System.Collections.Generic;
using UnityEngine;
using KingdomsClone.Data;
using KingdomsClone.Grid;
using KingdomsClone.Core;
using KingdomsClone.Core.Events;
using KingdomsClone.Core.Pooling;

namespace KingdomsClone.Gameplay
{
    [DefaultExecutionOrder(-740)]
    public class VillagerManager : MonoBehaviour
    {
        public static VillagerManager Instance { get; private set; }

        [Header("Prefab")]
        [Tooltip("Опционально. Если не задан — создаётся программно.")]
        [SerializeField] private GameObject villagerPrefab;
        [SerializeField] private GameObject deathEffectPrefab; // опционально

        [Header("Pool")]
        [SerializeField] private int prewarmCount = 10;
        [SerializeField] private int maxPoolSize = 300;

        [Header("Parent")]
        [SerializeField] private Transform villagerParent;

        private GameObject _runtimePrefab;
        private readonly List<Villager> _villagers = new();
        private static readonly HashSet<Vector2Int> _reservedWorkPositions = new();

        public IReadOnlyList<Villager> AllVillagers => _villagers;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            if (villagerPrefab == null)
                _runtimePrefab = CreateVillagerTemplate();
            else
                _runtimePrefab = villagerPrefab;

            if (PoolManager.Instance != null)
                PoolManager.Instance.Prewarm(_runtimePrefab, prewarmCount, maxPoolSize);

            if (deathEffectPrefab != null && PoolManager.Instance != null)
                PoolManager.Instance.Prewarm(deathEffectPrefab, 5, 50);
        }

        private void OnEnable() => EventBus.Subscribe<DayChangedEvent>(OnDayChanged);
        private void OnDisable() => EventBus.Unsubscribe<DayChangedEvent>(OnDayChanged);
        private void OnDestroy() { if (Instance == this) Instance = null; }

        private void OnDayChanged(DayChangedEvent e)
        {
            var snapshot = new List<Villager>(_villagers);
            foreach (var v in snapshot)
                if (v != null) v.OnNewDay(e.NewDay);
        }

        private GameObject CreateVillagerTemplate()
        {
            var root = new GameObject("VillagerTemplate");
            root.SetActive(false);
            root.transform.SetParent(transform, false);

            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            body.transform.SetParent(root.transform, false);
            body.transform.localPosition = Vector3.zero;
            var col = body.GetComponent<Collider>();
    
            var mr = body.GetComponent<MeshRenderer>();
            if (mr != null) mr.material.color = new Color(0.9f, 0.7f, 0.5f);

            var carry = GameObject.CreatePrimitive(PrimitiveType.Cube);
            carry.name = "CarryIndicator";
            carry.transform.SetParent(root.transform, false);
            carry.transform.localPosition = new Vector3(0, 1.5f, 0);
            carry.transform.localScale = Vector3.one * 0.35f;
            Destroy(carry.GetComponent<Collider>());
            carry.SetActive(false);

            root.AddComponent<Villager>();
            return root;
        }

        // ============ БРОНИРОВАНИЕ ============
        public static bool TryReserveWorkPosition(Vector2Int cell) => _reservedWorkPositions.Add(cell);
        public static void ReleaseWorkPosition(Vector2Int cell) => _reservedWorkPositions.Remove(cell);
        public static bool IsWorkPositionReserved(Vector2Int cell) => _reservedWorkPositions.Contains(cell);
        public static void ClearAllReservations() => _reservedWorkPositions.Clear();

        // ============ СПАВН ============
        public Villager SpawnVillager(Vector2Int cell, UnitDataSO data) => SpawnVillager(cell, data, null);

        public Villager SpawnVillager(Vector2Int cell, UnitDataSO data, BuildingBase home)
        {
            var grid = GridSystem.Instance;
            if (grid == null) return null;
            if (PoolManager.Instance == null) { Debug.LogError("[VillagerManager] PoolManager не в сцене!"); return null; }

            Vector3 pos = grid.CellToWorld(cell.x, cell.y) + Vector3.up * 0.5f;
            var go = PoolManager.Instance.Spawn(_runtimePrefab, pos, Quaternion.identity);
            if (go == null) return null;

            var v = go.GetComponent<Villager>();
            if (v == null) { Debug.LogError("[VillagerManager] У префаба нет Villager!"); return null; }

            v.Initialize(data);
            if (home != null) v.SetHome(home);
            if (villagerParent != null) go.transform.SetParent(villagerParent, true);

            _villagers.Add(v);

            if (v.CurrentJob == null && JobManager.Instance != null)
                JobManager.Instance.RegisterUnemployed(v);

            return v;
        }

        public void Register(Villager v) { if (v != null && !_villagers.Contains(v)) _villagers.Add(v); }
        public void Unregister(Villager v) { if (v != null) _villagers.Remove(v); }

        public void DespawnVillager(Villager v, bool playEffect = false)
        {
            if (v == null) return;
            _villagers.Remove(v);

            if (playEffect && deathEffectPrefab != null && PoolManager.Instance != null)
                PoolManager.Instance.Spawn(deathEffectPrefab, v.transform.position, Quaternion.identity);

            if (PoolManager.Instance != null)
                PoolManager.Instance.Despawn(v.gameObject);
            else
                Destroy(v.gameObject);
        }

        public void ClearAllSilent()
        {
            int count = _villagers.Count;
            var copy = new List<Villager>(_villagers);
            foreach (var v in copy) DespawnVillager(v, playEffect: false);
            _villagers.Clear();
            ClearAllReservations();

            if (JobManager.Instance != null)
                JobManager.Instance.ClearAllUnemployed();

            Debug.Log($"[VillagerManager] Уничтожено {count} жителей.");
        }

        public void ClearAll() => ClearAllSilent();
    }
}