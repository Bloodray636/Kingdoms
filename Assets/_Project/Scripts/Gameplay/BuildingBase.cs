using System.Collections;
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
        [Header("Runtime (Read Only)")]
        [SerializeField] private BuildingState _state = BuildingState.Placing;
        [SerializeField] private int _currentHealth;
        [SerializeField] private float _buildProgress;

        [Header("Debug")]
        [Tooltip("Клик по зданию наносит 20 урона — ТОЛЬКО ДЛЯ ТЕСТА. Выключи в релизе.")]
        [SerializeField] private bool debugClickDamage = false;

        public BuildingDataSO Data { get; private set; }
        public Vector2Int GridPosition { get; private set; } // Anchor — левый нижний угол footprint'а
        public Quaternion Rotation { get; private set; }
        public BuildingState State => _state;
        public int CurrentHealth => _currentHealth;
        public float BuildProgress => _buildProgress;

        public Vector2Int RotatedSize => Data != null
            ? FootprintUtil.GetRotatedSize(Data.Size, Rotation)
            : Vector2Int.one;

        private Coroutine _buildRoutine;

        // ==================== ИНИЦИАЛИЗАЦИЯ ====================

        public void Initialize(BuildingDataSO data, Vector2Int anchor, Quaternion rotation)
        {
            if (data == null)
            {
                Debug.LogError("[BuildingBase] Initialize: data == null! Уничтожаю объект.");
                Destroy(gameObject);
                return;
            }

            Data = data;
            GridPosition = anchor;
            Rotation = rotation;
            _currentHealth = data.MaxHealth;
            _buildProgress = 0f;

            gameObject.name = $"{data.DisplayName} ({anchor.x}, {anchor.y})";
            transform.rotation = rotation;

            // Коллайдер по повёрнутому footprint'у
            Vector2Int rotated = RotatedSize;
            var col = GetComponent<BoxCollider>();
            if (col != null)
            {
                col.center = new Vector3(0f, 0.5f, 0f);
                col.size = new Vector3(rotated.x, 1f, rotated.y);
            }

            // Регистрация в менеджере
            if (BuildingManager.Instance != null)
                BuildingManager.Instance.Register(this);

            // ФИКС 2.1: правильно переходим Placing → Building → Active (а не сразу Placing → Active)
            float buildTime = Mathf.Max(0f, data.BuildTimeSeconds);
            if (buildTime <= 0.01f)
            {
                SetState(BuildingState.Building);
                SetState(BuildingState.Active);
                _buildProgress = 1f;
                OnPlaced();
                EventBus.Publish(new BuildingPlacedEvent { Data = Data, Position = GridPosition });
            }
            else
            {
                SetState(BuildingState.Building);
                _buildRoutine = StartCoroutine(BuildRoutine(buildTime));
            }
        }

        private IEnumerator BuildRoutine(float duration)
        {
            _buildProgress = 0f;
            float t = 0f;

            while (t < duration && _state == BuildingState.Building)
            {
                t += Time.deltaTime;
                _buildProgress = Mathf.Clamp01(t / duration);
                yield return null;
            }

            _buildProgress = 1f;
            _buildRoutine = null;

            if (_state == BuildingState.Building)
            {
                SetState(BuildingState.Active);
                OnPlaced();
                EventBus.Publish(new BuildingPlacedEvent { Data = Data, Position = GridPosition });
            }
        }

        // ==================== ЖИЗНЕННЫЙ ЦИКЛ ====================

        private void OnDestroy()
        {
            if (BuildingManager.Instance != null)
                BuildingManager.Instance.Unregister(this);

            if (_buildRoutine != null)
            {
                StopCoroutine(_buildRoutine);
                _buildRoutine = null;
            }

            // ФИКС 2.5: при внешнем Destroy (не через SetState) освобождаем клетки.
            // Передаём gameObject как expectedOwner — чтобы не снести чужую регистрацию.
            if (GridSystem.Instance != null && Data != null)
            {
                foreach (var cell in GetOccupiedCells())
                    GridSystem.Instance.UnregisterBuilding(cell.x, cell.y, gameObject);
            }
        }

        // ==================== КЛЕТКИ ====================

        public IEnumerable<Vector2Int> GetOccupiedCells()
        {
            if (Data == null) yield break;
            foreach (var cell in FootprintUtil.GetOccupiedCells(GridPosition, Data.Size, Rotation))
                yield return cell;
        }

        // ==================== СОСТОЯНИЯ ====================

        public void SetState(BuildingState newState)
        {
            if (_state == newState) return;

            // ФИКС: запрет выхода из Destroyed
            if (_state == BuildingState.Destroyed)
            {
                Debug.LogWarning($"[BuildingBase] {Data?.DisplayName}: Destroyed → {newState} запрещён.");
                return;
            }

            // ФИКС: нельзя прыгнуть из Placing сразу в Active (только через Building)
            if (_state == BuildingState.Placing && newState == BuildingState.Active)
            {
                Debug.LogWarning($"[BuildingBase] {Data?.DisplayName}: Placing → Active запрещён, использую Building.");
                newState = BuildingState.Building;
            }

            _state = newState;

            if (_state == BuildingState.Destroyed)
            {
                if (_buildRoutine != null)
                {
                    StopCoroutine(_buildRoutine);
                    _buildRoutine = null;
                }

                if (GridSystem.Instance != null)
                {
                    foreach (var cell in GetOccupiedCells())
                        GridSystem.Instance.UnregisterBuilding(cell.x, cell.y, gameObject);
                }

                RefundHalfCost();
                OnRemoved();
                EventBus.Publish(new BuildingRemovedEvent { Data = Data, Position = GridPosition });
                Destroy(gameObject, 0.1f);
            }
        }

        // ==================== УРОН И ЛЕЧЕНИЕ ====================

        public void TakeDamage(int amount)
        {
            // ФИКС: отрицательный урон запрещён (было: "лечил" здание)
            if (amount <= 0) return;
            if (_state == BuildingState.Destroyed) return;

            _currentHealth = Mathf.Max(0, _currentHealth - amount);
            Debug.Log($"[Building] {Data.DisplayName} −{amount} HP, осталось {_currentHealth}");

            if (_currentHealth <= 0)
                SetState(BuildingState.Destroyed);
        }

        public void Heal(int amount)
        {
            if (amount <= 0) return;
            if (Data == null) return;
            if (_state == BuildingState.Destroyed) return;

            _currentHealth = Mathf.Min(Data.MaxHealth, _currentHealth + amount);
        }

        // ==================== РЕСУРСЫ ====================

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
            Debug.Log($"[Building] Возвращено 50% ресурсов за {Data.DisplayName}");
        }

        // ==================== ХУКИ ДЛЯ НАСЛЕДНИКОВ ====================

        protected virtual void OnPlaced() { }
        protected virtual void OnRemoved() { }

        // ==================== DEBUG ====================

        private void OnMouseDown()
        {
            if (!debugClickDamage) return;
            Debug.Log($"[Building] DEBUG клик по {Data?.DisplayName}");
            TakeDamage(20);
        }
    }
}