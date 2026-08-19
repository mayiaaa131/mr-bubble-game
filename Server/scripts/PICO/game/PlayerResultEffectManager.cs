using UnityEngine;
using BubbleBattle.Network;
using System.Collections.Generic;

/// <summary>
/// 结算时玩家胜利/失败特效管理器
/// 在 SingleRoundResultUI 或 GameScoreboardUI 弹出结算时被调用
/// </summary>
public class PlayerResultEffectManager : MonoBehaviour
{
    [Header("远程玩家管理器引用")]
    [SerializeField] private PicoRemotePlayerManager remotePlayerManager;

    [Header("本地玩家胜利/失败特效")]
    [SerializeField] private GameObject localVictoryEffect;   // 挂在本地玩家Transform下
    [SerializeField] private GameObject localDefeatEffect;    // 挂在本地玩家Transform下

    [Header("特效子节点名称（预制体内）")]
    [SerializeField] private string victoryEffectName = "VictoryEffect";
    [SerializeField] private string defeatEffectName = "DefeatEffect";

    private string _localPlayerId;
    private string _localTeamId;

    private bool _isShowing = false;

    void Start()
    {
        // 监听本地玩家ID分配
        if (PicoWebSocketClient.Instance != null)
        {
            PicoWebSocketClient.Instance.OnPlayerAssignedId += OnLocalPlayerIdAssigned;
        }
    }

    void OnDestroy()
    {
        if (PicoWebSocketClient.Instance != null)
        {
            PicoWebSocketClient.Instance.OnPlayerAssignedId -= OnLocalPlayerIdAssigned;
        }
    }

    private void OnLocalPlayerIdAssigned(string playerId)
    {
        _localPlayerId = playerId;
        Debug.Log($"[PlayerResultEffectManager] 本地玩家ID: {_localPlayerId}");
    }

    /// <summary>
    /// 由外部调用（SingleRoundResultUI / GameScoreboardUI 弹出时）
    /// winnerTeamName: 从 GameEndMsg 或 ScoreBroadcast 中取到的获胜队伍名
    /// </summary>
    public void ShowResultEffects(string winnerTeamName)
    {
        if (_isShowing) return;
        _isShowing = true;

        // 先隐藏所有特效（防止重复触发）
        HideAllResultEffects();

        bool redWins = winnerTeamName.ToLower().Contains("red");
        bool blueWins = winnerTeamName.ToLower().Contains("blue");
        bool isDraw = redWins && blueWins;

        Debug.Log($"[PlayerResultEffectManager] 显示结算特效，赢家: {winnerTeamName}");

        // ── 远程玩家特效 ──
        var allRemotePlayers = remotePlayerManager.GetAllRemotePlayers();
        foreach (var kvp in allRemotePlayers)
        {
            string teamId = kvp.Value.TeamId;
            GameObject go = kvp.Value.GameObject;

            bool isWinner;
            if (isDraw)
                isWinner = true; // 平局全部显示胜利
            else if (teamId.ToLower().Contains("red"))
                isWinner = redWins;
            else
                isWinner = blueWins;

            SetPlayerResultEffect(go.transform, isWinner);
        }

        // ── 本地玩家特效 ──
        bool localIsWinner;
        if (isDraw)
        {
            localIsWinner = true;
        }
        else if (!string.IsNullOrEmpty(_localTeamId))
        {
            localIsWinner = _localTeamId.ToLower().Contains("red") ? redWins : blueWins;
        }
        else
        {
            Debug.LogWarning("[PlayerResultEffectManager] 本地TeamId未知，跳过本地特效");
            return;
        }

        SetLocalPlayerResultEffect(localIsWinner);
    }

    /// <summary>
    /// 在预制体下找到对应特效节点并激活
    /// </summary>
    private void SetPlayerResultEffect(Transform playerTransform, bool isWinner)
    {
        Transform victoryTf = playerTransform.Find(victoryEffectName);
        Transform defeatTf = playerTransform.Find(defeatEffectName);

        // 只在未激活时才 SetActive，避免重复触发粒子  
        if (victoryTf != null && !victoryTf.gameObject.activeSelf)
            victoryTf.gameObject.SetActive(isWinner);
        if (defeatTf != null && !defeatTf.gameObject.activeSelf)
            defeatTf.gameObject.SetActive(!isWinner);
    }

    /// <summary>
    /// 本地玩家特效（通过Inspector直接引用）
    /// </summary>
    private void SetLocalPlayerResultEffect(bool isWinner)
    {
        // 同样只在未激活时才 SetActive  
        if (localVictoryEffect != null && !localVictoryEffect.activeSelf)
            localVictoryEffect.SetActive(isWinner);
        if (localDefeatEffect != null && !localDefeatEffect.activeSelf)
            localDefeatEffect.SetActive(!isWinner);
    }

    /// <summary>
    /// 隐藏所有胜负特效（换局时清理）
    /// </summary>
    public void HideAllResultEffects()
    {
        // 远程玩家
        if (remotePlayerManager != null)
        {
            var allRemotePlayers = remotePlayerManager.GetAllRemotePlayers();
            foreach (var kvp in allRemotePlayers)
            {
                Transform root = kvp.Value.GameObject.transform;
                Transform victoryTf = root.Find(victoryEffectName);
                Transform defeatTf = root.Find(defeatEffectName);
                if (victoryTf != null) victoryTf.gameObject.SetActive(false);
                if (defeatTf != null) defeatTf.gameObject.SetActive(false);
            }
        }
        // 本地玩家
        if (localVictoryEffect != null) localVictoryEffect.SetActive(false);
        if (localDefeatEffect != null) localDefeatEffect.SetActive(false);
    }

    /// <summary>
    /// 供外部设置本地玩家的队伍ID（在收到WorldState/PlayerInfo后调用）
    /// </summary>
    public void SetLocalTeamId(string teamId)
    {
        _localTeamId = teamId;
        Debug.Log($"[PlayerResultEffectManager] 本地玩家队伍: {_localTeamId}");
    }

    /// <summary>  
    /// 完全重置状态，允许下一轮重新触发特效（换局时由外部调用）  
    /// </summary>  
    public void ResetEffectState()
    {
        HideAllResultEffects();
        _isShowing = false; // 只在这里重置  
    }
}
