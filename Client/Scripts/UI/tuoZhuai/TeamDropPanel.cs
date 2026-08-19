using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class TeamDropPanel : MonoBehaviour, IDropHandler, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Drop信息")]
    public string teamId;
    public string teamName;

    [Header("颜色")]
    public Color normalColor = new Color(1f, 1f, 1f, 0.15f);
    public Color highlightColor = new Color(0f, 1f, 0f, 0.35f);
    public Color fullColor = new Color(1f, 0.85f, 0f, 0.45f);

    private Image panelImage;
    private bool isProcessingDrop = false;

    [Header("★ 对方队伍标记（可选：用来动态查找）")]
    public string oppositeTeamId; // 例如：这是红队，则填 "blue"；这是蓝队，则填 "red"

    [Header("手动设置（可选，不设置则自动查找）")]
    public Transform oppositePanel;

    private void Start()
    {
        panelImage = GetComponent<Image>();
        if (panelImage != null)
            panelImage.color = normalColor;

        // ★ 如果没手动设置 oppositeTeamId，则根据 teamId 推断
        if (string.IsNullOrEmpty(oppositeTeamId))
        {
            oppositeTeamId = (teamId == "red") ? "blue" : "red";
            Debug.Log($"[TeamDropPanel] {teamName} 自动设置 oppositeTeamId={oppositeTeamId}");
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!eventData.dragging) return;

        bool isFull = TeamAssignManager.Instance.IsTeamFull(teamId);
        if (panelImage != null)
            panelImage.color = isFull ? fullColor : highlightColor;

        Debug.Log($"[OnPointerEnter] team={teamId} isFull={isFull}");
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (panelImage != null)
            panelImage.color = normalColor;

        Debug.Log($"[OnPointerExit] team={teamId}");
    }

    public void OnDrop(PointerEventData eventData)
    {
        if (isProcessingDrop)
        {
            Debug.LogWarning($"正在处理中，忽略此次drop");
            return;
        }

        isProcessingDrop = true;

        try
        {
            if (panelImage != null)
                panelImage.color = normalColor;

            GameObject draggedGO = eventData.pointerDrag;

            DraggablePlayerButton draggedComponent = draggedGO?.GetComponent<DraggablePlayerButton>();

            if (draggedComponent == null)
            {
                Debug.LogError(" 无法获取 DraggablePlayerButton 组件");
                return;
            }

            // 同队检测
            if (draggedComponent.currentTeam == teamId)
            {
                Debug.Log($"检测到同队,执行 ReturnToOrigin()");
                draggedComponent.ReturnToOrigin();
                return;
            }

            Debug.Log($"不是同队，继续处理...");

            // 找到"当前有效的同ID按钮"
            DraggablePlayerButton validButton = FindActiveButtonById(draggedComponent.playerId);
            if (validButton != null && validButton != draggedComponent)
            {
                draggedComponent = validButton;
            }

            bool isFull = TeamAssignManager.Instance.IsTeamFull(teamId);

            if (!isFull)
            {
                Debug.Log($"目标队伍未满 → 正常分配");
                bool success = TeamAssignManager.Instance.AssignPlayerToTeam(
                    draggedComponent.playerId, draggedComponent.playerName, teamId);

                if (success)
                {
                    Debug.Log($"AssignPlayerToTeam 成功");
                    draggedComponent.OnAssignedToTeam(teamId, this.transform);
                    Debug.Log($"[分配完成] {draggedComponent.playerName} → {teamName}");
                }
                else
                {
                    Debug.LogError($"AssignPlayerToTeam 失败");
                    draggedComponent.ReturnToOrigin();
                }
            }
            else
            {
                string kickedId = TeamAssignManager.Instance.SwapPlayerToTeam(
                    draggedComponent.playerId, draggedComponent.playerName, teamId);

                if (string.IsNullOrEmpty(kickedId))
                {
                    draggedComponent.ReturnToOrigin();
                    return;
                }


                // 先把拖拽者换进当前面板
                draggedComponent.OnAssignedToTeam(teamId, this.transform);

                // ★ 关键修复：动态找对方面板（而不是用预先拖入的引用）
                Transform oppositePanelTransform = FindOppositePanelDynamic();
                if (oppositePanelTransform == null && oppositePanel != null)
                {
                    // 如果动态找不到，尝试用手动设置的引用
                    Debug.LogWarning($"⚠️ 动态找不到对方面板，尝试使用手动设置的 oppositePanel");
                    oppositePanelTransform = oppositePanel;
                }

                DraggablePlayerButton kickedButton = FindActiveButtonById(kickedId);
                if (kickedButton != null && oppositePanelTransform != null)
                {
                    string oppositeTeam = teamId == "red" ? "blue" : "red";
                    Debug.Log($"✓ [互换] 被踢玩家 {kickedId} -> {oppositeTeam}");
                    Debug.Log($"  → kickedButton: {kickedButton.playerName}");
                    Debug.Log($"  → oppositePanelTransform: {oppositePanelTransform.name}");
                    kickedButton.OnKickedToTeam(oppositeTeam, oppositePanelTransform);
                    Debug.Log($"✓ [互换完成] {draggedComponent.playerName} ↔ {kickedButton.playerName}");
                }
                else
                {
                    Debug.LogWarning($"⚠️ 互换失败:");
                    Debug.LogWarning($"  → kickedButton: {(kickedButton ? kickedButton.playerName : "NULL")}");
                    Debug.LogWarning($"  → oppositePanelTransform: {(oppositePanelTransform ? oppositePanelTransform.name : "NULL")}");
                }
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[OnDrop] EXCEPTION: {ex}");
        }
        finally
        {
            isProcessingDrop = false;

            //  找到红蓝两队的面板容器
            var allTeamPanels = FindObjectsByType<TeamDropPanel>(FindObjectsSortMode.None);
            Transform redTeamPanel = null;
            Transform blueTeamPanel = null;

            foreach (var panel in allTeamPanels)
            {
                if (panel.teamId == "red") redTeamPanel = panel.transform;
                else if (panel.teamId == "blue") blueTeamPanel = panel.transform;
            }

            //  延迟一帧后刷新编号 + 同步JSON（在协程里完成）
            StartCoroutine(RefreshAllTeamsAfterFrame(redTeamPanel, blueTeamPanel));

        }



    }
    private System.Collections.IEnumerator RefreshAllTeamsAfterFrame(
        Transform redPanel, Transform bluePanel)
    {
        yield return null; // 等一帧，确保 Destroy 已执行

        if (redPanel != null)
            TeamUIRefresher.RefreshTeamPlayerNames(redPanel, "red");
        if (bluePanel != null)
            TeamUIRefresher.RefreshTeamPlayerNames(bluePanel, "blue");

        // ★ 刷新完名字后再同步 JSON
        if (RoomDetailManager.Instance != null && redPanel != null && bluePanel != null)
        {
            RoomDetailManager.Instance.SyncPlayersToTeamJson(redPanel, bluePanel);
        }
    }

    /// <summary>
    /// ★ 关键修复：动态查找对方队伍的 Panel（而不是依赖预先拖入的引用）
    /// </summary>
    private Transform FindOppositePanelDynamic()
    {
        var allPanels = FindObjectsByType<TeamDropPanel>(FindObjectsSortMode.None);

        Debug.Log($"[SearchOppositePanel] 搜索 oppositeTeamId={oppositeTeamId}, 场景共{allPanels.Length}个 TeamDropPanel");

        foreach (var panel in allPanels)
        {
            Debug.Log($"  - {panel.teamName} (teamId={panel.teamId})");

            if (panel.teamId == oppositeTeamId)
            {
                Debug.Log($"  ✓ 找到对方面板: {panel.teamName}");
                return panel.transform;
            }
        }

        Debug.LogWarning($"❌ 找不到 oppositeTeamId={oppositeTeamId} 的面板");
        return null;
    }

    /// <summary>
    /// 找到激活且有效的按钮
    /// </summary>
    private DraggablePlayerButton FindActiveButtonById(string id)
    {
        var all = FindObjectsByType<DraggablePlayerButton>(FindObjectsSortMode.None);

        Debug.Log($"[SearchButton] 搜索ID={id}, 场景中共{all.Length}个按钮:");

        foreach (var btn in all)
        {
            bool raycastable = true;
            var cg = btn.GetComponent<CanvasGroup>();
            if (cg != null) raycastable = cg.blocksRaycasts;

            bool ok = (btn.playerId == id) && btn.enabled && btn.gameObject.activeSelf && raycastable;

            Debug.Log($"  - {btn.playerName} (ID={btn.playerId}, enabled={btn.enabled}, active={btn.gameObject.activeSelf}, raycastable={raycastable})");

            if (ok)
            {
                Debug.Log($"  ✓ 找到有效按钮: {btn.playerName}");
                return btn;
            }
        }

        Debug.LogWarning($"❌ 找不到有效的按钮 (ID={id})");
        return null;
    }
}