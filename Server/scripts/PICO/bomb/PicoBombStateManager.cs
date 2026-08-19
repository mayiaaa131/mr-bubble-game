// ===== PicoBombStateManager.cs =====
using BubbleBattle.Network;
using UnityEngine;
using System.Collections.Generic;
using System;

/// <summary>
/// 处理来自服务端的炸弹状态同步
/// 支持远程炸弹的生成、更新和爆炸
/// </summary>
public class PicoBombStateManager : MonoBehaviour
{
    [Header("炸弹配置")]
    [SerializeField] private GameObject bombVisualPrefabRed;    // 红队炸弹视觉预制体  
    [SerializeField] private GameObject bombVisualPrefabBlue;   // 蓝队炸弹视觉预制体  
    [Header("消除特效（沉默道具）")]
    [SerializeField] private GameObject silenceRemoveEffectPrefab; // 消除特效预制体  
    [SerializeField] private float silenceEffectDuration = 2f;     // 特效自动销毁时长（秒）  
    private Dictionary<string, GameObject> localBombs = new Dictionary<string, GameObject>();
    private Dictionary<string, long> explosionTriggeredTimestamp = new Dictionary<string, long>();

    private int lastReceivedFrameNumber = 0;
    private Queue<BombStateBroadcast> bombStateHistory = new Queue<BombStateBroadcast>();
    private const int HISTORY_FRAME_COUNT = 5;
    // 缓存两队Prefab 的原始Scale，避免重复实例化读取  
    private Vector3 _prefabScaleRed = Vector3.zero;
    private Vector3 _prefabScaleBlue = Vector3.zero;

    //可视化
    [Header("爆炸范围可视化")]
    [SerializeField] private bool showExplosionRanges = true;  // 是否显示爆炸范围  

    [Header("红队爆炸范围颜色")]
    [SerializeField] private Color explosionRangeColorRed = new Color(1f, 0f, 0f, 0.3f);  // 红色半透明  
    [SerializeField] private Color explosionRangeOutlineColorRed = new Color(1f, 0f, 0f, 1f);  // 红色边框  

    [Header("蓝队爆炸范围颜色")]
    [SerializeField] private Color explosionRangeColorBlue = new Color(0f, 0.5f, 1f, 0.3f);  // 蓝色半透明  
    [SerializeField] private Color explosionRangeOutlineColorBlue = new Color(0f, 0.5f, 1f, 1f);  // 蓝色边框  

    private Dictionary<string, GameObject[]> explosionRangeVisuals = new Dictionary<string, GameObject[]>();

    public event Action<string> OnBombDestroyedFromServer;  // 炸弹被销毁事件


    [Header("爆炸音效")]
    [SerializeField] private AudioClip explosionSoundClip;  // 爆炸音效  
    [SerializeField] private float explosionSoundVolume = 1f;  // 音量大小（0-1）  
    [SerializeField] private float explosionSoundPitch = 1f;   // 音调（1为正常）  

    [Header("爆炸特效")]
    [SerializeField] private GameObject explosionEffectPrefab;  // 爆炸特效预制体  
    void Start()
    {
        if (PicoWebSocketClient.Instance != null)
        {
            PicoWebSocketClient.Instance.OnRemoteBombStateReceived += HandleBombStateReceived;
            PicoWebSocketClient.Instance.OnWorldStateReceived += HandleWorldStateReceived;
            PicoWebSocketClient.Instance.OnGameEndReceived += HandleGameEnd;
        }
    }

