using UnityEngine;
using UnityEngine.UI;
using BubbleBattle.Network;
using System.Collections;

/// <summary>
/// 血条UI控制脚本
/// 根据玩家的 TeamId 显示对应颜色的血条
/// 白色血条始终显示，表示损失的血量
/// </summary>
public class HealthBarUIController : MonoBehaviour
{
    [Header("血条父对象")]
    [SerializeField] private GameObject redHeartContainer;    // red 父对象 - 显示当前血量
    [SerializeField] private GameObject blueHeartContainer;   // blue 父对象 - 显示当前血量
    [SerializeField] private GameObject whiteHeartContainer;  // white 父对象 - 始终显示损失血量

    [Header("单个心形图标")]
    [SerializeField] private Image[] redHearts;               // heartred1-6 - 红队当前血量
    [SerializeField] private Image[] blueHearts;              // heartblue1-6 - 蓝队当前血量
    [SerializeField] private Image[] whiteHearts;             // heartwhite1-6 - 损失血量（始终显示）

    private string _currentTeamId;
    private int _currentHealth = 6;
    private int _maxHealth = 6;
    [SerializeField] private GameObject deathUIPanel;
    [SerializeField] private PicoBombPlacement picoBombPlacement;

    [Header("受击和恢复图片")]
    [SerializeField] private Image damageImageUI;        // 受击图片  
    [SerializeField] private Image recoverImageUI;       // 恢复图片  
    [SerializeField] private float imageFadeDuration = 0.5f;  // 图片显示持续时间  
    private int _previousHealth = 6;  // 记录上一帧的血量  
    private Coroutine _fadeCoroutine;  // 用于管理淡出动画  
    void Start()
    {
        // 初始化血条显示（此时只显示白色血条）  
        InitializeHealthBar();

        // [新增]订阅 TeamId 分配事件，当收到 TeamId 时重新初始化  
        if (PicoWebSocketClient.Instance != null)
        {
            // 你需要在 WebSocketClient 中添加这个事件  
            PicoWebSocketClient.Instance.OnTeamIdAssigned += OnTeamIdAssigned;
            PicoWebSocketClient.Instance.OnPlayersBloodReceived += OnPlayersBloodReceived;
        }
        if (deathUIPanel != null)
        {
            deathUIPanel.SetActive(false);
        }
    }

    // [新增方法]当服务器分配 TeamId 时调用  
    private void OnTeamIdAssigned(string teamId)
    {
        _currentTeamId = teamId;
        //Debug.Log($"[HealthBar] 收到 TeamId: {teamId}，激活队伍血条");
        InitializeHealthBar();
    }

    void OnDestroy()
    {
        // 取消订阅  
        if (PicoWebSocketClient.Instance != null)
        {
            PicoWebSocketClient.Instance.OnTeamIdAssigned -= OnTeamIdAssigned;
            PicoWebSocketClient.Instance.OnPlayersBloodReceived -= OnPlayersBloodReceived;
        }
    }

    /// <summary>  
    /// 初始化血条显示  
    /// 一开始只显示白色血条，等收到 TeamId 后再显示对应颜色血条  
    /// </summary>  
    private void InitializeHealthBar()
    {
        // 获取当前玩家的 TeamId  
        _currentTeamId = PicoWebSocketClient.Instance.TeamId;
        _previousHealth = _maxHealth;  // [新增]初始化上一帧血量 

        // [关键]一开始隐藏所有队伍血条  
        if (redHeartContainer != null)
            redHeartContainer.SetActive(false);

        if (blueHeartContainer != null)
            blueHeartContainer.SetActive(false);

        // 白色血条始终显示  
        ShowWhiteHealthBar();

        if (string.IsNullOrEmpty(_currentTeamId))
        {
            //Debug.LogWarning("[HealthBar] TeamId 未设置，暂不显示队伍血条，只显示白色血条");
            return;  // 空 TeamId 时直接返回，不激活任何颜色血条  
        }

        // 根据 TeamId 判断并激活对应队伍血条  
        if (_currentTeamId.Contains("Red") || _currentTeamId.ToLower().Contains("red"))
        {
            ShowRedTeamHealthBar();
            //Debug.Log($"[HealthBar] 显示红队血条，TeamId: {_currentTeamId}");
        }
        else if (_currentTeamId.Contains("Blue") || _currentTeamId.ToLower().Contains("blue"))
        {
            ShowBlueTeamHealthBar();
            //Debug.Log($"[HealthBar] 显示蓝队血条，TeamId: {_currentTeamId}");
        }
        else
        {
            //Debug.LogWarning($"[HealthBar] 未知的队伍: {_currentTeamId}");
        }
    }

