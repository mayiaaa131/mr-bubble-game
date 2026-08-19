using System;
using NativeWebSocket;

/// <summary>
/// MR物理空间对齐的参与者（对等体）信息
/// 记录每个连接的客户端在空间同步中的信息
/// </summary>
[Serializable]
public class MRAlignmentPeer
{
    public string PlayerId { get; set; }
    public string ClientId { get; set; }
    public WebSocket WebSocket { get; set; }
    public string PlayerName { get; set; }
    public string TeamId { get; set; }
    public bool IsHost { get; set; }
    public long ConnectTime { get; set; }
}
