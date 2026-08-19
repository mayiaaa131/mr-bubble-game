using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 房间专用的游戏结束管理器（非单例版本）
/// ★ 改造核心：
/// 1. 删除 public static Instance
/// 2. 通过 public 字段注入依赖（GradeManager、NetworkManager）
/// 3. 不直接使用其他 Manager 的 Instance
/// </summary>
public class ServerGameEndManager : MonoBehaviour
{
    private string roomId = "";
    private bool isGameStarted = false;
    private bool gameEnded = false;
    private bool gameEndMessageBroadcasted = false;
    private bool shouldResetForNextRound = false;
    private bool hasInitializedManager = false;

    private string victoryCondition = "SingleRound";  // "BO3" / "BO5" / "SingleRound"
    private string gameMode = "BO3";
    private bool seriesEnded = false;

    // 队伍胜场统计：teamId -> 胜场数
    private Dictionary<string, int> teamVictoryCount = new Dictionary<string, int>();

    private float remainingTime = 15f;  // 当前剩余时间（秒）
    private float remainingTime2 = 0f;  // 备份的初始时间

    private bool isInitialized = false;

    // ★ 关键改造：通过 public 字段注入依赖
    [HideInInspector] public ServerGradeManager gradeManager;
    [HideInInspector] public GameNetworkManager networkManager;
    [HideInInspector] public ServerPlayerBloodManager bloodManager; // ★ 新增 

    /// <summary>
    /// 由 RoomGameManager 调用，注入依赖
    /// </summary>
    public void InjectDependencies(ServerGradeManager grade, GameNetworkManager network, ServerPlayerBloodManager blood = null) // ★ 新增 blood 参数
    {
        gradeManager = grade;
        networkManager = network;
        bloodManager = blood; // ★ 新增
        Debug.Log($"[ServerGameEndManager-{roomId}] ✅ 依赖注入完成");
    }

    /// <summary>
    /// 初始化Manager（由RoomGameManager调用）
    /// </summary>
    public void Initialize(string roomId)
    {
        if (isInitialized) return;

        this.roomId = roomId;
        Debug.Log($"[ServerGameEndManager-{roomId}] 初始化中...");

        // 步骤1：从 Room JSON 读取 gameMode
        LoadGameModeFromRoom(roomId);

        // 步骤2：从 Room JSON 读取倒计时配置
        LoadCountdownSecondsFromRoom(roomId);

        // 步骤3：初始化游戏结束数据
        InitializeGameEnd();

        hasInitializedManager = true;
        isInitialized = true;
        Debug.Log($"[ServerGameEndManager-{roomId}] ✅ 初始化完成，游戏模式: {gameMode}");
    }

    /// <summary>
    /// 从 Room JSON 加载游戏模式
    /// </summary>
    private void LoadGameModeFromRoom(string roomId)
    {
        try
        {
            Room room = RoomDataManager.Instance.GetRoomById(roomId);

            if (room != null && !string.IsNullOrEmpty(room.gameMode))
            {
                gameMode = room.gameMode;
                victoryCondition = gameMode;
                Debug.Log($"[ServerGameEndManager-{roomId}] ✅ 游戏模式加载: {gameMode}");
            }
            else
            {
                Debug.LogWarning($"[ServerGameEndManager-{roomId}] ⚠️ 无法读取gameMode，使用默认值: BO3");
                gameMode = "BO3";
                victoryCondition = "BO3";
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ServerGameEndManager-{roomId}] ❌ 加载gameMode失败: {ex.Message}，使用默认值: BO3");
            gameMode = "BO3";
            victoryCondition = "BO3";
        }
    }

    /// <summary>
    /// 从 Room JSON 读取倒计时配置
    /// </summary>
    private void LoadCountdownSecondsFromRoom(string roomId)
    {
        try
        {
            Room room = RoomDataManager.Instance.GetRoomById(roomId);

            if (room != null && room.countdownSeconds > 0)
            {
                remainingTime = room.countdownSeconds;
                Debug.Log($"[ServerGameEndManager-{roomId}] ✅ 倒计时加载: {remainingTime}秒");
            }
            else
            {
                Debug.LogWarning($"[ServerGameEndManager-{roomId}] ⚠️ 无法读取倒计时，使用默认值: 300秒");
                remainingTime = 300;
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ServerGameEndManager-{roomId}] ❌ 加载倒计时失败: {ex.Message}，使用默认值: 300秒");
            remainingTime = 300;
        }

        remainingTime2 = remainingTime;
    }

