using BubbleBattle.Network;
using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 炸弹数量UI管理器
/// 负责显示玩家剩余可放置的炸弹数量（0/1/2）
/// 监听放置事件和销毁事件来更新计数
/// </summary>
public class BombUIManager : MonoBehaviour
{
    [Header("炸弹放置脚本引用")]
    [SerializeField] private PicoBombPlacement bombPlacement;

    [Header("炸弹状态管理器引用")]
    [SerializeField] private PicoBombStateManager bombStateManager;

    [Header("UI预制体")]
    [SerializeField] private GameObject bombCountUI_2;  // 2个炸弹UI
    [SerializeField] private GameObject bombCountUI_1;  // 1个炸弹UI
    [SerializeField] private GameObject bombCountUI_0;  // 0个炸弹UI

    // 内部状态
    private int currentBombCount = 2;  // 当前剩余炸弹数
    private const int MAX_BOMB_COUNT = 2;  // 最大炸弹数

    // 追踪所有已放置的炸弹ID（用于统计还有多少炸弹未销毁）
    private HashSet<string> activeBombIds = new HashSet<string>();

    void Start()
    {
        // 自动查找脚本（如果Inspector中没有手动赋值）
        if (bombPlacement == null)
        {
            bombPlacement = FindObjectOfType<PicoBombPlacement>();
        }
        if (bombStateManager == null)
        {
            bombStateManager = FindObjectOfType<PicoBombStateManager>();
        }

        if (bombPlacement == null)
        {
            Debug.LogError("[BombUIManager] 未找到 PicoBombPlacement 脚本！");
            return;
        }
        if (bombStateManager == null)
        {
            Debug.LogError("[BombUIManager] 未找到 PicoBombStateManager 脚本！");
            return;
        }

        // 订阅炸弹放置事件
        bombPlacement.OnBombPlaced += HandleBombPlaced;

        // 订阅炸弹销毁事件
        bombStateManager.OnBombDestroyedFromServer += HandleBombDestroyed;

        // 初始显示UI（满弹夹状态）
        UpdateUIDisplay(MAX_BOMB_COUNT);

        Debug.Log("[BombUIManager] 初始化完成");
    }

    /// <summary>
    /// 炸弹放置时调用
    /// </summary>
    private void HandleBombPlaced()
    {
        currentBombCount = Mathf.Max(0, currentBombCount - 1);
        Debug.Log($"[BombUIManager] 炸弹已放置，剩余可放置炸弹数: {currentBombCount}");
        UpdateUIDisplay(currentBombCount);
    }

    /// <summary>
    /// 炸弹销毁时调用（从服务端广播收到销毁消息）
    /// </summary>
    /*
    private void HandleBombDestroyed(string bombId)
    {
        // ͬ��ֱ�Ӵ� PicoBombPlacement ��ȡ  
        if (bombPlacement != null && activeBombIds.Remove(bombId))
        {
            int count = bombPlacement.CurrentBombCount;
            count = Mathf.Min(MAX_BOMB_COUNT, count);

            Debug.Log($"[BombUIManager] ը�� {bombId} �����٣�ʣ��ɷ�����: {count}");
            UpdateUIDisplay(count);
        }
    }*/
    /// <summary>
    /// 炸弹销毁时调用（从服务端广播收到销毁消息）
    /// </summary>
    private void HandleBombDestroyed(string bombId)
    {
        if (activeBombIds.Remove(bombId))
        {
            // 增加炸弹数量
            currentBombCount = Mathf.Min(MAX_BOMB_COUNT, currentBombCount + 1);

            Debug.Log($"[BombUIManager] 炸弹 {bombId} 爆炸销毁，剩余可放置炸弹数: {currentBombCount}");
            UpdateUIDisplay(currentBombCount);
        }
    }


    /// <summary>
    /// 玩家放置炸弹时，记录炸弹ID
    /// 需要在PicoBombPlacement中调用这个方法
    /// </summary>
    public void RegisterNewBomb(string bombId)
    {
        activeBombIds.Add(bombId);
        Debug.Log($"[BombUIManager] 注册新炸弹: {bombId}，当前活跃炸弹数: {activeBombIds.Count}");
    }

    /// <summary>
    /// 更新UI显示
    /// 根据炸弹数量显示对应的UI预制体
    /// </summary>
    private void UpdateUIDisplay(int bombCount)
    {
        // 隐藏所有UI
        if (bombCountUI_2 != null)
            bombCountUI_2.SetActive(false);
        if (bombCountUI_1 != null)
            bombCountUI_1.SetActive(false);
        if (bombCountUI_0 != null)
            bombCountUI_0.SetActive(false);

        // 根据数量显示对应UI
        switch (bombCount)
        {
            case 2:
                if (bombCountUI_2 != null)
                {
                    bombCountUI_2.SetActive(true);
                    Debug.Log("[BombUIManager] 显示2个炸弹UI");
                }
                break;
            case 1:
                if (bombCountUI_1 != null)
                {
                    bombCountUI_1.SetActive(true);
                    Debug.Log("[BombUIManager] 显示1个炸弹UI");
                }
                break;
            case 0:
                if (bombCountUI_0 != null)
                {
                    bombCountUI_0.SetActive(true);
                    Debug.Log("[BombUIManager] 显示0个炸弹UI");
                }
                break;
            default:
                Debug.LogWarning($"[BombUIManager] 无效的炸弹数量: {bombCount}");
                break;
        }
    }

    /// <summary>
    /// 将炸弹储备补满至最大值
    /// 用于单局结束时的炸弹刷新
    /// </summary>
    public void ResetBombCount()
    {
        activeBombIds.Clear();
        currentBombCount = MAX_BOMB_COUNT;
        UpdateUIDisplay(currentBombCount);
        Debug.Log("[BombUIManager] 炸弹储备已补满");
    }



    /// <summary>
    /// 获取当前剩余炸弹数（供外部查询）
    /// </summary>
    public int GetCurrentBombCount()
    {
        return currentBombCount;
    }

    /// <summary>
    /// 检查是否可以放置炸弹
    /// </summary>
    public bool CanPlaceBomb()
    {
        return currentBombCount > 0;
    }

    void OnDestroy()
    {
        if (bombPlacement != null)
        {
            bombPlacement.OnBombPlaced -= HandleBombPlaced;
        }
        if (bombStateManager != null)
        {
            bombStateManager.OnBombDestroyedFromServer -= HandleBombDestroyed;
        }
    }
}
