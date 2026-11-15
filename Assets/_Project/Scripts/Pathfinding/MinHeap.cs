using System.Collections.Generic;

namespace KingdomsClone.Pathfinding
{
    /// <summary>
    /// Min-куча по F-score для A*. Классическая бинарная куча на массиве.
    /// Сложность: Push/Pop = O(log n), Peek = O(1).
    /// </summary>
    public class MinHeap
    {
        private readonly List<PathNode> _items = new();

        public int Count => _items.Count;
        public void Clear() => _items.Clear();

        public void Push(PathNode node)
        {
            _items.Add(node);
            int i = _items.Count - 1;
            while (i > 0)
            {
                int parent = (i - 1) / 2;
                if (_items[parent].F <= _items[i].F) break;
                (_items[parent], _items[i]) = (_items[i], _items[parent]);
                i = parent;
            }
        }

        public PathNode Pop()
        {
            var top = _items[0];
            int last = _items.Count - 1;
            _items[0] = _items[last];
            _items.RemoveAt(last);

            int i = 0;
            int n = _items.Count;
            while (true)
            {
                int l = 2 * i + 1;
                int r = 2 * i + 2;
                int smallest = i;

                if (l < n && _items[l].F < _items[smallest].F) smallest = l;
                if (r < n && _items[r].F < _items[smallest].F) smallest = r;
                if (smallest == i) break;

                (_items[smallest], _items[i]) = (_items[i], _items[smallest]);
                i = smallest;
            }
            return top;
        }
    }
}