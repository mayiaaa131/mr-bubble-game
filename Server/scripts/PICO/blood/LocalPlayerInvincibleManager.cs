using BubbleBattle.Network;
using UnityEngine;

/// <summary>
/// 本地玩家无敌状态管理（显示无敌特效）
/// 服务端发送的是实时倒计时，客户端只需要显示/隐藏特效
/// </summary>
public class LocalPlayerInvincibleManager : MonoBehaviour
{
    [SerializeField] private GameObject invincibleEffectPrefab;  // 无敌特效预制体
    private GameObject invincibleEffect;
    private Transform localPlayerTransform;
    private string localPlayerId;
    private InvincibleStateInfo currentInvincibleState;

    void Start()
    {
        // 获取本地玩家Transform
        if (localPlayerTransform == null)
        {
            AutoBindXRCamera();
        }

        // 监听WebSocket事件
        if (PicoWebSocketClient.Instance != null)
        {
            PicoWebSocketClient.Instance.OnPlayerAssignedId += OnLocalPlayerIdAssigned;
            PicoWebSocketClient.Instance.OnInvincibleStateReceived += HandleInvincibleStateUpdate;
        }
        else
        {
            Debug.LogError("[LocalPlayerInvincibleManager] PicoWebSocketClient.Instance 为空！");
        }
    }

    private void AutoBindXRCamera()
    {
        var xrOrigin = FindObjectOfType<Unity.XR.CoreUtils.XROrigin>();
        if (xrOrigin != null && xrOrigin.Camera != null)
        {
            localPlayerTransform = xrOrigin.Camera.transform;
            return;
        }

        if (Camera.main != null)
        {
            localPlayerTransform = Camera.main.transform;
            return;
        }

        Debug.LogError("[LocalPlayerInvincibleManager] 未找到可用 Camera！");
    }

    private void OnLocalPlayerIdAssigned(string playerId)
    {
        localPlayerId = playerId;
        Debug.Log($"[LocalPlayerInvincibleManager] 本地玩家ID: {localPlayerId}");
    }

    void Update()
    {
        // 只需要实时更新特效位置和状态，不需要计算倒计时
        if (invincibleEffect != null && invincibleEffect.activeSelf && currentInvincibleState != null)
        {
            invincibleEffect.transform.position = localPlayerTransform.position;
            UpdateInvincibleEffectAnimation(currentInvincibleState.invincibleCountdown);
        }
    }

    private void HandleInvincibleStateUpdate(InvincibleStateMessage invincibleMsg)
    {
        if (invincibleMsg?.invincibleStates == null || invincibleMsg.invincibleStates.Length == 0)
        {
            return;
        }

        // 查找本地玩家的无敌状态
        foreach (var state in invincibleMsg.invincibleStates)
        {
            if (state.playerId == localPlayerId)
            {
                currentInvincibleState = state;

                // 直接使用服务端发来的倒计时数据
                if (state.isInvincible && state.invincibleCountdown > 0)
                {
                    ShowInvincibleEffect();
                }
                else
                {
                    HideInvincibleEffect();
                }
                break;
            }
        }
    }

    private void ShowInvincibleEffect()
    {
        if (invincibleEffect == null)
        {
            if (invincibleEffectPrefab != null)
            {
                invincibleEffect = Instantiate(
                    invincibleEffectPrefab,
                    localPlayerTransform.position,
                    Quaternion.identity,
                    localPlayerTransform
                );
                invincibleEffect.name = "LocalPlayerInvincibleEffect";
                invincibleEffect.transform.localPosition = Vector3.zero;
            }
            else
            {
                Debug.LogWarning("[LocalPlayerInvincibleManager] invincibleEffectPrefab 未指定！");
                return;
            }
        }
        else if (!invincibleEffect.activeSelf)
        {
            invincibleEffect.SetActive(true);
        }
    }

    private void HideInvincibleEffect()
    {
        if (invincibleEffect != null && invincibleEffect.activeSelf)
        {
            invincibleEffect.SetActive(false);
        }
        currentInvincibleState = null;
    }

    private void UpdateInvincibleEffectAnimation(float countdown)
    {
        // 倒计时最后2秒闪烁
        if (countdown <= 2f)
        {
            if (invincibleEffect != null && invincibleEffect.TryGetComponent<CanvasGroup>(out var canvasGroup))
            {
                float alpha = Mathf.Abs(Mathf.Sin(Time.time * 5f));
                canvasGroup.alpha = alpha;
            }

            if (invincibleEffect != null && invincibleEffect.TryGetComponent<Renderer>(out var renderer))
            {
                float alpha = Mathf.Abs(Mathf.Sin(Time.time * 5f));
                Color color = renderer.material.color;
                color.a = alpha;
                renderer.material.color = color;
            }
        }
    }

    void OnDestroy()
    {
        if (PicoWebSocketClient.Instance != null)
        {
            PicoWebSocketClient.Instance.OnPlayerAssignedId -= OnLocalPlayerIdAssigned;
            PicoWebSocketClient.Instance.OnInvincibleStateReceived -= HandleInvincibleStateUpdate;
        }

        if (invincibleEffect != null)
        {
            Destroy(invincibleEffect);
        }
    }
}
