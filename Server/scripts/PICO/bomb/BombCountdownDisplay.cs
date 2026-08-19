using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 炸弹倒计时显示脚本
/// 挂在 Canvas 或 UI 容器上，自动管理倒计时显示
/// </summary>
public class BombCountdownDisplay : MonoBehaviour
{
    private TextMeshProUGUI _countdownText;  // 倒计时文本
    private float _remainingTime = 0f;       // 剩余时间
    private bool _isActive = false;          // 是否激活计时

    [SerializeField] private Color normalColor = Color.black;      // 正常颜色
    [SerializeField] private Color warningColor = Color.red;       // 警告颜色（最后1秒）
    [SerializeField] private float warningThreshold = 1f;          // 警告阈值（秒）

    void Start()
    {
        // 尝试获取 TextMeshProUGUI 组件
        _countdownText = GetComponentInChildren<TextMeshProUGUI>();
        if (_countdownText == null)
        {
            // 兼容旧的 Text 组件
            var legacyText = GetComponentInChildren<Text>();
            if (legacyText != null)
            {
                //Debug.LogWarning("[BombCountdown] 使用旧的 Text 组件，建议升级为 TextMeshProUGUI");
            }
        }

        if (_countdownText == null)
        {
            //Debug.LogError("[BombCountdown] 未找到 Text/TextMeshProUGUI 组件！");
            enabled = false;
        }
    }

    void Update()
    {
        if (!_isActive || _countdownText == null) return;

        // 倒计时递减
        _remainingTime -= Time.deltaTime;
        _remainingTime = Mathf.Max(0, _remainingTime);

        // 更新显示
        UpdateDisplay();

        // 倒计时结束
        if (_remainingTime <= 0)
        {
            _isActive = false;
            _countdownText.text = "Bomb!";
        }
    }

    /// <summary>
    /// 更新倒计时显示
    /// 显示整数秒数：1秒、2秒、3秒...
    /// </summary>
    private void UpdateDisplay()
    {
        //显示格式为整数（向上取整）
        _countdownText.text = Mathf.CeilToInt(_remainingTime).ToString();

        // 最后1秒变红
        if (_remainingTime <= warningThreshold)
        {
            _countdownText.color = warningColor;
        }
        else
        {
            _countdownText.color = normalColor;
        }
    }

    /// <summary>
    /// 外部调用：设置炸弹倒计时
    /// 每次收到服务端更新时调用一次
    /// </summary>
    public void SetCountdown(float remainingTime)
    {
        _remainingTime = Mathf.Max(0, remainingTime);
        _isActive = true;

        //Debug.Log($"[BombCountdown] 更新倒计时: {_remainingTime:F1}秒");
    }

    /// <summary>
    /// 重置倒计时
    /// </summary>
    public void ResetCountdown()
    {
        _isActive = false;
        _remainingTime = 0;
        if (_countdownText != null)
        {
            _countdownText.text = "0";
            _countdownText.color = normalColor;
        }
    }

    /// <summary>
    /// 获取是否正在计时
    /// </summary>
    public bool IsActive => _isActive;
}
