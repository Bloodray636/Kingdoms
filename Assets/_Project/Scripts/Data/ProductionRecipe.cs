using System;
using System.Collections.Generic;
using UnityEngine;

namespace KingdomsClone.Data
{
    [Serializable]
    public class ProductionRecipe
    {
        [SerializeField] private List<ResourceAmount> inputs = new();
        [SerializeField] private List<ResourceAmount> outputs = new();
        [Tooltip("Длительность одного цикла в секундах.")]
        [SerializeField, Min(0.1f)] private float cycleSeconds = 10f;

        public IReadOnlyList<ResourceAmount> Inputs => inputs;
        public IReadOnlyList<ResourceAmount> Outputs => outputs;
        public float CycleSeconds => cycleSeconds;

        public bool HasOutputs => outputs != null && outputs.Count > 0;
        public bool RequiresInputs => inputs != null && inputs.Count > 0;
    }
}