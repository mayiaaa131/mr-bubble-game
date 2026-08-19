using System;
using System.Collections.Generic;

/// <summary>
/// 爆炸范围（矩形）
/// </summary>
[Serializable]
public class ExplosionRange
{
    public float xMin;
    public float xMax;
    public float zMin;
    public float zMax;

    public ExplosionRange() { }
    public ExplosionRange(float xMin, float xMax, float zMin, float zMax)
    {
        this.xMin = xMin;
        this.xMax = xMax;
        this.zMin = zMin;
        this.zMax = zMax;
    }
}

[Serializable]
public class BombData
{
    public string bombId;
    public string playerId;
    public string teamId;
    public GSPosition position;
    public float totalTime;
    public float remainingTime;
    public string state;

    public string bombType;
    public string bombLevel;
    public List<ExplosionRange> explosionRanges;

    public long createTime;
    public long explosionTimestamp;
    public long serverTimestamp;

    // ★ 合并组字段（重构，移除 isAbsorbed）
    public string mergeGroupId;         // 所属合并组的主炸弹ID（空=独立炸弹）
    public int mergeCount;              // 组内炸弹总数（所有成员同步此值，用于客户端显示等级）
    public List<string> mergedBombIds;  // 组内从炸弹ID列表（仅主炸弹维护）
    public bool isMaster;               // 是否为主炸弹（负责倒计时）



    public BombData()
    {
        mergeCount = 1;
        mergedBombIds = new List<string>();
        isMaster = true;
        mergeGroupId = "";
    }
}

/// <summary>
/// 炸弹状态广播消息（服务端 → 所有客户端）
/// type = "BombState"
/// </summary>
[Serializable]
public class BombStateMessage
{
    public string type;                 // "BombState"
    public string roomId;               // 房间ID
    public long timestamp;              // 消息时刻戳
    public bool isRetransmit;           // 是否为补包
    public long frameSequenceNumber;    // 帧序列号
    public List<BombData> bombs;        // 所有炸弹信息

    public BombStateMessage()
    {
        bombs = new List<BombData>();
    }
}

/// <summary>  
/// 客户端发送炸弹创建请求（客户端 → 服务端）  
/// </summary>  
[Serializable]
public class BombCreateRequest
{
    public string type;                 // "BombCreate"  
    public string playerId;             // ← 改为 playerId（与客户端一致）  
    public string teamId;               // ← 新增：队伍ID  
    public GSPosition position;         // 炸弹位置  
    public string bombType;             // 炸弹类型  
    public string bombId;               // ← 新增：客户端生成的炸弹ID  
    public long timestamp;
}