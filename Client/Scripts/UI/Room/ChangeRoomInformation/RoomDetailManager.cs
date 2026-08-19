using UnityEngine;
using System;
using System.Collections.Generic;

/// <summary>
/// 房间详情管理器（最终合并版）
/// 负责房间详情页面的数据管理和更新
/// ✓ 新增：支持直接从 UI 统计的玩家数更新房间数据
/// ✓ 新增：获取当前房间ID方法
/// ✓ 新增：删除玩家时实时同步到 Team JSON
/// ✓ 新增：完整玩家数据的同步
/// ★ 保留：完整字段更新支持（包括游戏配置和分数系数）
/// ★ 改进：第二份代码的优化逻辑合并到第一份
/// </summary>
public class RoomDetailManager : MonoBehaviour
{
    private static RoomDetailManager instance;

    private string currentRoomId;  // ✓ 改为存储房间ID，而不是房间对象副本

    private void Awake( )
    {
        // 单例模式
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// 进入房间详情页面（初始化当前房间）
    /// ✓ 关键：只存储房间ID，不存储房间对象
    /// </summary>
    public void EnterRoomDetail( string roomId )
    {
        Debug.Log($"[RoomDetailManager.EnterRoomDetail] 参数 roomId: '{roomId}'");

        Room room = RoomDataManager.Instance.GetRoomById(roomId);

        if (room == null)
        {
            Debug.LogError($"❌ 无法找到房间: {roomId}");
            Debug.LogError($"   [错误信息] 请检查:");
            Debug.LogError($"   1. JSON文件中是否包含此房间");
            Debug.LogError($"   2. 房间ID是否正确");
            Debug.LogError($"   3. RoomDataManager.GetRoomById() 是否正常工作");
            return;
        }

        // ✓ 只存储房间ID
        currentRoomId = roomId;

        Debug.Log($"✓ 进入房间详情页面");
        Debug.Log($"  - 房间ID: {room.roomId}");
        Debug.Log($"  - 房间名称: {room.roomName}");
        Debug.Log($"  - 当前玩家数: {room.currentPlayers}/{room.maxPlayers}");
        Debug.Log($"  [已存储] currentRoomId = '{currentRoomId}'");
    }

    /// <summary>
    /// ✓ 新增方法：获取当前房间ID
    /// </summary>
    public string GetCurrentRoomId( )
    {
        return currentRoomId;
    }

    /// <summary>
    /// 创建玩家（包括生成ID、名称、分配队伍、创建预制体）
    /// </summary>
    public void CreatePlayer()
    {
        if (string.IsNullOrEmpty(currentRoomId))
        {
            Debug.LogError("❌ 没有当前房间ID，无法创建玩家");
            return;
        }

        // ✓ 步骤1: 从JSON重新加载最新的房间数据
        Room currentRoom = RoomDataManager.Instance.GetRoomById(currentRoomId);

        if (currentRoom == null)
        {
            Debug.LogError($"❌ 无法找到房间: {currentRoomId}");
            return;
        }

        // ✓ 步骤2: 检查玩家数是否已达上限
        if (currentRoom.currentPlayers >= currentRoom.maxPlayers)
        {
            Debug.LogWarning($"⚠ 房间 {currentRoom.roomId} 已满员（{currentRoom.currentPlayers}/{currentRoom.maxPlayers}）");
            return;
        }

        // ✓ 步骤3: 玩家数 +1
        currentRoom.currentPlayers++;

        // ★ 【关键新增】步骤4: 生成玩家ID和名称
        string newPlayerId = $"player_{currentRoom.currentPlayers}";
        string newPlayerName = ""; // 待定，由分配队伍后生成

        Debug.Log($"✓ 新玩家已创建（临时）");
        Debug.Log($"  - 玩家ID: {newPlayerId}");
        Debug.Log($"  - 房间ID: {currentRoom.roomId}");
        Debug.Log($"  - 当前玩家数: {currentRoom.currentPlayers}/{currentRoom.maxPlayers}");

        // ✓ 步骤5: 立即保存更新到 Room.json 文件
        UpdateRoomData(currentRoom);

        // ★ 【关键新增】步骤6: 添加玩家到 Team JSON 的默认队伍（优先红队）
        AddPlayerToDefaultTeam(currentRoomId, newPlayerId, currentRoom);
    }

    /// <summary>
    /// ★ 新增方法：将玩家添加到默认队伍（优先红队）
    /// </summary>
    private void AddPlayerToDefaultTeam(string roomId, string playerId, Room room)
    {
        try
        {
            RoomTeamsData roomTeamsData = TeamJsonFileHandler.Instance.LoadTeamsData(roomId);
            if (roomTeamsData == null || roomTeamsData.teams == null || roomTeamsData.teams.Count == 0)
            {
                Debug.LogError($"❌ 无法加载 Team JSON 数据: {roomId}");
                return;
            }

            // 找到红队和蓝队
            TeamInfo redTeam = roomTeamsData.teams.Find(t => t.teamId == room.teamRedId);
            TeamInfo blueTeam = roomTeamsData.teams.Find(t => t.teamId == room.teamBlueId);

            // 优先添加到红队，如果红队满了则添加到蓝队
            TeamInfo targetTeam = null;
            string targetTeamName = "";

            if (redTeam != null && (redTeam.players == null || redTeam.players.Count < 3))
            {
                targetTeam = redTeam;
                targetTeamName = "红队";
            }
            else if (blueTeam != null && (blueTeam.players == null || blueTeam.players.Count < 3))
            {
                targetTeam = blueTeam;
                targetTeamName = "蓝队";
            }

            if (targetTeam == null)
            {
                Debug.LogError("❌ 两个队伍都已满员");
                return;
            }

            // 计算新玩家名称（根据队伍现有人数）
            if (targetTeam.players == null)
                targetTeam.players = new List<TeamPlayer>();

            int playerIndex = targetTeam.players.Count + 1;
            string playerName = $"{targetTeamName}玩家{playerIndex}";

            // 创建新玩家对象
            TeamPlayer newPlayer = new TeamPlayer(playerId, playerName);
            targetTeam.players.Add(newPlayer);
            targetTeam.alivePlayerCount = targetTeam.players.Count;

            // 保存到 Team JSON
            TeamJsonFileHandler.Instance.SaveTeamsData(roomId, roomTeamsData);

            Debug.Log($"✓ 玩家已添加到 Team JSON");
            Debug.Log($"  - 玩家ID: {playerId}");
            Debug.Log($"  - 玩家名: {playerName}");
            Debug.Log($"  - 队伍: {targetTeamName}");
            Debug.Log($"  - {targetTeamName}现在有 {targetTeam.players.Count} 个玩家");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ 添加玩家到 Team JSON 失败: {e.Message}");
        }
    }

    /// <summary>
    /// 删除玩家（currentPlayers -1）
    /// ✓ 新增：删除后实时同步到 Team JSON
    /// </summary>
    public void DeletePlayer( )
    {
        if (string.IsNullOrEmpty(currentRoomId))
        {
            Debug.LogError("❌ 没有当前房间ID，无法删除玩家");
            return;
        }

        // ✓ 步骤1: 从JSON重新加载最新的房间数据
        Room currentRoom = RoomDataManager.Instance.GetRoomById(currentRoomId);

        if (currentRoom == null)
        {
            Debug.LogError($"❌ 无法找到房间: {currentRoomId}");
            return;
        }

        // ✓ 步骤2: 检查玩家数是否已为0
        if (currentRoom.currentPlayers <= 0)
        {
            Debug.LogWarning($"⚠ 房间 {currentRoom.roomId} 没有玩家可删除");
            return;
        }

        // ✓ 步骤3: 玩家数 -1
        currentRoom.currentPlayers--;
        Debug.Log($"✓ 玩家已删除");
        Debug.Log($"  - 房间ID: {currentRoom.roomId}");
        Debug.Log($"  - 当前玩家数: {currentRoom.currentPlayers}/{currentRoom.maxPlayers}");

        // ✓ 步骤4: 立即保存更新到 JSON 文件
        UpdateRoomData(currentRoom);

        // ✓ 【新增】步骤5: 从最后一个玩家中获取ID并删除
        Debug.Log("→ 从 Team JSON 中移除最后一个玩家");
        RemoveLastPlayerFromTeamJson(currentRoom);
    }

    /// <summary>
    /// ✓ 新增方法：从 Team JSON 中移除最后一个玩家
    /// </summary>
    private void RemoveLastPlayerFromTeamJson( Room currentRoom )
    {
        try
        {
            RoomTeamsData roomTeamsData = TeamJsonFileHandler.Instance.LoadTeamsData(currentRoomId);
            if (roomTeamsData == null) return;

            // 优先从蓝队删除，再从红队删除
            TeamInfo blueTeam = roomTeamsData.teams.Find(t => t.teamId == currentRoom.teamBlueId);
            TeamInfo redTeam = roomTeamsData.teams.Find(t => t.teamId == currentRoom.teamRedId);

            if (blueTeam != null && blueTeam.players.Count > 0)
            {
                string removedPlayerId = blueTeam.players[ blueTeam.players.Count - 1 ].playerId;
                blueTeam.players.RemoveAt(blueTeam.players.Count - 1);
                blueTeam.alivePlayerCount = blueTeam.players.Count;
                Debug.Log($"✓ 从蓝队移除玩家: {removedPlayerId}");
            }
            else if (redTeam != null && redTeam.players.Count > 0)
            {
                string removedPlayerId = redTeam.players[ redTeam.players.Count - 1 ].playerId;
                redTeam.players.RemoveAt(redTeam.players.Count - 1);
                redTeam.alivePlayerCount = redTeam.players.Count;
                Debug.Log($"✓ 从红队移除玩家: {removedPlayerId}");
            }

            TeamJsonFileHandler.Instance.SaveTeamsData(currentRoomId, roomTeamsData);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ 从 Team JSON 移除玩家失败: {e.Message}");
        }
    }

    /// <summary>
    /// 更新房间数据到 JSON 文件
    /// ★ 关键改进（第一份代码）：支持完整的房间字段更新（包括分数系数、配置等）
    /// ★ 关键改进（第二份代码）：仅更新玩家数的优化逻辑
    /// 当调用 UpdateRoomData() 时，自动检测传入的房间对象字段状态，择优更新
    /// </summary>
    public void UpdateRoomData( Room updatedRoom )
    {
        if (updatedRoom == null)
        {
            Debug.LogError("❌ 传入的房间对象为空，无法更新");
            return;
        }

        List<Room> rooms = JsonFileHandler.Instance.LoadRoomsData();

        if (rooms == null || rooms.Count == 0)
        {
            Debug.LogError("❌ 房间列表为空");
            return;
        }

        // ★ 找到对应的房间并更新
        bool found = false;
        for (int i = 0; i < rooms.Count; i++)
        {
            if (rooms[ i ].roomId == updatedRoom.roomId)
            {
                // ★ 基础字段（始终更新）
                rooms[ i ].roomName = updatedRoom.roomName;
                rooms[ i ].currentPlayers = updatedRoom.currentPlayers;
                rooms[ i ].mapId = updatedRoom.mapId;
                rooms[ i ].maxPlayers = updatedRoom.maxPlayers;
                rooms[ i ].state = updatedRoom.state;

                // ★ 新增（第二份代码的优化）：游戏配置字段（仅当有值时更新）
                if (updatedRoom.maxPlayerHealth > 0)
                {
                    rooms[ i ].maxPlayerHealth = updatedRoom.maxPlayerHealth;
                }
                if (!string.IsNullOrEmpty(updatedRoom.gameMode))
                {
                    rooms[ i ].gameMode = updatedRoom.gameMode;
                }
                if (updatedRoom.countdownSeconds > 0)
                {
                    rooms[ i ].countdownSeconds = updatedRoom.countdownSeconds;
                }

                // ★ 新增（第一份代码的完整功能）：分数系数配置
                if (updatedRoom.scoreCoefficients != null)
                {
                    rooms[ i ].scoreCoefficients = new Room.ScoreCoefficients
                    {
                        baseScore = updatedRoom.scoreCoefficients.baseScore,
                        killCoefficient = updatedRoom.scoreCoefficients.killCoefficient,
                        deathCoefficient = updatedRoom.scoreCoefficients.deathCoefficient,
                        assistCoefficient = updatedRoom.scoreCoefficients.assistCoefficient
                    };

                    Debug.Log($"✓ 分数系数已更新:");
                    Debug.Log($"  - 基础分: {rooms[ i ].scoreCoefficients.baseScore}");
                    Debug.Log($"  - 击杀系数: {rooms[ i ].scoreCoefficients.killCoefficient}");
                    Debug.Log($"  - 死亡系数: {rooms[ i ].scoreCoefficients.deathCoefficient}");
                    Debug.Log($"  - 助攻系数: {rooms[ i ].scoreCoefficients.assistCoefficient}");
                }

                found = true;
                Debug.Log($"✓ 房间 {updatedRoom.roomId} 已更新（玩家数: {updatedRoom.currentPlayers}）");
                break;
            }
        }

        if (!found)
        {
            Debug.LogError($"❌ 未找到房间 {updatedRoom.roomId}，无法更新");
            return;
        }

        // ✓ 保存更新后的房间列表到 JSON 文件
        JsonFileHandler.Instance.SaveRoomsData(rooms);
        Debug.Log($"✓ 房间数据已保存到JSON文件");
    }

    /// <summary>
    /// ✓ 新增方法：通过 UI 统计的玩家数直接更新房间数据
    /// 这是第二份代码提供的优化方法，用于在确认按钮时调用
    /// ★ 优化：仅更新 currentPlayers 字段，性能更高
    /// </summary>
    public void UpdateRoomPlayerCountByObject( Room updatedRoom )
    {
        if (updatedRoom == null)
        {
            Debug.LogError("❌ 传入的房间对象为空，无法更新");
            return;
        }

        // ✓ 步骤1: 加载所有房间列表
        List<Room> rooms = JsonFileHandler.Instance.LoadRoomsData();

        if (rooms == null || rooms.Count == 0)
        {
            Debug.LogError("❌ 房间列表为空");
            return;
        }

        // ✓ 步骤2: 找到对应的房间并更新
        bool found = false;
        for (int i = 0; i < rooms.Count; i++)
        {
            if (rooms[ i ].roomId == updatedRoom.roomId)
            {
                rooms[ i ].currentPlayers = updatedRoom.currentPlayers;  // ✓ 仅更新玩家数
                found = true;
                Debug.Log($"✓ [确认更新] 房间 {updatedRoom.roomId} 的玩家数已更新为 {updatedRoom.currentPlayers}");
                break;
            }
        }

        if (!found)
        {
            Debug.LogError($"❌ 未找到房间 {updatedRoom.roomId}，无法更新");
            return;
        }

        // ✓ 步骤3: 保存更新后的房间列表到 JSON 文件
        JsonFileHandler.Instance.SaveRoomsData(rooms);
        Debug.Log($"✓ [确认更新] 房间数据已保存到JSON文件");
    }

    /// <summary>
    /// ✓ 新增方法：同步玩家到 Team JSON 文件
    /// 在创建或删除玩家时调用
    /// </summary>
    public void SyncPlayersToTeamJson( Transform redTeamPanel, Transform blueTeamPanel )
    {
        if (string.IsNullOrEmpty(currentRoomId))
        {
            Debug.LogError("❌ 没有当前房间ID，无法同步玩家");
            return;
        }

        try
        {
            Debug.Log($"========== 同步玩家到 Team JSON: {currentRoomId} ==========");

            // 获取当前房间
            Room currentRoom = RoomDataManager.Instance.GetRoomById(currentRoomId);
            if (currentRoom == null)
            {
                Debug.LogError($"❌ 无法找到房间: {currentRoomId}");
                return;
            }

            // 加载 Team 数据
            RoomTeamsData roomTeamsData = TeamJsonFileHandler.Instance.LoadTeamsData(currentRoomId);
            if (roomTeamsData == null)
            {
                Debug.LogError($"❌ 无法加载 Team JSON: {currentRoomId}");
                return;
            }

            // 清空现有玩家列表
            foreach (TeamInfo team in roomTeamsData.teams)
            {
                team.players.Clear();
            }

            // ✓ 同步红队玩家
            if (redTeamPanel != null)
            {
                TeamInfo redTeam = roomTeamsData.teams.Find(t => t.teamId == currentRoom.teamRedId);
                if (redTeam != null)
                {
                    for (int i = 0; i < redTeamPanel.childCount; i++)
                    {
                        Transform playerTransform = redTeamPanel.GetChild(i);
                        DraggablePlayerButton playerBtn = playerTransform.GetComponent<DraggablePlayerButton>();

                        if (playerBtn != null)
                        {
                            TeamPlayer teamPlayer = new TeamPlayer(playerBtn.playerId, playerBtn.playerName);
                            redTeam.players.Add(teamPlayer);
                            Debug.Log($"  → 红队玩家已添加: {playerBtn.playerName} (ID={playerBtn.playerId})");
                        }
                    }
                    redTeam.alivePlayerCount = redTeam.players.Count;
                    Debug.Log($"✓ 红队同步完成：{redTeam.players.Count} 人");
                }
            }

            // ✓ 同步蓝队玩家
            if (blueTeamPanel != null)
            {
                TeamInfo blueTeam = roomTeamsData.teams.Find(t => t.teamId == currentRoom.teamBlueId);
                if (blueTeam != null)
                {
                    for (int i = 0; i < blueTeamPanel.childCount; i++)
                    {
                        Transform playerTransform = blueTeamPanel.GetChild(i);
                        DraggablePlayerButton playerBtn = playerTransform.GetComponent<DraggablePlayerButton>();

                        if (playerBtn != null)
                        {
                            TeamPlayer teamPlayer = new TeamPlayer(playerBtn.playerId, playerBtn.playerName);
                            blueTeam.players.Add(teamPlayer);
                            Debug.Log($"  → 蓝队玩家已添加: {playerBtn.playerName} (ID={playerBtn.playerId})");
                        }
                    }
                    blueTeam.alivePlayerCount = blueTeam.players.Count;
                    Debug.Log($"✓ 蓝队同步完成：{blueTeam.players.Count} 人");
                }
            }

            // 保存更新
            TeamJsonFileHandler.Instance.SaveTeamsData(currentRoomId, roomTeamsData);

            Debug.Log($"========== Team JSON 同步完成 ==========");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ 同步玩家失败: {e.Message}\n{e.StackTrace}");
        }
    }

    /// <summary>
    /// 获取当前房间的玩家数
    /// </summary>
    public int GetCurrentPlayerCount( )
    {
        if (string.IsNullOrEmpty(currentRoomId))
        {
            return 0;
        }

        Room currentRoom = RoomDataManager.Instance.GetRoomById(currentRoomId);
        if (currentRoom == null)
        {
            return 0;
        }

        return currentRoom.currentPlayers;
    }

    /// <summary>
    /// 获取当前房间的最大玩家数
    /// </summary>
    public int GetMaxPlayerCount( )
    {
        if (string.IsNullOrEmpty(currentRoomId))
        {
            return 0;
        }

        Room currentRoom = RoomDataManager.Instance.GetRoomById(currentRoomId);
        if (currentRoom == null)
        {
            return 0;
        }

        return currentRoom.maxPlayers;
    }

    /// <summary>
    /// 获取当前房间信息
    /// </summary>
    public Room GetCurrentRoom( )
    {
        if (string.IsNullOrEmpty(currentRoomId))
        {
            return null;
        }

        return RoomDataManager.Instance.GetRoomById(currentRoomId);
    }

    // 单例访问方法
    public static RoomDetailManager Instance
    {
        get
        {
            if (instance == null)
            {
                Debug.LogError("❌ RoomDetailManager 单例未初始化");
            }
            return instance;
        }
    }
}