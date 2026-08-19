using UnityEngine;
using System;
using System.IO;
using System.Collections.Generic;
using System.Text;

/// <summary>
/// JSON 文件处理器（数据层）
/// 负责所有 JSON 文件的读写操作
/// ✅ 改进：支持新的 Rooms.json 格式
/// </summary>
public class JsonFileHandler : MonoBehaviour
{
    private static JsonFileHandler instance;

    [SerializeField] private string roomJsonPath = "Assets/json/Room.json";        // ✓ 房间详细信息
    [SerializeField] private string roomsListJsonPath = "Assets/json/Rooms.json";  // ✓ 房间ID + 名称列表

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
    /// 将 JSON 字符串写入文件
    /// </summary>
    public void WriteJsonToFile( string path, string jsonContent )
    {
        try
        {
            // 确保目录存在
            string directory = Path.GetDirectoryName(path);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            File.WriteAllText(path, jsonContent);
            Debug.Log($"✓ JSON 文件写入成功: {path}");
        }
        catch (Exception e)
        {
            Debug.LogError($"❌ 写入 JSON 文件失败: {path} - {e.Message}");
        }
    }

    /// <summary>
    /// 从文件读取 JSON 字符串
    /// </summary>
    public string ReadJsonFromFile( string path )
    {
        try
        {
            if (File.Exists(path))
            {
                return File.ReadAllText(path);
            }
            else
            {
                Debug.LogWarning($"⚠ JSON 文件不存在: {path}");
                return null;
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"❌ 读取 JSON 文件失败: {path} - {e.Message}");
            return null;
        }
    }

    // ==================== Room.json 操作（房间数组） ====================

    /// <summary>
    /// 保存房间列表到 Room.json（直接数组格式）
    /// ✓ 关键：保存房间详细信息
    /// </summary>
    public void SaveRoomsData( List<Room> rooms, string customPath = null )
    {
        string path = customPath ?? roomJsonPath;

        try
        {
            // ✓ 直接手工构建数组 JSON
            StringBuilder sb = new StringBuilder();
            sb.Append("[\n");

            for (int i = 0; i < rooms.Count; i++)
            {
                // 序列化每个房间对象
                string roomJson = JsonUtility.ToJson(rooms[ i ], true);

                // 添加缩进（每行前加 4 个空格）
                string indentedJson = IndentJson(roomJson, 4);
                sb.Append(indentedJson);

                // 如果不是最后一个，添加逗号
                if (i < rooms.Count - 1)
                {
                    sb.Append(",");
                }
                sb.Append("\n");
            }

            sb.Append("]");

            string arrayJson = sb.ToString();
            WriteJsonToFile(path, arrayJson);
            Debug.Log($"✓ Room.json 已保存，共 {rooms.Count} 个房间的详细信息");
        }
        catch (Exception e)
        {
            Debug.LogError($"❌ 保存 Room.json 失败: {e.Message}");
        }
    }

