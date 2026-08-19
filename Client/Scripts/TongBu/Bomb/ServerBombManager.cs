using UnityEngine;
using System;
using System.Collections.Generic;
using System.Collections;

/// <summary>
/// 房间专用的炸弹管理器（非单例版本）
/// ★ 改造核心：
/// 1. 删除 public static Instance
/// 2. 通过 public 字段注入依赖（ReviveManager, BloodManager, GradeManager, GameStateWriter）
/// 3. 所有引用改为通过注入的字段调用
/// </summary>
public class ServerBombManager : MonoBehaviour
{
    private string roomId = "";
    private Dictionary<string, BombData> activeBombs = new Dictionary<string, BombData>();
    private Dictionary<string, long> lastBombCreateTime = new Dictionary<string, long>();

    private long bombIdCounter = 0;
    private Coroutine updateCoroutine;
    private bool isInitialized = false;
    private long frameCounter = 0;
    private const long DUPLICATE_THRESHOLD = 100; // 100ms 内视为重复

    // ★ 关键改造：通过 public 字段注入依赖（由 RoomGameManager 设置）
    [HideInInspector] public ServerReviveManager reviveManager;
    [HideInInspector] public ServerPlayerBloodManager bloodManager;
    [HideInInspector] public ServerGradeManager gradeManager;
    [HideInInspector] public GameStateJsonWriter gameStateWriter;

    /// <summary>
    /// 由 RoomGameManager 调用，注入依赖
    /// </summary>
    public void InjectDependencies(ServerReviveManager revive,
                                    ServerPlayerBloodManager blood,
                                    ServerGradeManager grade,
                                    GameStateJsonWriter gameState)
    {
        reviveManager = revive;
        bloodManager = blood;
        gradeManager = grade;
        gameStateWriter = gameState;

        Debug.Log($"[ServerBombManager-{roomId}] ✅ 依赖注入完成");
    }

    /// <summary>
    /// 初始化Manager（由RoomGameManager调用）
    /// </summary>
    public void Initialize(string roomId)
    {
        if (isInitialized) return;

        this.roomId = roomId;
        Debug.Log($"[ServerBombManager-{roomId}] 初始化中...");

        activeBombs.Clear();
        lastBombCreateTime.Clear();
        bombIdCounter = 0;
        frameCounter = 0;

        // 启动更新协程
        if (updateCoroutine != null)
            StopCoroutine(updateCoroutine);
        updateCoroutine = StartCoroutine(BombUpdateRoutine());

        isInitialized = true;
        Debug.Log($"[ServerBombManager-{roomId}] ✅ 初始化完成");
    }

