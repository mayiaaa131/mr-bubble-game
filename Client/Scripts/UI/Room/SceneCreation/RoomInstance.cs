// ============================================
// 文件路径：Assets/scripts/Room/RoomInstance.cs
// ============================================
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class RoomInstance : MonoBehaviour
{
    [Header("房间基础数据")]
    public Room roomData = new Room();

    // ── 子系统（各自独立，不共享）──
    public RoomMapLoader mapLoader { get; private set; }
    public RoomTeamManager teamManager { get; private set; }
    public RoomPlayerManager playerManager { get; private set; }
    public RoomTimerManager timerManager { get; private set; }

    // 地图根节点
    private Transform _mapRoot;

    /// <summary>
    /// ★ 改进：直接从 Room.json 加载，只用 Room 类
    /// </summary>
    public void Initialize( string roomId )
    {
        // ★ 直接从 Room.json 加载完整房间数据
        Room fullRoomData = JsonFileHandler.Instance.GetRoomById(roomId);

        if (fullRoomData == null)
        {
            Debug.LogError($"❌ 无法从 Room.json 加载房间: {roomId}");
            Debug.LogError($"   请检查 Room.json 中是否存在此房间ID");
            return;
        }

        // ★ 直接使用 JSON 数据
        roomData = fullRoomData;

        Debug.Log($"✓ 从 Room.json 加载房间数据: {roomData.roomId}");
        Debug.Log($"  - 房间名: {roomData.roomName}");
        Debug.Log($"  - 地图ID: {roomData.mapId}");
        Debug.Log($"  - 最大玩家数: {roomData.maxPlayers}");
        Debug.Log($"  - 倒计时: {roomData.countdownSeconds}秒");
        Debug.Log($"  - 计分系数已加载:");
        Debug.Log($"    • 基础分: {roomData.scoreCoefficients.baseScore}");
        Debug.Log($"    • 击杀系数: {roomData.scoreCoefficients.killCoefficient}");
        Debug.Log($"    • 死亡系数: {roomData.scoreCoefficients.deathCoefficient}");
        Debug.Log($"    • 助攻系数: {roomData.scoreCoefficients.assistCoefficient}");

        // 生成游戏ID
        roomData.gameId = System.Guid.NewGuid().ToString();

        // 生成随机的红蓝队 TeamId
        roomData.teamRedId = $"team_red_{System.Guid.NewGuid().ToString()[ ..8 ]}";
        roomData.teamBlueId = $"team_blue_{System.Guid.NewGuid().ToString()[ ..8 ]}";

        // 创建地图根节点
        _mapRoot = new GameObject($"MapRoot_{roomData.roomId}").transform;
        _mapRoot.SetParent(transform);

        // 挂载子系统
        mapLoader = gameObject.AddComponent<RoomMapLoader>();
        teamManager = gameObject.AddComponent<RoomTeamManager>();
        playerManager = gameObject.AddComponent<RoomPlayerManager>();
        timerManager = gameObject.AddComponent<RoomTimerManager>();

        // 子系统初始化
        mapLoader.Init(roomData.mapId, _mapRoot);
        teamManager.Init(roomData.roomId);
        playerManager.Init(teamManager, roomData.roomId);
        timerManager.Init(roomData.countdownSeconds, OnTimeUp, roomData.roomId);

        Debug.Log($"[Room {roomData.roomId}] 初始化完成，地图={roomData.mapId}，每队上限={roomData.maxPlayers / 2}\n" +
                  $"  红队ID: {roomData.teamRedId}\n" +
                  $"  蓝队ID: {roomData.teamBlueId}");
    }

    public bool AddPlayer( string playerId, string playerName )
    {
        if (roomData.state != "waiting")
        {
            Debug.LogWarning($"[Room {roomData.roomId}] 游戏已开始，拒绝加入");
            return false;
        }
        if (roomData.currentPlayers >= roomData.maxPlayers)
        {
            Debug.LogWarning($"[Room {roomData.roomId}] 房间已满");
            return false;
        }

        string team = teamManager.AutoAssignTeam(playerId, playerName);
        if (string.IsNullOrEmpty(team))
        {
            Debug.LogWarning($"[Room {roomData.roomId}] 队伍已满，{playerName} 无法加入");
            return false;
        }

        roomData.currentPlayers++;
        playerManager.AddPlayer(playerId, playerName, team);

        Debug.Log($"[Room {roomData.roomId}] {playerName} 加入 → {team}队（当前{roomData.currentPlayers}/{roomData.maxPlayers}）");

        // 人满自动开始
        if (roomData.currentPlayers >= roomData.maxPlayers)
            StartGame();

        return true;
    }

    public void StartGame( )
    {
        if (roomData.state == "playing")
        {
            Debug.LogWarning($"[Room {roomData.roomId}] 游戏已在进行中，忽略重复调用");
            return;
        }

        roomData.state = "playing";
        roomData.startTime = (int)System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        mapLoader.LoadMap();
        timerManager.StartCountdown();

        Debug.Log($"[Room {roomData.roomId}] 游戏开始！玩家数={roomData.currentPlayers}");
    }

    private void OnTimeUp( )
    {
        roomData.state = "finished";
        var result = playerManager.GenerateResult();

        Debug.Log($"[Room {roomData.roomId}] 游戏结束！胜者：{result.winningTeam} " +
                  $"({result.winningTeamScore} vs {result.losingTeamScore})");

        ServerRoomManager.Instance.OnRoomFinished(roomData.roomId, result);
    }

    public void Cleanup( )
    {
        if (_mapRoot != null)
            Destroy(_mapRoot.gameObject);

        string roomSceneName = $"Room_{roomData.roomId}";
        if (SceneManager.GetSceneByName(roomSceneName).isLoaded)
        {
            SceneManager.UnloadSceneAsync(roomSceneName);
            Debug.Log($"[RoomInstance] 房间场景已卸载：{roomSceneName}");
        }

        Destroy(gameObject);
    }
}
