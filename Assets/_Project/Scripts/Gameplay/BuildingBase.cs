using System.Collections.Generic;
using UnityEngine;
using KingdomsClone.Data;
using KingdomsClone.Core;
using KingdomsClone.Core.Events;
using KingdomsClone.Grid;

namespace KingdomsClone.Gameplay
{
    [RequireComponent(typeof(BoxCollider))]
    public class BuildingBase : MonoBehaviour
    {
        [Header("Runtime Info (Read Only)")]
        [SerializeField] private BuildingState _state = BuildingState.Placing;
        [SerializeField] private int _currentHealth;

        public BuildingDataSO Data { get; private set; }
        public Vector2Int GridPosition { get; private set; } // Левый нижний угол footprint'а
        public Quaternion Rotation { get; private set; }
        public BuildingState State => _state;
        public int CurrentHealth => _currentHealth;

        public void Initialize(BuildingDataSO data, Vector2Int gridPos, Quaternion rotation)
        {
            Data = data;
            GridPosition = gridPos;
            Rotation = rotation;
            _currentHealth = data.MaxHealth;
            
            gameObject.name = $"{data.DisplayName} ({gridPos.x}, {gridPos.y})";
            transform.rotation = rotation;

            // Коллайдер по размеру здания
            var col = GetComponent<BoxCollider>();
            if (col != null && data.Size.x > 0 && data.Size.y > 0)
            {
                col.center = new Vector3((data.Size.x - 1) * 0.5f, 0.5f, (data.Size.y - 1) * 0.5f);
                col.size = new Vector3(data.Size.x, 1f, data.Size.y);
            }

            SetState(BuildingState.Active); 
            OnPlaced();
            
            EventBus.Publish(new BuildingPlacedEvent { Data = Data, Position = GridPosition });
        }

        /// <summary>
        /// Все клетки, занимаемые зданием.
        /// </summary>
        public IEnumerable<Vector2Int> GetOccupiedCells()
        {
            if (Data == null) yield break;
            for (int x = 0; x < Data.Size.x; x++)
                for (int y = 0; y < Data.Size.y; y++)
                    yield return new Vector2Int(GridPosition.x + x, GridPosition.y + y);
        }

        public void SetState(BuildingState newState)
        {
            if (_state == newState) return;
            _state = newState;
            if (_state == BuildingState.Destroyed)
            {
                if (GridSystem.Instance != null)
                {
                    foreach (var cell in GetOccupiedCells())
                        GridSystem.Instance.UnregisterBuilding(cell.x, cell.y);
                }

                RefundHalfCost();
                OnRemoved();
                EventBus.Publish(new BuildingRemovedEvent { Data = Data, Position = GridPosition });
                Destroy(gameObject, 0.1f); 
            }
        }

        public void TakeDamage(int amount)
        {
            if (_state == BuildingState.Destroyed) return;
            _currentHealth -= amount;
            Debug.Log($"[Building] {Data.DisplayName} получил {amount} урона. HP: {_currentHealth}");
            if (_currentHealth <= 0)
            {
                _currentHealth = 0;
                SetState(BuildingState.Destroyed);
            }
        }

        private void RefundHalfCost()
        {
            if (Data == null || Data.BuildCost == null) return;
            if (ResourceManager.Instance == null) return;

            foreach (var ra in Data.BuildCost)
            {
                if (ra == null || ra.Resource == null) continue;
                int refund = Mathf.FloorToInt(ra.Amount * 0.5f);
                if (refund > 0) ResourceManager.Instance.AddResource(ra.Resource, refund);
            }
            Debug.Log($"[Building] Возвращено 50% за {Data.DisplayName}");
        }

        protected virtual void OnPlaced() { }
        protected virtual void OnRemoved() { }

        private void OnMouseDown() 
        {
            Debug.Log($"[Building] КЛИК по {Data?.DisplayName}");
            TakeDamage(20);
        }
    }
}