// PicoWebSocketClient.cs
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NativeWebSocket;
using UnityEngine;
using Unity.XR.CoreUtils;

namespace BubbleBattle.Network
{
    public class PicoWebSocketClient : MonoBehaviour
    {
        public static PicoWebSocketClient Instance { get; private set; }

        [Header("服务器地址")]
        [SerializeField] private string serverHost = "192.168.223.247";
        [SerializeField] private int serverPort = 8080;

        [Header("本地玩家信息")]
        [SerializeField] private string playerName = "玩家1";

        [Header("上报频率")]
        [SerializeField] private float sendInterval = 0.05f;

        [Header("本地玩家Transform（留空则自动绑定XR Camera）")]
        [SerializeField] public Transform localPlayerTransform;

        //新增
        [Header("网络配置")]
        [SerializeField] private bool useAutoDiscovery = true;
        [SerializeField] private string fallbackServerHost = "192.168.43.218";  // 备用IP  
        [SerializeField] private int fallbackServerPort = 8080;
        [Header("UDP发现配置")]
        [SerializeField] private int discoveryPort = 5354;
        [SerializeField] private int discoveryTimeoutMs = 3000;
        [SerializeField] private int discoveryRetries = 3;
        // 发现的服务器地址  
        private string _discoveredServerHost = "";
        private int _discoveredServerPort = 8080;


        public event Action<string> OnTeamIdAssigned;
        public event Action<string> OnPlayerAssignedId; // 新增：当服务器分配PlayerId时触发  
        //public event Action<string> OnSpatialAnchorReceived; // 新增：当收到共享锚点ID时触发
        public event Action OnBecomeHost;
        public event Action<BombStateBroadcast> OnRemoteBombStateReceived;  //  
        public event Action<PlayersBloodMsg> OnPlayersBloodReceived;
        public event Action<ScoreBroadcast> OnScoreBroadcastReceived;
        //public event Action<BoundaryInfoMsg> OnBoundaryInfoReceived;
        public event Action<PropStateBroadcast> OnPropStateBroadcastReceived;
        public event Action<MapDataMsg> OnMapDataReceived;
        public event Action<GameEndMsg> OnGameEndReceived;
        public event Action<InvincibleStateMessage> OnInvincibleStateReceived;
        public event Action OnConnected;      // 连接成功  
        public event Action OnDisconnected;   // 连接失败/断开 
        // ── 沉默道具相关事件 ─────────────────────────────────────────────────────  
        /// <summary>服务端通知本玩家拾取了沉默道具（单播）</summary>  
        public event Action OnSilencePropPickedUp;
        /// <summary>服务端广播：某玩家放置了沉默道具</summary>  
        public event Action<SilencePropPlacedMsg> OnSilencePropPlaced;
        public string PlayerId { get; private set; }
        public string RoomId { get; private set; }
        public string TeamId { get; private set; }

        private string _clientId;
        private WebSocket _ws;
        private float _sendTimer;
        private bool _isConnected;
        private bool _hasAssignedPlayerId;

        private readonly Queue<string> _msgQueue = new Queue<string>();
        private readonly object _queueLock = new object();

        public event Action<WorldStateMsg> OnWorldStateReceived;

        //private Transform _sharedAnchorTransform; // 新增：用于存储共享锚点的Transform
        //public Transform SharedAnchorTransform => _sharedAnchorTransform; // 暴露给外部访问

        void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            _clientId = SystemInfo.deviceUniqueIdentifier;
            //暂时删除
            //_clientId = Guid.NewGuid().ToString();
            PlayerId = "";
            _hasAssignedPlayerId = false;

            Debug.Log($"[WSClient] 生成的 ClientId: {_clientId}");
        }
        /*
        async void Start()
        {
            // Start 里绑定，确保 XROrigin 已经初始化完毕
            if (localPlayerTransform == null)
                AutoBindXRCamera();

            //await Connect();
            if (useAutoDiscovery)
            {
                await DiscoverServerAsync();
            }

            await Connect();
        }*/
        async void Start()
        {
            if (localPlayerTransform == null)
                AutoBindXRCamera();

            // 不自动连接，等待玩家选择房间
            Debug.Log("[WSClient] 客户端已初始化，等待玩家选择房间");
        }

