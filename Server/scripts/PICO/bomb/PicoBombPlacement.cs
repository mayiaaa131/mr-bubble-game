// PicoBombPlacement.cs
using BubbleBattle.Network;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using System;
using Unity.XR.PXR; // 添加引用，为了使用 DateTimeOffset

/// <summary>
/// Pico MR 炸弹放置系统
/// 使用 XR Interaction Toolkit + Pico 3.4 输入系统
/// 右手 Trigger 控制炸弹拖拽和放置
/// </summary>
public class PicoBombPlacement : MonoBehaviour
{
    [Header("炸弹配置")]
    [Header("炸弹Type配置")]
    private string[] bombTypeList = new string[] { "长条形", "横条形", "十字形", "正方形" };
    private int currentBombTypeIndex = 0;  // 用索引循环 (0-3)  
    private string currentBombType = "长条形";  // 默认第一种  
    private bool _bButtonPressedLastFrame = false;

    [SerializeField] private GameObject bombPrefabRed;      // 红队炸弹预制体  
    [SerializeField] private GameObject bombPrefabBlue;     // 蓝队炸弹预制体 
    [SerializeField] private float bombExplosionDelay = 3f;   // 爆炸延迟时间
    [SerializeField] private float explosionRadius = 10f;     // 爆炸范围
    [SerializeField] private float explosionDamage = 50f;     // 爆炸伤害


    [Header("右手控制器")]
    [SerializeField] private XRController rightController;    // 右手控制器
    [SerializeField] private Transform rightHandAnchor;       // 右手位置（通常是 XR Origin > Camera Offset > Right Controller）

    [Header("拖拽配置")]
    // [SerializeField] private Vector3 bombOffsetFromHand = new Vector3(0, -0.1f, 0.1f); // 炸弹相对手部的偏移 - 此字段在新逻辑中不再直接使用
    [SerializeField] private bool showDraggingBombPreview = true; // 显示拖拽预览
    [SerializeField] private float raycastMaxDistance = 20f; // 射线最大距离
    [SerializeField] private LayerMask raycastLayerMask;     // 射线检测的层级遮罩
    [SerializeField] private GameObject raycastHitIndicatorPrefab; // 射线命中指示器预制体

    // 运行时变量
    private GameObject _draggingBomb;           // 当前拖拽的炸弹
    private GameObject _hitIndicator;           // 射线命中指示器
    private bool _isTriggerPressed = false;     // Trigger 按键状态
    private bool _triggerPressedLastFrame = false;
    private float _bombPlacementCooldown = 0f;  // 放置冷却时间
    private const float COOLDOWN_TIME = 2f;   // 放置冷却（防止连续放置）

    //追踪最后放置的炸弹ID和状态  
    private string _lastPlacedBombId;              // 最后放置的炸弹ID  
    private bool _hasPlacedBomb = false;           // 标志：是否已放置且未拖拽  
    private float _bombTypeUpdateCooldown = 0f;
    private const float TYPE_UPDATE_COOLDOWN = 0.3f;

    //Bomb UI
    // 在类中添加（与其他 public 字段一起）  
    public event Action OnBombPlaced;      // 炸弹放置事件  
    public event Action<int> OnBombCountChanged;  // 炸弹计数变化事件  
    public string LastPlacedBombId => _lastPlacedBombId;
    private const int MAX_BOMB_COUNT = 2;
    // 公开属性给BombUIManager查询  
    public int CurrentBombCount { get; set; } = 2;


    private int _releaseCallCount = 0;
    private BombUIManager _bombUIManager;

