using System.Collections.Generic;
using UnityEngine;
using KingdomsClone.Grid;

namespace KingdomsClone.Gameplay
{
    /// <summary>
    /// Реестр всех зданий в сцене. Нужен, чтобы массово удалять здания
    /// при перегенерации карты или загрузке сейва.
    /// </summary>
    [DefaultExecutionOrder(-750)]
    public class BuildingManager : MonoBehaviour
    {
        public static BuildingManager Instance { get; private set; }

        private readonly List<BuildingBase> _buildings = new();
        public IReadOnlyList<BuildingBase> AllBuildings => _buildings;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void Register(BuildingBase b)
        {
            if (b != null && !_buildings.Contains(b))
                _buildings.Add(b);
        }

        public void Unregister(BuildingBase b)
        {
            if (b != null) _buildings.Remove(b);
        }

        /// <summary>
        /// Уничтожает ВСЕ здания БЕЗ возврата ресурсов и БЕЗ событий.
        /// Используется при перегенерации карты (иначе можно нафармить ресурсов).
        /// </summary>
        public void ClearAllSilent()
        {
            // Копия — потому что Destroy вызовет OnDestroy, который меняет _buildings
            var copy = new List<BuildingBase>(_buildings);
            foreach (var b in copy)
            {
                if (b == null) continue;

                // Снимаем регистрацию с клеток (если сетка ещё старая)
                if (GridSystem.Instance != null)
                {
                    foreach (var cell in b.GetOccupiedCells())
                        GridSystem.Instance.UnregisterBuilding(cell.x, cell.y);
                }

                Destroy(b.gameObject);
            }
            _buildings.Clear();
            Debug.Log($"[BuildingManager] Уничтожено {copy.Count} зданий.");
        }
    }
}