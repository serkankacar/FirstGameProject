using System;
using System.Collections.Generic;
using UnityEngine;

namespace OkeyGame.Network
{
    /// <summary>
    /// Unity ana thread'inde kod çalıştırmak için dispatcher.
    /// WebSocket callback'leri farklı thread'de çalışır, UI güncellemeleri ana thread'de olmalı.
    /// </summary>
    public class UnityMainThreadDispatcher : MonoBehaviour
    {
        private static UnityMainThreadDispatcher _instance;
        private static readonly Queue<Action> _executionQueue = new Queue<Action>();
        private static readonly object _lock = new object();

        public static UnityMainThreadDispatcher Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("UnityMainThreadDispatcher");
                    _instance = go.AddComponent<UnityMainThreadDispatcher>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Update()
        {
            lock (_lock)
            {
                while (_executionQueue.Count > 0)
                {
                    try
                    {
                        _executionQueue.Dequeue()?.Invoke();
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[MainThreadDispatcher] Error executing action: {ex.Message}");
                    }
                }
            }
        }

        /// <summary>
        /// Ana thread'de çalıştırılacak bir action ekler.
        /// </summary>
        public static void Enqueue(Action action)
        {
            if (action == null) return;
            
            lock (_lock)
            {
                _executionQueue.Enqueue(action);
            }
            
            // Instance'ı oluştur (lazy initialization)
            _ = Instance;
        }

        /// <summary>
        /// Tüm bekleyen action'ları temizler.
        /// </summary>
        public static void Clear()
        {
            lock (_lock)
            {
                _executionQueue.Clear();
            }
        }
    }
}
