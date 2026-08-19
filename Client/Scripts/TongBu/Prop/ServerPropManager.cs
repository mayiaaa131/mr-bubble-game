using UnityEngine;
using System;
using System.Collections.Generic;
using System.Collections;

/// <summary>
/// 房间专用的道具管理器（非单例版本）
/// ★ 改造核心：
/// 1. 删除 public static Instance
/// 2. 不需要依赖注入（但会调用 bloodManager）
/// 3. 所有引用改为通过注入的字段调用
/// </summary>
public class ServerPropManager : MonoBehaviour
{
    private string roomId = "";
    private Dictionary<string, PropInfo> activePropData = new Dictionary<string, PropInfo>();
    private Dictionary<string, long> propCooldownEndTime = new Dictionary<string, long>();
    private Dictionary<string, long> lastPickupTime = new Dictionary<string, long>();

    private long frameCounter = 0;
    private const float PICKUP_RANGE = 0.5f;
    private Coroutine propUpdateCoroutine;
    private bool isInitialized = false;

    // ★ 新增：沉默道具持有状态字典  
    // key = playerId, value = 是否持有沉默道具  
    private Dictionary<string, bool> playerHoldingSilenceProp = new Dictionary<string, bool>();

    // 沉默道具配置  
    private const float SILENCE_PROP_EFFECT_HALF_SIZE = 2.0f;  // 正方形范围半边长  
    private const float SILENCE_PROP_DURATION = 1.0f;          // 客户端实例存活时间（秒）  
    private const long SILENCE_PROP_RESPAWN_TIME = 20000;       // 沉默道具冷却时间（15秒）  

    // ★ 关键改造：通过 public 字段注入依赖  
    [HideInInspector] public ServerPlayerBloodManager bloodManager;
    [HideInInspector] public GameStateJsonWriter gameStateWriter;  // ← 新增 

    /// <summary>  
    /// 由 RoomGameManager 调用，注入依赖  
    /// </summary>  
    public void InjectDependencies(ServerPlayerBloodManager blood, GameStateJsonWriter gameState)  // ← 修改  
    {
        bloodManager = blood;
        gameStateWriter = gameState;  // ← 新增  
        Debug.Log($"[ServerPropManager-{roomId}] ✅ 依赖注入完成");
    }
    /// <summary>
    /// 初始化Manager（由RoomGameManager调用）
    /// </summary>
    public void Initialize(string roomId)
    {
        if (isInitialized) return;

        this.roomId = roomId;
        Debug.Log($"[ServerPropManager-{roomId}] 初始化中...");

        // 初始化道具数据
        InitializePropData();
        Debug.Log($"[ServerPropManager-{roomId}] ✅ 道具初始化完成，当前道具数: {activePropData.Count}");

        // 启动更新协程
        if (propUpdateCoroutine != null)
            StopCoroutine(propUpdateCoroutine);

        propUpdateCoroutine = StartCoroutine(PropUpdateRoutine());
        isInitialized = true;
        Debug.Log($"[ServerPropManager-{roomId}] ✅ PropUpdateRoutine 协程已启动");
    }

    /// <summary>
    /// 初始化所有道具
    /// </summary>
    private void InitializePropData()
    {
        CreateProp("prop_001", "BloodRestore", new Vector3(1.3f, 0.8f, 9f), 1);
        CreateProp("prop_002", "BloodRestore", new Vector3(1.3f, 0.8f, 1.5f), 1);
        CreateProp("prop_003", "BloodRestore", new Vector3(-1.3f, 0.8f, 1.5f), 2);
        CreateProp("prop_004", "BloodRestore", new Vector3(-1.3f, 0.8f, 9f), 1);



        // ★ 新增：沉默道具（冷却时间15秒，restoreAmount=0无意义）  
        PropInfo silenceProp = new PropInfo("prop_005", "Silence", new Vector3(-1f, 0.8f, 5.0f), 0);
        silenceProp.respawnTime = SILENCE_PROP_RESPAWN_TIME;
        activePropData["prop_005"] = silenceProp;
        long currentTime = System.DateTime.Now.Ticks / 10000;
        propCooldownEndTime["prop_005"] = currentTime;
        lastPickupTime["prop_005"] = 0;


        Debug.Log($"[ServerPropManager-{roomId}] ✅ 共初始化 {activePropData.Count} 个道具");
    }

