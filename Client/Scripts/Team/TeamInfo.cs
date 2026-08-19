using System;
using System.Collections.Generic;

/// <summary>
/// 单个队伍的数据结构
/// </summary>
[Serializable]
public class TeamInfo
{
    public string roomId;             // 房间ID
    public string teamId;             // 队伍ID（如 "RedTeam_1"）
    public string teamName;           // 队伍名称（如 "红队"）
    public int totalScore;            // 队伍总得分
    public int alivePlayerCount;      // 存活人数
    public List<TeamPlayer> players;  // 队伍中的玩家列表

    public TeamInfo()
    {
        players = new List<TeamPlayer>();
        totalScore = 0;
        alivePlayerCount = 0;
    }

    public TeamInfo(string roomId, string teamId, string teamName)
    {
        this.roomId = roomId;
        this.teamId = teamId;
        this.teamName = teamName;
        this.players = new List<TeamPlayer>();
        this.totalScore = 0;
        this.alivePlayerCount = 0;
    }
}
