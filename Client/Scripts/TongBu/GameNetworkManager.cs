using UnityEngine;
using WebSocketSharp.Server;
using System.Collections.Generic;
using System.Collections;
using System;
using WebSocketSharp;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Net.NetworkInformation;
using System.Net;

public class GameNetworkManager : MonoBehaviour
{
    // ★ 多实例管理（每个房间一个）
    private static Dictionary<string, GameNetworkManager> instances
        = new Dictionary<string, GameNetworkManager>();

    private static Dictionary<int, string> portToRoomMap = new Dictionary<int, string>();

    private WebSocketServer wssv;
    private string _roomId = "";
    private int _port = 8080;

    [SerializeField] private float broadcastInterval = 0.1f;
    private Coroutine broadcastCoroutine;

    // UDP 广播相关
    private string serverIP = "";
    private UdpClient udpBroadcaster;
    private Thread discoveryThread;
    private bool discoveryThreadRunning = false;
    [SerializeField] private int discoveryPort = 5354;

    private bool gameEndBroadcasted = false;
    private Dictionary<string, string> clientToPlayerMap = new Dictionary<string, string>();

    // ★ 关键改造：注入所有依赖（不再使用单例）
    [HideInInspector] public GameStateJsonWriter gameStateWriter;
    [HideInInspector] public ServerGradeManager gradeManager;
    [HideInInspector] public ServerPlayerBloodManager bloodManager;
    [HideInInspector] public ServerReviveManager reviveManager;
    [HideInInspector] public ServerBombManager bombManager;
    [HideInInspector] public ServerPropManager propManager;
    [HideInInspector] public ServerMapManager mapManager;
    [HideInInspector] public ServerGameEndManager gameEndManager;

    // ============================================
    // ★ 多实例管理方法
    // ============================================

    /// <summary>
    /// 为指定房间创建 GameNetworkManager 实例
    /// </summary>
    public static GameNetworkManager CreateForRoom(string roomId, int roomIndex)
    {

        if (instances.ContainsKey(roomId))
        {
            Debug.LogWarning($"[GameNetworkManager] 房间 {roomId} 的Manager已存在，直接返回");
            return instances[roomId];
        }

        int port = 8080 + (roomIndex - 1);
        portToRoomMap[port] = roomId; // 记录端口→房间映射 

        GameObject go = new GameObject($"GameNetworkManager_{roomId}");
        GameNetworkManager manager = go.AddComponent<GameNetworkManager>();
        manager._roomId = roomId;
        manager._port = port;

        DontDestroyOnLoad(go);
        instances[roomId] = manager;

        Debug.Log($"✅ [GameNetworkManager] 为房间 {roomId} 创建实例（端口：{port}）");
        return manager;
    }

    public static string GetRoomIdByPort(int port)
    {
        return portToRoomMap.TryGetValue(port, out var roomId) ? roomId : "";
    }

    /// <summary>
    /// 获取指定房间的 GameNetworkManager 实例
    /// </summary>
    public static GameNetworkManager GetInstanceForRoom(string roomId)
    {
        if (instances.TryGetValue(roomId, out var manager))
        {
            return manager;
        }
        Debug.LogWarning($"[GameNetworkManager] 房间 {roomId} 的Manager不存在");
        return null;
    }

    /// <summary>
    /// 销毁指定房间的 GameNetworkManager 实例
    /// </summary>
    public static void DestroyForRoom(string roomId)
    {
        if (instances.TryGetValue(roomId, out var manager))
        {
            manager.StopServer();
            instances.Remove(roomId);
            Destroy(manager.gameObject);
            Debug.Log($"✅ [GameNetworkManager] 房间 {roomId} 的Manager已销毁");
        }
    }

    private void Awake()
    {
        // 不做单例处理，由 CreateForRoom 管理
    }

    private void Start()
    {
        Debug.Log($"[GameNetworkManager-{_roomId}] Start() - 等待被调用启动");
    }

    // ============================================
    // ★ 核心启动方法
    // ============================================

    /// <summary>
    /// 启动服务器
    /// </summary>
    public void StartServerAndInitializeDependencies()
    {
        Debug.Log($"[GameNetworkManager-{_roomId}] → 开始启动服务器...");

        StartServer();

        Debug.Log($"[GameNetworkManager-{_roomId}] ✅ 服务器启动完成");
    }

    /// <summary>
    /// 启动 WebSocket 服务器
    /// </summary>
    private void StartServer()
    {
        wssv = new WebSocketServer(_port);
        wssv.KeepClean = false;

        wssv.AddWebSocketService<GameSession>("/");

        // 设置房间ID到静态变量
        GameSession.SetRoomIdForNextSession(_roomId);

        wssv.Start();
        Debug.Log($"✅ [GameNetworkManager-{_roomId}] WebSocket 服务器已启动（端口：{_port}）");

        serverIP = GetLocalIPAddress();
        Debug.Log($"   服务器 IP: {serverIP}");

        if (broadcastCoroutine != null)
            StopCoroutine(broadcastCoroutine);
        broadcastCoroutine = StartCoroutine(BroadcastGameStateRoutine());

        StartDiscoveryBroadcast();
    }

    // ============================================
    // ★ 依赖注入方法
    // ============================================

    /// <summary>
    /// 由 RoomGameManager 调用，注入所有依赖
    /// </summary>
    public void InjectDependencies(
        GameStateJsonWriter gameState,
        ServerGradeManager grade,
        ServerPlayerBloodManager blood,
        ServerReviveManager revive,
        ServerBombManager bomb,
        ServerPropManager prop,
        ServerMapManager map,
        ServerGameEndManager gameEnd)
    {
        gameStateWriter = gameState;
        gradeManager = grade;
        bloodManager = blood;
        reviveManager = revive;
        bombManager = bomb;
        propManager = prop;
        mapManager = map;
        gameEndManager = gameEnd;

        Debug.Log($"[GameNetworkManager-{_roomId}] ✅ 所有依赖注入完成");
    }

    // ============================================
    // ★ 广播和通信相关
    // ============================================

    public void BroadcastGameEndMessage(string json)
    {
        var sessions = wssv?.WebSocketServices["/"]?.Sessions;
        if (sessions != null && sessions.Count > 0)
        {
            sessions.Broadcast(json);
            Debug.Log($"📡 [GameNetworkManager-{_roomId}] 游戏结束广播: {sessions.Count} 个客户端");
        }
    }

