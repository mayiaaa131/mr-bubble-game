// SilencePropController.cs
using System.Collections;
using UnityEngine;
using UnityEngine.XR;
using BubbleBattle.Network;

namespace BubbleBattle
{
    /// <summary>
    /// 挂载在左手控制器 GameObject 上。
    /// 负责沉默道具的持有、射线预览、放置发包以及接收广播后生成道具实例。
    /// </summary>
    public class SilencePropController : MonoBehaviour
    {
        // ── Inspector 配置 ────────────────────────────────────────────────
        [Header("射线配置")]
        [SerializeField] private float rayMaxDistance = 20f;
        [SerializeField] private LayerMask placementLayerMask = ~0; // 射线检测层

        [Header("预览道具配置")]
        [SerializeField] private GameObject silencePreviewPrefab;       // 默认/兜底射线端点预览  
        [SerializeField] private GameObject silencePreviewPrefabRed;    // 红队射线端点预览预制体  
        [SerializeField] private GameObject silencePreviewPrefabBlue;   // 蓝队射线端点预览预制体  

        [SerializeField] private GameObject silencePlacedPrefab;        // 默认/兜底放置实例  
        [SerializeField] private GameObject silencePlacedPrefabRed;     // 红队放置实例预制体  
        [SerializeField] private GameObject silencePlacedPrefabBlue;    // 蓝队放置实例预制体
        [SerializeField] private GameObject silencePropRoot;
        [Header("效果范围预览配置")]
        [SerializeField] private bool showPlacementRangePreview = true;
        [SerializeField] private Color previewRangeColor = Color.magenta;   // 射线预览时的范围框颜色  
        [SerializeField] private Color placedRangeColor = Color.magenta;   // 放置后实例的范围框颜色 
        [Header("UI 配置")]
        [SerializeField] private GameObject silencePropHUDIcon_Gray;
        [SerializeField] private GameObject silencePropHUDIcon_Red;
        [SerializeField] private GameObject silencePropHUDIcon_Blue;

        [SerializeField] private Transform leftHandAnchor;

        // ── 私有状态 ─────────────────────────────────────────────────────
        private bool _isHolding = false;   // 是否持有沉默道具
        private bool _isTriggerHeld = false;   // Trigger 是否按住
        private bool _hasSentPlace = false;   // 本次按下是否已发包（防重复发送）

        private GameObject _previewInstance;    // 当前射线端点预览实例
        private GameObject _rangePreviewInstance; // 范围预览框
        private LineRenderer _rayLineRenderer;  // 射线可视化

        private InputDevice _leftHandDevice;

        // ── 生命周期 ──────────────────────────────────────────────────────
        void Start()
        {
            // 订阅 WebSocket 事件
            var ws = PicoWebSocketClient.Instance;
            if (ws != null)
            {
                ws.OnSilencePropPickedUp += HandleSilencePropPickedUp;
                ws.OnSilencePropPlaced += HandleSilencePropPlaced;
                ws.OnPlayerAssignedId += HandlePlayerAssignedId;
            }

            // 初始状态：只显示灰色 UI  
            silencePropRoot?.SetActive(false);
            silencePropHUDIcon_Gray?.SetActive(false);
            silencePropHUDIcon_Red?.SetActive(false);
            silencePropHUDIcon_Blue?.SetActive(false);

            // 初始化射线可视化
            _rayLineRenderer = gameObject.AddComponent<LineRenderer>();
            _rayLineRenderer.positionCount = 2;
            _rayLineRenderer.startWidth = 0.01f;
            _rayLineRenderer.endWidth = 0.01f;
            _rayLineRenderer.material = new Material(Shader.Find("Sprites/Default"));
            _rayLineRenderer.startColor = new Color(0.6f, 0f, 0.8f, 0.8f); // 紫色射线
            _rayLineRenderer.endColor = new Color(0.6f, 0f, 0.8f, 0.3f);
            _rayLineRenderer.enabled = false;


            // 获取左手设备
            _leftHandDevice = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
        }
        private void HandlePlayerAssignedId(string playerId)
        {
            silencePropRoot?.SetActive(true);
            silencePropHUDIcon_Gray?.SetActive(true);
            silencePropHUDIcon_Red?.SetActive(false);
            silencePropHUDIcon_Blue?.SetActive(false);
        }
        public void HidePropUI()
        {
            silencePropRoot?.SetActive(false);
        }
        void OnDestroy()
        {
            var ws = PicoWebSocketClient.Instance;
            if (ws != null)
            {
                ws.OnSilencePropPickedUp -= HandleSilencePropPickedUp;
                ws.OnSilencePropPlaced -= HandleSilencePropPlaced;
                ws.OnPlayerAssignedId -= HandlePlayerAssignedId;
            }
        }