    /// <summary>
    /// 显示红队血条（隐藏蓝队血条）
    /// 白色血条保持显示
    /// </summary>
    private void ShowRedTeamHealthBar()
    {
        // 显示红色血条
        if (redHeartContainer != null)
            redHeartContainer.SetActive(true);

        // 隐藏蓝色血条
        if (blueHeartContainer != null)
            blueHeartContainer.SetActive(false);

        Debug.Log("[HealthBar] 红队血条已激活");
    }

    /// <summary>
    /// 显示蓝队血条（隐藏红队血条）
    /// 白色血条保持显示
    /// </summary>
    private void ShowBlueTeamHealthBar()
    {
        // 隐藏红色血条
        if (redHeartContainer != null)
            redHeartContainer.SetActive(false);

        // 显示蓝色血条
        if (blueHeartContainer != null)
            blueHeartContainer.SetActive(true);

        Debug.Log("[HealthBar] 蓝队血条已激活");
    }

    /// <summary>
    /// 显示白色血条（损失血量）
    /// 白色血条始终显示
    /// </summary>
    private void ShowWhiteHealthBar()
    {
        if (whiteHeartContainer != null)
            whiteHeartContainer.SetActive(true);

        //Debug.Log("[HealthBar] 白色血条已激活（始终显示）");
    }
    /*
    public void UpdateHealth(int currentHealth, int maxHealth = 6)
    {
        _maxHealth = maxHealth;
        if (redHeartContainer != null && !redHeartContainer.activeSelf &&
            blueHeartContainer != null && !blueHeartContainer.activeSelf)
        {
            Debug.LogWarning("[HealthBar] 队伍血条都未激活，尝试重新初始化");
            InitializeHealthBar();
        }

        // 获取当前应该显示的心形数组（红或蓝）  
        Image[] activeTeamHearts = GetActiveTeamHearts();

        if (activeTeamHearts == null)
        {
            Debug.LogWarning("[HealthBar] 未找到对应的心形数组");
            return;
        }

        // 只更新红/蓝心形，白色心形保持不动（始终显示完整的最大血量）  
        for (int i = 0; i < activeTeamHearts.Length; i++)
        {
            activeTeamHearts[i].enabled = (i < currentHealth);
        }

        Debug.Log($"[HealthBar] 血量更新 - 当前: {currentHealth}, 最大: {maxHealth}, 损失: {maxHealth - currentHealth}");
    }*/

