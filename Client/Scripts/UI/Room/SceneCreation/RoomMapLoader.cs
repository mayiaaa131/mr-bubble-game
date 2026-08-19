// ============================================
// 文件路径：Assets/scripts/Room/RoomMapLoader.cs
// ★ 完整版本 - 支持 MapData 更新方案 + 详细日志
// ★ 改进：使用 prefabName 直接查找，不依赖 prefabIndex
// ============================================
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;

/// <summary>
/// ★ 升级版 RoomMapLoader
/// - 完全支持新格式 MapData JSON
/// - 统一 objects 数组（包含所有物体类型）
/// - 使用 prefabName 直接标识物体（简洁高效）
/// - Prefab 缓存机制（性能优化）
/// - 完整的日志链路追踪
/// - 错误恢复机制
/// </summary>
public class RoomMapLoader : MonoBehaviour
{
    private string _mapId;
    private Transform _mapRoot;
    private MapData _mapData;

    // ★ Prefab 缓存（避免重复加载）
    private Dictionary<string, GameObject> _prefabCache = new Dictionary<string, GameObject>();

    // ★ 加载统计
    private int _loadedObjectCount = 0;
    private int _failedObjectCount = 0;

    /// <summary>
    /// 初始化加载器
    /// </summary>
    public void Init(string mapId, Transform mapRoot)
    {
        _mapId = mapId;
        _mapRoot = mapRoot;
        _prefabCache.Clear();
        _loadedObjectCount = 0;
        _failedObjectCount = 0;

        Debug.Log($"[MapLoader] ✓ 初始化完成");
        Debug.Log($"  - 地图ID: {mapId}");
        Debug.Log($"  - 根节点: {mapRoot.name}");
    }

    /// <summary>
    /// ★ 核心方法：加载新格式 MapData JSON
    /// </summary>
    public void LoadMap()
    {
        Debug.Log($"\n[MapLoader] ========== 【开始加载地图】 ==========");
        Debug.Log($"[MapLoader] 时间戳: {System.DateTime.Now:HH:mm:ss.fff}");

        // ★ 步骤1：验证基本参数
        if (string.IsNullOrEmpty(_mapId))
        {
            Debug.LogError($"[MapLoader] ❌ 地图ID为空！");
            return;
        }

        if (_mapRoot == null)
        {
            Debug.LogError($"[MapLoader] ❌ 地图根节点为空！");
            return;
        }

        Debug.Log($"[MapLoader] → 步骤1: 参数验证 ✓");

        // ★ 步骤2：构建文件路径
        string mapJsonPath = System.IO.Path.Combine(
            Application.dataPath, "json", "MapData", $"{_mapId}.json"
        );

        Debug.Log($"[MapLoader] → 步骤2: 文件路径");
        Debug.Log($"  - 完整路径: {mapJsonPath}");

        // ★ 步骤3：检查文件存在性
        if (!System.IO.File.Exists(mapJsonPath))
        {
            Debug.LogError($"[MapLoader] ❌ 地图文件不存在: {mapJsonPath}");
            Debug.LogError($"[MapLoader]    请检查以下几点:");
            Debug.LogError($"[MapLoader]    1️⃣ 文件是否放在: Assets/json/MapData/ 目录下");
            Debug.LogError($"[MapLoader]    2️⃣ 文件名是否为: {_mapId}.json");
            Debug.LogError($"[MapLoader]    3️⃣ 文件是否已保存");
            return;
        }

        Debug.Log($"[MapLoader] → 步骤3: 文件存在 ✓");

        // ★ 步骤4：读取并解析 JSON
        try
        {
            Debug.Log($"[MapLoader] → 步骤4: 读取 JSON 文件...");

            string json = System.IO.File.ReadAllText(mapJsonPath);
            Debug.Log($"[MapLoader]    - 文件大小: {json.Length} 字节");

            // 尝试反序列化
            _mapData = JsonConvert.DeserializeObject<MapData>(json);

            if (_mapData == null)
            {
                Debug.LogError($"[MapLoader] ❌ JSON 反序列化返回 null");
                Debug.LogError($"[MapLoader]    请检查 JSON 格式是否正确");
                return;
            }

            Debug.Log($"[MapLoader] → 步骤4: JSON 解析 ✓");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[MapLoader] ❌ JSON 解析异常:");
            Debug.LogError($"[MapLoader]    异常类型: {ex.GetType().Name}");
            Debug.LogError($"[MapLoader]    异常信息: {ex.Message}");
            Debug.LogError($"[MapLoader]    堆栈: {ex.StackTrace}");
            return;
        }

        // ★ 步骤5：验证 MapData 完整性
        Debug.Log($"[MapLoader] → 步骤5: 验证 MapData 数据");
        Debug.Log($"  - 地图名: {_mapData.mapName ?? "【未设置】"}");
        Debug.Log($"  - 保存时间: {_mapData.savedAt ?? "【未设置】"}");
        Debug.Log($"  - 地图类型: {_mapData.type ?? "【未设置】"}");
        Debug.Log($"  - 物体数量: {_mapData.objects?.Count ?? 0}");

        if (_mapData.objects == null || _mapData.objects.Count == 0)
        {
            Debug.LogWarning($"[MapLoader] ⚠️  地图物体列表为空！");
            Debug.Log($"[MapLoader] ✓ 空地图加载完成（不生成任何物体）");
            return;
        }

        Debug.Log($"[MapLoader] → 步骤5: 数据验证 ✓");

        // ★ 步骤6：清空旧地图
        // ★ 步骤6：清空旧地图（复用 ClearLoadedMap，避免逻辑重复）  
        Debug.Log($"[MapLoader] → 步骤6: 清空旧地图...");
        ClearLoadedMap(); // ★ 改为调用统一清空函数  
        Debug.Log($"[MapLoader] → 步骤6: 清空完成 ✓");

        // ★ 步骤7：生成所有物体
        Debug.Log($"[MapLoader] → 步骤7: 生成 {_mapData.objects.Count} 个物体...");

        _loadedObjectCount = 0;
        _failedObjectCount = 0;

        for (int i = 0; i < _mapData.objects.Count; i++)
        {
            SpawnObject(_mapData.objects[i], i);
        }

        Debug.Log($"[MapLoader] → 步骤7: 物体生成完成 ✓");

        // ★ 步骤8：输出最终统计
        Debug.Log($"[MapLoader] ========== 【地图加载完成】 ==========");
        Debug.Log($"[MapLoader] ✅ 加载统计:");
        Debug.Log($"  - 成功: {_loadedObjectCount} 个物体");
        Debug.Log($"  - 失败: {_failedObjectCount} 个物体");
        Debug.Log($"  - 总计: {_mapData.objects.Count} 个物体");
        Debug.Log($"  - 成功率: {(_loadedObjectCount * 100 / _mapData.objects.Count)}%");
        Debug.Log($"  - 缓存中: {_prefabCache.Count} 个 Prefab");
        Debug.Log($"  - 地图名: 【{_mapData.mapName}】");
        Debug.Log($"[MapLoader] =============================\n");

        // ★ 如果全部失败，输出警告
        if (_loadedObjectCount == 0 && _mapData.objects.Count > 0)
        {
            Debug.LogError($"[MapLoader] ❌ 【致命错误】所有物体加载失败！");
            Debug.LogError($"[MapLoader]    请检查 Prefab 路径是否正确");
        }
    }

