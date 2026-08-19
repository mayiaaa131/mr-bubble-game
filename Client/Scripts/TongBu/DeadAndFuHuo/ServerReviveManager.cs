using UnityEngine;
using System;
using System.Collections.Generic;
using System.Collections;
using System.Linq;

/// <summary>
/// 房间专用的复活管理器（非单例版本）
/// ★ 改造核心：
/// 1. 删除 public static Instance
/// 2. 通过 public 字段注入依赖（BloodManager）
/// 3. 监听血量变化 → 检测死亡 → 倒计时 → 自动复活
/// </summary>
public class ServerReviveManager : MonoBehaviour
{
    private string roomId = "";
    private Dictionary<string, float> reviveCountdown = new Dictionary<string, float>();
    private Dictionary<string, int> lastFramePlayerBlood = new Dictionary<string, int>();
    private Dictionary<string, bool> playerDeadState = new Dictionary<string, bool>();
    private Dictionary<string, float> invincibleCountdown = new Dictionary<string, float>();
    private Dictionary<string, bool> isInvincibleState = new Dictionary<string, bool>();

    private const float REVIVE_DELAY = 3f;
    private const float INVINCIBLE_DURATION = 5f;
    private bool isInitialized = false;
    private Coroutine initCoroutine;
    // ★ 关键改造：通过 public 字段注入依赖  
    [HideInInspector] public ServerPlayerBloodManager bloodManager;
    [HideInInspector] public GameStateJsonWriter gameStateWriter;  // ← 新增  
    [HideInInspector] public ServerGradeManager gradeManager;  // ← 新增这一行！  


    /// <summary>  
    /// 由 RoomGameManager 调用，注入依赖  
    /// </summary>  
    public void InjectDependencies(ServerPlayerBloodManager blood,
                                   GameStateJsonWriter gameState,
                                   ServerGradeManager grade)  // ← 新增
    {
        bloodManager = blood;
        gameStateWriter = gameState;
        gradeManager = grade;  // ← 新增
        Debug.Log($"[ServerReviveManager-{roomId}] ✅ 依赖注入完成");
    }
    /// <summary>
    /// 初始化Manager（由RoomGameManager调用）
    /// </summary>
    public void Initialize(string roomId)
    {
        if (isInitialized) return;

        this.roomId = roomId;
        Debug.Log($"[ServerReviveManager-{roomId}] 初始化中...");

        if (initCoroutine != null)
            StopCoroutine(initCoroutine);
        initCoroutine = StartCoroutine(RetryInitializeReviveData());

        Debug.Log($"[ServerReviveManager-{roomId}] ✅ 初始化启动");
    }

