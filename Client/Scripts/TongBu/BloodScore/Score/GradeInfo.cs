using System;
using System.Collections.Generic;

/// <summary>
/// 玩家积分信息
/// </summary>
[Serializable]
public class PlayerGradeInfo
{
    public string playerId;
    public string playerName;
    public int finalKills;      // 击杀数
    public int assists;         // 助攻数
    public int deaths;          // 死亡数
    public int score;           // ★ 新增：个人得分
}

/// <summary>
/// 队伍积分信息
/// </summary>
[Serializable]
public class TeamGradeInfo
{
    public string teamId;
    public string teamName;
    public List<PlayerGradeInfo> players;
    public int totalScore;      // ★ 新增：队伍总分

    public TeamGradeInfo()
    {
        players = new List<PlayerGradeInfo>();
        totalScore = 0;
    }

    public TeamGradeInfo(string teamId, string teamName)
    {
        this.teamId = teamId;
        this.teamName = teamName;
        this.players = new List<PlayerGradeInfo>();
        this.totalScore = 0;
    }
}

/// <summary>
/// 玩家积分广播消息
/// 服务端 → 所有客户端
/// type = "Grade"
/// </summary>
[Serializable]
public class GradeMessage
{
    public string type;                 // "Grade"
    public string roomId;               // 房间ID
    public long timestamp;              // 消息时刻戳
    public List<TeamGradeInfo> teams;   // 所有队伍和玩家积分信息

    public GradeMessage()
    {
        teams = new List<TeamGradeInfo>();
    }
}
