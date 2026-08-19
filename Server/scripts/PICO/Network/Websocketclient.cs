using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NativeWebSocket;
using UnityEngine;

namespace BubbleBattle.Network
{
    public class WebSocketClient : MonoBehaviour
    {
        public static WebSocketClient Instance { get; private set; }

        [Header("服务器地址")]
        [SerializeField] private string serverHost = "192.168.223.247";
        [SerializeField] private int serverPort = 8080;

        [Header("本地玩家信息")]
        [SerializeField] private string playerName = "玩家1";

        [Header("上报频率")]
        [SerializeField] private float sendInterval = 0.05f;

        [Header("本地玩家Transform")]
        [SerializeField] private Transform localPlayerTransform;

        public event Action<string> OnTeamIdAssigned;
        public string PlayerId { get; private set; }
        public string RoomId { get; private set; }
        public string TeamId { get; private set; }

        private string _clientId; // 客户端唯一ID，在启动时自动生成
        private WebSocket _ws;
        private float _sendTimer;
        private bool _isConnected;
        private bool _hasAssignedPlayerId;

        private readonly Queue<string> _msgQueue = new Queue<string>();
        private readonly object _queueLock = new object();

        public event Action<WorldStateMsg> OnWorldStateReceived;
        void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            // 每次启动时自动生成唯一的 clientId，确保不会使用预设值
            _clientId = Guid.NewGuid().ToString();
            PlayerId = "";
            _hasAssignedPlayerId = false;

            Debug.Log($"[WSClient] 生成的 ClientId: {_clientId}");
        }

        async void Start()
        {
            await Connect();
        }

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

        public async Task Connect()
        {
            string url = $"ws://{serverHost}:{serverPort}/";
            Debug.Log($"[WSClient] 连接到 {url}");

            _ws = new WebSocket(url);

            _ws.OnOpen += () =>
            {
                Debug.Log("[WSClient] 已连接 ");
                _isConnected = true;
                SendJoinMessage();
            };

            _ws.OnMessage += (bytes) =>
            {
                string json = System.Text.Encoding.UTF8.GetString(bytes);
                lock (_queueLock) { _msgQueue.Enqueue(json); }
            };

            _ws.OnError += (err) =>
            {
                Debug.LogError($"[WSClient] 错误: {err}");
            };

            _ws.OnClose += (code) =>
            {
                Debug.Log($"[WSClient] 断开: {code}");
                _isConnected = false;
                _hasAssignedPlayerId = false;
                PlayerId = "";
                RoomId = "";
                TeamId = "";
                Invoke(nameof(ReconnectAsync), 3f);
            };

            await _ws.Connect();
        }

        private async void ReconnectAsync()
        {
            Debug.Log("[WSClient] 尝试重连...");
            await Connect();
        }

        private void SendJoinMessage()
        {
            var msg = new PlayerJoinMsg
            {
                clientId = _clientId, // 使用自动生成的唯一 clientId
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };
            SendJson(JsonUtility.ToJson(msg));
            Debug.Log($"[WSClient] 发送 PlayerJoinMsg: {JsonUtility.ToJson(msg)}");
        }

        private void SendPlayerUpdate()
        {
            if (localPlayerTransform == null) return;

            var msg = new PlayerUpdateMsg
            {
                playerId = PlayerId,
                position = new Vec3(localPlayerTransform.position),
                rotation = new Quat(localPlayerTransform.rotation),
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };
            SendJson(JsonUtility.ToJson(msg));
        }

        private async void SendJson(string json)
        {
            if (_ws == null || _ws.State != WebSocketState.Open) return;
            await _ws.SendText(json);
        }

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
                    Debug.LogWarning($"[WSClient] 接收到无法解析类型字段的消息: {json}");
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
                        OnTeamIdAssigned?.Invoke(TeamId);  // 触发事件  
                        Debug.Log($"[WSClient] 服务器分配的 PlayerId: {PlayerId}, RoomId: {RoomId}, TeamId: {TeamId}");
                        break;
                    default:
                        Debug.LogWarning($"[WSClient] 未知消息类型: {base_.type} | 内容: {json}");
                        break;
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[WSClient] 解析消息失败: {e.Message}\n{json}");
            }

        }

        public void SendRawMessage(string json) => SendJson(json);
    }
}