    void Start()
    {
        // 自动查找右手控制器
        if (rightController == null)
        {
            rightController = FindRightController();
        }

        // 自动查找右手 Anchor
        if (rightHandAnchor == null && rightController != null)
        {
            rightHandAnchor = rightController.transform;
        }

        if (rightController == null)
            //Debug.LogError("[PicoBomb] 无法找到右手控制器！");

        if (bombPrefabRed == null || bombPrefabBlue == null)
            //Debug.LogError("[PicoBomb] 炸弹预制体未指定！");

        // 实例化射线命中指示器
        if (raycastHitIndicatorPrefab != null)
        {
            _hitIndicator = Instantiate(raycastHitIndicatorPrefab);
            _hitIndicator.SetActive(false); // 初始禁用
        }
        _bombUIManager = FindObjectOfType<BombUIManager>();

        //调试
        CheckForSpatialMesh();
    }

    private void CheckForSpatialMesh()
    {
        //Debug.Log("=== 诊断 Spatial Mesh Layer ===");

        // 搜索所有 MeshCollider（Spatial Mesh 通常使用 MeshCollider）  
        MeshCollider[] meshColliders = FindObjectsOfType<MeshCollider>();

        foreach (var mc in meshColliders)
        {
            int layer = mc.gameObject.layer;
            string layerName = LayerMask.LayerToName(layer);

            //Debug.Log($"找到 MeshCollider: {mc.gameObject.name}");
            //Debug.Log($"  Layer ID: {layer}");
            //Debug.Log($"  Layer Name: {layerName}");
            //Debug.Log($"  是否在 raycastLayerMask 中: {((raycastLayerMask >> layer) & 1) == 1}");

            // 如果不在 Mask 中，添加它！  
            if (((raycastLayerMask >> layer) & 1) == 0)
            {
                //Debug.LogWarning($" Spatial Mesh 的 Layer '{layerName}' 不在 raycastLayerMask 中！");
            }
        }
    }

    void Update()
    {
        UpdateBombPlacementCooldown();
        UpdateBombTypeUpdateCooldown();

        // 检测右手 Trigger 按键
        DetectTriggerInput();

        // 拖拽中的炸弹跟随射线命中点
        if (_draggingBomb != null)
        {
            UpdateDraggingBombPosition();
        }
        // 检测B键输入用于切换炸弹type  
        DetectBButtonInput();
        CheckForSpatialMesh();
    }

    private void UpdateBombTypeUpdateCooldown()
    {
        if (_bombTypeUpdateCooldown > 0)
        {
            _bombTypeUpdateCooldown -= Time.deltaTime;
        }
    }
    private int _lastProcessedFrame = -1;
    private void DetectTriggerInput()
    {
        if (Time.frameCount == _lastProcessedFrame) return;
        _lastProcessedFrame = Time.frameCount;
        if (rightController == null) return;

        bool triggerPressed = false;
        bool readSuccess = rightController.inputDevice.IsPressed(
            InputHelpers.Button.TriggerButton,
            out triggerPressed
        );

        if (!readSuccess)
        {
            // 读取失败时也要更新lastFrame，否则下帧会误判
            _triggerPressedLastFrame = false;
            return;
        }

        _isTriggerPressed = triggerPressed;

        if (_isTriggerPressed && !_triggerPressedLastFrame)
            OnTriggerPressed();

        if (!_isTriggerPressed && _triggerPressedLastFrame)
        {
            Debug.Log($"[PicoBomb] 检测到松开！isTriggerPressed={_isTriggerPressed}, lastFrame={_triggerPressedLastFrame}");
            OnTriggerReleased();
        }

        _triggerPressedLastFrame = _isTriggerPressed;
    }

