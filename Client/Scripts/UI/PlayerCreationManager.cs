using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PlayerCreationManager : MonoBehaviour
{

    private static int playerCounter = 0;  // ✓ 新增：全局计数器  

    [Header("预制体配置")]
    [SerializeField] private GameObject redTeamPlayerPrefab;
    [SerializeField] private GameObject blueTeamPlayerPrefab;

    [Header("队伍容器")]
    [SerializeField] private Transform redTeamContainer;
    [SerializeField] private Transform blueTeamContainer;

    [Header("创建按钮")]
    [SerializeField] private Button createPlayerButton;

    [Header("Canvas")]
    [SerializeField] private Canvas rootCanvas;

    private void Start()
    {
        PrintDebugInfo();
        BindButtonEvents();
    }

    private void PrintDebugInfo()
    {
        Debug.Log("========== PlayerCreationManager 初始化 ==========");
        Debug.Log($"redTeamPlayerPrefab: {(redTeamPlayerPrefab != null ? redTeamPlayerPrefab.name : "❌ NULL")}");
        Debug.Log($"blueTeamPlayerPrefab: {(blueTeamPlayerPrefab != null ? blueTeamPlayerPrefab.name : "❌ NULL")}");
        Debug.Log($"redTeamContainer: {(redTeamContainer != null ? redTeamContainer.name : "❌ NULL")}");
        Debug.Log($"blueTeamContainer: {(blueTeamContainer != null ? blueTeamContainer.name : "❌ NULL")}");
        Debug.Log($"createPlayerButton: {(createPlayerButton != null ? createPlayerButton.name : "❌ NULL")}");
        Debug.Log($"rootCanvas: {(rootCanvas != null ? rootCanvas.name : "❌ NULL")}");
        Debug.Log("=====================================================");
    }

    private void BindButtonEvents()
    {
        if (createPlayerButton != null)
        {
            createPlayerButton.onClick.AddListener(() => CreatePlayerWithAutoAssign());
            Debug.Log("✓ 创建玩家按钮已绑定");
        }
        else
        {
            Debug.LogError("❌ 无法绑定按钮，createPlayerButton为null");
        }
    }

    /// <summary>
    /// 动态统计Panel中的实际玩家数
    /// </summary>
    private int GetActualRedTeamCount()
    {
        return redTeamContainer != null ? redTeamContainer.childCount : 0;
    }

    private int GetActualBlueTeamCount()
    {
        return blueTeamContainer != null ? blueTeamContainer.childCount : 0;
    }

    private int GetActualTotalCount()
    {
        return GetActualRedTeamCount() + GetActualBlueTeamCount();
    }

    /// <summary>
    /// 创建玩家（自动分配队伍，使用实际计数）
    /// ✓ 新增：创建后实时同步到 Team JSON
    /// </summary>
    private void CreatePlayerWithAutoAssign()
    {
        Debug.Log("\n┌─────────────────────────────────────┐");
        Debug.Log("│ [CreatePlayer] 创建流程开始");
        Debug.Log("└─────────────────────────────────────┘");

        // ✓ 【关键改进】先更新实际计数
        int actualRedCount = GetActualRedTeamCount();
        int actualBlueCount = GetActualBlueTeamCount();
        int actualTotalCount = GetActualTotalCount();

        Debug.Log($"[实时统计] 红队: {actualRedCount}, 蓝队: {actualBlueCount}, 总计: {actualTotalCount}");

        // 1. 通过实际计数判断应该分配到哪个队
        string targetTeam = "";
        if (actualRedCount < TeamAssignManager.Instance.maxPerTeam)
        {
            targetTeam = "red";
            Debug.Log($"[决策] 红队未满({actualRedCount}/{TeamAssignManager.Instance.maxPerTeam}) → 分配到红队");
        }
        else if (actualBlueCount < TeamAssignManager.Instance.maxPerTeam)
        {
            targetTeam = "blue";
            Debug.Log($"[决策] 红队已满，蓝队未满({actualBlueCount}/{TeamAssignManager.Instance.maxPerTeam}) → 分配到蓝队");
        }
        else
        {
            Debug.LogWarning($"⚠️ 两队已满，弹出提示窗口 (红:{actualRedCount}, 蓝:{actualBlueCount})");

            // ★ 唯一新增的一行：调用弹窗  
            if (PlayerLimitPopup.Instance != null)
                PlayerLimitPopup.Instance.Show();
            else
                Debug.LogError("❌ PlayerLimitPopup 未初始化，请检查场景中是否挂载该脚本");

            return;
        }

        // ✓ [改进]生成有意义的玩家ID  
        playerCounter++;
        string playerId = $"player_{playerCounter}";  // 格式：player_1, player_2...  

        // ✓ 根据实际队伍人数生成正确的名字  
        string playerName = "";
        if (targetTeam == "red")
        {
            int newRedIndex = actualRedCount + 1;
            playerName = $"红队玩家{newRedIndex}";
            Debug.Log($"  → 生成红队玩家: {playerName}");
        }
        else
        {
            int newBlueIndex = actualBlueCount + 1;
            playerName = $"蓝队玩家{newBlueIndex}";
            Debug.Log($"  → 生成蓝队玩家: {playerName}");
        }

        Debug.Log($"[生成玩家] {playerName} (ID={playerId})");


        // 3. 先在 Manager 中记录玩家数据
        bool success = TeamAssignManager.Instance.AssignPlayerToTeam(playerId, playerName, targetTeam);

        if (!success)
        {
            Debug.LogError($"❌ AssignPlayerToTeam 失败");
            return;
        }

        // 4. 根据队伍 ID 选择对应预制体
        GameObject prefabToUse = (targetTeam == "red") ? redTeamPlayerPrefab : blueTeamPlayerPrefab;
        Transform containerToUse = (targetTeam == "red") ? redTeamContainer : blueTeamContainer;

        if (prefabToUse == null || containerToUse == null)
        {
            Debug.LogError($"❌ 预制体或容器未设置 (team={targetTeam})");
            return;
        }

        // 5. 实例化玩家按钮
        GameObject newButton = Instantiate(prefabToUse, containerToUse);
        newButton.name = $"{playerName}_{targetTeam}";

        // 6. 初始化按钮脚本
        DraggablePlayerButton draggable = newButton.GetComponent<DraggablePlayerButton>();
        if (draggable != null)
        {
            draggable.playerId = playerId;
            draggable.playerName = playerName;
            draggable.currentTeam = targetTeam;
            draggable.redTeamPlayerPrefab = redTeamPlayerPrefab;
            draggable.blueTeamPlayerPrefab = blueTeamPlayerPrefab;
            draggable.blueTeamContainer = blueTeamContainer;
            draggable.rootCanvas = rootCanvas;

            draggable.enabled = true;

            var cg = newButton.GetComponent<CanvasGroup>();
            if (cg != null)
            {
                cg.alpha = 1f;
                cg.blocksRaycasts = true;
            }

            Debug.Log($"✓ 玩家按钮已初始化");
        }
        else
        {
            Debug.LogError($"❌ 新按钮缺少 DraggablePlayerButton 脚本");
            return;
        }

        // 7. 更新按钮文本
        TextMeshProUGUI buttonText = newButton.GetComponentInChildren<TextMeshProUGUI>();
        if (buttonText != null)
        {
            buttonText.text = playerName;
            Debug.Log($"  → 按钮文本已更新: {playerName}");
        }

        // ✓ 打印最终统计（使用实际计数）
        int finalRedCount = GetActualRedTeamCount();
        int finalBlueCount = GetActualBlueTeamCount();
        int finalTotalCount = GetActualTotalCount();

        Debug.Log($"┌─────────────────────────────────────┐");
        Debug.Log($"│ [CreatePlayer] 创建完成: {playerName}");
        Debug.Log($"│ 红队玩家数: {finalRedCount}");
        Debug.Log($"│ 蓝队玩家数: {finalBlueCount}");
        Debug.Log($"│ 当前玩家总数: {finalTotalCount}");
        Debug.Log($"└─────────────────────────────────────┘\n");

        // ✓ [新增]第一步：创建玩家后立即同步到 Team JSON  
        Debug.Log("→ 创建玩家后同步到 Team JSON");
        if (RoomDetailManager.Instance != null)
        {
            RoomDetailManager.Instance.SyncPlayersToTeamJson(redTeamContainer, blueTeamContainer);
        }
    }

    /// <summary>
    /// ✓ 新增方法：实时同步 Team JSON（在每次创建/删除玩家时调用）
    /// 根据当前 UI 中的实际玩家数更新 Team JSON
    /// </summary>
    private void SyncTeamJsonRealTime()
    {
        try
        {
            Debug.Log("========== 实时同步 Team JSON ==========");

            // 获取当前房间 ID
            if (RoomDetailManager.Instance == null || string.IsNullOrEmpty(RoomDetailManager.Instance.GetCurrentRoomId()))
            {
                Debug.LogWarning("⚠ 无法获取当前房间ID，跳过同步");
                return;
            }

            string currentRoomId = RoomDetailManager.Instance.GetCurrentRoomId();
            Room currentRoom = RoomDataManager.Instance.GetRoomById(currentRoomId);

            if (currentRoom == null)
            {
                Debug.LogError($"❌ 无法找到房间: {currentRoomId}");
                return;
            }

            // 加载 Team 数据
            RoomTeamsData roomTeamsData = TeamJsonFileHandler.Instance.LoadTeamsData(currentRoomId);
            if (roomTeamsData == null)
            {
                Debug.LogError($"❌ 无法加载 Team JSON: {currentRoomId}");
                return;
            }

            // 清空并重新同步玩家列表
            foreach (TeamInfo team in roomTeamsData.teams)
            {
                team.players.Clear();
            }

            // ✓ 同步红队玩家
            TeamInfo redTeam = roomTeamsData.teams.Find(t => t.teamId == currentRoom.teamRedId);
            if (redTeam != null && redTeamContainer != null)
            {
                for (int i = 0; i < redTeamContainer.childCount; i++)
                {
                    Transform playerTransform = redTeamContainer.GetChild(i);
                    DraggablePlayerButton playerBtn = playerTransform.GetComponent<DraggablePlayerButton>();

                    if (playerBtn != null)
                    {
                        TeamPlayer teamPlayer = new TeamPlayer(playerBtn.playerId, playerBtn.playerName);
                        redTeam.players.Add(teamPlayer);
                    }
                }
                redTeam.alivePlayerCount = redTeam.players.Count;
                Debug.Log($"✓ 红队已同步: {redTeam.players.Count} 人");
            }

            // ✓ 同步蓝队玩家
            TeamInfo blueTeam = roomTeamsData.teams.Find(t => t.teamId == currentRoom.teamBlueId);
            if (blueTeam != null && blueTeamContainer != null)
            {
                for (int i = 0; i < blueTeamContainer.childCount; i++)
                {
                    Transform playerTransform = blueTeamContainer.GetChild(i);
                    DraggablePlayerButton playerBtn = playerTransform.GetComponent<DraggablePlayerButton>();

                    if (playerBtn != null)
                    {
                        TeamPlayer teamPlayer = new TeamPlayer(playerBtn.playerId, playerBtn.playerName);
                        blueTeam.players.Add(teamPlayer);
                    }
                }
                blueTeam.alivePlayerCount = blueTeam.players.Count;
                Debug.Log($"✓ 蓝队已同步: {blueTeam.players.Count} 人");
            }

            // 保存更新
            TeamJsonFileHandler.Instance.SaveTeamsData(currentRoomId, roomTeamsData);

            Debug.Log("========== Team JSON 实时同步完成 ==========");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ 实时同步 Team JSON 失败: {e.Message}\n{e.StackTrace}");
        }
    }

    /// <summary>
    /// 获取当前玩家数量（使用实际统计）
    /// </summary>
    public int GetPlayerCount()
    {
        return GetActualTotalCount();
    }

    /// <summary>
    /// 获取红队玩家数量
    /// </summary>
    public int GetRedTeamPlayerCount()
    {
        return GetActualRedTeamCount();
    }

    /// <summary>
    /// 获取蓝队玩家数量
    /// </summary>
    public int GetBlueTeamPlayerCount()
    {
        return GetActualBlueTeamCount();
    }

    /// <summary>
    /// 清除所有玩家（用于重置）
    /// </summary>
    public void ClearAllPlayers()
    {
        if (redTeamContainer != null)
        {
            foreach (Transform child in redTeamContainer)
            {
                Destroy(child.gameObject);
            }
        }

        if (blueTeamContainer != null)
        {
            foreach (Transform child in blueTeamContainer)
            {
                Destroy(child.gameObject);
            }
        }

        TeamAssignManager.Instance.ClearAllTeams();
        Debug.Log("✓ 已清除所有玩家");
    }
}
