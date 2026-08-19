using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using Unity.XR.PXR;
using static Unity.XR.PXR.PXR_Input;

/// <summary>
/// 地图编辑器核心管理器 v5
///
/// 修正拖出预制体的流程：
///   旧流程（有bug）：等射线离开Image → 创建幽灵 → 关UI
///   新流程：
///     Trigger按下 + 射线在Image上
///       → 立刻创建幽灵（但不激活显示）
///       → UI保持开启
///     持续按住Trigger，每帧做射线检测：
///       → 射线打到MR表面 → 幽灵移动到落点并激活 → 关闭UI
///     Trigger松开 → 放置幽灵
///
///   这样避免了"射线离开Image"的时机判断问题，逻辑更简单可靠。
/// </summary>
[RequireComponent(typeof(MapEditorMapSaver))]
public class MapEditorManager : MonoBehaviour
{
    // ═══════════════════════════════════════════════════
    //  Inspector 配置
    // ═══════════════════════════════════════════════════
    [Header("坐标参考系")]
    public Transform mapRootContainer;  // 编辑时的所有物体放在这下面  

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


    //缩放
    private enum ScaleAxis { Uniform, X, Y, Z }
    private ScaleAxis _currentScaleAxis = ScaleAxis.Uniform;
    private bool _prevLeftX = false;

    // ═══════════════════════════════════════════════════
    //  运行时状态
    // ═══════════════════════════════════════════════════

    public int CurrentPrefabIndex { get; private set; } = 0;
    public bool IsUIOpen { get; private set; } = false;

    // 场景物体选中拖拽
    private GameObject _selectedObject = null;
    private bool _isDraggingSelected = false;

    // 从UI拖出预制体
    // 状态1：已创建幽灵，但还没打到MR表面（UI仍显示）
    // 状态2：已打到MR表面，幽灵已激活，UI已关闭
    private GameObject _draggingFromUI = null;
    private bool _isDraggingFromUI = false;
    private bool _ghostHasLanded = false; // 幽灵是否已打到MR表面

    // XR 设备
    private InputDevice _rightCtrl;
    private InputDevice _leftCtrl;

    // 输入防抖
    private bool _prevRightB = false;
    private bool _prevLeftB = false;
    private bool _prevRightA = false;
    private bool _prevLeftGrip = false;
    private bool _prevRightGrip = false;
    private bool _prevRightTrig = false;

    private MapEditorMapSaver _saver;
    private List<PlacedObjectData> _placedObjects = new List<PlacedObjectData>();

    // ═══════════════════════════════════════════════════
    //  Unity 生命周期
    // ═══════════════════════════════════════════════════

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
        //测试用
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

    // ═══════════════════════════════════════════════════
    //  设备管理
    // ═══════════════════════════════════════════════════

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

    // ═══════════════════════════════════════════════════
    //  按键处理
    // ═══════════════════════════════════════════════════

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

    // ── Trigger 主逻辑 ────────────────────────────────

    /*
    void HandleTrigger_Main()
    {
        _rightCtrl.TryGetFeatureValue(CommonUsages.trigger, out float trigVal);
        bool trigHeld = trigVal > 0.7f;
        bool trigDown = trigHeld && !_prevRightTrig;
        bool trigUp = !trigHeld && _prevRightTrig;

        Ray ray = GetRightRay();

        // ── 按下瞬间 ──────────────────────────────────
        if (trigDown)
        {
            // 情况A：UI开着，射线打到PrefabImage
            //   → 立刻创建幽灵（不激活），UI先保持显示
            if (IsUIOpen && uiCanvas != null && uiCanvas.IsRayHittingPrefabImage(ray))
            {
                BeginDragFromUI_CreateGhost();
            }
            // 情况B：射线打到已放置物体 → 选中拖拽
            else if (Physics.Raycast(ray, out RaycastHit hit, 20f, placedObjectLayer))
            {
                SelectObject(hit.collider.gameObject);
                _isDraggingSelected = true;
            }
        }

        // ── 持续按住 ──────────────────────────────────
        if (trigHeld)
        {
            // 从UI拖出：幽灵跟随射线落点
            if (_isDraggingFromUI && _draggingFromUI != null)
            {
                if (Physics.Raycast(ray, out RaycastHit mrHit, 20f, mrRaycastLayer))
                {
                    _draggingFromUI.transform.position = mrHit.point;

                    // 第一次打到MR表面：激活幽灵 + 关闭UI
                    if (!_ghostHasLanded)
                    {
                        _ghostHasLanded = true;
                        _draggingFromUI.SetActive(true);

                        if (IsUIOpen) CloseUI();

                        Debug.Log("[MapEditor] 幽灵已落地，UI已关闭");
                    }
                }
                // 还没打到MR表面时不做任何事，UI保持，幽灵不激活
            }

            // 场景物体拖拽
            if (_isDraggingSelected && _selectedObject != null)
            {
                if (Physics.Raycast(ray, out RaycastHit mrHit, 20f, mrRaycastLayer))
                    _selectedObject.transform.position = mrHit.point;
            }
        }

        // ── 松开 ──────────────────────────────────────
        if (trigUp)
        {
            if (_isDraggingFromUI)
            {
                if (_draggingFromUI != null && _ghostHasLanded)
                {
                    // 已落地 → 正式放置
                    PlaceGhostAsReal();
                }
                else if (_draggingFromUI != null)
                {
                    // 没有打到MR表面就松开（在UI上点了一下就松开）→ 取消，销毁幽灵
                    Destroy(_draggingFromUI);
                    _draggingFromUI = null;
                    Debug.Log("[MapEditor] 拖拽取消（未落地）");
                }

                _isDraggingFromUI = false;
                _ghostHasLanded = false;
            }

            _isDraggingSelected = false;
        }

        _prevRightTrig = trigHeld;
    }
    */
    // ── Trigger 主逻辑 ────────────────────────────────