    private bool _hasProcessedRelease = false;
    private void OnTriggerPressed()
    {
        if (_draggingBomb != null) return; // 已有拖拽炸弹  
        if (_bombPlacementCooldown > 0) return; // 冷却中  
        _hasProcessedRelease = false;
        // 开始拖拽时，禁用已放置炸弹的修改权限  
        _hasPlacedBomb = false;

        CreateDraggingBomb();
        //Debug.Log("[PicoBomb] 开始拖拽炸弹（本地预览）");
    }
    private void OnTriggerReleased()
    {
        _releaseCallCount++;
        Debug.Log($"[PicoBomb] OnTriggerReleased被调用！第{_releaseCallCount}次，draggingBomb={_draggingBomb != null}, cooldown={_bombPlacementCooldown}, hasProcessed={_hasProcessedRelease}");
        if (_draggingBomb == null) return;
        if (_bombPlacementCooldown > 0) return;
        if (_hasProcessedRelease) return; // 防止重复触发
        _hasProcessedRelease = true;
        _bombPlacementCooldown = COOLDOWN_TIME;

        Debug.Log("[PicoBomb] === 开始诊断 ===");
        Debug.Log($"[PicoBomb] PlayerId={PicoWebSocketClient.Instance?.PlayerId ?? "NULL"}");
        Debug.Log($"[PicoBomb] TeamId={PicoWebSocketClient.Instance?.TeamId ?? "NULL"}");
        Debug.Log($"[PicoBomb] BombPosition={_draggingBomb.transform.position}");

        // 生成炸弹ID  
        _lastPlacedBombId = $"{PicoWebSocketClient.Instance.PlayerId}_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_{UnityEngine.Random.Range(0, 10000)}";
        //放置后，允许修改这个炸弹  
        _hasPlacedBomb = true;

        // 先保存引用，立即把字段置null，防止重入
        GameObject bombToPlace = _draggingBomb;


        // 发送网络消息
        SyncBombPlacementToNetwork(_draggingBomb, _lastPlacedBombId);

        BombUIManager uiManager = FindObjectOfType<BombUIManager>();
        if (uiManager != null)
        {
            uiManager.RegisterNewBomb(_lastPlacedBombId);
            Debug.Log($"[PicoBomb] 已将炸弹 {_lastPlacedBombId} 注册到UI管理器");
        }
        else
        {
            Debug.LogError("[PicoBomb] 找不到BombUIManager！");
        }

        // 关键：立刻销毁本地预览物体，不保留！
        //    真正的炸弹 GameObject 由 PicoBombStateManager 在收到服务端广播后统一创建
        Destroy(_draggingBomb);
        _draggingBomb = null;
        //_bombPlacementCooldown = COOLDOWN_TIME;

        CurrentBombCount--;
        CurrentBombCount = Mathf.Max(0, CurrentBombCount);

        // 触发事件，通知UI管理器  
        OnBombPlaced?.Invoke();
        OnBombCountChanged?.Invoke(CurrentBombCount);

        if (_hitIndicator != null)
            _hitIndicator.SetActive(false);

        Debug.Log("[PicoBomb] 炸弹已发送给服务端，等待广播后显示");
    }

    /// <summary>
    /// 创建拖拽中的炸弹预制体
    /// </summary>
    private void CreateDraggingBomb()
    {
        // 获取当前玩家的队伍  
        string currentTeamId = PicoWebSocketClient.Instance.TeamId;

        // 根据队伍选择对应颜色的预制体  
        GameObject selectedPrefab = GetBombPrefabByTeam(currentTeamId);

        if (selectedPrefab == null)
        {
            Debug.LogError("[PicoBomb] 无法获取对应队伍的炸弹预制体！");
            return;
        }

        Vector3 spawnPos = Vector3.zero;
        Quaternion spawnRot = Quaternion.identity;

        if (PerformRaycast(out RaycastHit hit))
        {
            spawnPos = hit.point;
        }
        else
        {
            spawnPos = rightHandAnchor.position + rightHandAnchor.forward * raycastMaxDistance * 0.5f;
        }

        _draggingBomb = Instantiate(selectedPrefab, spawnPos, spawnRot);
        // 禁用炸弹碰撞（拖拽时不与环境碰撞）  
        Collider bombCollider = _draggingBomb.GetComponent<Collider>();
        if (bombCollider != null)
        {
            bombCollider.enabled = false;
        }

        // 显示拖拽预览效果  
        if (showDraggingBombPreview)
        {
            ApplyDraggingVisuals(_draggingBomb);
        }
    }