        void Update()
        {
            // 设备可能在运行时才就绪
            if (!_leftHandDevice.isValid)
                _leftHandDevice = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);

            if (_isHolding)
                HandleTriggerInput();
        }

        // ── 事件处理：拾取通知 ────────────────────────────────────────────
        private void HandleSilencePropPickedUp()
        {
            _isHolding = true;
            _hasSentPlace = false;

            string teamId = PicoWebSocketClient.Instance?.TeamId ?? "";

            silencePropHUDIcon_Gray?.SetActive(false);
            silencePropHUDIcon_Red?.SetActive(teamId.ToLower().Contains("red"));
            silencePropHUDIcon_Blue?.SetActive(teamId.ToLower().Contains("blue"));
        }

        // ── 事件处理：放置广播 ────────────────────────────────────────────
        private void HandleSilencePropPlaced(SilencePropPlacedMsg msg)
        {
            // 所有客户端均生成视觉实例，区分队伍颜色可在 prefab 内处理
            Vector3 placePos = new Vector3(msg.position.x, msg.position.y, msg.position.z);
            StartCoroutine(SpawnSilencePlacedInstance(placePos, msg));
        }

        private IEnumerator SpawnSilencePlacedInstance(Vector3 position, SilencePropPlacedMsg msg)
        {
            // 修改：根据 msg.placedByTeamId 动态选择放置预制体  
            GameObject prefab = GetPlacedPrefabByTeam(msg.placedByTeamId);

            if (prefab == null)
            {
                Debug.LogWarning("[SilencePropController] 无可用放置预制体，跳过生成");
                yield break;
            }

            GameObject instance = Instantiate(prefab, position, Quaternion.Euler(-90f, 0f, 0f));
            instance.name = $"SilencePlaced_{msg.placedByPlayerId}_{msg.timestamp}";

            // 预制体本身已区分队伍颜色，注释掉动态着色，避免颜色被覆盖  
            // ApplyTeamColor(instance, msg.placedByTeamId);  

            // 生成范围可视化（保持不变）  
            CreatePlacedRangeVisual(instance, position, msg.effectHalfSize);

            Debug.Log($"[SilencePropController] 生成沉默道具实例: " +
                      $"放置者={msg.placedByPlayerId}, 队伍={msg.placedByTeamId}, " +
                      $"预制体={prefab.name}, 存活={msg.duration}s");

            yield return new WaitForSeconds(msg.duration);
            if (instance != null) Destroy(instance);
        }

        // ── Trigger 输入逻辑 ──────────────────────────────────────────────
        // ──────────────────────────────────────────────────────────  
        //  修复：Trigger刚按下时才创建预览（参考 OnTriggerPressed）  
        // ──────────────────────────────────────────────────────────  
        private void HandleTriggerInput()
        {
            bool triggerPressed = false;
            _leftHandDevice.TryGetFeatureValue(CommonUsages.triggerButton, out triggerPressed);

            if (triggerPressed && !_isTriggerHeld)
            {
                // Trigger 刚按下：只在这里创建一次预览实例  
                _isTriggerHeld = true;
                _hasSentPlace = false;
                EnsurePreviewExists();          // ← 只创建一次！  
                ShowRayAndPreview(true);
                Debug.Log("[SilencePropController] Trigger 按下，显示射线预览");
            }
            else if (triggerPressed && _isTriggerHeld)
            {
                // Trigger 持续按住：只更新位置，不再重复创建  
                UpdateRayAndPreview();
            }
            else if (!triggerPressed && _isTriggerHeld)
            {
                _isTriggerHeld = false;
                ShowRayAndPreview(false);
                if (!_hasSentPlace)
                {
                    PlaceSilenceProp();
                    _hasSentPlace = true;
                }
            }
        }

        // ── 射线 & 预览 ───────────────────────────────────────────────────
        private void ShowRayAndPreview(bool show)
        {
            _rayLineRenderer.enabled = show;

            if (!show)
            {
                // 隐藏预览实例
                if (_previewInstance != null)
                    _previewInstance.SetActive(false);
                if (_rangePreviewInstance != null)
                    _rangePreviewInstance.SetActive(false);
            }
            else
            {
                // 确保预览实例存在
                EnsurePreviewExists();
            }
        }

