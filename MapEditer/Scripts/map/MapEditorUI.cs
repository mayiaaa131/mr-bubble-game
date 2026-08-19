using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 预制体选择UI面板 v4
/// IsRayHittingPrefabImage 修正平面法线方向
/// </summary>
[RequireComponent(typeof(Canvas))]
public class MapEditorUI : MonoBehaviour
{
    [Header("UI 元件")]
    public Image prefabImage;
    public Text prefabNameText;

    [Header("Canvas 尺寸（米）")]
    public float canvasWidth = 0.4f;
    public float canvasHeight = 0.25f;

    private MapEditorManager _manager;
    private Canvas _canvas;
    private RectTransform _imageRect;

    public void Initialize(MapEditorManager manager)
    {
        _manager = manager;
        _canvas = GetComponent<Canvas>();

        _canvas.renderMode = RenderMode.WorldSpace;
        RectTransform rt = GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(canvasWidth * 1000f, canvasHeight * 1000f);
        rt.localScale = Vector3.one * 0.001f;

        if (prefabImage != null)
            _imageRect = prefabImage.GetComponent<RectTransform>();

        RefreshUI();
    }

    public void RefreshUI()
    {
        if (_manager == null) return;
        int idx = _manager.CurrentPrefabIndex;

        if (_manager.prefabPreviews != null && idx < _manager.prefabPreviews.Length)
            prefabImage.sprite = _manager.prefabPreviews[idx];

        if (prefabNameText != null
            && _manager.spawnablePrefabs != null
            && idx < _manager.spawnablePrefabs.Length)
            prefabNameText.text = _manager.spawnablePrefabs[idx].name;
    }

    /// <summary>
    /// 检测射线是否命中PrefabImage区域
    /// 修正：平面法线朝向射线来源方向（Canvas正面朝玩家）
    /// </summary>
    public bool IsRayHittingPrefabImage(Ray ray)
    {
        if (_imageRect == null || !gameObject.activeInHierarchy) return false;

        // Canvas面向玩家，其forward指向玩家，所以法线用 +transform.forward
        // Plane构造：法线 + 平面上一点
        Plane plane = new Plane(transform.forward, transform.position);

        // plane.Raycast：射线从法线正方向射入才返回true，enter > 0
        if (!plane.Raycast(ray, out float enter) || enter < 0f) return false;

        Vector3 hitPoint = ray.GetPoint(enter);

        // 转换到Image本地坐标系判断是否在矩形内
        Vector3 localHit = _imageRect.InverseTransformPoint(hitPoint);
        Rect rect = _imageRect.rect;

        bool result = rect.Contains(new Vector2(localHit.x, localHit.y));
        return result;
    }

    void LateUpdate()
    {
        if (_manager?.mainCamera == null) return;
        Vector3 dir = transform.position - _manager.mainCamera.transform.position;
        if (dir.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(dir);
    }
}