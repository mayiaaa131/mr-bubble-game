using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using Unity.XR.PXR;
using static Unity.XR.PXR.PXR_Input;

/// <summary>
/// 地图编辑器核心管理类 v5（含拖拽偏移修正）
/// </summary>
[RequireComponent(typeof(MapEditorMapSaver))]
public class MapEditorManager : MonoBehaviour
{
    // ══════════════════════════════════════════════════════
    //  Inspector 配置
    // ══════════════════════════════════════════════════════
    [Header("坐标参考系")]
    public Transform mapRootContainer;

    [Header("预制体配置")]
    public GameObject[] spawnablePrefabs;
    public Sprite[] prefabPreviews;

    [Header("场景引用")]
    public Camera mainCamera;
    public MapEditorUI uiCanvas;
    public MapEditorRayInteractor rightInteractor;
    public MapEditorRayInteractor leftInteractor;

    [Header("射线检测 Layer")]
    public LayerMask mrRaycastLayer;
    public LayerMask placedObjectLayer;
    public LayerMask uiLayer;

    [Header("旋转速度（度/秒）")]
    public float rotationSpeed = 90f;

    [Header("自由放置参数")]
    public float freePlaceDistance = 1.5f;   // 没有MR表面时，物体放置在射线前方的距离（米）
    public float freePlaceHeight = 0f;       // 可选：固定高度偏移（0=不偏移）

    // ══════════════════════════════════════════════════════
    //  缩放轴
    // ══════════════════════════════════════════════════════
    private enum ScaleAxis { Uniform, X, Y, Z }
    private ScaleAxis _currentScaleAxis = ScaleAxis.Uniform;
    private bool _prevLeftX = false;

    // ══════════════════════════════════════════════════════
    //  运行时状态
    // ══════════════════════════════════════════════════════

    public int CurrentPrefabIndex { get; private set; } = 0;
    public bool IsUIOpen { get; private set; } = false;

    // 已放置物体选中与拖拽
    private GameObject _selectedObject = null;
    private bool _isDraggingSelected = false;

    // ★ 新增：拖拽偏移量，选中瞬间计算，拖拽全程保持不变
    private Vector3 _dragOffset = Vector3.zero;

    // 从UI拖出预制体（幽灵）
    private GameObject _draggingFromUI = null;
    private bool _isDraggingFromUI = false;
    private bool _ghostHasLanded = false;

    // XR 设备
    private InputDevice _rightCtrl;
    private InputDevice _leftCtrl;

    // 按键上帧状态
    private bool _prevRightB = false;
    private bool _prevLeftB = false;
    private bool _prevRightA = false;
    private bool _prevLeftGrip = false;
    private bool _prevRightGrip = false;
    private bool _prevRightTrig = false;

    private MapEditorMapSaver _saver;
    private List<PlacedObjectData> _placedObjects = new List<PlacedObjectData>();

    // ══════════════════════════════════════════════════════
    //  Unity 生命周期
    // ══════════════════════════════════════════════════════

    void Awake()
    {
        _saver = GetComponent<MapEditorMapSaver>();
    }

    void Start()
    {
        RefreshInputDevices();
        InputDevices.deviceConnected += OnDeviceConnected;
        InputDevices.deviceDisconnected += OnDeviceDisconnected;

        if (uiCanvas != null)
        {
            uiCanvas.Initialize(this);
            uiCanvas.gameObject.SetActive(false);
        }
    }

    void OnDestroy()
    {
        InputDevices.deviceConnected -= OnDeviceConnected;
        InputDevices.deviceDisconnected -= OnDeviceDisconnected;
    }

    void Update()
    {
        Debug.Log($"XROrigin rotation: {mainCamera.transform.parent?.eulerAngles}");
        if (!_rightCtrl.isValid || !_leftCtrl.isValid)
            RefreshInputDevices();

        HandleRightB_UIToggle();
        HandleLeftB_SaveMap();
        HandleGrip_PrefabSwitch();
        HandleTrigger_Main();
        HandleStick_Rotation();
        HandleRightA_Delete();
    }

    // ══════════════════════════════════════════════════════
    //  设备管理
    // ══════════════════════════════════════════════════════

    void RefreshInputDevices()
    {
        var rights = new List<InputDevice>();
        var lefts = new List<InputDevice>();
        InputDevices.GetDevicesAtXRNode(XRNode.RightHand, rights);
        InputDevices.GetDevicesAtXRNode(XRNode.LeftHand, lefts);
        if (rights.Count > 0) _rightCtrl = rights[0];
        if (lefts.Count > 0) _leftCtrl = lefts[0];
    }

    void OnDeviceConnected(InputDevice d) => RefreshInputDevices();
    void OnDeviceDisconnected(InputDevice d) => RefreshInputDevices();

