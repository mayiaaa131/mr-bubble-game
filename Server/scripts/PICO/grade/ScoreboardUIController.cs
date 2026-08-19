using UnityEngine;
using TMPro;
using BubbleBattle.Network;

/// <summary>
/// 得分显示UI控制脚本
/// 简单显示红队得分 vs 蓝队得分（基于玩家死亡分数）
/// 使用 TextMeshPro
/// </summary>
public class ScoreboardUIController : MonoBehaviour
{
    [Header("队伍得分显示")]
    [SerializeField] private TextMeshProUGUI redTeamScoreText;
    [SerializeField] private TextMeshProUGUI blueTeamScoreText;

    private int currentRedScore = 0;
    private int currentBlueScore = 0;

    void Start()
    {
        // 订阅得分广播事件
        if (PicoWebSocketClient.Instance != null)
        {
            PicoWebSocketClient.Instance.OnScoreBroadcastReceived += OnScoreBroadcastReceived;
        }

        Debug.Log("[ScoreDisplay] 初始化完成");
    }

    /// <summary>
    /// 当收到得分广播时调用
    /// </summary>
    private void OnScoreBroadcastReceived(ScoreBroadcast scoreBroadcast)
    {
        if (scoreBroadcast?.teams == null || scoreBroadcast.teams.Length == 0)
        {
            Debug.LogWarning("[ScoreDisplay] 收到的得分消息为空");
            return;
        }

        // 遍历队伍信息
        foreach (var teamInfo in scoreBroadcast.teams)
        {
            if (teamInfo.teamId.ToLower().Contains("red"))
            {
                currentRedScore = teamInfo.totalScore;  // 直接取 score 字段
                if (redTeamScoreText != null)
                {
                    redTeamScoreText.text = currentRedScore.ToString();
                    Debug.Log($"[ScoreDisplay] 红队得分: {currentRedScore}");
                }
            }
            else if (teamInfo.teamId.ToLower().Contains("blue"))
            {
                currentBlueScore = teamInfo.totalScore;  // 直接取 score 字段
                if (blueTeamScoreText != null)
                {
                    blueTeamScoreText.text = currentBlueScore.ToString();
                    Debug.Log($"[ScoreDisplay] 蓝队得分: {currentBlueScore}");
                }
            }
        }

        Debug.Log($"[ScoreDisplay] 得分更新 - 红队: {currentRedScore}, 蓝队: {currentBlueScore}");
    }

    /// <summary>
    /// 获取当前红队得分
    /// </summary>
    public int GetRedTeamScore() => currentRedScore;

    /// <summary>
    /// 获取当前蓝队得分
    /// </summary>
    public int GetBlueTeamScore() => currentBlueScore;

    void OnDestroy()
    {
        if (PicoWebSocketClient.Instance != null)
        {
            PicoWebSocketClient.Instance.OnScoreBroadcastReceived -= OnScoreBroadcastReceived;
        }
    }
}