    /// <summary>
    /// 接收并处理炸弹状态消息[^23,^24]
    /// </summary>
    private void HandleBombStateReceived(BombStateBroadcast stateMsg)
    {

        Debug.Log($"[BombStateManager] 收到广播: 共{stateMsg?.bombs?.Length ?? -1}个炸弹, 帧号={stateMsg?.frameSequenceNumber}");
        if(stateMsg?.bombs != null)
    {
            foreach (var b in stateMsg.bombs)
            {
                Debug.Log($"[BombStateManager]   炸弹: id={b.bombId}, teamId='{b.teamId}', state='{b.state}', pos=({b.position?.x},{b.position?.y},{b.position?.z})");
            }
        }

        //  步骤1: 检测丢包（这是唯一能知道丢包的方式）  
        DetectMissingFrames(stateMsg.frameSequenceNumber);
        lastReceivedFrameNumber = stateMsg.frameSequenceNumber;

        //  步骤2: 处理炸弹状态  
        ProcessBombStates(stateMsg);

        //  步骤3: 清理已删除的炸弹  
        CleanupRemovedBombs(stateMsg);

        Debug.Log($" 处理完帧#{stateMsg.frameSequenceNumber}");
    }

    /// <summary>
    /// 检测丢包[^24]
    /// </summary>
    private void DetectMissingFrames(int currentFrameNumber)
    {
        if (lastReceivedFrameNumber == 0)
        {
            Debug.Log($"[BombStateManager] 首次接收，帧号={currentFrameNumber}");
            return;
        }

        int expectedNext = lastReceivedFrameNumber + 1;

        if (currentFrameNumber > expectedNext)
        {
            int missedFrames = currentFrameNumber - expectedNext;
            Debug.LogWarning($"检测到丢包！" +
                $"丢失{missedFrames}帧 " +
                $"(期望{expectedNext}, 实际{currentFrameNumber})");

            // 立即发起补包请求  
            RequestMissingFrames(
                lastReceivedFrameNumber + 1,    // 从哪一帧开始丢  
                currentFrameNumber - 1          // 到哪一帧丢  
            );
        }
    }

