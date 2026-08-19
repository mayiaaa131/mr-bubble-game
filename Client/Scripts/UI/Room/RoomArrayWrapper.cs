using System;
using System.Collections.Generic;

/// <summary>
/// 房间数组包装器（用于 JsonUtility 支持数组序列化）
/// </summary>
[Serializable]
public class RoomArrayWrapper
{
    public Room[] rooms;

    public RoomArrayWrapper(List<Room> roomList)
    {
        rooms = roomList.ToArray();
    }

    public List<Room> ToList()
    {
        return new List<Room>(rooms);
    }
}
