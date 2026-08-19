using UnityEngine;
using TMPro;
using BubbleBattle.Network;
using BubbleBattle;

public class NetworkUIManager : MonoBehaviour
{
    [SerializeField] private Canvas connectedCanvas;
    [SerializeField] private Canvas disconnectedCanvas;
    [SerializeField] private TMP_Text roomNumberText; // B Canvas 上显示房间号的 Text

    private int _selectedRoom = 1;
    private const int MAX_ROOM = 10;
    private const int MIN_ROOM = 1;

    // 按键防抖
    private bool _aWasPressed = false;
    private bool _xWasPressed = false;
    private bool _yWasPressed = false;

    private void OnEnable()
    {
        if (PicoWebSocketClient.Instance != null)
        {
            PicoWebSocketClient.Instance.OnConnected += ShowConnectedCanvas;
            PicoWebSocketClient.Instance.OnDisconnected += ShowDisconnectedCanvas;

            if (!string.IsNullOrEmpty(PicoWebSocketClient.Instance.PlayerId))
                ShowConnectedCanvas();
            else
                ShowDisconnectedCanvas();
        }
        else
        {
            ShowDisconnectedCanvas();
        }

        UpdateRoomText();
    }

    private void OnDisable()
    {
        if (PicoWebSocketClient.Instance != null)
        {
            PicoWebSocketClient.Instance.OnConnected -= ShowConnectedCanvas;
            PicoWebSocketClient.Instance.OnDisconnected -= ShowDisconnectedCanvas;
        }
    }

    private void Update()
    {
        // 只在 B Canvas 显示时响应输入
        if (disconnectedCanvas == null || !disconnectedCanvas.enabled) return;

        // 右手 A 键 → 下一个房间
        bool aPressed = UnityEngine.XR.InputDevices
            .GetDeviceAtXRNode(UnityEngine.XR.XRNode.RightHand)
            .TryGetFeatureValue(UnityEngine.XR.CommonUsages.primaryButton, out bool aVal) && aVal;

        if (aPressed && !_aWasPressed)
        {
            _selectedRoom = (_selectedRoom >= MAX_ROOM) ? MIN_ROOM : _selectedRoom + 1;
            UpdateRoomText();
            //Debug.Log($"[NetworkUIManager] 切换到房间 {_selectedRoom}");
        }
        _aWasPressed = aPressed;

        // 左手 X 键 → 上一个房间
        bool xPressed = UnityEngine.XR.InputDevices
            .GetDeviceAtXRNode(UnityEngine.XR.XRNode.LeftHand)
            .TryGetFeatureValue(UnityEngine.XR.CommonUsages.primaryButton, out bool xVal) && xVal;

        if (xPressed && !_xWasPressed)
        {
            _selectedRoom = (_selectedRoom <= MIN_ROOM) ? MAX_ROOM : _selectedRoom - 1;
            UpdateRoomText();
            //Debug.Log($"[NetworkUIManager] 切换到房间 {_selectedRoom}");
        }
        _xWasPressed = xPressed;

        // 左手 Y 键 → 进入房间
        bool yPressed = UnityEngine.XR.InputDevices
            .GetDeviceAtXRNode(UnityEngine.XR.XRNode.LeftHand)
            .TryGetFeatureValue(UnityEngine.XR.CommonUsages.secondaryButton, out bool yVal) && yVal;

        if (yPressed && !_yWasPressed)
        {
            //Debug.Log($"[NetworkUIManager] 确认进入房间 {_selectedRoom}");
            ConnectToRoom();
        }
        _yWasPressed = yPressed;
    }

    private async void ConnectToRoom()
    {
        if (roomNumberText != null)
            roomNumberText.text = "连接服务器...";
        if (PicoWebSocketClient.Instance != null)
            await PicoWebSocketClient.Instance.ConnectToRoom(_selectedRoom);
        //else
            //Debug.LogError("[NetworkUIManager] PicoWebSocketClient.Instance 为空！");
    }

    private void UpdateRoomText()
    {
        if (roomNumberText != null)
            roomNumberText.text = $"房间 {_selectedRoom}";
    }

    private void ShowConnectedCanvas()
    {
        connectedCanvas.enabled = true;
        disconnectedCanvas.enabled = false;
        //Debug.Log("[NetworkUIManager] 显示已连接Canvas");
    }

    private void ShowDisconnectedCanvas()
    {
        connectedCanvas.enabled = false;
        disconnectedCanvas.enabled = true;
        //Debug.Log("[NetworkUIManager] 显示未连接Canvas");
        FindObjectOfType<SilencePropController>()?.HidePropUI();
    }
}