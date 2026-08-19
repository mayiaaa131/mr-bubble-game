using System;

/// <summary>
/// 房间数据结构（扩展版 - 添加分数计算系数）
/// </summary>
[Serializable]
public class Room
{
    public string roomId;           // 房间id
    public string roomName;         // 房间名称
    public string mapId;            // 地图id
    public int maxPlayers;          // 最多玩家人数
    public int currentPlayers;      // 已有玩家人数
    public string state;            // 状态（playing：正在游戏，waiting：等待游戏开始）
    public string gameId;           // 游戏ID（用于统计结果）
    public int countdownSeconds = 300;    // 对战倒计时（秒数）
    public int startTime;           // 游戏开始时间
    public int remainingTime;       // 剩余时间
    public string teamRedId;        // 红方队伍ID
    public string teamBlueId;       // 蓝方队伍ID

    // ★ 游戏配置字段  
    public int scoreCoefficient = 10;        // 积分系数 [-100, 100]  
    public int maxPlayerHealth = 100;        // 玩家最大血量 [6, 100]  
    public string gameMode = "single";       // 游玩局数

    // ★ 房间级别的游戏配置
    [Serializable]
    public class RoomSettings
    {
        public string gameMode = "single";      // "single" / "best3" / "best5"
        public int playerDefaultHp = 6;         // 玩家初始血量（最小为6）
    }

    // ★ 分数计算系数配置
    [Serializable]
    public class ScoreCoefficients
    {
        // 基础分数
        public int baseScore = 100;             // 基础分数（默认100）

        // 计分系数（范围：-100 到 100）
        public int killCoefficient = 10;        // 击杀系数：默认 +10分/次
        public int deathCoefficient = -5;       // 死亡系数：默认 -5分/次
        public int assistCoefficient = 2;       // 辅助系数：默认 +2分/次

        /// <summary>
        /// 计算玩家最终得分
        /// 公式：100 + killCoefficient*K + deathCoefficient*D + assistCoefficient*A
        /// </summary>
        public int CalculateScore( int kills, int deaths, int assists )
        {
            int score = baseScore;
            score += killCoefficient * kills;
            score += deathCoefficient * deaths;  // deathCoefficient 是负数
            score += assistCoefficient * assists;
            return Math.Max(0, score);  // 分数不能为负
        }

        /// <summary>
        /// 验证系数范围
        /// </summary>
        public bool ValidateCoefficients( )
        {
            return killCoefficient >= -100 && killCoefficient <= 100 &&
                   deathCoefficient >= -100 && deathCoefficient <= 100 &&
                   assistCoefficient >= -100 && assistCoefficient <= 100;
        }

        /// <summary>
        /// 重置为默认值
        /// </summary>
        public void ResetToDefault( )
        {
            baseScore = 100;
            killCoefficient = 10;
            deathCoefficient = -5;
            assistCoefficient = 2;
        }
    }

    // ★ 关键：scoreCoefficients 实例（直接可序列化）
    public ScoreCoefficients scoreCoefficients = new ScoreCoefficients();
}
