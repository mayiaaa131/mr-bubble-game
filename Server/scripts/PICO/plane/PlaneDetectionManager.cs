using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using System;
using System.Threading.Tasks;
using Unity.XR.PXR;
using UnityEngine.XR;

/// <summary>
/// 平面检测管理器
/// 负责启动/停止平面检测，并维护平面数据缓存
/// </summary>
public class PlaneDetectionManager : MonoBehaviour
{
    public static PlaneDetectionManager Instance { get; private set; }

    private Dictionary<Guid, PxrPlaneData> _detectedPlanes = new Dictionary<Guid, PxrPlaneData>();
    private List<PxrPlaneData> _floorPlanes = new List<PxrPlaneData>();

    public event Action<List<PxrPlaneData>> OnPlanesUpdated;
    public bool IsReady { get; private set; } = false;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        _ = StartPlaneDetectionAsync();
    }

    private async Task StartPlaneDetectionAsync()
    {
        Debug.Log("[PlaneDetection] 正在启动平面检测...");

        PxrResult startResult = await PXR_MixedReality.StartSenseDataProvider(
            PxrSenseDataProviderType.PlaneDetection
        );

        Debug.Log($"[PlaneDetection] StartSenseDataProvider 返回: {startResult}");

        if (startResult != PxrResult.SUCCESS)
        {
            Debug.LogError($"[PlaneDetection] 启动失败: {startResult}\n" +
                           "请检查：\n" +
                           "1. AndroidManifest.xml 是否有 com.picovr.permission.SCENE_UNDERSTANDING\n" +
                           "2. PICO 系统设置里是否开启了空间感知授权");
            return;
        }

        // 轮询等待状态变为 Running（最多等 5 秒）
        int maxWaitMs = 5000;
        int waited = 0;
        int intervalMs = 100;

        while (waited < maxWaitMs)
        {
            PXR_MixedReality.GetSenseDataProviderState(
                PxrSenseDataProviderType.PlaneDetection, out var state);

            Debug.Log($"[PlaneDetection] 当前状态: {state}（已等待 {waited}ms）");

            if (state == PxrSenseDataProviderState.Running)
            {
                IsReady = true;
                Debug.Log("[PlaneDetection] 平面检测已就绪，开始接收数据");
                return;
            }

            await Task.Delay(intervalMs);
            waited += intervalMs;
        }

        Debug.LogError("[PlaneDetection] 平面检测启动超时！请检查权限和设备支持");
    }

    void OnEnable()
    {
        PXR_Manager.PlaneDetectionDataUpdated += HandlePlaneDetectionDataUpdated;
    }

    void OnDisable()
    {
        PXR_Manager.PlaneDetectionDataUpdated -= HandlePlaneDetectionDataUpdated;
    }

    private void HandlePlaneDetectionDataUpdated(List<PxrPlaneData> planeDatas)
    {
        if (planeDatas == null || planeDatas.Count == 0)
            return;

        foreach (PxrPlaneData planeData in planeDatas)
        {
            if (planeData.state == MeshChangeState.Removed)
            {
                _detectedPlanes.Remove(planeData.uuid);
                _floorPlanes.RemoveAll(p => p.uuid == planeData.uuid);
            }
            else
            {
                _detectedPlanes[planeData.uuid] = planeData;

                bool isFloor = planeData.label == PxrSemanticLabel.Floor ||
                               planeData.orientationMode == PxrPlaneOrientation.HorizontalUpward;

                if (isFloor)
                {
                    int existingIdx = _floorPlanes.FindIndex(p => p.uuid == planeData.uuid);
                    if (existingIdx >= 0)
                        _floorPlanes[existingIdx] = planeData;
                    else
                        _floorPlanes.Add(planeData);
                }
            }
        }

        Debug.Log($"[PlaneDetection] 检测到 {_detectedPlanes.Count} 个平面，其中 {_floorPlanes.Count} 个地面");
        OnPlanesUpdated?.Invoke(planeDatas);
    }

    public List<PxrPlaneData> GetFloorPlanes() => new List<PxrPlaneData>(_floorPlanes);

    public Dictionary<Guid, PxrPlaneData> GetAllPlanes() => new Dictionary<Guid, PxrPlaneData>(_detectedPlanes);

    public bool HasFloorPlane() => _floorPlanes.Count > 0;

    void OnDestroy()
    {
        Debug.Log("[PlaneDetection] 停止平面检测功能");
        _ = PXR_MixedReality.StopSenseDataProvider(PxrSenseDataProviderType.PlaneDetection);
    }
}