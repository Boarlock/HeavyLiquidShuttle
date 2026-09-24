using System;
using System.Collections.Generic;
using System.Text;

namespace HeavyLiquidShuttleMod
{
    public class HashSetQueue<T>
    {
        private readonly Queue<T> _queue = new Queue<T>();
        private readonly HashSet<T> _set = new HashSet<T>();

        // Add item if it is not already in the set
        public bool Enqueue(T item)
        {
            if (_set.Add(item))
            {
                _queue.Enqueue(item);
                return true;
            }
            return false; 
        }

        // Remove and return the oldest item
        public T Dequeue()
        {
            if (_queue.Count == 0)
            {
                throw new InvalidOperationException("The queue is empty.");
            }

            T item = _queue.Dequeue();

            // Keep the hash set synchronized
            _set.Remove(item); 
            return item;
        }

        // Check the item at the top of the Queue without removing it
        public T Peek()
        {
            if (_queue.Count == 0)
            {
                throw new InvalidOperationException("The queue is empty.");
            }

            return _queue.Peek();
        }

        // Check if an item is currently in the queue
        public bool Contains(T item) => _set.Contains(item);

        // Get the current number of unique items
        public int Count => _queue.Count;

        // Clear both collections
        public void Clear()
        {
            _queue.Clear();
            _set.Clear();
        }
    }
}