    /// <summary>  
    /// 根据队伍ID选择炸弹预制体  
    /// </summary>  
    private GameObject GetBombPrefabByTeam(string teamId)
    {
        Debug.Log($"[PicoBomb] 正在查找 TeamId: {teamId} 的炸弹预制体");

        if (string.IsNullOrEmpty(teamId))
        {
            Debug.LogError("[PicoBomb] TeamId 为空！使用默认红队");
            return bombPrefabRed;
        }

        if (teamId.ToLower().Contains("red"))
        {
            if (bombPrefabRed == null)
                Debug.LogError("[PicoBomb] bombPrefabRed 未赋值！");
            return bombPrefabRed;
        }
        else if (teamId.ToLower().Contains("blue"))
        {
            if (bombPrefabBlue == null)
                Debug.LogError("[PicoBomb] bombPrefabBlue 未赋值！");
            return bombPrefabBlue;
        }

        Debug.LogError($"[PicoBomb] 无法识别 TeamId: {teamId}");
        return null;
    }


    /// <summary>
    /// 更新拖拽炸弹位置（跟随射线命中点）
    /// </summary>
    private void UpdateDraggingBombPosition()
    {
        if (rightHandAnchor == null || _draggingBomb == null) return;

        if (PerformRaycast(out RaycastHit hit))
        {
            _draggingBomb.transform.position = hit.point;
            // 炸弹的旋转可以根据表面法线调整，或者保持不变
            // _draggingBomb.transform.rotation = Quaternion.FromToRotation(Vector3.up, hit.normal);
            if (_hitIndicator != null)
            {
                _hitIndicator.SetActive(true);
                _hitIndicator.transform.position = hit.point;
                _hitIndicator.transform.up = hit.normal; // 指示器与表面对齐
            }
        }
        else
        {
            // 如果射线未命中任何物体，炸弹可以在手柄前方一定距离显示
            _draggingBomb.transform.position = rightHandAnchor.position + rightHandAnchor.forward * raycastMaxDistance * 0.5f;
            if (_hitIndicator != null)
            {
                _hitIndicator.SetActive(false); // 没有命中点则隐藏指示器
            }
        }
    }
    /*暂时注释
    /// <summary>
    /// 执行射线检测
    /// </summary>
    private bool PerformRaycast(out RaycastHit hit)
    {
        if (rightHandAnchor == null)
        {
            hit = default;
            return false;
        }
        // 从右手柄发出一条射线
        Ray ray = new Ray(rightHandAnchor.position, rightHandAnchor.forward);
        return Physics.Raycast(ray, out hit, raycastMaxDistance, raycastLayerMask);
    }
    */
    /*
    /// <summary>
    /// 通过网络同步炸弹放置信息
    /// </summary>
    private void SyncBombPlacementToNetwork(GameObject bomb, string bombId)
    {
        if (PicoWebSocketClient.Instance == null ||
            string.IsNullOrEmpty(PicoWebSocketClient.Instance.PlayerId))
        {
            Debug.LogWarning("[PicoBomb] WebSocketClient未初始化或PlayerId未分配");
            return;
        }

        Vector3 sendPosition = bomb.transform.position;
        if (PicoWebSocketClient.Instance.SharedAnchorTransform != null)
        {
            sendPosition = PicoWebSocketClient.Instance.SharedAnchorTransform
                .InverseTransformPoint(bomb.transform.position);
        }
        Debug.Log($"[PicoBomb] SyncBombPlacement called! bombId={bombId}");
        Debug.Log(System.Environment.StackTrace); // 打印完整调用链
        //string bombid = $"{PicoWebSocketClient.Instance.PlayerId}_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_{UnityEngine.Random.Range(0, 10000)}";

        // 使用本地数据类，不依赖 NetworkMessages
        var bombData = new BombCreate
        {
            type = "BombCreate",
            playerId = PicoWebSocketClient.Instance.PlayerId,
            teamId = PicoWebSocketClient.Instance.TeamId,
            position = new Vec3(sendPosition),
            bombType = currentBombType,
            bombId = bombId,
            timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };

        // 直接序列化并发送
        string json = JsonUtility.ToJson(bombData);
        PicoWebSocketClient.Instance.SendRawMessage(json);

        Debug.Log($"[PicoBomb] 炸弹Type: {this.currentBombType}, Position: {sendPosition}");
    }*/
    /// <summary>  
    /// 通过网络同步炸弹放置信息  
    /// </summary>  
    private void SyncBombPlacementToNetwork(GameObject bomb, string bombId)
    {
        if (PicoWebSocketClient.Instance == null ||
            string.IsNullOrEmpty(PicoWebSocketClient.Instance.PlayerId))
        {
            Debug.LogWarning("[PicoBomb] WebSocketClient未初始化或PlayerId未分配");
            return;
        }

        // 简化版本 - 直接发送世界坐标，不需要转换  
        Vector3 sendPosition = bomb.transform.position;

        Debug.Log($"[PicoBomb] SyncBombPlacement called! bombId={bombId}");
        Debug.Log(System.Environment.StackTrace);

        // 使用本地数据类，不依赖 NetworkMessages  
        var bombData = new BombCreate
        {
            type = "BombCreate",
            playerId = PicoWebSocketClient.Instance.PlayerId,
            teamId = PicoWebSocketClient.Instance.TeamId,
            position = new Vec3(sendPosition),  // 直接使用世界坐标  
            bombType = currentBombType,
            bombId = bombId,
            timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };

        // 直接序列化并发送  
        string json = JsonUtility.ToJson(bombData);
        PicoWebSocketClient.Instance.SendRawMessage(json);

        Debug.Log($"[PicoBomb] 炸弹Type: {this.currentBombType}, Position: {sendPosition}");
    }


