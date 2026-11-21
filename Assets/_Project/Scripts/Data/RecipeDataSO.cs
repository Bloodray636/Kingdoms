using System.Collections.Generic;
using UnityEngine;

namespace KingdomsClone.Data
{
    /// <summary>
    /// Рецепт производства: каждые CycleSeconds забирает Inputs, выдаёт Outputs.
    /// Если Inputs пуст — пассивное производство (лесоруб, ферма).
    /// Если есть — активное (кузница, плавильня).
    /// </summary>
    [CreateAssetMenu(
        menuName = "KingdomsClone/Recipe",
        fileName = "Recipe_New",
        order = 4)]
    public class RecipeDataSO : DataSO
    {
        [Header("Inputs (потребление)")]
        [Tooltip("Что забирается перед производством. Пусто — пассивное производство.")]
        [SerializeField] private List<ResourceAmount> inputs = new();

        [Header("Outputs (результат)")]
        [Tooltip("Что производится за один цикл. Сейчас используется ТОЛЬКО первый output.")]
        [SerializeField] private List<ResourceAmount> outputs = new();

        [Header("Time")]
        [SerializeField, Min(0.5f)] private float cycleSeconds = 10f;

        public IReadOnlyList<ResourceAmount> Inputs => inputs;
        public IReadOnlyList<ResourceAmount> Outputs => outputs;
        public float CycleSeconds => cycleSeconds;

        public bool HasInputs => inputs != null && inputs.Count > 0;
        public bool HasOutputs => outputs != null && outputs.Count > 0;

        /// <summary>
        /// Первый output — то, что несёт житель в руках (MVP: один ресурс за цикл).
        /// </summary>
        public ResourceAmount PrimaryOutput =>
            HasOutputs ? outputs[0] : null;

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            if (cycleSeconds < 0.5f) cycleSeconds = 0.5f;

            // Валидация: все записи должны быть не-null и с amount > 0
            ValidateList(inputs, "inputs");
            ValidateList(outputs, "outputs");
        }

        private void ValidateList(List<ResourceAmount> list, string label)
        {
            if (list == null) return;
            for (int i = 0; i < list.Count; i++)
            {
                var ra = list[i];
                if (ra == null)
                {
                    Debug.LogWarning($"[{name}] {label}[{i}] == null");
                    continue;
                }
                if (ra.Resource == null)
                    Debug.LogWarning($"[{name}] {label}[{i}] ресурс не назначен");
                if (ra.Amount <= 0)
                    Debug.LogWarning($"[{name}] {label}[{i}] amount = {ra.Amount} (должно быть > 0)");
            }
        }
#endif
    }
}