    /// <summary>
    /// 创建单个道具
    /// </summary>
    public void CreateProp(string propId, string propType, Vector3 position, int restoreAmount)
    {
        PropInfo prop = new PropInfo(propId, propType, position, restoreAmount);
        activePropData[propId] = prop;

        long currentTime = System.DateTime.Now.Ticks / 10000;
        propCooldownEndTime[propId] = currentTime;
        lastPickupTime[propId] = 0;

        //Debug.Log($"[ServerPropManager-{roomId}] 道具创建: {propId} ({propType}) @ ({position.x:F2}, {position.z:F2})");
    }

    /// <summary>
    /// 道具更新协程（每50ms更新一次）
    /// </summary>
    private IEnumerator PropUpdateRoutine()
    {
        //Debug.Log($"[ServerPropManager-{roomId}] 🟢 PropUpdateRoutine 协程开始运行");

        int updateCount = 0;

        while (true)
        {
            yield return new WaitForSeconds(0.05f);

            if (!isInitialized) continue;

            updateCount++;
            long currentTime = System.DateTime.Now.Ticks / 10000;

            // 每 20 帧（1秒）打印一次调试信息
            if (updateCount % 20 == 0)
            {
                //Debug.Log($"[ServerPropManager-{roomId}] 🔄 更新第 {updateCount} 帧，当前道具数: {activePropData.Count}");
            }

            // 更新道具冷却状态
            UpdatePropStates(currentTime);

            // 检测玩家拾取
            DetectPlayerPropPickups();
        }
    }

    /// <summary>
    /// 更新道具冷却状态
    /// </summary>
    private void UpdatePropStates(long currentTime)
    {
        if (activePropData.Count == 0)
            return;

        foreach (var kvp in activePropData)
        {
            string propId = kvp.Key;
            PropInfo prop = kvp.Value;

            if (propCooldownEndTime.TryGetValue(propId, out long cooldownEnd))
            {
                if (currentTime >= cooldownEnd)
                {
                    if (prop.state != "Available")
                    {
                        prop.state = "Available";
                        Debug.Log($"[ServerPropManager-{roomId}] 道具冷却结束: {propId}");
                    }
                }
                else
                {
                    prop.state = "Cooldown";
                    long remainingMs = cooldownEnd - currentTime;

                    // 每 1 秒打印一次
                    if (remainingMs > 0 && remainingMs % 1000 < 50)
                    {
                        Debug.Log($"[ServerPropManager-{roomId}] ⏱️ 道具 {propId} 冷却中: {remainingMs}ms");
                    }
                }
            }
            else
            {
                prop.state = "Available";
                propCooldownEndTime[propId] = currentTime;
            }
        }
    }

    /// <summary>
    /// 检测玩家拾取道具
    /// </summary>
    private void DetectPlayerPropPickups()
    {
        // ★ 检查 1：GameStateJsonWriter 是否注入  
        if (gameStateWriter == null)  // ✅ 改为注入的字段  
            return;

        // ★ 检查 2：获取游戏状态  
        GameStateData gameState = gameStateWriter.GetCurrentGameState();  // ✅ 改为注入的字段
        if (gameState == null || gameState.teams == null)
            return;

        // ★ 检查 3：是否有玩家
        int totalPlayers = 0;
        foreach (var team in gameState.teams)
        {
            if (team.players != null)
                totalPlayers += team.players.Count;
        }

        if (totalPlayers == 0)
            return;

        // ════════════════════════════════════════════════════════════════
        // 遍历所有玩家
        // ════════════════════════════════════════════════════════════════
        foreach (var team in gameState.teams)
        {
            if (team.players == null) continue;

            foreach (var player in team.players)
            {
                Vector3 playerPos = new Vector3(
                    player.position.x,
                    player.position.y,
                    player.position.z
                );

                //Debug.Log($"[ServerPropManager-{roomId}] 👤 检测玩家 {player.playerId} 位置: ({playerPos.x:F2}, {playerPos.z:F2})");

                // 遍历所有道具
                foreach (var propKvp in activePropData)
                {
                    PropInfo prop = propKvp.Value;

                    // 距离检查
                    float distX = Mathf.Abs(playerPos.x - prop.position.x);
                    float distZ = Mathf.Abs(playerPos.z - prop.position.z);

                    //Debug.Log($"[ServerPropManager-{roomId}]   📏 道具 {prop.propId} 距离: X={distX:F2}, Z={distZ:F2}, 状态={prop.state}");

                    // ★ 关键条件：距离在范围内 + 道具可用
                    if (distX <= PICKUP_RANGE && distZ <= PICKUP_RANGE && prop.state == "Available")
                    {
                        long currentTime = System.DateTime.Now.Ticks / 10000;
                        long timeSinceLastPickup = currentTime - prop.lastPickupTime;

                        if (timeSinceLastPickup < 100 && prop.lastPickupTime > 0)
                        {
                            Debug.Log($"[ServerPropManager-{roomId}]   ⏱️ 道具 {prop.propId} 防重复中 ({timeSinceLastPickup}ms < 100ms)");
                            continue;
                        }

                        // ✅ 执行拾取
                        Debug.Log($"[ServerPropManager-{roomId}] ✨ 玩家 {player.playerId} 拾取了道具 {prop.propId}");
                        ExecutePropPickup(player.playerId, prop);
                        break;
                    }
                }
            }
        }
    }