    [Header("自由放置配置")]
    public float freePlaceDistance = 1.5f;   // 没打到表面时，幽灵离控制器的距离（米）
    public float freePlaceHeight = 0f;       // 可选：固定高度偏移（0=不偏移）

    void HandleTrigger_Main()
    {
        _rightCtrl.TryGetFeatureValue(CommonUsages.trigger, out float trigVal);
        bool trigHeld = trigVal > 0.7f;
        bool trigDown = trigHeld && !_prevRightTrig;
        bool trigUp = !trigHeld && _prevRightTrig;

        Ray ray = GetRightRay();

        // ── 按下瞬间 ──────────────────────────────────
        if (trigDown)
        {
            // 情况A：UI 开着，射线打到 PrefabImage → 开始拖出预制体
            if (IsUIOpen && uiCanvas != null && uiCanvas.IsRayHittingPrefabImage(ray))
            {
                BeginDragFromUI_CreateGhost();
            }
            // 情况B：射线打到已放置物体 → 选中拖拽
            else if (Physics.Raycast(ray, out RaycastHit hit, 20f, placedObjectLayer))
            {
                SelectObject(hit.collider.gameObject);
                _isDraggingSelected = true;
            }
        }

        // ── 持续按住 ──────────────────────────────────
        if (trigHeld)
        {
            // 从UI拖出：幽灵跟随射线落点（打到MR表面就吸附，打不到就悬浮）
            if (_isDraggingFromUI && _draggingFromUI != null)
            {
                Vector3 targetPos;

                if (Physics.Raycast(ray, out RaycastHit mrHit, 20f, mrRaycastLayer))
                {
                    // 打到 MR 表面：吸附到表面
                    targetPos = mrHit.point;
                }
                else
                {
                    // 没打到：悬浮在射线前方 freePlaceDistance 处
                    targetPos = ray.GetPoint(freePlaceDistance);
                }

                // 可选：加高度偏移
                targetPos.y += freePlaceHeight;

                _draggingFromUI.transform.position = targetPos;

                // 第一次移动就激活幽灵 + 关闭UI（不再需要"必须打到MR表面"）
                if (!_ghostHasLanded)
                {
                    _ghostHasLanded = true;
                    _draggingFromUI.SetActive(true);
                    if (IsUIOpen) CloseUI();
                    Debug.Log("[MapEditor] 幽灵已激活（自由放置模式）");
                }
            }

            // 场景物体拖拽（同样支持自由悬浮）
            if (_isDraggingSelected && _selectedObject != null)
            {
                Vector3 targetPos;

                if (Physics.Raycast(ray, out RaycastHit mrHit, 20f, mrRaycastLayer))
                {
                    targetPos = mrHit.point;
                }
                else
                {
                    targetPos = ray.GetPoint(freePlaceDistance);
                }

                targetPos.y += freePlaceHeight;
                _selectedObject.transform.position = targetPos;
            }
        }

        // ── 松开 ──────────────────────────────────────
        if (trigUp)
        {
            if (_isDraggingFromUI)
            {
                if (_draggingFromUI != null && _ghostHasLanded)
                {
                    // 直接放置，不管在哪里
                    PlaceGhostAsReal();
                    Debug.Log("[MapEditor] 预制体已放置（自由位置）");
                }
                else if (_draggingFromUI != null)
                {
                    // 极端情况：幽灵还没激活就松手 → 取消
                    Destroy(_draggingFromUI);
                    _draggingFromUI = null;
                    Debug.Log("[MapEditor] 拖拽取消");
                }

                _isDraggingFromUI = false;
                _ghostHasLanded = false;
            }

            _isDraggingSelected = false;
        }

        _prevRightTrig = trigHeld;
    }



