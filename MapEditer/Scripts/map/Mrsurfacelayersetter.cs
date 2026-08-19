using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.XR.PXR;
using UnityEngine.XR;

/// <summary>
/// 运行时通过 PXR_MixedReality.QueryMeshAnchorAsync 获取场景网格，
/// 在场景中创建带 MeshCollider 的 GameObject 并设置到 MRSurface Layer，
/// 供 MapEditorManager 的射线检测使用。
///
/// 挂载：与 MapEditorManager 同一个 GameObject 即可
///
/// 前置条件：
///   1. PXR_Manager 上开启 Spatial Mesh 权限
///   2. PICO 设备已完成房间扫描（Room Capture）
///   3. Project Settings → Tags and Layers 里存在 "MRSurface" Layer
/// </summary>
public class MRSurfaceLayerSetter : MonoBehaviour
{
    [Header("配置")]
    [Tooltip("与 MapEditorManager.mrRaycastLayer 对应的 Layer 名称")]
    public string mrSurfaceLayerName = "MRSurface";

    [Tooltip("多少秒重新查询一次 Mesh（处理更新/新增的块）")]
    public float queryInterval = 3f;

    [Tooltip("是否显示场景网格（调试用，正式发布建议关闭）")]
    public bool showMeshDebug = false;

    // ── 内部状态 ──────────────────────────────────────
    private int _targetLayer = -1;
    private float _queryTimer = 0f;

    // uuid → 已创建的 GameObject，避免重复创建
    private Dictionary<System.Guid, GameObject> _meshObjects
        = new Dictionary<System.Guid, GameObject>();

    // 网格父节点，方便管理
    private Transform _meshRoot;

    // ── 生命周期 ──────────────────────────────────────

    void Start()
    {
        _targetLayer = LayerMask.NameToLayer(mrSurfaceLayerName);
        if (_targetLayer == -1)
        {
            Debug.LogError($"[MRSurface] Layer '{mrSurfaceLayerName}' 不存在！" +
                           "请在 Project Settings → Tags and Layers 中添加。");
            return;
        }

        // 创建父节点
        _meshRoot = new GameObject("_MRSurfaceMeshes").transform;
        _meshRoot.SetParent(null);

        // 启动查询
        StartCoroutine(QueryMeshLoop());
    }

    void Update()
    {
        // 也可以用协程的 WaitForSeconds，这里Update里做一个备用触发
        _queryTimer += Time.deltaTime;
    }

    // ── 主循环：定期查询 ──────────────────────────────

    private IEnumerator QueryMeshLoop()
    {
        // 等一帧，确保 PXR_Manager 已初始化
        yield return null;

        while (true)
        {
            yield return QueryAndBuildMeshes();
            yield return new WaitForSeconds(queryInterval);
        }
    }

    private IEnumerator QueryAndBuildMeshes()
    {
        // QueryMeshAnchorAsync 是 async Task，用协程包装
        bool done = false;
        List<PxrSpatialMeshInfo> meshInfos = null;
        PxrResult queryResult = PxrResult.Unknown;

        // 在后台线程执行异步查询，完成后回到主线程
        var task = PXR_MixedReality.QueryMeshAnchorAsync();
        while (!task.IsCompleted)
            yield return null;

        queryResult = task.Result.result;
        meshInfos = task.Result.meshInfos;

        if (queryResult != PxrResult.SUCCESS)
        {
            Debug.LogWarning($"[MRSurface] QueryMeshAnchorAsync 失败: {queryResult}");
            yield break;
        }

        if (meshInfos == null || meshInfos.Count == 0)
        {
            Debug.Log("[MRSurface] 没有查询到 Mesh 数据，请确认已完成房间扫描。");
            yield break;
        }

        int addedCount = 0;
        int updatedCount = 0;
        int removedCount = 0;

        foreach (var info in meshInfos)
        {
            switch (info.state)
            {
                case MeshChangeState.Added:
                    CreateMeshObject(info);
                    addedCount++;
                    break;

                case MeshChangeState.Updated:
                    // 先删再建
                    DestroyMeshObject(info.uuid);
                    CreateMeshObject(info);
                    updatedCount++;
                    break;

                case MeshChangeState.Removed:
                    DestroyMeshObject(info.uuid);
                    removedCount++;
                    break;

                case MeshChangeState.Unchanged:
                    // 不需要处理
                    break;
            }
        }

        if (addedCount + updatedCount + removedCount > 0)
        {
            Debug.Log($"[MRSurface] Mesh 更新 → 新增:{addedCount} 更新:{updatedCount} 删除:{removedCount}  " +
                      $"当前总数:{_meshObjects.Count}");
        }
    }

    // ── 创建 Mesh GameObject ──────────────────────────

    private void CreateMeshObject(PxrSpatialMeshInfo info)
    {
        if (info.vertices == null || info.vertices.Length == 0) return;
        if (info.indices == null || info.indices.Length == 0) return;

        var go = new GameObject($"SceneMesh_{info.uuid}");
        go.transform.SetParent(_meshRoot);
        go.layer = _targetLayer;

        // 构建 UnityEngine.Mesh
        var mesh = new Mesh();
        mesh.name = $"Mesh_{info.uuid}";

        // PxrSpatialMeshInfo.vertices 是 Vector3[]
        mesh.vertices = info.vertices;

        // PxrSpatialMeshInfo.indices 是 int[]
        mesh.triangles = System.Array.ConvertAll(info.indices, x => (int)x);

        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        // MeshCollider（物理碰撞，供射线检测）
        var collider = go.AddComponent<MeshCollider>();
        collider.sharedMesh = mesh;

        // 可选：调试时显示 Mesh
        if (showMeshDebug)
        {
            var mf = go.AddComponent<MeshFilter>();
            var mr = go.AddComponent<MeshRenderer>();
            mf.sharedMesh = mesh;
            mr.material = new Material(Shader.Find("Standard"))
            {
                color = new Color(0f, 1f, 0.5f, 0.2f)
            };
            // 半透明
            mr.material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mr.material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mr.material.SetInt("_ZWrite", 0);
            mr.material.EnableKeyword("_ALPHABLEND_ON");
            mr.material.renderQueue = 3000;
        }

        _meshObjects[info.uuid] = go;
    }

    // ── 删除 Mesh GameObject ──────────────────────────

    private void DestroyMeshObject(System.Guid uuid)
    {
        if (_meshObjects.TryGetValue(uuid, out var go))
        {
            if (go != null) Destroy(go);
            _meshObjects.Remove(uuid);
        }
    }

    // ── 清理 ──────────────────────────────────────────

    void OnDestroy()
    {
        StopAllCoroutines();
        if (_meshRoot != null)
            Destroy(_meshRoot.gameObject);
    }
}