    /// <summary>
    /// 执行道具拾取逻辑
    /// </summary>
    private void ExecutePropPickup(string playerId, PropInfo prop)
    {
        long currentTime = System.DateTime.Now.Ticks / 10000;

        switch (prop.propType)
        {
            case "BloodRestore":
                HandleBloodRestoreProp(playerId, prop);
                break;

            // ★ 新增：沉默道具分支
            case "Silence":
                HandleSilencePropPickup(playerId, prop);
                return; // ★ 注意：直接 return，不走下面的 Cooldown 逻辑
                        // 因为沉默道具放置时才进入冷却，拾取时只标记持有状态

            default:
                Debug.LogWarning($"[ServerPropManager-{roomId}] ⚠️ 未知道具类型: {prop.propType}");
                break;
        }

        prop.state = "Cooldown";
        prop.lastPickupTime = currentTime;
        propCooldownEndTime[prop.propId] = currentTime + prop.respawnTime;

        Debug.Log($"[ServerPropManager-{roomId}] ✅ 道具 {prop.propId} 已拾取，将在 {prop.respawnTime}ms 后可用");
    }

    /// <summary>
    /// 处理血量恢复道具
    /// 使用注入的 bloodManager，不是单例
    /// </summary>
    private void HandleBloodRestoreProp(string playerId, PropInfo prop)
    {
        if (bloodManager == null)
        {
            Debug.LogError($"[ServerPropManager-{roomId}] BloodManager 未注入");
            return;
        }

        int currentBlood = bloodManager.GetPlayerBlood(playerId);
        int maxBlood = 6;
        int restoreAmount = prop.restoreAmount;

        int newBlood = Mathf.Min(currentBlood + restoreAmount, maxBlood);
        int actualRestore = newBlood - currentBlood;

        bloodManager.RestorePlayerBlood(playerId, actualRestore);

        Debug.Log($"[ServerPropManager-{roomId}]玩家 {playerId} 恢复了 {actualRestore} 点血量 ({currentBlood} → {newBlood})");
    }

    /// <summary>
    /// 生成道具状态广播消息
    /// </summary>
    public PropStateMessage GeneratePropStateMessage()
    {
        frameCounter++;
        long serverTime = System.DateTime.Now.Ticks / 10000;

        PropStateMessage msg = new PropStateMessage
        {
            type = "PropStateBroadcast",
            roomId = roomId,
            timestamp = serverTime
        };

        foreach (var kvp in activePropData)
        {
            PropInfo prop = kvp.Value;
            prop.serverTimestamp = serverTime;
            msg.props.Add(prop);
        }

        return msg;
    }

    /// <summary>
    /// 获取所有道具
    /// </summary>
    public Dictionary<string, PropInfo> GetAllProps() => activePropData;

    /// <summary>
    /// 获取活跃道具数
    /// </summary>
    public int GetActivePropCount() => activePropData.Count;


