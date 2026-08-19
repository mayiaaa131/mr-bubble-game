using System;
using System.Collections.Generic;

/// <summary>
/// 锚点状态枚举
/// </summary>
public enum AnchorStatus
{
    None = 0,           // 未初始化
    Active = 1,         // 有效，Host在线
    HostOffline = 2,    // Host离线但锚点仍可用
    Invalid = 3         // 已失效，需重建
}

/// <summary>
/// 物理空间对齐房间信息结构
/// 管理单个房间中的空间同步数据
/// </summary>
[Serializable]
public class MRAlignmentRoom
{
    public string RoomId { get; set; }
    public string HostPlayerId { get; set; }                    // Host的PlayerId
    public string SharedAnchorId { get; set; }                  // 共享锚点UUID
    public long AnchorTimestamp { get; set; }                   // 锚点创建时间戳
    public AnchorStatus AnchorStatus { get; set; } = AnchorStatus.None;  // ★ P0 修复：锚点状态
    public Dictionary<string, MRAlignmentPeer> Peers { get; set; } = new Dictionary<string, MRAlignmentPeer>();
}
