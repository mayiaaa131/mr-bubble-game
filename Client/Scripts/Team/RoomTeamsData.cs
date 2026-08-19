using System;
using System.Collections.Generic;

/// <summary>
/// 单个房间包含的两个队伍数据（用于Team JSON）
/// 结构: Team_room_001.json 包含红队和蓝队两个队伍
/// </summary>
[Serializable]
public class RoomTeamsData
{
    public string roomId;             // 房间ID
    public List<TeamInfo> teams;      // 红队和蓝队（最多2个）

    public RoomTeamsData()
    {
        teams = new List<TeamInfo>();
    }

    public RoomTeamsData(string roomId)
    {
        this.roomId = roomId;
        teams = new List<TeamInfo>();
    }
}
