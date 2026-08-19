using UnityEngine;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

/// <summary>
/// 房间专用的地图管理器（非单例版本）
/// ★ 改造核心：
/// 1. 删除 public static Instance
/// 2. 不需要依赖注入（只读取地图数据并广播）
/// 3. 所有引用改为通过初始化的字段调用
/// </summary>
public class ServerMapManager : MonoBehaviour
{
    private string roomId = "";
    private MapInfo currentMap;
    private string mapDataFolderPath = "Assets/json/MapData";
    private bool isInitialized = false;

    /// <summary>
    /// 初始化Manager（由RoomGameManager调用）
    /// </summary>
    public void Initialize(string roomId)
    {
        if (isInitialized) return;

        this.roomId = roomId;
        Debug.Log($"[ServerMapManager-{roomId}] 初始化中...");

        // 从 Room JSON 读取 mapId
        LoadMapDataFromRoom(roomId);

        isInitialized = true;
        Debug.Log($"[ServerMapManager-{roomId}] ✅ 初始化完成");
    }

    /// <summary>
    /// 从房间JSON读取地图ID并加载地图
    /// </summary>
    private void LoadMapDataFromRoom(string roomId)
    {
        try
        {
            // 步骤1：从 Room JSON 获取 mapId
            Room room = RoomDataManager.Instance.GetRoomById(roomId);

            if (room == null)
            {
                Debug.LogError($"[ServerMapManager-{roomId}] ❌ 无法读取房间配置");
                return;
            }

            if (string.IsNullOrEmpty(room.mapId))
            {
                Debug.LogWarning($"[ServerMapManager-{roomId}] ⚠️ 房间未配置mapId");
                return;
            }

            // 步骤2：使用 mapId 加载地图数据
            LoadMapFromJson(roomId, room.mapId);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ServerMapManager-{roomId}] ❌ 读取房间配置失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 从JSON文件加载地图数据
    /// </summary>
    public bool LoadMapFromJson(string roomId, string mapName)
    {
        if (string.IsNullOrEmpty(mapName))
        {
            Debug.LogWarning($"[ServerMapManager-{roomId}] ⚠️ 地图名称为空");
            return false;
        }

        try
        {
            string filePath = Path.Combine(mapDataFolderPath, $"{mapName}.json");

            if (!File.Exists(filePath))
            {
                Debug.LogError($"[ServerMapManager-{roomId}] ❌ 地图文件不存在: {filePath}");
                return false;
            }

            string json = File.ReadAllText(filePath, Encoding.UTF8);
            currentMap = JsonUtility.FromJson<MapInfo>(json);

            if (currentMap == null)
            {
                Debug.LogError($"[ServerMapManager-{roomId}] ❌ JSON 解析失败: {mapName}");
                return false;
            }

            if (currentMap.objects == null)
            {
                currentMap.objects = new List<MapGameObject>();
            }

            Debug.Log($"[ServerMapManager-{roomId}] ✅ 地图加载成功: {mapName} (包含 {currentMap.objects.Count} 个物体)");
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ServerMapManager-{roomId}] ❌ 加载地图异常: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 生成地图广播消息
    /// </summary>
    public MapBroadcastMessage GenerateMapBroadcastMessage()
    {
        if (currentMap == null)
        {
            Debug.LogWarning($"[ServerMapManager-{roomId}] ⚠️ 当前地图为空");
            return null;
        }

        long serverTime = System.DateTime.Now.Ticks / 10000;

        MapBroadcastMessage msg = new MapBroadcastMessage
        {
            type = "MapData",
            roomId = roomId,
            mapName = currentMap.mapName ?? "Unknown",
            timestamp = serverTime
        };

        if (currentMap.objects != null)
        {
            foreach (var obj in currentMap.objects)
            {
                msg.objects.Add(obj);
            }
        }

        Debug.Log($"[ServerMapManager-{roomId}] 📍 生成地图消息，包含 {msg.objects.Count} 个物体");
        return msg;
    }

    /// <summary>
    /// 获取地图物体总数
    /// </summary>
    public int GetMapObjectCount()
    {
        if (currentMap == null || currentMap.objects == null)
            return 0;
        return currentMap.objects.Count;
    }

    /// <summary>
    /// 获取当前地图信息
    /// </summary>
    public MapInfo GetCurrentMap()
    {
        return currentMap;
    }

    /// <summary>
    /// 获取特定索引的地图物体
    /// </summary>
    public MapGameObject GetMapObject(int index)
    {
        if (currentMap == null || currentMap.objects == null || index < 0 || index >= currentMap.objects.Count)
        {
            Debug.LogWarning($"[ServerMapManager-{roomId}] ⚠️ 地图物体索引越界: {index}");
            return null;
        }

        return currentMap.objects[index];
    }

    /// <summary>
    /// 清理数据
    /// </summary>
    public void Cleanup()
    {
        Debug.Log($"[ServerMapManager-{roomId}] → 开始清理...");

        try
        {
            currentMap = null;
            isInitialized = false;

            Debug.Log($"[ServerMapManager-{roomId}] ✅ 已清理");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ServerMapManager-{roomId}] ❌ 清理失败: {ex.Message}");
        }
    }
}
