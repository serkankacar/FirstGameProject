using System;
using System.Collections.Generic;
using UnityEngine;

namespace OkeyGame.Unity.Networking
{
    /// <summary>
    /// Unity Main Thread üzerinde callback çalıştırmak için yardımcı sınıf.
    /// SignalR callback'leri farklı thread'den gelir, 
    /// Unity API'leri sadece main thread'den çağrılabilir.
    /// </summary>
    public class MainThreadDispatcher : MonoBehaviour
    {
        #region Singleton

        private static MainThreadDispatcher _instance;
        private static readonly object _lock = new object();
        private static bool _applicationIsQuitting = false;

        public static MainThreadDispatcher Instance
        {
            get
            {
                if (_applicationIsQuitting)
                {
                    return null;
                }

                lock (_lock)
                {
                    if (_instance == null)
                    {
                        _instance = FindObjectOfType<MainThreadDispatcher>();

                        if (_instance == null)
                        {
                            var go = new GameObject("MainThreadDispatcher");
                            _instance = go.AddComponent<MainThreadDispatcher>();
                            DontDestroyOnLoad(go);
                        }
                    }

                    return _instance;
                }
            }
        }

        #endregion

        #region Fields

        private readonly Queue<Action> _executionQueue = new Queue<Action>();
        private readonly object _queueLock = new object();

        #endregion

        #region Unity Lifecycle

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
            ProcessQueue();
        }

        private void OnDestroy()
        {
            _applicationIsQuitting = true;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Bir aksiyonu main thread kuyruğuna ekler.
        /// </summary>
        public static void Enqueue(Action action)
        {
            if (action == null) return;

            var instance = Instance;
            if (instance == null) return;

            lock (instance._queueLock)
            {
                instance._executionQueue.Enqueue(action);
            }
        }

        /// <summary>
        /// Bir aksiyonu main thread'de çalıştırır (senkron bekler).
        /// DİKKAT: Main thread'den çağrılmamalı - deadlock oluşturur!
        /// </summary>
        public static void ExecuteOnMainThread(Action action)
        {
            if (action == null) return;

            bool completed = false;
            Exception capturedException = null;

            Enqueue(() =>
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    capturedException = ex;
                }
                finally
                {
                    completed = true;
                }
            });

            // Tamamlanana kadar bekle
            while (!completed)
            {
                System.Threading.Thread.Sleep(1);
            }

            if (capturedException != null)
            {
                throw capturedException;
            }
        }

        #endregion

        #region Private Methods

        private void ProcessQueue()
        {
            lock (_queueLock)
            {
                while (_executionQueue.Count > 0)
                {
                    try
                    {
                        var action = _executionQueue.Dequeue();
                        action?.Invoke();
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[MainThreadDispatcher] Aksiyon hatası: {ex}");
                    }
                }
            }
        }

        #endregion
    }
}
