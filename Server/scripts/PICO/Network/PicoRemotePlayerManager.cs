// PicoRemotePlayerManager.cs
using BubbleBattle.Network;
using UnityEngine;
using System.Collections.Generic;
using UnityEngine.XR;
using TMPro;
using System.Collections;

/// <summary>
/// Pico MR设备专用的远程玩家管理器
/// 专为Pico设备优化，包含XR追踪、性能优化和MR场景适配
/// </summary>
public class PicoRemotePlayerManager : MonoBehaviour
{
    [SerializeField] private GameObject playerPrefab; // 远程玩家模型预制体
    [SerializeField] private Transform playersParent; // 远程玩家对象的父容器

    [Header("Pico MR 特定设置")]
    [SerializeField] private float updateThreshold = 0.01f; // 位置更新阈值（米）
    [SerializeField] private float rotationThreshold = 0.1f; // 旋转更新阈值
    [SerializeField] private float smoothTime = 0.1f; // 平滑移动时间
    [SerializeField] private int maxRemotePlayersShown = 10; // 最多显示的远程玩家数
    [SerializeField] private float cullingDistance = 50f; // 视距范围（米）

    [Header("性能优化")]
    [SerializeField] private bool enableLOD = true; // 启用LOD系统
    [SerializeField] private bool enablePositionSmoothing = true; // 启用位置平滑
    [SerializeField] private int updateFrameSkip = 2; // 每N帧更新一次远处玩家

    private Dictionary<string, RemotePlayer> remotePlayersDict = new();
    private HashSet<string> currentPlayerIds = new();
    private int frameCounter = 0;

    [SerializeField] private GameObject healthBarPrefab;
    [SerializeField] private GameObject playerPrefabRed;   // 红队玩家预制体  
    [SerializeField] private GameObject playerPrefabBlue;  // 蓝队玩家预制体  

    // Pico XR追踪相关
    private List<XRNodeState> nodeStates = new();
    private Vector3 localPlayerPosition; // 本地玩家世界坐标

    //
    [SerializeField] private GameObject invincibleEffectPrefab;  // 无敌特效预制体  
    private Dictionary<string, InvincibleStateInfo> invincibleStatesDict = new();
    private Dictionary<string, int> remotePlayerLastBlood = new();

    void Start()
    {
        if (PicoWebSocketClient.Instance != null)
        {
            PicoWebSocketClient.Instance.OnWorldStateReceived += HandleWorldState;
            PicoWebSocketClient.Instance.OnPlayersBloodReceived += HandlePlayersBloodUpdate;
            PicoWebSocketClient.Instance.OnInvincibleStateReceived += HandleInvincibleStateUpdate;
        }
        else
        {
            //Debug.LogError("[PicoRemotePlayerManager] PicoWebSocketClient.Instance 为空！");
        }
    }

    void Update()
    {
        frameCounter++;
        SyncInvincibleEffectPositions();
        // 获取本地玩家位置（用于视距剔除）
        UpdateLocalPlayerPosition();
    }
    private void SyncInvincibleEffectPositions()
    {
        foreach (var state in invincibleStatesDict.Values)
        {
            if (state.isInvincible && state.invincibleCountdown > 0
            && remotePlayersDict.TryGetValue(state.playerId, out var remotePlayer))
            {
                Transform invincibleEffect = remotePlayer.GameObject.transform.Find("InvincibleEffect");
                if (invincibleEffect != null && invincibleEffect.gameObject.activeSelf)  //激活检查  
                {
                    invincibleEffect.position = remotePlayer.GameObject.transform.position;
                }
            }
        }
    }


    private void UpdateLocalPlayerPosition()
    {
        // 从Pico XR设备获取头部位置  
        InputTracking.GetNodeStates(nodeStates);

        foreach (var nodeState in nodeStates)
        {
            if (nodeState.nodeType == XRNode.Head && nodeState.tracked)
            {
                nodeState.TryGetPosition(out localPlayerPosition);
                break;
            }
        }
    }

