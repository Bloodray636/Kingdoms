using System.Collections.Generic;
using UnityEngine;
using KingdomsClone.Grid;
using KingdomsClone.Core;
using KingdomsClone.Core.Events;

namespace KingdomsClone.Gameplay
{
    [DefaultExecutionOrder(-750)]
    public class BuildingManager : MonoBehaviour
    {
        public static BuildingManager Instance { get; private set; }

        private readonly List<BuildingBase> _buildings = new();
        public IReadOnlyList<BuildingBase> AllBuildings => _buildings;

        private void Awake()
        {
            if (Instance != null && Instance != this) { 
                Destroy(gameObject); 
                return; 
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (Instance == this) {
                Instance = null;
            }
        }

        public void Register(BuildingBase b)
        {
            if (b != null && !_buildings.Contains(b)) {
                _buildings.Add(b);
            }
        }

        public void Unregister(BuildingBase b)
        {
            if (b != null) {
                _buildings.Remove(b);
            }
        }

        public void ClearAllSilent()
        {
            var copy = new List<BuildingBase>(_buildings);

            foreach (var b in copy) {
                if (b == null) continue;

                if (GridSystem.Instance != null) {
                    foreach (var cell in b.GetOccupiedCells()) {
                        GridSystem.Instance.UnregisterBuilding(cell.x, cell.y, b.gameObject);
                    }
                }

                Destroy(b.gameObject);
            }

            _buildings.Clear();
            EventBus.Publish(new PathCacheClearEvent());

            Debug.Log($"[BuildingManager] Уничтожено {copy.Count} зданий.");
        }
    }
}