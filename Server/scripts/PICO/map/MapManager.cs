using BubbleBattle.Network;
using UnityEngine;
using System.Collections.Generic;

/// <summary>  
/// 地图管理器  
/// 负责接收服务端地图数据，加载预制体并实例化到场景  
/// </summary>  
public class MapManager : MonoBehaviour
{
    public static MapManager Instance { get; private set; }

    [Header("预制体配置")]
    [SerializeField] private GameObject[] prefabsArray;  // 预制体数组，索引对应prefabIndex  

    [Header("地图容器")]
    [SerializeField] private Transform mapContainer;     // 地图对象的父容器  

    private Dictionary<string, GameObject> loadedMapObjects = new Dictionary<string, GameObject>();
    private string currentMapName;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        if (PicoWebSocketClient.Instance != null)
        {
            PicoWebSocketClient.Instance.OnMapDataReceived += HandleMapDataReceived;
        }
        else
        {
            Debug.LogError("[MapManager] PicoWebSocketClient.Instance 为空！");
        }

        // 如果没有指定容器，创建一个  
        if (mapContainer == null)
        {
            GameObject containerObj = new GameObject("MapObjects");
            mapContainer = containerObj.transform;
        }
    }

    /// <summary>  
    /// 处理从服务端接收的地图数据  
    /// </summary>  
    private void HandleMapDataReceived(MapDataMsg mapData)
    {
        Debug.Log($"[MapManager] 开始加载地图: {mapData.mapName}");

        currentMapName = mapData.mapName;

        // 清理旧地图（如果需要）  
        ClearCurrentMap();

        // 实例化所有对象  
        if (mapData.objects != null && mapData.objects.Length > 0)
        {
            //int length=mapData.objects.Length;
            foreach (var objData in mapData.objects)
            {
                if (objData.prefabIndex < 0 || objData.prefabIndex >= prefabsArray.Length)
                {
                    Debug.LogWarning($"[MapManager] 预制体索引超出范围: {objData.prefabIndex}，跳过对象 {objData.prefabName}");
                    // 跳过当前对象，继续处理下一个  
                    continue;
                }
                InstantiateMapObject(objData);
            }
        }

        Debug.Log($"[MapManager] 地图加载完成，共实例化 {loadedMapObjects.Count} 个对象");
    }

    /// <summary>  
    /// 实例化单个地图对象  
    /// </summary>  
    /// 

    private void InstantiateMapObject(MapObjectInfo objData)
    {
        // 验证预制体索引  
        if (objData.prefabIndex < 0 || objData.prefabIndex >= prefabsArray.Length)
        {
            Debug.LogWarning($"[MapManager] 无效的预制体索引: {objData.prefabIndex}，预制体名: {objData.prefabName}");
            return;
        }

        GameObject prefab = prefabsArray[objData.prefabIndex];
        if (prefab == null)
        {
            Debug.LogError($"[MapManager] 预制体数组[{objData.prefabIndex}]为空，预制体名: {objData.prefabName}");
            return;
        }

        // 直接使用服务端坐标  
        Vector3 position = new Vector3(objData.position.x, objData.position.y, objData.position.z);
        Quaternion rotation = Quaternion.Euler(objData.rotation.x, objData.rotation.y, objData.rotation.z);
        Vector3 scale = new Vector3(objData.scale.x, objData.scale.y, objData.scale.z);

        // 实例化  
        GameObject instance = Instantiate(
            prefab,
            position,
            rotation,
            mapContainer
        );

        instance.transform.localScale = scale;
        instance.name = $"{objData.prefabName}_{loadedMapObjects.Count}";

        instance.layer = LayerMask.NameToLayer("Default");

        // 生成唯一ID并存储  
        string objectId = $"{currentMapName}_{objData.prefabIndex}_{loadedMapObjects.Count}";
        loadedMapObjects[objectId] = instance;

        Debug.Log($"[MapManager] 实例化对象: {instance.name} 在位置 {position}");
    }

    /// <summary>  
    /// 清理当前地图的所有对象  
    /// </summary>  
    private void ClearCurrentMap()
    {
        foreach (var obj in loadedMapObjects.Values)
        {
            if (obj != null)
            {
                Destroy(obj);
            }
        }
        loadedMapObjects.Clear();
        Debug.Log("[MapManager] 已清理之前的地图对象");
    }

    /// <summary>  
    /// 获取当前地图名称  
    /// </summary>  
    public string GetCurrentMapName() => currentMapName;

    /// <summary>  
    /// 获取已加载的对象数量  
    /// </summary>  
    public int GetLoadedObjectCount() => loadedMapObjects.Count;

    /// <summary>  
    /// 根据名称查找地图对象  
    /// </summary>  
    public GameObject FindMapObject(string objectName)
    {
        if (loadedMapObjects.TryGetValue(objectName, out GameObject obj))
        {
            return obj;
        }
        return null;
    }

    void OnDestroy()
    {
        if (PicoWebSocketClient.Instance != null)
        {
            PicoWebSocketClient.Instance.OnMapDataReceived -= HandleMapDataReceived;
        }
    }
}
