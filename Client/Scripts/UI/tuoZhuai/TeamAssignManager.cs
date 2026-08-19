using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public class TeamAssignManager : MonoBehaviour
{
    public static TeamAssignManager Instance;

    [System.Serializable]
    public class TeamPlayerInfo
    {
        public string playerId;
        public string playerName;
        public string team;
        public int killCount = 0;
        public int deathCount = 0;
        public int assistCount = 0;
        public int currentScore = 100;
    }

    [Header("队伍数据")]
    public List<TeamPlayerInfo> redTeamPlayers = new List<TeamPlayerInfo>();
    public List<TeamPlayerInfo> blueTeamPlayers = new List<TeamPlayerInfo>();

    [Header("每队人数上限")]
    public int maxPerTeam = 3;

    [Header("UI 人数显示")]
    public Text redCountText;
    public Text blueCountText;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    // ────────────────────────────────────────
    // ★ 新增：获取新玩家应该分配的队伍
    // 优先红队，红队满则蓝队，都满则返回空
    // ────────────────────────────────────────
    public string GetTargetTeamForNewPlayer()
    {
        // 优先检查红队
        if (!IsTeamFull("red"))
        {
            Debug.Log($"[GetTargetTeamForNewPlayer] 红队未满 → 返回 \"red\" (当前: {redTeamPlayers.Count}/{maxPerTeam})");
            return "red";
        }

        // 红队满了，检查蓝队
        if (!IsTeamFull("blue"))
        {
            Debug.Log($"[GetTargetTeamForNewPlayer] 红队已满，蓝队未满 → 返回 \"blue\" (当前: {blueTeamPlayers.Count}/{maxPerTeam})");
            return "blue";
        }

        // 都满了
        Debug.LogWarning($"❌ [GetTargetTeamForNewPlayer] 两队都已满 → 返回空字符串");
        return "";
    }

    // ────────────────────────────────────────
    // 普通分配（目标队未满时）
    // ────────────────────────────────────────
    public bool AssignPlayerToTeam(string playerId, string playerName, string teamId)
    {
        // 必须先移除，再判断目标队是否满
        // 防止同一玩家换队时，自己还占着来源队名额导致计数错误
        RemovePlayerFromAllTeams(playerId);

        if (IsTeamFull(teamId))
        {
            Debug.LogWarning($"{teamId} 队已满，无法分配 {playerName}");
            return false;
        }

        GetTeam(teamId).Add(new TeamPlayerInfo
        {
            playerId = playerId,
            playerName = playerName,
            team = teamId
        });

        Debug.Log($"玩家 {playerName} 已分配到 {teamId} 队");
        RefreshCountUI();
        PrintTeamStatus();
        return true;
    }

    // ────────────────────────────────────────
    // 互换逻辑
    // ────────────────────────────────────────
    public string SwapPlayerToTeam(string draggedId, string draggedName, string targetTeam)
    {
        string sourceTeam = targetTeam == "red" ? "blue" : "red";
        List<TeamPlayerInfo> targetList = GetTeam(targetTeam);
        List<TeamPlayerInfo> sourceList = GetTeam(sourceTeam);

        // ★ 先从来源队移除拖拽玩家
        sourceList.RemoveAll(p => p.playerId == draggedId);

        // 取出目标队最后一个玩家
        TeamPlayerInfo kickedPlayer = targetList[targetList.Count - 1];
        string kickedId = kickedPlayer.playerId;

        // 从目标队移除最后一个
        targetList.RemoveAt(targetList.Count - 1);

        // 拖拽玩家加入目标队
        targetList.Add(new TeamPlayerInfo
        {
            playerId = draggedId,
            playerName = draggedName,
            team = targetTeam
        });

        // 被踢出玩家加入来源队
        kickedPlayer.team = sourceTeam;
        sourceList.Add(kickedPlayer);

        Debug.Log($"✓ 互换完成: {draggedName} → {targetTeam} 队, {kickedPlayer.playerName} → {sourceTeam} 队");
        RefreshCountUI();
        PrintTeamStatus();
        return kickedId;
    }

    // ────────────────────────────────────────
    // 从两队移除某玩家
    // ────────────────────────────────────────
    public void RemovePlayerFromAllTeams(string playerId)
    {
        redTeamPlayers.RemoveAll(p => p.playerId == playerId);
        blueTeamPlayers.RemoveAll(p => p.playerId == playerId);
    }

    public bool IsTeamFull(string teamId)
    {
        return GetTeam(teamId).Count >= maxPerTeam;
    }

    public List<TeamPlayerInfo> GetTeam(string teamId)
    {
        return teamId == "red" ? redTeamPlayers : blueTeamPlayers;
    }

    public string GetPlayerTeam(string playerId)
    {
        if (redTeamPlayers.Any(p => p.playerId == playerId)) return "red";
        if (blueTeamPlayers.Any(p => p.playerId == playerId)) return "blue";
        return "";
    }

    private void RefreshCountUI()
    {
        if (redCountText != null)
            redCountText.text = $"红队 {redTeamPlayers.Count}/{maxPerTeam}";
        if (blueCountText != null)
            blueCountText.text = $"蓝队 {blueTeamPlayers.Count}/{maxPerTeam}";
    }

    private void PrintTeamStatus()
    {
        Debug.Log("── 红队 ──");
        redTeamPlayers.ForEach(p => Debug.Log($"  {p.playerName}"));
        Debug.Log("── 蓝队 ──");
        blueTeamPlayers.ForEach(p => Debug.Log($"  {p.playerName}"));
    }

    public TeamData ExportTeamData(string teamId)
    {
        var players = GetTeam(teamId);
        return new TeamData
        {
            teamId = teamId,
            teamName = teamId == "red" ? "红队" : "蓝队",
            totalScore = players.Sum(p => p.currentScore),
            alivePlayerCount = players.Count
        };
    }

    /// <summary>
    /// 清空所有队伍数据（用于重置）
    /// </summary>
    public void ClearAllTeams()
    {
        redTeamPlayers.Clear();
        blueTeamPlayers.Clear();
        RefreshCountUI();
    }
}