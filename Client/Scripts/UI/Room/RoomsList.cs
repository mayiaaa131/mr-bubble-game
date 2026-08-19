using System;
using System.Collections.Generic;

/// <summary>
/// Rooms 列表数据结构（改进：存储房间ID + 名称）
/// </summary>
[Serializable]
public class RoomsList
{
    public List<RoomInfo> rooms;  // ✅ 改为存储房间信息对象

    [Serializable]
    public class RoomInfo
    {
        public string roomId;      // 房间ID
        public string roomName;    // 房间名称
    }

    public RoomsList()
    {
        rooms = new List<RoomInfo>();
    }
}