    /// <summary>
    /// ★ 核心方法：生成单个物体
    /// 使用 prefabName 直接查找（简洁高效）
    /// </summary>
    private void SpawnObject(MapObject obj, int index)
    {
        try
        {
            // ★ 验证物体数据
            if (obj == null)
            {
                Debug.LogError($"[MapLoader] ❌ [{index}] 物体数据为 null");
                _failedObjectCount++;
                return;
            }

            if (string.IsNullOrEmpty(obj.prefabName))
            {
                Debug.LogError($"[MapLoader] ❌ [{index}] prefabName 为空");
                _failedObjectCount++;
                return;
            }

            // ★ 直接使用 prefabName 加载或从缓存获取
            if (!_prefabCache.TryGetValue(obj.prefabName, out GameObject prefab))
            {
                // 从 Resources/Prefabs/ 目录加载
                prefab = Resources.Load<GameObject>($"Prefabs/{obj.prefabName}");

                if (prefab == null)
                {
                    Debug.LogWarning($"[MapLoader] ❌ [{index}] Prefab 不存在");
                    Debug.LogWarning($"[MapLoader]    路径: Resources/Prefabs/{obj.prefabName}");
                    Debug.LogWarning($"[MapLoader]    请检查 Prefab 名称是否正确");
                    _failedObjectCount++;
                    return;
                }

                // 添加到缓存
                _prefabCache[obj.prefabName] = prefab;
                Debug.Log($"[MapLoader] ⚡ [{index}] Prefab 已加载 (新): {obj.prefabName}");
            }
            else
            {
                Debug.Log($"[MapLoader] ⚡ [{index}] Prefab 已加载 (缓存): {obj.prefabName}");
            }

            // ★ 实例化物体
            GameObject go = Instantiate(prefab, _mapRoot);

            if (go == null)
            {
                Debug.LogError($"[MapLoader] ❌ [{index}] 实例化失败: {obj.prefabName}");
                _failedObjectCount++;
                return;
            }

            // ★ 设置物体名称
            go.name = $"[{index}_{obj.prefabName}]";

            // ★ 绑定标识组件（用于后续追踪）
            var marker = go.AddComponent<MapObjectMarker>();
            marker.objectId = $"object_{index}_{obj.prefabName}";
            marker.prefabName = obj.prefabName;

            // ★ 应用变换（位置、旋转、缩放）
            if (obj.position != null)
            {
                go.transform.localPosition = obj.position.ToVector3();
            }

            if (obj.rotation != null)
            {
                go.transform.localEulerAngles = obj.rotation.ToVector3();
            }

            if (obj.scale != null)
            {
                go.transform.localScale = obj.scale.ToVector3();
            }
            else
            {
                // ★ 默认缩放为 (1, 1, 1)
                go.transform.localScale = Vector3.one;
            }

            // ★ 详细日志
            Debug.Log($"[MapLoader] ✓ [{index}] {obj.prefabName} 生成成功");
            Debug.Log($"  - ID: {marker.objectId}");
            Debug.Log($"  - 位置: ({obj.position.x:F2}, {obj.position.y:F2}, {obj.position.z:F2})");
            Debug.Log($"  - 旋转: ({obj.rotation.x:F2}°, {obj.rotation.y:F2}°, {obj.rotation.z:F2}°)");
            Debug.Log($"  - 缩放: ({obj.scale.x:F2}, {obj.scale.y:F2}, {obj.scale.z:F2})");

            _loadedObjectCount++;
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[MapLoader] ❌ [{index}] 生成物体异常");
            Debug.LogError($"  - 物体名: {obj?.prefabName ?? "【未知】"}");
            Debug.LogError($"  - 异常类型: {ex.GetType().Name}");
            Debug.LogError($"  - 异常信息: {ex.Message}");
            Debug.LogError($"  - 堆栈: {ex.StackTrace}");
            _failedObjectCount++;
        }
    }