    /// <summary>
    /// 创建炸弹
    /// </summary>
    public void CreateBomb(string creatorPlayerId, string creatorTeamId, string clientBombId,
                          Vector3 position, string bombType)
    {
        Debug.Log($"🔔 [ServerBombManager-{roomId}] CreateBomb 被调用:\n" +
                  $"  → creatorPlayerId: {creatorPlayerId}\n" +
                  $"  → creatorTeamId:   {creatorTeamId}\n" +
                  $"  → clientBombId:    {clientBombId}\n" +
                  $"  → position:        ({position.x:F2}, {position.y:F2}, {position.z:F2})\n" +
                  $"  → bombType:        {bombType}\n" +
                  $"  → isInitialized:   {isInitialized}\n" +
                  $"  → 当前活跃炸弹数:  {activeBombs.Count}");

        // ★ 校验1：初始化检查
        if (!isInitialized)
        {
            Debug.LogError($"❌ [ServerBombManager-{roomId}] CreateBomb 失败: Manager未初始化！");
            return;
        }

        long currentTime = System.DateTime.Now.Ticks / 10000;

        // ★ 校验2：防重复创建检查（关键日志）
        if (lastBombCreateTime.TryGetValue(creatorPlayerId, out long lastTime))
        {
            long timeDifference = currentTime - lastTime;
            Debug.Log($"⏱️ [ServerBombManager-{roomId}] 防重复检查:\n" +
                      $"  → 玩家: {creatorPlayerId}\n" +
                      $"  → 上次创建时间: {lastTime}\n" +
                      $"  → 当前时间:     {currentTime}\n" +
                      $"  → 时间差:       {timeDifference}ms\n" +
                      $"  → 阈值:         {DUPLICATE_THRESHOLD}ms\n" +
                      $"  → 是否拦截:     {timeDifference < DUPLICATE_THRESHOLD}");

            if (timeDifference < DUPLICATE_THRESHOLD)
            {
                Debug.LogWarning($"⚠️ [ServerBombManager-{roomId}] ❌ CreateBomb 失败: 防重复拦截！\n" +
                                 $"  → 玩家: {creatorPlayerId}\n" +
                                 $"  → 时间差 {timeDifference}ms < 阈值 {DUPLICATE_THRESHOLD}ms\n" +
                                 $"  → 💡 建议: 适当调大 DUPLICATE_THRESHOLD（当前100ms）");
                return;
            }
        }
        else
        {
            Debug.Log($"ℹ️ [ServerBombManager-{roomId}] 玩家 {creatorPlayerId} 首次创建炸弹，无防重复记录");
        }

        // ★ 校验3：检查bombId是否已存在
        if (activeBombs.ContainsKey(clientBombId))
        {
            Debug.LogWarning($"⚠️ [ServerBombManager-{roomId}] ❌ CreateBomb 失败: bombId '{clientBombId}' 已存在！");
            return;
        }

        lastBombCreateTime[creatorPlayerId] = currentTime;
        Debug.Log($"📝 [ServerBombManager-{roomId}] 更新玩家 {creatorPlayerId} 的最后创建时间: {currentTime}");

        BombData newBomb = new BombData
        {
            bombId = clientBombId,
            playerId = creatorPlayerId,
            teamId = creatorTeamId,
            position = new GSPosition(position.x, position.y, position.z),
            bombType = bombType,
            bombLevel = "一级",
            state = "Active",
            totalTime = 4f,
            remainingTime = 4f,
            createTime = currentTime,
            explosionTimestamp = 0,
            explosionRanges = BombRangeCalculator.CalculateExplosionRanges(bombType, position),
            mergeCount = 1,           // ★ 新增  
            mergedBombIds = new List<string>(),  // ★ 新增  
            isMaster = true,          // ★ 新增  
            mergeGroupId = ""         // ★ 新增  
        };

        // ★ 新增：寻找可合并的主炸弹  
        BombData targetMaster = FindMergeTarget(newBomb);
        if (targetMaster != null)
        {
            JoinMergeGroup(targetMaster, newBomb, currentTime);
            activeBombs[newBomb.bombId] = newBomb;
        }
        else
        {
            activeBombs[newBomb.bombId] = newBomb;
        }
    }




    /// <summary>
    /// 寻找可合并的主炸弹
    /// 条件：同队(teamId一致) + isMaster + Active + mergeCount < 3 + 范围重叠
    /// ★ 不同队的炸弹绝对不能合并
    /// </summary>
    private BombData FindMergeTarget(BombData newBomb)
    {
        foreach (var kvp in activeBombs)
        {
            BombData candidate = kvp.Value;

            if (!candidate.isMaster) continue; // 只找主炸弹
            if (candidate.state != "Active") continue;
            if (candidate.mergeCount >= 3) continue; // ★ 三级封顶，拒绝加入

            // ★ 关键：只有同队炸弹才能合并升级
            if (candidate.teamId != newBomb.teamId) continue;

            if (BombRangeCalculator.HasOverlap(candidate.explosionRanges, newBomb.explosionRanges))
                return candidate;
        }
        return null;
    }





