using System;
using System.Collections.Generic;
using UnityEngine;
using BubbleBattle.Network;

public class PropManager : MonoBehaviour
{

    [SerializeField] private GameObject propPrefab;
    [SerializeField] private Material rangeVisualizationMaterial; // 用于范围可视化的材质  
    [SerializeField] private bool showPropRanges = true; // 是否显示范围  
    [SerializeField] private Color propColor = Color.magenta;   // 射线预览时的范围框颜色  
    [Header("沉默道具配置")]
    [SerializeField] private GameObject silencePropPrefab;

    private Dictionary<string, GameObject> _propGameObjects = new();
    private Dictionary<string, PropInfo> _propInfoDict = new();
    private Dictionary<string, GameObject> _propRangeVisuals = new(); // 存储范围可视化对象 

    void Start()
    {
        PicoWebSocketClient.Instance.OnPropStateBroadcastReceived += OnPropStateBroadcastReceived;
    }

    void OnDestroy()
    {
        if (PicoWebSocketClient.Instance != null)
        {
            PicoWebSocketClient.Instance.OnPropStateBroadcastReceived -= OnPropStateBroadcastReceived;
        }
    }

    private void OnPropStateBroadcastReceived(PropStateBroadcast broadcast)
    {
        if (broadcast.props == null)
            return;

        foreach (var propInfo in broadcast.props)
        {
            UpdatePropDisplay(propInfo);
        }
    }
    /*
    private void UpdatePropDisplay(PropInfo propInfo)
    {
        if (!_propGameObjects.ContainsKey(propInfo.propId))
        {
            CreatePropGameObject(propInfo);
        }

        var propGO = _propGameObjects[propInfo.propId];
        _propInfoDict[propInfo.propId] = propInfo;

        // 只有 Available 时显示，其他状态都隐藏
        if (propInfo.state == "Available")
        {
            propGO.SetActive(true);
            //Debug.Log($"[PropManager] 道具 {propInfo.propId} 可用");
            Vector3 propWorldPosition = new Vector3(propInfo.position.x, 0, propInfo.position.z);
            CreatePropRangeVisuals(propInfo.propId, propInfo, propWorldPosition);
        }
        else
        {
            propGO.SetActive(false);
           // Debug.Log($"[PropManager] 道具 {propInfo.propId} 隐藏 ({propInfo.state})");
        }
    }*/
    private void UpdatePropDisplay(PropInfo propInfo)
    {
        if (!_propGameObjects.ContainsKey(propInfo.propId))
        {
            CreatePropGameObject(propInfo);
        }

        var propGO = _propGameObjects[propInfo.propId];
        _propInfoDict[propInfo.propId] = propInfo;

        if (propInfo.state == "Available")
        {
            propGO.SetActive(true);
            Vector3 propWorldPosition = new Vector3(propInfo.position.x, 0, propInfo.position.z);

            // ── 根据道具类型选择范围颜色 ──────────────────────────────  
            Color rangeColor = propInfo.propType == "Silence" ? propColor : Color.green;
            CreatePropRangeVisuals(propInfo.propId, propInfo, propWorldPosition, rangeColor);
        }
        else
        {
            propGO.SetActive(false);
        }
    }
    /*

    private void CreatePropGameObject(PropInfo propInfo)
    {
        if (propPrefab == null)
        {
           // Debug.LogError("[PropManager] propPrefab 未设置！");
            return;
        }

        GameObject propGO = Instantiate(propPrefab, transform);
        propGO.name = $"Prop_{propInfo.propId}";

        // 简化版本 - 直接使用接收到的位置作为世界坐标
        Vector3 worldPosition = new Vector3(
            propInfo.position.x,
            propInfo.position.y,
            propInfo.position.z
        );

        propGO.transform.position = worldPosition;
        _propGameObjects[propInfo.propId] = propGO;

        //Debug.Log($"[PropManager] 创建道具: {propInfo.propId} 在世界位置 {worldPosition}");
    }*/
    private void CreatePropGameObject(PropInfo propInfo)
    {
        // 选择预制体：Silence 优先用 silencePropPrefab，否则 fallback 到 propPrefab  
        GameObject prefabToUse = (propInfo.propType == "Silence" && silencePropPrefab != null)
            ? silencePropPrefab
            : propPrefab;

        if (prefabToUse == null)
        {
            Debug.LogError("[PropManager] propPrefab 未设置！");
            return;
        }

        Vector3 worldPosition = new Vector3(
            propInfo.position.x,
            propInfo.position.y,
            propInfo.position.z
        );

        GameObject propGO = Instantiate(prefabToUse, transform);
        propGO.name = $"Prop_{propInfo.propId}";
        propGO.transform.position = worldPosition;
        _propGameObjects[propInfo.propId] = propGO;
    }