    /// <summary>
    /// 初始化游戏结束数据
    /// </summary>
    private void InitializeGameEnd()
    {
        teamVictoryCount.Clear();

        RoomTeamsData roomTeams = TeamJsonFileHandler.Instance.LoadTeamsData(roomId);

        if (roomTeams != null && roomTeams.teams != null)
        {
            foreach (TeamInfo team in roomTeams.teams)
            {
                teamVictoryCount[team.teamId] = 0;
                Debug.Log($"[ServerGameEndManager-{roomId}] ✓ 初始化队伍胜场: {team.teamId} ({team.teamName})");
            }
        }

        gameEnded = false;
        gameEndMessageBroadcasted = false;
        remainingTime = remainingTime2;

        if (victoryCondition == "SingleRound")
        {
            seriesEnded = false;
        }

        Debug.Log($"[ServerGameEndManager-{roomId}] ✅ 游戏结束数据初始化完成，游戏模式: {victoryCondition}");
    }

    /// <summary>
    /// 第一个玩家加入时调用
    /// </summary>
    public void OnFirstPlayerJoined()
    {
        if (!isGameStarted)
        {
            isGameStarted = true;
            Debug.Log($"[ServerGameEndManager-{roomId}] ▶️ 第一个玩家已加入，倒计时开始！");

            // 初始化队伍胜场统计
            InitializeGameEnd();

            // 立即广播初始的 GameEndMessage
            BroadcastInitialGameEndMessage();
        }
    }

    /// <summary>
    /// 在玩家刚加入时立即广播初始的 GameEndMessage
    /// </summary>
    private void BroadcastInitialGameEndMessage()
    {
        GameEndMessage gameEndMsg = CreateCurrentGameEndMessage();
        gameEndMsg.timestamp = System.DateTime.Now.Ticks;

        string json = JsonUtility.ToJson(gameEndMsg, true);
        Debug.Log($"[ServerGameEndManager-{roomId}] 📡 初始化广播 GameEndMessage");

        // ★ 改造：使用注入的 networkManager，不是单例
        if (networkManager != null)
        {
            networkManager.BroadcastGameEndMessage(json);
        }

        if (GameEndJsonWriter.Instance != null)
        {
            GameEndJsonWriter.Instance.SaveGameEndToFile(roomId, gameEndMsg);
        }
    }

    /// <summary>
    /// 每帧更新（由外部调用，通常在RoomGameManager中）
    /// </summary>
    public void UpdateGameEnd()
    {
        if (!isGameStarted) return;

        if (gameEnded)
        {
            UpdateGameEndJsonFile();

            if (shouldResetForNextRound)
            {
                ResetForNextRound();
                shouldResetForNextRound = false;
            }
            return;
        }

        // 核心逻辑：时间倒计时
        remainingTime -= Time.deltaTime;

        // 检查是否时间到 0
        if (remainingTime <= 0 && !gameEndMessageBroadcasted)
        {
            remainingTime = 0;
            gameEnded = true;
            gameEndMessageBroadcasted = true;
            Debug.Log($"[ServerGameEndManager-{roomId}] ⏰ 游戏时间结束");
            EndGame();
        }
    }

    /// <summary>
    /// 实时更新 GameEnd JSON 文件
    /// </summary>
    private void UpdateGameEndJsonFile()
    {
        if (!gameEnded) return;

        GameEndMessage gameEndMsg = CreateCurrentGameEndMessage();
        gameEndMsg.timestamp = System.DateTime.Now.Ticks;

        if (GameEndJsonWriter.Instance != null)
        {
            GameEndJsonWriter.Instance.SaveGameEndToFile(roomId, gameEndMsg);

            if (Time.frameCount % 30 == 0)
            {
                //Debug.Log($"[ServerGameEndManager-{roomId}] 📝 JSON更新: {gameEndMsg.redTeamVictory}:{gameEndMsg.blueTeamVictory}");
            }
        }
    }