    /// <summary>
    /// 将新炸弹加入主炸弹的合并组
    /// ★ 新炸弹独立保留在 activeBombs，通过 mergeGroupId 关联主炸弹
    /// ★ 主炸弹维护并集范围用于伤害结算，从炸弹保留自身范围用于客户端显示
    /// </summary>
    private void JoinMergeGroup(BombData master, BombData incoming, long currentTime)
    {
        // 1. 建立/沿用合并组ID
        if (string.IsNullOrEmpty(master.mergeGroupId))
            master.mergeGroupId = master.bombId;

        // 2. 从炸弹标记归属
        incoming.mergeGroupId = master.mergeGroupId;
        incoming.isMaster = false;

        // 3. 主炸弹更新成员与计数
        master.mergedBombIds.Add(incoming.bombId);
        master.mergeCount++;

        // 4. 升级等级
        master.bombLevel = master.mergeCount switch
        {
            2 => "二级",
            3 => "三级",
            _ => master.bombLevel
        };

        // 5. 重置倒计时
        float newTotalTime = master.mergeCount switch
        {
            2 => 2f,
            3 => 1f,
            _ => 4f
        };
        master.totalTime = newTotalTime;
        master.remainingTime = newTotalTime;
        master.createTime = currentTime; // ★ 从合并时刻重新计时

        // 6. 从炸弹同步等级和倒计时（供客户端显示）
        incoming.bombLevel = master.bombLevel;
        incoming.mergeCount = master.mergeCount;
        incoming.totalTime = newTotalTime;
        incoming.remainingTime = newTotalTime;
        incoming.createTime = currentTime;
        incoming.state = "Active"; // 从炸弹保持 Active，客户端可正常渲染范围

        // ★ 修复：同步组内所有已存在的从炸弹（第三颗连入时，第二颗也需要更新）
        foreach (string memberId in master.mergedBombIds)
        {
            if (memberId == incoming.bombId) continue; // 安全起见排除刚加入的

            if (activeBombs.TryGetValue(memberId, out BombData existingMember))
            {
                existingMember.bombLevel = master.bombLevel;
                existingMember.mergeCount = master.mergeCount;
                existingMember.totalTime = newTotalTime;
                existingMember.remainingTime = newTotalTime;
                existingMember.createTime = currentTime;
            }
        }


        // 7. 主炸弹合并爆炸范围取并集（用于伤害结算）
        master.explosionRanges = BombRangeCalculator.MergeRanges(
            master.explosionRanges,
            incoming.explosionRanges
        );

        Debug.Log($"[ServerBombManager-{roomId}] ⬆️ 合并升级: {incoming.bombId} → 组[{master.bombId}]" +
                  $" 等级:{master.bombLevel} 倒计时:{newTotalTime}s 组内炸弹数:{master.mergeCount}");
    }

    /// <summary>
    /// 联动爆炸：同组所有炸弹同时爆炸
    /// ★ 伤害使用主炸弹的并集范围，一次性结算，damage = mergeCount（几颗伤几血）
    /// ★ 每颗炸弹独立进入 Exploding 状态，客户端各自播放爆炸特效
    /// </summary>
    private void TriggerGroupExplosion(BombData master, long currentTime)
    {
        // 收集组内所有炸弹（含主炸弹）
        List<BombData> groupBombs = new List<BombData> { master };
        foreach (string memberId in master.mergedBombIds)
        {
            if (activeBombs.TryGetValue(memberId, out BombData member))
                groupBombs.Add(member);
        }

        // ★ 所有炸弹切换为 Exploding（客户端每颗都能播特效）
        foreach (BombData bomb in groupBombs)
        {
            bomb.state = "Exploding";
            bomb.explosionTimestamp = currentTime;
            bomb.remainingTime = 0;
        }

        // ★ 伤害只结算一次，防止同一玩家被多颗炸弹重复扣血
        HandleGroupExplosionDamage(master);

        Debug.Log($"[ServerBombManager-{roomId}] 💥 联动爆炸! 组[{master.bombId}]" +
                  $" 等级:{master.bombLevel} 伤害:{master.mergeCount}血 共{groupBombs.Count}颗");
    }