    private void HandleWorldState(WorldStateMsg worldState)
    {
        if (worldState?.teams == null)
            return;

        currentPlayerIds.Clear();
        int displayedPlayers = 0;

        // 遍历所有队伍和玩家
        foreach (var team in worldState.teams)
        {
            if (team?.players == null)
                continue;

            foreach (var playerState in team.players)
            {
                // 跳过本地玩家
                if (playerState.playerId == PicoWebSocketClient.Instance?.PlayerId)
                    continue;

                currentPlayerIds.Add(playerState.playerId);

                // Pico性能优化：限制显示的玩家数
                if (displayedPlayers >= maxRemotePlayersShown)
                    continue;

                // 视距剔除
                if (!IsPlayerInViewDistance(playerState))
                    continue;

                UpdateRemotePlayer(playerState, team.teamId);
                displayedPlayers++;
            }
        }

        // 移除不在列表中的玩家
        RemoveDisconnectedPlayers();
    }

    /*
    // 修改 IsPlayerInViewDistance 方法以处理锚点相对坐标
    private bool IsPlayerInViewDistance(PlayerStateInfo playerState)
    {
        Vector3 playerPosFromMessage = new Vector3(
            playerState.position.x,
            playerState.position.y,
            playerState.position.z
        );

        Vector3 playerWorldPosition;

        // 如果存在共享锚点，将接收到的局部坐标转换为世界坐标
        if (PicoWebSocketClient.Instance.SharedAnchorTransform != null)
        {
            playerWorldPosition = PicoWebSocketClient.Instance.SharedAnchorTransform.TransformPoint(playerPosFromMessage);
        }
        else
        {
            // 否则，假定接收到的就是世界坐标（旧行为或无锚点场景）
            playerWorldPosition = playerPosFromMessage;
        }

        // localPlayerPosition 已经是世界坐标
        float distance = Vector3.Distance(localPlayerPosition, playerWorldPosition);
        return distance <= cullingDistance;
    }*/
    private bool IsPlayerInViewDistance(PlayerStateInfo playerState)
    {
        Vector3 playerWorldPosition = new Vector3(
            playerState.position.x,
            playerState.position.y,
            playerState.position.z
        );

        float distance = Vector3.Distance(localPlayerPosition, playerWorldPosition);
        return distance <= cullingDistance;
    }

