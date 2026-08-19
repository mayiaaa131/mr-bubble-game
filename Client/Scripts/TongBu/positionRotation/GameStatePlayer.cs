using System;
using System.Collections.Generic;

[Serializable]
public class GameStatePlayer
{
    public string playerId;
    public string playerName;
    public GSPosition position;
    public GSRotation rotation;

    public GameStatePlayer() { }

    public GameStatePlayer(string id, string name)
    {
        playerId = id;
        playerName = name;
        position = new GSPosition();
        rotation = new GSRotation();
    }
}

[Serializable]
public class GSPosition
{
    public float x;
    public float y;
    public float z;

    public GSPosition() { x = 0; y = 0; z = 0; }
    public GSPosition(float x, float y, float z)
    {
        this.x = x; this.y = y; this.z = z;
    }
}

[Serializable]
public class GSRotation
{
    public float x;
    public float y;
    public float z;
    public float w;

    public GSRotation() { x = 0; y = 0; z = 0; w = 1; }
    public GSRotation(float x, float y, float z, float w)
    {
        this.x = x; this.y = y; this.z = z; this.w = w;
    }
}

[Serializable]
public class GameStateTeam
{
    public string teamId;
    public string teamName;
    public List<GameStatePlayer> players;

    public GameStateTeam()
    {
        players = new List<GameStatePlayer>();
    }

    public GameStateTeam(string teamId, string teamName)
    {
        this.teamId = teamId;
        this.teamName = teamName;
        players = new List<GameStatePlayer>();
    }
}

/// <summary>
/// 游戏状态整体数据（WorldState 消息格式）
/// ✅ 改正：
/// - type 字段改为 "WorldState"
/// - timestamp 改为 long 类型
/// - 移除 roomId 字段（只在顶层有）
/// </summary>
[Serializable]
public class GameStateData
{
    public string type = "WorldState";   // ✅ 改为 "WorldState"
    public string roomId;
    public long timestamp;              // ✅ 改为 long
    public List<GameStateTeam> teams;

    public GameStateData()
    {
        teams = new List<GameStateTeam>();
    }

    public GameStateData(string roomId)
    {
        this.type = "WorldState";
        this.roomId = roomId;
        this.timestamp = System.DateTime.Now.Ticks;  // ✅ 使用 Ticks
        teams = new List<GameStateTeam>();
    }
}