    // ════════════════════════════════════════════════════════════════
    // ★ 新增：沉默道具相关方法
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// 处理沉默道具拾取
    /// 只标记持有状态，道具进入冷却在放置时触发
    /// </summary>
    private void HandleSilencePropPickup(string playerId, PropInfo prop)
    {
        // ★ 如果已经持有，跳过（但仍更新JSON，确保调试文件最新）  
        if (IsPlayerHoldingSilenceProp(playerId))
        {
            Debug.Log($"[断线重连测试]HandleSilencePropPickup: 玩家 {playerId} 已持有沉默道具，跳过重复拾取");

            // ★ 仍然保存一次JSON（重连后客户端是空状态，JSON需要反映最新）  
            SaveSilencePropHoldStateToJson();
            return;
        }

        // ★ 标记该玩家持有沉默道具
        playerHoldingSilenceProp[playerId] = true;

        Debug.Log($"【断线重连测试】HandleSilencePropPickup: 玩家 {playerId} 成功拾取沉默道具，playerHoldingSilenceProp[{playerId}]={playerHoldingSilenceProp[playerId]}");

        // ★ 道具进入"被持有"冷却状态，防止其他玩家重复拾取
        prop.state = "Cooldown";
        prop.lastPickupTime = System.DateTime.Now.Ticks / 10000;
        propCooldownEndTime[prop.propId] = prop.lastPickupTime + SILENCE_PROP_RESPAWN_TIME;

        Debug.Log($"【断线重连测试】沉默道具 {prop.propId} 进入 Cooldown，将在 {SILENCE_PROP_RESPAWN_TIME}ms 后重新可用");

        // ★ 保存持有状态到JSON
        SaveSilencePropHoldStateToJson();

        // ★ 发送单播通知给玩家
        GameNetworkManager networkMgr = GameNetworkManager.GetInstanceForRoom(roomId);
        if (networkMgr != null)
        {
            networkMgr.SendSilencePropPickedUpToPlayer(playerId, roomId);
            Debug.Log($"【断线重连测试】SilencePropPickedUp 已发送给 {playerId}");
        }
        else
        {
            Debug.LogWarning($"【断线重连测试】⚠️ networkMgr 为空，无法发送 SilencePropPickedUp");
        }
    }

    /// <summary>
    /// 处理沉默道具放置请求
    /// 由 GameNetworkManager 收到 SilencePropPlace 消息后调用
    /// </summary>
    public void HandleSilencePropPlace(string trustedPlayerId, string trustedTeamId, GSPosition position)

    {

        // ★ 增加调试信息  
        Debug.Log($"[断线重连测试]HandleSilencePropPlace: 玩家 {trustedPlayerId} 放置沉默道具");
        Debug.Log($"[断线重连测试]放置前 playerHoldingSilenceProp[{trustedPlayerId}]={IsPlayerHoldingSilenceProp(trustedPlayerId)}");
        // ★ 校验1：玩家是否持有沉默道具
        if (!playerHoldingSilenceProp.TryGetValue(trustedPlayerId, out bool isHolding) || !isHolding)
        {
            Debug.LogWarning($"[ServerPropManager-{roomId}] ⚠️ 玩家 {trustedPlayerId} 未持有沉默道具，拒绝放置");
            return;
        }


        if (!IsPlayerHoldingSilenceProp(trustedPlayerId))
        {
            Debug.LogWarning($"[断线重连测试]⚠️ 玩家 {trustedPlayerId} 不持有沉默道具，拒绝放置");
            return;
        }

        // ★ 清除持有状态（你原来已有此行）  
        playerHoldingSilenceProp[trustedPlayerId] = false;

        Debug.Log($"[断线重连测试]放置后 playerHoldingSilenceProp[{trustedPlayerId}]={IsPlayerHoldingSilenceProp(trustedPlayerId)}");

        //Debug.Log($"[ServerPropManager-{roomId}] 🤫 玩家 {trustedPlayerId}（{trustedTeamId}）放置沉默道具");
        //Debug.Log($"[ServerPropManager-{roomId}]    放置位置: ({position.x:F2}, {position.y:F2}, {position.z:F2})");
        //Debug.Log($"[ServerPropManager-{roomId}]    正方形范围半边长: {SILENCE_PROP_EFFECT_HALF_SIZE}");

        // ★ 消除范围内的敌方炸弹
        GameNetworkManager networkMgr = GameNetworkManager.GetInstanceForRoom(roomId);
        if (networkMgr != null && networkMgr.bombManager != null)
        {
            int destroyedCount = networkMgr.bombManager.DestroyEnemyBombsInRange(
                position,
                SILENCE_PROP_EFFECT_HALF_SIZE,
                trustedTeamId
            );
            Debug.Log($"[ServerPropManager-{roomId}] 💥 已消除 {destroyedCount} 个敌方炸弹");
        }

        // ★ 广播放置结果给所有客户端
        if (networkMgr != null)
        {
            networkMgr.BroadcastSilencePropPlaced(
                trustedPlayerId,
                trustedTeamId,
                position,
                SILENCE_PROP_EFFECT_HALF_SIZE,
                SILENCE_PROP_DURATION,
                roomId
            );
        }

        // ★ 保存调试JSON
        SaveSilencePropHoldStateToJson();
    }