    private void UpdateRemotePlayer(PlayerStateInfo playerState, string teamId)
    {
        if (remotePlayersDict.ContainsKey(playerState.playerId))
        {
            var remotePlayer = remotePlayersDict[playerState.playerId];
            UpdateExistingPlayer(remotePlayer, playerState);
        }
        else
        {
            // 创建时传入 teamId  
            CreateRemotePlayer(playerState, teamId);
        }
    }
    /*
    // 修改 UpdateExistingPlayer 方法以处理锚点相对坐标
    private void UpdateExistingPlayer(RemotePlayer remotePlayer, PlayerStateInfo playerState)
    {
        Vector3 newPositionFromMessage = new Vector3(
            playerState.position.x,
            playerState.position.y,
            playerState.position.z
        );

        Quaternion newRotationFromMessage = new Quaternion(
            playerState.rotation.x,
            playerState.rotation.y,
            playerState.rotation.z,
            playerState.rotation.w
        );

        Vector3 targetWorldPosition;
        Quaternion targetWorldRotation;

        // 如果存在共享锚点，将接收到的局部坐标转换为世界坐标
        if (PicoWebSocketClient.Instance.SharedAnchorTransform != null)
        {
            targetWorldPosition = PicoWebSocketClient.Instance.SharedAnchorTransform.TransformPoint(newPositionFromMessage);
            targetWorldRotation = PicoWebSocketClient.Instance.SharedAnchorTransform.rotation * newRotationFromMessage; // 注意这里是乘法
        }
        else
        {
            // 否则，假定接收到的就是世界坐标
            targetWorldPosition = newPositionFromMessage;
            targetWorldRotation = newRotationFromMessage;
        }


        // 检查位置变化是否足够大 (与目标世界位置比较)
        float positionDelta = Vector3.Distance(remotePlayer.CurrentPosition, targetWorldPosition);
        float rotationDelta = Quaternion.Angle(remotePlayer.CurrentRotation, targetWorldRotation);

        if (positionDelta > updateThreshold || rotationDelta > rotationThreshold)
        {
            remotePlayer.TargetPosition = targetWorldPosition;
            remotePlayer.TargetRotation = targetWorldRotation;
            remotePlayer.LastUpdateTime = Time.time;

            // Pico性能优化：远处玩家跳帧更新
            if (enableLOD && frameCounter % updateFrameSkip == 0)
            {
                // 远处玩家使用更新频率
                int distance = (int)Vector3.Distance(localPlayerPosition, targetWorldPosition); // 与目标世界位置比较
                if (distance > cullingDistance * 0.5f)
                    return;
            }

            // 立即更新或平滑更新
            if (enablePositionSmoothing)
            {
                remotePlayer.IsSmoothing = true;
            }
            else
            {
                remotePlayer.GameObject.transform.position = targetWorldPosition;
                remotePlayer.GameObject.transform.rotation = targetWorldRotation;
                remotePlayer.CurrentPosition = targetWorldPosition;
                remotePlayer.CurrentRotation = targetWorldRotation;
            }
        }

        // 应用平滑移动
        if (remotePlayer.IsSmoothing)
        {
            remotePlayer.GameObject.transform.position = Vector3.Lerp(
                remotePlayer.GameObject.transform.position,
                remotePlayer.TargetPosition,
                Time.deltaTime / smoothTime
            );

            remotePlayer.GameObject.transform.rotation = Quaternion.Lerp(
                remotePlayer.GameObject.transform.rotation,
                remotePlayer.TargetRotation,
                Time.deltaTime / smoothTime
            );

            // 检查是否接近目标
            if (Vector3.Distance(remotePlayer.GameObject.transform.position, remotePlayer.TargetPosition) < 0.001f)
            {
                remotePlayer.IsSmoothing = false;
                remotePlayer.CurrentPosition = remotePlayer.TargetPosition;
                remotePlayer.CurrentRotation = remotePlayer.TargetRotation;
            }
        }
    }*/
    private void UpdateExistingPlayer(RemotePlayer remotePlayer, PlayerStateInfo playerState)
    {
        Vector3 targetWorldPosition = new Vector3(
            playerState.position.x,
            playerState.position.y,
            playerState.position.z
        );

        Quaternion targetWorldRotation = new Quaternion(
            playerState.rotation.x,
            playerState.rotation.y,
            playerState.rotation.z,
            playerState.rotation.w
        );

        // 检查位置变化是否足够大  
        float positionDelta = Vector3.Distance(remotePlayer.CurrentPosition, targetWorldPosition);
        float rotationDelta = Quaternion.Angle(remotePlayer.CurrentRotation, targetWorldRotation);

        if (positionDelta > updateThreshold || rotationDelta > rotationThreshold)
        {
            remotePlayer.TargetPosition = targetWorldPosition;
            remotePlayer.TargetRotation = targetWorldRotation;
            remotePlayer.LastUpdateTime = Time.time;

            if (enableLOD && frameCounter % updateFrameSkip == 0)
            {
                int distance = (int)Vector3.Distance(localPlayerPosition, targetWorldPosition);
                if (distance > cullingDistance * 0.5f)
                    return;
            }

            if (enablePositionSmoothing)
            {
                remotePlayer.IsSmoothing = true;
            }
            else
            {
                remotePlayer.GameObject.transform.position = targetWorldPosition;
                remotePlayer.GameObject.transform.rotation = targetWorldRotation;
                remotePlayer.CurrentPosition = targetWorldPosition;
                remotePlayer.CurrentRotation = targetWorldRotation;
            }
        }

        // 平滑移动逻辑保持不变...  
        // 应用平滑移动
        if (remotePlayer.IsSmoothing)
        {
            remotePlayer.GameObject.transform.position = Vector3.Lerp(
                remotePlayer.GameObject.transform.position,
                remotePlayer.TargetPosition,
                Time.deltaTime / smoothTime
            );

            remotePlayer.GameObject.transform.rotation = Quaternion.Lerp(
                remotePlayer.GameObject.transform.rotation,
                remotePlayer.TargetRotation,
                Time.deltaTime / smoothTime
            );

            // 检查是否接近目标
            if (Vector3.Distance(remotePlayer.GameObject.transform.position, remotePlayer.TargetPosition) < 0.001f)
            {
                remotePlayer.IsSmoothing = false;
                remotePlayer.CurrentPosition = remotePlayer.TargetPosition;
                remotePlayer.CurrentRotation = remotePlayer.TargetRotation;
            }
        }
    }
    /// <summary>  
    /// 根据队伍ID获取对应的玩家预制体  
    /// </summary>  
    private GameObject GetPlayerPrefabByTeam(string teamId)
    {
        if (string.IsNullOrEmpty(teamId))
        {
            //Debug.LogWarning("[PicoRemotePlayerManager] TeamId 为空，使用默认预制体");
            return playerPrefab;
        }

        if (teamId.ToLower().Contains("red"))
        {
            if (playerPrefabRed == null)
            {
                //Debug.LogError("[PicoRemotePlayerManager] playerPrefabRed 未赋值！");
                return playerPrefab; // 回退到默认预制体  
            }
            return playerPrefabRed;
        }
        else if (teamId.ToLower().Contains("blue"))
        {
            if (playerPrefabBlue == null)
            {
                //Debug.LogError("[PicoRemotePlayerManager] playerPrefabBlue 未赋值！");
                return playerPrefab; // 回退到默认预制体  
            }
            return playerPrefabBlue;
        }

        //Debug.LogWarning($"[PicoRemotePlayerManager] 无法识别 TeamId: {teamId}，使用默认预制体");
        return playerPrefab;
    }