        private void UpdateRayAndPreview()
        {
            if (_previewInstance == null) return; // 实例不存在直接返回，不重新创建  

            Transform origin = leftHandAnchor != null ? leftHandAnchor : transform;
            PerformRaycast(out Vector3 hitPoint);

            // 更新射线可视化  
            _rayLineRenderer.SetPosition(0, origin.position);
            _rayLineRenderer.SetPosition(1, hitPoint);

            // 直接赋值，不重复 Instantiate  
            _previewInstance.transform.position = hitPoint;
            _previewInstance.SetActive(true);

            if (_rangePreviewInstance != null)
                _rangePreviewInstance.transform.position = hitPoint;
        }
        private bool PerformRaycast(out Vector3 hitPoint)
        {
            //  使用 leftHandAnchor，而不是 transform  
            Transform origin = leftHandAnchor != null ? leftHandAnchor : transform;
            Ray ray = new Ray(origin.position, origin.forward);

            if (Physics.Raycast(ray, out RaycastHit hit, rayMaxDistance, placementLayerMask))
            {
                hitPoint = hit.point;
                return true;
            }
            else
            {
                // 未命中时取射线末端（与炸弹逻辑一致）  
                hitPoint = origin.position + origin.forward * rayMaxDistance * 0.5f;
                return false;
            }
        }
        private void EnsurePreviewExists()
        {
            if (_previewInstance == null)
            {
                // 修改：根据 TeamId 动态选择预览预制体  
                GameObject prefabToUse = GetPreviewPrefabByTeam();

                if (prefabToUse != null)
                {
                    _previewInstance = Instantiate(prefabToUse);
                    _previewInstance.name = "SilencePropPreview";
                }
                else
                {
                    // 无预制体时创建默认球体（保持原有兜底逻辑不变）  
                    _previewInstance = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    _previewInstance.name = "SilencePropPreview_Default";
                    _previewInstance.transform.localScale = Vector3.one * 0.15f;
                    var rend = _previewInstance.GetComponent<Renderer>();
                    if (rend != null)
                    {
                        rend.material = new Material(Shader.Find("Standard"));
                        rend.material.color = new Color(0.6f, 0f, 0.8f, 0.6f);
                    }
                    var col = _previewInstance.GetComponent<Collider>();
                    if (col != null) col.enabled = false;
                }
            }

            // 范围预览框保持不变  
            if (_rangePreviewInstance == null && showPlacementRangePreview)
            {
                _rangePreviewInstance = BuildRangeSquare(Vector3.zero, 2f, previewRangeColor);
                _rangePreviewInstance.name = "SilencePropRangePreview";
            }
        }

        // ── 发送放置包 ────────────────────────────────────────────────────
        private void PlaceSilenceProp()
        {
            if (_previewInstance == null) return;

            Vector3 placePos = _previewInstance.transform.position;
            PicoWebSocketClient.Instance?.SendSilencePropPlace(placePos);

            // 放置后清除持有状态
            _isHolding = false;
            silencePropHUDIcon_Gray?.SetActive(true);
            silencePropHUDIcon_Red?.SetActive(false);
            silencePropHUDIcon_Blue?.SetActive(false);

            // 销毁预览
            if (_previewInstance != null) { Destroy(_previewInstance); _previewInstance = null; }
            if (_rangePreviewInstance != null) { Destroy(_rangePreviewInstance); _rangePreviewInstance = null; }

            Debug.Log($"[SilencePropController] 放置完毕，位置={placePos}，已发送 SilencePropPlace");
        }

        /*
        // ── 辅助：生成正方形范围可视化 ────────────────────────────────────
        /// <param name="halfSize">半边长（预览时固定 1f，放置后使用服务端 effectHalfSize）</param>
        private GameObject BuildRangeSquare(Vector3 center, float halfSize, Color color)
        {
            GameObject root = new GameObject("SilenceRangeSquare");
            root.transform.position = center;

            float y = center.y;
            Vector3[] corners = {
                new Vector3(center.x - halfSize, y, center.z - halfSize),
                new Vector3(center.x + halfSize, y, center.z - halfSize),
                new Vector3(center.x + halfSize, y, center.z + halfSize),
                new Vector3(center.x - halfSize, y, center.z + halfSize),
            };

            AddEdgeLine(root, corners[0], corners[1], color);
            AddEdgeLine(root, corners[1], corners[2], color);
            AddEdgeLine(root, corners[2], corners[3], color);
            AddEdgeLine(root, corners[3], corners[0], color);

            return root;
        }*/
        private GameObject BuildRangeSquare(Vector3 center, float halfSize, Color color)
        {
            GameObject root = new GameObject("SilenceRangeSquare");
            root.transform.position = center;

            //  顶点改为相对父物体的本地坐标（不再依赖 center 的绝对值）  
            Vector3[] corners = {
                    new Vector3(-halfSize, 0f, -halfSize),
                    new Vector3( halfSize, 0f, -halfSize),
                    new Vector3( halfSize, 0f,  halfSize),
                    new Vector3(-halfSize, 0f,  halfSize)
             };

            AddEdgeLine(root, corners[0], corners[1], color);
            AddEdgeLine(root, corners[1], corners[2], color);
            AddEdgeLine(root, corners[2], corners[3], color);
            AddEdgeLine(root, corners[3], corners[0], color);

            return root;
        }