    /// <summary>
    /// 处理炸弹状态更新
    /// </summary>
    private void ProcessBombStates(BombStateBroadcast stateMsg)
    {
        foreach (BombInfo bomb in stateMsg.bombs)
        {
            if (bomb.state.Equals("Exploding", System.StringComparison.OrdinalIgnoreCase))
            {
                if (localBombs.TryGetValue(bomb.bombId, out GameObject bombObj))
                {
                    if (explosionTriggeredTimestamp[bomb.bombId] == 0)
                    {
                        explosionTriggeredTimestamp[bomb.bombId] = bomb.explosionTimestamp;

                        //新增：停止呼吸动画  
                        BombBreathingAnimation breathing = bombObj.GetComponent<BombBreathingAnimation>();
                        if (breathing != null) breathing.StopAnimation();

                        Debug.Log($"[BombStateManager] 触发爆炸: {bomb.bombId}");
                        PlayExplosionSound(bombObj.transform.position);
                        PlayExplosionEffect(bombObj.transform.position);
                    }

                    // 清理爆炸范围可视化  
                    RemoveExplosionRangeVisuals(bomb.bombId);

                    Destroy(bombObj);
                    localBombs.Remove(bomb.bombId);
                    explosionTriggeredTimestamp.Remove(bomb.bombId);
                    // 触发事件，通知UI管理器
                    OnBombDestroyedFromServer?.Invoke(bomb.bombId);
                    Debug.Log($"[BombStateManager] 销毁炸弹: {bomb.bombId} (state=Exploding)");
                }
                continue;
            }

            // ── Removed 状态（被沉默道具消除）─────────────────  
            if (bomb.state.Equals("Removed", System.StringComparison.OrdinalIgnoreCase))
            {
                if (localBombs.TryGetValue(bomb.bombId, out GameObject bombObj))
                {
                    Debug.Log($"[BombStateManager] 炸弹被沉默道具消除: {bomb.bombId}");

                    // 播放消除特效  
                    PlaySilenceRemoveEffect(bombObj.transform.position);

                    // 清理爆炸范围可视化  
                    RemoveExplosionRangeVisuals(bomb.bombId);

                    // 立即销毁炸弹  
                    Destroy(bombObj);
                    localBombs.Remove(bomb.bombId);
                    explosionTriggeredTimestamp.Remove(bomb.bombId);

                    // 通知UI管理器（归还炸弹数量）  
                    OnBombDestroyedFromServer?.Invoke(bomb.bombId);

                    Debug.Log($"[BombStateManager] 销毁炸弹: {bomb.bombId} (state=Removed)");
                }
                continue;
            }

            // 仅处理 "Active" 状态的炸弹  
            if (!bomb.state.Equals("Active", System.StringComparison.OrdinalIgnoreCase))
            {
                continue;  // 其他状态暂不处理  
            }

            // 将锚点相对坐标转换为世界坐标  
            Vector3 worldPosition = ConvertToWorldPosition(bomb.position);
            if (!localBombs.ContainsKey(bomb.bombId))
            {
                // 新炸弹：创建视觉对象（仅当 state == "Active"）  
                CreateBombVisual(bomb, worldPosition);

                // 创建爆炸范围可视化（自动根据 teamId 着色）  
                CreateExplosionRangeVisuals(bomb.bombId, bomb, worldPosition);

                explosionTriggeredTimestamp[bomb.bombId] = 0;
                Debug.Log($"[BombStateManager] 新炸弹: {bomb.bombId}, 状态: Active, 队伍: {bomb.teamId}");
            }
            else
            {
                // 更新已存在的炸弹位置和倒计时  
                UpdateBombVisual(bomb, worldPosition);

                // 更新爆炸范围位置  
                UpdateExplosionRangeVisuals(bomb.bombId, worldPosition);
            }
        }
    }
    /// <summary>  
    /// 播放沉默道具消除炸弹的特效  
    /// </summary>  
    private void PlaySilenceRemoveEffect(Vector3 position)
    {
        if (silenceRemoveEffectPrefab == null)
        {
            Debug.LogWarning("[BombStateManager] 消除特效预制体未配置！");
            return;
        }

        GameObject effectInstance = Instantiate(silenceRemoveEffectPrefab, position, Quaternion.identity);
        effectInstance.name = "SilenceRemoveEffect";

        // 自动销毁特效  
        Destroy(effectInstance, silenceEffectDuration);

        Debug.Log($"[BombStateManager] 播放消除特效，位置: {position}");
    }
    /*
    /// <summary>
    /// 创建炸弹视觉对象
    /// </summary>
    private void CreateBombVisual(BombInfo bomb, Vector3 worldPosition)
    {
        Debug.Log($"[BombStateManager] 尝试创建炸弹视觉: bombId={bomb.bombId}, teamId='{bomb.teamId}'");
        Debug.Log($"[BombStateManager]   prefabRed={bombVisualPrefabRed != null}, prefabBlue={bombVisualPrefabBlue != null}");
        // 根据团队ID选择对应的预制体  
        GameObject selectedPrefab = GetBombPrefabByTeam(bomb.teamId);

        if (selectedPrefab == null)
        {
            Debug.LogError($"[BombStateManager] 无法找到对应队伍 ({bomb.teamId}) 的炸弹视觉预制体！");
            return;
        }

        GameObject bombObj = Instantiate(selectedPrefab, worldPosition, Quaternion.identity);
        bombObj.name = $"RemoteBomb_{bomb.bombId}";

        // 修改倒计时
        BombCountdownDisplay countdown = bombObj.GetComponentInChildren<BombCountdownDisplay>();
        if (countdown != null)
        {
            countdown.SetCountdown(bomb.remainingTime);
            Debug.Log($"[BombStateManager] 初始化倒计时: {bomb.remainingTime:F1}秒");
        }

        // 将实际创建的对象存储到字典中  
        localBombs[bomb.bombId] = bombObj;
    }*/

