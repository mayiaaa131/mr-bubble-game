using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 道具信息类
/// </summary>
[Serializable]
public class PropInfo
{
    public string propId;           // 道具ID
    public string propType;         // 道具类型：BloodRestore、AmmoRestore等
    public GSPosition position;     // 道具位置
    public int restoreAmount;       // 恢复血量
    public string state;            // Available（可用）/ Cooldown（冷却中）
    public long respawnTime;        // 重生时间（毫秒）
    public long lastPickupTime;     // 上次被拾取的时间戳
    public long serverTimestamp;    // 服务器时间戳

    public PropInfo() { }
    public PropInfo(string propId, string propType, Vector3 position, int restoreAmount)
    {
        this.propId = propId;
        this.propType = propType;
        this.position = new GSPosition(position.x, position.y, position.z);
        this.restoreAmount = restoreAmount;
        this.state = "Available";
        this.respawnTime = 10000;   // 10秒
        this.lastPickupTime = 0;
    }
}

/// <summary>
/// 道具状态广播消息
/// 服务端 → 所有客户端
/// type = "PropStateBroadcast"
/// </summary>
[Serializable]
public class PropStateMessage
{
    public string type;                 // "PropStateBroadcast"
    public string roomId;               // 房间ID
    public long timestamp;              // 消息时刻戳
    public List<PropInfo> props;        // 所有道具信息

    public PropStateMessage()
    {
        props = new List<PropInfo>();
    }
}

/// <summary>
/// 玩家拾取道具请求
/// 客户端 → 服务端
/// </summary>
[Serializable]
public class PropPickupRequest
{
    public string type;                 // "PropPickup"
    public string playerId;
    public string propId;
    public long timestamp;
}