    /// <summary>
    /// 爆炸伤害结算
    /// ★ 使用主炸弹的并集范围检测命中
    /// ★ damage = mergeCount（几颗炸弹重叠就伤几血）
    /// ★ 积分归属：记录为主炸弹的 playerId
    ///   （如需多玩家分别计分，见下方扩展注释）
    /// </summary>
    private void HandleGroupExplosionDamage(BombData master)
    {
        Dictionary<string, Vector3> playerPositions = GetAllPlayerPositions();

        // ★ 用主炸弹并集范围（已在 JoinMergeGroup 中维护）
        List<string> affectedPlayerIds = BombRangeCalculator.GetPlayersInRange(
            master.explosionRanges,
            playerPositions
        );

        // ★ 伤害值 = 组内炸弹数量
        int damage = master.mergeCount;

        Debug.Log($"[ServerBombManager-{roomId}] 炸弹组[{master.bombId}]" +
                  $" 命中{affectedPlayerIds.Count}个玩家，伤害:{damage}血");

        foreach (string victimId in affectedPlayerIds)
        {
            // 跳过同队
            string targetTeamId = GetPlayerTeamId(victimId);
            if (!string.IsNullOrEmpty(targetTeamId) && targetTeamId == master.teamId)
            {
                Debug.Log($"[ServerBombManager-{roomId}] 玩家 {victimId} 同队，跳过");
                continue;
            }

            // 跳过无敌
            if (reviveManager != null && reviveManager.IsPlayerInvincible(victimId))
            {
                Debug.Log($"[ServerBombManager-{roomId}] 🛡️ 玩家 {victimId} 无敌，跳过");
                continue;
            }

            // ★ 积分归主炸弹玩家
            ApplyDamageToPlayer(victimId, damage, master.playerId);
            Debug.Log($"[ServerBombManager-{roomId}] 玩家 {victimId} 受到 {damage} 血伤害");
        }

        /*
         * ★ 后期多玩家分别计分扩展方案（保留注释备用）：
         * 
         * 思路：伤害只扣一次，但为组内每颗炸弹的 playerId 各记录一次"击中贡献"
         * 
         * HashSet<string> damagedVictims = new HashSet<string>();
         * foreach (BombData memberBomb in groupBombs)
         * {
         *     var hits = BombRangeCalculator.GetPlayersInRange(memberBomb.explosionRanges, playerPositions);
         *     foreach (string victimId in hits)
         *     {
         *         if (damagedVictims.Contains(victimId)) continue; // 血只扣一次
         *         // 同队/无敌检测...
         *         ApplyDamageToPlayer(victimId, damage, memberBomb.playerId); // 各自 playerId 计分
         *         damagedVictims.Add(victimId);
         *     }
         * }
         */
    }



    /// <summary>
    /// 炸弹更新协程
    /// </summary>
    private IEnumerator BombUpdateRoutine()
    {
        Debug.Log($"🟢 [ServerBombManager-{roomId}] BombUpdateRoutine 协程启动");
        int loopCount = 0;

        while (true)
        {
            yield return new WaitForSeconds(0.05f);

            if (!isInitialized)
            {
                // 每100次打印一次，避免刷屏
                if (loopCount % 100 == 0)
                    Debug.LogWarning($"⏸️ [ServerBombManager-{roomId}] BombUpdateRoutine 跳过：未初始化");
                loopCount++;
                continue;
            }

            long currentTime = System.DateTime.Now.Ticks / 10000;
            int bombCountBefore = activeBombs.Count;

            UpdateBombs(currentTime);

            int bombCountAfter = activeBombs.Count;

            // ★ 炸弹数量变化时打印
            if (bombCountBefore != bombCountAfter)
            {
                Debug.Log($"🔄 [ServerBombManager-{roomId}] 炸弹数量变化: {bombCountBefore} → {bombCountAfter}");
            }

            SaveBombStateToFile();
            loopCount++;
        }
    }

    /// <summary>
    /// 更新炸弹倒计时和爆炸判定
    /// </summary>

