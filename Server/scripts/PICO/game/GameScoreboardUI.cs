// GameScoreboardUI.cs - 修复版
using UnityEngine;
using BubbleBattle.Network;

/// <summary>
/// 游戏总积分榜UI，系列赛结束时显示。
/// 收到 GameEndMsg 且 remainingTime <= 0 && isSeriesEnd == true 时弹出。
/// 显示对应的获胜预制体。
/// </summary>
public class GameScoreboardUI : MonoBehaviour
{
    [Header("积分榜面板")]
    [SerializeField] private GameObject scoreboardPanel;

    [Header("获胜预制体")]
    [SerializeField] private GameObject redWinText;
    [SerializeField] private GameObject blueWinText;
    [SerializeField] private GameObject winwinText;

    [Header("需要隐藏的UI元素")]
    [SerializeField] private GameObject hideGameObject1;
    [SerializeField] private GameObject hideGameObject2;
    [SerializeField] private GameObject hideGameObject3;
    [SerializeField] private GameObject hideGameObject4;
    [SerializeField] private GameObject hideGameObject5;

    [Header("单局结果脚本引用")]
    [SerializeField] private SingleRoundResultUI singleRoundResultUI;

    [Header("炸弹放置脚本引用")]
    [SerializeField] private PicoBombPlacement picoBombPlacement;
    [Header("结算特效管理器")]
    [SerializeField] private PlayerResultEffectManager playerResultEffectManager;

    private bool _isGameEnded = false;

    void Start()
    {
        if (scoreboardPanel != null) scoreboardPanel.SetActive(false);
        if (redWinText != null) redWinText.SetActive(false);
        if (blueWinText != null) blueWinText.SetActive(false);
        if (winwinText != null) winwinText.SetActive(false);

        if (picoBombPlacement == null)
        {
            //picoBombPlacement = FindObjectOfType<PicoBombPlacement>();
            if (picoBombPlacement != null)
                Debug.Log("[GameScoreboard] 自动找到 PicoBombPlacement");
            else
                Debug.LogWarning("[GameScoreboard] 未找到 PicoBombPlacement，请在 Inspector 中赋值");
        }

        if (PicoWebSocketClient.Instance != null)
        {
            PicoWebSocketClient.Instance.OnGameEndReceived += OnGameEndReceived;
        }

        Debug.Log("[GameScoreboard] 初始化完毕");
    }

    void OnDestroy()
    {
        if (PicoWebSocketClient.Instance != null)
        {
            PicoWebSocketClient.Instance.OnGameEndReceived -= OnGameEndReceived;
        }
    }

    private void OnGameEndReceived(GameEndMsg gameEndMsg)
    {
        // 只处理系列赛最终结束
        if (gameEndMsg.remainingTime > 0 || !gameEndMsg.isSeriesEnd)
        {
            Debug.Log("[GameScoreboard] 非系列赛最终局，不显示");
            return;
        }

        if (_isGameEnded) return;
        _isGameEnded = true;

        Debug.Log($"[GameScoreboard] 系列赛结束，赢家：{gameEndMsg.winnerTeamName}");

        // 通知 SingleRoundResultUI 系列赛已结束（而不是仅 enabled = false）
        // 这样可以阻断其事件回调中的显示逻辑，并关闭正在显示的面板
        if (singleRoundResultUI != null)
        {
            singleRoundResultUI.NotifySeriesEnded();
            Debug.Log("[GameScoreboard] 已通知 SingleRoundResultUI 系列赛结束");
        }

        if (picoBombPlacement != null)
        {
            picoBombPlacement.enabled = false;
            Debug.Log("[GameScoreboard] 已禁用炸弹放置脚本");
        }
        else
        {
            Debug.LogWarning("[GameScoreboard] picoBombPlacement 为 null！");
        }

        // 显示积分榜面板
        if (scoreboardPanel != null)
        {
            scoreboardPanel.SetActive(true);
            picoBombPlacement.enabled = false;
            Debug.Log("[GameScoreboard] 显示积分榜面板");
        }

        HideGameObjects();
        DisplayWinnerText(gameEndMsg.winnerTeamName);

        // 新增：系列赛结束，触发胜负特效  
        if (playerResultEffectManager != null)
        {
            playerResultEffectManager.ResetEffectState(); // 先强制重置，再触发
            playerResultEffectManager.ShowResultEffects(gameEndMsg.winnerTeamName);
        }

        DisableBombPlacement();
    }

    private void HideGameObjects()
    {
        if (hideGameObject1 != null) hideGameObject1.SetActive(false);
        if (hideGameObject2 != null) hideGameObject2.SetActive(false);
        if (hideGameObject3 != null) hideGameObject3.SetActive(false);
        if (hideGameObject4 != null) hideGameObject4.SetActive(false);
        if (hideGameObject5 != null) hideGameObject5.SetActive(false);
    }

    private void HideAllWinnerTexts()
    {
        if (redWinText != null) redWinText.SetActive(false);
        if (blueWinText != null) blueWinText.SetActive(false);
        if (winwinText != null) winwinText.SetActive(false);
    }

    private void DisplayWinnerText(string winnerTeamName)
    {
        HideAllWinnerTexts();

        bool hasRed = winnerTeamName.Contains("red");
        bool hasBlue = winnerTeamName.Contains("blue");

        if (hasRed && hasBlue)
        {
            if (winwinText != null)
            {
                winwinText.SetActive(true);
                Debug.Log($"[GameScoreboard] 显示平局预制体 - {winnerTeamName}");
            }
            else Debug.LogError("[GameScoreboard] winwinText 未赋值！");
        }
        else if (hasRed)
        {
            if (redWinText != null)
            {
                redWinText.SetActive(true);
                Debug.Log($"[GameScoreboard] 显示红队获胜预制体 - {winnerTeamName}");
            }
            else Debug.LogError("[GameScoreboard] redWinText 未赋值！");
        }
        else if (hasBlue)
        {
            if (blueWinText != null)
            {
                blueWinText.SetActive(true);
                Debug.Log($"[GameScoreboard] 显示蓝队获胜预制体 - {winnerTeamName}");
            }
            else Debug.LogError("[GameScoreboard] blueWinText 未赋值！");
        }
        else
        {
            Debug.LogError($"[GameScoreboard] 未知队伍名称：{winnerTeamName}");
        }
    }

    private void DisableBombPlacement()
    {
        if (picoBombPlacement != null)
        {
            picoBombPlacement.enabled = false;
            Debug.Log("[GameScoreboard] 已禁用炸弹放置脚本");
        }
        else
        {
            Debug.LogWarning("[GameScoreboard] picoBombPlacement 为 null，无法禁用");
        }
    }
}