    // 简化版本 - worldPosition 已经是世界坐标，直接使用
    private void CreateBombVisual(BombInfo bomb, Vector3 worldPosition)
    {
        Debug.Log($"[BombStateManager] 尝试创建炸弹视觉: bombId={bomb.bombId}, teamId='{bomb.teamId}'");
        Debug.Log($"[BombStateManager]   prefabRed={bombVisualPrefabRed != null}, prefabBlue={bombVisualPrefabBlue != null}");

        // 根据团队ID选择对应的预制体  
        GameObject selectedPrefab = GetBombPrefabByTeam(bomb.teamId);

        if (selectedPrefab == null)
        {
            Debug.LogError($"[BombStateManager] 无法找到对应队伍 ({bomb.teamId}) 的炸弹视觉预制体！");
            return;
        }

        // 直接使用世界坐标实例化
        GameObject bombObj = Instantiate(selectedPrefab, worldPosition, Quaternion.identity);
        bombObj.name = $"RemoteBomb_{bomb.bombId}";

        // 关键：以Prefab实例化后的原始localScale作为一级基准  
        Vector3 prefabOriginalScale = bombObj.transform.localScale;
        float levelMultiplier = GetBombLevelMultiplier(bomb.bombLevel);
        bombObj.transform.localScale = prefabOriginalScale * levelMultiplier;

        // 修改倒计时
        BombCountdownDisplay countdown = bombObj.GetComponentInChildren<BombCountdownDisplay>();
        if (countdown != null)
        {
            countdown.SetCountdown(bomb.remainingTime);
            Debug.Log($"[BombStateManager] 初始化倒计时: {bomb.remainingTime:F1}秒");
        }

        BombBreathingAnimation breathing = bombObj.AddComponent<BombBreathingAnimation>();
        Canvas countdownCanvas = bombObj.GetComponentInChildren<Canvas>();
        if (countdownCanvas != null)
        {
            breathing.SetExcludedChildren(new Transform[] { countdownCanvas.transform });
        }

        float speed = GetBreathSpeedByLevel(bomb.bombLevel);
        breathing.SetBreathSpeed(speed);

        // 将实际创建的对象存储到字典中  
        localBombs[bomb.bombId] = bombObj;
    }


    private GameObject GetBombPrefabByTeam(string teamId)
    {
        if (string.IsNullOrEmpty(teamId))
        {
            Debug.LogWarning("[BombStateManager] TeamId 为空，使用默认红队预制体");
            return bombVisualPrefabRed;
        }

        if (teamId.ToLower().Contains("red"))
        {
            if (bombVisualPrefabRed == null)
                Debug.LogError("[BombStateManager] bombVisualPrefabRed 未赋值！");
            return bombVisualPrefabRed;
        }
        else if (teamId.ToLower().Contains("blue"))
        {
            if (bombVisualPrefabBlue == null)
                Debug.LogError("[BombStateManager] bombVisualPrefabBlue 未赋值！");
            return bombVisualPrefabBlue;
        }

        Debug.LogError($"[BombStateManager] 无法识别 TeamId: {teamId}");
        return null;
    }

    /*
    /// <summary>
    /// 更新炸弹位置和倒计时显示
    /// </summary>
    private void UpdateBombVisual(BombInfo bomb, Vector3 worldPosition)
    {
        if (!localBombs.TryGetValue(bomb.bombId, out GameObject bombObj)) return;

        bombObj.transform.position = worldPosition;

        // 更新倒计时文本
        var countdownText = bombObj.GetComponentInChildren<TextMesh>();
        if (countdownText != null)
        {
            countdownText.text = Mathf.Max(0, bomb.remainingTime).ToString("F1");
        }
    }*/
    private void UpdateBombVisual(BombInfo bomb, Vector3 worldPosition)
    {
        if (!localBombs.TryGetValue(bomb.bombId, out GameObject bombObj)) return;

        bombObj.transform.position = worldPosition;

        BombCountdownDisplay countdown = bombObj.GetComponentInChildren<BombCountdownDisplay>();
        if (countdown != null)
        {
            countdown.SetCountdown(bomb.remainingTime);
        }

        BombBreathingAnimation breathing = bombObj.GetComponent<BombBreathingAnimation>();
        if (breathing != null)
        {
            float levelMultiplier = GetBombLevelMultiplier(bomb.bombLevel);
            Vector3 prefabScale = GetPrefabOriginalScale(bomb.teamId);
            if (prefabScale != Vector3.zero)
            {
                breathing.SetBaseScale(prefabScale * levelMultiplier);
            }

            // 呼吸速度随时间紧迫度动态加快  
            float baseSpeed = GetBreathSpeedByLevel(bomb.bombLevel);
            float urgencyBonus = 0f;
            if (bomb.totalTime > 0f)
            {
                float ratio = 1f - Mathf.Clamp01(bomb.remainingTime / bomb.totalTime);
                urgencyBonus = Mathf.Clamp01((ratio - 0.7f) / 0.3f) * 2.0f;
            }
            breathing.SetBreathSpeed(baseSpeed + urgencyBonus);
        }
    }