    private void UpdateBombs(long currentTime)
    {
        List<string> bombsToDelete = new List<string>();

        foreach (var kvp in activeBombs)
        {
            BombData bomb = kvp.Value;

            if (bomb.state == "Active")
            {
                long elapsedMs = currentTime - bomb.createTime;
                long totalTimeMs = (long)(bomb.totalTime * 1000);

                // ✅ 改：移除 Mathf.Max(0, ...) 限制，允许负数
                bomb.remainingTime = (bomb.totalTime * 1000 - elapsedMs) / 1000f;

                if (elapsedMs >= totalTimeMs && bomb.state == "Active")
                {
                    bomb.state = "Exploding";
                    bomb.explosionTimestamp = currentTime;
                    bomb.remainingTime = 0;

                    bomb.explosionRanges = BombRangeCalculator.CalculateExplosionRanges(
                        bomb.bombType,
                        new Vector3(bomb.position.x, bomb.position.y, bomb.position.z)
                    );

                    HandleExplosionDamage(bomb);
                    Debug.Log($"[ServerBombManager-{roomId}] 💥 炸弹爆炸: {bomb.bombId}");
                }
            }

            // ✅ Exploding 状态：继续计算负数的 remainingTime
            if (bomb.state == "Exploding")
            {
                long elapsedSinceExplosion = currentTime - bomb.explosionTimestamp;
                // ✅ 持续计算负的剩余时间
                bomb.remainingTime = -(elapsedSinceExplosion / 1000f);

                // 超过3秒后标记为删除
                if (elapsedSinceExplosion > 3000)
                {
                    bomb.state = "Removed";
                    bombsToDelete.Add(bomb.bombId);
                }
            }

            // ★ 新增：被沉默道具标记为 Removed 的炸弹，继续倒计时直到 < -3秒  
            if (bomb.state == "Removed" && !bombsToDelete.Contains(bomb.bombId))
            {
                long elapsedMs = currentTime - bomb.createTime;
                bomb.remainingTime = (bomb.totalTime * 1000 - elapsedMs) / 1000f;

                if (bomb.remainingTime < -3f)
                {
                    bombsToDelete.Add(bomb.bombId);
                }
            }

        }

        // ✅ 关键：在删除前保存一次 JSON
        if (bombsToDelete.Count > 0)
        {
            SaveBombStateToFile();
        }

        // 执行删除
        foreach (string bombId in bombsToDelete)
        {
            activeBombs.Remove(bombId);
            Debug.Log($"[ServerBombManager-{roomId}] 🗑️ 炸弹已从内存删除: {bombId}");
        }
    }

    /// <summary>
    /// 处理爆炸伤害
    /// ★ 关键改造：使用注入的Manager引用，不是单例
    /// </summary>
    private void HandleExplosionDamage(BombData bomb)
    {
        Dictionary<string, Vector3> playerPositions = GetAllPlayerPositions();

        List<string> affectedPlayerIds = BombRangeCalculator.GetPlayersInRange(
            bomb.explosionRanges,
            playerPositions
        );

        Debug.Log($"[ServerBombManager-{roomId}] 炸弹 {bomb.bombId} 影响 {affectedPlayerIds.Count} 个玩家");

        foreach (string playerId in affectedPlayerIds)
        {
            string targetTeamId = GetPlayerTeamId(playerId);

            // 跳过同队玩家
            if (!string.IsNullOrEmpty(targetTeamId) && targetTeamId == bomb.teamId)
            {
                Debug.Log($"[ServerBombManager-{roomId}] 玩家 {playerId} 与炸弹同队，跳过伤害");
                continue;
            }

            // ★ 关键改造：通过注入的 reviveManager 调用，不是单例
            if (reviveManager != null && reviveManager.IsPlayerInvincible(playerId))
            {
                Debug.Log($"[ServerBombManager-{roomId}] 🛡️ 玩家 {playerId} 处于无敌状态");
                continue;
            }

            int damage = BombRangeCalculator.GetDamageByBombLevel(bomb.bombLevel);
            ApplyDamageToPlayer(playerId, damage, bomb.playerId);
            Debug.Log($"[ServerBombManager-{roomId}] 玩家 {playerId} 受到 {damage} 点伤害");
        }
    }

    /// <summary>
    /// 对玩家造成伤害
    /// ★ 改造：只处理血量，不处理积分
    /// 积分由 ReviveManager 检测到真正死亡后再处理
    /// </summary>
    private void ApplyDamageToPlayer(string victimId, int damage, string killerId)
    {
        // ★ 只处理血量
        if (bloodManager != null)
        {
            bloodManager.DealDamageToPlayer(victimId, damage, killerId);
            Debug.Log($"[ServerBombManager-{roomId}] 玩家 {victimId} 受到 {damage} 点伤害");
        }

        // ❌ 移除这段代码！不要在这里记录死亡
        // if (gradeManager != null)
        // {
        //     gradeManager.RecordPlayerDeath(victimId, killerId);
        // }
    }

