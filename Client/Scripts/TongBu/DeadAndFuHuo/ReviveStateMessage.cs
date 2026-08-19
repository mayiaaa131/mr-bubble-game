using System;
using System.Collections.Generic;

/// <summary>
/// 玩家复活状态信息
/// </summary>
[Serializable]
public class ReviveStateInfo
{
    public string playerId;
    public string playerName;
    public bool isDead;              // 是否已死亡
    public float reviveCountdown;    // 复活倒计时（秒）
}

/// <summary>
/// 玩家复活状态广播消息
/// 服务端 → 所有客户端
/// type = "ReviveState"
/// </summary>
[Serializable]
public class ReviveStateMessage
{
    public string type;                      // "ReviveState"
    public string roomId;
    public long timestamp;
    public List<ReviveStateInfo> reviveStates;

    public ReviveStateMessage()
    {
        reviveStates = new List<ReviveStateInfo>();
    }
}