    /// <summary>  
    /// 更新血条UI显示  
    /// currentHealth: 当前血量（1-6）  
    /// maxHealth: 最大血量（默认6）  
    ///   
    /// 显示规则：  
    /// - 彩色heart显示当前血量个数（其他灭掉）  
    /// - 白色heart始终显示完整6个（表示损失的血量用白色填充）  
    /// </summary>  
    /// 
    /*
    private void UpdateHealthDisplay(int currentHealth, int maxHealth)
    {
        _currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
        _maxHealth = maxHealth;

        // 确保队伍血条已激活  
        if (redHeartContainer != null && !redHeartContainer.activeSelf &&
            blueHeartContainer != null && !blueHeartContainer.activeSelf)
        {
            Debug.LogWarning("[HealthBar] 队伍血条都未激活，尝试重新初始化");
            InitializeHealthBar();
        }

        // 获取当前应该显示的彩色heart数组（红或蓝）  
        Image[] activeTeamHearts = GetActiveTeamHearts();

        if (activeTeamHearts == null)
        {
            Debug.LogWarning("[HealthBar] 未找到对应的彩色heart数组");
            return;
        }

        // ═══ 更新彩色heart（当前血量）═══  
        // 规则：前 currentHealth 个显示，后面的关闭  
        for (int i = 0; i < activeTeamHearts.Length; i++)
        {
            Debug.Log("第"+i+"个血条显示！");
            activeTeamHearts[i].enabled = (i < _currentHealth);
        }

        // ═══ 白色heart始终显示完整6个（损失血量用白色表示）═══  
        // 因为白色heart是在彩色heart下方的背景层，所以始终显示所有  
        for (int i = 0; i < whiteHearts.Length; i++)
        {
            whiteHearts[i].enabled = true;
        }

        int lostHealth = _maxHealth - _currentHealth;
        Debug.Log($"[HealthBar] 血条更新 - 当前: {_currentHealth}/{_maxHealth}, 损失: {lostHealth}");

        // 血量为0时显示死亡UI，否则隐藏  
        if (deathUIPanel != null)
        {
            if (_currentHealth <= 0)
            {
                deathUIPanel.SetActive(true);
                Debug.Log("[HealthBar] 血量为0，显示死亡UI");

                if (picoBombPlacement != null)
                {
                    picoBombPlacement.enabled = false;
                    Debug.Log("[HealthBar] 已禁用炸弹放置功能");
                }
            }
            else
            {
                deathUIPanel.SetActive(false);

                if (picoBombPlacement != null)
                {
                    picoBombPlacement.enabled = true;
                    Debug.Log("[HealthBar] 已启用炸弹放置功能");
                }
            }
        }
    }*/
    private void UpdateHealthDisplay(int currentHealth, int maxHealth)
    {
        _currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
        _maxHealth = maxHealth;

        if (_previousHealth > _currentHealth)
        {
            ShowDamageImage();  // 血量减少，显示受击图片  
        }
        else if (_previousHealth < _currentHealth)
        {
            ShowRecoverImage();  // 血量增加，显示恢复图片  
        }
        _previousHealth = _currentHealth;  // 更新上一帧血量  

        // 确保队伍血条已激活  
        if (redHeartContainer != null && !redHeartContainer.activeSelf &&
            blueHeartContainer != null && !blueHeartContainer.activeSelf)
        {
            //Debug.LogWarning("[HealthBar] 队伍血条都未激活，尝试重新初始化");
            InitializeHealthBar();
        }

        // 获取当前应该显示的彩色heart数组（红或蓝）  
        Image[] activeTeamHearts = GetActiveTeamHearts();

        if (activeTeamHearts == null)
        {
            //Debug.LogWarning("[HealthBar] 未找到对应的彩色heart数组");
            return;
        }

        // ===== 核心改动：根据 maxHealth 动态显示 =====  
        // 1. 先隐藏所有超过 maxHealth 的彩色heart（从右边开始）  
        for (int i = maxHealth; i < activeTeamHearts.Length; i++)
        {
            activeTeamHearts[i].enabled = false;
        }

        // 2. 在 maxHealth 范围内，根据当前血量显示/隐藏彩色heart  
        for (int i = 0; i < maxHealth; i++)
        {
            activeTeamHearts[i].enabled = (i < _currentHealth);
        }

        // 3. 白色heart的显示规则：仅显示前 maxHealth 颗（其余隐藏）  
        for (int i = 0; i < whiteHearts.Length; i++)
        {
            whiteHearts[i].enabled = (i < maxHealth);
        }

        int lostHealth = _maxHealth - _currentHealth;
        //Debug.Log($"[HealthBar] 血条更新 - 当前: {_currentHealth}/{_maxHealth}, 损失: {lostHealth}");

        // 血量为0时显示死亡UI...  
        if (deathUIPanel != null)
        {
            if (_currentHealth <= 0)
            {
                deathUIPanel.SetActive(true);
                if (picoBombPlacement != null)
                {
                    picoBombPlacement.enabled = false;
                }
            }
            else
            {
                deathUIPanel.SetActive(false);
                if (picoBombPlacement != null)
                {
                    picoBombPlacement.enabled = true;
                }
            }
        }
    }

    /// <summary>
    /// 获取当前应该显示的队伍心形数组（红或蓝）
    /// </summary>
    private Image[] GetActiveTeamHearts()
    {
        if (redHeartContainer != null && redHeartContainer.activeSelf)
        {
            return redHearts;
        }
        else if (blueHeartContainer != null && blueHeartContainer.activeSelf)
        {
            return blueHearts;
        }

        //Debug.LogWarning("[HealthBar] 没有激活的队伍血条");
        return null;
    }

