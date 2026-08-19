using UnityEngine;
using System;
using System.Collections.Generic;
using System.Collections;

/// <summary>
/// 房间数据管理器（业务逻辑层）
/// 负责房间数据的生成、管理和业务逻辑
/// </summary>
public class RoomDataManager : MonoBehaviour
{
    private static RoomDataManager instance;

    [Header("★ 房间已满弹窗配置")]
    [SerializeField] private GameObject roomFullPopupPanel;  // 房间已满提示弹窗 Panel


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
    /// 创建新房间并保存数据
    /// ✓ 新增：同时创建对应的 Team JSON 文件
    /// ★ 改进：支持房间ID限制在1-10，删除后优先补位
    /// </summary>
    public Room CreateAndSaveNewRoom( )
    {
        Debug.Log("========== 开始创建新房间 ==========");

        // ★ 获取下一个房间ID（新逻辑）
        int nextRoomId = GetNextRoomId();

        if (nextRoomId == -1)
        {
            Debug.LogError("❌ 无法创建房间：房间数量已达上限或ID生成失败");

            // ★ 新增：显示房间已满弹窗
            ShowRoomFullPopup();

            return null;
        }


        int nextTeamId = GetNextTeamId();

        string newRoomId = "room_" + nextRoomId.ToString("D3");

        // ✓ 关键修改：gameId 与 roomId 对应
        string newGameId = "game_" + nextRoomId.ToString("D3");

        // ✓ 关键：RedTeam 和 BlueTeam 使用相同的后缀数字
        string teamIdRed = "RedTeam_" + nextTeamId;
        string teamIdBlue = "BlueTeam_" + nextTeamId;

        // 创建 Room 对象
        Room newRoom = new Room
        {
            roomId = newRoomId,
            roomName = "新手房间",
            mapId = "map_001",
            maxPlayers = 6,
            currentPlayers = 0,
            state = "waiting",
            gameId = newGameId,
            countdownSeconds = 300,
            startTime = -1,
            remainingTime = 300,
            teamRedId = teamIdRed,
            teamBlueId = teamIdBlue
        };

        // ✓ 步骤1: 加载现有房间列表
        List<Room> rooms = JsonFileHandler.Instance.LoadRoomsData();

        // ✓ 步骤2: 检查房间是否已存在
        bool roomExists = false;
        foreach (Room room in rooms)
        {
            if (room.roomId == newRoomId)
            {
                roomExists = true;
                break;
            }
        }

        // ✓ 步骤3: 如果不存在则添加
        if (!roomExists)
        {
            rooms.Add(newRoom);
            Debug.Log($"✓ 房间 {newRoomId} 已添加");
        }
        else
        {
            Debug.LogWarning($"⚠ 房间 {newRoomId} 已存在，跳过添加");
        }

        // ✓ 步骤4: 保存房间列表到 Room.json
        JsonFileHandler.Instance.SaveRoomsData(rooms);

        // ✓ 步骤5: 更新 Rooms.json（ID列表）
        UpdateRoomsList(newRoomId, newRoom.roomName);

        // ✓ 【新增】步骤6: 创建对应的 Team JSON 文件
        Debug.Log("→ 步骤6: 创建 Team JSON 文件");
        if (TeamJsonFileHandler.Instance != null)
        {
            TeamJsonFileHandler.Instance.SaveTeamsDataForNewRoom(newRoomId, teamIdRed, teamIdBlue);
            Debug.Log($"✓ Team JSON 文件已创建");
        }
        else
        {
            Debug.LogError("❌ TeamJsonFileHandler 单例未初始化");
        }

        Debug.Log($"✓ 房间信息已保存");
        Debug.Log($"  - 房间ID: {newRoomId}");
        Debug.Log($"  - 游戏ID: {newGameId}");
        Debug.Log($"  - 房间名称: {newRoom.roomName}");
        Debug.Log($"  - 红方队伍: {teamIdRed}");
        Debug.Log($"  - 蓝方队伍: {teamIdBlue}");
        Debug.Log($"  - 当前总房间数: {rooms.Count}");

        Debug.Log("========== 房间创建完成 ==========");
        return newRoom;
    }


    /// <summary>
    /// 更新 Rooms.json 的ID列表
    /// ✅ 优化：使用LINQ的Exists()，添加默认参数
    /// </summary>
    private void UpdateRoomsList( string roomId, string roomName = "新手房间" )
    {
        RoomsList roomsList = JsonFileHandler.Instance.LoadRoomsList();
        bool exists = roomsList.rooms.Exists(r => r.roomId == roomId);
        if (!exists)
        {
            roomsList.rooms.Add(new RoomsList.RoomInfo
            {
                roomId = roomId,
                roomName = roomName
            });
            Debug.Log($"✓ 房间 {roomId} 已添加到 Rooms.json");
        }
        JsonFileHandler.Instance.SaveRoomsList(roomsList);
    }