    /// <summary>
    /// 可视化爆炸范围（调试用）
    /// </summary>
    private void DrawExplosionRange(ExplosionRange range, Vector3 bombPosition)
    {
        Vector3 center = bombPosition;
        float width = range.xMax - range.xMin;
        float height = range.zMax - range.zMin;

        Debug.Log($"爆炸范围: [{range.xMin}, {range.xMax}] Y[{range.zMin}, {range.zMax}]");
        // 实际可以在这里绘制矩形框供调试
    }

    // 添加一个玩家队伍缓存  
    private Dictionary<string, string> _playerTeamCache = new Dictionary<string, string>();

    // 在 HandleBombStateReceived 中同时订阅 WorldState 更新来维护缓存  
    private void HandleWorldStateReceived(WorldStateMsg worldState)
    {
        // 缓存所有玩家的队伍信息  
        if (worldState?.teams != null)
        {
            foreach (var team in worldState.teams)
            {
                if (team?.players != null)
                {
                    foreach (var player in team.players)
                    {
                        _playerTeamCache[player.playerId] = team.teamId;
                    }
                }
            }
        }
    }

    /// <summary>  
    /// 处理游戏结束消息  
    /// </summary>  
    private void HandleGameEnd(GameEndMsg gameEndMsg)
    {
        Debug.Log($"[BombStateManager] 收到游戏结束消息，赢家: {gameEndMsg.winnerTeamName}");

        // 销毁所有炸弹  
        DestroyAllBombs();
    }
    /// <summary>
    /// 清理删除的炸弹
    /// </summary>
    /// 
    private void CleanupRemovedBombs(BombStateBroadcast stateMsg)
    {
        var clientKeys = new List<string>(localBombs.Keys);
        foreach (string bombId in clientKeys)
        {
            bool stillExists = System.Array.Exists(stateMsg.bombs, b => b.bombId == bombId);
            if (!stillExists)
            {
                Destroy(localBombs[bombId]);
                localBombs.Remove(bombId);
                explosionTriggeredTimestamp.Remove(bombId);

                // 清理爆炸范围可视化
                RemoveExplosionRangeVisuals(bombId);

                // 触发事件  
                OnBombDestroyedFromServer?.Invoke(bombId);

                Debug.Log($"清理炸弹: {bombId}");
            }
        }
    }


    /// <summary>
    /// 发起补包请求[^25]
    /// </summary>
    private void RequestMissingFrames(int fromFrame, int toFrame)
    {
        var request = new MissingFrameRequest
        {
            type = "MissingFrameRequest",
            clientId = PicoWebSocketClient.Instance.PlayerId,
            fromFrameNumber = fromFrame,
            toFrameNumber = toFrame,
            timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };

        string json = JsonUtility.ToJson(request);
        PicoWebSocketClient.Instance.SendRawMessage(json);

        Debug.Log($"已发送补包请求: 帧{fromFrame}~{toFrame}");
        // 服务端收到这个请求后，会查询历史缓冲[^22]  
        // 然后只给这个客户端补发丢失的帧  
    }
    /*
    private Vector3 ConvertToWorldPosition(Vec3 pos)
    {
        Vector3 localPos = new Vector3(pos.x, pos.y, pos.z);

        // 如果有共享锚点，转换为世界坐标[^38]
        if (PicoWebSocketClient.Instance?.SharedAnchorTransform != null)
        {
            return PicoWebSocketClient.Instance.SharedAnchorTransform
                .TransformPoint(localPos);
        }

        return localPos;
    }*/
    // 简化版本 - 直接返回作为世界坐标  
    private Vector3 ConvertToWorldPosition(Vec3 pos)
    {
        // 因为已删除锚点，接收到的位置就是世界坐标  
        return new Vector3(pos.x, pos.y, pos.z);
    }