    // ══════════════════════════════════════════════════════
    //  按键处理
    // ══════════════════════════════════════════════════════

    void HandleRightB_UIToggle()
    {
        _rightCtrl.TryGetFeatureValue(CommonUsages.secondaryButton, out bool cur);
        if (cur && !_prevRightB) ToggleUI();
        _prevRightB = cur;
    }

    void HandleLeftB_SaveMap()
    {
        _leftCtrl.TryGetFeatureValue(CommonUsages.secondaryButton, out bool cur);
        if (cur && !_prevLeftB) SaveCurrentMap();
        _prevLeftB = cur;
    }

    void HandleGrip_PrefabSwitch()
    {
        _leftCtrl.TryGetFeatureValue(CommonUsages.grip, out float leftGrip);
        _rightCtrl.TryGetFeatureValue(CommonUsages.grip, out float rightGrip);

        bool leftDown = leftGrip > 0.7f;
        bool rightDown = rightGrip > 0.7f;

        if (leftDown && !_prevLeftGrip) SetPrefabIndex(CurrentPrefabIndex - 1);
        if (rightDown && !_prevRightGrip) SetPrefabIndex(CurrentPrefabIndex + 1);

        _prevLeftGrip = leftDown;
        _prevRightGrip = rightDown;
    }

    // ══════════════════════════════════════════════════════
    //  Trigger 核心交互逻辑
    // ══════════════════════════════════════════════════════

    void HandleTrigger_Main()
    {
        _rightCtrl.TryGetFeatureValue(CommonUsages.trigger, out float trigVal);
        bool trigHeld = trigVal > 0.7f;
        bool trigDown = trigHeld && !_prevRightTrig;
        bool trigUp = !trigHeld && _prevRightTrig;

        Ray ray = GetRightRay();

        // ── 按下瞬间 ────────────────────────────────────────
        if (trigDown)
        {
            // 情况A：UI 打开 且 射线命中 PrefabImage → 开始拖出预制体
            if (IsUIOpen && uiCanvas != null && uiCanvas.IsRayHittingPrefabImage(ray))
            {
                BeginDragFromUI_CreateGhost();
            }
            // 情况B：射线命中已放置物体 → 选中并开始拖拽
            else if (Physics.Raycast(ray, out RaycastHit hit, 20f, placedObjectLayer))
            {
                SelectObject(hit.collider.gameObject);
                _isDraggingSelected = true;

                // ★ 计算拖拽偏移：物体当前位置 - 此刻的目标落点
                Vector3 targetPoint = GetTargetPoint(ray);
                _dragOffset = _selectedObject.transform.position - targetPoint;
            }
        }

        // ── 持续按住 ────────────────────────────────────────
        if (trigHeld)
        {
            // 从UI拖出的幽灵预制体（无需偏移，以射线落点为准）
            if (_isDraggingFromUI && _draggingFromUI != null)
            {
                Vector3 targetPos = GetTargetPoint(ray);
                _draggingFromUI.transform.position = targetPos;

                // 首次有落点：幽灵变可见 + 关闭UI
                if (!_ghostHasLanded)
                {
                    _ghostHasLanded = true;
                    _draggingFromUI.SetActive(true);
                    if (IsUIOpen) CloseUI();
                    Debug.Log("[MapEditor] 幽灵已落地，进入自由放置模式。");
                }
            }

            // ★ 已选中物体拖拽（应用偏移，保持选中时的相对位置）
            if (_isDraggingSelected && _selectedObject != null)
            {
                Vector3 targetPoint = GetTargetPoint(ray);
                _selectedObject.transform.position = targetPoint + _dragOffset;
            }
        }

        // ── 松开 ────────────────────────────────────────────
        if (trigUp)
        {
            if (_isDraggingFromUI)
            {
                if (_draggingFromUI != null && _ghostHasLanded)
                {
                    // 幽灵已落地 → 正式放置
                    PlaceGhostAsReal();
                    Debug.Log("[MapEditor] 预制体已放置（自由位置）。");
                }
                else if (_draggingFromUI != null)
                {
                    // 还没落地就松开 → 取消放置
                    Destroy(_draggingFromUI);
                    _draggingFromUI = null;
                    Debug.Log("[MapEditor] 拖拽取消。");
                }

                _isDraggingFromUI = false;
                _ghostHasLanded = false;
            }

            _isDraggingSelected = false;
            // ★ 清空偏移（下次选中重新计算）
            _dragOffset = Vector3.zero;
        }

        _prevRightTrig = trigHeld;
    }

