using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UDPServer.Network
{
    public class MainThreadDispatcher : MonoBehaviour
    {
        private static MainThreadDispatcher _instance;
        private readonly Queue<Action> _executeQueue = new Queue<Action>();
        private readonly object _lock = new object();

        public static MainThreadDispatcher Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("MainThreadDispatcher");
                    _instance = go.AddComponent<MainThreadDispatcher>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        private void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
                DontDestroyOnLoad(gameObject);
            } else if (_instance != this)
            {
                Destroy(gameObject);
            }
        }

        private void Update()
        {
            lock (_lock)
            {
                while (_executeQueue.Count > 0)
                {
                    _executeQueue.Dequeue()?.Invoke();
                }
            }
        }

        public void Enqueue(Action action)
        {
            if (action == null) return;

            lock (_lock)
            {
                _executeQueue.Enqueue(action);
            }
        }
    }
}
