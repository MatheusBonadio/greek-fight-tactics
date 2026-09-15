using System;

namespace Game.Grid
{
    internal sealed class NodeHeap
    {
        private const int InitialCapacity = 16;

        private int[] nodes = new int[InitialCapacity];
        private int[] priorities = new int[InitialCapacity];

        public int Count { get; private set; }

        public void Clear() => Count = 0;

        public void EnsureCapacity(int capacity)
        {
            if (nodes.Length >= capacity)
            {
                return;
            }

            Array.Resize(ref nodes, capacity);
            Array.Resize(ref priorities, capacity);
        }

        public void Push(int node, int priority)
        {
            if (Count == nodes.Length)
            {
                EnsureCapacity(Count * 2);
            }

            var index = Count++;
            while (index > 0)
            {
                var parent = (index - 1) / 2;
                if (priorities[parent] <= priority)
                {
                    break;
                }

                nodes[index] = nodes[parent];
                priorities[index] = priorities[parent];
                index = parent;
            }

            nodes[index] = node;
            priorities[index] = priority;
        }

        public bool TryPop(out int node)
        {
            if (Count == 0)
            {
                node = -1;
                return false;
            }

            node = nodes[0];
            Count--;
            var lastNode = nodes[Count];
            var lastPriority = priorities[Count];

            var index = 0;
            while (true)
            {
                var child = index * 2 + 1;
                if (child >= Count)
                {
                    break;
                }

                if (child + 1 < Count && priorities[child + 1] < priorities[child])
                {
                    child++;
                }

                if (priorities[child] >= lastPriority)
                {
                    break;
                }

                nodes[index] = nodes[child];
                priorities[index] = priorities[child];
                index = child;
            }

            nodes[index] = lastNode;
            priorities[index] = lastPriority;
            return true;
        }
    }
}
