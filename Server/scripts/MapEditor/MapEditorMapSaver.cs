using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// 地图数据 JSON 序列化 / 反序列化
/// 
/// 保存路径（PICO设备）：Application.persistentDataPath/Maps/
/// 通常是 /storage/emulated/0/Android/data/<包名>/files/Maps/
/// 
/// 服务端/客户端读取方式：
///   string json = File.ReadAllText(filePath);
///   MapData map = JsonUtility.FromJson<MapData>(json);
///   // 或用 Newtonsoft.Json（服务端更推荐）
///   // MapData map = JsonConvert.DeserializeObject<MapData>(json);
/// </summary>
public class MapEditorMapSaver : MonoBehaviour
{
    [Header("保存配置")]
    [Tooltip("地图文件夹名")]
    public string mapFolderName = "Maps";

    [Tooltip("地图文件名前缀，后面自动加时间戳")]
    public string mapFilePrefix = "Map";

    // 保存目录完整路径
    public string SaveDirectory =>
        Path.Combine(Application.persistentDataPath, mapFolderName);

    // ── 保存 ──────────────────────────────────────────

    /// <summary>
    /// 保存当前地图，返回保存的文件路径（失败返回null）
    /// </summary>
    /// 添加quaternion测试
    public string SaveMap(List<MapEditorManager.PlacedObjectData> placedObjects, Quaternion xrOriginRotation, string mapName = null)
    {
        try
        {
            // 确保目录存在
            Directory.CreateDirectory(SaveDirectory);

            // 构建数据
            MapData data = new MapData();
            data.mapName = mapName ?? $"{mapFilePrefix}_{DateTime.Now:yyyyMMdd_HHmmss}";
            data.savedAt = DateTime.Now.ToString("o"); // ISO 8601
            data.objects = new List<MapObjectData>();

            /*
            foreach (var placed in placedObjects)
            {
                if (placed.instance == null) continue;

                Transform t = placed.instance.transform;
                data.objects.Add(new MapObjectData
                {
                    prefabIndex = placed.prefabIndex,
                    prefabName = placed.instance.name.Replace("(Clone)", "").Trim(),
                    position = new SerializableVector3(t.position),
                    rotation = new SerializableVector3(t.eulerAngles),
                    scale = new SerializableVector3(t.localScale)
                });
            }*/
            foreach (var placed in placedObjects)
            {
                if (placed.instance == null) continue;

                Transform t = placed.instance.transform;

                //获取相对于mapRootContainer的坐标  
                Vector3 relativePos = t.localPosition;  // 因为parent已经是mapRootContainer  
                Vector3 relativeRot = t.localEulerAngles;
                Vector3 relativeScale = t.localScale;

                data.objects.Add(new MapObjectData
                {
                    prefabIndex = placed.prefabIndex,
                    prefabName = placed.instance.name.Replace("(Clone)", "").Trim(),
                    position = new SerializableVector3(relativePos),
                    rotation = new SerializableVector3(relativeRot),
                    scale = new SerializableVector3(relativeScale)
                });
            }

            // 序列化
            string json = JsonUtility.ToJson(data, prettyPrint: true);
            string fileName = data.mapName + ".json";
            string filePath = Path.Combine(SaveDirectory, fileName);

            File.WriteAllText(filePath, json);

            Debug.Log($"[MapSaver] 地图已保存：{filePath}\n共 {data.objects.Count} 个物体");
            return filePath;
        }
        catch (Exception e)
        {
            Debug.LogError($"[MapSaver] 保存失败：{e.Message}");
            return null;
        }
    }

    // ── 加载 ──────────────────────────────────────────

    /// <summary>
    /// 从文件路径加载地图数据
    /// </summary>
    public MapData LoadMap(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                Debug.LogWarning($"[MapSaver] 文件不存在：{filePath}");
                return null;
            }

