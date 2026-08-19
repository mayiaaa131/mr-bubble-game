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

    void HandleUIRaycast()
    {
        Canvas[] canvases = FindObjectsOfType<Canvas>();
        foreach (var canvas in canvases)
        {
            if (!canvas.gameObject.activeInHierarchy) continue;
            GraphicRaycaster gr = canvas.GetComponent<GraphicRaycaster>();
            if (gr == null) continue;
            RaycastToCanvas(canvas, gr);
        }
    }

    void RaycastToCanvas(Canvas canvas, GraphicRaycaster gr)
    {
        Plane canvasPlane = new Plane(-canvas.transform.forward, canvas.transform.position);
        Ray ray = new Ray(transform.position, transform.forward);

        if (!canvasPlane.Raycast(ray, out float enter)) return;

        Vector3 hitPoint = ray.GetPoint(enter);
        RectTransform canvasRT = canvas.GetComponent<RectTransform>();

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRT,
                manager.mainCamera.WorldToScreenPoint(hitPoint),
                manager.mainCamera,
                out Vector2 localPoint)) return;

        if (!canvasRT.rect.Contains(localPoint)) return;

        _pointerData.position = manager.mainCamera.WorldToScreenPoint(hitPoint);

        var results = new List<RaycastResult>();
        gr.Raycast(_pointerData, results);

        if (results.Count == 0) return;

        GameObject target = results[0].gameObject;
        ExecuteEvents.Execute(target, _pointerData, ExecuteEvents.pointerEnterHandler);

        if (_triggerVal > 0.7f)
            ExecuteEvents.Execute(target, _pointerData, ExecuteEvents.pointerDownHandler);
        else
            ExecuteEvents.Execute(target, _pointerData, ExecuteEvents.pointerUpHandler);
    }

    // ── 公开属性（供Manager读取）─────────────────────

    public float TriggerValue => _triggerVal;
}