    private void CreateRemotePlayer(PlayerStateInfo playerState, string teamId)
    {
        /*
        if (playerPrefab == null)
        {
            Debug.LogError("[PicoRemotePlayerManager] playerPrefab 未指定！");
            return;
        }

        Vector3 finalSpawnPosition = new Vector3(
            playerState.position.x,
            playerState.position.y,
            playerState.position.z
        );

        Quaternion finalSpawnRotation = new Quaternion(
            playerState.rotation.x,
            playerState.rotation.y,
            playerState.rotation.z,
            playerState.rotation.w
        );

        // 实例化玩家对象  
        GameObject playerObj = Instantiate(
            playerPrefab,
            */
            // 根据 teamId 获取对应的预制体
            GameObject selectedPrefab = GetPlayerPrefabByTeam(teamId);

            if (selectedPrefab == null)
            {
                //Debug.LogError("[PicoRemotePlayerManager] 无法获取有效的玩家预制体！");
                return;
            }

            Vector3 finalSpawnPosition = new Vector3(
                playerState.position.x,
                playerState.position.y,
                playerState.position.z
            );

            Quaternion finalSpawnRotation = new Quaternion(
                playerState.rotation.x,
                playerState.rotation.y,
                playerState.rotation.z,
                playerState.rotation.w
            );

            // 实例化玩家对象  
            GameObject playerObj = Instantiate(
                selectedPrefab,  // ← 使用选中的预制体
                finalSpawnPosition,
                finalSpawnRotation,
            playersParent
        );

        playerObj.name = $"RemotePlayer_{playerState.playerName}";
        playerObj.tag = "RemotePlayer";

        // 禁用物理碰撞  
        Collider[] colliders = playerObj.GetComponentsInChildren<Collider>();
        foreach (var collider in colliders)
        {
            collider.enabled = false;
        }

        // 显示玩家名字和血条...  
        SetPlayerNameDisplay(playerObj, playerState.playerName);
        CreateRemotePlayerHealthBar(playerObj, playerState.playerId, playerState.playerName, teamId);

        // 创建RemotePlayer数据结构  
        var remotePlayer = new RemotePlayer
        {
            PlayerId = playerState.playerId,
            PlayerName = playerState.playerName,
            GameObject = playerObj,
            CurrentPosition = finalSpawnPosition,
            CurrentRotation = finalSpawnRotation,
            TargetPosition = finalSpawnPosition,
            TargetRotation = finalSpawnRotation,
            LastUpdateTime = Time.time,
            IsSmoothing = false,
            TeamId = teamId
        };

        remotePlayersDict[playerState.playerId] = remotePlayer;
        //Debug.Log($"[PicoRemotePlayerManager] 创建远程玩家: {playerState.playerName} ({playerState.playerId}) 在位置: {finalSpawnPosition}");
    }