    /// <summary>
    /// 应用拖拽视觉效果（透明度调整）
    /// </summary>
    private void ApplyDraggingVisuals(GameObject bomb)
    {
        // 降低透明度表示正在拖拽
        foreach (var renderer in bomb.GetComponentsInChildren<Renderer>())
        {
            foreach (var material in renderer.materials)
            {
                // 确保材质支持透明度
                if (material.HasProperty("_Color"))
                {
                    Color color = material.color;
                    color.a = 0.5f; // 可以调整透明度
                    material.color = color;
                }
            }
        }
    }

    /// <summary>
    /// 移除拖拽视觉效果
    /// </summary>
    private void RemoveDraggingVisuals(GameObject bomb)
    {
        // 恢复透明度
        foreach (var renderer in bomb.GetComponentsInChildren<Renderer>())
        {
            foreach (var material in renderer.materials)
            {
                if (material.HasProperty("_Color"))
                {
                    Color color = material.color;
                    color.a = 1f;
                    material.color = color;
                }
            }
        }
    }

    /// <summary>
    /// 更新炸弹放置冷却时间
    /// </summary>
    private void UpdateBombPlacementCooldown()
    {
        if (_bombPlacementCooldown > 0)
        {
            _bombPlacementCooldown -= Time.deltaTime;
        }
    }

    /// <summary>
    /// 自动查找右手控制器
    /// </summary>
    private XRController FindRightController()
    {
        var controllers = FindObjectsOfType<XRController>();
        foreach (var controller in controllers)
        {
            // 查找标记为 Right 的控制器
            if (controller.name.Contains("Right") || controller.controllerNode == UnityEngine.XR.XRNode.RightHand)
            {
                return controller;
            }
        }

        //Debug.LogWarning("[PicoBomb] 无法自动找到右手控制器，请手动指定");
        return null;
    }

    // TTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTT
    // 调试接口
    // TTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTT

    public void SetBombExplosionDelay(float delay)
    {
        bombExplosionDelay = delay;
        //Debug.Log($"[PicoBomb] 爆炸延迟设置为: {delay}秒");
    }

    private void DetectBButtonInput()
    {
        if (rightController == null) return;

        bool bPressed = false;
        if (rightController.inputDevice.IsPressed(
            InputHelpers.Button.SecondaryButton,  // B键对应SecondaryButton  
            out bPressed
        ))
        {
            // 仅在B键按下的边界检测（防止连续切换）  
            if (bPressed && !_bButtonPressedLastFrame)
            {
                OnBButtonPressed();
            }
            _bButtonPressedLastFrame = bPressed;
        }
    }

    private void OnBButtonPressed()
    {
        //只有在放置了炸弹且没有拖拽时才工作  
        if (!_hasPlacedBomb || _draggingBomb != null)
        {
            Debug.Log("[PicoBomb] B键被禁用：要么正在拖拽，要么还没放置炸弹");
            return;
        }
        if (_bombTypeUpdateCooldown > 0)
        {
            Debug.Log("[PicoBomb] B键冷却中，请稍候...");
            return;
        }
        // 循环切换 type："长条形" → "横条形" → "十字形" → "正方形" → "长条形"  
        currentBombTypeIndex++;
        if (currentBombTypeIndex >= bombTypeList.Length)  // 自动支持任意长度  
        {
            currentBombTypeIndex = 0;
        }

        currentBombType = bombTypeList[currentBombTypeIndex];
        Debug.Log($"[PicoBomb] 炸弹Type已切换为: {currentBombType}");

        if (_draggingBomb != null)
        {
            UpdateDraggingBombTypeVisual(_draggingBomb);
        }
        // 发送类型更新消息  
        SendBombTypeUpdate(_lastPlacedBombId, currentBombType);
        _bombTypeUpdateCooldown = TYPE_UPDATE_COOLDOWN;
    }

    /// <summary>  
    /// 发送炸弹类型更新消息  
    /// </summary>  
    private void SendBombTypeUpdate(string bombId, string newBombType)
    {
        if (PicoWebSocketClient.Instance == null ||
            string.IsNullOrEmpty(PicoWebSocketClient.Instance.PlayerId))
        {
            Debug.LogWarning("[PicoBomb] WebSocketClient未初始化或PlayerId未分配");
            return;
        }

        // 复用 BombCreate 类发送更新（只改 type 标识和 bombType）  
        var updateMsg = new BombCreate
        {
            type = "BombTypeUpdate",  // 服务端识别为类型更新  
            bombId = bombId,
            playerId = PicoWebSocketClient.Instance.PlayerId,
            teamId = PicoWebSocketClient.Instance.TeamId,
            bombType = newBombType,
            timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };

        string json = JsonUtility.ToJson(updateMsg);
        PicoWebSocketClient.Instance.SendRawMessage(json);

        Debug.Log($"[PicoBomb] 已发送炸弹类型更新: BombId={bombId}, NewType={newBombType}");
    }

    // 更新拖拽炸弹的视觉显示，反映不同的type  
    private void UpdateDraggingBombTypeVisual(GameObject bomb)
    {
        // 可选：改变材质、大小或显示type标记等  
        var renderer = bomb.GetComponent<Renderer>();
        if (renderer != null)
        {
            // 根据type改变颜色或其他视觉属性  
            Material mat = new Material(renderer.material);
            mat.color = GetBombTypeColor(currentBombType);
            renderer.material = mat;
        }
    }