        /*
        private void AddEdgeLine(GameObject parent, Vector3 start, Vector3 end, Color color)
        {
            var go = new GameObject("Edge");
            go.transform.SetParent(parent.transform);

            var lr = go.AddComponent<LineRenderer>();
            lr.positionCount = 2;
            lr.SetPosition(0, start);
            lr.SetPosition(1, end);
            lr.startColor = color;
            lr.endColor = color;
            lr.startWidth = 0.05f;
            lr.endWidth = 0.05f;
            lr.material = new Material(Shader.Find("Sprites/Default"));
        }*/
        private void AddEdgeLine(GameObject parent, Vector3 start, Vector3 end, Color color)
        {
            var go = new GameObject("Edge");
            go.transform.SetParent(parent.transform);
            go.transform.localPosition = Vector3.zero;

            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = false; // 改为本地空间，这样父物体移动时线段跟着动  
            lr.positionCount = 2;
            lr.SetPosition(0, start);
            lr.SetPosition(1, end);
            lr.startColor = color;
            lr.endColor = color;
            lr.startWidth = 0.05f;
            lr.endWidth = 0.05f;
            lr.material = new Material(Shader.Find("Sprites/Default"));
        }

        /// <summary>根据放置方队伍设置道具实例颜色（可扩展）</summary>
        private void ApplyTeamColor(GameObject instance, string teamId)
        {
            // 约定：RedTeam 为红色，BlueTeam 为蓝色，均叠加紫色沉默特效
            // 具体 Shader 或粒子颜色调整在此处实现
            var renderers = instance.GetComponentsInChildren<Renderer>();
            foreach (var rend in renderers)
            {
                if (teamId != null && teamId.ToLower().Contains("red"))
                    rend.material.color = new Color(1f, 0.2f, 0.8f); // 红紫
                else if (teamId != null && teamId.ToLower().Contains("blue"))
                    rend.material.color = new Color(0.2f, 0.4f, 1f); // 蓝紫
                else
                    rend.material.color = new Color(0.6f, 0f, 0.8f); // 默认紫
            }
        }

        /// <summary>
        /// 放置结果广播中携带 effectHalfSize，此方法在生成实例后补充真实范围框
        /// </summary>
        private void CreatePlacedRangeVisual(GameObject parent, Vector3 center, float halfSize)
        {
            GameObject square = BuildRangeSquare(center, halfSize, placedRangeColor);
            square.transform.SetParent(parent.transform);
        }
        /// <summary>  
        /// 根据当前玩家 TeamId 选择射线端点预览预制体  
        /// </summary>  
        private GameObject GetPreviewPrefabByTeam()
        {
            string teamId = PicoWebSocketClient.Instance?.TeamId ?? "";

            if (string.IsNullOrEmpty(teamId))
            {
                Debug.LogWarning("[SilencePropController] TeamId 为空，使用默认预览预制体");
                return silencePreviewPrefab;
            }

            string id = teamId.ToLower();

            if (id.Contains("red"))
            {
                if (silencePreviewPrefabRed != null) return silencePreviewPrefabRed;
                Debug.LogWarning("[SilencePropController] silencePreviewPrefabRed 未赋值，fallback 到默认");
                return silencePreviewPrefab;
            }
            else if (id.Contains("blue"))
            {
                if (silencePreviewPrefabBlue != null) return silencePreviewPrefabBlue;
                Debug.LogWarning("[SilencePropController] silencePreviewPrefabBlue 未赋值，fallback 到默认");
                return silencePreviewPrefab;
            }

            Debug.LogWarning($"[SilencePropController] 未识别 TeamId: {teamId}，使用默认预览预制体");
            return silencePreviewPrefab;
        }

        /// <summary>  
        /// 根据放置方 TeamId 选择放置实例预制体  
        /// </summary>  
        private GameObject GetPlacedPrefabByTeam(string teamId)
        {
            if (string.IsNullOrEmpty(teamId))
            {
                Debug.LogWarning("[SilencePropController] placedByTeamId 为空，使用默认放置预制体");
                return silencePlacedPrefab;
            }

            string id = teamId.ToLower();

            if (id.Contains("red"))
            {
                if (silencePlacedPrefabRed != null) return silencePlacedPrefabRed;
                Debug.LogWarning("[SilencePropController] silencePlacedPrefabRed 未赋值，fallback 到默认");
                return silencePlacedPrefab;
            }
            else if (id.Contains("blue"))
            {
                if (silencePlacedPrefabBlue != null) return silencePlacedPrefabBlue;
                Debug.LogWarning("[SilencePropController] silencePlacedPrefabBlue 未赋值，fallback 到默认");
                return silencePlacedPrefab;
            }

            Debug.LogWarning($"[SilencePropController] 未识别 teamId: {teamId}，使用默认放置预制体");
            return silencePlacedPrefab;
        }
    }
}