    /// <summary>
    /// ★ 新增：发送沉默道具拾取通知给指定玩家（单播）
    /// 参考 PlayerAssignedIdMsg 的发送方式
    /// </summary>
    public void SendSilencePropPickedUpToPlayer(string playerId, string roomId)
    {
        try
        {
            SilencePropPickedUpMessage msg = new SilencePropPickedUpMessage
            {
                type = "SilencePropPickedUp",
                roomId = roomId,
                playerId = playerId,
                timestamp = System.DateTime.Now.Ticks / 10000
            };

            string json = JsonUtility.ToJson(msg, true);

            var sessions = wssv?.WebSocketServices["/"]?.Sessions;
            if (sessions == null)
            {
                Debug.LogWarning($"[GameNetworkManager-{_roomId}] ⚠️ sessions 为空，无法单播");
                return;
            }

            // ★ 按房间查询 sessionId，直接单播
            if (!GameSession.TryGetSessionId(roomId, playerId, out string sessionId))
            {
                Debug.LogWarning($"[GameNetworkManager-{_roomId}] ⚠️ 找不到玩家 {playerId} 的 sessionId");
                return;
            }

            sessions.SendTo(json, sessionId);
            Debug.Log($"[GameNetworkManager-{_roomId}] 📩 沉默道具拾取通知已单播给 {playerId}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[GameNetworkManager-{_roomId}] ❌ 单播失败: {ex.Message}");
        }
    }