    private Color GetBombTypeColor(string bombType)  // 改为 string  
    {
        return bombType switch
        {
            "长条形" => new Color(1f, 0.5f, 0f),    // 橙色  
            "横条形" => new Color(0f, 0.5f, 1f),    // 浅蓝  
            "十字形" => new Color(1f, 0f, 1f),      // 紫红  
            "正方形" => new Color(0f, 0f, 0f),
            _ => Color.white
        };
    }
    /// <summary>  
    /// 增加可用炸弹数量（当远程炸弹爆炸销毁时调用）  
    /// </summary>  
    public void AddBombCount(int amount = 1)
    {
        CurrentBombCount += amount;
        CurrentBombCount = Mathf.Min(MAX_BOMB_COUNT, CurrentBombCount);
        OnBombCountChanged?.Invoke(CurrentBombCount);
        Debug.Log($"[PicoBomb] 炸弹数量增加，现在剩余: {CurrentBombCount}");
    }
    public bool IsDraggingBomb() => _draggingBomb != null;
    public float GetCooldownTimeRemaining() => _bombPlacementCooldown;



    //平面检测
    /// <summary>
    /// 执行射线检测（同时与平面检测集成）
    /// </summary>
    private bool PerformRaycast(out RaycastHit hit)
    {
        if (rightHandAnchor == null)
        {
            hit = default;
            return false;
        }

        // 步骤1：先尝试与平面检测数据相交[^44]
        Ray ray = new Ray(rightHandAnchor.position, rightHandAnchor.forward);

        if (RaycastAgainstDetectedPlanes(ray, out Vector3 planeHitPoint))
        {
            // 创建一个虚拟的 RaycastHit 用于返回
            hit = new RaycastHit();
            hit.point = planeHitPoint;
            hit.normal = Vector3.up; // 地面法线通常是向上的
            hit.distance = Vector3.Distance(rightHandAnchor.position, planeHitPoint);
            //Debug.Log($"[PicoBomb] 射线击中平面: {hit.point}");
            return true;
        }

        // 步骤2：回退到物理射线检测（用于其他物体）
        bool raycastHit = Physics.Raycast(ray, out hit, raycastMaxDistance, raycastLayerMask);
        if (raycastHit)
        {
            Debug.Log($"[PicoBomb] 射线击中物体: {hit.collider.name}");
        }

        return raycastHit;
    }

    /// <summary>
    /// 与检测到的平面进行射线检测[^44]
    /// 返回射线与平面的交点
    /// </summary>
    private bool RaycastAgainstDetectedPlanes(Ray ray, out Vector3 hitPoint)
    {
        hitPoint = Vector3.zero;

        PlaneDetectionManager planeManager = PlaneDetectionManager.Instance;
        if (planeManager == null)
            return false;

        System.Collections.Generic.List<PxrPlaneData> floorPlanes = planeManager.GetFloorPlanes();
        if (floorPlanes.Count == 0)
        {
            Debug.LogWarning("[PicoBomb] 未检测到地面平面");
            return false;
        }

        float closestDistance = float.MaxValue;
        bool foundHit = false;

        // 遍历所有地面平面，找到最近的交点
        foreach (PxrPlaneData planeData in floorPlanes)
        {
            if (RayIntersectsPlane(ray, planeData, out Vector3 intersection, out float distance))
            {
                // 检查交点是否在平面边界内
                if (IsPointInPlaneBounds(intersection, planeData))
                {
                    if (distance < closestDistance)
                    {
                        closestDistance = distance;
                        hitPoint = intersection;
                        foundHit = true;
                        Debug.Log($"[PicoBomb] 找到平面交点: {intersection}, 距离: {distance}");
                    }
                }
            }
        }

        return foundHit;
    }