            string json = File.ReadAllText(filePath);
            MapData data = JsonUtility.FromJson<MapData>(json);
            Debug.Log($"[MapSaver] 地图已加载：{data.mapName}，共 {data.objects.Count} 个物体");
            return data;
        }
        catch (Exception e)
        {
            Debug.LogError($"[MapSaver] 加载失败：{e.Message}");
            return null;
        }
    }

    /// <summary>
    /// 加载最新的地图文件
    /// </summary>
    public MapData LoadLatestMap()
    {
        string[] files = GetSavedMapPaths();
        if (files == null || files.Length == 0)
        {
            Debug.LogWarning("[MapSaver] 没有找到任何地图文件");
            return null;
        }

        // 按修改时间降序，取最新
        Array.Sort(files, (a, b) =>
            File.GetLastWriteTime(b).CompareTo(File.GetLastWriteTime(a)));

        return LoadMap(files[0]);
    }

    /// <summary>
    /// 获取所有已保存的地图文件路径列表
    /// </summary>
    public string[] GetSavedMapPaths()
    {
        if (!Directory.Exists(SaveDirectory)) return new string[0];
        return Directory.GetFiles(SaveDirectory, "*.json");
    }

    /// <summary>
    /// 根据地图数据在场景中实例化物体
    /// （供客户端/服务端还原地图用）
    /// </summary>
    public List<GameObject> InstantiateMap(MapData mapData, GameObject[] prefabTable, Transform parent = null)
    {
        var instances = new List<GameObject>();

        foreach (var objData in mapData.objects)
        {
            // 优先用 prefabIndex，越界时尝试按名字匹配
            GameObject prefab = null;
            if (objData.prefabIndex >= 0 && objData.prefabIndex < prefabTable.Length)
            {
                prefab = prefabTable[objData.prefabIndex];
            }
            else
            {
                // 按名字fallback
                prefab = Array.Find(prefabTable, p =>
                    p.name.Equals(objData.prefabName, StringComparison.OrdinalIgnoreCase));
            }

            if (prefab == null)
            {
                Debug.LogWarning($"[MapSaver] 找不到预制体：index={objData.prefabIndex} name={objData.prefabName}，跳过");
                continue;
            }

            GameObject go = Instantiate(prefab, parent);
            go.transform.position = objData.position.ToVector3();
            go.transform.eulerAngles = objData.rotation.ToVector3();
            go.transform.localScale = objData.scale.ToVector3();

            instances.Add(go);
        }

        Debug.Log($"[MapSaver] 实例化完成：{instances.Count}/{mapData.objects.Count} 个物体");
        return instances;
    }
}

// ════════════════════════════════════════════════════
//  数据结构（与服务端/客户端共享这份定义）
// ════════════════════════════════════════════════════

/// <summary>
/// 整张地图的数据
/// </summary>
[Serializable]
public class MapData
{
    public string mapName;
    public string savedAt;           // ISO 8601 时间字符串
    public List<MapObjectData> objects;
}

/// <summary>
/// 单个放置物体的数据
/// </summary>
[Serializable]
public class MapObjectData
{
    public int prefabIndex;       // 预制体列表索引（快速匹配）
    public string prefabName;        // 预制体名称（可读 / fallback匹配）
    public SerializableVector3 position;
    public SerializableVector3 rotation; // 欧拉角（度）
    public SerializableVector3 scale;
}

/// <summary>
/// JsonUtility可序列化的Vector3
/// （Unity原生Vector3无法被JsonUtility正确序列化嵌套）
/// </summary>
[Serializable]
public class SerializableVector3
{
    public float x, y, z;

    public SerializableVector3() { }
    public SerializableVector3(Vector3 v) { x = v.x; y = v.y; z = v.z; }
    public SerializableVector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }

    public Vector3 ToVector3() => new Vector3(x, y, z);

    public override string ToString() => $"({x:F3}, {y:F3}, {z:F3})";
}