    private void SetPlayerNameDisplay(GameObject playerObj, string playerName)
    {
        // 查找TextMesh并设置玩家名字
        TextMesh textMesh = playerObj.GetComponentInChildren<TextMesh>();
        if (textMesh != null)
        {
            textMesh.text = playerName;
            textMesh.fontSize = 100;
            textMesh.anchor = TextAnchor.MiddleCenter;
        }

        // 或者查找TextMeshPro
        var textMeshPro = playerObj.GetComponentInChildren<TMPro.TextMeshPro>();
        if (textMeshPro != null)
        {
            textMeshPro.text = playerName;
            textMeshPro.alignment = TMPro.TextAlignmentOptions.Center;
        }
    }

    /// <summary>  
    /// 处理血量更新广播  
    /// </summary>  
    private void HandlePlayersBloodUpdate(PlayersBloodMsg bloodMsg)
    {
        //Debug.Log("进入HandlePlayersBloodUpdate");
        if (bloodMsg?.teams == null || bloodMsg.teams.Length == 0)
        {
            //Debug.LogWarning("[PicoRemotePlayerManager] 收到的血量消息为空");
            return;
        }

        // 遍历所有队伍和玩家  
        foreach (var teamInfo in bloodMsg.teams)
        {

            if (teamInfo?.players == null)
                continue;
            //Debug.Log("进入foreach");
            foreach (var playerBlood in teamInfo.players)
            {
                // 跳过本地玩家  
                if (playerBlood.playerId == PicoWebSocketClient.Instance?.PlayerId)
                    continue;
                //Debug.Log("跳过本地玩家");
                // 更新远程玩家血条  
                if (remotePlayersDict.TryGetValue(playerBlood.playerId, out var remotePlayer))
                {
                    // 同步血条世界位置（关键！）  
                    Transform healthBar = remotePlayer.GameObject.transform.Find("HealthBar");
                    if (healthBar != null)
                    {
                        healthBar.position = remotePlayer.GameObject.transform.position + Vector3.up * 0.3f;
                    }

                    var healthBarScript = remotePlayer.GameObject.GetComponentInChildren<RemotePlayerHealthBar>();
                    if (healthBarScript != null)
                    {
                        healthBarScript.UpdateHealth(playerBlood.blood, playerBlood.maxBlood);
                    }

                    if (remotePlayerLastBlood.TryGetValue(playerBlood.playerId, out int lastBlood))
                    {
                        if (playerBlood.blood > lastBlood)
                            ShowBloodVFX(remotePlayer.GameObject, "HealEffect");
                        else if (playerBlood.blood < lastBlood)
                            ShowBloodVFX(remotePlayer.GameObject, "DamageEffect");
                    }
                    // 记录本次血量，供下次比较  
                    remotePlayerLastBlood[playerBlood.playerId] = playerBlood.blood;

                }
            }
        }
    }

    void OnDestroy()
    {
        if (PicoWebSocketClient.Instance != null)
        {
            PicoWebSocketClient.Instance.OnWorldStateReceived -= HandleWorldState;
            //注销血量事件监听  
            PicoWebSocketClient.Instance.OnPlayersBloodReceived -= HandlePlayersBloodUpdate;
            PicoWebSocketClient.Instance.OnInvincibleStateReceived -= HandleInvincibleStateUpdate;
        }
    }

