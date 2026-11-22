using System.Collections.Generic;
using UnityEngine;

namespace KingdomsClone.Core.Events
{
    public struct PathInvalidationEvent
    {
        public IReadOnlyList<Vector2Int> Cells;
    }
}