// ============================================
// 文件路径：Assets/scripts/Room/ServerRoomManager.cs
// ============================================
using System.Collections.Generic;
using UnityEngine;

public class ServerRoomManager : MonoBehaviour
{
    public static ServerRoomManager Instance;

    private Dictionary<string, RoomInstance> _rooms = new();

    private string _currentRoomId = "";

    void Awake( )
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            var al = GetComponent<AudioListener>();
            if (al != null) al.enabled = false;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// ★ 修复版：创建房间时同时保存到 JSON
    /// </summary>
    public RoomInstance CreateRoom( string roomName, string mapId,
                                   int maxPlayers = 4, int countdownSeconds = 300 )
    {
        // ★ 步骤1：通过 RoomDataManager 创建房间并保存到 JSON
        Room newRoom = RoomDataManager.Instance.CreateAndSaveNewRoom();

        if (newRoom == null)
        {
            Debug.LogError("[ServerRoomManager] ❌ 创建房间失败");
            return null;
        }

        string roomId = newRoom.roomId;

        // ★ 步骤2：更新房间信息（名称、地图等）
        newRoom.roomName = roomName;
        newRoom.mapId = mapId;
        newRoom.maxPlayers = maxPlayers;
        newRoom.countdownSeconds = countdownSeconds;
        newRoom.state = "waiting";

        // 保存更新到 JSON
        RoomDetailManager.Instance.UpdateRoomData(newRoom);

        // ★ 步骤3：创建运行时 RoomInstance
        GameObject go = new GameObject($"Room_{roomId}");
     

        RoomInstance room = go.AddComponent<RoomInstance>();
        room.Initialize(roomId);

        _rooms[ roomId ] = room;
        _currentRoomId = roomId;

        Debug.Log($"[ServerRoomManager] ✅ 创建房间成功");
        Debug.Log($"  - 房间ID: {roomId}");
        Debug.Log($"  - 房间名: {roomName}");
        Debug.Log($"  - 地图ID: {mapId}");
        Debug.Log($"  - 房间已保存到 JSON");

        return room;
    }


    /// <summary>
    /// 获取当前房间（包含详细的验证）
    /// </summary>
    public RoomInstance GetCurrentRoom( )
    {
        // ✅ 第一层检查：ID 是否为空
        if (string.IsNullOrEmpty(_currentRoomId))
        {
            Debug.LogWarning("[ServerRoomManager] 当前房间ID为空");
            return null;
        }

        // ✅ 第二层检查：房间是否仍在字典中
        if (!_rooms.TryGetValue(_currentRoomId, out var room))
        {
            Debug.LogError($"[ServerRoomManager] 房间 {_currentRoomId} 不在运行时字典中，可能已被删除");
            _currentRoomId = "";  // 清空无效ID
            return null;
        }

        // ✅ 第三层检查：房间对象是否有效
        if (room == null)
        {
            Debug.LogError("[ServerRoomManager] 房间对象为 null");
            _currentRoomId = "";
            return null;
        }

        return room;
    }

    /// <summary>
    /// 添加房间到字典（供外部使用）
    /// </summary>
    public void AddRoomToDict( string roomId, RoomInstance room )
    {
        _rooms[ roomId ] = room;
        Debug.Log($"✓ 房间已添加到字典: {roomId}");
    }

    /// <summary>
    /// 设置当前房间ID
    /// </summary>
    public void SetCurrentRoom( string roomId )
    {
        _currentRoomId = roomId;
        Debug.Log($"✓ 当前房间已设置: {roomId}");
    }

    /// <summary>
    /// 获取指定房间（如果不存在则返回 null）
    /// </summary>
    public RoomInstance GetRoom( string roomId )
    {
        return _rooms.TryGetValue(roomId, out var room) ? room : null;
    }


    public bool JoinRoom( string roomId, string playerId, string playerName )
    {
        if (!_rooms.TryGetValue(roomId, out var room))
        {
            Debug.LogWarning($"[ServerRoomManager] 房间 {roomId} 不存在");
            return false;
        }
        return room.AddPlayer(playerId, playerName);
    }

    public void OnRoomFinished( string roomId, GameResult result )
    {
        Debug.Log($"[ServerRoomManager] 房间 {roomId} 结算 → " +
                  $"胜：{result.winningTeam} {result.winningTeamScore}:{result.losingTeamScore}");

        if (_rooms.TryGetValue(roomId, out var room))
        {
            _rooms.Remove(roomId);
            room.Cleanup();
        }

        if (_currentRoomId == roomId)
            _currentRoomId = "";
    }

    /// <summary>
    /// ★ 新增：删除指定房间实例
    /// </summary>
    public void RemoveRoom( string roomId )
    {
        if (_rooms.ContainsKey(roomId))
        {
            RoomInstance removedRoom = _rooms[ roomId ];
            _rooms.Remove(roomId);

            Debug.Log($"✓ 房间实例已从 ServerRoomManager 移除: {roomId}");

            // 如果删除的是当前房间，则清空当前房间ID
            if (_currentRoomId == roomId)
            {
                _currentRoomId = "";
                Debug.Log($"✓ 当前房间引用已清空");
            }
        }
        else
        {
            Debug.LogWarning($"⚠ 房间不存在: {roomId}");
        }
    }





    public IEnumerable<RoomInstance> GetAllRooms( ) => _rooms.Values;

    /// <summary>
    /// ★ 改进：打印所有房间状态（使用 Room 而不是 RoomData）
    /// </summary>
    public void PrintAllRooms( )
    {
        Debug.Log("————————当前所有房间列表————————");

        if (_rooms.Count == 0)
        {
            Debug.Log("【空】没有房间");
        }
        else
        {
            int idx = 1;
            foreach (var kvp in _rooms)
            {
                var room = kvp.Value;

                // ★ 直接访问 room.roomData（类型为 Room）
                Debug.Log($"[{idx}] roomId={room.roomData.roomId}" +
                          $" | 房间名={room.roomData.roomName}" +
                          $" | 地图={room.roomData.mapId}" +
                          $" | 状态={room.roomData.state}" +
                          $" | 玩家={room.roomData.currentPlayers}/{room.roomData.maxPlayers}");
                idx++;
            }
        }

        Debug.Log($"→ 当前房间: {(string.IsNullOrEmpty(_currentRoomId) ? "无" : _currentRoomId)}\n");
    }
}
