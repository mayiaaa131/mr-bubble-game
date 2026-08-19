using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Collections;
using TMPro; // ★ 顶部加上这个！  
public class DraggablePlayerButton : MonoBehaviour,
    IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("玩家数据")]
    public string playerId;
    public string playerName;
    public string currentTeam = "";

    [Header("拖拽设置")]
    public Canvas rootCanvas;

    [Header("★ 队伍预制体")]
    public GameObject redTeamPlayerPrefab;
    public GameObject blueTeamPlayerPrefab;

    public Transform blueTeamContainer;

    private RectTransform rectTransform;
    private CanvasGroup canvasGroup;

    private GameObject ghostObj;
    private RectTransform ghostRect;

    private Transform originalParent;
    private int originalSiblingIndex;
    private Vector2 originalAnchoredPos;

    private bool assignedThisDrag = false;

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();

        if (rootCanvas == null)
            rootCanvas = GetComponentInParent<Canvas>();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        Debug.Log($"[OnBeginDrag] {playerName} (ID={playerId})");

        assignedThisDrag = false;

        // 保存当前位置
        originalParent = transform.parent;
        originalSiblingIndex = transform.GetSiblingIndex();
        originalAnchoredPos = rectTransform.anchoredPosition;

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0.33f;
            canvasGroup.blocksRaycasts = false;
        }

        if (rootCanvas == null)
        {
            Debug.LogError($"❌ {playerName} rootCanvas is null");
            return;
        }

        // ★ 关键修复：先清理旧 ghost（防止遗留）
        DestroyGhost();

        // ★ 改为同步创建 ghost（不用 coroutine）
        ghostObj = Instantiate(gameObject, rootCanvas.transform);
        ghostObj.name = $"{playerName}_ghost_{playerId}";

        ghostRect = ghostObj.GetComponent<RectTransform>();

        // 配置 ghost
        var ghostDrag = ghostObj.GetComponent<DraggablePlayerButton>();
        if (ghostDrag != null) ghostDrag.enabled = false;

        var ghostLE = ghostObj.GetComponent<LayoutElement>();
        if (ghostLE != null) ghostLE.enabled = false;

        var ghostCSF = ghostObj.GetComponent<ContentSizeFitter>();
        if (ghostCSF != null) ghostCSF.enabled = false;

        var ghostCG = ghostObj.GetComponent<CanvasGroup>();
        if (ghostCG == null) ghostCG = ghostObj.AddComponent<CanvasGroup>();
        ghostCG.blocksRaycasts = false;
        ghostCG.alpha = 0.65f;

        // 设置初始大小
        float w, h;
        var le = GetComponent<LayoutElement>();
        if (le != null && le.preferredWidth > 0)
        {
            w = le.preferredWidth;
            h = le.preferredHeight;
        }
        else
        {
            w = rectTransform.rect.width;
            h = rectTransform.rect.height;
        }
        ghostRect.sizeDelta = new Vector2(w, h);

        // 设置初始位置
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            rootCanvas.GetComponent<RectTransform>(),
            eventData.position,
            eventData.pressEventCamera,
            out Vector2 localPoint))
        {
            ghostRect.localPosition = localPoint;
        }

        Debug.Log($"  → Ghost created: {ghostObj.name}");
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (ghostRect == null || rootCanvas == null) return;

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            rootCanvas.GetComponent<RectTransform>(),
            eventData.position,
            eventData.pressEventCamera,
            out Vector2 localPoint))
        {
            ghostRect.localPosition = localPoint;
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        Debug.Log($"[OnEndDrag] {playerName} (ID={playerId}) assignedThisDrag={assignedThisDrag}");

        // ★ 清理 ghost
        DestroyGhost();

        // 如果本次拖拽没有被分配，恢复按钮
        if (!assignedThisDrag && canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.blocksRaycasts = true;
        }
    }

    /// <summary>
    /// ★ 强制销毁 ghost 的专用方法（确保清理干净）
    /// </summary>
    private void DestroyGhost()
    {
        if (ghostObj != null)
        {
            Debug.Log($"[DestroyGhost] {playerName} destroying {ghostObj.name}");
            Destroy(ghostObj);
            ghostObj = null;
            ghostRect = null;
        }
    }

    public void OnAssignedToTeam(string teamId, Transform newParent)
    {
        assignedThisDrag = true;
        Debug.Log($"[OnAssignedToTeam] {playerName} -> {teamId}");
        ReplaceWithTeamPrefab(teamId, newParent);
    }

    public void OnKickedToTeam(string teamId, Transform newParent)
    {
        Debug.Log($"[OnKickedToTeam] {playerName} -> {teamId}");
        ReplaceWithTeamPrefab(teamId, newParent);
    }

    private void ReplaceWithTeamPrefab(string teamId, Transform newParent)
    {
        GameObject prefabToUse = (teamId == "red") ? redTeamPlayerPrefab : blueTeamPlayerPrefab;

        if (prefabToUse == null)
        {
            Debug.LogError($"❌ {teamId}队预制体未设置！playerId={playerId}");
            return;
        }

        GameObject oldButton = this.gameObject;
        DestroyGhost();

        // ★ 完全照抄 PlayerCreationManager 的逻辑  
        // 先数 newParent 下当前有多少子物体（排除自己，因为自己马上要被销毁）  
        int existingCount = 0;
        for (int i = 0; i < newParent.childCount; i++)
        {
            if (newParent.GetChild(i).gameObject != oldButton)
                existingCount++;
        }

        // ★ 和创建玩家一样：childCount + 1 = 新编号  
        int newIndex = existingCount + 1;
        string prefix = (teamId == "red") ? "红队玩家" : "蓝队玩家";
        string dynamicName = $"{prefix}{newIndex}";

        Debug.Log($"[ReplaceWithTeamPrefab] newParent={newParent.name}, existingCount={existingCount}, 新名字={dynamicName}");

        // 实例化新按钮  
        GameObject newButton = Instantiate(prefabToUse, newParent);
        newButton.name = $"{dynamicName}_{teamId}";

        // ★ 和创建玩家一样：用 TextMeshProUGUI 更新文字  
        TextMeshProUGUI buttonText = newButton.GetComponentInChildren<TextMeshProUGUI>();
        if (buttonText != null)
        {
            buttonText.text = dynamicName;
            Debug.Log($"  → TextMeshProUGUI 已更新: {dynamicName}");
        }
        else
        {
            Debug.LogWarning($"  ⚠️ 找不到 TextMeshProUGUI！请检查预制体结构");
        }

        // 复制数据到新按钮  
        DraggablePlayerButton newDraggable = newButton.GetComponent<DraggablePlayerButton>();
        if (newDraggable != null)
        {
            newDraggable.playerId = this.playerId;
            newDraggable.playerName = dynamicName; // ★ 用动态名字  
            newDraggable.currentTeam = teamId;
            newDraggable.redTeamPlayerPrefab = this.redTeamPlayerPrefab;
            newDraggable.blueTeamPlayerPrefab = this.blueTeamPlayerPrefab;
            newDraggable.blueTeamContainer = this.blueTeamContainer;
            newDraggable.rootCanvas = this.rootCanvas;

            newDraggable.enabled = true;
            var cg = newButton.GetComponent<CanvasGroup>();
            if (cg != null)
            {
                cg.alpha = 1f;
                cg.blocksRaycasts = true;
            }
        }

        Destroy(oldButton);
        Debug.Log($"[ReplaceWithTeamPrefab] 完成 → {dynamicName}");


        // ★ 新增：销毁旧按钮后，延迟一帧再重排（等 Destroy 生效）  
        // 需要用 MonoBehaviour 的 Coroutine，改为在 newDraggable 上启动  
        if (newDraggable != null)
        {
            newDraggable.StartCoroutine(RefreshAfterFrame(teamId, newParent));
        }
    }


    private System.Collections.IEnumerator RefreshAfterFrame(string teamId, Transform container)
    {
        yield return null; // 等一帧，确保 Destroy 已执行  
        TeamUIRefresher.RefreshTeamPlayerNames(container, teamId);
    }
    public void ReturnToOrigin()
    {
        if (originalParent == null)
        {
            Debug.LogError($"❌ {playerName} originalParent is null");
            return;
        }

        StartCoroutine(ReturnToOriginCoroutine());
    }

    private IEnumerator ReturnToOriginCoroutine()
    {
        transform.SetParent(originalParent, false);
        yield return null;

        transform.SetSiblingIndex(originalSiblingIndex);
        yield return null;

        rectTransform.anchoredPosition = originalAnchoredPos;

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.blocksRaycasts = true;
        }

        Debug.Log($"[ReturnToOrigin] {playerName} restored");
    }
}