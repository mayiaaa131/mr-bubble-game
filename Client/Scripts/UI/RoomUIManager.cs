using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 房间创建UI管理器（集成优化版本）
/// ✓ 保留版本1的完整功能：地图选择、房间编辑面板
/// ✓ 集成版本2的改进：ForceMeshUpdate()、简化调试日志
/// </summary>
public class RoomUIManager : MonoBehaviour
{
    // ========== 【静态变量】==========
    private static string _lastCreatedRoomId = null;


    public static string LastCreatedRoomId
    {
        get
        {
            Debug.Log($"[LastCreatedRoomId] 读取值: '{_lastCreatedRoomId}'");
            return _lastCreatedRoomId;
        }
        set
        {
            Debug.Log($"[LastCreatedRoomId] 赋值: '{_lastCreatedRoomId}' → '{value}'");
            _lastCreatedRoomId = value;
        }
    }

    // ========== 【左侧选项按钮】==========
    [Header("左侧选项按钮")]
    [SerializeField] private Button option1Button;
    [SerializeField] private Button option2Button;
    [SerializeField] private Button option3Button;
    [SerializeField] private Button option4Button;
    [SerializeField] private Button option5Button;
    [SerializeField] private Button option6Button;
    [SerializeField] private Button option7Button;

    // ========== 【右侧详细页面选项】==========
    [Header("右侧详细页面选项")]
    [SerializeField] private GameObject optionPanel;         // 整体选项容器
    [SerializeField] private GameObject option1Panel;
    [SerializeField] private GameObject option2Panel;
    [SerializeField] private GameObject option3Panel;
    [SerializeField] private GameObject option4Panel;
    [SerializeField] private GameObject option5Panel;
    [SerializeField] private GameObject option6Panel;
    [SerializeField] private GameObject option7Panel;

    // ========== 【选项2内容】==========
    [Header("选项2内容")]
    [SerializeField] private Image option2Background;
    [SerializeField] private TextMeshProUGUI roomNameText;
    [SerializeField] private TextMeshProUGUI roomDescText;

    // ========== 【按钮引用】==========
    [Header("按钮引用")]
    [SerializeField] private Button createRoomButton;
    [SerializeField] private Button confirmToMapButton;      // 进入地图选择
    [SerializeField] private GameObject mapSelectPanel;      // 地图选择界面
    [SerializeField] private Button printRoomsButton;

    // ========== 【房间编辑面板】==========
    [Header("房间编辑面板")]
    [SerializeField] private GameObject roomEditPanel;

    private void Start( )
    {
        PrintDebugInfo();
        InitializeUI();
        BindButtonEvents();
    }

    /// <summary>
    /// 【调试用】打印初始化信息（简化版 - 仅关键检查）
    /// </summary>
    private void PrintDebugInfo( )
    {
        Debug.Log("========== RoomUIManager 初始化 ==========");
        Debug.Log($"option1Button: {(option1Button != null ? "✓" : "❌")}");
        Debug.Log($"option1Panel: {(option1Panel != null ? "✓" : "❌")}");
        Debug.Log($"createRoomButton: {(createRoomButton != null ? "✓" : "❌")}");
        Debug.Log($"confirmToMapButton: {(confirmToMapButton != null ? "✓" : "❌")}");
        Debug.Log($"[初始状态] LastCreatedRoomId: '{_lastCreatedRoomId}'");
        Debug.Log("=======================================");
    }

    private void InitializeUI( )
    {
        Debug.Log("✓ 开始初始化UI");
        ShowPanel(1);

        // 地图选择界面默认隐藏
        if (mapSelectPanel != null)
            mapSelectPanel.SetActive(false);
    }

