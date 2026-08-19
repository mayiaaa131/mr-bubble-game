using UnityEngine;
using System;
using System.Collections.Generic;

/// <summary>
/// 房间专用的积分管理器（非单例版本）
/// ★ 改造核心：
/// 1. 删除 public static Instance
/// 2. 通过 public 字段注入依赖（BloodManager）
/// 3. 所有引用改为通过注入的字段调用
/// </summary>
public class ServerGradeManager : MonoBehaviour
{
    private string roomId = "";
    private GradeMessage gameState;
    private long frameCounter = 0;
    private bool isInitialized = false;

    // 积分系数（从Room JSON读取）
    private int baseScore = 60;
    private int killCoefficient = 30;
    private int deathCoefficient = -10;
    private int assistCoefficient = 5;

    // ★ 关键改造：通过 public 字段注入依赖
    [HideInInspector] public ServerPlayerBloodManager bloodManager;

    /// <summary>
    /// 由 RoomGameManager 调用，注入依赖
    /// </summary>
    public void InjectDependencies(ServerPlayerBloodManager blood)
    {
        bloodManager = blood;
        Debug.Log($"[ServerGradeManager-{roomId}] ✅ 依赖注入完成");
    }

    /// <summary>
    /// 初始化Manager（由RoomGameManager调用）
    /// </summary>
    public void Initialize(string roomId)
    {
        if (isInitialized) return;

        this.roomId = roomId;
        Debug.Log($"[ServerGradeManager-{roomId}] 初始化中...");

        // 从 Room JSON 读取积分系数
        LoadScoreCoefficientsFromRoom(roomId);

        // 初始化游戏状态
        InitializeGameState();

        isInitialized = true;
        Debug.Log($"[ServerGradeManager-{roomId}] ✅ 初始化完成");
    }

    /// <summary>
    /// 从Room JSON读取积分系数
    /// </summary>
    private void LoadScoreCoefficientsFromRoom(string roomId)
    {
        try
        {
            Room roomConfig = RoomDataManager.Instance.GetRoomById(roomId);

            if (roomConfig != null && roomConfig.scoreCoefficients != null)
            {
                baseScore = roomConfig.scoreCoefficients.baseScore;
                killCoefficient = roomConfig.scoreCoefficients.killCoefficient;
                deathCoefficient = roomConfig.scoreCoefficients.deathCoefficient;
                assistCoefficient = roomConfig.scoreCoefficients.assistCoefficient != 0
                    ? roomConfig.scoreCoefficients.assistCoefficient : 5;

                Debug.Log($"[ServerGradeManager-{roomId}] ✅ 加载积分系数成功");
            }
            else
            {
                Debug.LogWarning($"[ServerGradeManager-{roomId}] ⚠️ 无法读取积分系数，使用默认值");
                SetDefaultCoefficients();
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ServerGradeManager-{roomId}] ❌ 加载积分系数失败: {ex.Message}");
            SetDefaultCoefficients();
        }
    }

    /// <summary>
    /// 设置默认积分系数
    /// </summary>
    private void SetDefaultCoefficients()
    {
        baseScore = 60;
        killCoefficient = 30;
        deathCoefficient = -10;
        assistCoefficient = 5;
    }

    /// <summary>
    /// 初始化游戏状态
    /// </summary>
    private void InitializeGameState()
    {
        gameState = new GradeMessage
        {
            type = "Grade",
            roomId = roomId,
            timestamp = System.DateTime.Now.Ticks / 10000,
            teams = new List<TeamGradeInfo>()
        };

        // 从 Team JSON 读取队伍和玩家信息
        RoomTeamsData roomTeams = TeamJsonFileHandler.Instance.LoadTeamsData(roomId);

        if (roomTeams == null || roomTeams.teams == null)
        {
            Debug.LogError($"[ServerGradeManager-{roomId}] ❌ 无法读取Team JSON");
            return;
        }

        foreach (TeamInfo team in roomTeams.teams)
        {
            TeamGradeInfo teamGrade = new TeamGradeInfo(team.teamId, team.teamName);

            if (team.players != null)
            {
                int teamBaseScore = team.players.Count * baseScore;

                foreach (TeamPlayer player in team.players)
                {
                    PlayerGradeInfo playerGrade = new PlayerGradeInfo
                    {
                        playerId = player.playerId,
                        playerName = player.playerName,
                        finalKills = 0,
                        assists = 0,
                        deaths = 0,
                        score = baseScore
                    };
                    teamGrade.players.Add(playerGrade);
                }

                teamGrade.totalScore = teamBaseScore;
            }

            gameState.teams.Add(teamGrade);
        }

        SaveGradeToJson();
        Debug.Log($"[ServerGradeManager-{roomId}] ✅ 游戏状态初始化完成");
    }