    /// <summary>
    /// 获取本局赢家
    /// ★ 改造：使用注入的 gradeManager，不是单例
    /// </summary>
    private (string teamId, string teamName) GetCurrentRoundWinner()
    {
        string winnerTeamId = "BlueTeam_1";
        string winnerTeamName = "blue";  // 默认值

        if (gradeManager != null)
        {
            GradeMessage gradeMsg = gradeManager.GenerateGradeMessage();

            if (gradeMsg != null && gradeMsg.teams != null && gradeMsg.teams.Count >= 2)
            {
                int redTeamScore = gradeMsg.teams[0].totalScore;
                int blueTeamScore = gradeMsg.teams[1].totalScore;

                //Debug.Log($"[ServerGameEndManager-{roomId}] 🏆 本局积分: " +
                          //$"{gradeMsg.teams[0].teamName}:{redTeamScore} vs " +
                          //$"{gradeMsg.teams[1].teamName}:{blueTeamScore}");

                if (redTeamScore > blueTeamScore)
                {
                    winnerTeamId = gradeMsg.teams[0].teamId;
                    winnerTeamName = "red";   // ✅ 不用 teamName，固定返回 "red"
                }
                else if (blueTeamScore > redTeamScore)
                {
                    winnerTeamId = gradeMsg.teams[1].teamId;
                    winnerTeamName = "blue";  // ✅ 不用 teamName，固定返回 "blue"
                }
                else
                {
                    Debug.Log($"[ServerGameEndManager-{roomId}] ⚠️ 本局平分");
                    return (null, "平局");
                }
            }
        }

        return (winnerTeamId, winnerTeamName);
    }

    /// <summary>
    /// 生成完整的 GameEndMessage
    /// </summary>
    /// <summary>
    /// 生成完整的 GameEndMessage
    /// ★ 改造：当系列赛结束时，根据总胜场数判断赢家
    /// </summary>
    private GameEndMessage CreateCurrentGameEndMessage()
    {
        int redVictory = 0;
        int blueVictory = 0;

        var teamIds = new List<string>(teamVictoryCount.Keys);

        if (teamIds.Count >= 2)
        {
            if (teamVictoryCount.TryGetValue(teamIds[0], out int red))
                redVictory = red;
            if (teamVictoryCount.TryGetValue(teamIds[1], out int blue))
                blueVictory = blue;
        }

        string redTeamId = teamIds.Count > 0 ? teamIds[0] : "RedTeam_1";
        string blueTeamId = teamIds.Count > 1 ? teamIds[1] : "BlueTeam_1";

        // 计算剩余轮数和系列赛是否结束
        int maxRounds;
        int remainingRounds;
        bool isSeriesEnd;

        if (victoryCondition == "SingleRound")
        {
            maxRounds = 1;
            int completedWins = redVictory + blueVictory;
            remainingRounds = maxRounds - completedWins;
            isSeriesEnd = (completedWins >= maxRounds);
        }
        else
        {
            maxRounds = victoryCondition == "BO5" ? 5 : 3;
            int totalPlayedRounds = redVictory + blueVictory;
            remainingRounds = Mathf.Max(0, maxRounds - totalPlayedRounds);
            isSeriesEnd = (redVictory > maxRounds / 2) || (blueVictory > maxRounds / 2);
        }

        if (isSeriesEnd != seriesEnded)
        {
            seriesEnded = isSeriesEnd;
            if (isSeriesEnd)
            {
                Debug.Log($"[ServerGameEndManager-{roomId}] 🏆 系列赛结束: {victoryCondition} | 最终比分: {redVictory}:{blueVictory}");
            }
        }

        // ★ 关键改造：根据 isSeriesEnd 分别判断赢家
        string winnerTeamId = redTeamId;
        string winnerTeamName = "red";

        if (isSeriesEnd)
        {
            // ★ 系列赛已结束：根据 redVictory 和 blueVictory 判断
            if (redVictory > blueVictory)
            {
                winnerTeamId = redTeamId;
                winnerTeamName = "red";
                Debug.Log($"[ServerGameEndManager-{roomId}] 🏆 系列赛赢家: {winnerTeamName} ({redVictory}:{blueVictory})");
            }
            else if (blueVictory > redVictory)
            {
                winnerTeamId = blueTeamId;
                winnerTeamName = "blue";
                Debug.Log($"[ServerGameEndManager-{roomId}] 🏆 系列赛赢家: {winnerTeamName} ({redVictory}:{blueVictory})");
            }
            else
            {
                // 系列赛平局（理论上不应出现，但为了容错）
                winnerTeamId = null;
                winnerTeamName = "平局";
                Debug.Log($"[ServerGameEndManager-{roomId}] ⚠️ 系列赛平局: {redVictory}:{blueVictory}");
            }
        }
        else
        {
            // ★ 系列赛未结束：根据本局游戏的赢家判断
            var (currentWinnerId, currentWinnerName) = GetCurrentRoundWinner();
            winnerTeamId = currentWinnerId;
            winnerTeamName = currentWinnerName;
        }

        return new GameEndMessage
        {
            type = "GameEnd",
            roomId = roomId,
            timestamp = System.DateTime.Now.Ticks,
            remainingTime = Mathf.Max(0, remainingTime),
            remainingRounds = remainingRounds,
            victoryCondition = victoryCondition,
            redTeamVictory = redVictory,
            blueTeamVictory = blueVictory,
            winnerTeamId = winnerTeamId,
            winnerTeamName = winnerTeamName,
            isSeriesEnd = isSeriesEnd
        };
    }