    /// <summary>
    /// 获取目标落点：优先 MR 表面交点，否则射线前方自由点。统一加高度偏移。
    /// </summary>
    private Vector3 GetTargetPoint(Ray ray)
    {
        Vector3 point;
        if (Physics.Raycast(ray, out RaycastHit mrHit, 20f, mrRaycastLayer))
            point = mrHit.point;
        else
            point = ray.GetPoint(freePlaceDistance);

        point.y += freePlaceHeight;
        return point;
    }

    // ══════════════════════════════════════════════════════
    //  摇杆旋转 & 缩放
    // ══════════════════════════════════════════════════════

    void HandleStick_Rotation()
    {
        if (_selectedObject == null) return;

        // 右摇杆左右 → 绕 Y 轴旋转
        _rightCtrl.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 stick);
        if (Mathf.Abs(stick.x) > 0.2f)
            _selectedObject.transform.Rotate(
                Vector3.up, stick.x * rotationSpeed * Time.deltaTime, Space.World);

        // 左 X 键 → 循环切换缩放轴
        _leftCtrl.TryGetFeatureValue(CommonUsages.primaryButton, out bool leftX);
        if (leftX && !_prevLeftX)
        {
            _currentScaleAxis = (ScaleAxis)(((int)_currentScaleAxis + 1) % 4);
            Debug.Log($"[MapEditor] 缩放轴：{_currentScaleAxis}");
        }
        _prevLeftX = leftX;

