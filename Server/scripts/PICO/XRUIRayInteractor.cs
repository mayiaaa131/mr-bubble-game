using UnityEngine;
using UnityEngine.XR;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Collections.Generic;

[RequireComponent(typeof(LineRenderer))]
public class XRUIRayInteractor : MonoBehaviour
{
    [Header("…Ë÷√")]
    public bool isRightHand = true;
    public float maxRayLength = 10f;
    public Color normalColor = new Color(1f, 1f, 1f, 0.5f);
    public Color hoverColor = new Color(0f, 1f, 1f, 0.8f);
    public Color selectingColor = new Color(1f, 0.8f, 0f, 0.9f);
    public GameObject dotReticle;

    private LineRenderer _line;
    private InputDevice _device;
    private float _triggerVal;
    private PointerEventData _pointerData;

    private GameObject _lastPointerDownTarget;
    private bool _triggerWasDown;

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
        InputDevices.GetDevicesAtXRNode(isRightHand ? XRNode.RightHand : XRNode.LeftHand, devices);
        if (devices.Count > 0) _device = devices[0];
    }

    void Update()
    {
        if (!_device.isValid) RefreshDevice();
        _device.TryGetFeatureValue(CommonUsages.trigger, out _triggerVal);

        UpdateRayVisual();
        HandleUIRaycast();
    }

    void UpdateRayVisual()
    {
        Ray ray = new Ray(transform.position, transform.forward);
        float hitDist = maxRayLength;
        bool hitAnything = false;

        if (Physics.Raycast(ray, out RaycastHit hit, maxRayLength))
        {
            hitDist = hit.distance;
            hitAnything = true;
        }

        _line.SetPosition(0, transform.position);
        _line.SetPosition(1, ray.GetPoint(hitDist));

        Color c = _triggerVal > 0.7f ? selectingColor
                : hitAnything ? hoverColor : normalColor;
        _line.startColor = c;
        _line.endColor = c;

        if (dotReticle != null)
        {
            dotReticle.SetActive(hitAnything);
            if (hitAnything) dotReticle.transform.position = ray.GetPoint(hitDist);
        }
    }

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

        if (hitTarget != null)
            ExecuteEvents.Execute(hitTarget, _pointerData, ExecuteEvents.pointerEnterHandler);

        if (triggerDown && !_triggerWasDown)
        {
            _lastPointerDownTarget = hitTarget;
            if (hitTarget != null)
                ExecuteEvents.Execute(hitTarget, _pointerData, ExecuteEvents.pointerDownHandler);
        }

        if (!triggerDown && _triggerWasDown)
        {
            if (hitTarget != null)
                ExecuteEvents.Execute(hitTarget, _pointerData, ExecuteEvents.pointerUpHandler);
            if (hitTarget != null && hitTarget == _lastPointerDownTarget)
                ExecuteEvents.Execute(hitTarget, _pointerData, ExecuteEvents.pointerClickHandler);
            _lastPointerDownTarget = null;
        }

        _triggerWasDown = triggerDown;
    }

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

    public float TriggerValue => _triggerVal;
}