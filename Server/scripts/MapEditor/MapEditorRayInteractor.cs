using UnityEngine;
using UnityEngine.XR;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// 手柄射线交互器 v2
/// 修正：Trigger值改用 InputDevices + CommonUsages.trigger
/// </summary>
[RequireComponent(typeof(LineRenderer))]
public class MapEditorRayInteractor : MonoBehaviour
{
    [Header("配置")]
    public bool isRightHand = true;
    public MapEditorManager manager;

    [Header("射线视觉")]
    public float maxRayLength = 10f;
    public Color normalColor = new Color(1f, 1f, 1f, 0.5f);
    public Color hoverColor = new Color(0f, 1f, 1f, 0.8f);
    public Color selectingColor = new Color(1f, 0.8f, 0f, 0.9f);
    public GameObject dotReticle;

    // ── 内部状态 ──────────────────────────────────────
    private LineRenderer _line;
    private InputDevice _device;
    private float _triggerVal;

    // UI交互
    private PointerEventData _pointerData;

    void Awake()
    {
        _line = GetComponent<LineRenderer>();
        _line.positionCount = 2;
        _line.startWidth = 0.004f;
        _line.endWidth = 0.001f;
        _line.useWorldSpace = true;
        _line.material = new Material(Shader.Find("Sprites/Default"));

        _pointerData = new PointerEventData(EventSystem.current);
    }

    void Start()
    {
        RefreshDevice();
        InputDevices.deviceConnected += _ => RefreshDevice();
        InputDevices.deviceDisconnected += _ => RefreshDevice();
    }

    void RefreshDevice()
    {
        var devices = new List<InputDevice>();
        InputDevices.GetDevicesAtXRNode(
            isRightHand ? XRNode.RightHand : XRNode.LeftHand, devices);
        if (devices.Count > 0) _device = devices[0];
    }

    void Update()
    {
        if (!_device.isValid) RefreshDevice();

        // 读取Trigger值
        _device.TryGetFeatureValue(CommonUsages.trigger, out _triggerVal);

        UpdateRayVisual();

        // 只有右手做UI射线交互
        if (isRightHand) HandleUIRaycast();
    }

    // ── 射线可视化 ───────────────────────────────────

    void UpdateRayVisual()
    {
        Ray ray = new Ray(transform.position, transform.forward);
        float hitDist = maxRayLength;
        bool hitAnything = false;

        LayerMask detectMask = manager.placedObjectLayer
                             | manager.mrRaycastLayer
                             | manager.uiLayer;

        if (Physics.Raycast(ray, out RaycastHit hit, maxRayLength, detectMask))
        {
            hitDist = hit.distance;
            hitAnything = true;
        }

        _line.SetPosition(0, transform.position);
        _line.SetPosition(1, ray.GetPoint(hitDist));

        Color c = _triggerVal > 0.7f ? selectingColor
                : hitAnything ? hoverColor
                                     : normalColor;
        _line.startColor = c;
        _line.endColor = c;

        if (dotReticle != null)
        {
            dotReticle.SetActive(hitAnything);
            if (hitAnything) dotReticle.transform.position = ray.GetPoint(hitDist);
        }
    }

    // ── UI射线交互 ───────────────────────────────────

    private GameObject _lastPointerDownTarget = null;
    private bool _triggerWasDown = false;

    void HandleUIRaycast()
    {
        bool triggerDown = _triggerVal > 0.7f;

        Canvas[] canvases = FindObjectsOfType<Canvas>();
        GameObject hitTarget = null;

        foreach (var canvas in canvases)
        {
            if (!canvas.gameObject.activeInHierarchy) continue;
            GraphicRaycaster gr = canvas.GetComponent<GraphicRaycaster>();
            if (gr == null) continue;
            hitTarget = RaycastToCanvas(canvas, gr);
            if (hitTarget != null) break;
        }

        // Hover
        if (hitTarget != null)
            ExecuteEvents.Execute(hitTarget, _pointerData, ExecuteEvents.pointerEnterHandler);

        // Trigger 按下瞬间
        if (triggerDown && !_triggerWasDown)
        {
            _lastPointerDownTarget = hitTarget;
            if (hitTarget != null)
                ExecuteEvents.Execute(hitTarget, _pointerData, ExecuteEvents.pointerDownHandler);
        }

        // Trigger 松开瞬间 → 触发 Click
        if (!triggerDown && _triggerWasDown)
        {
            if (hitTarget != null)
                ExecuteEvents.Execute(hitTarget, _pointerData, ExecuteEvents.pointerUpHandler);

            // Down 和 Up 在同一目标上才算 Click
            if (hitTarget != null && hitTarget == _lastPointerDownTarget)
                ExecuteEvents.Execute(hitTarget, _pointerData, ExecuteEvents.pointerClickHandler);

            _lastPointerDownTarget = null;
        }

        _triggerWasDown = triggerDown;
    }

    // 返回命中的 GameObject，没命中返回 null
    GameObject RaycastToCanvas(Canvas canvas, GraphicRaycaster gr)
    {
        Plane canvasPlane = new Plane(-canvas.transform.forward, canvas.transform.position);
        Ray ray = new Ray(transform.position, transform.forward);

        if (!canvasPlane.Raycast(ray, out float enter)) return null;

        Vector3 hitPoint = ray.GetPoint(enter);
        RectTransform canvasRT = canvas.GetComponent<RectTransform>();

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRT,
                Camera.main.WorldToScreenPoint(hitPoint),
                Camera.main,
                out Vector2 localPoint)) return null;

        if (!canvasRT.rect.Contains(localPoint)) return null;

        _pointerData.position = Camera.main.WorldToScreenPoint(hitPoint);

        var results = new List<RaycastResult>();
        gr.Raycast(_pointerData, results);

        return results.Count > 0 ? results[0].gameObject : null;
    }

    // ── 公开属性（供Manager读取）─────────────────────

    public float TriggerValue => _triggerVal;
}