    private float GetBombLevelMultiplier(string bombLevel)
    {
        return bombLevel switch
        {
            "一级" => 1.0f,
            "二级" => 2.0f,
            "三级" => 3.0f,
            _ => 1.0f
        };
    }
    private float GetBreathSpeedByLevel(string bombLevel)
    {
        return bombLevel switch
        {
            "一级" => 0.8f,   // 最慢，平静  
            "二级" => 1.4f,   // 明显比一级快  
            "三级" => 2.2f,   // 紧张  
            _ => 0.8f
        };
    }
    private Vector3 GetPrefabOriginalScale(string teamId)
    {
        if (teamId.ToLower().Contains("red"))
        {
            if (_prefabScaleRed == Vector3.zero && bombVisualPrefabRed != null)
                _prefabScaleRed = bombVisualPrefabRed.transform.localScale;
            return _prefabScaleRed;
        }
        else if (teamId.ToLower().Contains("blue"))
        {
            if (_prefabScaleBlue == Vector3.zero && bombVisualPrefabBlue != null)
                _prefabScaleBlue = bombVisualPrefabBlue.transform.localScale;
            return _prefabScaleBlue;
        }
        return Vector3.one;
    }
    void OnDestroy()
    {
        if (PicoWebSocketClient.Instance != null)
        {
            PicoWebSocketClient.Instance.OnRemoteBombStateReceived -= HandleBombStateReceived;
            PicoWebSocketClient.Instance.OnWorldStateReceived -= HandleWorldStateReceived;
            PicoWebSocketClient.Instance.OnGameEndReceived -= HandleGameEnd;
        }
    }

    //可视化
    /// <summary>  
    /// 为炸弹创建爆炸范围的可视化对象  
    /// </summary>  
    private void CreateExplosionRangeVisuals(string bombId, BombInfo bomb, Vector3 bombWorldPosition)
    {
        //Debug.Log($"[DEBUG] showExplosionRanges={showExplosionRanges}");
        //Debug.Log($"[DEBUG] explosionRanges={bomb.explosionRanges?.Length ?? -1}");

        if (!showExplosionRanges || bomb.explosionRanges == null || bomb.explosionRanges.Length == 0)
        {
            //Debug.LogWarning($"爆炸范围未创建！showExplosionRanges={showExplosionRanges}, rangesCount={bomb.explosionRanges?.Length ?? -1}");
            return;
        }
        //Debug.LogWarning($"[DEBUG] 爆炸范围创建，未进if！");
        List<GameObject> rangeVisuals = new List<GameObject>();

        // 根据 teamId 获取对应的颜色  
        Color fillColor = GetExplosionRangeColorByTeam(bomb.teamId, false);
        Color outlineColor = GetExplosionRangeColorByTeam(bomb.teamId, true);

        foreach (ExplosionRange range in bomb.explosionRanges)
        {
            //Debug.Log($"创建爆炸范围：X[{range.xMin},{range.xMax}] Z[{range.zMin},{range.zMax}]");
            GameObject rangeVisual = CreateSingleRangeVisual(range, bombWorldPosition, fillColor, outlineColor);
            if (rangeVisual != null)
            {
                rangeVisuals.Add(rangeVisual);
                if (localBombs.TryGetValue(bombId, out GameObject bombObj))
                {
                    rangeVisual.transform.SetParent(bombObj.transform);
                }
            }
        }

        if (rangeVisuals.Count > 0)
        {
            explosionRangeVisuals[bombId] = rangeVisuals.ToArray();
        }
    }

