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
        public float BaseHappiness => baseHappiness;

        private readonly Dictionary<string, float> _modifiers = new();

        private void Awake()
        {
            if (Instance != null && Instance != this) { 
                Destroy(gameObject); 
                return; 
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            Recalculate();
        }

        private void OnDestroy() { if (Instance == this) Instance = null; }

        public void SetModifier(string source, float value)
        {
            _modifiers[source] = value;
            Recalculate();
        }

        public void RemoveModifier(string source)
        {
            if (_modifiers.Remove(source)) Recalculate();
        }

        public float GetModifiersSum()
        {
            float sum = 0f;

            foreach (var kvp in _modifiers) {
                sum += kvp.Value;
            }

            return sum;
        }

        private void Recalculate()
        {
            float old = CurrentHappiness;
            float total = baseHappiness;

            foreach (var kvp in _modifiers) total += kvp.Value;

            CurrentHappiness = Mathf.Clamp(total, 0f, 10f);

            if (!Mathf.Approximately(old, CurrentHappiness)) {
                EventBus.Publish(new HappinessChangedEvent
                {
                    OldValue = old,
                    NewValue = CurrentHappiness
                });

                Debug.Log($"[Happiness] {old:F1} → {CurrentHappiness:F1} (модификаторов: {_modifiers.Count})");
            }
        }

        public void DebugForceSet(float value)
        {
            value = Mathf.Clamp(value, 0f, 10f);

            float modifierSum = GetModifiersSum();
            float newBase = value - modifierSum;

            if (newBase < 0f || newBase > 10f) {
                Debug.LogWarning($"[Happiness] DebugForceSet({value:F1}): base={newBase:F1} вне [0,10] " + $"(модификаторы={modifierSum:F1}). Итог будет {Mathf.Clamp(newBase + modifierSum, 0f, 10f):F1}, не {value:F1}.");
            }

            baseHappiness = newBase;
            Recalculate();

            Debug.Log($"[Happiness] DebugForceSet({value:F1}): base={baseHappiness:F1}, модификаторы={modifierSum:F1} (сохранены).");
        }

        public void DebugResetAll(float newBase = 5f)
        {
            _modifiers.Clear();
            baseHappiness = Mathf.Clamp(newBase, 0f, 10f);
            Recalculate();
            Debug.Log($"[Happiness] DebugResetAll: base={baseHappiness:F1}, модификаторы очищены.");
        }

        public IReadOnlyDictionary<string, float> GetModifiers() => _modifiers;
    }
}