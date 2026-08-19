// SingleRoundResultUI.cs - 修复版
using UnityEngine;
using BubbleBattle.Network;
using TMPro;

/// <summary>
/// 单局结束结果UI（简化版）
/// 只显示哪个队伍胜出的预制体
/// 自动在延迟时间后关闭，并恢复炸弹放置功能
/// </summary>
public class SingleRoundResultUI : MonoBehaviour
{
    [Header("UI 面板")]
    [SerializeField] private GameObject redWinPanel;
    [SerializeField] private GameObject blueWinPanel;
    [SerializeField] private GameObject drawPanel;

    [Header("炸弹UI管理器引用")]
    [SerializeField] private BombUIManager bombUIManager;


    [Header("延迟设置")]
    [SerializeField] private float autoCloseDelay = 1f;

    [Header("炸弹放置脚本引用")]
    [SerializeField] private PicoBombPlacement picoBombPlacement;
    [Header("结算特效管理器")]
    [SerializeField] private PlayerResultEffectManager playerResultEffectManager;

    private float _closeTimer = -1f;          // -1 表示未激活
    private GameObject _currentActivePanel;
    private ScoreBroadcast _lastScoreBroadcast;
    private GameEndMsg _lastGameEndMsg;

    private bool _resultPanelDisplayed = false;  // 添加标志位  

    // 由 GameScoreboardUI 设置，系列赛结束后阻止本脚本弹出任何 UI
    private bool _seriesEnded = false;

    void Start()
    {
        if (redWinPanel != null) redWinPanel.SetActive(false);
        if (blueWinPanel != null) blueWinPanel.SetActive(false);
        if (drawPanel != null) drawPanel.SetActive(false);
        bombUIManager = FindObjectOfType<BombUIManager>();



        if (picoBombPlacement == null)
        {
            //picoBombPlacement = FindObjectOfType<PicoBombPlacement>();
            if (picoBombPlacement != null)
                Debug.Log("[SingleRound] 自动找到 PicoBombPlacement");
            else
                Debug.LogWarning("[SingleRound] 未找到 PicoBombPlacement，请在 Inspector 中赋值");
        }

        if (PicoWebSocketClient.Instance != null)
        {
            PicoWebSocketClient.Instance.OnGameEndReceived += OnGameEndReceived;
            PicoWebSocketClient.Instance.OnScoreBroadcastReceived += OnScoreBroadcastReceived;
        }
    }

    void Update()
    {
        // 只有 timer 被激活（>= 0）且面板可见时才倒计时
        if (_closeTimer >= 0f && _currentActivePanel != null && _currentActivePanel.activeSelf)
        {
            _closeTimer -= Time.deltaTime;
            if (_closeTimer <= 0f)
            {
                redWinPanel.SetActive(false);
                blueWinPanel.SetActive(false);
                drawPanel.SetActive(false);
                CloseResultPanel();
            }
        }
    }

    void OnDestroy()
    {
        if (PicoWebSocketClient.Instance != null)
        {
            PicoWebSocketClient.Instance.OnGameEndReceived -= OnGameEndReceived;
            PicoWebSocketClient.Instance.OnScoreBroadcastReceived -= OnScoreBroadcastReceived;
        }
    }

    /// <summary>
    /// 由 GameScoreboardUI 调用，标记系列赛已结束，阻止本脚本继续弹出 UI
    /// 必须在取消事件订阅之前调用，或直接替代 enabled = false
    /// </summary>
    public void NotifySeriesEnded()
    {
        _seriesEnded = true;
        _resultPanelDisplayed = false;
        // 如果当前有面板正在显示，立即关闭
        if (_currentActivePanel != null && _currentActivePanel.activeSelf)
        {
            _currentActivePanel.SetActive(false);
            _currentActivePanel = null;
        }
        _closeTimer = -1f;

        Debug.Log("[SingleRound] 系列赛已结束，单局 UI 已禁用");
    }

    private void OnScoreBroadcastReceived(ScoreBroadcast scoreBroadcast)
    {
        if (_seriesEnded) return;

        _lastScoreBroadcast = scoreBroadcast;

        if (!_resultPanelDisplayed  // 添加这个条件  
        && _lastGameEndMsg != null
        && _lastGameEndMsg.remainingTime <= 0
        && !_lastGameEndMsg.isSeriesEnd)
        {
            picoBombPlacement.enabled = false;
            DetermineAndDisplayWinner();
        }
    }

    private void OnGameEndReceived(GameEndMsg gameEndMsg)
    {
        // 系列赛最终结束 → 直接忽略，由 GameScoreboardUI 处理
        if (gameEndMsg.isSeriesEnd)
        {
            Debug.Log("[SingleRound] 检测到系列赛结束，不显示单局 UI");
            return;
        }

        if (_seriesEnded) return;

        if (gameEndMsg.remainingTime <= 0)
        {
            _lastGameEndMsg = gameEndMsg;

            Debug.Log($"[SingleRound] 单局结束，剩余回合数：{gameEndMsg.remainingRounds}");

            //DisableBombPlacement();
            DetermineAndDisplayWinner();
        }
    }

