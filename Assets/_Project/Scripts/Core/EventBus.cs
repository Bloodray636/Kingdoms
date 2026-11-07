using System;
using System.Collections.Generic;
using UnityEngine;

namespace KingdomsClone.Core
{
    public static class EventBus
    {
        private static readonly Dictionary<Type, Delegate> _handlers = new();

        public static void Subscribe<T>(Action<T> handler) where T : struct
        {
            if (handler == null) return;
            var type = typeof(T);
            if (_handlers.TryGetValue(type, out var existing))
                _handlers[type] = Delegate.Combine(existing, handler);
            else
                _handlers[type] = handler;
        }

        public static void Unsubscribe<T>(Action<T> handler) where T : struct
        {
            if (handler == null) return;
            var type = typeof(T);
            if (!_handlers.TryGetValue(type, out var existing)) return;
            var result = Delegate.Remove(existing, handler);
            if (result == null) _handlers.Remove(type);
            else _handlers[type] = result;
        }

        public static void Publish<T>(T evt) where T : struct
        {
            if (!_handlers.TryGetValue(typeof(T), out var d)) return;
            // Снимок списка — защита от подписки/отписки во время рассылки
            var invocations = d.GetInvocationList();
            for (int i = 0; i < invocations.Length; i++)
            {
                try
                {
                    ((Action<T>)invocations[i]).Invoke(evt);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[EventBus] {typeof(T).Name} handler threw: {e}");
                }
            }
        }

        /// <summary>Очистить всё. Использовать только при выходе в меню/рестарте.</summary>
        public static void Clear() => _handlers.Clear();
    }
}