    /// <summary>  
    /// 为远程玩家创建血条显示  
    /// 血条预制体包含 white/red/blue 三层容器，RemotePlayerHealthBar 根据 teamId 激活对应容器  
    /// </summary>  
    private void CreateRemotePlayerHealthBar(GameObject playerObj, string playerId, string playerName, string teamId)
    {
        if (healthBarPrefab == null)
        {
            //Debug.LogError("[PicoRemotePlayerManager] healthBarPrefab 未指定！");
            return;
        }

        // 实例化血条预制体（在玩家头顶）  
        GameObject healthBarContainer = Instantiate(
            healthBarPrefab,
            playerObj.transform.position,
            Quaternion.identity,
            playerObj.transform
        );
        Canvas canvas = healthBarContainer.GetComponent<Canvas>();

        // 强制改成 World Space
        canvas.renderMode = RenderMode.WorldSpace;

        healthBarContainer.name = "HealthBar";
        healthBarContainer.transform.localPosition = new Vector3(0, 0.3f, 0);

        // 设置血条缩放大小  
        healthBarContainer.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);

        // 添加 BillboardUI（如果预制体中没有）  
        if (healthBarContainer.GetComponent<BillboardUI>() == null)
        {
            healthBarContainer.AddComponent<BillboardUI>();
        }

        // 获取或添加 RemotePlayerHealthBar 脚本  
        RemotePlayerHealthBar healthBar = healthBarContainer.GetComponent<RemotePlayerHealthBar>();
        if (healthBar == null)
        {
            healthBar = healthBarContainer.AddComponent<RemotePlayerHealthBar>();
        }

        // 初始化血条，传入 teamId  
        // RemotePlayerHealthBar 会根据 teamId 激活对应的 red/blue 容器，white 始终显示  
        healthBar.Initialize(playerId, 6, teamId);

