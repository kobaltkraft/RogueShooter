using System;
using System.Collections.Generic;
using UnityEngine;

namespace RogueArena.VFX
{
    /// <summary>
    /// Minimal, allocation-free component pool. Pre-warms instances, grows lazily,
    /// and reuses them - the game never Instantiate/Destroy cycles effects.
    /// </summary>
    public sealed class ComponentPool<T> where T : Component
    {
        readonly Func<T> factory;
        readonly Action<T> onReturn;
        readonly Stack<T> free = new Stack<T>();
        readonly List<T> all = new List<T>();
        readonly int maxInstances;

        public ComponentPool(Func<T> factory, int prewarm = 0, Action<T> onReturn = null, int maxInstances = 256)
        {
            this.factory = factory;
            this.onReturn = onReturn;
            this.maxInstances = maxInstances;
            for (int i = 0; i < prewarm; i++)
            {
                T item = factory();
                SetActive(item, false);
                free.Push(item);
                all.Add(item);
            }
        }

        public T Get()
        {
            if (free.Count == 0)
            {
                if (all.Count >= maxInstances) return null; // pool saturated: skip politely
                T item = factory();
                all.Add(item);
                SetActive(item, false);
                free.Push(item);
            }
            T result = free.Pop();
            SetActive(result, true);
            return result;
        }

        public void Return(T item)
        {
            if (item == null) return;
            onReturn?.Invoke(item);
            SetActive(item, false);
            if (!free.Contains(item)) free.Push(item);
        }

        static void SetActive(T item, bool active)
        {
            if (item != null && item.gameObject.activeSelf != active)
                item.gameObject.SetActive(active);
        }
    }
}
