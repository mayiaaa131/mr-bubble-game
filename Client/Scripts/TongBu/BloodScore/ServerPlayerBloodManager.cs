using UnityEngine;
using System;
using System.Collections.Generic;
using System.Collections;

/// <summary>
/// 房间专用的玩家血量管理器（非单例版本）
/// ★ 改造核心：
/// 1. 删除 public static Instance
/// 2. 通过 public 字段注入 GameStateJsonWriter 和 PlayerBloodJsonWriter
/// 3. 所有引用改为通过注入的字段调用
/// </summary>
public class ServerPlayerBloodManager : MonoBehaviour
{
    private string roomId = "";
    private Dictionary<string, (int currentBlood, int maxBlood)> playerBloodData =
        new Dictionary<string, (int, int)>();

    // ★ 关键改造：记录每个玩家最后的伤害来源
    private Dictionary<string, string> lastDamagerMap = new Dictionary<string, string>();

    private int maxPlayerHealth = 6;
    private bool isInitialized = false;
    private Coroutine initCoroutine;

    // ★ 关键改造：通过 public 字段注入依赖
    [HideInInspector] public GameStateJsonWriter gameStateWriter;


    /// <summary>
    /// 由 RoomGameManager 调用，注入依赖
    /// </summary>
    public void InjectDependencies(GameStateJsonWriter gameState)
    {
        gameStateWriter = gameState;
        Debug.Log($"[ServerPlayerBloodManager-{roomId}] ✅ 依赖注入完成");
    }

    /// <summary>
    /// 初始化Manager（由RoomGameManager调用）
    /// </summary>
    public void Initialize(string roomId)
    {
        if (isInitialized) return;

        this.roomId = roomId;
        Debug.Log($"[ServerPlayerBloodManager-{roomId}] 初始化中...");

        // 加载最大血量配置
        LoadMaxPlayerHealthFromRoom(roomId);

        // 启动异步初始化协程
        if (initCoroutine != null)
            StopCoroutine(initCoroutine);
        initCoroutine = StartCoroutine(WaitForInitializationReady());

        Debug.Log($"[ServerPlayerBloodManager-{roomId}] ✅ 初始化启动");
    }