    /// <summary>
    /// 为 JSON 字符串添加缩进
    /// </summary>
    private string IndentJson( string json, int spaces )
    {
        string indent = new string(' ', spaces);
        string[ ] lines = json.Split('\n');
        StringBuilder sb = new StringBuilder();

        for (int i = 0; i < lines.Length; i++)
        {
            sb.Append(indent);
            sb.Append(lines[ i ]);
            if (i < lines.Length - 1)
            {
                sb.Append("\n");
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// 从 Room.json 加载房间列表（直接数组格式）
    /// ✓ 关键：加载房间详细信息
    /// </summary>
    public List<Room> LoadRoomsData( string customPath = null )
    {
        string path = customPath ?? roomJsonPath;
        string json = ReadJsonFromFile(path);

        if (json == null || json.Trim() == "[]")
        {
            return new List<Room>();
        }

        try
        {
            // ✓ 包裹数组为 {"rooms":[...]} 格式，这样 JsonUtility 才能解析
            string wrappedJson = "{\"rooms\":" + json + "}";
            RoomArrayWrapper wrapper = JsonUtility.FromJson<RoomArrayWrapper>(wrappedJson);

            if (wrapper == null || wrapper.rooms == null)
            {
                return new List<Room>();
            }

            return wrapper.ToList();
        }
        catch (Exception e)
        {
            Debug.LogError($"❌ 解析 Room.json 失败: {e.Message}\n JSON 内容: {json}");
            return new List<Room>();
        }
    }

    // ==================== Rooms.json 操作（房间ID + 名称列表） ====================

    /// <summary>
    /// 保存房间ID + 名称列表到 Rooms.json
    /// ✅ 改进：保存ID + 名称对
    /// </summary>
    public void SaveRoomsList( RoomsList roomsList, string customPath = null )
    {
        string path = customPath ?? roomsListJsonPath;

        try
        {
            string roomsListJson = JsonUtility.ToJson(roomsList, true);
            WriteJsonToFile(path, roomsListJson);
            Debug.Log($"✓ Rooms.json 已保存，共 {roomsList.rooms.Count} 个房间");
        }
        catch (Exception e)
        {
            Debug.LogError($"❌ 保存 Rooms.json 失败: {e.Message}");
        }
    }

    /// <summary>
    /// 从 Rooms.json 加载房间ID + 名称列表
    /// ✅ 改进：加载ID + 名称对
    /// </summary>
    public RoomsList LoadRoomsList( string customPath = null )
    {
        string path = customPath ?? roomsListJsonPath;
        string json = ReadJsonFromFile(path);

        if (json == null)
        {
            return new RoomsList { rooms = new List<RoomsList.RoomInfo>() };
        }

        try
        {
            RoomsList roomsList = JsonUtility.FromJson<RoomsList>(json);
            if (roomsList == null || roomsList.rooms == null)
            {
                roomsList = new RoomsList { rooms = new List<RoomsList.RoomInfo>() };
            }
            return roomsList;
        }
        catch (Exception e)
        {
            Debug.LogError($"❌ 解析 Rooms.json 失败: {e.Message}");
            return new RoomsList { rooms = new List<RoomsList.RoomInfo>() };
        }
    }

    /// <summary>
    /// 更新 Rooms.json 中的房间名称
    /// ✓ 新增方法：用于更新房间列表中的房间信息
    /// </summary>
    public void UpdateRoomNameInList( string roomId, string newRoomName )
    {
        try
        {
            // ✓ 步骤1: 加载 Rooms.json
            RoomsList roomsList = LoadRoomsList();

            if (roomsList == null || roomsList.rooms == null)
            {
                Debug.LogError($"❌ RoomsList 为空，无法更新房间名称");
                return;
            }

            // ✓ 步骤2: 找到对应的房间并更新名称
            bool found = false;
            foreach (var roomInfo in roomsList.rooms)
            {
                if (roomInfo.roomId == roomId)
                {
                    string oldName = roomInfo.roomName;
                    roomInfo.roomName = newRoomName;
                    found = true;
                    Debug.Log($"✓ 房间名称已更新: {roomId}");
                    Debug.Log($"  - 旧名称: {oldName}");
                    Debug.Log($"  - 新名称: {newRoomName}");
                    break;
                }
            }

            if (!found)
            {
                Debug.LogWarning($"⚠ 未找到房间 {roomId}，无法更新名称");
                return;
            }

            // ✓ 步骤3: 保存更新后的列表到 Rooms.json
            SaveRoomsList(roomsList);
            Debug.Log($"✓ Rooms.json 已更新");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ 更新房间名称失败: {e.Message}");
        }
    }

    /// <summary>
    /// 根据房间ID获取单个房间
    /// </summary>
    public Room GetRoomById( string roomId )
    {
        List<Room> rooms = LoadRoomsData();
        if (rooms == null || rooms.Count == 0)
        {
            return null;
        }

        foreach (Room room in rooms)
        {
            if (room.roomId == roomId)
            {
                return room;
            }
        }

        Debug.LogWarning($"⚠ 未找到房间: {roomId}");
        return null;
    }

    /// <summary>
    /// 更新房间的地图ID
    /// ✓ 新增方法：用于更新房间的 mapId 字段
    /// </summary>
    public void UpdateMapIdInRoom( string roomId, string newMapId )
    {
        try
        {
            // ✓ 步骤1: 加载所有房间数据
            List<Room> rooms = LoadRoomsData();

            if (rooms == null || rooms.Count == 0)
            {
                Debug.LogError($"❌ 房间列表为空，无法更新地图ID");
                return;
            }

            // ✓ 步骤2: 找到对应的房间并更新 mapId
            bool found = false;
            foreach (var room in rooms)
            {
                if (room.roomId == roomId)
                {
                    string oldMapId = room.mapId;
                    room.mapId = newMapId;
                    found = true;
                    Debug.Log($"✓ 房间地图ID已更新: {roomId}");
                    Debug.Log($"  - 旧地图ID: {oldMapId}");
                    Debug.Log($"  - 新地图ID: {newMapId}");
                    break;
                }
            }

            if (!found)
            {
                Debug.LogWarning($"⚠ 未找到房间 {roomId}，无法更新地图ID");
                return;
            }

            // ✓ 步骤3: 保存更新后的房间列表到 Room.json
            SaveRoomsData(rooms);
            Debug.Log($"✓ Room.json 已更新");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ 更新地图ID失败: {e.Message}");
        }
    }


    // 单例访问方法
    public static JsonFileHandler Instance
    {
        get
        {
            if (instance == null)
            {
                Debug.LogError("❌ JsonFileHandler 单例未初始化");
            }
            return instance;
        }
    }
}