    private void DetermineAndDisplayWinner()
    {
        if (_seriesEnded) return;

        if (_lastScoreBroadcast == null
            || _lastScoreBroadcast.teams == null
            || _lastScoreBroadcast.teams.Length == 0)
        {
            Debug.LogWarning("[SingleRound] 得分消息不可用，无法判断获胜队");
            return;
        }

        int redScore = 0, blueScore = 0;
        string redName = "红队", blueName = "蓝队";

        foreach (var team in _lastScoreBroadcast.teams)
        {
            if (team.teamId.ToLower().Contains("red"))
            {
                redScore = team.totalScore;
                redName = team.teamName ?? "红队";
            }
            else if (team.teamId.ToLower().Contains("blue"))
            {
                blueScore = team.totalScore;
                blueName = team.teamName ?? "蓝队";
            }
        }

        Debug.Log($"[SingleRound] 单局得分 - {redName}: {redScore}, {blueName}: {blueScore}");

        if (redScore > blueScore)
            DisplayWinnerPanel("red", redName);
        else if (blueScore > redScore)
            DisplayWinnerPanel("blue", blueName);
        else
            DisplayDrawPanel(redName, blueName, redScore);
    }

    private void DisplayWinnerPanel(string winnerTeamId, string winnerTeamName)
    {
        if (redWinPanel != null) redWinPanel.SetActive(false);
        if (blueWinPanel != null) blueWinPanel.SetActive(false);
        if (drawPanel != null) drawPanel.SetActive(false);

        GameObject target = winnerTeamId.ToLower().Contains("red") ? redWinPanel
                          : winnerTeamId.ToLower().Contains("blue") ? blueWinPanel
                          : null;

        if (target != null)
        {
            target.SetActive(true);
            _currentActivePanel = target;
            // 在面板真正显示时才启动计时器
            _resultPanelDisplayed = true;
            _closeTimer = autoCloseDelay;

            // 新增：触发胜负特效  
            if (playerResultEffectManager != null)
                playerResultEffectManager.ShowResultEffects(winnerTeamId);

            Debug.Log($"[SingleRound] 显示 {winnerTeamName} 获胜面板，{autoCloseDelay}s 后关闭");
            //新增
            if (picoBombPlacement != null)
            {
                if (bombUIManager != null) bombUIManager.ResetBombCount();

                picoBombPlacement.enabled = false;
                Debug.Log("[SingleRound] 已禁用炸弹放置脚本");
            }
        }
        else
        {
            Debug.LogError($"[SingleRound] 未知队伍ID：{winnerTeamId}");
        }
    }

    private void DisplayDrawPanel(string redName, string blueName, int score)
    {
        if (redWinPanel != null) redWinPanel.SetActive(false);
        if (blueWinPanel != null) blueWinPanel.SetActive(false);

        if (drawPanel != null)
        {
            //新增
            if (picoBombPlacement != null)
            {
                if (bombUIManager != null) bombUIManager.ResetBombCount();

                picoBombPlacement.enabled = false;
                Debug.Log("[SingleRound] 已禁用炸弹放置脚本");
            }
            drawPanel.SetActive(true);
            _currentActivePanel = drawPanel;
            // 在面板真正显示时才启动计时器
            _resultPanelDisplayed = true;
            _closeTimer = autoCloseDelay;

            // 新增：平局触发（红蓝都显示胜利）  
            if (playerResultEffectManager != null)
                playerResultEffectManager.ShowResultEffects("red+blue");

            Debug.Log($"[SingleRound] 显示平局面板 - {redName}:{score} vs {blueName}:{score}，{autoCloseDelay}s 后关闭");
        }
        else
        {
            Debug.LogError("[SingleRound] drawPanel 未赋值！");
        }
    }

    private void CloseResultPanel()
    {
        if (_currentActivePanel != null)
        {
            _currentActivePanel.SetActive(false);
            _currentActivePanel = null;
        }
        redWinPanel.SetActive(false);
        blueWinPanel.SetActive(false);
        drawPanel.SetActive(false);
        _closeTimer = -1f;
        Debug.Log("[SingleRound] 结果面板已关闭");
        // 新增：关闭时隐藏所有特效  
        if (playerResultEffectManager != null)
            playerResultEffectManager.ResetEffectState(); // 明确重置，而不是隐式副作用 
    }

    private void DisableBombPlacement()
    {
        if (picoBombPlacement != null)
        {
            picoBombPlacement.enabled = false;
            Debug.Log("[SingleRound] 已禁用炸弹放置脚本");
        }
        else
        {
            Debug.LogWarning("[SingleRound] picoBombPlacement 为 null，无法禁用");
        }
    }
}