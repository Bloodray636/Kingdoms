using System.Collections.Generic;
using UnityEngine;

namespace KingdomsClone.Core.Events
{
    /// <summary>
    /// Публикуется при ЛЮБОМ изменении навигации (здание, дорога, снос).
    /// Cells = конкретные клетки, которые изменились.
    /// Cells = null / пустой список → инвалидировать ВСЁ (перегенерация карты).
    /// </summary>
    public struct PathInvalidationEvent
    {
        public IReadOnlyList<Vector2Int> Cells;
    }
}