    /// <summary>  
    /// 清理爆炸范围可视化  
    /// </summary>  
    private void RemoveExplosionRangeVisuals(string bombId)
    {
        if (explosionRangeVisuals.TryGetValue(bombId, out GameObject[] visuals))
        {
            foreach (GameObject visual in visuals)
            {
                if (visual != null)
                {
                    //Debug.Log("清理爆炸范围可视化！");
                    Destroy(visual);
                }
            }
            explosionRangeVisuals.Remove(bombId);
        }
    }

    /// <summary>  
    /// 根据队伍ID获取爆炸范围颜色  
    /// </summary>  
    private Color GetExplosionRangeColorByTeam(string teamId, bool isOutline)
    {
        if (string.IsNullOrEmpty(teamId))
        {
            return isOutline ? explosionRangeOutlineColorRed : explosionRangeColorRed;
        }

        if (teamId.ToLower().Contains("red"))
        {
            return isOutline ? explosionRangeOutlineColorRed : explosionRangeColorRed;
        }
        else if (teamId.ToLower().Contains("blue"))
        {
            return isOutline ? explosionRangeOutlineColorBlue : explosionRangeColorBlue;
        }

        // 默认红队  
        return isOutline ? explosionRangeOutlineColorRed : explosionRangeColorRed;
    }


    /// <summary>  
    /// 创建单个爆炸范围的可视化（透明矩形框）  
    /// </summary>  
    private GameObject CreateSingleRangeVisual(ExplosionRange range, Vector3 bombWorldPosition, Color fillColor, Color outlineColor)
    {
        //Debug.Log("CreateSingleRangeVisual");
        GameObject container = new GameObject("ExplosionRangeVisual");
        container.transform.position = bombWorldPosition;

       
        // 计算矩形的中心和大小  
        float width = range.xMax - range.xMin;
        float depth = range.zMax - range.zMin;
        float centerX = (range.xMin + range.xMax) / 2f;
        float centerZ = (range.zMin + range.zMax) / 2f;
        /*
        // 创建透明填充面（使用 Quad）  
        GameObject fillQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        fillQuad.name = "RangeFill";
        fillQuad.transform.SetParent(container.transform);
        fillQuad.transform.localPosition = new Vector3(centerX, 0.01f, centerZ);
        fillQuad.transform.localScale = new Vector3(width, 1f, depth);
        fillQuad.transform.rotation = Quaternion.Euler(90, 0, 0);  // 旋转到 XZ 平面  
        
       // 设置材质为半透明，使用传入的颜色  
       Renderer fillRenderer = fillQuad.GetComponent<Renderer>();
       Material fillMaterial = new Material(Shader.Find("Standard"));
       fillMaterial.SetFloat("_Mode", 3);  // Transparent 模式  
       fillMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
       fillMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
       fillMaterial.SetInt("_ZWrite", 0);
       fillMaterial.DisableKeyword("_ALPHATEST_ON");
       fillMaterial.EnableKeyword("_ALPHABLEND_ON");
       fillMaterial.renderQueue = 3000;
       fillMaterial.color = fillColor;  // ★ 使用动态颜色  
       fillRenderer.material = fillMaterial;

       // 移除碰撞体  
       DestroyImmediate(fillQuad.GetComponent<Collider>());
       */
        // 创建边框（使用线条）  
        CreateRangeOutline(container.transform, range, centerX, centerZ, outlineColor);

        return container;
    }
    /// <summary>
    /// 更新爆炸范围可视化的位置（跟随炸弹）
    /// </summary>
    private void UpdateExplosionRangeVisuals(string bombId, Vector3 bombWorldPosition)
    {
        if (!explosionRangeVisuals.TryGetValue(bombId, out GameObject[] visuals))
            return;

        foreach (GameObject visual in visuals)
        {
            if (visual != null)
            {
                visual.transform.position = bombWorldPosition;
            }
        }
    }

    
    /// <summary>  
    /// 创建爆炸范围的边框线条  
    /// </summary>  
    private void CreateRangeOutline(Transform parent, ExplosionRange range, float centerX, float centerZ, Color outlineColor)
    {
        //Debug.Log("CreateRangeOutline");
        GameObject outlineObj = new GameObject("RangeOutline");
        outlineObj.transform.SetParent(parent);
        outlineObj.transform.localPosition = Vector3.zero;

        LineRenderer lineRenderer = outlineObj.AddComponent<LineRenderer>();
        lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        lineRenderer.startColor = outlineColor;
        lineRenderer.endColor = outlineColor;
        lineRenderer.startWidth = 0.1f;
        lineRenderer.endWidth = 0.1f;

        // 直接使用 range 值作为本地坐标，不再减去 centerX/centerZ
        Vector3[] positions = new Vector3[5]
        {
        new Vector3(range.xMin, 0.57f, range.zMin),
        new Vector3(range.xMax, 0.57f, range.zMin),
        new Vector3(range.xMax, 0.57f, range.zMax),
        new Vector3(range.xMin, 0.57f, range.zMax),
        new Vector3(range.xMin, 0.57f, range.zMin)
        };

        lineRenderer.positionCount = positions.Length;
        lineRenderer.SetPositions(positions);
    }
   

