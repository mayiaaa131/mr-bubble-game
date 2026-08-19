using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 改进版 RoomTeamManager - 与 Team JSON 同步
/// </summary>
public class RoomTeamManager : MonoBehaviour
{
    public List<TeamAssignManager.TeamPlayerInfo> redTeam = new();
    public List<TeamAssignManager.TeamPlayerInfo> blueTeam = new();
    public int maxPerTeam = 2;

    private string _roomId;  // ★ 新增：房间ID
    private RoomTeamsData _teamData;  // ★ 新增：缓存 Team JSON 数据

    /// <summary>
    /// ★ 改进：从 Team JSON 初始化队伍
    /// </summary>
    public void Init( string roomId )
    {
        this._roomId = roomId;

        // ★ 关键：从 JSON 加载队伍数据
        LoadTeamsFromJson();

        Debug.Log($"[RoomTeamManager] 初始化完成 - 房间ID: {roomId}");
        Debug.Log($"  红队: {redTeam.Count}人");
        Debug.Log($"  蓝队: {blueTeam.Count}人");
    }

    /// <summary>
    /// ★ 新增：从 Team JSON 加载队伍数据
    /// </summary>
    private void LoadTeamsFromJson( )
    {
        redTeam.Clear();
        blueTeam.Clear();

        // 加载 JSON 数据
        _teamData = TeamJsonFileHandler.Instance.LoadTeamsData(_roomId);
        if (_teamData == null || _teamData.teams == null)
        {
            Debug.LogWarning($"[RoomTeamManager] 未找到房间 {_roomId} 的 Team 数据，使用空队伍");
            return;
        }

        // ★ 从 JSON 中恢复队伍玩家
        foreach (TeamInfo teamInfo in _teamData.teams)
        {
            List<TeamAssignManager.TeamPlayerInfo> targetTeam =
                (teamInfo.teamId.Contains("Red") || teamInfo.teamId.Contains("red"))
                ? redTeam
                : blueTeam;

            foreach (TeamPlayer player in teamInfo.players)
            {
                targetTeam.Add(new TeamAssignManager.TeamPlayerInfo
                {
                    playerId = player.playerId,
                    playerName = player.playerName,
                    team = teamInfo.teamId.Contains("Red") ? "red" : "blue",
                    killCount = player.killCount,
                    deathCount = player.deathCount,
                    assistCount = player.assistCount,
                    currentScore = player.currentScore
                });
            }
        }

        Debug.Log($"✓ 从 Team JSON 加载队伍: 红队{redTeam.Count}人，蓝队{blueTeam.Count}人");
    }

    /// <summary>
    /// ★ 改进：添加玩家并同步到 JSON
    /// </summary>
    public string AutoAssignTeam( string playerId, string playerName )
    {
        bool blueFull = IsTeamFull("blue");
        bool redFull = IsTeamFull("red");

        if (blueFull && redFull)
        {
            Debug.LogWarning($"[RoomTeamManager] 两队均满");
            return "";
        }

        string teamId = (!blueFull && (redFull || blueTeam.Count <= redTeam.Count))
            ? "blue"
            : "red";

        var list = teamId == "red" ? redTeam : blueTeam;
        list.Add(new TeamAssignManager.TeamPlayerInfo
        {
            playerId = playerId,
            playerName = playerName,
            team = teamId
        });

        // ★ 关键：同步到 JSON
        SyncToJson();

        Debug.Log($"✓ 玩家 {playerName} 分配到 {teamId} 队（蓝{blueTeam.Count}/{maxPerTeam} 红{redTeam.Count}/{maxPerTeam}）");
        return teamId;
    }

    /// <summary>
    /// ★ 改进：移除玩家并同步到 JSON
    /// </summary>
    public void RemovePlayer( string playerId )
    {
        redTeam.RemoveAll(p => p.playerId == playerId);
        blueTeam.RemoveAll(p => p.playerId == playerId);

        // ★ 关键：同步到 JSON
        SyncToJson();

        Debug.Log($"✓ 玩家 {playerId} 已移除");
    }

    /// <summary>
    /// ★ 新增：同步所有队伍数据到 JSON
    /// </summary>
    private void SyncToJson( )
    {
        if (_teamData == null || string.IsNullOrEmpty(_roomId))
            return;

        // 更新红队
        TeamInfo redTeamInfo = _teamData.teams.Find(t => t.teamId.Contains("Red") || t.teamId.Contains("red"));
        if (redTeamInfo != null)
        {
            redTeamInfo.players.Clear();
            foreach (var player in redTeam)
            {
                redTeamInfo.players.Add(new TeamPlayer(player.playerId, player.playerName));
            }
            redTeamInfo.alivePlayerCount = redTeam.Count;
        }

        // 更新蓝队
        TeamInfo blueTeamInfo = _teamData.teams.Find(t => t.teamId.Contains("Blue") || t.teamId.Contains("blue"));
        if (blueTeamInfo != null)
        {
            blueTeamInfo.players.Clear();
            foreach (var player in blueTeam)
            {
                blueTeamInfo.players.Add(new TeamPlayer(player.playerId, player.playerName));
            }
            blueTeamInfo.alivePlayerCount = blueTeam.Count;
        }

        // 保存到 JSON
        TeamJsonFileHandler.Instance.SaveTeamsData(_roomId, _teamData);
        Debug.Log($"✓ Team JSON 已同步: 红队{redTeam.Count}人，蓝队{blueTeam.Count}人");
    }

    public bool IsTeamFull( string teamId )
        => (teamId == "red" ? redTeam : blueTeam).Count >= maxPerTeam;

    public string GetPlayerTeam( string playerId )
    {
        if (redTeam.Any(p => p.playerId == playerId)) return "red";
        if (blueTeam.Any(p => p.playerId == playerId)) return "blue";
        return "";
    }
}