    /// <summary>
    /// 结束游戏
    /// </summary>
    private void EndGame()
    {
        GameEndMessage gameEndMsg = CreateCurrentGameEndMessage();

        // 检查是否为平局  
        bool isDraw = string.IsNullOrEmpty(gameEndMsg.winnerTeamId) || gameEndMsg.winnerTeamName == "平局";

        if (isDraw)
        {
            // 清空炸弹
            ClearBombsImmediately();

            gameEnded = true;
            gameEndMessageBroadcasted = true;

            BroadcastGameEnd(gameEndMsg);

            // 广播空炸弹列表  
            BroadcastEmptyBombs();

            if (GameEndJsonWriter.Instance != null)
            {
                GameEndJsonWriter.Instance.SaveGameEndToFile(roomId, gameEndMsg);
            }

            if (victoryCondition == "SingleRound")
            {
                Debug.Log($"[ServerGameEndManager-{roomId}]  单局平分，3秒后准备重新开始");
                Invoke("ResetCurrentRound", 3f);
            }
            else
            {
                Debug.Log($"[ServerGameEndManager-{roomId}]  系列赛平分，3秒后准备下一局");
                Invoke("PrepareNextRound", 3f);
            }
            return;
        }

        // 非平局处理
        if (victoryCondition == "SingleRound")
        {
            RecordTeamVictory(gameEndMsg.winnerTeamId);
            gameEndMsg = CreateCurrentGameEndMessage();

            Debug.Log($"[ServerGameEndManager-{roomId}]  单局结束: {gameEndMsg.winnerTeamName} 获胜！");

            // 清空炸弹
            ClearBombsImmediately();

            BroadcastGameEnd(gameEndMsg);

            // 广播空炸弹列表  
            BroadcastEmptyBombs();
            Debug.Log($"[ServerGameEndManager-{roomId}]  单局结束 - 空炸弹广播已发送");

            if (GameEndJsonWriter.Instance != null)
            {
                GameEndJsonWriter.Instance.SaveGameEndToFile(roomId, gameEndMsg);
            }
            return;
        }

        // BO3/BO5 模式
        RecordTeamVictory(gameEndMsg.winnerTeamId);
        gameEndMsg = CreateCurrentGameEndMessage();

        Debug.Log($"[ServerGameEndManager-{roomId}] 本局结束: {gameEndMsg.winnerTeamName} 获胜！");
        Debug.Log($"[ServerGameEndManager-{roomId}] 当前比分: {gameEndMsg.redTeamVictory}:{gameEndMsg.blueTeamVictory}");

        // 清空炸弹
        ClearBombsImmediately();

        BroadcastGameEnd(gameEndMsg);

        // 广播空炸弹列表  
        BroadcastEmptyBombs();
        Debug.Log($"[ServerGameEndManager-{roomId}]  本局结束 - 空炸弹广播已发送");

        if (GameEndJsonWriter.Instance != null)
        {
            GameEndJsonWriter.Instance.SaveGameEndToFile(roomId, gameEndMsg);
        }

        if (!gameEndMsg.isSeriesEnd)
        {
            Debug.Log($"[ServerGameEndManager-{roomId}]  继续系列赛，5秒后准备下一局");
            Invoke("PrepareNextRound", 5f);
        }
        else
        {
            Debug.Log($"[ServerGameEndManager-{roomId}]  系列赛结束！最终赢家: {gameEndMsg.winnerTeamName}");
        }
    }
    /// <summary>  
    /// ★ 新增：广播空炸弹列表（带详细调试）
    /// </summary>  
    private void BroadcastEmptyBombs()
    {
        Debug.Log($"[ServerGameEndManager-{roomId}]  BroadcastEmptyBombs 被调用");

        if (networkManager == null)
        {
            Debug.LogError($"[ServerGameEndManager-{roomId}]  networkManager 为空，无法广播");
            return;
        }

        BombStateMessage emptyMsg = new BombStateMessage
        {
            type = "BombStateBroadcast",
            roomId = roomId,
            timestamp = System.DateTime.Now.Ticks / 10000,
            isRetransmit = false,
            frameSequenceNumber = 0,
            bombs = new List<BombData>()
        };

        Debug.Log($"[ServerGameEndManager-{roomId}]  空炸弹消息: type={emptyMsg.type}, bombs.Count={emptyMsg.bombs.Count}");

        string json = JsonUtility.ToJson(emptyMsg, true);
        Debug.Log($"[ServerGameEndManager-{roomId}]  JSON内容:\n{json}");

        networkManager.BroadcastGameEndMessage(json);

        Debug.Log($"[ServerGameEndManager-{roomId}]  空炸弹列表已广播");
    }