        public async Task ConnectToRoom(int roomNumber)
        {
            if (roomNumber < 1 || roomNumber > 10)
            {
                Debug.LogError($"[WSClient] 无效房间号: {roomNumber}");
                return;
            }

            // 先进行 UDP 自动发现  
            if (useAutoDiscovery)
            {
                Debug.Log("[WSClient] 开始 UDP 自动发现...");
                await DiscoverServerAsync();
            }

            // 如果发现失败，使用配置或备用 IP，并添加房间号对应的端口  
            if (string.IsNullOrEmpty(_discoveredServerHost))
            {
                _discoveredServerHost = serverHost;
            }

            _discoveredServerPort = 8080 + (roomNumber - 1);

            Debug.Log($"[WSClient] 选择房间 {roomNumber}，连接 {_discoveredServerHost}:{_discoveredServerPort}");
            await Connect();
        }


        // ─── 自动绑定 XR Camera ────────────────────────────────────────────────
        private void AutoBindXRCamera()
        {
            // 优先：XROrigin.Camera（最准确，就是 TrackedPoseDriver 驱动的那个）
            var xrOrigin = FindObjectOfType<XROrigin>();
            if (xrOrigin != null && xrOrigin.Camera != null)
            {
                localPlayerTransform = xrOrigin.Camera.transform;
                Debug.Log($"[WSClient] 绑定 XROrigin.Camera: {localPlayerTransform.name}");
                return;
            }

            // 兜底：Camera.main
            if (Camera.main != null)
            {
                localPlayerTransform = Camera.main.transform;
                Debug.Log($"[WSClient] 兜底绑定 Camera.main: {localPlayerTransform.name}");
                return;
            }

            Debug.LogError("[WSClient] 未找到可用 Camera，玩家位置将无法上报！");
        }

        // ─── Update ────────────────────────────────────────────────────────────
        void Update()
        {
            if (_ws != null)
            {
#if !UNITY_WEBGL || UNITY_EDITOR
                _ws.DispatchMessageQueue();
#endif
            }

            ProcessMessageQueue();

            if (_isConnected && _hasAssignedPlayerId)
            {
                _sendTimer += Time.deltaTime;
                if (_sendTimer >= sendInterval)
                {
                    _sendTimer = 0f;
                    SendPlayerUpdate();
                }
            }
        }

        async void OnDestroy()
        {
            if (_ws != null && _ws.State == WebSocketState.Open && _hasAssignedPlayerId)
            {
                var leaveMsg = new PlayerLeaveMsg
                {
                    playerId = PlayerId,
                    timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                };
                await _ws.SendText(JsonUtility.ToJson(leaveMsg));
                await _ws.Close();
            }
        }


        /*
        // ─── 连接 ──────────────────────────────────────────────────────────────
        public async Task Connect()
        {
            string url = $"ws://{serverHost}:{serverPort}/";
            Debug.Log($"[WSClient] 尝试连接到: {url}");

            _ws = new WebSocket(url);

            _ws.OnOpen += () =>
            {
                Debug.Log("[WSClient] WebSocket 连接成功！");
                _isConnected = true;
                SendJoinMessage();
            };

            _ws.OnMessage += (bytes) =>
            {
                string json = System.Text.Encoding.UTF8.GetString(bytes);
                Debug.Log($"[WSClient] 接收到消息 (长度{json.Length})");
                lock (_queueLock) { _msgQueue.Enqueue(json); }
            };

            _ws.OnError += (err) =>
            {
                Debug.LogError($"[WSClient] ❌ 错误: {err}");
            };

            _ws.OnClose += (code) =>
            {
                Debug.Log($"[WSClient] 断开: {code}");
                _isConnected = false;
                _hasAssignedPlayerId = false;
                PlayerId = "";
                Invoke(nameof(ReconnectAsync), 3f);
            };

            await _ws.Connect();
        }*/
        public async Task Connect()
        {
            // 优先使用发现的 IP，其次使用配置的 IP  
            string connectHost = _discoveredServerHost;
            int connectPort = _discoveredServerPort;

            if (string.IsNullOrEmpty(connectHost))
            {
                connectHost = fallbackServerHost;
                connectPort = fallbackServerPort;
                Debug.Log("[WSClient] 未发现服务器，使用备用地址");
            }

            string url = $"ws://{connectHost}:{connectPort}/";
            Debug.Log($"[WSClient]  连接到 {url}");

            _ws = new WebSocket(url);

            _ws.OnOpen += () =>
            {
                Debug.Log("[WSClient] 已连接");
                _isConnected = true;
                OnConnected?.Invoke();
                SendJoinMessage();
            };

            _ws.OnMessage += (bytes) =>
            {
                string json = System.Text.Encoding.UTF8.GetString(bytes);
                lock (_queueLock) { _msgQueue.Enqueue(json); }
            };

            _ws.OnError += (err) =>
            {
                Debug.LogError($"[WSClient]  错误: {err}");
            };

            _ws.OnClose += (code) =>
            {
                Debug.Log($"[WSClient] 断开: {code}");
                _isConnected = false;
                _hasAssignedPlayerId = false;
                PlayerId = "";
                RoomId = "";
                TeamId = "";

                // 断开后清空发现的信息，准备重新发现  
                _discoveredServerHost = "";
                _discoveredServerPort = 8080;
                OnDisconnected?.Invoke();
                Invoke(nameof(ReconnectAsync), 3f);
            };

            await _ws.Connect();
        }
        /*
        private async void ReconnectAsync()
        {
            Debug.Log("[WSClient] 尝试重连...");
            // 重连前重新绑定，防止场景重载后引用失效
            if (localPlayerTransform == null)
                AutoBindXRCamera();
            await Connect();
        }*/
        private async void ReconnectAsync()
        {
            Debug.Log("[WSClient]  尝试重连...");

            if (localPlayerTransform == null)
                AutoBindXRCamera();

            //  重新发现服务器  
            if (useAutoDiscovery)
            {
                await DiscoverServerAsync();
            }

            await Connect();
        }