    void HandleStick_Rotation()
    {
        if (_selectedObject == null) return;
        _rightCtrl.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 stick);
        if (Mathf.Abs(stick.x) > 0.2f)
            _selectedObject.transform.Rotate(
                Vector3.up, stick.x * rotationSpeed * Time.deltaTime, Space.World);

        // 左 X 短按 → 循环切换缩放轴
        _leftCtrl.TryGetFeatureValue(CommonUsages.primaryButton, out bool leftX);
        if (leftX && !_prevLeftX)
        {
            _currentScaleAxis = (ScaleAxis)(((int)_currentScaleAxis + 1) % 4);
            Debug.Log($"[MapEditor] 缩放轴：{_currentScaleAxis}");
        }
        _prevLeftX = leftX;

        float scaleSpeed = 1.5f;
        // 左摇杆上下 → 缩放
        _leftCtrl.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 leftStick);
        if (Mathf.Abs(leftStick.y) > 0.2f)
        {
            float delta = leftStick.y * scaleSpeed* Time.deltaTime;
            Vector3 s = _selectedObject.transform.localScale;
            float minScale = 0.1f;
            int maxScale = 10;
            switch (_currentScaleAxis)
            {
                case ScaleAxis.Uniform:
                    float uniformScale = 1f + delta;
                    s *= uniformScale;
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
        }
        _prevRightA = cur;
    }

    // ═══════════════════════════════════════════════════
    //  UI 拖出核心
    // ═══════════════════════════════════════════════════

    /// <summary>
    /// Trigger按下在Image上时立刻调用：
    /// 创建幽灵（不激活），记录状态，UI继续显示
    /// </summary>
    private void BeginDragFromUI_CreateGhost()
    {
        if (CurrentPrefabIndex < 0 || CurrentPrefabIndex >= spawnablePrefabs.Length) return;

        DeselectObject();

        //_draggingFromUI = Instantiate(spawnablePrefabs[CurrentPrefabIndex]);
        _draggingFromUI = Instantiate(spawnablePrefabs[CurrentPrefabIndex], mapRootContainer);
        SetGhostMaterial(_draggingFromUI, true);
        _draggingFromUI.SetActive(false);   // 先不激活，等落地

        _isDraggingFromUI = true;
        _ghostHasLanded = false;

        Debug.Log($"[MapEditor] 开始拖出预制体：{spawnablePrefabs[CurrentPrefabIndex].name}，等待落地...");
    }

    // ═══════════════════════════════════════════════════
    //  保存地图
    // ═══════════════════════════════════════════════════

    public void SaveCurrentMap()
    {
        _placedObjects.RemoveAll(p => p.instance == null);

        if (_placedObjects.Count == 0)
        {
            Debug.Log("[MapEditor] 地图为空，无需保存");
            PXR_Input.SendHapticImpulse(VibrateType.LeftController, 0.2f, 50);
            return;
        }

        // 传入 XROrigin 的当前旋转
        Quaternion xrRot = mainCamera.transform.parent != null
            ? mainCamera.transform.parent.rotation
            : Quaternion.identity;

        string path = _saver.SaveMap(_placedObjects, xrRot);
        //string path = _saver.SaveMap(_placedObjects);
        if (path != null)
        {
            Debug.Log($"[MapEditor] 保存成功！共{_placedObjects.Count}个物体 → {path}");
            PXR_Input.SendHapticImpulse(VibrateType.LeftController, 0.8f, 60);
        }
        else
        {
            Debug.LogError("[MapEditor] 保存失败！");
            PXR_Input.SendHapticImpulse(VibrateType.LeftController, 0.3f, 200);
        }
    }

    // ═══════════════════════════════════════════════════
    //  公共方法
    // ═══════════════════════════════════════════════════

    public void ToggleUI()
    {
        if (IsUIOpen)
            CloseUI();
        else
            OpenUI();
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

    // ═══════════════════════════════════════════════════
    //  私有工具
    // ═══════════════════════════════════════════════════

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

        Debug.Log($"[MapEditor] 预制体已放置：{_draggingFromUI.name}，当前共{_placedObjects.Count}个");

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
      // ═══════════════════════════════════════════════════
    //  数据结构
    // ═══════════════════════════════════════════════════

    [System.Serializable]
    public class PlacedObjectData
    {
        public int prefabIndex;
        public GameObject instance;
    }
}