    /// <summary>
    /// ★ 新增：立即清空所有炸弹并保存
    /// </summary>
    private void ClearBombsImmediately()
    {
        Debug.Log($"[ServerGameEndManager-{roomId}] 🟡 ClearBombsImmediately 被调用");

        // 从 RoomGameManager 获取 BombManager
        RoomGameManager roomManager = RoomGameManager.GetInstance(roomId);
        if (roomManager == null)
        {
            Debug.LogError($"[ServerGameEndManager-{roomId}] ❌ 无法获取 RoomGameManager");
            return;
        }

        ServerBombManager bombManager = roomManager.BombManager;
        if (bombManager == null)
        {
            Debug.LogError($"[ServerGameEndManager-{roomId}] ❌ BombManager 为空");
            return;
        }

        // 获取当前炸弹数
        int bombCountBefore = bombManager.GetActiveBombCount();
        Debug.Log($"[ServerGameEndManager-{roomId}] 📊 清空前炸弹数: {bombCountBefore}");

        // 清空炸弹内存
        Dictionary<string, BombData> allBombs = bombManager.GetAllBombs();
        if (allBombs != null)
        {
            int clearCount = allBombs.Count;
            allBombs.Clear();
            Debug.Log($"[ServerGameEndManager-{roomId}] 🗑️ 已清空内存中的 {clearCount} 个炸弹");
        }

        // 生成空的炸弹消息并保存到文件
        BombStateMessage emptyBombMsg = new BombStateMessage
        {
            type = "BombStateBroadcast",
            roomId = roomId,
            timestamp = System.DateTime.Now.Ticks / 10000,
            isRetransmit = false,
            frameSequenceNumber = 0,
            bombs = new List<BombData>()
        };

        // 保存空的炸弹状态到JSON
        if (BombStateJsonWriter.Instance != null)
        {
            bool saved = BombStateJsonWriter.Instance.SaveBombStateToFile(roomId, emptyBombMsg);
            Debug.Log($"[ServerGameEndManager-{roomId}] 💾 炸弹JSON文件已保存 (成功: {saved})");
        }
        else
        {
            Debug.LogWarning($"[ServerGameEndManager-{roomId}] ⚠️ BombStateJsonWriter.Instance 为空");
        }

        int bombCountAfter = bombManager.GetActiveBombCount();
        Debug.Log($"[ServerGameEndManager-{roomId}] ✅ 清空后炸弹数: {bombCountAfter}");
    }

    /// <summary>
    /// 广播游戏结束消息
    /// ★ 改造：使用注入的 networkManager，不是单例
    /// </summary>
    private void BroadcastGameEnd(GameEndMessage gameEndMsg)
    {
        string json = JsonUtility.ToJson(gameEndMsg, true);
        Debug.Log($"[ServerGameEndManager-{roomId}] 📡 游戏结束广播");

        if (networkManager != null)
        {
            networkManager.BroadcastGameEndMessage(json);
        }
    }

    /// <summary>
    /// 记录某队伍赢得一局
    /// </summary>
    public void RecordTeamVictory(string teamId)
    {
        if (teamVictoryCount.TryGetValue(teamId, out int currentVictory))
        {
            teamVictoryCount[teamId]++;
            Debug.Log($"[ServerGameEndManager-{roomId}] 🏆 胜场记录: {teamId} 赢得一局，目前胜场: {teamVictoryCount[teamId]}");
        }
    }

