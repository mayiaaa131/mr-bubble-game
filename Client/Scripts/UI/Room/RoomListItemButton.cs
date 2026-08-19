using UnityEngine;
using UnityEngine.UI;
using TMPro;  // ? 新增引用

/// <summary>
/// 房间列表项按钮脚本
/// 对标 MapSelectPanel 的实现方式
/// ★ 每个按钮都有自己独立的脚本和数据
/// ? 改进：支持 TextMeshProUGUI 和 Legacy Text
/// </summary>
public class RoomListItemButton : MonoBehaviour
{
    [Header("按钮数据")]
    private string _roomId;
    private string _roomName;
    private RoomListManager _manager;

    private Button _button;
    private TextMeshProUGUI _roomNameTMP;   // ? 新增 TMP 引用
    private Text _roomNameText;            // 保留 Legacy Text 备选

    private Image _buttonImage;

    /// <summary>
    /// 初始化按钮数据
    /// ★ 对标 MapSelectPanel 中 CreateMapButton 的做法
    /// ? 改进：支持两种文本组件
    /// </summary>
    public void Initialize( string roomId, string roomName, RoomListManager manager )
    {
        _roomId = roomId;
        _roomName = roomName;
        _manager = manager;

        // ? 获取必要的 UI 组件
        _button = GetComponent<Button>();
        _buttonImage = GetComponent<Image>();

        // ? 改进：先尝试 TextMeshProUGUI，再尝试 Legacy Text
        _roomNameTMP = GetComponentInChildren<TextMeshProUGUI>();
        if (_roomNameTMP == null)
        {
            _roomNameText = GetComponentInChildren<Text>();
        }

        // ? 更新显示文本
        if (_roomNameTMP != null)
        {
            _roomNameTMP.text = _roomName;
            Debug.Log($"[RoomListItemButton] 设置文本 (TextMeshProUGUI): {_roomName}");
        }
        else if (_roomNameText != null)
        {
            _roomNameText.text = _roomName;
            Debug.Log($"[RoomListItemButton] 设置文本 (Legacy Text): {_roomName}");
        }
        else
        {
            Debug.LogWarning($"?? {gameObject.name} 上没有找到 Text 或 TextMeshProUGUI 组件");
        }

        Debug.Log($"[RoomListItemButton] 按钮已初始化: {_roomName} ({_roomId})");
    }

    /// <summary>
    /// 获取房间ID
    /// </summary>
    public string GetRoomId( )
    {
        return _roomId;
    }

    /// <summary>
    /// 获取房间名称
    /// </summary>
    public string GetRoomName( )
    {
        return _roomName;
    }

    /// <summary>
    /// 获取管理器引用
    /// </summary>
    public RoomListManager GetManager( )
    {
        return _manager;
    }

    /// <summary>
    /// 获取按钮组件
    /// </summary>
    public Button GetButton( )
    {
        return _button;
    }

    /// <summary>
    /// 获取按钮的 Image 组件（用于高亮）
    /// </summary>
    public Image GetButtonImage( )
    {
        return _buttonImage;
    }

    /// <summary>
    /// 销毁时清理
    /// </summary>
    private void OnDestroy( )
    {
        //Debug.Log($"[RoomListItemButton] 房间按钮已销毁: {_roomName}");
    }
}
