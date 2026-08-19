using UnityEngine;

/// <summary>
/// 玩家血量管理系统
/// 可以被炸弹、敌人等伤害来源调用
/// </summary>
public class PlayerHealthSystem : MonoBehaviour
{
    [Header("血量配置")]
    public float maxHealth = 100f;

    private float _currentHealth;

    void Start()
    {
        _currentHealth = maxHealth;
        Debug.Log($"[Player] 初始血量：{_currentHealth}");
    }

    /// <summary>
    /// 玩家受伤
    /// </summary>
    public void TakeDamage(float damageAmount)
    {
        _currentHealth -= damageAmount;
        _currentHealth = Mathf.Clamp(_currentHealth, 0, maxHealth);

        Debug.Log($"[Player] 受伤 -{damageAmount}，当前血量：{_currentHealth}/{maxHealth}");

        // 如果血量为0 → 死亡
        if (_currentHealth <= 0)
        {
            OnPlayerDeath();
        }
    }

    /// <summary>
    /// 玩家回血
    /// </summary>
    public void Heal(float healAmount)
    {
        _currentHealth += healAmount;
        _currentHealth = Mathf.Clamp(_currentHealth, 0, maxHealth);
        Debug.Log($"[Player] 回血 +{healAmount}，当前血量：{_currentHealth}/{maxHealth}");
    }

    /// <summary>
    /// 玩家死亡
    /// </summary>
    void OnPlayerDeath()
    {
        Debug.Log("[Player] 玩家已死亡！");
        // TODO: 这里可以调用游戏结束、重启等逻辑
        // GameManager.Instance.OnPlayerDeath();
    }

    // ── 公开接口 ────────────────────────────────────

    public float GetCurrentHealth() => _currentHealth;
    public float GetMaxHealth() => maxHealth;
    public float GetHealthPercent() => _currentHealth / maxHealth;
}
