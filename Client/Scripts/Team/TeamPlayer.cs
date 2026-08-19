using System;
using System.Collections.Generic;

/// <summary>
/// 玩家数据结构（用于Team JSON）
/// </summary>
[Serializable]
public class TeamPlayer
{
    public string playerId;           // 玩家ID
    public string playerName;         // 玩家名称（可选，便于调试）
    public int currentBlood;          // 当前血量
    public int killCount;             // 杀敌数
    public int deathCount;            // 死亡数
    public int assistCount;           // 助攻数
    public int currentScore;          // 当前得分

    public TeamPlayer()
    {
        // 默认构造函数
        currentBlood = 6;
        killCount = 0;
        deathCount = 0;
        assistCount = 0;
        currentScore = 0;
    }

    public TeamPlayer(string id, string name = "")
    {
        playerId = id;
        playerName = name;
        currentBlood = 6;
        killCount = 0;
        deathCount = 0;
        assistCount = 0;
        currentScore = 0;
    }
}