    /// <summary>
    /// 协程：重试初始化
    /// </summary>
    private IEnumerator RetryInitializeReviveData()
    {
        int retryCount = 0;
        const int MAX_RETRIES = 120;

        while (retryCount < MAX_RETRIES)
        {
            bool shouldRetry = false;

            try
            {
                // ★ 改这里：使用注入的 gameStateWriter，不是单例
                if (gameStateWriter == null)
                {
                    shouldRetry = true;
                }
                else
                {
                    GameStateData gameState = gameStateWriter.GetCurrentGameState();  // ← 改这里

                    if (gameState == null || gameState.teams == null || gameState.teams.Count == 0)
                    {
                        shouldRetry = true;
                    }
                    else
                    {
                        int totalPlayers = 0;
                        foreach (var team in gameState.teams)
                        {
                            if (team.players != null)
                                totalPlayers += team.players.Count;
                        }

                        if (totalPlayers == 0)
                        {
                            shouldRetry = true;
                        }
                        else
                        {
                            if (TryInitializeReviveData())
                            {
                                Debug.Log($"[ServerReviveManager-{roomId}] ✅ 初始化成功");
                                isInitialized = true;
                                yield break;
                            }
                            else
                            {
                                shouldRetry = true;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ServerReviveManager-{roomId}] ❌ 初始化异常: {ex.Message}");
                shouldRetry = true;
            }

            if (shouldRetry)
            {
                retryCount++;
                yield return new WaitForSeconds(1f);
            }
        }

        Debug.LogError($"[ServerReviveManager-{roomId}] ❌ 初始化失败，已重试 120 秒");
    }

    /// <summary>
    /// 尝试初始化复活数据
    /// </summary>
    private bool TryInitializeReviveData()
    {
        // ★ 改这里：使用注入的 gameStateWriter，不是单例
        if (gameStateWriter == null)
            return false;

        GameStateData gameState = gameStateWriter.GetCurrentGameState();  // ← 改这里

        if (gameState == null || gameState.teams == null || gameState.teams.Count == 0)
            return false;

        try
        {
            reviveCountdown.Clear();
            lastFramePlayerBlood.Clear();
            playerDeadState.Clear();
            invincibleCountdown.Clear();
            isInvincibleState.Clear();

            int totalPlayers = 0;
            foreach (GameStateTeam team in gameState.teams)
            {
                if (team.players == null) continue;

                foreach (GameStatePlayer player in team.players)
                {
                    lastFramePlayerBlood[player.playerId] = 6;
                    reviveCountdown[player.playerId] = 0;
                    playerDeadState[player.playerId] = false;
                    invincibleCountdown[player.playerId] = 0;
                    isInvincibleState[player.playerId] = false;
                    totalPlayers++;
                }
            }

            Debug.Log($"[ServerReviveManager-{roomId}] ✅ 复活数据初始化完成，共 {totalPlayers} 个玩家");
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ServerReviveManager-{roomId}] ❌ 初始化异常: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Update中每帧调用此方法
    /// </summary>
    public void UpdateReviveSystem()
    {
        if (!isInitialized) return;

        CheckPlayerDeathAndRevive();
        UpdateInvincibleCountdown();
    }

    /// <summary>
    /// 检测死亡并处理复活
    /// </summary>
    private void CheckPlayerDeathAndRevive()
    {
        if (bloodManager == null)
            return;

        Dictionary<string, (int, int)> allPlayerBlood = bloodManager.GetAllPlayersBlood();

        // ★ 第一阶段：检测新的死亡事件  
        foreach (var kvp in allPlayerBlood)
        {
            string playerId = kvp.Key;
            int currentBlood = kvp.Value.Item1;

            if (!lastFramePlayerBlood.ContainsKey(playerId))
            {
                lastFramePlayerBlood[playerId] = currentBlood;
                playerDeadState[playerId] = (currentBlood <= 0);
                continue;
            }

            int lastBlood = lastFramePlayerBlood[playerId];
            bool wasAlive = lastBlood > 0;
            bool isNowDead = currentBlood <= 0;

            if (wasAlive && isNowDead && !playerDeadState[playerId])
            {
                string killerPlayerId = bloodManager.GetLastDamager(playerId);

                Debug.Log($"[ServerReviveManager-{roomId}] ☠️ 玩家 {playerId} 已死亡，击杀者: {killerPlayerId ?? "未知"}");

                playerDeadState[playerId] = true;
                reviveCountdown[playerId] = REVIVE_DELAY;

                // ★ 新增：在真正检测到死亡时，处理积分  
                if (!string.IsNullOrEmpty(killerPlayerId))
                {
                    HandlePlayerDeathScoring(playerId, killerPlayerId);
                }
            }

            lastFramePlayerBlood[playerId] = currentBlood;
        }

        // ★ 第二阶段：处理复活倒计时
        List<string> playersToRevive = new List<string>();

        foreach (var playerId in reviveCountdown.Keys.ToList())
        {
            if (reviveCountdown[playerId] > 0)
            {
                reviveCountdown[playerId] -= Time.deltaTime;

                if (reviveCountdown[playerId] <= 0)
                {
                    playersToRevive.Add(playerId);
                    reviveCountdown[playerId] = 0;
                }
            }
        }

        // ★ 第三阶段：执行复活
        foreach (string playerId in playersToRevive)
        {
            RevivePlayerNow(playerId);
        }
    }

    /// <summary>  
    /// ★ 新增：处理玩家死亡的积分计算  
    /// 根据 Room.json 中的配置计算得分  
    /// </summary>  
    private void HandlePlayerDeathScoring(string victimId, string killerId)
    {
        // 使用注入的 gradeManager 处理积分  
        // 这会自动调用 Room.json 中的积分系数  
        if (gradeManager != null)
        {
            // RecordPlayerDeath 内部会：  
            // 1. 给受害者减分（根据 deathCoefficient）  
            // 2. 给击杀者加分（根据 killCoefficient）  
            // 3. 保存到 JSON  
            gradeManager.RecordPlayerDeath(victimId, killerId);

            Debug.Log($"[ServerReviveManager-{roomId}] 📊 积分已记录: {killerId} 击杀了 {victimId}");
        }
    }

    /// <summary>
    /// 立即复活玩家
    /// </summary>
    private void RevivePlayerNow(string playerId)
    {
        if (bloodManager != null)
        {
            bloodManager.RevivePlayer(playerId);
        }

        playerDeadState[playerId] = false;
        reviveCountdown[playerId] = 0;

        // 进入无敌状态
        invincibleCountdown[playerId] = INVINCIBLE_DURATION;
        isInvincibleState[playerId] = true;

        Debug.Log($"[ServerReviveManager-{roomId}] ✅ 玩家 {playerId} 已复活，进入无敌状态");

        SaveInvincibleStateToJson();
    }

    /// <summary>
    /// 更新无敌倒计时
    /// </summary>
    private void UpdateInvincibleCountdown()
    {
        bool stateChanged = false;

        foreach (var playerId in invincibleCountdown.Keys.ToList())
        {
            if (invincibleCountdown[playerId] > 0)
            {
                invincibleCountdown[playerId] -= Time.deltaTime;

                if (!isInvincibleState.TryGetValue(playerId, out bool currentState) || !currentState)
                {
                    isInvincibleState[playerId] = true;
                    stateChanged = true;
                }

                if (invincibleCountdown[playerId] <= 0)
                {
                    invincibleCountdown[playerId] = 0;
                    isInvincibleState[playerId] = false;
                    stateChanged = true;
                    Debug.Log($"[ServerReviveManager-{roomId}] ⏰ 玩家 {playerId} 无敌状态结束");
                }
            }
        }

        if (stateChanged)
        {
            SaveInvincibleStateToJson();
        }
    }

    /// <summary>
    /// 保存无敌状态到JSON
    /// </summary>
    private void SaveInvincibleStateToJson()
    {
        if (InvincibleStateJsonWriter.Instance == null)
            return;

        try
        {
            InvincibleStateMessage msg = new InvincibleStateMessage
            {
                type = "InvincibleState",
                roomId = roomId,
                timestamp = System.DateTime.Now.Ticks / 10000
            };

            // ★ 改这里：使用注入的 gameStateWriter，不是单例
            if (gameStateWriter != null)
            {
                GameStateData gameState = gameStateWriter.GetCurrentGameState();  // ← 改这里
                if (gameState?.teams != null)
                {
                    foreach (var team in gameState.teams)
                    {
                        if (team.players == null) continue;

                        foreach (var player in team.players)
                        {
                            bool isInvincible = IsPlayerInvincible(player.playerId);
                            float countdown = GetInvincibleCountdown(player.playerId);

                            InvincibleStateInfo info = new InvincibleStateInfo
                            {
                                playerId = player.playerId,
                                playerName = player.playerName,
                                isInvincible = isInvincible,
                                invincibleCountdown = countdown
                            };
                            msg.invincibleStates.Add(info);
                        }
                    }
                }
            }

            InvincibleStateJsonWriter.Instance.SaveInvincibleStateToFile(roomId, msg);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ServerReviveManager-{roomId}] ❌ 保存无敌状态失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 获取玩家的复活倒计时
    /// </summary>
    public float GetReviveCountdown(string playerId)
    {
        if (reviveCountdown.TryGetValue(playerId, out float countdown))
            return Mathf.Max(0, countdown);
        return 0;
    }

    /// <summary>
    /// 获取玩家是否处于无敌状态
    /// </summary>
    public bool IsPlayerInvincible(string playerId)
    {
        if (isInvincibleState.TryGetValue(playerId, out bool isInvincible))
            return isInvincible;
        return false;
    }

    /// <summary>
    /// 获取玩家无敌倒计时
    /// </summary>
    public float GetInvincibleCountdown(string playerId)
    {
        if (invincibleCountdown.TryGetValue(playerId, out float countdown))
            return Mathf.Max(0, countdown);
        return 0;
    }

    /// <summary>
    /// 清理数据
    /// </summary>
    public void Cleanup()
    {
        Debug.Log($"[ServerReviveManager-{roomId}] → 开始清理...");

        try
        {
            if (initCoroutine != null)
            {
                StopCoroutine(initCoroutine);
                initCoroutine = null;
            }

            reviveCountdown.Clear();
            lastFramePlayerBlood.Clear();
            playerDeadState.Clear();
            invincibleCountdown.Clear();
            isInvincibleState.Clear();

            isInitialized = false;
            bloodManager = null;
            gameStateWriter = null;  // ← 新增

            Debug.Log($"[ServerReviveManager-{roomId}] ✅ 已清理");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ServerReviveManager-{roomId}] ❌ 清理失败: {ex.Message}");
        }
    }

    private void OnDestroy()
    {
        if (initCoroutine != null)
            StopCoroutine(initCoroutine);
    }
}