    /// <summary>
    /// 获取所有玩家位置
    /// ★ 改造：使用注入的 gameStateWriter，不是单例
    /// </summary>
    private Dictionary<string, Vector3> GetAllPlayerPositions()
    {
        Dictionary<string, Vector3> positions = new Dictionary<string, Vector3>();

        if (gameStateWriter == null)
            return positions;

        GameStateData gameState = gameStateWriter.GetCurrentGameState();
        if (gameState?.teams == null)
            return positions;

        foreach (var team in gameState.teams)
        {
            if (team.players == null) continue;

            foreach (var player in team.players)
            {
                positions[player.playerId] = new Vector3(
                    player.position.x,
                    player.position.y,
                    player.position.z
                );
            }
        }

        return positions;
    }





    /// <summary>
    /// 获取玩家的队伍ID
    /// ★ 改造：使用注入的 gameStateWriter，不是单例
    /// </summary>
    private string GetPlayerTeamId(string playerId)
    {
        if (gameStateWriter == null)
            return null;

        GameStateData gameState = gameStateWriter.GetCurrentGameState();
        if (gameState?.teams == null)
            return null;

        foreach (var team in gameState.teams)
        {
            if (team.players == null) continue;

            foreach (var player in team.players)
            {
                if (player.playerId == playerId)
                    return team.teamId;
            }
        }

        return null;
    }

    /// <summary>
    /// 保存炸弹状态到JSON
    /// </summary>
    private void SaveBombStateToFile()
    {
        BombStateMessage msg = GenerateBombStateMessage(isRetransmit: false);

        if (msg != null && BombStateJsonWriter.Instance != null)
        {
            BombStateJsonWriter.Instance.SaveBombStateToFile(roomId, msg);
        }
    }

    /// <summary>
    /// 生成广播消息
    /// </summary>
    public BombStateMessage GenerateBombStateMessage(bool isRetransmit = false)
    {
        frameCounter++;
        long serverTime = System.DateTime.Now.Ticks / 10000;

        BombStateMessage msg = new BombStateMessage
        {
            type = "BombStateBroadcast",
            roomId = roomId,
            timestamp = serverTime,
            isRetransmit = isRetransmit,
            frameSequenceNumber = frameCounter
        };

        foreach (var kvp in activeBombs)
        {
            BombData clientBomb = kvp.Value;

            // ✅ 改：包含所有炸弹（包括 Removed 状态）
            // 只要炸弹在 activeBombs 字典中，就添加到消息中
            msg.bombs.Add(clientBomb);
        }

        return msg;
    }

    /// <summary>
    /// 获取活跃炸弹数
    /// </summary>
    public int GetActiveBombCount() => activeBombs.Count;

    /// <summary>
    /// 获取所有炸弹
    /// </summary>
    public Dictionary<string, BombData> GetAllBombs() => activeBombs;

