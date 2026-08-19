using System;
using System.Collections.Generic;

/// <summary>
/// 游戏状态中的玩家数据（包含血量和积分）
/// </summary>
[Serializable]
public class GameStatePlayerWithStats : GameStatePlayer
{
    public int currentBlood;          // 当前血量
    public int killCount;             // 杀敌数
    public int deathCount;            // 死亡数
    public int assistCount;           // 助攻数
    public int currentScore;          // 当前得分

    public GameStatePlayerWithStats() { }

    public GameStatePlayerWithStats(string id, string name) : base(id, name)
    {
        currentBlood = 6;
        killCount = 0;
        deathCount = 0;
        assistCount = 0;
        currentScore = 0;
    }

    public GameStatePlayerWithStats(TeamPlayer teamPlayer) : base(teamPlayer.playerId, teamPlayer.playerName)
    {
        this.currentBlood = teamPlayer.currentBlood;
        this.killCount = teamPlayer.killCount;
        this.deathCount = teamPlayer.deathCount;
        this.assistCount = teamPlayer.assistCount;
        this.currentScore = teamPlayer.currentScore;
        this.position = new GSPosition();
        this.rotation = new GSRotation();
    }
}

/// <summary>
/// 游戏状态中的队伍数据（包含玩家完整信息）
/// </summary>
[Serializable]
public class GameStateTeamWithStats
{
    public string roomId;
    public string teamId;
    public string teamName;
    public int totalScore;            // 队伍总得分
    public int alivePlayerCount;      // 存活人数
    public List<GameStatePlayerWithStats> players;

    public GameStateTeamWithStats()
    {
        players = new List<GameStatePlayerWithStats>();
    }

    public GameStateTeamWithStats(string roomId, string teamId, string teamName)
    {
        this.roomId = roomId;
        this.teamId = teamId;
        this.teamName = teamName;
        this.totalScore = 0;
        this.alivePlayerCount = 0;
        players = new List<GameStatePlayerWithStats>();
    }
}

/// <summary>
/// 完整的游戏世界状态数据（包含血量和积分）
/// </summary>
[Serializable]
public class GameStateDataWithStats
{
    public string roomId;
    public long timestamp;
    public List<GameStateTeamWithStats> teams;

    public GameStateDataWithStats()
    {
        teams = new List<GameStateTeamWithStats>();
    }

    public GameStateDataWithStats(string roomId)
    {
        this.roomId = roomId;
        this.timestamp = DateTime.Now.Ticks;
        teams = new List<GameStateTeamWithStats>();
    }
}

