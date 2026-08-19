using UnityEngine;
using System;
using System.Collections.Generic;

/// <summary>
/// 把子线程任务派发到 Unity 主线程执行
/// （WebSocket 回调在子线程，不能直接调 Unity API）
/// </summary>
public class UnityMainThreadDispatcher : MonoBehaviour
{
    private static readonly Queue<Action> _queue = new Queue<Action>();
    private static UnityMainThreadDispatcher _instance;

    public static void Enqueue(Action action)
    {
        lock (_queue)
        {
            _queue.Enqueue(action);
        }
    }

    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Update()
    {
        lock (_queue)
        {
            while (_queue.Count > 0)
            {
                _queue.Dequeue().Invoke();
            }
        }
    }
}
