using UnityEngine;

/// <summary>
/// 积分计算配置
/// 方便后续调整分数权重
/// </summary>
public class ScoreConfig
{
    // 基础分
    public static int BaseScore = 100;

    // 击杀倍数
    public static int KillMultiplier = 10;

    // 死亡倍数（减分）
    public static int DeathMultiplier = 5;

    // 助攻倍数
    public static int AssistMultiplier = 2;

    /// <summary>  
    /// ★ 新增：从 Room.json 读取指定房间的积分系数  
    /// 传入 roomId，自动读取对应的积分配置  
    /// </summary>  
    public static int CalculatePlayerScore( string roomId, int kills, int assists, int deaths )
    {
        // 从 JSON 加载房间数据  
        Room room = JsonFileHandler.Instance.GetRoomById(roomId);

        if (room == null || room.scoreCoefficients == null)
        {
            Debug.LogWarning($"⚠️ 无法读取房间 {roomId} 的积分系数，使用默认值");
            return CalculatePlayerScore(kills, assists, deaths);  // 回退到静态方法  
        }

        // 使用房间中的动态系数计算  
        int score = room.scoreCoefficients.baseScore;
        score += room.scoreCoefficients.killCoefficient * kills;
        score += room.scoreCoefficients.deathCoefficient * deaths;
        score += room.scoreCoefficients.assistCoefficient * assists;

        return Mathf.Max(0, score);  // 分数不能为负  
    }


    /// <summary>
    /// 计算玩家得分
    /// </summary>
    public static int CalculatePlayerScore(int kills, int assists, int deaths)
    {
        return BaseScore
            + (kills * KillMultiplier)
            - (deaths * DeathMultiplier)
            + (assists * AssistMultiplier);
    }
}
