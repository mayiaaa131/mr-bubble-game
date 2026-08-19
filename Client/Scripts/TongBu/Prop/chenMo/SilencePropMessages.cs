using System;
using System.Collections.Generic;

// ════════════════════════════════════════════════════════
// 沉默道具：服务端 → 拾取者（单播）
// ════════════════════════════════════════════════════════

/// <summary>
/// 沉默道具拾取通知
/// 服务端 → 仅拾取者（单播）
/// type = "SilencePropPickedUp"
/// </summary>
[Serializable]
public class SilencePropPickedUpMessage
{
    public string type;         // "SilencePropPickedUp"
    public string roomId;
    public string playerId;
    public long timestamp;
}

// ════════════════════════════════════════════════════════
// 沉默道具：客户端 → 服务端（放置请求）
// ════════════════════════════════════════════════════════

/// <summary>
/// 沉默道具放置请求
/// 客户端 → 服务端
/// type = "SilencePropPlace"
/// </summary>
[Serializable]
public class SilencePropPlaceRequest
{
    public string type;         // "SilencePropPlace"
    public string playerId;     // ⚠️ 服务端会用可信值覆盖
    public string teamId;       // ⚠️ 服务端会用可信值覆盖
    public GSPosition position;
    public long timestamp;
}

// ════════════════════════════════════════════════════════
// 沉默道具：服务端 → 所有客户端（放置结果广播）
// ════════════════════════════════════════════════════════

/// <summary>
/// 沉默道具放置结果广播
/// 服务端 → 所有客户端
/// type = "SilencePropPlaced"
/// </summary>
[Serializable]
public class SilencePropPlacedMessage
{
    public string type;                 // "SilencePropPlaced"
    public string roomId;
    public string placedByPlayerId;
    public string placedByTeamId;
    public GSPosition position;
    public float effectHalfSize;        // 正方形范围半边长
    public float duration;              // 客户端道具实例存活时间（秒）
    public long timestamp;
}

// ════════════════════════════════════════════════════════
// 沉默道具：持有状态（仅用于调试JSON）
// ════════════════════════════════════════════════════════

/// <summary>
/// 单个玩家的沉默道具持有状态（调试用）
/// </summary>
[Serializable]
public class SilencePropHoldStateInfo
{
    public string playerId;
    public string playerName;
    public string teamId;
    public bool isHolding;
}

/// <summary>
/// 沉默道具持有状态消息（调试JSON用）
/// </summary>
[Serializable]
public class SilencePropHoldStateMessage
{
    public string type;         // "SilencePropHoldState"
    public string roomId;
    public long timestamp;
    public List<SilencePropHoldStateInfo> holdStates;

    public SilencePropHoldStateMessage()
    {
        holdStates = new List<SilencePropHoldStateInfo>();
    }
}