        // ─── 发送 ──────────────────────────────────────────────────────────────
        private void SendJoinMessage()
        {
            var msg = new PlayerJoinMsg
            {
                clientId = _clientId,
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };
            SendJson(JsonUtility.ToJson(msg));
            Debug.Log($"[WSClient] 发送 PlayerJoinMsg: {JsonUtility.ToJson(msg)}");
        }
        /*
        // 修改 SendPlayerUpdate 方法以支持锚点相对坐标
        private void SendPlayerUpdate()
        {
            if (localPlayerTransform == null)
            {
                AutoBindXRCamera();
                if (localPlayerTransform == null) return;
            }

            Vector3 sendPosition;
            Quaternion sendRotation;


            // 如果已经有了共享锚点Transform，则发送相对于锚点的局部坐标
            if (_sharedAnchorTransform != null)
            {
                // 将本地玩家的世界坐标转换为共享锚点的局部坐标
                sendPosition = _sharedAnchorTransform.InverseTransformPoint(localPlayerTransform.position);
                sendRotation = Quaternion.Inverse(_sharedAnchorTransform.rotation) * localPlayerTransform.rotation;
                Debug.Log($"[WSClient] 发送锚点相对位置: {sendPosition}"); // 调试用
                Debug.Log($"共享锚点: {(_sharedAnchorTransform != null ? _sharedAnchorTransform.name : "null")}, " +
                $"发送位置: {sendPosition}, " +$"发送类型: {(_sharedAnchorTransform != null ? "相对坐标" : "世界坐标")}");
            }
            else
            {
                // 如果没有共享锚点，则发送世界坐标（兼容旧行为或无锚点场景）
                sendPosition = localPlayerTransform.position;
                sendRotation = localPlayerTransform.rotation;
                Debug.Log($"[WSClient] 发送世界位置: {sendPosition}"); // 调试用
            }

            var msg = new PlayerUpdateMsg
            {
                playerId = PlayerId,
                position = new Vec3(sendPosition),
                rotation = new Quat(sendRotation),
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                //isAnchorRelative = (_sharedAnchorTransform != null)  //  新增这一行  
            };
            SendJson(JsonUtility.ToJson(msg));
        }*/
        private void SendPlayerUpdate()
        {
            if (localPlayerTransform == null)
            {
                AutoBindXRCamera();
                if (localPlayerTransform == null) return;
            }

            // 直接发送世界坐标  
            var msg = new PlayerUpdateMsg
            {
                playerId = PlayerId,
                position = new Vec3(localPlayerTransform.position),
                rotation = new Quat(localPlayerTransform.rotation),
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };
            SendJson(JsonUtility.ToJson(msg));
        }
        /*
        // 新增发送 Spatial Anchor 消息的方法
        public void SendSpatialAnchor(string anchorId)
        {
            if (string.IsNullOrEmpty(PlayerId))
            {
                Debug.LogWarning("[WSClient] PlayerId 未分配，无法发送 SpatialAnchorMsg。");
                return;
            }
            var msg = new SpatialAnchorMsg
            {
                ownerPlayerId = PlayerId,
                anchorId = anchorId,
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };
            SendJson(JsonUtility.ToJson(msg));
            Debug.Log($"[WSClient] 发送共享锚点ID: {anchorId}");
        }*/
        /*
        // 新增设置共享锚点Transform的方法
        public void SetSharedAnchorTransform(Transform anchorTransform)
        {
            _sharedAnchorTransform = anchorTransform;
            if (anchorTransform != null)
            {
                Debug.Log($"[WSClient] 成功设置共享锚点Transform: {anchorTransform.name}");
            }
            else
            {
                Debug.Log($"[WSClient] 共享锚点Transform被置空。");
            }
        }*/