    private void BindButtonEvents( )
    {
        // 绑定左侧选项按钮
        if (option1Button != null) option1Button.onClick.AddListener(( ) => OnOptionButtonClicked(1));
        if (option2Button != null) option2Button.onClick.AddListener(( ) => OnOptionButtonClicked(2));
        if (option3Button != null) option3Button.onClick.AddListener(( ) => OnOptionButtonClicked(3));
        if (option4Button != null) option4Button.onClick.AddListener(( ) => OnOptionButtonClicked(4));
        if (option5Button != null) option5Button.onClick.AddListener(( ) => OnOptionButtonClicked(5));
        if (option6Button != null) option6Button.onClick.AddListener(( ) => OnOptionButtonClicked(6));
        if (option7Button != null) option7Button.onClick.AddListener(( ) => OnOptionButtonClicked(7));

        // 绑定创建房间按钮
        if (createRoomButton != null)
        {
            createRoomButton.onClick.AddListener(( ) => OnCreateRoomButtonClicked());
            Debug.Log("✓ 创建房间按钮事件已绑定");
        }

        // 绑定确认按钮事件（进入地图选择）
        if (confirmToMapButton != null)
        {
            confirmToMapButton.onClick.AddListener(OnClickConfirmToMap);
            Debug.Log("✓ confirmToMapButton 事件已绑定");
        }

        if (printRoomsButton != null)
        {
            printRoomsButton.onClick.AddListener(( ) =>
            {
                ServerRoomManager.Instance?.PrintAllRooms();
            });
            Debug.Log("✓ printRoomsButton 事件已绑定");
        }
    }

    private void OnOptionButtonClicked( int optionNumber )
    {
        Debug.Log($"✓ 点击了选项{optionNumber}");
        ShowPanel(optionNumber);
    }

    private void OnCreateRoomButtonClicked( )
    {
        Debug.Log("========== 创建房间流程开始 ==========");

        try
        {
            // ✓ 步骤1: 调用业务逻辑层创建房间
            Room newRoom = RoomDataManager.Instance.CreateAndSaveNewRoom();

            if (newRoom == null)
            {
                Debug.LogError("❌ 创建房间失败 - newRoom 为 null");
                return;
            }

            Debug.Log($"✓ 房间创建成功: {newRoom.roomId} - {newRoom.roomName}");

            // ✓ 步骤2: 记录房间ID
            LastCreatedRoomId = newRoom.roomId;

            // ✓ 步骤3: 初始化房间详情管理器
            InitializeRoomDetailPage();

            // ★ 【关键修改】步骤3.5: 显示房间信息面板 + 初始化 RoomDetailUIController
            ShowPanel(2);
            UpdateOption2Content(newRoom);

            // ★ 【新增】步骤3.6: 主动调用 InitializeWithRoom
            RoomDetailUIController roomDetailUI = FindFirstObjectByType<RoomDetailUIController>();
            if (roomDetailUI != null)
            {
                roomDetailUI.InitializeWithRoom(newRoom.roomId, isFromRoomList: false);
                Debug.Log($"✓ RoomDetailUIController 已初始化: {newRoom.roomId}");
            }

            Debug.Log("========== 创建房间流程完成 ==========");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ 创建房间失败: {e.Message}\n{e.StackTrace}");
        }
    }

    /// <summary>
    /// 初始化房间详情页面
    /// </summary>
    private void InitializeRoomDetailPage( )
    {
        try
        {
            if (RoomDetailManager.Instance == null)
            {
                Debug.LogError("❌ RoomDetailManager 单例未初始化");
                return;
            }

            string roomIdToLoad = LastCreatedRoomId;
            Debug.Log($"✓ 使用房间ID: '{roomIdToLoad}'");

            RoomDetailManager.Instance.EnterRoomDetail(roomIdToLoad);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ 初始化房间详情页面失败: {e.Message}\n{e.StackTrace}");
        }
    }

    /// <summary>
    /// 确认按钮点击 → 隐藏选项面板，显示地图选择界面
    /// </summary>
    private void OnClickConfirmToMap( )
    {
        Debug.Log("✓ 确认按钮点击，进入地图选择界面");

        HideAllPanels();

        if (mapSelectPanel != null)
        {
            mapSelectPanel.SetActive(true);
            mapSelectPanel.GetComponent<MapSelectPanel>()?.RefreshMapList();
        }
        else
        {
            Debug.LogError("❌ mapSelectPanel 未拖拽！");
        }
    }

