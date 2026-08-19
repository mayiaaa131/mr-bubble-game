using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using UnityEngine.SceneManagement;


public class RoomDetailUIController : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI roomNameText;
    [SerializeField] private TextMeshProUGUI playerCountText;
    [SerializeField] private Button createPlayerButton;
    [SerializeField] private Button deletePlayerButton;
    [SerializeField] private Button cancelButton;

    [Header("玩家预制体")]
    [SerializeField] private GameObject redTeamPlayerPrefab;    // ★ 红队玩家预制体  
    [SerializeField] private GameObject blueTeamPlayerPrefab;   // ★ 蓝队玩家预制体  



    [Header("房间信息显示")]
    [SerializeField] private TextMeshProUGUI roomNameDisplay;

    [SerializeField] private Transform redTeamPanel;
    [SerializeField] private Transform blueTeamPanel;

    [Header("★ 其他信息按钮")]
    [SerializeField] private Button otherInfoButton;

    private string _currentRoomId;
    private bool _isFromRoomList = false;
    private bool isInitialized = false;
    private bool isRoomNameSet = false;

    // 添加 Awake 方法来初始化 isInitialized  
    private void Awake( )
    {
        Debug.Log("========== RoomDetailUIController.Awake() ==========");
        // 确保 isInitialized 在 Start() 之前被设置为 true  
        isInitialized = true;
        // 任何需要在 Start() 之前完成的初始化都可以在这里进行  
    }

    private void Start( )
    {
        Debug.Log("========== RoomDetailUIController.Start() ==========");
        // 因为 Awake() 已经设置了 isInitialized = true，这里就不需要再进行判断了  
        // 如果 ValidateRequiredReferences 依赖于其他 Awake() 后的初始化，它应该被放在 Awake() 中或者在 Start() 中确保其依赖已满足。  
        if (!ValidateRequiredReferences())
        {
            Debug.LogError("必需配置验证失败，无法继续");
            PrintConfigurationSummary();
            Debug.LogError("========== 请在 Inspector 中配置缺失的必需字段 ==========");
            return;
        }

        BindButtonListeners();

        // 此时 isInitialized 已经是 true，RefreshUI 不会提前返回  
        RefreshUI();

        Debug.Log("========== RoomDetailUIController 初始化完成 ==========");

        // 使用 Invoke 延迟调用，确保Canvas完全初始化  
        Invoke("UpdateRoomNameDisplay", 0.1f);

        
    }

    private void OnEnable()
    {
        //Debug.Log("[OnEnable] RoomDetailUIController 页面显示");

        // 只负责刷新UI和加载房间数据，不再绑定事件
        if (RoomDetailManager.Instance != null)
        {
            Room currentRoom = RoomDetailManager.Instance.GetCurrentRoom();
            if (currentRoom != null && _currentRoomId != currentRoom.roomId)
            {
                InitializeWithRoom(currentRoom.roomId, isFromRoomList: true);
            }
        }

        // ★ 移除这里的 BindButtonListeners() 调用，只在 Start 中调用一次
        RefreshUI();
        Invoke("UpdateRoomNameDisplay", 0.1f);
    }

    /// <summary>
    /// ★ 初始化玩家编辑面板（从房间列表打开时调用）
    /// 供 RoomUIManager 在导航时调用
    /// </summary>
    public void InitializeWithRoom( string roomId, bool isFromRoomList = true )
    {
        Debug.Log($"[RoomDetailUIController] 初始化玩家编辑面板: {roomId}");
        Debug.Log($"[RoomDetailUIController] 打开来源: {(isFromRoomList ? "房间列表" : "创建房间后")}");

        // ★ 【最关键】先设置新的房间ID
        _currentRoomId = roomId;
        _isFromRoomList = isFromRoomList;

        // ★ 【第1步】彻底清空旧玩家
        if (redTeamPanel != null)
        {
            while (redTeamPanel.childCount > 0)
            {
                DestroyImmediate(redTeamPanel.GetChild(0).gameObject);
            }
            Debug.Log($"✓ 红队Panel已清空");
        }

        if (blueTeamPanel != null)
        {
            while (blueTeamPanel.childCount > 0)
            {
                DestroyImmediate(blueTeamPanel.GetChild(0).gameObject);
            }
            Debug.Log($"✓ 蓝队Panel已清空");
        }

        // ★ 【第2步】清理 TeamAssignManager 中的数据
        if (TeamAssignManager.Instance != null)
        {
            TeamAssignManager.Instance.ClearAllTeams();
            Debug.Log($"✓ TeamAssignManager 已清空所有队伍数据");
        }

        // ★ 【第3步】强制Canvas立即更新
        Canvas.ForceUpdateCanvases();

        // ★ 【第4步】从 JSON 加载房间数据
        Room room = RoomDataManager.Instance.GetRoomById(roomId);

        if (room == null)
        {
            Debug.LogError($"❌ 无法找到房间: {roomId}");
            return;
        }

        // ★ 【第5步】初始化房间详情管理器
        RoomDetailManager.Instance.EnterRoomDetail(roomId);

        // ★ 【关键修改】直接调用 SyncPlayerListFromJSON，不要中间插入其他方法
        // 此时 _currentRoomId 已经是新值，所以 SyncPlayerListFromJSON 里用的就是对的 roomId
        SyncPlayerListFromJSON(room);

        Debug.Log($"✓ 玩家编辑面板初始化完成，房间玩家数: {room.currentPlayers}/{room.maxPlayers}");
    }

    /// <summary>
    /// ★ 从 JSON 同步房间数据到 UI
    /// </summary>
    private void SyncRoomDataFromJSON( Room room )
    {
        Debug.Log($"[RoomDetailUIController] 开始从 JSON 同步房间数据");

        if (room == null)
        {
            Debug.LogError("❌ 房间数据为空");
            return;
        }

        // 使用 SetRoomInfo 方法统一处理
        SetRoomInfo(room);

        Debug.Log($"✓ 房间数据已从 JSON 同步");
        SyncPlayerListFromJSON(room);
    }

    /// <summary>
    /// ★ 设置房间信息（统一处理显示逻辑）
    /// </summary>
    public void SetRoomInfo( Room room )
    {
        if (room == null)
        {
            Debug.LogError("❌ SetRoomInfo: 传入的 room 为 null");
            return;
        }

        Debug.Log($"========== SetRoomInfo ==========");
        Debug.Log($"  → 房间名: {room.roomName}");
        Debug.Log($"  → 玩家数: {room.currentPlayers}/{room.maxPlayers}");

        // 更新 roomNameDisplay
        if (roomNameDisplay != null)
        {
            roomNameDisplay.text = room.roomName;
            roomNameDisplay.ForceMeshUpdate();
            Debug.Log($"  ✅ roomNameDisplay 已更新: '{room.roomName}'");
        }
        else
        {
            Debug.LogWarning("⚠ roomNameDisplay 未挂载（可选）");
        }

        // 更新 roomNameText（如果也挂了的话）
        if (roomNameText != null)
        {
            roomNameText.text = room.roomName;
            roomNameText.ForceMeshUpdate();
            Debug.Log($"  ✅ roomNameText 已更新: '{room.roomName}'");
        }

        // 更新玩家数
        if (playerCountText != null)
        {
            playerCountText.text = $"{room.currentPlayers}/{room.maxPlayers}";
            Debug.Log($"  ✅ playerCountText 已更新: {room.currentPlayers}/{room.maxPlayers}");
        }

        Debug.Log($"========== SetRoomInfo 完成 ==========");
    }

    /// <summary>
    /// ★ 同步玩家列表
    /// 从 Team JSON 文件中读取现有玩家并显示到 UI 面板
    /// </summary>
    private void SyncPlayerListFromJSON( Room room )
    {
        Debug.Log($"[RoomDetailUIController] 开始同步玩家列表");
        Debug.Log($"当前房间玩家数: {room.currentPlayers}");

        try
        {
            string roomId = _currentRoomId ?? room.roomId;
            if (string.IsNullOrEmpty(roomId))
            {
                Debug.LogWarning("⚠ 无法获取房间ID");
                return;
            }

            // ★ 步骤1: 清空现有玩家按钮
            ClearTeamPanels();

            // ★ 步骤2: 加载该房间的 Team JSON 数据
            RoomTeamsData roomTeamsData = TeamJsonFileHandler.Instance.LoadTeamsData(roomId);

            if (roomTeamsData == null || roomTeamsData.teams == null || roomTeamsData.teams.Count == 0)
            {
                Debug.Log("⚠ 该房间暂无玩家数据（新房间）");
                return;
            }

            Debug.Log($"✓ 成功加载 Team JSON，队伍数: {roomTeamsData.teams.Count}");

            // ★ 步骤3: 根据房间的 teamRedId 和 teamBlueId 找到对应的队伍
            TeamInfo redTeam = roomTeamsData.teams.Find(t => t.teamId == room.teamRedId);
            TeamInfo blueTeam = roomTeamsData.teams.Find(t => t.teamId == room.teamBlueId);

            // ★ 显示红队玩家
            if (redTeam != null && redTeamPanel != null)
            {
                Debug.Log($"→ 正在加载红队玩家: {redTeam.players.Count} 人");
                foreach (TeamPlayer player in redTeam.players)
                {
                    CreatePlayerButton(player, redTeamPanel, isRedTeam: true);  // ★ 传入 true
                    Debug.Log($"  ✓ 红队玩家已添加: {player.playerName}");
                }
            }

            // ★ 显示蓝队玩家
            if (blueTeam != null && blueTeamPanel != null)
            {
                Debug.Log($"→ 正在加载蓝队玩家: {blueTeam.players.Count} 人");
                foreach (TeamPlayer player in blueTeam.players)
                {
                    CreatePlayerButton(player, blueTeamPanel, isRedTeam: false);  // ★ 传入 false
                    Debug.Log($"  ✓ 蓝队玩家已添加: {player.playerName}");
                }
            }


            int totalPlayers = (redTeam?.players.Count ?? 0) + (blueTeam?.players.Count ?? 0);
            Debug.Log($"✓ 玩家列表同步完成，共显示 {totalPlayers} 个玩家");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ 同步玩家列表失败: {e.Message}\n{e.StackTrace}");
        }
    }




    /// <summary>
    /// ★ 改进：使用预制体创建玩家按钮，并设置必要的拖拽配置
    /// </summary>
    private void CreatePlayerButton( TeamPlayer player, Transform targetPanel, bool isRedTeam )
    {
        if (targetPanel == null || player == null)
        {
            Debug.LogWarning("⚠ targetPanel 或 player 为空");
            return;
        }

        // 根据队伍选择对应的预制体
        GameObject prefabToUse = isRedTeam ? redTeamPlayerPrefab : blueTeamPlayerPrefab;

        if (prefabToUse == null)
        {
            Debug.LogError($"❌ {(isRedTeam ? "红队" : "蓝队")}玩家预制体未配置");
            return;
        }

        // 从预制体实例化
        GameObject playerButtonGo = Instantiate(prefabToUse, targetPanel);
        playerButtonGo.name = $"Player_{player.playerId}";

        // ★ 设置玩家数据
        DraggablePlayerButton draggableBtn = playerButtonGo.GetComponent<DraggablePlayerButton>();
        if (draggableBtn != null)
        {
            draggableBtn.playerId = player.playerId;
            draggableBtn.playerName = player.playerName;
            draggableBtn.currentTeam = isRedTeam ? "red" : "blue";  // ★ 新增：设置当前队伍

            // ★ 【关键】设置拖拽所需的预制体和容器引用
            if (redTeamPlayerPrefab != null)
                draggableBtn.redTeamPlayerPrefab = redTeamPlayerPrefab;
            if (blueTeamPlayerPrefab != null)
                draggableBtn.blueTeamPlayerPrefab = blueTeamPlayerPrefab;
            if (blueTeamPanel != null)
                draggableBtn.blueTeamContainer = blueTeamPanel;

            // ★ 获取 Canvas
            Canvas rootCanvas = GetComponentInParent<Canvas>();
            if (rootCanvas != null)
                draggableBtn.rootCanvas = rootCanvas;
        }

        // ★ 更新玩家名称文本
        TextMeshProUGUI playerNameText = playerButtonGo.GetComponentInChildren<TextMeshProUGUI>();
        if (playerNameText != null)
        {
            playerNameText.text = player.playerName;
            Debug.Log($"  ✓ {(isRedTeam ? "红队" : "蓝队")}玩家名称已设置: {player.playerName}");
        }

        Debug.Log($"  ✓ {(isRedTeam ? "红队" : "蓝队")}玩家预制体已实例化: {player.playerName}");
    }


    /// <summary>
    /// ★ 新增方法: 清空两个 Team Panel 中的所有玩家按钮
    /// </summary>
    /// <summary>
    /// ★ 改进：强制清空并立即销毁（不等下一帧）
    /// </summary>
    private void ClearTeamPanels( )
    {
        Debug.Log("[ClearTeamPanels] 开始清空玩家面板");

        if (redTeamPanel != null)
        {
            int redCount = redTeamPanel.childCount;
            for (int i = redTeamPanel.childCount - 1; i >= 0; i--)
            {
                Transform child = redTeamPanel.GetChild(i);
                Debug.Log($"  → 销毁红队玩家: {child.name}");
                Destroy(child.gameObject);
            }
            Debug.Log($"✓ 红队已清空（{redCount}个）");
        }

        if (blueTeamPanel != null)
        {
            int blueCount = blueTeamPanel.childCount;
            for (int i = blueTeamPanel.childCount - 1; i >= 0; i--)
            {
                Transform child = blueTeamPanel.GetChild(i);
                Debug.Log($"  → 销毁蓝队玩家: {child.name}");
                Destroy(child.gameObject);
            }
            Debug.Log($"✓ 蓝队已清空（{blueCount}个）");
        }

        Debug.Log("✓ 已清空玩家 Panel");
    }

    /// <summary>
    /// ★ 更新房间名字显示（多层防护版本）
    /// </summary>
    private void UpdateRoomNameDisplay( )
    {
        try
        {
            Debug.Log("========== UpdateRoomNameDisplay 执行 ==========");

            // 步骤1: 检查字段配置
            if (roomNameDisplay == null)
            {
                Debug.LogError("❌ roomNameDisplay 为 null，无法更新");
                return;
            }

            Debug.Log($"✓ roomNameDisplay 已配置: {roomNameDisplay.gameObject.name}");

            // 步骤2: 检查 Canvas 状态
            Canvas canvas = roomNameDisplay.GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                Debug.LogError("❌ 找不到 Canvas，TextMeshProUGUI 无法正常工作");
                return;
            }

            if (!canvas.enabled)
            {
                Debug.LogWarning("⚠ Canvas 未激活，尝试激活");
                canvas.enabled = true;
            }

            Debug.Log($"✓ Canvas 已确认激活: {canvas.gameObject.name}");

            // 步骤3: 检查 TextMeshProUGUI 本身
            if (!roomNameDisplay.enabled)
            {
                Debug.LogWarning("⚠ TextMeshProUGUI 组件未启用，尝试启用");
                roomNameDisplay.enabled = true;
            }

            if (!roomNameDisplay.gameObject.activeSelf)
            {
                Debug.LogWarning("⚠ TextMeshProUGUI GameObject 未激活，尝试激活");
                roomNameDisplay.gameObject.SetActive(true);
            }

            Debug.Log($"✓ TextMeshProUGUI 已确认启用");

            // 步骤4: 获取当前房间
            if (RoomDetailManager.Instance == null)
            {
                Debug.LogError("❌ RoomDetailManager 单例未初始化");
                return;
            }

            Room currentRoomObj = RoomDetailManager.Instance.GetCurrentRoom();
            if (currentRoomObj == null)
            {
                Debug.LogError("❌ 当前房间对象为 null");
                return;
            }

            string currentRoomId = currentRoomObj.roomId;
            Debug.Log($"[步骤4] 获取房间ID: '{currentRoomId}'");

            if (string.IsNullOrEmpty(currentRoomId))
            {
                Debug.LogError("❌ 当前房间ID为空");
                roomNameDisplay.text = "未选择房间";
                roomNameDisplay.SetAllDirty();
                return;
            }

            // 步骤5: 从 RoomDataManager 获取房间数据
            Room currentRoom = RoomDataManager.Instance.GetRoomById(currentRoomId);
            Debug.Log($"[步骤5] 获取房间对象: {(currentRoom != null ? "成功" : "失败")}");

            if (currentRoom == null)
            {
                Debug.LogError($"❌ 找不到房间数据: {currentRoomId}");
                roomNameDisplay.text = "房间信息加载失败";
                roomNameDisplay.SetAllDirty();
                return;
            }

            // 步骤6: 打印房间信息
            Debug.Log($"✓ 房间数据获取成功:");
            Debug.Log($"  - 房间ID: {currentRoom.roomId}");
            Debug.Log($"  - 房间名称: {currentRoom.roomName}");
            Debug.Log($"  - 玩家数: {currentRoom.currentPlayers}/{currentRoom.maxPlayers}");

            // 步骤7: 设置文本（关键！）
            string roomName = currentRoom.roomName;
            Debug.Log($"[步骤7] 准备设置文本: '{roomName}'");

            roomNameDisplay.text = roomName;
            Debug.Log($"✓ 文本已设置: '{roomNameDisplay.text}'");

            // 步骤8: 强制刷新 UI
            roomNameDisplay.SetAllDirty();
            Debug.Log($"✓ SetAllDirty() 已调用");

            // 步骤9: 强制重新布局
            LayoutRebuilder.ForceRebuildLayoutImmediate(roomNameDisplay.GetComponent<RectTransform>());
            Debug.Log($"✓ LayoutRebuilder.ForceRebuildLayoutImmediate() 已调用");

            Debug.Log("========== UpdateRoomNameDisplay 完成 ==========");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ 更新房间名字失败: {e.Message}\n{e.StackTrace}");
            if (roomNameDisplay != null)
            {
                roomNameDisplay.text = "加载出错";
            }
        }
    }

    /// <summary>
    /// ★ 打印配置检查结果
    /// </summary>
    private void PrintConfigurationSummary( )
    {
        Debug.LogError($"  ❌ 必需字段（3个）:");
        Debug.LogError($"    createPlayerButton: {(createPlayerButton != null ? "✅ 已配置" : "❌ 未配置")}");
        Debug.LogError($"    redTeamPanel: {(redTeamPanel != null ? "✅ 已配置" : "❌ 未配置")}");
        Debug.LogError($"  ⚠ 可选字段（7个）:");
        Debug.LogError($"    roomNameText: {(roomNameText != null ? "✅ 已配置" : "⚠ 未配置(可选)")}");
        Debug.LogError($"    playerCountText: {(playerCountText != null ? "✅ 已配置" : "⚠ 未配置(可选)")}");
        Debug.LogError($"    deletePlayerButton: {(deletePlayerButton != null ? "✅ 已配置" : "⚠ 未配置(可选)")}");
        Debug.LogError($"    cancelButton: {(cancelButton != null ? "✅ 已配置" : "⚠ 未配置(可选)")}");
        Debug.LogError($"    blueTeamPanel: {(blueTeamPanel != null ? "✅ 已配置" : "⚠ 未配置(可选)")}");
        Debug.LogError($"    roomNameDisplay: {(roomNameDisplay != null ? "✅ 已配置" : "⚠ 未配置(可选)")}");
        Debug.LogError($"    otherInfoButton: {(otherInfoButton != null ? "✅ 已配置" : "⚠ 未配置(可选)")}");
    }

    private bool ValidateRequiredReferences( )
    {
        Debug.Log("========== 验证必需 UI 引用 ==========");

        bool isValid = true;
        int validCount = 0;
        int totalRequired = 3;

        if (createPlayerButton == null)
        {
            Debug.LogError("❌ [1/3] Create Player Button 未配置（必需）");
            isValid = false;
        }
        else
        {
            Debug.Log("✅ [1/3] Create Player Button 已配置");
            validCount++;
        }


        if (redTeamPanel == null)
        {
            Debug.LogError("❌ [3/3] Red Team Panel 未配置（必需）");
            isValid = false;
        }
        else
        {
            Debug.Log("✅ [3/3] Red Team Panel 已配置");
            validCount++;
        }

        Debug.Log("========== 可选字段状态 ==========");
        if (roomNameText == null)
            Debug.LogWarning("⚠ Room Name Text 未配置（可选）");
        else
            Debug.Log("✅ Room Name Text 已配置");

        if (playerCountText == null)
            Debug.LogWarning("⚠ Player Count Text 未配置（可选）");
        else
            Debug.Log("✅ Player Count Text 已配置");

        if (deletePlayerButton == null)
            Debug.LogWarning("⚠ Delete Player Button 未配置（可选）");
        else
            Debug.Log("✅ Delete Player Button 已配置");

        if (cancelButton == null)
            Debug.LogWarning("⚠ Cancel Button 未配置（可选）");
        else
            Debug.Log("✅ Cancel Button 已配置");

        if (blueTeamPanel == null)
            Debug.LogWarning("⚠ Blue Team Panel 未配置（可选）");
        else
            Debug.Log("✅ Blue Team Panel 已配置");

        if (roomNameDisplay == null)
            Debug.LogWarning("⚠ Room Name Display 未配置（可选）");
        else
            Debug.Log("✅ Room Name Display 已配置");

        if (otherInfoButton == null)
            Debug.LogWarning("⚠ Other Info Button 未配置（可选）");
        else
            Debug.Log("✅ Other Info Button 已配置");

        Debug.Log($"========== 必需字段验证完成：{validCount}/{totalRequired} ==========");

        if (!isValid)
        {
            Debug.LogError($"❌ 有 {totalRequired - validCount} 个必需字段未配置！");
        }

        return isValid;
    }

    private void BindButtonListeners()
    {
        Debug.Log("========== 绑定按钮事件 ==========");

        try
        {
            if (createPlayerButton != null)
            {
                // ★ 关键修复：先移除所有旧监听器，再添加新的
                createPlayerButton.onClick.RemoveAllListeners();
                createPlayerButton.onClick.AddListener(OnCreatePlayerButtonClicked);
                Debug.Log("✅ Create Player Button 已绑定");
            }

            if (deletePlayerButton != null)
            {
                deletePlayerButton.onClick.RemoveAllListeners();
                deletePlayerButton.onClick.AddListener(OnDeletePlayerButtonClicked);
                Debug.Log("✅ Delete Player Button 已绑定");
            }

            if (cancelButton != null)
            {
                cancelButton.onClick.RemoveAllListeners();
                cancelButton.onClick.AddListener(OnCancelButtonClicked);
                Debug.Log("✅ Cancel Button 已绑定");
            }

            if (otherInfoButton != null)
            {
                otherInfoButton.onClick.RemoveAllListeners();
                otherInfoButton.onClick.AddListener(OnOtherInfoButtonClicked);
                Debug.Log("✅ Other Info Button 已绑定");
            }

            Debug.Log("========== 按钮事件绑定完成 ==========");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ 绑定按钮事件失败: {e.Message}");
        }
    }

    private void OnCreatePlayerButtonClicked()
    {
        Debug.Log(">>> 点击了'创建玩家'按钮");
        if (!isInitialized) return;

        try
        {
            // ★ 步骤1: 创建玩家（包括添加到 Team JSON）
            RoomDetailManager.Instance.CreatePlayer();

            // ★ 步骤2: 只刷新 UI 显示，不要重新加载玩家列表
            RefreshDisplayFromManager();

            // ★ 删除这段：
            // Room currentRoom = RoomDetailManager.Instance.GetCurrentRoom();
            // if (currentRoom != null)
            // {
            //     SyncPlayerListFromJSON(currentRoom);  // 不要调用这个
            // }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ 创建玩家失败: {e.Message}");
        }
    }

    private void OnDeletePlayerButtonClicked( )
    {
        Debug.Log("========== 删除玩家操作开始 ==========");
        if (!isInitialized) return;

        try
        {
            Transform playerToDelete = null;
            string deletedPlayerId = "";

            if (blueTeamPanel != null && blueTeamPanel.childCount > 0)
                playerToDelete = blueTeamPanel.GetChild(blueTeamPanel.childCount - 1);
            else if (redTeamPanel != null && redTeamPanel.childCount > 0)
                playerToDelete = redTeamPanel.GetChild(redTeamPanel.childCount - 1);
            else
            {
                Debug.LogWarning("⚠ 没有玩家可删除");
                return;
            }

            DraggablePlayerButton playerBtn = playerToDelete.GetComponent<DraggablePlayerButton>();
            if (playerBtn != null)
            {
                deletedPlayerId = playerBtn.playerId;
                TeamAssignManager.Instance.RemovePlayerFromAllTeams(deletedPlayerId);
            }

            playerToDelete.SetParent(null);
            Destroy(playerToDelete.gameObject);
            RoomDetailManager.Instance.DeletePlayer();
            RemovePlayerDirectlyFromTeamJson(deletedPlayerId);

            // ✅ 删除后从 Manager 刷新
            RefreshDisplayFromManager();

            Debug.Log("========== 删除玩家操作完成 ==========");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ 删除玩家失败: {e.Message}");
        }
    }

    private void RemovePlayerDirectlyFromTeamJson( string playerId )
    {
        try
        {
            string currentRoomId = RoomDetailManager.Instance?.GetCurrentRoomId();
            if (string.IsNullOrEmpty(currentRoomId)) return;

            RoomTeamsData roomTeamsData = TeamJsonFileHandler.Instance.LoadTeamsData(currentRoomId);
            if (roomTeamsData == null) return;

            bool removed = false;
            foreach (TeamInfo team in roomTeamsData.teams)
            {
                int beforeCount = team.players.Count;
                team.players.RemoveAll(p => p.playerId == playerId);
                if (team.players.Count < beforeCount)
                {
                    team.alivePlayerCount = team.players.Count;
                    removed = true;
                }
            }

            if (removed)
                TeamJsonFileHandler.Instance.SaveTeamsData(currentRoomId, roomTeamsData);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ 删除玩家JSON失败: {e.Message}");
        }
    }

    private void OnConfirmButtonClicked( )
    {
        Debug.Log(">>> 点击了'确认'按钮");
        if (!isInitialized) return;

        try
        {
            if (redTeamPanel == null) return;

            int redTeamCount = redTeamPanel.childCount;
            int blueTeamCount = blueTeamPanel != null ? blueTeamPanel.childCount : 0;
            int totalPlayers = redTeamCount + blueTeamCount;

            Room currentRoom = RoomDetailManager.Instance.GetCurrentRoom();
            if (currentRoom == null) return;

            if (totalPlayers > 0)
            {
                currentRoom.currentPlayers = totalPlayers;
                RoomDetailManager.Instance.UpdateRoomPlayerCountByObject(currentRoom);
                RoomDetailManager.Instance.SyncPlayersToTeamJson(redTeamPanel, blueTeamPanel);
                RefreshDisplayFromManager();
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ 确认失败: {e.Message}");
        }
    }

    private void OnCancelButtonClicked( )
    {
        Debug.Log(">>> 点击了'取消'按钮");
        Debug.Log($"   返回位置: {(_isFromRoomList ? "房间列表" : "创建房间界面")}");

        if (!isInitialized)
        {
            Debug.LogError("❌ RoomDetailUIController 未初始化");
            return;
        }

        try
        {
            RoomUIManager uiManager = FindFirstObjectByType<RoomUIManager>();
            if (uiManager != null)
            {
                if (_isFromRoomList)
                {
                    uiManager.ShowPanel(1);
                    if (RoomListManager.Instance != null)
                        RoomListManager.Instance.RefreshRoomList();
                    Debug.Log("✓ 已返回房间列表");
                }
                else
                {
                    uiManager.ShowPanel(1);
                    Debug.Log("✓ 已返回初始界面");
                    RoomUIManager.LastCreatedRoomId = null;
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ 取消按钮操作失败: {e.Message}\n{e.StackTrace}");
        }
    }

    private void OnOtherInfoButtonClicked( )
    {
        Debug.Log(">>> 点击了'其它信息'按钮");

        if (!isInitialized)
        {
            Debug.LogError("❌ RoomDetailUIController 未初始化");
            return;
        }

        try
        {
            string currentRoomId = RoomDetailManager.Instance.GetCurrentRoom()?.roomId;

            if (string.IsNullOrEmpty(currentRoomId))
            {
                Debug.LogError("❌ 无法获取当前房间ID");
                return;
            }

            Debug.Log($"→ 跳转到房间编辑界面，房间ID: {currentRoomId}");

            RoomUIManager uiManager = FindFirstObjectByType<RoomUIManager>();
            if (uiManager != null)
            {
                uiManager.ShowRoomEditPanel(currentRoomId, false);
                Debug.Log("✓ 房间编辑界面已显示");
            }
            else
            {
                Debug.LogError("❌ RoomUIManager 未找到");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ 其它信息按钮操作失败: {e.Message}");
        }
    }

    private void RefreshDisplayFromManager( )
    {
        Room currentRoom = RoomDetailManager.Instance.GetCurrentRoom();
        if (currentRoom == null) return;
        SetRoomInfo(currentRoom);
    }

    private void RefreshUI( )
    {
        if (!isInitialized)
        {
            Debug.LogWarning("⚠ RoomDetailUIController 未初始化，无法刷新UI");
            return;
        }

        try
        {
            Room currentRoom = RoomDetailManager.Instance.GetCurrentRoom();

            if (currentRoom == null)
            {
                Debug.LogWarning("⚠ 当前房间为空");
                return;
            }

            if (roomNameText != null)
            {
                if (string.IsNullOrEmpty(currentRoom.roomName))
                {
                    currentRoom.roomName = "新手房间";
                }
                roomNameText.text = currentRoom.roomName;
                Debug.Log($"✓ 房间名称已更新: {currentRoom.roomName}");
            }

            if (playerCountText != null)
            {
                int currentPlayers = RoomDetailManager.Instance.GetCurrentPlayerCount();
                int maxPlayers = RoomDetailManager.Instance.GetMaxPlayerCount();
                playerCountText.text = $"{currentPlayers}/{maxPlayers}";
                Debug.Log($"✓ 玩家数已更新: {currentPlayers}/{maxPlayers}");
            }

            Debug.Log($"✓ UI 已刷新 - 房间: {currentRoom.roomName}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ 刷新UI失败: {e.Message}");
        }
    }

    private void OnDestroy( )
    {
        Debug.Log("✓ RoomDetailUIController 已销毁");

        if (createPlayerButton != null)
            createPlayerButton.onClick.RemoveListener(OnCreatePlayerButtonClicked);

        if (deletePlayerButton != null)
            deletePlayerButton.onClick.RemoveListener(OnDeletePlayerButtonClicked);

       

        if (cancelButton != null)
            cancelButton.onClick.RemoveListener(OnCancelButtonClicked);

        if (otherInfoButton != null)
            otherInfoButton.onClick.RemoveListener(OnOtherInfoButtonClicked);
    }
}