    // ════════════════════════════════════════════════════════════════
    // ★ 新增：沉默道具消除敌方炸弹
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// 消除正方形范围内的敌方炸弹
    /// 由 ServerPropManager.HandleSilencePropPlace() 调用
    /// </summary>
    /// <param name="center">放置位置</param>
    /// <param name="halfSize">正方形范围半边长</param>
    /// <param name="placerTeamId">放置者的队伍ID（己方炸弹不消除）</param>
    /// <returns>被消除的炸弹数量</returns>
    public int DestroyEnemyBombsInRange(GSPosition center, float halfSize, string placerTeamId)
    {
        if (!isInitialized)
        {
            Debug.LogWarning($"[ServerBombManager-{roomId}] ⚠️ DestroyEnemyBombsInRange: Manager未初始化");
            return 0;
        }

        // 【销毁炸弹】沉默道具触发，开始执行炸弹销毁
        Debug.Log($"【销毁炸弹】DestroyEnemyBombsInRange 被调用" +
                  $"\n  → placerTeamId: {placerTeamId}" +
                  $"\n  → center: ({center.x:F2}, {center.y:F2}, {center.z:F2})" +
                  $"\n  → halfSize: {halfSize}" +
                  $"\n  → 当前activeBombs数量: {activeBombs.Count}");

        List<string> bombsToDestroy = new List<string>();

        foreach (var kvp in activeBombs)
        {
            BombData bomb = kvp.Value;

            // 【销毁炸弹】逐个检查炸弹
            Debug.Log($"【销毁炸弹】检查炸弹 {bomb.bombId}" +
                      $"\n  → state:    {bomb.state}" +
                      $"\n  → teamId:   {bomb.teamId}" +
                      $"\n  → position: ({bomb.position.x:F2}, {bomb.position.y:F2}, {bomb.position.z:F2})");

            // ★ 只处理 Active 状态的炸弹
            if (bomb.state != "Active")
            {
                Debug.Log($"【销毁炸弹】跳过 {bomb.bombId}，state={bomb.state}（非Active）");
                continue;
            }

            // ★ 只消除敌方炸弹
            if (bomb.teamId == placerTeamId)
            {
                Debug.Log($"【销毁炸弹】跳过 {bomb.bombId}，与放置者同队（teamId={bomb.teamId}）");
                continue;
            }

            float distX = Mathf.Abs(bomb.position.x - center.x);
            float distZ = Mathf.Abs(bomb.position.z - center.z);

            // 【销毁炸弹】打印距离判断结果
            Debug.Log($"【销毁炸弹】距离判断 {bomb.bombId}" +
                      $"\n  → distX={distX:F2}，halfSize={halfSize}，X是否在范围内: {distX <= halfSize}" +
                      $"\n  → distZ={distZ:F2}，halfSize={halfSize}，Z是否在范围内: {distZ <= halfSize}");

            if (distX <= halfSize && distZ <= halfSize)
            {
                bombsToDestroy.Add(bomb.bombId);
                Debug.Log($"【销毁炸弹】✅ {bomb.bombId} 在范围内，加入销毁列表");
            }
            else
            {
                Debug.Log($"【销毁炸弹】❌ {bomb.bombId} 不在范围内，跳过");
            }
        }

        // 【销毁炸弹】打印最终销毁列表
        Debug.Log($"【销毁炸弹】共 {bombsToDestroy.Count} 个炸弹将被销毁");

        foreach (string bombId in bombsToDestroy)
        {
            if (activeBombs.TryGetValue(bombId, out BombData bomb))
            {
                bomb.state = "Removed";
                Debug.Log($"【销毁炸弹】🗑️ {bombId} state → Removed");
            }
            else
            {
                Debug.LogWarning($"【销毁炸弹】⚠️ {bombId} 在activeBombs中找不到，销毁失败！");
            }
        }

        if (bombsToDestroy.Count > 0)
        {
            SaveBombStateToFile();
            Debug.Log($"【销毁炸弹】✅ 销毁完成，共消除 {bombsToDestroy.Count} 个敌方炸弹");
        }
        else
        {
            Debug.Log($"【销毁炸弹】ℹ️ 范围内没有可销毁的敌方炸弹");
        }

        return bombsToDestroy.Count;
    }





    /// <summary>
    /// 清理数据
    /// </summary>
    public void Cleanup()
    {
        Debug.Log($"[ServerBombManager-{roomId}] → 开始清理...");

        try
        {
            if (updateCoroutine != null)
            {
                StopCoroutine(updateCoroutine);
            }

            activeBombs.Clear();
            lastBombCreateTime.Clear();
            isInitialized = false;

            reviveManager = null;
            bloodManager = null;
            gradeManager = null;
            gameStateWriter = null;

            Debug.Log($"[ServerBombManager-{roomId}] ✅ 已清理");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ServerBombManager-{roomId}] ❌ 清理失败: {ex.Message}");
        }
    }

    private void OnDestroy()
    {
        if (updateCoroutine != null)
            StopCoroutine(updateCoroutine);
    }
}
