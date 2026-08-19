/// <summary>
/// ✅ 玩家位置更新消息（客户端 → 服务器）
/// </summary>
[System.Serializable]
public class PlayerUpdateMessage
{
    public string type;      // "PlayerUpdate"
    public string playerId;  // 玩家ID
    public GSPosition position;  // 位置
    public GSRotation rotation;  // 旋转
    public long timestamp;   // 时间戳

}