    /// <summary>
    /// 从房间JSON读取最大血量设置
    /// </summary>
    private void LoadMaxPlayerHealthFromRoom(string roomId)
    {
        try
        {
            Room room = RoomDataManager.Instance.GetRoomById(roomId);

            if (room != null && room.maxPlayerHealth > 0)
            {
                maxPlayerHealth = room.maxPlayerHealth;
                Debug.Log($"[ServerPlayerBloodManager-{roomId}] ✅ 从Room.json加载最大血量: {maxPlayerHealth}");
            }
            else
            {
                Debug.LogWarning($"[ServerPlayerBloodManager-{roomId}] ⚠️ 无法读取最大血量，使用默认值: 6");
                maxPlayerHealth = 6;
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[ServerPlayerBloodManager-{roomId}] ❌ 读取最大血量失败: {e.Message}，使用默认值: 6");
            maxPlayerHealth = 6;
        }
    }

    /// <summary>
    /// 协程：轮询等待所有系统就绪
    /// ★ 改造：使用注入的 gameStateWriter，不是单例
    /// </summary>
    private IEnumerator WaitForInitializationReady()
    {
        Debug.Log($"[ServerPlayerBloodManager-{roomId}] 🟡 开始等待系统就绪...");

        int checkCount = 0;
        float maxWaitTime = 60f;
        float elapsedTime = 0f;

        while (elapsedTime < maxWaitTime)
        {
            checkCount++;

            // 检查 1：GameStateJsonWriter 是否存在
            if (gameStateWriter == null)
            {
                yield return new WaitForSeconds(0.3f);
                elapsedTime += 0.3f;
                continue;
            }

            // 检查 2：GameState 是否已初始化
            GameStateData gameState = gameStateWriter.GetCurrentGameState();
            if (gameState == null)
            {
                yield return new WaitForSeconds(0.3f);
                elapsedTime += 0.3f;
                continue;
            }

            // 检查 3：teams 列表是否有数据
            if (gameState.teams == null || gameState.teams.Count == 0)
            {
                yield return new WaitForSeconds(0.3f);
                elapsedTime += 0.3f;
                continue;
            }

            // 检查 4：teams 中是否有 player 数据
            bool hasPlayers = false;
            foreach (var team in gameState.teams)
            {
                if (team.players != null && team.players.Count > 0)
                {
                    hasPlayers = true;
                    break;
                }
            }

            if (!hasPlayers)
            {
                yield return new WaitForSeconds(0.3f);
                elapsedTime += 0.3f;
                continue;
            }

            // ✅ 一切就绪！开始初始化
            InitializePlayerBlood();
            isInitialized = true;
            break;
        }

        if (elapsedTime >= maxWaitTime)
        {
            Debug.LogError($"[ServerPlayerBloodManager-{roomId}] ❌ 初始化超时（60秒）");
        }

    }

    /// <summary>
    /// 初始化玩家血量数据
    /// </summary>
    public void InitializePlayerBlood()
    {
        playerBloodData.Clear();
        lastDamagerMap.Clear();

        if (string.IsNullOrEmpty(roomId))
        {
            Debug.LogError($"[ServerPlayerBloodManager] ❌ 房间ID为空");
            return;
        }

        // 步骤1：从Room JSON读取maxPlayerHealth
        Room roomData = RoomDataManager.Instance.GetRoomById(roomId);
        int playerMaxHealth = roomData != null ? roomData.maxPlayerHealth : 6;

        Debug.Log($"[ServerPlayerBloodManager-{roomId}] ✅ 从Room.json读取maxPlayerHealth: {playerMaxHealth}");

        // 步骤2：从Team JSON读取玩家信息
        RoomTeamsData teamData = TeamJsonFileHandler.Instance.LoadTeamsData(roomId);

        if (teamData == null || teamData.teams == null || teamData.teams.Count == 0)
        {
            Debug.LogError($"[ServerPlayerBloodManager-{roomId}] ❌ 无法读取Team JSON数据");
            return;
        }

        int totalPlayers = 0;

        // 步骤3：遍历Team JSON中的所有队伍和玩家
        foreach (TeamInfo team in teamData.teams)
        {
            Debug.Log($"[ServerPlayerBloodManager-{roomId}] 📋 处理队伍 {team.teamName}");

            if (team.players == null || team.players.Count == 0)
            {
                Debug.LogWarning($"[ServerPlayerBloodManager-{roomId}] ⚠️ 队伍 {team.teamName} 没有玩家");
                continue;
            }

            foreach (TeamPlayer player in team.players)
            {
                playerBloodData[player.playerId] = (playerMaxHealth, playerMaxHealth);
                lastDamagerMap[player.playerId] = null;
                totalPlayers++;

                //Debug.Log($"[ServerPlayerBloodManager-{roomId}]   ✓ 玩家 {player.playerId} ({player.playerName}): {playerMaxHealth}/{playerMaxHealth} HP");
            }
        }

        isInitialized = true;
        Debug.Log($"[ServerPlayerBloodManager-{roomId}] ✅ 血量初始化完成，共 {totalPlayers} 个玩家");

        // ★ 关键：初始化后立即保存到JSON  
        SavePlayerBloodToJson();
    }

    /// <summary>
    /// 对玩家造成伤害
    /// </summary>
    public void DealDamageToPlayer(string playerId, int damage, string killerPlayerId = null)
    {
        if (!isInitialized)
        {
            Debug.LogWarning($"[ServerPlayerBloodManager-{roomId}]血量系统未初始化");
            InitializePlayerBlood();
            isInitialized = true;
        }

        if (!playerBloodData.TryGetValue(playerId, out var bloodInfo))
        {
            Debug.LogError($"[ServerPlayerBloodManager-{roomId}]玩家 {playerId} 不存在");
            return;
        }

        int lastBlood = bloodInfo.currentBlood;
        int newBlood = Mathf.Max(0, bloodInfo.currentBlood - damage);
        playerBloodData[playerId] = (newBlood, bloodInfo.maxBlood);

        // 记录伤害者（只在真正导致死亡时）
        if (damage > 0 && newBlood <= 0 && lastBlood > 0 && !string.IsNullOrEmpty(killerPlayerId))
        {
            lastDamagerMap[playerId] = killerPlayerId;
        }

        Debug.Log($"[ServerPlayerBloodManager-{roomId}]玩家 {playerId} 受伤: {lastBlood} → {newBlood}");

        // 保存到JSON
        SavePlayerBloodToJson();
    }

    /// <summary>
    /// 恢复玩家血量
    /// </summary>
    public void RestorePlayerBlood(string playerId, int restoreAmount)
    {
        if (!playerBloodData.TryGetValue(playerId, out var bloodInfo))
        {
            Debug.LogError($"[ServerPlayerBloodManager-{roomId}]玩家 {playerId} 不存在");
            return;
        }

        int newBlood = Mathf.Min(bloodInfo.currentBlood + restoreAmount, bloodInfo.maxBlood);
        playerBloodData[playerId] = (newBlood, bloodInfo.maxBlood);

        Debug.Log($"[ServerPlayerBloodManager-{roomId}]玩家 {playerId} 恢复血量: {bloodInfo.currentBlood} → {newBlood}");

        SavePlayerBloodToJson();
    }

    /// <summary>
    /// 保存玩家血量到JSON
    /// ★ 改造：使用注入的 bloodJsonWriter，不是单例
    /// </summary>
    private void SavePlayerBloodToJson()
    {
        PlayersBloodMessage msg = GeneratePlayersBloodMessage();

        // ✅ 改为：直接访问单例，就像 BombManager 一样
        if (msg != null && PlayerBloodJsonWriter.Instance != null)
        {
            PlayerBloodJsonWriter.Instance.SavePlayerBloodToFile(roomId, msg);
        }
        else
        {
            Debug.LogError($"[ServerPlayerBloodManager-{roomId}] ❌ 保存失败: Instance is null");
        }
    }



    /// <summary>
    /// 生成玩家血量广播消息
    /// </summary>
    public PlayersBloodMessage GeneratePlayersBloodMessage()
    {
        long serverTime = System.DateTime.Now.Ticks / 10000;

        PlayersBloodMessage msg = new PlayersBloodMessage
        {
            type = "PlayersBlood",
            roomId = roomId,
            timestamp = serverTime
        };

        if (string.IsNullOrEmpty(roomId))
        {
            Debug.LogWarning($"[ServerPlayerBloodManager] ⚠️ 房间ID为空");
            return msg;
        }

        // 步骤1：从Room JSON读取maxPlayerHealth
        Room roomData = RoomDataManager.Instance.GetRoomById(roomId);
        int defaultMaxBlood = roomData != null ? roomData.maxPlayerHealth : 6;

        // 步骤2：从Team JSON读取队伍和玩家结构
        RoomTeamsData teamData = TeamJsonFileHandler.Instance.LoadTeamsData(roomId);

        if (teamData == null || teamData.teams == null || teamData.teams.Count == 0)
        {
            Debug.LogWarning($"[ServerPlayerBloodManager-{roomId}] ⚠️ 无法读取Team JSON数据");
            return msg;
        }

        // 步骤3：遍历Team JSON构建消息
        foreach (TeamInfo team in teamData.teams)
        {
            TeamBloodInfo teamBloodInfo = new TeamBloodInfo(team.teamId, team.teamName);

            if (team.players != null && team.players.Count > 0)
            {
                foreach (TeamPlayer player in team.players)
                {
                    int currentBlood;
                    int maxBlood = defaultMaxBlood;

                    if (playerBloodData.TryGetValue(player.playerId, out var bloodInfo))
                    {
                        currentBlood = bloodInfo.currentBlood;
                        maxBlood = bloodInfo.maxBlood;
                    }
                    else
                    {
                        currentBlood = defaultMaxBlood;
                    }

                    PlayerBloodInfo playerBloodInfo = new PlayerBloodInfo(
                        player.playerId,
                        player.playerName,
                        currentBlood,
                        maxBlood
                    );

                    teamBloodInfo.players.Add(playerBloodInfo);

                    //Debug.Log($"[ServerPlayerBloodManager-{roomId}]   → {player.playerName}: {currentBlood}/{maxBlood} HP");
                }

            }

            if (teamBloodInfo.players.Count > 0)
            {
                msg.teams.Add(teamBloodInfo);
            }
        }

        return msg;
    }

    /// <summary>
    /// 获取玩家当前血量
    /// </summary>
    public int GetPlayerBlood(string playerId)
    {
        if (playerBloodData.TryGetValue(playerId, out var bloodInfo))
            return bloodInfo.currentBlood;

        Debug.LogWarning($"[ServerPlayerBloodManager-{roomId}] ⚠️ 无法获取玩家 {playerId} 的血量");
        return maxPlayerHealth;
    }

    /// <summary>
    /// 复活单个玩家（恢复血量到最大值）
    /// </summary>
    public void RevivePlayer(string playerId)
    {
        if (!playerBloodData.TryGetValue(playerId, out var bloodInfo))
        {
            Debug.LogError($"[ServerPlayerBloodManager-{roomId}] ❌ 玩家 {playerId} 不存在");
            return;
        }

        playerBloodData[playerId] = (bloodInfo.maxBlood, bloodInfo.maxBlood);
        ClearDamagerRecord(playerId);

        Debug.Log($"[ServerPlayerBloodManager-{roomId}] ✅ 玩家 {playerId} 已复活，血量: {bloodInfo.maxBlood}/{bloodInfo.maxBlood}");

        SavePlayerBloodToJson();
    }

    /// <summary>
    /// 重置所有玩家血量
    /// </summary>
    public void ResetAllPlayersBlood()
    {
        foreach (var playerId in new List<string>(playerBloodData.Keys))
        {
            var bloodInfo = playerBloodData[playerId];
            playerBloodData[playerId] = (bloodInfo.maxBlood, bloodInfo.maxBlood);
        }

        Debug.Log($"[ServerPlayerBloodManager-{roomId}] ✅ 所有玩家血量已重置");

        SavePlayerBloodToJson();
    }

    /// <summary>
    /// 获取对某玩家造成最后一次伤害的玩家ID
    /// </summary>
    public string GetLastDamager(string playerId)
    {
        if (lastDamagerMap.TryGetValue(playerId, out var damager))
        {
            lastDamagerMap.Remove(playerId);
            return damager;
        }
        return null;
    }

    /// <summary>
    /// 清除某玩家的伤害记录
    /// </summary>
    public void ClearDamagerRecord(string playerId)
    {
        if (lastDamagerMap.ContainsKey(playerId))
        {
            lastDamagerMap.Remove(playerId);
            Debug.Log($"[ServerPlayerBloodManager-{roomId}] ✓ 已清除 {playerId} 的伤害者记录");
        }
    }

    /// <summary>
    /// 获取所有玩家血量
    /// </summary>
    public Dictionary<string, (int, int)> GetAllPlayersBlood() => playerBloodData;

    /// <summary>
    /// 清理数据
    /// </summary>
    public void Cleanup()
    {
        Debug.Log($"[ServerPlayerBloodManager-{roomId}] → 开始清理...");

        try
        {
            if (initCoroutine != null)
            {
                StopCoroutine(initCoroutine);
                initCoroutine = null;
            }

            playerBloodData.Clear();
            lastDamagerMap.Clear();
            isInitialized = false;
            gameStateWriter = null;

            Debug.Log($"[ServerPlayerBloodManager-{roomId}] ✅ 已清理");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ServerPlayerBloodManager-{roomId}] ❌ 清理失败: {ex.Message}");
        }
    }

    private void OnDestroy()
    {
        if (initCoroutine != null)
            StopCoroutine(initCoroutine);
    }
}
