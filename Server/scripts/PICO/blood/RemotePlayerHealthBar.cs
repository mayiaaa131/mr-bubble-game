using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class RemotePlayerHealthBar : MonoBehaviour
{
    private string _playerId;
    private string _teamId;
    //private TextMeshProUGUI _healthText;

    // 白色心（始终显示最大血量）
    [SerializeField] private Image[] _whiteHearts;

    // 彩色心（根据队伍显示）
    [SerializeField] private Image[] _redHearts;
    [SerializeField] private Image[] _blueHearts;

    [SerializeField] private int maxHealth = 6;

    // [新增]容器Transform - 在编辑器中拖配置  
    [SerializeField] private Transform redContainer;
    [SerializeField] private Transform blueContainer;

    // 队伍颜色配置
    [SerializeField] private Color redTeamColor = new Color(1f, 0f, 0f, 1f);      // 红色
    [SerializeField] private Color blueTeamColor = new Color(0f, 0.5f, 1f, 1f);   // 蓝色

    void Start()
    {

    }

    /// <summary>
    /// 初始化血条（绑定到指定玩家）
    /// </summary>
    public void Initialize(string playerId, int currentHealth, string teamId)
    {
        _playerId = playerId;
        _teamId = teamId;

        Debug.Log($"[RemoteHealthBar] 初始化玩家 {playerId}, 队伍: {teamId}");

        // 根据队伍激活对应的容器
        ActivateTeamHearts();

        // 更新血量显示
        UpdateHealth(currentHealth, maxHealth);
    }

    /// <summary>  
    /// 根据队伍ID激活对应颜色的心  
    /// </summary>  
    private void ActivateTeamHearts()
    {
        if (string.IsNullOrEmpty(_teamId))
        {
            Debug.LogWarning($"[RemoteHealthBar] 玩家 {_playerId} 的 TeamId 为空");
            return;
        }

        Debug.Log($"[RemoteHealthBar] 玩家 {_playerId} - 原始 TeamId: '{_teamId}'");

        // 隐藏所有心容器  
        if (redContainer != null)
            redContainer.gameObject.SetActive(false);
        if (blueContainer != null)
            blueContainer.gameObject.SetActive(false);

        // 根据队伍激活对应容器  
        if (_teamId.ToLower().Contains("red"))
        {
            if (redContainer != null)
            {
                redContainer.gameObject.SetActive(true);
                Debug.Log($"[RemoteHealthBar] 玩家 {_playerId} 激活红队心");
            }
            else
            {
                Debug.LogError($"[RemoteHealthBar] 玩家 {_playerId} 的 redContainer 为 null！请在编辑器中配置");
            }
        }
        else if (_teamId.ToLower().Contains("blue"))
        {
            if (blueContainer != null)
            {
                blueContainer.gameObject.SetActive(true);
                Debug.Log($"[RemoteHealthBar] 玩家 {_playerId} 激活蓝队心");
            }
            else
            {
                Debug.LogError($"[RemoteHealthBar] 玩家 {_playerId} 的 blueContainer 为 null！请在编辑器中配置");
            }
        }
        else
        {
            Debug.LogError($"[RemoteHealthBar] 玩家 {_playerId} TeamId '{_teamId}' 既不包含'red'也不包含'blue'！");
        }
    }

    /// <summary>
    /// 更新血量显示
    /// 规则：
    /// - 白色心始终显示所有6个（表示最大血量）
    /// - 彩色心根据当前血量显示（从右边开始隐藏）
    /// 例如：血量5/6，显示5个红/蓝心，隐藏最右边的第6个，露出白色的第6个
    /// </summary>
    /// 
    /*
    public void UpdateHealth(int currentHealth, int maxHealth = 6)
    {
        if (string.IsNullOrEmpty(_playerId))
            return;

        maxHealth = Mathf.Max(1, maxHealth);
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);

        // 获取当前激活的彩色心数组（红或蓝）
        Image[] activeColorHearts = GetActiveColorHearts();

        if (activeColorHearts != null && activeColorHearts.Length > 0)
        {
            // 关键逻辑：根据当前血量显示/隐藏彩色心
            // 前 currentHealth 个显示，后面的隐藏（露出白色的）
            for (int i = 0; i < activeColorHearts.Length; i++)
            {
                activeColorHearts[i].enabled = (i < currentHealth);
            }

            Debug.Log($"[RemoteHealthBar] 玩家 {_playerId} 血量: {currentHealth}/{maxHealth}，显示 {currentHealth} 颗彩色心");
        }

        // 白色心始终显示所有6个（作为背景/损失血量的表示）
        if (_whiteHearts != null && _whiteHearts.Length > 0)
        {
            for (int i = 0; i < _whiteHearts.Length; i++)
            {
                _whiteHearts[i].enabled = true;
            }
        }
    }*/
    public void UpdateHealth(int currentHealth, int maxHealth = 6)
    {
        if (string.IsNullOrEmpty(_playerId))
            return;

        maxHealth = Mathf.Max(1, maxHealth);
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);

        // 获取当前激活的彩色心数组（红或蓝）  
        Image[] activeColorHearts = GetActiveColorHearts();

        if (activeColorHearts != null && activeColorHearts.Length > 0)
        {
            // ===== 核心改动：根据 maxHealth 动态显示 =====  
            // 1. 先隐藏所有超过 maxHealth 的彩色heart（从右边开始）  
            for (int i = maxHealth; i < activeColorHearts.Length; i++)
            {
                activeColorHearts[i].enabled = false;
            }

            // 2. 在 maxHealth 范围内，根据当前血量显示/隐藏彩色heart  
            for (int i = 0; i < maxHealth; i++)
            {
                activeColorHearts[i].enabled = (i < currentHealth);
            }

            Debug.Log($"[RemoteHealthBar] 玩家 {_playerId} 血量: {currentHealth}/{maxHealth}，显示 {currentHealth} 颗彩色心");
        }

        // 白色心只显示前 maxHealth 颗（其余隐藏）  
        if (_whiteHearts != null && _whiteHearts.Length > 0)
        {
            for (int i = 0; i < _whiteHearts.Length; i++)
            {
                _whiteHearts[i].enabled = (i < maxHealth);
            }
        }
    }

    /// <summary>
    /// 获取当前激活的彩色心数组（红或蓝）
    /// </summary>
    private Image[] GetActiveColorHearts()
    {

        if (redContainer != null && redContainer.gameObject.activeSelf)
        {
            return _redHearts;
        }
        else if (blueContainer != null && blueContainer.gameObject.activeSelf)
        {
            return _blueHearts;
        }

        return null;
    }

    public string GetPlayerId() => _playerId;
    public string GetTeamId() => _teamId;
}