    /// <summary>
    /// 查询玩家是否持有沉默道具
    /// </summary>
    public bool IsPlayerHoldingSilenceProp(string playerId)
    {
        return playerHoldingSilenceProp.TryGetValue(playerId, out bool isHolding) && isHolding;
    }

    /// <summary>
    /// 保存沉默道具持有状态到调试JSON
    /// 参考 InvincibleStateJsonWriter 的写法
    /// </summary>
    private void SaveSilencePropHoldStateToJson()
    {
        if (SilencePropStateJsonWriter.Instance == null)
            return;

        try
        {
            SilencePropHoldStateMessage msg = new SilencePropHoldStateMessage
            {
                type = "SilencePropHoldState",
                roomId = roomId,
                timestamp = System.DateTime.Now.Ticks / 10000
            };

            // 从 GameStateWriter 读取玩家列表来填充数据
            if (gameStateWriter != null)
            {
                GameStateData gameState = gameStateWriter.GetCurrentGameState();
                if (gameState?.teams != null)
                {
                    foreach (var team in gameState.teams)
                    {
                        if (team.players == null) continue;

                        foreach (var player in team.players)
                        {
                            bool isHolding = IsPlayerHoldingSilenceProp(player.playerId);
                            msg.holdStates.Add(new SilencePropHoldStateInfo
                            {
                                playerId = player.playerId,
                                playerName = player.playerName,
                                teamId = team.teamId,
                                isHolding = isHolding
                            });
                        }
                    }
                }
            }

            SilencePropStateJsonWriter.Instance.SaveSilencePropStateToFile(roomId, msg);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ServerPropManager-{roomId}] ❌ 保存沉默道具状态失败: {ex.Message}");
        }
    }




    /// <summary>
    /// 清理数据
    /// </summary>
    public void Cleanup()
    {
        Debug.Log($"[ServerPropManager-{roomId}] → 开始清理...");
        Debug.Log($"【断线重连测试】Cleanup 被调用，清空 playerHoldingSilenceProp（当前持有人数: {CountHoldingPlayers()}）");

        try
        {
            if (propUpdateCoroutine != null)
            {
                StopCoroutine(propUpdateCoroutine);
                propUpdateCoroutine = null;
            }

            activePropData.Clear();
            propCooldownEndTime.Clear();
            lastPickupTime.Clear();

            // ★ 关键：服务器关闭时，清空持有状态字典
            // 下次服务器启动时 Initialize() → InitializePropData() 全新初始化
            // 不会读取 JSON，与血量管理器行为完全一致
            playerHoldingSilenceProp.Clear();
            Debug.Log($"【断线重连测试】playerHoldingSilenceProp 已清空，服务器重启后将从头初始化");

            isInitialized = false;
            bloodManager = null;

            Debug.Log($"[ServerPropManager-{roomId}] ✅ 已清理");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ServerPropManager-{roomId}] ❌ 清理失败: {ex.Message}");
        }
    }

    private int CountHoldingPlayers()
    {
        int count = 0;
        foreach (var kvp in playerHoldingSilenceProp)
            if (kvp.Value) count++;
        return count;
    }

    private void OnDestroy()
    {
        if (propUpdateCoroutine != null)
            StopCoroutine(propUpdateCoroutine);
    }
}