    /// <summary>  
    /// 重置当前局  
    /// </summary>  
    private void ResetCurrentRound()
    {
        Debug.Log($"[ServerGameEndManager-{roomId}] 🔄 重置当前局...");

        gameEnded = false;
        gameEndMessageBroadcasted = false;

        // ★ 核心修复：确保重置后时间有效
        if (remainingTime2 <= 0)
        {
            Debug.LogWarning($"[ServerGameEndManager-{roomId}] ⚠️ remainingTime2={remainingTime2} 无效，重新从配置加载");
            LoadCountdownSecondsFromRoom(roomId);
        }
        else
        {
            remainingTime = remainingTime2;
        }

        Debug.Log($"[ServerGameEndManager-{roomId}] ✅ 重置后剩余时间: {remainingTime}s");

        // ★ 新增：重置血量（参考积分重置）
        if (bloodManager != null)
        {
            bloodManager.ResetAllPlayersBlood();
            Debug.Log($"[ServerGameEndManager-{roomId}] ✅ 血量已重置（当前局）");
        }

        if (networkManager != null)
        {
            networkManager.ResetGameEndBroadcastFlag();
        }

        Debug.Log($"[ServerGameEndManager-{roomId}] ✅ 当前局重置完成");
    }
    /// <summary>
    /// 准备下一局
    /// </summary>
    public void PrepareNextRound()
    {
        Debug.Log($"[ServerGameEndManager-{roomId}] 🔔 准备下一局");
        shouldResetForNextRound = true;
    }
    /// <summary>  
    /// 为下一局重置  
    /// </summary>  
    private void ResetForNextRound()
    {
        Debug.Log($"[ServerGameEndManager-{roomId}] 🔄 为下一局做准备...");

        gameEnded = false;
        gameEndMessageBroadcasted = false;

        // ★ 已有：重置积分
        if (gradeManager != null)
        {
            gradeManager.ResetGradeForNewRound();
            GradeMessage resetGradeMsg = gradeManager.GenerateGradeMessage();
            if (resetGradeMsg != null)
            {
                string gradeJson = JsonUtility.ToJson(resetGradeMsg, true);
                Debug.Log($"[ServerGameEndManager-{roomId}] 📊 重置后 Grade:\n{gradeJson}");
            }
        }

        // ★ 新增：重置血量（参考积分重置）
        if (bloodManager != null)
        {
            bloodManager.ResetAllPlayersBlood();
            Debug.Log($"[ServerGameEndManager-{roomId}] ✅ 血量已重置（下一局）");
        }

        remainingTime = remainingTime2;

        if (networkManager != null)
        {
            networkManager.ResetGameEndBroadcastFlag();
        }

        Debug.Log($"[ServerGameEndManager-{roomId}] ✅ 下一局准备完成");
    }
    /// 对外接口
    /// </summary>
    public bool IsGameStarted() => isGameStarted;
    public bool IsGameEnded() => gameEnded;
    public bool IsSeriesEnded() => seriesEnded;
    public float GetRemainingTime() => remainingTime;
    public string GetVictoryCondition() => victoryCondition;
    public Dictionary<string, int> GetTeamVictoryCount() => teamVictoryCount;

    public void SetRemainingTime(float time) => remainingTime = time;

    /// <summary>
    /// 设置游戏模式
    /// </summary>
    public void SetVictoryCondition(string condition)
    {
        if (condition == "BO3" || condition == "BO5" || condition == "SingleRound")
        {
            victoryCondition = condition;
            Debug.Log($"[ServerGameEndManager-{roomId}] ✅ 游戏模式已设置: {victoryCondition}");
        }
    }

    /// <summary>
    /// 清理数据
    /// </summary>
    public void Cleanup()
    {
        Debug.Log($"[ServerGameEndManager-{roomId}] → 开始清理...");

        try
        {
            isGameStarted = false;
            gameEnded = false;
            gameEndMessageBroadcasted = false;
            shouldResetForNextRound = false;
            seriesEnded = false;

            remainingTime = 15f;
            teamVictoryCount.Clear();
            isInitialized = false;
            gradeManager = null;
            networkManager = null;
            bloodManager = null; // ★ 新增  

            Debug.Log($"[ServerGameEndManager-{roomId}] ✅ 已清理");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ServerGameEndManager-{roomId}] ❌ 清理失败: {ex.Message}");
        }
    }
}