    /*
    //道具可视化：
    /// <summary>  
    /// 为道具创建范围可视化对象  
    /// </summary>  
    private void CreatePropRangeVisuals(string propId, PropInfo propInfo, Vector3 propWorldPosition)
    {
        //Debug.Log($"[DEBUG] showPropRanges={showPropRanges}");

        if (!showPropRanges)
        {
            //Debug.LogWarning($"道具范围未创建！showPropRanges={showPropRanges}");
            return;
        }

        // 如果已存在旧的范围可视化，先删除  
        if (_propRangeVisuals.TryGetValue(propId, out GameObject oldRangeVisual))
        {
            Destroy(oldRangeVisual);
        }

        // 创建单个范围可视化：x±1, z±1  
        GameObject rangeVisual = CreateSinglePropRangeVisual(propWorldPosition);

        if (rangeVisual != null)
        {
            rangeVisual.name = $"PropRange_{propId}";
            _propRangeVisuals[propId] = rangeVisual;

            // 设置为道具的子对象  
            if (_propGameObjects.TryGetValue(propId, out GameObject propObj))
            {
                rangeVisual.transform.SetParent(propObj.transform);
                rangeVisual.transform.localPosition = Vector3.zero;
            }

            //Debug.Log($"[PropManager] 创建道具范围可视化：{propId} 中心位置 {propWorldPosition}");
        }
    }*/
    // ── CreatePropRangeVisuals：新增 color 参数 ───────────────────────────  
    private void CreatePropRangeVisuals(string propId, PropInfo propInfo,
                                        Vector3 propWorldPosition, Color rangeColor)
    {
        if (!showPropRanges) return;

        if (_propRangeVisuals.TryGetValue(propId, out GameObject oldRangeVisual))
            Destroy(oldRangeVisual);

        GameObject rangeVisual = CreateSinglePropRangeVisual(propWorldPosition, rangeColor);

        if (rangeVisual != null)
        {
            rangeVisual.name = $"PropRange_{propId}";
            _propRangeVisuals[propId] = rangeVisual;

            if (_propGameObjects.TryGetValue(propId, out GameObject propObj))
            {
                rangeVisual.transform.SetParent(propObj.transform);
                rangeVisual.transform.localPosition = Vector3.zero;
            }
        }
    }
    /*
    /// <summary>  
    /// 创建单个道具范围的可视化对象  
    /// </summary>  
    private GameObject CreateSinglePropRangeVisual(Vector3 centerPosition)
    {
        // 范围：x±1, z±1  
        float xMin = centerPosition.x - 1f;
        float xMax = centerPosition.x + 1f;
        float zMin = centerPosition.z - 1f;
        float zMax = centerPosition.z + 1f;

        // 创建父对象  
        GameObject rangeVisualGO = new GameObject("RangeVisual");
        rangeVisualGO.transform.position = centerPosition;

        // 四个角的坐标  
        Vector3[] corners = new Vector3[4]
        {
            new Vector3(xMin, centerPosition.y, zMin), // 左下  
            new Vector3(xMax, centerPosition.y, zMin), // 右下  
            new Vector3(xMax, centerPosition.y, zMax), // 右上  
            new Vector3(xMin, centerPosition.y, zMax)  // 左上  
        };

        // 创建四条边（使用LineRenderer）  
        // 边1: 左下 - 右下  
        CreateEdgeLine(rangeVisualGO, corners[0], corners[1]);
        // 边2: 右下 - 右上  
        CreateEdgeLine(rangeVisualGO, corners[1], corners[2]);
        // 边3: 右上 - 左上  
        CreateEdgeLine(rangeVisualGO, corners[2], corners[3]);
        // 边4: 左上 - 左下  
        CreateEdgeLine(rangeVisualGO, corners[3], corners[0]);

        return rangeVisualGO;
    }*/
    private GameObject CreateSinglePropRangeVisual(Vector3 centerPosition, Color edgeColor)
    {
        float xMin = centerPosition.x - 0.5f;
        float xMax = centerPosition.x + 0.5f;
        float zMin = centerPosition.z - 0.5f;
        float zMax = centerPosition.z + 0.5f;

        GameObject rangeVisualGO = new GameObject("RangeVisual");
        rangeVisualGO.transform.position = centerPosition;

        Vector3[] corners = new Vector3[4]
        {
            new Vector3(xMin, centerPosition.y+0.53f, zMin),
            new Vector3(xMax, centerPosition.y+0.53f, zMin),
            new Vector3(xMax, centerPosition.y+0.53f, zMax),
            new Vector3(xMin, centerPosition.y+0.53f, zMax)
        };

        CreateEdgeLine(rangeVisualGO, corners[0], corners[1], edgeColor);
        CreateEdgeLine(rangeVisualGO, corners[1], corners[2], edgeColor);
        CreateEdgeLine(rangeVisualGO, corners[2], corners[3], edgeColor);
        CreateEdgeLine(rangeVisualGO, corners[3], corners[0], edgeColor);

        return rangeVisualGO;
    }
    /*
    /// <summary>  
    /// 创建单条边线  
    /// </summary>  
    private void CreateEdgeLine(GameObject parent, Vector3 start, Vector3 end)
    {
        GameObject lineGO = new GameObject("Edge");
        lineGO.transform.SetParent(parent.transform);
        lineGO.transform.position = Vector3.zero;

        LineRenderer lineRenderer = lineGO.AddComponent<LineRenderer>();

        // 配置LineRenderer  
        lineRenderer.positionCount = 2;
        lineRenderer.SetPosition(0, start);
        lineRenderer.SetPosition(1, end);

        // 绿色边框  
        lineRenderer.startColor = Color.green;
        lineRenderer.endColor = Color.green;

        // 线宽  
        lineRenderer.startWidth = 0.1f;
        lineRenderer.endWidth = 0.1f;

        // 使用简单的材质  
        if (rangeVisualizationMaterial != null)
        {
            lineRenderer.material = rangeVisualizationMaterial;
        }
        else
        {
            lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        }
    }*/
    private void CreateEdgeLine(GameObject parent, Vector3 start, Vector3 end, Color color)
    {
        GameObject lineGO = new GameObject("Edge");
        lineGO.transform.SetParent(parent.transform);
        lineGO.transform.position = Vector3.zero;

        LineRenderer lineRenderer = lineGO.AddComponent<LineRenderer>();
        lineRenderer.positionCount = 2;
        lineRenderer.SetPosition(0, start);
        lineRenderer.SetPosition(1, end);

        lineRenderer.startColor = color;   // ← 动态颜色  
        lineRenderer.endColor = color;
        lineRenderer.startWidth = 0.1f;
        lineRenderer.endWidth = 0.1f;

        lineRenderer.material = rangeVisualizationMaterial != null
            ? rangeVisualizationMaterial
            : new Material(Shader.Find("Sprites/Default"));
    }
}