    /// <summary>  
    /// 播放炸弹爆炸音效  
    /// </summary>  
    private void PlayExplosionSound(Vector3 explosionPosition)
    {
        if (explosionSoundClip == null)
        {
            //Debug.LogWarning("[BombStateManager] 爆炸音效未配置！");
            return;
        }

        // 方法1：通过 AudioSource 播放（推荐）  
        AudioSource audioSource = gameObject.GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        audioSource.clip = explosionSoundClip;
        audioSource.volume = explosionSoundVolume;
        audioSource.pitch = explosionSoundPitch;
        audioSource.PlayOneShot(explosionSoundClip, explosionSoundVolume);

        //Debug.Log($"[BombStateManager] 播放爆炸音效，位置: {explosionPosition}");

    }

    /// <summary>  
    /// 播放炸弹爆炸特效  
    /// </summary>  
    private void PlayExplosionEffect(Vector3 explosionPosition)
    {
        if (explosionEffectPrefab == null)
        {
            //Debug.LogWarning("[BombStateManager] 爆炸特效预制体未配置！");
            return;
        }

        // 实例化爆炸特效  
        GameObject effectInstance = Instantiate(explosionEffectPrefab, explosionPosition, Quaternion.identity);
        effectInstance.name = "ExplosionEffect";

        // 可选：自动销毁特效（假设特效时长为3秒）  
        Destroy(effectInstance, 3f);

        //Debug.Log($"[BombStateManager] 播放爆炸特效，位置: {explosionPosition}");
    }
    /// <summary>  
    /// 销毁所有场上的炸弹（游戏结束时调用）  
    /// </summary>  
    public void DestroyAllBombs()
    {
        Debug.Log("[BombStateManager] 销毁所有炸弹！");

        var bombIds = new List<string>(localBombs.Keys);
        foreach (string bombId in bombIds)
        {
            // 销毁炸弹GameObject  
            if (localBombs.TryGetValue(bombId, out GameObject bombObj))
            {
                Destroy(bombObj);
            }

            // 清理爆炸范围可视化  
            RemoveExplosionRangeVisuals(bombId);

            // 清理数据  
            localBombs.Remove(bombId);
            explosionTriggeredTimestamp.Remove(bombId);

           // Debug.Log($"[BombStateManager] 销毁炸弹: {bombId}");
        }
    }
}
/*
[System.Serializable]
public class BombStateFrame
{
    public int frameSequenceNumber;
    public long timestamp;
    public Dictionary<string, BombInfo> bombs;
}
*/