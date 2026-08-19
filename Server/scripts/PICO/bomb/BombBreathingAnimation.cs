using UnityEngine;

public class BombBreathingAnimation : MonoBehaviour
{
    [Header("呼吸动画配置")]
    [SerializeField] private float breathAmplitude = 0.08f;
    [SerializeField] private float breathSpeed = 1.2f;
    [SerializeField] private float speedSmoothTime = 0.5f;

    [Header("排除缩放的子物体（如倒计时Canvas）")]
    [SerializeField] private Transform[ ] excludedChildren;

    private Vector3 _originalScale;
    private bool _isAnimating = true;

    private float _currentSpeed;
    private float _targetSpeed;
    private float _speedVelocity = 0f;
    private float _phase = 0f;

    private Vector3[ ] _excludedOriginalScales;

    void Start( )
    {
        _originalScale = transform.localScale;
        _currentSpeed = breathSpeed;
        _targetSpeed = breathSpeed;

        CacheExcludedScales();
    }

    void Update( )
    {
        if (!_isAnimating) return;

        _currentSpeed = Mathf.SmoothDamp(
            _currentSpeed,
            _targetSpeed,
            ref _speedVelocity,
            speedSmoothTime
        );

        _phase += _currentSpeed * Time.deltaTime;
        if (_phase > 1f) _phase -= 1f;

        float pingPong = Mathf.PingPong(_phase * 2f, 1f);
        float smoothValue = Mathf.SmoothStep(0f, 1f, pingPong);
        float scaleFactor = 1f + (smoothValue * 2f - 1f) * breathAmplitude;

        transform.localScale = _originalScale * scaleFactor;

        // 逆向补偿，Canvas 不跟随缩放
        if (excludedChildren != null)
        {
            for (int i = 0; i < excludedChildren.Length; i++)
            {
                if (excludedChildren[ i ] != null && scaleFactor != 0f)
                {
                    excludedChildren[ i ].localScale = _excludedOriginalScales[ i ] / scaleFactor;
                }
            }
        }
    }

    public void SetBreathSpeed( float speed )
    {
        _targetSpeed = speed;
    }

    /// <summary>
    /// ★ 新增：外部更新基准缩放（炸弹升级时调用）
    /// 平滑过渡到新的基准大小，不会产生跳变
    /// </summary>
    public void SetBaseScale( Vector3 newBaseScale )
    {
        _originalScale = newBaseScale;
    }

    public void SetExcludedChildren( Transform[ ] children )
    {
        excludedChildren = children;
        CacheExcludedScales();
    }

    public void StopAnimation( )
    {
        _isAnimating = false;
        transform.localScale = _originalScale;

        if (excludedChildren != null)
        {
            for (int i = 0; i < excludedChildren.Length; i++)
            {
                if (excludedChildren[ i ] != null)
                    excludedChildren[ i ].localScale = _excludedOriginalScales[ i ];
            }
        }
    }

    private void CacheExcludedScales( )
    {
        if (excludedChildren == null) return;
        _excludedOriginalScales = new Vector3[ excludedChildren.Length ];
        for (int i = 0; i < excludedChildren.Length; i++)
        {
            if (excludedChildren[ i ] != null)
                _excludedOriginalScales[ i ] = excludedChildren[ i ].localScale;
        }
    }
}
