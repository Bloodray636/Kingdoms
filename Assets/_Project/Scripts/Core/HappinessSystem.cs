using System.Collections.Generic;
using UnityEngine;
using KingdomsClone.Core.Events;

namespace KingdomsClone.Core
{
    [DefaultExecutionOrder(-760)]
    public class HappinessSystem : MonoBehaviour
    {
        public static HappinessSystem Instance { get; private set; }

        [Header("Base")]
        [SerializeField] private float baseHappiness = 5f;

        public float CurrentHappiness { get; private set; }

        // Модификаторы: "tax" → -5, "food" → +2, "church" → +3 и т.д.
        private readonly Dictionary<string, float> _modifiers = new();

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Recalculate();
        }

        private void OnDestroy() { if (Instance == this) Instance = null; }

        /// <summary>
        /// Установить модификатор от конкретной системы. Значение заменяется.
        /// </summary>
        public void SetModifier(string source, float value)
        {
            _modifiers[source] = value;
            Recalculate();
        }

        public void RemoveModifier(string source)
        {
            if (_modifiers.Remove(source)) Recalculate();
        }

        private void Recalculate()
        {
            float old = CurrentHappiness;
            float total = baseHappiness;

            foreach (var kvp in _modifiers) total += kvp.Value;

            CurrentHappiness = Mathf.Clamp(total, 0f, 10f);

            if (!Mathf.Approximately(old, CurrentHappiness))
            {
                EventBus.Publish(new HappinessChangedEvent
                {
                    OldValue = old,
                    NewValue = CurrentHappiness
                });
                Debug.Log($"[Happiness] {old:F1} → {CurrentHappiness:F1} (модификаторы: {_modifiers.Count})");
            }
        }

        /// <summary>
        /// Для отладки/дебаг-меню. Не использовать в геймплее.
        /// </summary>
        public void DebugForceSet(float value)
        {
            baseHappiness = Mathf.Clamp(value, 0f, 10f);
            _modifiers.Clear();
            Recalculate();
        }

        public IReadOnlyDictionary<string, float> GetModifiers() => _modifiers;
    }
}