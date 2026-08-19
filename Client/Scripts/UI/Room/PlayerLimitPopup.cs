using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 人数上限提示弹窗
/// 职责：只负责弹窗的显示/隐藏和内容更新
/// </summary>
public class PlayerLimitPopup : MonoBehaviour
{
    public static PlayerLimitPopup Instance { get; private set; }

    [Header("弹窗根节点")]
    [SerializeField] private GameObject popupRoot; // 整个弹窗 GameObject

    [Header("按钮")]
    [SerializeField] private Button confirmButton;

    private void Awake()
    {
        // 单例
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        // 初始隐藏
        if (popupRoot != null)
            popupRoot.SetActive(false);

        // 绑定确认按钮
        if (confirmButton != null)
            confirmButton.onClick.AddListener(Hide);
    }

    /// <summary>
    /// 显示弹窗（自动读取房间JSON中的最大人数）
    /// </summary>
    public void Show()
    {
        // ★ 从 RoomDetailManager 读取 JSON 中的最大人数
        int maxPlayers = 0;
        int currentPlayers = 0;

        if (RoomDetailManager.Instance != null)
        {
            maxPlayers = RoomDetailManager.Instance.GetMaxPlayerCount();
            currentPlayers = RoomDetailManager.Instance.GetCurrentPlayerCount();
        }


        // 显示弹窗
        if (popupRoot != null)
            popupRoot.SetActive(true);

        Debug.Log($"[PlayerLimitPopup] 弹窗已显示: {currentPlayers}/{maxPlayers}");
    }

    /// <summary>
    /// 隐藏弹窗
    /// </summary>
    public void Hide()
    {
        if (popupRoot != null)
            popupRoot.SetActive(false);

        Debug.Log("[PlayerLimitPopup] 弹窗已关闭");
    }
}