    /// <summary>  
    /// 检查两个队伍ID是否匹配（容错处理）  
    /// </summary>  
    private bool IsTeamMatch(string teamId1, string teamId2)
    {
        if (string.IsNullOrEmpty(teamId1) || string.IsNullOrEmpty(teamId2))
            return false;

        return teamId1 == teamId2 ||
               teamId1.Contains(teamId2) ||
               teamId2.Contains(teamId1);
    }


    /// <summary>  
    /// 新增方法：当收到玩家血量广播时调用  
    /// </summary>  
    private void OnPlayersBloodReceived(PlayersBloodMsg bloodMsg)
    {
        if (bloodMsg?.teams == null || bloodMsg.teams.Length == 0)
        {
            //Debug.LogWarning("[HealthBar] 收到的血量消息为空");
            return;
        }

        // 获取当前玩家的PlayerId和TeamId  
        string currentPlayerId = PicoWebSocketClient.Instance.PlayerId;
        string currentTeamId = PicoWebSocketClient.Instance.TeamId;

        if (string.IsNullOrEmpty(currentPlayerId) || string.IsNullOrEmpty(currentTeamId))
        {
            //Debug.LogWarning("[HealthBar] PlayerId 或 TeamId 未设置");
            return;
        }

        // 在血量消息中查找当前玩家的信息  
        foreach (var teamInfo in bloodMsg.teams)
        {
            // 检查是否是当前玩家所在的队伍  
            if (IsTeamMatch(teamInfo.teamId, currentTeamId))
            {
                // 在队伍的玩家列表中查找当前玩家  
                foreach (var playerBlood in teamInfo.players)
                {
                    if (playerBlood.playerId == currentPlayerId)
                    {
                        // 找到当前玩家，更新血条UI  
                        UpdateHealthDisplay(playerBlood.blood, playerBlood.maxBlood);
                        //Debug.Log($"[HealthBar] 更新当前玩家血量: {playerBlood.blood}/{playerBlood.maxBlood}");
                        return;
                    }
                }
            }
        }

        //Debug.LogWarning($"[HealthBar] 未在血量消息中找到当前玩家 (PlayerId: {currentPlayerId})");
    }

    /// <summary>  
    /// 显示受击图片并淡出  
    /// </summary>  
    private void ShowDamageImage()
    {
        if (damageImageUI == null) return;

        // 停止之前的淡出动画  
        if (_fadeCoroutine != null)
        {
            StopCoroutine(_fadeCoroutine);
        }

        // 立即显示图片  
        damageImageUI.gameObject.SetActive(true);
        Color color = damageImageUI.color;
        color.a = 1f;
        damageImageUI.color = color;

        // 启动淡出动画  
        _fadeCoroutine = StartCoroutine(FadeOutImage(damageImageUI));
        //Debug.Log("[HealthBar] 显示受击图片");
    }

    /// <summary>  
    /// 显示恢复图片并淡出  
    /// </summary>  
    private void ShowRecoverImage()
    {
        if (recoverImageUI == null) return;

        // 停止之前的淡出动画  
        if (_fadeCoroutine != null)
        {
            StopCoroutine(_fadeCoroutine);
        }

        // 立即显示图片  
        recoverImageUI.gameObject.SetActive(true);
        Color color = recoverImageUI.color;
        color.a = 1f;
        recoverImageUI.color = color;

        // 启动淡出动画  
        _fadeCoroutine = StartCoroutine(FadeOutImage(recoverImageUI));
        //Debug.Log("[HealthBar] 显示恢复图片");
    }

    /// <summary>  
    /// 图片淡出动画  
    /// </summary>  
    private IEnumerator FadeOutImage(Image image)
    {
        float elapsedTime = 0f;

        while (elapsedTime < imageFadeDuration)
        {
            elapsedTime += Time.deltaTime;
            Color color = image.color;
            color.a = Mathf.Lerp(1f, 0f, elapsedTime / imageFadeDuration);
            image.color = color;
            yield return null;
        }

        // 动画结束，隐藏图片  
        image.gameObject.SetActive(false);
    }

}
