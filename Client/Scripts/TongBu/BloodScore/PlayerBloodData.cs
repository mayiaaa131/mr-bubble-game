using System;
using System.Collections.Generic;

/// <summary>
/// 玩家血量数据类
/// </summary>
[Serializable]
public class PlayerBloodInfo
{
    public string playerId;
    public string playerName;
    public int blood;
    public int maxBlood;

    public PlayerBloodInfo() { }
    public PlayerBloodInfo(string playerId, string playerName, int blood, int maxBlood)
    {
        this.playerId = playerId;
        this.playerName = playerName;
        this.blood = blood;
        this.maxBlood = maxBlood;
    }
}

/// <summary>
/// 队伍血量数据类
/// </summary>
[Serializable]
public class TeamBloodInfo
{
    public string teamId;
    public string teamName;
    public List<PlayerBloodInfo> players;

    public TeamBloodInfo()
    {
        players = new List<PlayerBloodInfo>();
    }

    public TeamBloodInfo(string teamId, string teamName)
    {
        this.teamId = teamId;
        this.teamName = teamName;
        this.players = new List<PlayerBloodInfo>();
    }
}

/// <summary>
/// 玩家血量广播消息（服务端 → 所有客户端）
/// type = "PlayersBlood"
/// </summary>
[Serializable]
public class PlayersBloodMessage
{
    public string type;                 // "PlayersBlood"
    public string roomId;               // 房间ID
    public long timestamp;              // 消息时刻戳
    public List<TeamBloodInfo> teams;   // 所有队伍和玩家血量信息

    public PlayersBloodMessage()
    {
        teams = new List<TeamBloodInfo>();
    }
}