        private async void SendJson(string json)
        {
            if (_ws == null || _ws.State != WebSocketState.Open) return;
            await _ws.SendText(json);
        }

        // ─── 接收 ──────────────────────────────────────────────────────────────
        private void ProcessMessageQueue()
        {
            while (true)
            {
                string json;
                lock (_queueLock)
                {
                    if (_msgQueue.Count == 0) break;
                    json = _msgQueue.Dequeue();
                }
                HandleMessage(json);
            }
        }

        private void HandleMessage(string json)
        {
            try
            {
                var base_ = JsonUtility.FromJson<BaseMsg>(json);
                if (base_ == null || string.IsNullOrEmpty(base_.type))
                {
                    Debug.LogWarning($"[WSClient] 无法解析类型字段: {json}");
                    return;
                }

                switch (base_.type)
                {
                    case MsgType.WorldState:
                        var worldState = JsonUtility.FromJson<WorldStateMsg>(json);
                        OnWorldStateReceived?.Invoke(worldState);
                        break;

                    case MsgType.PlayerAssignedId:
                        var assignedIdMsg = JsonUtility.FromJson<PlayerAssignedIdMsg>(json);
                        PlayerId = assignedIdMsg.playerId;
                        RoomId = assignedIdMsg.roomId;
                        TeamId = assignedIdMsg.teamId;
                        _hasAssignedPlayerId = true;
                        OnTeamIdAssigned?.Invoke(TeamId);
                        OnPlayerAssignedId?.Invoke(PlayerId); // 新增：触发 PlayerId 分配事件  
                        Debug.Log($"[WSClient] PlayerId={PlayerId}, RoomId={RoomId}, TeamId={TeamId}");
                        break;
                        /*
                    case MsgType.SpatialAnchor: //处理 SpatialAnchorMsg
                        var anchorMsg = JsonUtility.FromJson<SpatialAnchorMsg>(json);
                        OnSpatialAnchorReceived?.Invoke(anchorMsg.anchorId);
                        Debug.Log($"[WSClient] 收到共享锚点ID: {anchorMsg.anchorId}");
                        break;

                    case MsgType.BecomeHost: //处理 BecomeHost 消息  
                        var becomeHostMsg = JsonUtility.FromJson<BecomeHostMsg>(json);
                        if (becomeHostMsg.playerId == PlayerId) // 确认是发给自己的 Host 通知  
                        {
                            OnBecomeHost?.Invoke();
                            Debug.Log($"[WSClient] 收到 BecomeHost 通知，PlayerId: {becomeHostMsg.playerId}");
                        }
                        break;*/
                    case MsgType.BombStateBroadcast:  // 新增  
                        var BombStateBroadcast = JsonUtility.FromJson<BombStateBroadcast>(json);
                        OnRemoteBombStateReceived?.Invoke(BombStateBroadcast);
                        Debug.Log($"[WSClient] 收到炸弹状态: {BombStateBroadcast.bombs?.Length ?? 0} 个, " +
                                  $"帧号={BombStateBroadcast.frameSequenceNumber}, " +
                                  $"重传={BombStateBroadcast.isRetransmit}");
                        break;

                    case MsgType.MissingFrameRequest:  // 新增  
                                                       // 客户端通常不处理这个，这是服务端接收的  
                        Debug.Log($"[WSClient] 收到补包请求（客户端不应处理）");
                        break;
                    case MsgType.PlayersBlood:
                        var playersBloodMsg = JsonUtility.FromJson<PlayersBloodMsg>(json);
                        OnPlayersBloodReceived?.Invoke(playersBloodMsg);
                        Debug.Log($"[WSClient] 收到玩家血量更新: {playersBloodMsg.teams?.Length ?? 0} 个队伍");
                        break;
                    case MsgType.MapData:
                        var mapDataMsg = JsonUtility.FromJson<MapDataMsg>(json);
                        OnMapDataReceived?.Invoke(mapDataMsg);
                        Debug.Log($"[WSClient] 收到地图数据: {mapDataMsg.mapName}, 包含 {mapDataMsg.objects?.Length ?? 0} 个对象");
                        break;
                    case MsgType.Grade:
                        var scoreBroadcast = JsonUtility.FromJson<ScoreBroadcast>(json);
                        OnScoreBroadcastReceived?.Invoke(scoreBroadcast);
                        Debug.Log($"[WSClient] 收到得分广播: 红队={GetTeamScore(scoreBroadcast, "Red")}, 蓝队={GetTeamScore(scoreBroadcast, "Blue")}");
                        break;
                        /*
                    case "BoundaryInfo":
                        var boundaryInfo = JsonUtility.FromJson<BoundaryInfoMsg>(json);
                        OnBoundaryInfoReceived?.Invoke(boundaryInfo);
                        Debug.Log($"[WSClient] 收到玩家 {boundaryInfo.playerId} 的边界信息");
                        break;
                        */
                    case MsgType.PropStateBroadcast: 
                        var propStateBroadcast = JsonUtility.FromJson<PropStateBroadcast>(json);
                        OnPropStateBroadcastReceived?.Invoke(propStateBroadcast);
                        Debug.Log($"[WSClient] 收到道具状态: {propStateBroadcast.props?.Length ?? 0} 个道具");
                        break;
                    case MsgType.GameEnd:
                        var gameEndMsg = JsonUtility.FromJson<GameEndMsg>(json);
                        OnGameEndReceived?.Invoke(gameEndMsg);
                        Debug.Log($"[WSClient] 收到游戏结束消息: 赢家={gameEndMsg.winnerTeamName}, " +
                                  $"红队胜{gameEndMsg.redTeamVictory}局, 蓝队胜{gameEndMsg.blueTeamVictory}局, " +
                                  $"剩余轮数={gameEndMsg.remainingRounds}, " +
                                  $"系列赛结束={gameEndMsg.isSeriesEnd}, " +
                                  $"剩余时间={gameEndMsg.remainingTime}");
                        break;
                    case MsgType.InvincibleState:   
                        var invincibleStateMsg = JsonUtility.FromJson<InvincibleStateMessage>(json);
                        OnInvincibleStateReceived?.Invoke(invincibleStateMsg);
                        Debug.Log($"[WSClient] 收到无敌状态更新: {invincibleStateMsg.invincibleStates?.Length ?? 0} 个玩家");
                        break;
                    case MsgType.SilencePropPickedUp:
                        // 单播给本玩家，直接触发事件  
                        var silencePickedMsg = JsonUtility.FromJson<SilencePropPickedUpMsg>(json);
                        if (silencePickedMsg.playerId == PlayerId)
                        {
                            OnSilencePropPickedUp?.Invoke();
                            Debug.Log("[WSClient] 收到沉默道具拾取通知，已激活放置能力");
                        }
                        break;
                    case MsgType.SilencePropPlaced:
                        var silencePlacedMsg = JsonUtility.FromJson<SilencePropPlacedMsg>(json);
                        OnSilencePropPlaced?.Invoke(silencePlacedMsg);
                        Debug.Log($"[WSClient] 收到沉默道具放置广播: 放置者={silencePlacedMsg.placedByPlayerId}, " +
                                  $"队伍={silencePlacedMsg.placedByTeamId}, " +
                                  $"位置=({silencePlacedMsg.position.x:F2},{silencePlacedMsg.position.z:F2}), " +
                                  $"半径={silencePlacedMsg.effectHalfSize}, 持续={silencePlacedMsg.duration}s");
                        break;
                    default:
                        Debug.LogWarning($"[WSClient] 未知消息类型: {base_.type} | {json}");
                        break;
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[WSClient] 解析失败: {e.Message}\n{json}");
            }
        }
        private int GetTeamScore(ScoreBroadcast broadcast, string teamColor)
        {
            if (broadcast?.teams == null) return 0;
            foreach (var team in broadcast.teams)
            {
                if (team.teamId.ToLower().Contains(teamColor.ToLower()))
                {
                    return team.totalScore;
                }
            }
            return 0;
        }

        /// <summary>  
        /// 自动发现游戏服务器（带重试和更智能的网络检测）  
        /// </summary>  
        private async Task DiscoverServerAsync()
        {
            Debug.Log($"[WSClient]  开始搜索游戏服务器... (端口: {discoveryPort}, 超时: {discoveryTimeoutMs}ms)");

            for (int attempt = 0; attempt < discoveryRetries; attempt++)
            {
                Debug.Log($"[WSClient]  发现尝试 {attempt + 1}/{discoveryRetries}");

                if (await TryDiscoverOnce())
                {
                    return;
                }

                // 等待后再重试  
                if (attempt < discoveryRetries - 1)
                {
                    Debug.Log($"[WSClient]  等待 2 秒后重试...");
                    await Task.Delay(2000);
                }
            }

            Debug.LogWarning($"[WSClient]  经过 {discoveryRetries} 次尝试，未能发现服务器，将使用备用地址");
        }

        /// <summary>  
        /// 单次发现尝试（在后台线程中执行，不阻塞主线程）  
        /// </summary>  
        private async Task<bool> TryDiscoverOnce()
        {
            return await Task.Run(() =>
            {
                System.Net.Sockets.UdpClient udpClient = null;
                try
                {
                    udpClient = new System.Net.Sockets.UdpClient();
                    udpClient.EnableBroadcast = true;
                    udpClient.Client.ReceiveTimeout = discoveryTimeoutMs;

                    byte[] discoverRequest = System.Text.Encoding.UTF8.GetBytes("DISCOVER_GAMESERVER");
                    List<System.Net.IPEndPoint> broadcastEndpoints = GetBroadcastEndpoints();

                    Debug.Log($"[WSClient]  UDP发现配置:");
                    Debug.Log($"   - 发现端口: {discoveryPort}");
                    Debug.Log($"   - 超时时间: {discoveryTimeoutMs}ms");
                    Debug.Log($"   - 广播端点数: {broadcastEndpoints.Count}");

                    if (broadcastEndpoints.Count == 0)
                    {
                        Debug.LogError("[WSClient]  没有有效的广播地址");
                        return false;
                    }

                    // 发送请求到所有广播地址  
                    foreach (var endpoint in broadcastEndpoints)
                    {
                        try
                        {
                            Debug.Log($"[WSClient]  发送到: {endpoint.Address}:{endpoint.Port}");
                            udpClient.Send(discoverRequest, discoverRequest.Length, endpoint);
                        }
                        catch (Exception e)
                        {
                            Debug.LogWarning($"[WSClient]  发送失败: {e.Message}");
                        }
                    }

                    // 在后台线程中等待响应  
                    try
                    {
                        System.Net.IPEndPoint responseEP = new System.Net.IPEndPoint(System.Net.IPAddress.Any, 0);
                        byte[] responseData = udpClient.Receive(ref responseEP);
                        string response = System.Text.Encoding.UTF8.GetString(responseData);

                        Debug.Log($"[WSClient]  收到响应: {response}");
                        Debug.Log($"[WSClient]  来自: {responseEP.Address}:{responseEP.Port}");

                        if (response.StartsWith("GAMESERVER|"))
                        {
                            string[] parts = response.Split('|');
                            if (parts.Length >= 3)
                            {
                                string serverIP = parts[1].Trim();
                                /*
                                if (int.TryParse(parts[2].Trim(), out int port))
                                {
                                    _discoveredServerHost = serverIP;
                                    _discoveredServerPort = port;*/
                                    _discoveredServerHost = serverIP;//添加
                                    Debug.Log($" [WSClient] 成功发现服务器!");
                                    Debug.Log($"   服务器IP: {_discoveredServerHost}");
                                    Debug.Log($"   服务器端口: {_discoveredServerPort}");
                                    return true;
                                //}
                            }
                        }
                    }
                    catch (System.Net.Sockets.SocketException ex) when (ex.SocketErrorCode == System.Net.Sockets.SocketError.TimedOut)
                    {
                        Debug.LogWarning($"[WSClient]  发现超时({discoveryTimeoutMs}ms)");
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[WSClient]  异常: {ex.Message}");
                    return false;
                }
                finally
                {
                    try
                    {
                        udpClient?.Close();
                        udpClient?.Dispose();
                    }
                    catch { }
                }

                return false;
            });
        }

        /// <summary>  
        /// 获取可用的广播地址（过滤掉回环和特殊地址）  
        /// </summary>  
        private List<System.Net.IPEndPoint> GetBroadcastEndpoints()
        {
            var endpoints = new List<System.Net.IPEndPoint>();

            try
            {
                foreach (System.Net.NetworkInformation.NetworkInterface nic in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces())
                {
                    // 跳过不活跃的接口  
                    if (nic.OperationalStatus != System.Net.NetworkInformation.OperationalStatus.Up)
                        continue;

                    // 跳过虚拟、回环接口  
                    if (nic.NetworkInterfaceType == System.Net.NetworkInformation.NetworkInterfaceType.Loopback)
                        continue;

                    if (nic.Description.Contains("Virtual") || nic.Description.Contains("vEthernet"))
                        continue;

                    System.Net.NetworkInformation.IPInterfaceProperties properties = nic.GetIPProperties();

                    foreach (System.Net.NetworkInformation.UnicastIPAddressInformation uni in properties.UnicastAddresses)
                    {
                        if (uni.Address.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
                            continue;

                        byte[] ipBytes = uni.Address.GetAddressBytes();

                        // 过滤掉特殊地址  
                        if (ipBytes[0] == 127) continue;
                        if (ipBytes[0] == 169 && ipBytes[1] == 254) continue;

                        if (!IsValidPrivateIP(uni.Address.ToString()))
                            continue;

                        // 计算子网广播地址  
                        System.Net.IPAddress subnetMask = uni.IPv4Mask;
                        byte[] maskBytes = subnetMask.GetAddressBytes();
                        byte[] broadcastBytes = new byte[4];

                        for (int i = 0; i < 4; i++)
                        {
                            broadcastBytes[i] = (byte)(ipBytes[i] | ~maskBytes[i]);
                        }

                        System.Net.IPAddress broadcastAddr = new System.Net.IPAddress(broadcastBytes);
                        endpoints.Add(new System.Net.IPEndPoint(broadcastAddr, discoveryPort));

                        Debug.Log($"[WSClient]  广播地址计算: {uni.Address} -> {broadcastAddr}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[WSClient]  获取网络接口失败: {ex.Message}");
            }

            return endpoints;
        }

        /// <summary>  
        /// 验证私有IP地址  
        /// </summary>  
        private bool IsValidPrivateIP(string ip)
        {
            if (string.IsNullOrEmpty(ip))
                return false;

            if (!System.Net.IPAddress.TryParse(ip, out System.Net.IPAddress parsedIP))
                return false;

            byte[] bytes = parsedIP.GetAddressBytes();

            // 10.0.0.0 - 10.255.255.255（排除 10.255.x.x）  
            if (bytes[0] == 10 && bytes[1] != 255)
                return true;

            // 172.16.0.0 - 172.31.255.255  
            if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
                return true;

            // 192.168.0.0 - 192.168.255.255  
            if (bytes[0] == 192 && bytes[1] == 168)
                return true;

            return false;
        }

        public void SendRawMessage(string json) => SendJson(json);

        /// <summary>  
        /// 向服务端发送沉默道具放置请求（松开左手 Trigger 时调用）  
        /// </summary>  
        public void SendSilencePropPlace(Vector3 worldPosition)
        {
            if (string.IsNullOrEmpty(PlayerId))
            {
                Debug.LogWarning("[WSClient] PlayerId 未分配，无法发送 SilencePropPlace");
                return;
            }
            var msg = new SilencePropPlaceMsg
            {
                playerId = PlayerId,
                teamId = TeamId,
                position = new Vec3(worldPosition),
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };
            SendJson(JsonUtility.ToJson(msg));
            Debug.Log($"[WSClient] 发送 SilencePropPlace: 位置={worldPosition}");
        }
    }
}