        //Debug.Log($"[PicoRemotePlayerManager] 为玩家 {playerName}({playerId}) 创建了血条UI，队伍: {teamId}");
    }
    /*
    // 新增：根据队伍选择血条预制体  
    private GameObject GetHealthBarPrefabByTeam(string teamId)
    {
        if (string.IsNullOrEmpty(teamId))
        {
            Debug.LogWarning("[PicoRemotePlayerManager] TeamId 为空，使用默认红队血条");
            return healthBarPrefabRed;
        }

        if (teamId.ToLower().Contains("red"))
        {
            if (healthBarPrefabRed == null)
                Debug.LogError("[PicoRemotePlayerManager] healthBarPrefabRed 未赋值！");
            return healthBarPrefabRed;
        }
        else if (teamId.ToLower().Contains("blue"))
        {
            if (healthBarPrefabBlue == null)
                Debug.LogError("[PicoRemotePlayerManager] healthBarPrefabBlue 未赋值！");
            return healthBarPrefabBlue;
        }

        Debug.LogError($"[PicoRemotePlayerManager] 无法识别 TeamId: {teamId}");
        return null;
    }*/

    private void RemoveDisconnectedPlayers()
    {
        var playerIdsToRemove = new List<string>();

        foreach (var playerId in remotePlayersDict.Keys)
        {
            if (!currentPlayerIds.Contains(playerId))
            {
                playerIdsToRemove.Add(playerId);
            }
        }

        foreach (var playerId in playerIdsToRemove)
        {
            if (remotePlayersDict.TryGetValue(playerId, out var remotePlayer))
            {
                //Debug.Log($"[PicoRemotePlayerManager] 移除远程玩家: {remotePlayer.PlayerName}");
                Destroy(remotePlayer.GameObject);
                remotePlayerLastBlood.Remove(playerId);
                remotePlayersDict.Remove(playerId);
            }
        }
    }

    /// <summary>
    /// 远程玩家数据结构
    /// </summary>
    public class RemotePlayer
    {
        public string PlayerId { get; set; }
        public string PlayerName { get; set; }
        public GameObject GameObject { get; set; }
        public Vector3 CurrentPosition { get; set; } // 存储世界坐标
        public Quaternion CurrentRotation { get; set; } // 存储世界旋转
        public Vector3 TargetPosition { get; set; } // 存储世界坐标
        public Quaternion TargetRotation { get; set; } // 存储世界旋转
        public float LastUpdateTime { get; set; }
        public bool IsSmoothing { get; set; }
        public string TeamId { get; set; }
    }

    // 调试方法
    public void SetCullingDistance(float distance)
    {
        cullingDistance = distance;
        //Debug.Log($"[PicoRemotePlayerManager] 视距设置为: {distance}米");
    }

    public int GetActiveRemotePlayerCount()
    {
        return remotePlayersDict.Count;
    }

    public void ToggleLOD(bool enable)
    {
        enableLOD = enable;
        //Debug.Log($"[PicoRemotePlayerManager] LOD系统: {(enable ? "启用" : "禁用")}");
    }

    /// <summary>  
    /// 获取所有远程玩家字典，供 PlayerResultEffectManager 使用  
    /// </summary>  
    public Dictionary<string, RemotePlayer> GetAllRemotePlayers()
    {
        return remotePlayersDict;
    }

    //无敌特效
    private void HandleInvincibleStateUpdate(InvincibleStateMessage invincibleMsg)
    {
        if (invincibleMsg?.invincibleStates == null || invincibleMsg.invincibleStates.Length == 0)
        {
            return;
        }

        // 更新无敌状态字典  
        foreach (var state in invincibleMsg.invincibleStates)
        {
            invincibleStatesDict[state.playerId] = state;

            // 获取对应的远程玩家，显示/隐藏无敌特效  
            if (remotePlayersDict.TryGetValue(state.playerId, out var remotePlayer))
            {
                UpdateInvincibleEffect(remotePlayer, state);
            }
        }

        //Debug.Log($"[PicoRemotePlayerManager] 更新无敌状态: {invincibleMsg.invincibleStates.Length} 个玩家");
    }

    private void UpdateInvincibleEffect(RemotePlayer remotePlayer, InvincibleStateInfo state)
    {
        Transform invincibleEffect = remotePlayer.GameObject.transform.Find("InvincibleEffect");

        // 显示或隐藏特效（根据无敌状态）  
        if (state.isInvincible && state.invincibleCountdown > 0)
        {
            if (invincibleEffect == null)
            {
                if (invincibleEffectPrefab != null)
                {
                    invincibleEffect = Instantiate(
                        invincibleEffectPrefab,
                        remotePlayer.GameObject.transform.position,
                        Quaternion.identity,
                        remotePlayer.GameObject.transform
                    ).transform;
                    invincibleEffect.name = "InvincibleEffect";
                    invincibleEffect.localPosition = Vector3.zero;
                }
            }
            else if (!invincibleEffect.gameObject.activeSelf)
            {
                invincibleEffect.gameObject.SetActive(true);
            }

            // 根据倒计时更新闪烁效果  
            UpdateInvincibleEffectAnimation(invincibleEffect, state.invincibleCountdown);
        }
        else
        {
            // 无敌结束，隐藏特效  
            if (invincibleEffect != null && invincibleEffect.gameObject.activeSelf)
            {
                invincibleEffect.gameObject.SetActive(false);
            }
        }
    }

    private void UpdateInvincibleEffectAnimation(Transform effect, float countdown)
    {
        // 倒计时最后2秒闪烁  
        if (countdown <= 2f)
        {
            if (effect.TryGetComponent<CanvasGroup>(out var canvasGroup))
            {
                float alpha = Mathf.Abs(Mathf.Sin(Time.time * 5f));
                canvasGroup.alpha = alpha;
            }

            if (effect.TryGetComponent<Renderer>(out var renderer))
            {
                float alpha = Mathf.Abs(Mathf.Sin(Time.time * 5f));
                Color color = renderer.material.color;
                color.a = alpha;
                renderer.material.color = color;
            }
        }
    }

    private void ShowBloodVFX(GameObject playerObj, string effectName)
    {
        Transform vfx = playerObj.transform.Find(effectName);
        if (vfx == null) return;

        StartCoroutine(AutoPlayVFX(vfx.gameObject));
    }

    private IEnumerator AutoPlayVFX(GameObject vfx)
    {
        vfx.SetActive(true);

        var ps = vfx.GetComponentInChildren<ParticleSystem>();
        if (ps != null)
        {
            ps.Stop();
            ps.Play();
            yield return new WaitForSeconds(ps.main.duration + ps.main.startLifetime.constantMax);
        }
        else
        {
            yield return new WaitForSeconds(1.5f);
        }

        vfx.SetActive(false);
    }
}
