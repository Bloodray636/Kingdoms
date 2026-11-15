using System.Collections.Generic;
using UnityEngine;
using KingdomsClone.Data;
using KingdomsClone.Grid;

namespace KingdomsClone.Gameplay
{
    [DefaultExecutionOrder(-740)]
    public class VillagerManager : MonoBehaviour
    {
        public static VillagerManager Instance { get; private set; }

        [SerializeField] private Transform villagerParent;
        private readonly List<Villager> _villagers = new();

        // Глобальные брони рабочих позиций — чтобы два здания рядом не выбрали одну клетку
        private static readonly HashSet<Vector2Int> _reservedWorkPositions = new();

        public IReadOnlyList<Villager> AllVillagers => _villagers;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy() { if (Instance == this) Instance = null; }

        // ============ БРОНИРОВАНИЕ РАБОЧИХ МЕСТ ============

        public static bool TryReserveWorkPosition(Vector2Int cell)
        {
            return _reservedWorkPositions.Add(cell);
        }

        public static void ReleaseWorkPosition(Vector2Int cell)
        {
            _reservedWorkPositions.Remove(cell);
        }

        public static bool IsWorkPositionReserved(Vector2Int cell)
        {
            return _reservedWorkPositions.Contains(cell);
        }

        public static void ClearAllReservations()
        {
            _reservedWorkPositions.Clear();
        }

        // ============ СПАВН ============

        public Villager SpawnVillager(Vector2Int cell, UnitDataSO data)
        {
            return SpawnVillager(cell, data, null);
        }

        public Villager SpawnVillager(Vector2Int cell, UnitDataSO data, BuildingBase home)
        {
            var grid = GridSystem.Instance;
            if (grid == null) return null;

            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = "Villager";
            go.transform.SetParent(villagerParent != null ? villagerParent : transform);
            go.transform.position = grid.CellToWorld(cell.x, cell.y) + Vector3.up * 0.5f;

            var mr = go.GetComponent<MeshRenderer>();
            if (mr != null) mr.material.color = new Color(0.9f, 0.7f, 0.5f);

            var v = go.AddComponent<Villager>();
            v.Initialize(data);
            if (home != null) v.SetHome(home);

            _villagers.Add(v);

            if (v.CurrentJob == null && JobManager.Instance != null)
                JobManager.Instance.RegisterUnemployed(v);

            Debug.Log($"[VillagerManager] Заспавнен {go.name} в {cell}. Всего: {_villagers.Count}");
            return v;
        }

        public void ClearAll()
        {
            foreach (var v in _villagers) if (v != null) Destroy(v.gameObject);
            _villagers.Clear();
            ClearAllReservations();
        }
    }
}