    /// <summary>
    /// 获取下一个房间ID（改进版：支持1-10范围，删除后优先补位）
    /// </summary>
    private int GetNextRoomId( )
    {
        // ★ 步骤1: 检查房间数量是否已达上限
        RoomsList roomsList = JsonFileHandler.Instance.LoadRoomsList();
        if (roomsList == null || roomsList.rooms == null)
        {
            roomsList = new RoomsList();
        }

        if (roomsList.rooms.Count >= 10)
        {
            Debug.LogError("❌ 房间数量已达上限(10)，无法创建新房间");
            return -1;  // 返回-1表示失败
        }

        // ★ 步骤2: 获取已使用的房间ID列表
        HashSet<int> usedIds = new HashSet<int>();
        foreach (var roomInfo in roomsList.rooms)
        {
            string numberPart = roomInfo.roomId.Replace("room_", "");
            if (int.TryParse(numberPart, out int id) && id >= 1 && id <= 10)
            {
                usedIds.Add(id);
            }
        }

        // ★ 步骤3: 从小到大查找第一个未被使用的ID（1-10范围内）
        for (int i = 1; i <= 10; i++)
        {
            if (!usedIds.Contains(i))
            {
                Debug.Log($"✓ 找到可用房间ID: {i} (已使用: {string.Join(", ", usedIds)})");
                return i;
            }
        }

        Debug.LogError("❌ 无法获取有效的房间ID");
        return -1;
    }


    /// <summary>
    /// 获取下一个队伍ID
    /// </summary>
    private int GetNextTeamId( )
    {
        RoomsList roomsList = JsonFileHandler.Instance.LoadRoomsList();
        if (roomsList == null || roomsList.rooms == null || roomsList.rooms.Count == 0)
            return 1;
        return roomsList.rooms.Count + 1;
    }

    /// <summary>
    /// 获取所有房间
    /// </summary>
    public List<Room> GetAllRooms( )
    {
        return JsonFileHandler.Instance.LoadRoomsData();
    }

    /// <summary>
    /// 获取指定房间
    /// </summary>
    public Room GetRoomById( string roomId )
    {
        return JsonFileHandler.Instance.GetRoomById(roomId);
    }

    /// <summary>
    /// 删除指定房间
    /// ✓ 新增：同时删除对应的 Team JSON 文件
    /// </summary>
    public void DeleteRoom( string roomId )
    {
        // ✓ 步骤1: 从 Room.json 删除房间
        List<Room> rooms = JsonFileHandler.Instance.LoadRoomsData();
        rooms.RemoveAll(room => room.roomId == roomId);
        JsonFileHandler.Instance.SaveRoomsData(rooms);

        // ✓ 步骤2: 从 Rooms.json 删除ID
        RoomsList roomsList = JsonFileHandler.Instance.LoadRoomsList();
        roomsList.rooms.RemoveAll(r => r.roomId == roomId);
        JsonFileHandler.Instance.SaveRoomsList(roomsList);

        // ✓ [关键]步骤3: 删除 Team JSON 文件  
        if (TeamJsonFileHandler.Instance != null)
        {
            TeamJsonFileHandler.Instance.DeleteTeamJsonFile(roomId);
            Debug.Log($"✓ Team JSON 文件已删除");
        }

        Debug.Log($"✓ 房间 {roomId} 已删除（包括 Team JSON 文件）");
    }

    /// <summary>
    /// ★ 新增：显示房间已满弹窗（1.5s后自动消失）
    /// </summary>
    private void ShowRoomFullPopup( )
    {
        if (roomFullPopupPanel == null)
        {
            Debug.LogWarning("⚠ roomFullPopupPanel 未在 Inspector 中配置");
            return;
        }

        // 显示弹窗
        roomFullPopupPanel.SetActive(true);
        Debug.Log("[RoomDataManager] 房间已满弹窗已显示");

        // 启动自动隐藏协程
        StartCoroutine(HidePopupAfterDelay(1.5f));
    }

    /// <summary>
    /// ★ 新增：延迟隐藏弹窗的协程
    /// </summary>
    private IEnumerator HidePopupAfterDelay( float delaySeconds )
    {
        yield return new WaitForSeconds(delaySeconds);

        if (roomFullPopupPanel != null)
        {
            roomFullPopupPanel.SetActive(false);
            Debug.Log("[RoomDataManager] 房间已满弹窗已自动关闭");
        }
    }


    // 单例访问方法
    public static RoomDataManager Instance
    {
        get
        {
            if (instance == null)
            {
                Debug.LogError("❌ RoomDataManager 单例未初始化");
            }
            return instance;
        }
    }
}