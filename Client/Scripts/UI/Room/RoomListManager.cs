using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 房间列表管理器
/// 负责房间列表的加载、显示和交互
/// ? 单例模式：整个项目中只有一个实例
/// ? 关键：从 Rooms.json 加载房间ID列表，然后从 Room.json 获取详情
/// ? 改进：使用 RoomListItemButton 脚本管理每个按钮
/// </summary>
public class RoomListManager : MonoBehaviour
{
    private static RoomListManager instance;

    [Header("房间列表容器")]
    [SerializeField] private Transform roomListContent;  // ScrollView 的 Content

    [Header("房间列表项 Prefab")]
    [SerializeField] private GameObject roomListItemPrefab;


    private List<RoomListItemButton> roomButtons = new List<RoomListItemButton>();
    private GameObject _lastSelectedButton = null;

    private string _roomIdToDelete = "";
    private string _roomNameToDelete = "";
    private GameObject _buttonGoToDelete = null;

    private void Awake( )
    {
        // ★ 单例模式
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start( )
    {
        Debug.Log("========== RoomListManager 启动 ==========");

        if (!ValidateReferences())
        {
            Debug.LogError("? 必需配置验证失败");
            return;
        }

        RefreshRoomList();
        Debug.Log("========== RoomListManager 初始化完成 ==========");
    }

    /// <summary>
    /// 验证必需的UI引用
    /// </summary>
    private bool ValidateReferences( )
    {
        Debug.Log("========== 验证 RoomListManager 引用 ==========");

        bool isValid = true;

        if (roomListContent == null)
        {
            Debug.LogError("? roomListContent 未配置");
            isValid = false;
        }
        else
        {
            Debug.Log("? roomListContent 已配置");
        }

        if (roomListItemPrefab == null)
        {
            Debug.LogError("? roomListItemPrefab 未配置");
            isValid = false;
        }
        else
        {
            Debug.Log("? roomListItemPrefab 已配置");
        }



        return isValid;
    }

    /// <summary>
    /// 刷新房间列表
    /// ★ 关键：从 Rooms.json 加载房间ID列表，然后为每个房间创建按钮
    /// </summary>
    public void RefreshRoomList( )
    {
        Debug.Log("[RoomListManager] 开始刷新房间列表");

        // ? 步骤1: 清空旧按钮
        foreach (var btn in roomButtons)
        {
            Destroy(btn.gameObject);
        }
        roomButtons.Clear();

        if (roomListContent == null)
        {
            Debug.LogError("? roomListContent 未配置，无法刷新");
            return;
        }

        if (roomListItemPrefab == null)
        {
            Debug.LogError("? roomListItemPrefab 未配置，无法创建按钮");
            return;
        }

        // ? 步骤2: 加载房间ID列表 (Rooms.json)
        Debug.Log("→ 调用 JsonFileHandler.LoadRoomsList()");
        RoomsList roomsList = JsonFileHandler.Instance.LoadRoomsList();

        if (roomsList == null)
        {
            Debug.LogError("? RoomsList 对象为 null");
            return;
        }

        if (roomsList.rooms == null || roomsList.rooms.Count == 0)
        {
            Debug.LogWarning("? 房间列表为空，没有房间可显示");
            return;
        }

        //Debug.Log($"? 成功加载房间ID列表，共 {roomsList.rooms.Count} 个房间");

        // ? 步骤3: 为每个房间ID创建按钮
        foreach (var roomInfo in roomsList.rooms)
        {
            // 从 Room.json 获取房间详情
            Room room = RoomDataManager.Instance.GetRoomById(roomInfo.roomId);

            if (room != null)
            {
                CreateRoomButton(room.roomId, room.roomName);
            }
            else
            {
                Debug.LogWarning($"? 无法找到房间详情: {roomInfo.roomId}（ID在Rooms.json中但Room.json中不存在）");
            }
        }

        Debug.Log($"? 房间列表刷新完成，共 {roomButtons.Count} 个按钮");
    }

    /// <summary>
    /// 创建单个房间按钮
    /// ★ 对标 MapSelectPanel.CreateMapButton 的做法
    /// ? 关键：每个按钮有独立的名字和 RoomListItemButton 脚本
    /// </summary>
    private void CreateRoomButton( string roomId, string roomName )
    {
        Debug.Log($"[RoomListManager] 创建房间按钮: {roomName} ({roomId})");

        GameObject go = Instantiate(roomListItemPrefab, roomListContent);
        go.name = $"RoomBtn_{roomId}";

        RoomListItemButton itemButton = go.GetComponent<RoomListItemButton>();
        if (itemButton == null)
        {
            itemButton = go.AddComponent<RoomListItemButton>();
        }

        itemButton.Initialize(roomId, roomName, this);

        Button btn = go.GetComponent<Button>();
        if (btn != null)
        {
            string capturedRoomId = roomId;
            string capturedRoomName = roomName;
            GameObject capturedGo = go;

            btn.onClick.AddListener(( ) => OnSelectRoom(capturedRoomId, capturedRoomName, capturedGo));

            // ★ 删除按钮绑定代码已完全移除

            roomButtons.Add(itemButton);
            Debug.Log($"? 房间按钮已创建并绑定: {roomName} ({roomId})");
        }
        else
        {
            Debug.LogError($"? 房间按钮 Prefab 上没有 Button 组件");
            Destroy(go);
        }
    }


    /// <summary>
    /// 房间按钮被选择
    /// ★ 修改：进入玩家编辑界面而不是房间编辑面板
    /// </summary>
    private void OnSelectRoom( string roomId, string roomName, GameObject buttonGo )
    {
        Debug.Log($"[RoomListManager] 选中房间: {roomName} ({roomId})");

        // ★ 高亮当前按钮
        if (_lastSelectedButton != null && _lastSelectedButton != buttonGo)
        {
            var oldImage = _lastSelectedButton.GetComponent<Image>();
            if (oldImage != null)
            {
                oldImage.color = Color.white;
            }
        }

        var image = buttonGo.GetComponent<Image>();
        if (image != null)
        {
            image.color = new Color(0.5f, 0.85f, 1f);  // 蓝色高亮
        }

        _lastSelectedButton = buttonGo;

        // ★ 修改：进入玩家编辑界面而不是房间编辑模式
        EnterPlayerEditMode(roomId, roomName);
    }

    /// <summary>
    /// 进入玩家编辑界面
    /// ★ 新方法：与创建房间后的流程一致
    /// </summary>
    private void EnterPlayerEditMode( string roomId, string roomName )
    {
        Debug.Log($"[RoomListManager] 进入玩家编辑界面: {roomName} ({roomId})");

        // ★ [新增]步骤0: 为房间创建 RoomInstance（如果不存在）  
        if (ServerRoomManager.Instance?.GetRoom(roomId) == null)
        {
            Debug.Log($"→ 房间 {roomId} 不存在于运行时，创建 RoomInstance");

            Room roomData = RoomDataManager.Instance.GetRoomById(roomId);
            if (roomData != null)
            {
                GameObject go = new GameObject($"Room_{roomId}");
                go.transform.SetParent(ServerRoomManager.Instance.transform);

                RoomInstance room = go.AddComponent<RoomInstance>();
                room.Initialize(roomId);

                // 添加到 ServerRoomManager  
                ServerRoomManager.Instance.AddRoomToDict(roomId, room);
                ServerRoomManager.Instance.SetCurrentRoom(roomId);

                Debug.Log($"✅ RoomInstance 已创建并注册: {roomId}");
            }
        }
        else
        {
            // 设置为当前房间  
            ServerRoomManager.Instance.SetCurrentRoom(roomId);
            Debug.Log($"✓ 房间已在运行时，设置为当前房间");
        }


        // ? 步骤1: 初始化房间详情管理器
        if (RoomDetailManager.Instance != null)
        {
            RoomDetailManager.Instance.EnterRoomDetail(roomId);
            Debug.Log($"? RoomDetailManager 已初始化房间: {roomId}");
        }
        else
        {
            Debug.LogError("? RoomDetailManager 单例未初始化");
            return;
        }

        // ? 步骤2: 显示玩家编辑界面（不显示房间编辑面板）
        RoomUIManager uiManager = FindFirstObjectByType<RoomUIManager>();
        if (uiManager != null)
        {
            // ★ 直接显示玩家编辑界面，传入 isFromRoomList=true
            uiManager.ShowPanel(2);
            Debug.Log($"? 玩家编辑界面已显示");
        }
        else
        {
            Debug.LogError("? RoomUIManager 未找到");
        }

        // ★ 【新增】步骤3: 初始化房间详情 UI（这才是关键！）
        RoomDetailUIController detailUI = FindFirstObjectByType<RoomDetailUIController>();
        if (detailUI != null)
        {
            detailUI.InitializeWithRoom(roomId, isFromRoomList: true);
            Debug.Log($"✓ RoomDetailUIController 已初始化，玩家数据已加载");
        }
        else
        {
            Debug.LogError("❌ RoomDetailUIController 未找到");
        }
    }

    // ★ 删除原来的 EnterRoomEditMode 方法





    private System.Collections.IEnumerator HideMessageAfterDelay( GameObject panel, float delay )
    {
        yield return new WaitForSeconds(delay);
        if (panel != null)
            panel.SetActive(false);
    }

    /// <summary>
    /// 获取单例实例
    /// </summary>
    public static RoomListManager Instance
    {
        get
        {
            if (instance == null)
            {
                Debug.LogError("? RoomListManager 单例未初始化");
            }
            return instance;
        }
    }

    private void OnDestroy( )
    {
        Debug.Log("? RoomListManager 已销毁");
    }
}
