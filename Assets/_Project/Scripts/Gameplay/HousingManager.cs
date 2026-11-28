using System.Collections.Generic;
using UnityEngine;

namespace KingdomsClone.Gameplay
{
    [DefaultExecutionOrder(-745)]
    public class HousingManager : MonoBehaviour
    {
        public static HousingManager Instance { get; private set; }

        private readonly List<BuildingBase> _houses = new();

        public IReadOnlyList<BuildingBase> AllHouses => _houses;

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

        public void Register(BuildingBase house)
        {
            if (house != null && !_houses.Contains(house)) {
                _houses.Add(house);
            }
        }

        public void Unregister(BuildingBase house)
        {
            if (house != null) {
                _houses.Remove(house);
            }
        }

        public BuildingBase GetFreeHouse(BuildingBase exclude = null)
        {
            foreach (var h in _houses) {
                if (h == null || h == exclude) continue;
                if (h.HasFreeSpace) return h;
            }

            return null;
        }

        public int GetTotalCapacity()
        {
            int total = 0;

            foreach (var h in _houses) {
                if (h != null && h.Data != null && h.State == BuildingState.Active) {
                    total += h.Data.MaxResidents;
                }
            }

            return total;
        }

        public int GetTotalResidents()
        {
            int total = 0;

            foreach (var h in _houses) {
                if (h != null && h.State == BuildingState.Active) {
                    total += h.Residents;
                }
            }

            return total;
        }

        public int GetFreeSlots() => GetTotalCapacity() - GetTotalResidents();
    }
}