    /// <summary>
    /// 计算射线与平面的交点[^44]
    /// 使用平面数据的position和rotation
    /// </summary>
    private bool RayIntersectsPlane(Ray ray, PxrPlaneData planeData, out Vector3 intersection, out float distance)
    {
        intersection = Vector3.zero;
        distance = 0f;

        // 从平面的rotation和position构造平面法线
        Vector3 planeNormal = planeData.rotation * Vector3.up; // 假设平面向上
        Vector3 planePoint = planeData.position;

        // 计算射线与平面的交点
        float denominator = Vector3.Dot(planeNormal, ray.direction);

        if (Mathf.Abs(denominator) < 0.0001f)
        {
            // 射线平行于平面
            return false;
        }

        float numerator = Vector3.Dot(planeNormal, planePoint - ray.origin);
        distance = numerator / denominator;

        if (distance < 0)
        {
            // 交点在射线后面
            return false;
        }

        intersection = ray.origin + ray.direction * distance;
        return true;
    }

    /// <summary>
    /// 检查点是否在平面的2D边界框内[^44]
    /// </summary>
    private bool IsPointInPlaneBounds(Vector3 point, PxrPlaneData planeData)
    {
        if (planeData.vertices != null && planeData.vertices.Length > 0)
        {
            return IsPointInPolygon(point, planeData);
        }

        PxrSceneBox2D box2D = planeData.box2D;
        Vector3 localPoint = planeData.rotation * (point - planeData.position);

        float minX = box2D.offset.x - box2D.extent.width;
        float maxX = box2D.offset.x + box2D.extent.width;
        float minZ = box2D.offset.y - box2D.extent.height;
        float maxZ = box2D.offset.y + box2D.extent.height;

        //Debug.Log($"[PlaneDetection] 检查点在边界内: " +
            //$"X[{minX},{maxX}], Z[{minZ},{maxZ}], 点坐标: ({localPoint.x},{localPoint.z})");

        return localPoint.x >= minX && localPoint.x <= maxX &&
               localPoint.z >= minZ && localPoint.z <= maxZ;
    }
    /// <summary>
    /// 使用平面顶点进行更精确的点-多边形检测
    /// </summary>
    private bool IsPointInPolygon(Vector3 point, PxrPlaneData planeData)
    {
        if (planeData.vertices == null || planeData.vertices.Length < 3)
            return false;

        // 简化的点-多边形检测（假设平面是凸多边形）
        Vector3 planeNormal = planeData.rotation * Vector3.up;

        for (int i = 0; i < planeData.indices.Length; i += 3)
        {
            Vector3 v1 = planeData.vertices[planeData.indices[i]];
            Vector3 v2 = planeData.vertices[planeData.indices[i + 1]];
            Vector3 v3 = planeData.vertices[planeData.indices[i + 2]];

            // 将顶点转换到世界坐标
            v1 = planeData.position + planeData.rotation * v1;
            v2 = planeData.position + planeData.rotation * v2;
            v3 = planeData.position + planeData.rotation * v3;

            if (PointInTriangle(point, v1, v2, v3))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 检查点是否在三角形内
    /// </summary>
    private bool PointInTriangle(Vector3 p, Vector3 a, Vector3 b, Vector3 c)
    {
        // 使用重心坐标法
        Vector3 v0 = c - a;
        Vector3 v1 = b - a;
        Vector3 v2 = p - a;

        float dot00 = Vector3.Dot(v0, v0);
        float dot01 = Vector3.Dot(v0, v1);
        float dot02 = Vector3.Dot(v0, v2);
        float dot11 = Vector3.Dot(v1, v1);
        float dot12 = Vector3.Dot(v1, v2);

        float invDenom = 1f / (dot00 * dot11 - dot01 * dot01);
        float u = (dot11 * dot02 - dot01 * dot12) * invDenom;
        float v = (dot00 * dot12 - dot01 * dot02) * invDenom;

        return (u >= 0) && (v >= 0) && (u + v <= 1);
    }

}
// 在 PicoBombPlacement.cs 顶部或单独文件中  
[System.Serializable]
public class BombCreate
{
    public string type;
    public string bombId;
    public string playerId;
    public string teamId;
    public Vec3 position;
    public string bombType;
    public string bombLevel = "一级";
    public long timestamp;
}