        // 左摇杆上下 → 缩放
        _leftCtrl.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 leftStick);
        if (Mathf.Abs(leftStick.y) > 0.2f)
        {
            float scaleSpeed = 1.5f;
            float delta = leftStick.y * scaleSpeed * Time.deltaTime;
            Vector3 s = _selectedObject.transform.localScale;
            const float minScale = 0.1f;
            const int maxScale = 10;

            switch (_currentScaleAxis)
            {
                case ScaleAxis.Uniform:
                    s *= (1f + delta);
                    s = Vector3.Max(s, Vector3.one * minScale);
                    s = Vector3.Min(s, Vector3.one * maxScale);
                    break;
                case ScaleAxis.X:
                    s.x = Mathf.Clamp(s.x + delta, minScale, maxScale);
                    break;
                case ScaleAxis.Y:
                    s.y = Mathf.Clamp(s.y + delta, minScale, maxScale);
                    break;
                case ScaleAxis.Z:
                    s.z = Mathf.Clamp(s.z + delta, minScale, maxScale);
                    break;
            }

            _selectedObject.transform.localScale = s;
        }
    }

    void HandleRightA_Delete()
    {
        _rightCtrl.TryGetFeatureValue(CommonUsages.primaryButton, out bool cur);
        if (cur && !_prevRightA && _selectedObject != null)
        {
            PXR_Input.SendHapticImpulse(VibrateType.RightController, 0.6f, 80);
            _placedObjects.RemoveAll(p => p.instance == _selectedObject);
            Destroy(_selectedObject);
            _selectedObject = null;
            _dragOffset = Vector3.zero;
        }
        _prevRightA = cur;
    }

    // ══════════════════════════════════════════════════════
    //  UI 拖出预制体
    // ══════════════════════════════════════════════════════

    /// <summary>
    /// Trigger 命中 Image 时调用：创建幽灵（半透明，初始隐藏）
    /// </summary>
    private void BeginDragFromUI_CreateGhost()
    {
        if (CurrentPrefabIndex < 0 || CurrentPrefabIndex >= spawnablePrefabs.Length) return;

        DeselectObject();

        _draggingFromUI = Instantiate(spawnablePrefabs[CurrentPrefabIndex], mapRootContainer);
        SetGhostMaterial(_draggingFromUI, true);
        _draggingFromUI.SetActive(false);   // 先不显示，等落地

        _isDraggingFromUI = true;
        _ghostHasLanded = false;

        Debug.Log($"[MapEditor] 开始拖出预制体：{spawnablePrefabs[CurrentPrefabIndex].name}，等待落地...");
    }

    // ══════════════════════════════════════════════════════
    //  保存地图
    // ══════════════════════════════════════════════════════

    public void SaveCurrentMap()
    {
        _placedObjects.RemoveAll(p => p.instance == null);

        if (_placedObjects.Count == 0)
        {
            Debug.Log("[MapEditor] 地图为空，不予保存。");
            PXR_Input.SendHapticImpulse(VibrateType.LeftController, 0.2f, 50);
            return;
        }

        Quaternion xrRot = mainCamera.transform.parent != null
            ? mainCamera.transform.parent.rotation
            : Quaternion.identity;

        string path = _saver.SaveMap(_placedObjects, xrRot);
        if (path != null)
        {
            Debug.Log($"[MapEditor] 保存成功，共{_placedObjects.Count}个物体 → {path}");
            PXR_Input.SendHapticImpulse(VibrateType.LeftController, 0.8f, 60);
        }
        else
        {
            Debug.LogError("[MapEditor] 保存失败！");
            PXR_Input.SendHapticImpulse(VibrateType.LeftController, 0.3f, 200);
        }
    }

    // ══════════════════════════════════════════════════════
    //  UI 面板控制
    // ══════════════════════════════════════════════════════

    public void ToggleUI()
    {
        if (IsUIOpen) CloseUI();
        else OpenUI();
    }

    private void OpenUI()
    {
        IsUIOpen = true;
        if (uiCanvas == null) return;

        uiCanvas.gameObject.SetActive(true);

        Transform camT = mainCamera.transform;
        Vector3 pos = camT.position + camT.forward * 1.5f;
        pos.y = camT.position.y - 0.05f;
        uiCanvas.transform.position = pos;
        uiCanvas.transform.rotation = Quaternion.LookRotation(
            uiCanvas.transform.position - camT.position);

        PXR_Input.SendHapticImpulse(VibrateType.RightController, 0.3f, 40);
    }

    private void CloseUI()
    {
        IsUIOpen = false;
        uiCanvas?.gameObject.SetActive(false);
    }

    public void SetPrefabIndex(int index)
    {
        if (spawnablePrefabs == null || spawnablePrefabs.Length == 0) return;
        CurrentPrefabIndex = (index + spawnablePrefabs.Length) % spawnablePrefabs.Length;
        uiCanvas?.RefreshUI();
        PXR_Input.SendHapticImpulse(VibrateType.BothController, 0.2f, 30);
    }

    public void OnUIItemDragStart(int prefabIndex)
    {
        CurrentPrefabIndex = prefabIndex;
        BeginDragFromUI_CreateGhost();
    }

    public List<PlacedObjectData> GetPlacedObjects() => _placedObjects;

    // ══════════════════════════════════════════════════════
    //  私有工具方法
    // ══════════════════════════════════════════════════════

    private Ray GetRightRay()
    {
        if (rightInteractor != null)
            return new Ray(rightInteractor.transform.position, rightInteractor.transform.forward);
        return new Ray(mainCamera.transform.position, mainCamera.transform.forward);
    }

    private void PlaceGhostAsReal()
    {
        SetGhostMaterial(_draggingFromUI, false);
        SetLayerRecursively(_draggingFromUI, LayerMaskToIndex(placedObjectLayer));

        _placedObjects.Add(new PlacedObjectData
        {
            prefabIndex = CurrentPrefabIndex,
            instance = _draggingFromUI
        });

        SelectObject(_draggingFromUI);
        PXR_Input.SendHapticImpulse(VibrateType.RightController, 0.5f, 60);

        Debug.Log($"[MapEditor] 预制体已放置：{_draggingFromUI.name}，当前共{_placedObjects.Count}个。");

        _draggingFromUI = null;
    }

    private void SelectObject(GameObject obj)
    {
        DeselectObject();
        _selectedObject = obj;
        obj.GetComponent<ObjectHighlight>()?.SetHighlight(true);
    }

    private void DeselectObject()
    {
        _selectedObject?.GetComponent<ObjectHighlight>()?.SetHighlight(false);
        _selectedObject = null;
    }

    private void SetGhostMaterial(GameObject obj, bool isGhost)
    {
        foreach (var r in obj.GetComponentsInChildren<Renderer>())
        {
            foreach (var mat in r.materials)
            {
                Color c = mat.color;
                c.a = isGhost ? 0.4f : 1f;
                mat.color = c;

                if (isGhost)
                {
                    mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    mat.SetInt("_ZWrite", 0);
                    mat.DisableKeyword("_ALPHATEST_ON");
                    mat.EnableKeyword("_ALPHABLEND_ON");
                    mat.renderQueue = 3000;
                }
                else
                {
                    mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
                    mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
                    mat.SetInt("_ZWrite", 1);
                    mat.DisableKeyword("_ALPHABLEND_ON");
                    mat.renderQueue = -1;
                }
            }
        }
    }

    private void SetLayerRecursively(GameObject obj, int layer)
    {
        obj.layer = layer;
        foreach (Transform child in obj.transform)
            SetLayerRecursively(child.gameObject, layer);
    }

    private int LayerMaskToIndex(LayerMask mask)
    {
        int m = mask.value;
        for (int i = 0; i < 32; i++)
            if ((m & (1 << i)) != 0) return i;
        return 0;
    }

    // ══════════════════════════════════════════════════════
    //  数据结构
    // ══════════════════════════════════════════════════════

    [System.Serializable]
    public class PlacedObjectData
    {
        public int prefabIndex;
        public GameObject instance;
    }
}