    /// <summary>
    /// 显示指定的面板（支持7个选项）
    /// </summary>
    public void ShowPanel( int panelNumber )
    {
        Debug.Log($"→ ShowPanel({panelNumber})");
        HideAllPanels();

        switch (panelNumber)
        {
            case 1:
                if (option1Panel != null) option1Panel.SetActive(true);
                // ★ 关键：显示房间列表面板时，自动刷新列表数据
                if (RoomListManager.Instance != null)
                {
                    RoomListManager.Instance.RefreshRoomList();
                    Debug.Log("[RoomUIManager] 房间列表已刷新");
                }
                break;

            case 2: if (option2Panel != null) option2Panel.SetActive(true); break;
            case 3: if (option3Panel != null) option3Panel.SetActive(true); break;
            case 4: if (option4Panel != null) option4Panel.SetActive(true); break;
            case 5: if (option5Panel != null) option5Panel.SetActive(true); break;
            case 6: if (option6Panel != null) option6Panel.SetActive(true); break;
            case 7: if (option7Panel != null) option7Panel.SetActive(true); break;
        }
    }

    private void HideAllPanels( )
    {
        if (option1Panel != null) option1Panel.SetActive(false);
        if (option2Panel != null) option2Panel.SetActive(false);
        if (option3Panel != null) option3Panel.SetActive(false);
        if (option4Panel != null) option4Panel.SetActive(false);
        if (option5Panel != null) option5Panel.SetActive(false);
        if (option6Panel != null) option6Panel.SetActive(false);
        if (option7Panel != null) option7Panel.SetActive(false);
    }

    /// <summary>
    /// 【改进】集成版本2的 ForceMeshUpdate() 方法
    /// 确保 TextMesh 立即更新渲染，不会出现延迟显示
    /// </summary>
    private void UpdateOption2Content( Room room )
    {
        if (roomNameText != null)
        {
            roomNameText.text = room.roomName;
            roomNameText.ForceMeshUpdate();  // ← 版本2的改进：强制更新网格
            Debug.Log($"✓ 已更新房间名称: {room.roomName}");
        }

        // roomDescText 默认不显示房间描述，可通过 SetRoomDescription() 方法单独调用
    }

    // ========== 【公共接口】==========

    public void SetOption2Background( Sprite sprite )
    {
        if (option2Background != null && sprite != null)
        {
            option2Background.sprite = sprite;
            Debug.Log($"✓ 已更新选项2背景");
        }
    }

    public void SetRoomName( string roomName )
    {
        if (roomNameText != null)
        {
            roomNameText.text = roomName;
            roomNameText.ForceMeshUpdate();  // ← 加入 ForceMeshUpdate()
            Debug.Log($"✓ 已更新房间名称");
        }
    }

    public void SetRoomDescription( string description )
    {
        if (roomDescText != null)
        {
            roomDescText.text = description;
            roomDescText.ForceMeshUpdate();  // ← 加入 ForceMeshUpdate()
            Debug.Log($"✓ 已更新房间描述");
        }
    }

    public void BackToOption1( )
    {
        ShowPanel(1);
        Debug.Log("✓ 已返回到选项1");
    }

    /// <summary>
    /// 从地图选择界面返回时调用（重新显示选项面板）
    /// </summary>
    public void ShowOptionPanelAndOption2( )
    {
        if (optionPanel != null)
        {
            optionPanel.SetActive(true);
            Debug.Log("✓ optionPanel 已重新显示");
        }
        ShowPanel(2);
    }

    /// <summary>
    /// 显示房间编辑面板
    /// </summary>
    public void ShowRoomEditPanel( string roomId, bool isFromRoomList = true )
    {
        Debug.Log($"[RoomUIManager] 显示房间编辑面板: {roomId}");

        if (roomEditPanel == null)
        {
            Debug.LogError("❌ roomEditPanel 未配置");
            return;
        }

        roomEditPanel.SetActive(true);

        RoomEditPanel editPanel = roomEditPanel.GetComponent<RoomEditPanel>();
        if (editPanel != null)
        {
            editPanel.InitializeWithRoom(roomId, isFromRoomList);
        }
    }

    /// <summary>
    /// 隐藏房间编辑面板
    /// </summary>
    public void HideRoomEditPanel( )
    {
        Debug.Log("[RoomUIManager] 隐藏房间编辑面板");

        if (roomEditPanel != null)
        {
            roomEditPanel.SetActive(false);
        }
    }
}