    /// <summary>
    /// ★ 新增方法：获取加载统计信息
    /// </summary>
    public void PrintLoadStatistics()
    {
        Debug.Log($"\n[MapLoader] ========== 【加载统计信息】 ==========");
        Debug.Log($"  - 成功加载: {_loadedObjectCount}");
        Debug.Log($"  - 加载失败: {_failedObjectCount}");
        Debug.Log($"  - 总物体数: {_mapData?.objects?.Count ?? 0}");
        Debug.Log($"  - Prefab 缓存: {_prefabCache.Count}");
        Debug.Log($"  - 地图名称: {_mapData?.mapName ?? "【未加载】"}");
        Debug.Log($"[MapLoader] =================================\n");
    }

    /// <summary>
    /// ★ 新增：清空场景中所有带 MapObjectMarker 标记的地图物体
    /// 不依赖 _mapRoot，全场景扫描，彻底清除残留
    /// 在返回大厅 / 每次 LoadMap 前调用均有效
    /// </summary>
    public void ClearLoadedMap()
    {
        Debug.Log("[MapLoader] ========== 【开始清空地图】 ==========");

        // ★ 方案一：通过 _mapRoot 清空（快速路径）
        if (_mapRoot != null && _mapRoot.childCount > 0)
        {
            int count = _mapRoot.childCount;

            // 收集后再销毁，避免遍历中修改集合
            List<GameObject> toDestroy = new List<GameObject>();
            foreach (Transform child in _mapRoot)
            {
                toDestroy.Add(child.gameObject);
            }

            foreach (GameObject go in toDestroy)
            {
                DestroyImmediate(go); // ★ 用 DestroyImmediate 确保立即生效
            }

            Debug.Log($"[MapLoader] ✓ _mapRoot 下已清空 {count} 个物体");
        }
        else
        {
            Debug.LogWarning("[MapLoader] ⚠ _mapRoot 为空或无子节点，转入全场景扫描");
        }

        // ★ 方案二：全场景扫描 MapObjectMarker，兜底清除所有残留物体
        MapObjectMarker[] allMarkers = FindObjectsByType<MapObjectMarker>(FindObjectsSortMode.None);

        if (allMarkers.Length > 0)
        {
            Debug.Log($"[MapLoader] 🔍 全场景扫描发现 {allMarkers.Length} 个残留地图物体，开始清除...");

            foreach (MapObjectMarker marker in allMarkers)
            {
                if (marker != null && marker.gameObject != null)
                {
                    Debug.Log($"[MapLoader]   - 销毁残留: {marker.objectId}");
                    DestroyImmediate(marker.gameObject);
                }
            }

            Debug.Log($"[MapLoader] ✓ 全场景残留物体已全部清除");
        }
        else
        {
            Debug.Log("[MapLoader] ✓ 全场景扫描：无残留地图物体");
        }

        // ★ 清空 Prefab 缓存 & 重置统计
        _prefabCache.Clear();
        _loadedObjectCount = 0;
        _failedObjectCount = 0;

        Debug.Log("[MapLoader] ✓ Prefab 缓存已清空");
        Debug.Log("[MapLoader] ========== 【地图清空完成】 ==========");
    }




    /// <summary>
    /// ★ 新增方法：获取加载是否成功
    /// </summary>
    public bool IsLoadSuccess()
    {
        return _failedObjectCount == 0 && _loadedObjectCount > 0;
    }

    /// <summary>
    /// ★ 新增方法：获取加载的物体总数
    /// </summary>
    public int GetLoadedObjectCount()
    {
        return _loadedObjectCount;
    }



}

/// <summary>
/// ★ MapObjectMarker：标记每个生成的物体
/// 用于后续追踪、编辑和删除
/// </summary>
public class MapObjectMarker : MonoBehaviour
{
    public string objectId;       // 唯一标识
    public string prefabName;     // Prefab 名称

    private void OnDestroy()
    {
        Debug.Log($"[MapObjectMarker] 物体已销毁: {objectId}");
    }
}
