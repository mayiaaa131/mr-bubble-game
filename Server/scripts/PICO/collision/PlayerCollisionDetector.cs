using BubbleBattle.Network;
using UnityEngine;

public class PlayerCollisionDetector : MonoBehaviour
{
    [Header("冷却配置")]
    [SerializeField] private float collisionCooldown = 2f;  // 2秒冷却

    private float _lastCollisionTime = -999f;
    private string _playerId;
    private string _teamId;
    private string _roomId;

    void Start()
    {
        // 从 WebSocket 客户端获取玩家信息
        if (PicoWebSocketClient.Instance != null)
        {
            PicoWebSocketClient.Instance.OnPlayerAssignedId += (id) =>
            {
                _playerId = id;
                _teamId = PicoWebSocketClient.Instance.TeamId;
                _roomId = PicoWebSocketClient.Instance.RoomId;
                Debug.Log($"[CollisionDetector] 玩家初始化: {_playerId}");
            };
        }
    }
    /*
    /// <summary>
    /// OnTriggerEnter - 检测到穿墙
    /// </summary>
    private void OnTriggerEnter(Collider other)
    {
        // 检查碰撞物体是否在 Default Layer
        if (other.gameObject.layer != LayerMask.NameToLayer("Default"))
            return;
        // 检查冷却时间
        if (Time.time - _lastCollisionTime < collisionCooldown)
            return;
        // 检查玩家信息
        if (string.IsNullOrEmpty(_playerId))
        {
            Debug.LogWarning("[CollisionDetector] 玩家ID未初始化");
            return;
        }
        // 更新冷却时间
        _lastCollisionTime = Time.time;
        // 发送穿墙碰撞消息
        SendCollisionMessage();
    }*/
    private void OnTriggerStay(Collider other)
    {
        Debug.Log($"[Collision] Stay with: {other.gameObject.name}");
        if (other.gameObject.layer != LayerMask.NameToLayer("Default"))
            return;
        if (Time.time - _lastCollisionTime < collisionCooldown)
            return;

        if (string.IsNullOrEmpty(_playerId))
            return;

        _lastCollisionTime = Time.time;
        SendCollisionMessage();
    }

    /// <summary>
    /// 发送碰撞消息给服务器
    /// </summary>
    private void SendCollisionMessage()
    {
        var msg = new ObstacleCollisionMsg
        {
            playerId = _playerId,
            teamId = _teamId,
            roomId = _roomId,
            timestamp = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };

        string json = JsonUtility.ToJson(msg);
        PicoWebSocketClient.Instance.SendRawMessage(json);

        Debug.Log($"[CollisionDetector] 穿墙！发送碰撞消息，冷却 {collisionCooldown}秒");
    }
}