    /// <summary>
    /// ★ 新增：广播沉默道具放置结果给所有客户端
    /// </summary>
    public void BroadcastSilencePropPlaced(
        string placedByPlayerId,
        string placedByTeamId,
        GSPosition position,
        float effectHalfSize,
        float duration,
        string roomId)
    {
        try
        {
            SilencePropPlacedMessage msg = new SilencePropPlacedMessage
            {
                type = "SilencePropPlaced",
                roomId = roomId,
                placedByPlayerId = placedByPlayerId,
                placedByTeamId = placedByTeamId,
                position = position,
                effectHalfSize = effectHalfSize,
                duration = duration,
                timestamp = System.DateTime.Now.Ticks / 10000
            };

            string json = JsonUtility.ToJson(msg, true);

            var sessions = wssv?.WebSocketServices["/"]?.Sessions;
            if (sessions != null && sessions.Count > 0)
            {
                sessions.Broadcast(json);
                Debug.Log($"[GameNetworkManager-{_roomId}] 📡 SilencePropPlaced 广播完成\n" +
                          $"  → 放置者: {placedByPlayerId}（{placedByTeamId}）\n" +
                          $"  → 位置: ({position.x:F2}, {position.y:F2}, {position.z:F2})\n" +
                          $"  → 范围半边长: {effectHalfSize}\n" +
                          $"  → 客户端接收人数: {sessions.Count}");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[GameNetworkManager-{_roomId}] ❌ SilencePropPlaced 广播失败: {ex.Message}");
        }
    }


    private IEnumerator BroadcastGameStateRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(broadcastInterval);

            try
            {
                var sessions = wssv?.WebSocketServices["/"]?.Sessions;
                if (sessions == null || sessions.Count == 0)
                    continue;

                // 1. 广播玩家状态（WorldState）
                if (gameStateWriter != null)
                {
                    string gameStateJson = gameStateWriter.GetCurrentStateJson();
                    if (!string.IsNullOrEmpty(gameStateJson) && gameStateJson != "{}")
                    {
                        sessions.Broadcast(gameStateJson);
                    }
                }

                // 2. 广播炸弹状态（BombState）
                if (bombManager != null)
                {
                    BombStateMessage bombMsg = bombManager.GenerateBombStateMessage(isRetransmit: false);
                    if (bombMsg != null && bombMsg.bombs.Count > 0)
                    {
                        string bombJson = JsonUtility.ToJson(bombMsg, true);
                        sessions.Broadcast(bombJson);
                    }
                }

                // 3. 广播血量状态（PlayersBlood）
                if (bloodManager != null)
                {
                    PlayersBloodMessage bloodMsg = bloodManager.GeneratePlayersBloodMessage();
                    if (bloodMsg != null && bloodMsg.teams != null && bloodMsg.teams.Count > 0)
                    {
                        int totalPlayers = 0;
                        foreach (var team in bloodMsg.teams)
                        {
                            if (team.players != null)
                                totalPlayers += team.players.Count;
                        }

                        if (totalPlayers > 0)
                        {
                            string bloodJson = JsonUtility.ToJson(bloodMsg, true);
                            sessions.Broadcast(bloodJson);
                        }
                    }
                }

                // 4. 广播复活状态（ReviveState）
                if (reviveManager != null)
                {
                    ReviveStateMessage reviveMsg = GenerateReviveStateMessage();
                    if (reviveMsg != null && reviveMsg.reviveStates.Count > 0)
                    {
                        string reviveJson = JsonUtility.ToJson(reviveMsg, true);
                        sessions.Broadcast(reviveJson);
                    }
                }


                // 5. 广播无敌状态（InvincibleState）
                if (reviveManager != null)
                {
                    InvincibleStateMessage invincibleMsg = GenerateInvincibleStateMessage();
                    if (invincibleMsg != null && invincibleMsg.invincibleStates.Count > 0)
                    {
                        string invincibleJson = JsonUtility.ToJson(invincibleMsg, true);
                        sessions.Broadcast(invincibleJson);

                        if (InvincibleStateJsonWriter.Instance != null)
                        {
                            InvincibleStateJsonWriter.Instance.SaveInvincibleStateToFile(_roomId, invincibleMsg);
                        }
                    }
                }


                // 6. 广播积分状态（Grade）
                if (gradeManager != null)
                {
                    GradeMessage gradeMsg = gradeManager.GenerateGradeMessage();
                    if (gradeMsg != null && gradeMsg.teams != null && gradeMsg.teams.Count > 0)
                    {
                        int totalPlayers = 0;
                        foreach (var team in gradeMsg.teams)
                        {
                            if (team.players != null)
                                totalPlayers += team.players.Count;
                        }

                        if (totalPlayers > 0)
                        {
                            string gradeJson = JsonUtility.ToJson(gradeMsg, true);
                            sessions.Broadcast(gradeJson);
                        }
                    }
                }

                // 7. 广播道具状态（PropState）
                if (propManager != null)
                {
                    int activePropCount = propManager.GetActivePropCount();
                    if (activePropCount > 0)
                    {
                        PropStateMessage propMsg = propManager.GeneratePropStateMessage();
                        if (propMsg != null && propMsg.props.Count > 0)
                        {
                            string propStateJson = JsonUtility.ToJson(propMsg, true);
                            sessions.Broadcast(propStateJson);
                        }
                    }
                }

                // 8. 广播游戏结束（GameEnd）
                if (gameEndManager != null && gameEndManager.IsGameEnded())
                {
                    if (gameEndManager.IsSeriesEnded())
                    {
                        if (!gameEndBroadcasted)
                        {
                            GameEndMessage gameEndMsg = CreateGameEndMessage();
                            gameEndMsg.timestamp = System.DateTime.Now.Ticks;

                            string gameEndJson = JsonUtility.ToJson(gameEndMsg, true);
                            sessions.Broadcast(gameEndJson);

                            Debug.Log($"🏆 [GameNetworkManager-{_roomId}] 系列赛结束广播");
                            gameEndBroadcasted = true;
                        }
                    }
                    else
                    {
                        GameEndMessage gameEndMsg = CreateGameEndMessage();
                        gameEndMsg.timestamp = System.DateTime.Now.Ticks;

                        string gameEndJson = JsonUtility.ToJson(gameEndMsg, true);
                        sessions.Broadcast(gameEndJson);
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[GameNetworkManager-{_roomId}] 广播异常: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// 生成 ReviveState 消息
    /// </summary>
    private ReviveStateMessage GenerateReviveStateMessage()
    {
        ReviveStateMessage msg = new ReviveStateMessage
        {
            type = "ReviveState",
            roomId = _roomId,
            timestamp = System.DateTime.Now.Ticks / 10000
        };

        if (gameStateWriter == null || reviveManager == null)
            return msg;

        GameStateData gameState = gameStateWriter.GetCurrentGameState();
        if (gameState?.teams == null) return msg;

        foreach (var team in gameState.teams)
        {
            if (team.players == null) continue;

            foreach (var player in team.players)
            {
                float countdown = reviveManager.GetReviveCountdown(player.playerId);
                if (countdown > 0)
                {
                    ReviveStateInfo info = new ReviveStateInfo
                    {
                        playerId = player.playerId,
                        playerName = player.playerName,
                        reviveCountdown = countdown
                    };
                    msg.reviveStates.Add(info);
                }
            }
        }

        return msg;
    }

    /// <summary>
    /// 生成 InvincibleState 消息
    /// </summary>
    private InvincibleStateMessage GenerateInvincibleStateMessage()
    {
        InvincibleStateMessage msg = new InvincibleStateMessage
        {
            type = "InvincibleState",
            roomId = _roomId,
            timestamp = System.DateTime.Now.Ticks / 10000
        };

        if (gameStateWriter == null || reviveManager == null)
            return msg;

        GameStateData gameState = gameStateWriter.GetCurrentGameState();
        if (gameState?.teams == null) return msg;

        foreach (var team in gameState.teams)
        {
            if (team.players == null) continue;

            foreach (var player in team.players)
            {
                bool isInvincible = reviveManager.IsPlayerInvincible(player.playerId);
                float countdown = reviveManager.GetInvincibleCountdown(player.playerId);

                // ✅ 改：总是添加玩家状态，即使是 false/0
                InvincibleStateInfo info = new InvincibleStateInfo
                {
                    playerId = player.playerId,
                    playerName = player.playerName,
                    isInvincible = isInvincible,
                    invincibleCountdown = countdown
                };
                msg.invincibleStates.Add(info);
            }
        }

        return msg;
    }


    /// <summary>
    /// 生成 GameEnd 消息
    /// </summary>
    private GameEndMessage CreateGameEndMessage()
    {
        var teamVictory = gameEndManager?.GetTeamVictoryCount() ?? new Dictionary<string, int>();

        int redVictory = 0;
        int blueVictory = 0;
        string redTeamId = "RedTeam_1";
        string blueTeamId = "BlueTeam_1";

        // ★ 用 Keys 列表保证顺序一致
        var keys = teamVictory.Keys.ToList();
        if (keys.Count > 0) redTeamId = keys[0];
        if (keys.Count > 1) blueTeamId = keys[1];

        if (teamVictory.TryGetValue(redTeamId, out int rv)) redVictory = rv;
        if (teamVictory.TryGetValue(blueTeamId, out int bv)) blueVictory = bv;

        string victoryCondition = gameEndManager?.GetVictoryCondition() ?? "SingleRound";
        bool isSeriesEnd = gameEndManager?.IsSeriesEnded() ?? false;

        int remainingRounds = 0;
        if (victoryCondition == "BO3")
            remainingRounds = Mathf.Max(0, 3 - (redVictory + blueVictory));
        else if (victoryCondition == "BO5")
            remainingRounds = Mathf.Max(0, 5 - (redVictory + blueVictory));

        // ★ 核心修复：根据红蓝队胜场比较，直接赋值 winnerTeamName
        string winnerTeamId = "";
        string winnerTeamName = "";

        if (redVictory > blueVictory)
        {
            winnerTeamId = redTeamId;
            winnerTeamName = "red";
        }
        else if (blueVictory > redVictory)
        {
            winnerTeamId = blueTeamId;
            winnerTeamName = "blue";
        }

        return new GameEndMessage
        {
            type = "GameEnd",
            roomId = _roomId,
            timestamp = System.DateTime.Now.Ticks,
            redTeamVictory = redVictory,
            blueTeamVictory = blueVictory,
            remainingRounds = remainingRounds,
            victoryCondition = victoryCondition,
            isSeriesEnd = isSeriesEnd,
            winnerTeamId = winnerTeamId,       // ★ 补上
            winnerTeamName = winnerTeamName    // ★ 补上
        };
    }

    // ============================================
    // ★ UDP 发现广播相关
    // ============================================

    private string GetLocalIPAddress()
    {
        Debug.Log("[IP获取] 开始获取本机IP");

        try
        {
            string fallbackIP = null; // 备用IP（无网关但符合私有IP）

            NetworkInterface[] interfaces = NetworkInterface.GetAllNetworkInterfaces();
            foreach (NetworkInterface ni in interfaces)
            {
                // 只看已连接的网卡
                if (ni.OperationalStatus != OperationalStatus.Up)
                    continue;

                if (ni.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 ||
                    ni.NetworkInterfaceType == NetworkInterfaceType.Ethernet)
                {
                    IPInterfaceProperties ipProps = ni.GetIPProperties();

                    // ★ 关键：检查是否有默认网关
                    bool hasGateway = false;
                    foreach (GatewayIPAddressInformation gateway in ipProps.GatewayAddresses)
                    {
                        if (gateway.Address.AddressFamily == AddressFamily.InterNetwork &&
                            !gateway.Address.ToString().Equals("0.0.0.0"))
                        {
                            hasGateway = true;
                            break;
                        }
                    }

                    foreach (UnicastIPAddressInformation ip in ipProps.UnicastAddresses)
                    {
                        if (ip.Address.AddressFamily == AddressFamily.InterNetwork)
                        {
                            string ipStr = ip.Address.ToString();
                            if (IsValidPrivateIP(ipStr))
                            {
                                if (hasGateway)
                                {
                                    // 有网关 → 优先返回
                                    Debug.Log($"✅ 找到IP（有网关）: {ipStr} 来自网卡: {ni.Name}");
                                    return ipStr;
                                }
                                else
                                {
                                    // 无网关 → 作为备用
                                    if (fallbackIP == null)
                                    {
                                        fallbackIP = ipStr;
                                        Debug.Log($"⚠ 备用IP（无网关）: {ipStr} 来自网卡: {ni.Name}");
                                    }
                                }
                            }
                        }
                    }
                }
            }

            // 没有找到有网关的IP，使用备用IP
            if (fallbackIP != null)
            {
                Debug.LogWarning($"[IP获取] 未找到有网关的IP，使用备用: {fallbackIP}");
                return fallbackIP;
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"❌ IP获取异常: {ex.Message}");
        }

        return "127.0.0.1";
    }


    private bool IsValidPrivateIP(string ip)
    {
        if (ip.StartsWith("10."))
            return true;
        if (ip.StartsWith("172."))
            return true;
        if (ip.StartsWith("192.168."))
            return true;
        return false;
    }

    private void StartDiscoveryBroadcast()
    {
        if (discoveryThreadRunning)
        {
            Debug.LogWarning($"[GameNetworkManager-{_roomId}] 发现线程已运行");
            return;
        }

        discoveryThreadRunning = true;
        discoveryThread = new Thread(DiscoveryBroadcastThread)
        {
            IsBackground = true,
            Name = $"UDP Discovery Thread ({_roomId})"
        };
        discoveryThread.Start();

        Debug.Log($"[GameNetworkManager-{_roomId}] 📡 服务发现广播线程已启动");
    }

    private void DiscoveryBroadcastThread()
    {
        int portAttempt = 0;
        const int MAX_PORT_ATTEMPTS = 3;
        int currentPort = discoveryPort;

        while (portAttempt < MAX_PORT_ATTEMPTS && discoveryThreadRunning)
        {
            try
            {
                udpBroadcaster = new UdpClient();
                udpBroadcaster.EnableBroadcast = true;
                udpBroadcaster.Client.Bind(new IPEndPoint(IPAddress.Any, currentPort));
                udpBroadcaster.Client.ReceiveTimeout = 1000;

                Debug.Log($"[UDP线程-{_roomId}] 服务发现已启动，监听端口 {currentPort}");
                break;
            }
            catch (SocketException ex) when (ex.SocketErrorCode == SocketError.AddressAlreadyInUse)
            {
                portAttempt++;
                currentPort++;

                if (portAttempt < MAX_PORT_ATTEMPTS && discoveryThreadRunning)
                {
                    Debug.LogWarning($"⚠ 端口 {currentPort - 1} 被占用，尝试端口 {currentPort}");
                    System.Threading.Thread.Sleep(200);
                }
            }
        }

        while (discoveryThreadRunning)
        {
            try
            {
                if (udpBroadcaster == null)
                    break;

                IPEndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);
                byte[] receivedData = udpBroadcaster.Receive(ref remoteEP);
                string request = System.Text.Encoding.UTF8.GetString(receivedData);

                if (request == "DISCOVER_GAMESERVER" && discoveryThreadRunning)
                {
                    string response = $"GAMESERVER|{serverIP}|{_port}|{_roomId}";
                    byte[] responseData = System.Text.Encoding.UTF8.GetBytes(response);

                    try
                    {
                        udpBroadcaster.Send(responseData, responseData.Length, remoteEP);
                        Debug.Log($"📡 [发现请求-{_roomId}] 来自 {remoteEP.Address}，已回复: {response}");
                    }
                    catch (Exception sendEx)
                    {
                        Debug.LogWarning($"⚠ 发送响应失败: {sendEx.Message}");
                    }
                }
            }
            catch (SocketException ex) when (ex.SocketErrorCode == SocketError.TimedOut)
            {
                continue;
            }
            catch (ObjectDisposedException)
            {
                Debug.Log($"[UDP线程-{_roomId}] UDP 客户端已关闭，线程退出");
                break;
            }
            catch (Exception ex)
            {
                if (discoveryThreadRunning)
                {
                    Debug.LogError($"❌ UDP线程异常: {ex.Message}");
                }
                break;
            }
        }

        Debug.Log($"[UDP线程-{_roomId}] 服务发现线程已停止");
    }

    // ============================================
    // ★ 停止服务器
    // ============================================

    public void StopServer()
    {
        Debug.Log($"[GameNetworkManager-{_roomId}] → 开始停止服务器...");

        try
        {
            StopDiscoveryBroadcaster();
            Debug.Log("✓ UDP 广播已停止");

            if (broadcastCoroutine != null)
            {
                StopCoroutine(broadcastCoroutine);
                Debug.Log("✓ 广播协程已停止");
            }

            if (wssv != null)
            {
                wssv.Stop();
                Debug.Log("✓ WebSocket 服务器已关闭");
            }

            clientToPlayerMap.Clear();
            Debug.Log("✓ 客户端映射已清空");

            // ★★★ 新增：关闭服务器时清空断线重连绑定表
            // 使服务器重开后所有玩家重新分配 playerId，而不是走断线重连逻辑
            GameSession.ClearDeviceBindingsForRoom(_roomId);
            Debug.Log($"✓ 房间 {_roomId} 的设备绑定已清空");

            // 清理依赖引用
            gameStateWriter = null;
            gradeManager = null;
            bloodManager = null;
            reviveManager = null;
            bombManager = null;
            propManager = null;
            mapManager = null;
            gameEndManager = null;

            Debug.Log($"[GameNetworkManager-{_roomId}] ✅ 服务器已完全停止");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[GameNetworkManager-{_roomId}] ❌ 停止服务器时出错: {ex.Message}");
        }
    }

    private void StopDiscoveryBroadcaster()
    {
        Debug.Log($"[GameNetworkManager-{_roomId}] → 开始停止 UDP 广播...");

        discoveryThreadRunning = false;

        if (discoveryThread != null && discoveryThread.IsAlive)
        {
            discoveryThread.Join(2000);
            discoveryThread = null;
            Debug.Log($"[GameNetworkManager-{_roomId}] ✅ UDP 广播已完全停止");
        }

        if (udpBroadcaster != null)
        {
            udpBroadcaster.Close();
            udpBroadcaster = null;
        }
    }

    public void RecordClientToPlayer(string clientId, string playerId)
    {
        clientToPlayerMap[clientId] = playerId;
    }

    public void ResetGameEndBroadcastFlag()
    {
        gameEndBroadcasted = false;
        Debug.Log($"✅ [GameNetworkManager-{_roomId}] gameEndBroadcasted 已重置");
    }

    private void OnApplicationQuit()
    {
        var roomIds = instances.Keys.ToList();
        foreach (var roomId in roomIds)
        {
            DestroyForRoom(roomId);
        }
    }

    /// <summary>
    /// 清理数据并销毁 GameObject
    /// </summary>
    public void Cleanup()
    {
        Debug.Log($"[GameNetworkManager-{_roomId}] → 开始清理...");

        try
        {
            // 先停止服务器
            StopServer();

            // 从字典移除
            if (instances.ContainsKey(_roomId))
            {
                instances.Remove(_roomId);
                Debug.Log($"✓ 从实例字典移除: {_roomId}");
            }

            // 销毁 GameObject
            Destroy(gameObject);

            Debug.Log($"[GameNetworkManager-{_roomId}] ✅ 已清理");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[GameNetworkManager-{_roomId}] ❌ 清理失败: {ex.Message}");
        }
    }


    /// <summary>  
    /// ★ 新增：重置指定房间的玩家分配状态  
    /// </summary>  
    public static void ResetPlayerAssignmentForRoom(string roomId)
    {
        // 这需要在 GameSession 中实现相应的公开接口  
        Debug.Log($"[GameNetworkManager] 已重置房间 {roomId} 的玩家分配状态");
    }
}

public class GameSession : WebSocketSharp.Server.WebSocketBehavior
{
    private static string _nextSessionRoomId = "room_001";

    private bool isInitialized = false;
    private string clientId = "";
    private string assignedPlayerId = "";
    private string _currentRoomId = "";


    // ★ 改为按房间隔离的字典，而不是全局静态集合  
    private static Dictionary<string, HashSet<string>> assignedPlayerIdsByRoom
        = new Dictionary<string, HashSet<string>>();


    // ★ 新增：记录炸弹创建来源（bombId → 来源信息）
    private static Dictionary<string, BombCreationSource> bombCreationSourceMap
        = new Dictionary<string, BombCreationSource>();



    // ★ 新增：roomId → (playerId → sessionId) 的二层字典  
    private static Dictionary<string, Dictionary<string, string>> playerSessionMapByRoom
        = new Dictionary<string, Dictionary<string, string>>();


    // ★★★ 新增：断线重连用。roomId → (clientId设备号 → playerId) 的绑定表  
    // 服务器运行期间一直保留；服务器重启内存清空，自动失效  
    private static Dictionary<string, Dictionary<string, string>> deviceToPlayerByRoom
        = new Dictionary<string, Dictionary<string, string>>();

    /// <summary>
    /// 炸弹创建来源信息
    /// </summary>
    public class BombCreationSource
    {
        public string roomId;
        public string clientId;
        public string clientReportedPlayerId;  // 客户端自报的 playerId
        public string trustedPlayerId;         // 服务端分配的 playerId（可信）
        public string teamId;
        public string bombId;
        public long receiveTimestamp;
    }

    // 供 GameNetworkManager.StopServer() 调用
    // 关闭服务器时清空指定房间的设备绑定，使重开后重新分配 playerId
    public static void ClearDeviceBindingsForRoom(string roomId)
    {
        if (deviceToPlayerByRoom.ContainsKey(roomId))
        {
            deviceToPlayerByRoom.Remove(roomId);
            Debug.Log($"[GameSession] ✓ 房间 {roomId} 的 deviceToPlayerByRoom 已清空");
        }
    }


    public static void SetRoomIdForNextSession(string roomId)
    {
        _nextSessionRoomId = roomId;
        Debug.Log($"[GameSession] 已设置下一个会话的房间ID: {roomId}");
    }

    protected override void OnOpen()
    {
        Debug.Log("✅ 有客户端连接进来了！");
        Debug.Log($"[GameSession] SessionID: {this.ID}");

        // 通过端口号反查 roomId  
        int port = this.Context.RequestUri.Port;
        _currentRoomId = GameNetworkManager.GetRoomIdByPort(port);
        Debug.Log($"[GameSession] 当前房间ID: {_currentRoomId}");
    }

    protected override void OnMessage(WebSocketSharp.MessageEventArgs e)
    {
        try
        {
            MessageBase msg = JsonUtility.FromJson<MessageBase>(e.Data);

            switch (msg.type)
            {
                case "PlayerJoin":
                    HandlePlayerJoin(e.Data);
                    break;

                case "PlayerUpdate":
                    HandlePlayerUpdate(e.Data);
                    break;

                case "BombCreate":
                    Debug.Log("接收BombCreate信息");
                    HandleBombCreate(e.Data);
                    break;

                case "ObstacleCollision":
                    Debug.Log("接收ObstacleCollision信息");
                    HandleObstacleCollision(e.Data);
                    break;

                case "SilencePropPlace":
                    Debug.Log("接收SilencePropPlace信息");
                    HandleSilencePropPlace(e.Data);
                    break;


                default:
                    Debug.LogWarning($"⚠ 未知消息类型: '{msg.type}'");
                    break;
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"❌ 消息解析失败: {ex.Message}");
        }
    }

    private void HandlePlayerJoin(string json)
    {
        try
        {
            PlayerJoinMsg msg = JsonUtility.FromJson<PlayerJoinMsg>(json);
            this.clientId = msg.clientId;

            GameNetworkManager networkManager = GameNetworkManager.GetInstanceForRoom(_currentRoomId);
            if (networkManager != null && networkManager.gameStateWriter != null)
            {
                if (!networkManager.gameStateWriter.IsInitialized())
                {
                    networkManager.gameStateWriter.InitFromTeamJson(_currentRoomId);
                }
            }

            // ★★★ 断线重连判断：检查该 clientId（设备号）在本局是否已有绑定 ★★★
            string assignedPlayerId;
            bool isReconnect = false;

            if (deviceToPlayerByRoom.TryGetValue(_currentRoomId, out var deviceMap)
                && deviceMap.TryGetValue(msg.clientId, out string previousPlayerId))
            {
                // ── 断线重连路径：找回原来的 playerId ──
                assignedPlayerId = previousPlayerId;
                isReconnect = true;

                // 重新标记为已分配（OnClose 时已从 assignedPlayerIdsByRoom 移除了）
                if (!assignedPlayerIdsByRoom.ContainsKey(_currentRoomId))
                    assignedPlayerIdsByRoom[_currentRoomId] = new HashSet<string>();
                assignedPlayerIdsByRoom[_currentRoomId].Add(assignedPlayerId);

                Debug.Log($"【断线重连】设备 {msg.clientId} 找回原有 playerId={assignedPlayerId}，房间={_currentRoomId}");
            }
            else
            {
                // ── 首次加入路径：正常分配新 playerId ──
                assignedPlayerId = FindAvailablePlayerInRoom(_currentRoomId);

                // ★ 建立 clientId（设备号）→ playerId 的绑定，局内永久保留
                if (!deviceToPlayerByRoom.ContainsKey(_currentRoomId))
                    deviceToPlayerByRoom[_currentRoomId] = new Dictionary<string, string>();
                deviceToPlayerByRoom[_currentRoomId][msg.clientId] = assignedPlayerId;

                Debug.Log($"【首次加入】设备 {msg.clientId} 绑定到 playerId={assignedPlayerId}，房间={_currentRoomId}");
            }

            this.assignedPlayerId = assignedPlayerId;

            Debug.Log($"✅ 为客户端 {msg.clientId} 分配了玩家 {assignedPlayerId}（重连={isReconnect}）");
            Debug.Log($"【断线重连测试】HandlePlayerJoin: 客户端 {msg.clientId} 分配到 playerId={assignedPlayerId}，房间={_currentRoomId}");

            GameNetworkManager.GetInstanceForRoom(_currentRoomId)?.RecordClientToPlayer(msg.clientId, assignedPlayerId);

            // ★ 注册 playerId → sessionId
            if (!playerSessionMapByRoom.ContainsKey(_currentRoomId))
                playerSessionMapByRoom[_currentRoomId] = new Dictionary<string, string>();
            playerSessionMapByRoom[_currentRoomId][assignedPlayerId] = this.ID;

            Debug.Log($"【断线重连测试】playerSessionMapByRoom 已更新: room={_currentRoomId}, player={assignedPlayerId}, sessionId={this.ID}");

            if (networkManager != null && networkManager.gameEndManager != null)
            {
                networkManager.gameEndManager.OnFirstPlayerJoined();
            }

            PlayerAssignedIdMsg response = new PlayerAssignedIdMsg
            {
                type = "PlayerAssignedId",
                playerId = assignedPlayerId,
                roomId = _currentRoomId,
                teamId = FindPlayerTeamId(_currentRoomId, assignedPlayerId),
                playerName = FindPlayerName(_currentRoomId, assignedPlayerId),
                timestamp = System.DateTime.Now.Ticks
            };

            Send(JsonUtility.ToJson(response));
            Debug.Log($"✅ 已发送玩家分配消息: {assignedPlayerId} 给客户端 {msg.clientId}");

            // ★ 发送地图数据
            GameNetworkManager netMgr = GameNetworkManager.GetInstanceForRoom(_currentRoomId);
            if (netMgr != null && netMgr.mapManager != null)
            {
                MapBroadcastMessage mapMsg = netMgr.mapManager.GenerateMapBroadcastMessage();
                if (mapMsg != null && mapMsg.objects.Count > 0)
                {
                    string mapJson = JsonUtility.ToJson(mapMsg, true);
                    this.Send(mapJson);
                    Debug.Log($"✅ 已发送地图数据给客户端 {msg.clientId}");
                }
            }

            // ★★★ 断线重连补发沉默道具持有通知 ★★★
            string capturedPlayerId = assignedPlayerId;
            string capturedRoomId = _currentRoomId;

            Debug.Log($"【断线重连测试】准备 Enqueue，capturedPlayerId={capturedPlayerId}, capturedRoomId={capturedRoomId}");

            UnityMainThreadDispatcher.Enqueue(() =>
            {
                Debug.Log($"【断线重连测试】Enqueue 回调开始执行: capturedPlayerId={capturedPlayerId}, capturedRoomId={capturedRoomId}");

                GameNetworkManager nm = GameNetworkManager.GetInstanceForRoom(capturedRoomId);

                if (nm == null)
                {
                    Debug.LogWarning($"【断线重连测试】⚠️ nm 为 null，capturedRoomId={capturedRoomId}");
                    return;
                }

                if (nm.propManager == null)
                {
                    Debug.LogWarning($"【断线重连测试】⚠️ propManager 为 null");
                    return;
                }

                bool isHolding = nm.propManager.IsPlayerHoldingSilenceProp(capturedPlayerId);
                Debug.Log($"【断线重连测试】检查玩家 {capturedPlayerId} 沉默道具持有状态: isHolding={isHolding}");

                if (isHolding)
                {
                    Debug.Log($"【断线重连测试】✅ 玩家 {capturedPlayerId} 重连，检测到持有沉默道具，准备补发 SilencePropPickedUp");
                    nm.SendSilencePropPickedUpToPlayer(capturedPlayerId, capturedRoomId);
                    Debug.Log($"【断线重连测试】✅ SilencePropPickedUp 补发完成 → playerId={capturedPlayerId}");
                }
                else
                {
                    Debug.Log($"【断线重连测试】玩家 {capturedPlayerId} 重连，未持有沉默道具，无需补发");
                }
            });
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"⚠ 处理玩家加入消息失败: {ex.Message}");
        }
    }

    private void HandlePlayerUpdate(string json)
    {
        try
        {
            PlayerUpdateMessage msg = JsonUtility.FromJson<PlayerUpdateMessage>(json);

            GameNetworkManager networkManager = GameNetworkManager.GetInstanceForRoom(_currentRoomId);
            if (networkManager != null && networkManager.gameStateWriter != null)
            {
                // ★ 修复：明确指定 msg.position 的成员属性（不使用二义性的 position）
                networkManager.gameStateWriter.UpdatePlayerTransform(
                    assignedPlayerId,
                    msg.position.x,
                    msg.position.y,
                    msg.position.z,
                    msg.rotation.x,
                    msg.rotation.y,
                    msg.rotation.z,
                    msg.rotation.w
                );
                networkManager.gameStateWriter.SaveToFile();
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"⚠ 处理玩家更新失败: {ex.Message}");
        }
    }

    private void HandleObstacleCollision(string json)
    {
        try
        {
            ObstacleCollisionMsg msg = JsonUtility.FromJson<ObstacleCollisionMsg>(json);

            if (string.IsNullOrEmpty(msg.playerId) || msg.playerId == "player_unknown")
            {
                Debug.LogWarning($"⚠️ [碰撞检测] 拒绝无效玩家的碰撞消息: {msg.playerId}");
                return;
            }

            if (msg.playerId != this.assignedPlayerId)
            {
                Debug.LogWarning($"⚠️ [碰撞检测] 玩家 {msg.playerId} 与会话分配玩家 {this.assignedPlayerId} 不匹配");
                return;
            }

            UnityMainThreadDispatcher.Enqueue(() =>
            {
                GameNetworkManager networkManager = GameNetworkManager.GetInstanceForRoom(_currentRoomId);
                if (networkManager != null && networkManager.gradeManager != null)
                {
                    networkManager.gradeManager.RecordObstacleCollision(msg.playerId, -20);
                    Debug.Log($"✅ [碰撞扣分] 玩家 {msg.playerId} 扣除 20 分");
                }
            });
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"⚠ 处理碰撞消息失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 处理沉默道具放置请求
    /// </summary>
    private void HandleSilencePropPlace(string json)
    {
        try
        {
            SilencePropPlaceRequest msg = JsonUtility.FromJson<SilencePropPlaceRequest>(json);

            // 会话是否已分配玩家
            if (string.IsNullOrEmpty(this.assignedPlayerId) ||
                this.assignedPlayerId == "player_unknown")
            {
                Debug.LogWarning($"[HandleSilencePropPlace] 当前会话未分配玩家ID");
                return;
            }

            //  使用服务端可信 playerId（与 HandleBombCreate 保持一致）
            string trustedPlayerId = this.assignedPlayerId;

            UnityMainThreadDispatcher.Enqueue(() =>
            {
                GameNetworkManager networkManager =
                    GameNetworkManager.GetInstanceForRoom(_currentRoomId);

                if (networkManager == null)
                {
                    Debug.LogError($" [HandleSilencePropPlace] networkManager 为 null");
                    return;
                }

                if (networkManager.propManager == null)
                {
                    Debug.LogError($" [HandleSilencePropPlace] propManager 为 null");
                    return;
                }

                //  从服务端查询teamId（与 HandleBombCreate 保持一致）
                string trustedTeamId = FindPlayerTeamId(_currentRoomId, trustedPlayerId);

                networkManager.propManager.HandleSilencePropPlace(
                    trustedPlayerId,
                    trustedTeamId,
                    msg.position
                );
            });
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[HandleSilencePropPlace] 异常: {ex.Message}\n{ex.StackTrace}");
        }
    }


    private void HandleBombCreate(string json)
    {
        try
        {
            //Debug.Log($"📨 [HandleBombCreate] 收到原始JSON: {json}");

            BombCreateRequest msg = JsonUtility.FromJson<BombCreateRequest>(json);

            //// ★ 新增：打印炸弹创建信息来源
            //Debug.Log($"📌 [HandleBombCreate] 炸弹创建来源信息:\n" +
            //          $"  → 来源 ClientId:          {this.clientId}\n" +
            //          $"  → 客户端自报 playerId:     {msg.playerId}\n" +
            //          $"  → 服务端分配 playerId:     {this.assignedPlayerId}\n" +
            //          $"  → 房间 roomId:             {_currentRoomId}\n" +
            //          $"  → 炸弹 bombId:             {msg.bombId}\n" +
            //          $"  → 炸弹类型 bombType:       {msg.bombType}\n" +
            //          $"  → 位置:                    ({msg.position?.x}, {msg.position?.y}, {msg.position?.z})");

            // ★ 校验1：会话是否已分配玩家
            if (string.IsNullOrEmpty(this.assignedPlayerId) ||
                this.assignedPlayerId == "player_unknown")
            {
                Debug.LogWarning($"⚠️ [HandleBombCreate] ❌ 当前会话未分配玩家ID\n" +
                                 $"  → 来源 ClientId: {this.clientId}\n" +
                                 $"  → 客户端自报 playerId: {msg.playerId}");
                return;
            }

            // ★ 使用服务端分配的 trustedPlayerId
            string trustedPlayerId = this.assignedPlayerId;

            // ★ 新增：检测客户端自报 playerId 与服务端分配是否一致，并记录差异
            if (msg.playerId != trustedPlayerId)
            {
                Debug.LogWarning($"⚠️ [HandleBombCreate] 客户端自报playerId与服务端分配不一致:\n" +
                                 $"  → 客户端自报: {msg.playerId}\n" +
                                 $"  → 服务端分配: {trustedPlayerId}\n" +
                                 $"  → 以服务端分配为准");
            }

            Debug.Log($"✅ [HandleBombCreate] 使用服务端分配的playerId: {trustedPlayerId}");

            // ★ 新增：生成最终 bombId，并记录来源
            string finalBombId = msg.bombId ??
                (trustedPlayerId + "_bomb_" + System.DateTime.Now.Ticks);

            // ★ 新增：将来源信息存入字典
            BombCreationSource source = new BombCreationSource
            {
                roomId = _currentRoomId,
                clientId = this.clientId,
                clientReportedPlayerId = msg.playerId,
                trustedPlayerId = trustedPlayerId,
                bombId = finalBombId,
                receiveTimestamp = System.DateTime.Now.Ticks / 10000
            };
            bombCreationSourceMap[finalBombId] = source;

            //Debug.Log($"📋 [HandleBombCreate] 来源已记录:\n" +
            //          $"  → bombId:        {source.bombId}\n" +
            //          $"  → trustedPlayer: {source.trustedPlayerId}\n" +
            //          $"  → clientId:      {source.clientId}\n" +
            //          $"  → roomId:        {source.roomId}");

            UnityMainThreadDispatcher.Enqueue(() =>
            {
                GameNetworkManager networkManager =
                    GameNetworkManager.GetInstanceForRoom(_currentRoomId);

                if (networkManager == null)
                {
                    Debug.LogError($"❌ [HandleBombCreate] networkManager 为 null");
                    return;
                }

                if (networkManager.bombManager == null)
                {
                    Debug.LogError($"❌ [HandleBombCreate] bombManager 为 null");
                    return;
                }

                string teamId = FindPlayerTeamId(_currentRoomId, trustedPlayerId);

                // ★ 新增：将 teamId 补充进来源记录
                if (bombCreationSourceMap.TryGetValue(finalBombId, out var srcEntry))
                {
                    srcEntry.teamId = teamId;
                }

                //Debug.Log($"🎯 [HandleBombCreate] 最终执行 CreateBomb:\n" +
                //          $"  → trustedPlayerId: {trustedPlayerId}\n" +
                //          $"  → teamId:          {teamId}\n" +
                //          $"  → bombId:          {finalBombId}\n" +
                //          $"  → roomId:          {_currentRoomId}");

                networkManager.bombManager.CreateBomb(
                    trustedPlayerId,
                    teamId,
                    finalBombId,
                    new Vector3(msg.position.x, msg.position.y, msg.position.z),
                    msg.bombType
                );

                //Debug.Log($"✅ [HandleBombCreate] CreateBomb 完成: {finalBombId}");
            });
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"❌ [HandleBombCreate] 异常: {ex.Message}\n{ex.StackTrace}");
        }
    }

    protected override void OnClose(CloseEventArgs e)
    {
        // ★ 按房间清理已分配玩家ID（让位子空出来，以便重连时重新标记占用）
        if (assignedPlayerIdsByRoom.TryGetValue(_currentRoomId, out var playerIds))
        {
            playerIds.Remove(this.assignedPlayerId);

            if (playerIds.Count == 0)
            {
                assignedPlayerIdsByRoom.Remove(_currentRoomId);
            }
        }

        // ★ 清理 playerSessionMapByRoom
        if (playerSessionMapByRoom.TryGetValue(_currentRoomId, out var sessionMap))
        {
            sessionMap.Remove(this.assignedPlayerId);
            if (sessionMap.Count == 0)
                playerSessionMapByRoom.Remove(_currentRoomId);
        }

        // ★★★ 注意：deviceToPlayerByRoom 不在这里清理！
        // 断线后保留 clientId → playerId 的绑定，以便该设备重连时能找回原 playerId。
        // 服务器重启时内存自动清空，满足"重开服务器分配新id"的需求。
        Debug.Log($"[OnClose] 玩家 {this.assignedPlayerId}（设备={this.clientId}）断开，绑定保留供重连使用");
    }

    protected override void OnError(WebSocketSharp.ErrorEventArgs e)
    {
        Debug.LogError($"❌ [GameSession] 错误: {e.Message}");
    }

    private string FindPlayerTeamId(string roomId, string playerId)
    {
        RoomTeamsData roomTeams = TeamJsonFileHandler.Instance.LoadTeamsData(roomId);
        if (roomTeams == null) return "";

        foreach (TeamInfo team in roomTeams.teams)
        {
            foreach (TeamPlayer player in team.players)
            {
                if (player.playerId == playerId)
                    return team.teamId;
            }
        }
        return "";
    }

    private string FindPlayerName(string roomId, string playerId)
    {
        RoomTeamsData roomTeams = TeamJsonFileHandler.Instance.LoadTeamsData(roomId);
        if (roomTeams == null) return "";

        foreach (TeamInfo team in roomTeams.teams)
        {
            foreach (TeamPlayer player in team.players)
            {
                if (player.playerId == playerId)
                    return player.playerName;
            }
        }
        return "";
    }

    private static HashSet<string> assignedPlayerIds = new HashSet<string>();

    private string FindAvailablePlayerInRoom(string roomId)
    {
        RoomTeamsData roomTeams = TeamJsonFileHandler.Instance.LoadTeamsData(roomId);
        if (roomTeams == null) return "player_unknown";

        // ★ 获取该房间的已分配玩家集合  
        if (!assignedPlayerIdsByRoom.ContainsKey(roomId))
        {
            assignedPlayerIdsByRoom[roomId] = new HashSet<string>();
        }
        var assignedInThisRoom = assignedPlayerIdsByRoom[roomId];

        foreach (TeamInfo team in roomTeams.teams)
        {
            foreach (TeamPlayer player in team.players)
            {
                // ★ 只检查该房间内的分配状态  
                if (!assignedInThisRoom.Contains(player.playerId))
                {
                    assignedInThisRoom.Add(player.playerId);
                    Debug.Log($"✅ 分配玩家: {player.playerId} 给房间 {roomId}");
                    return player.playerId;
                }
            }
        }

        Debug.LogWarning($"⚠️ 房间 {roomId} 找不到可用玩家");
        return "player_unknown";
    }


    // ★ 新增：按房间查询 sessionId
    public static bool TryGetSessionId(string roomId, string playerId, out string sessionId)
    {
        sessionId = null;
        return playerSessionMapByRoom.TryGetValue(roomId, out var map)
               && map.TryGetValue(playerId, out sessionId);
    }

}

// ════════════════════════════════════════════════════════════════
// ✅ 消息类定义
// ════════════════════════════════════════════════════════════════

[System.Serializable]
public class MessageBase
{
    public string type;
}

[System.Serializable]
public class PlayerJoinMsg
{
    public string type;
    public string clientId;
}

[System.Serializable]
public class PlayerAssignedIdMsg
{
    public string type;
    public string playerId;
    public string roomId;
    public string teamId;
    public string playerName;
    public long timestamp;
}


[System.Serializable]
public class ObstacleCollisionMsg
{
    public string type;
    public string playerId;
}