    /// <summary>
    /// 记录玩家击杀
    /// </summary>
    public void RecordPlayerKill(string killerId, string victimId)
    {
        if (gameState == null) return;

        foreach (var team in gameState.teams)
        {
            var killer = team.players?.Find(p => p.playerId == killerId);
            if (killer != null)
            {
                killer.finalKills++;
                killer.score += killCoefficient;
                team.totalScore += killCoefficient;

                Debug.Log($"[ServerGradeManager-{roomId}] 🎯 {killer.playerName} 击杀 (+{killCoefficient}分)");
                SaveGradeToJson();
                return;
            }
        }
    }

    /// <summary>
    /// 记录玩家死亡
    /// </summary>
    public void RecordPlayerDeath(string victimId, string killerId = null)
    {
        if (gameState == null) return;

        foreach (var team in gameState.teams)
        {
            var victim = team.players?.Find(p => p.playerId == victimId);
            if (victim != null)
            {
                victim.deaths++;
                victim.score += deathCoefficient;
                team.totalScore += deathCoefficient;

                Debug.Log($"[ServerGradeManager-{roomId}] ☠️ {victim.playerName} 死亡 ({deathCoefficient}分)");

                if (!string.IsNullOrEmpty(killerId))
                {
                    RecordPlayerKill(killerId, victimId);
                }

                SaveGradeToJson();
                return;
            }
        }
    }

    /// <summary>
    /// 记录玩家碰撞障碍物扣分
    /// </summary>
    public void RecordObstacleCollision(string playerId, int penaltyScore)
    {
        if (gameState == null) return;

        foreach (var team in gameState.teams)
        {
            var player = team.players?.Find(p => p.playerId == playerId);
            if (player != null)
            {
                player.score += penaltyScore;
                team.totalScore += penaltyScore;

                if (player.score < 0) player.score = 0;
                if (team.totalScore < 0) team.totalScore = 0;

                Debug.Log($"[ServerGradeManager-{roomId}] 碰撞扣分: {player.playerName} {penaltyScore}分");
                SaveGradeToJson();
                return;
            }
        }
    }

    /// <summary>
    /// ★ 关键改造：保存积分到JSON
    /// 使用 GradeJsonWriter.Instance 静态实例来保存
    /// </summary>
    private void SaveGradeToJson()
    {
        if (gameState == null) return;

        gameState.timestamp = System.DateTime.Now.Ticks / 10000;

        if (GradeJsonWriter.Instance != null)
        {
            GradeJsonWriter.Instance.SaveGradeToFile(roomId, gameState);
        }
    }

    /// <summary>
    /// 生成广播消息
    /// </summary>
    public GradeMessage GenerateGradeMessage()
    {
        if (gameState == null) return null;

        gameState.timestamp = System.DateTime.Now.Ticks / 10000;
        return gameState;
    }

    /// <summary>
    /// 重置新局积分
    /// </summary>
    public void ResetGradeForNewRound()
    {
        if (gameState == null)
        {
            Debug.LogWarning($"[ServerGradeManager-{roomId}] ⚠️ gameState 为空，无法重置");
            return;
        }

        Debug.Log($"[ServerGradeManager-{roomId}] 🔄 积分重置中...");

        if (gameState.teams != null)
        {
            foreach (var team in gameState.teams)
            {
                int teamBaseScore = 0;

                if (team.players != null)
                {
                    foreach (var player in team.players)
                    {
                        player.finalKills = 0;
                        player.assists = 0;
                        player.deaths = 0;
                        player.score = baseScore;
                        teamBaseScore += baseScore;
                    }
                }

                team.totalScore = teamBaseScore;
            }
        }

        SaveGradeToJson();
        Debug.Log($"[ServerGradeManager-{roomId}] ✅ 积分已重置");
    }

    /// <summary>
    /// 清理数据
    /// </summary>
    public void Cleanup()
    {
        Debug.Log($"[ServerGradeManager-{roomId}] → 开始清理...");

        try
        {
            if (gameState != null && gameState.teams != null)
            {
                gameState.teams.Clear();
            }

            gameState = null;
            isInitialized = false;
            bloodManager = null;

            Debug.Log($"[ServerGradeManager-{roomId}] ✅ 已清理");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ServerGradeManager-{roomId}] ❌ 清理失败: {ex.Message}");
        }
    }
}
