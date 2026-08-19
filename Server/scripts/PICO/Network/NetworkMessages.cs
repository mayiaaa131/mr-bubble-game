using System;
using System.Collections.Generic;
using UnityEngine; // 确保包含 UnityEngine，因为 Vec3 和 Quat 会用到 Vector3 和 Quaternion

namespace BubbleBattle.Network
{
    // 定义所有消息的基类，包含 type 字段
    [Serializable]
    public class BaseMsg
    {
        public string type;
    }

    // 辅助枚举，用于简化 type 字段的字符串比较
    public static class MsgType
    {
        public const string PlayerJoin = "PlayerJoin";
        public const string PlayerUpdate = "PlayerUpdate";
        public const string PlayerLeave = "PlayerLeave";
        public const string PlayerAssignedId = "PlayerAssignedId"; // 服务器分配 PlayerId 的消息类型
        public const string WorldState = "WorldState";
        public const string BombStateBroadcast = "BombStateBroadcast";
        public const string BombStateRetransmit = "BombStateRetransmit";
        public const string MissingFrameRequest = "MissingFrameRequest";
        public const string PlayersBlood = "PlayersBlood";
        public const string Grade = "Grade";
        public const string PropStateBroadcast = "PropStateBroadcast";
        public const string MapData = "MapData";
        public const string GameEnd = "GameEnd";
        public const string ObstacleCollision = "ObstacleCollision";
        public const string InvincibleState = "InvincibleState";
        public const string SilencePropPickedUp = "SilencePropPickedUp";   // 服务端→拾取者（单播）  
        public const string SilencePropPlace = "SilencePropPlace";      // 客户端→服务端  
        public const string SilencePropPlaced = "SilencePropPlaced";     // 服务端→所有客户端
    }

    // 玩家加入消息：客户端发送给服务器
    [Serializable]
    public class PlayerJoinMsg : BaseMsg
    {
        public string clientId; // 客户端生成的唯一ID
        public long timestamp; // 客户端生成的时间戳，用于服务器排序
        public PlayerJoinMsg() { type = MsgType.PlayerJoin; }
    }

    // 玩家更新消息：客户端发送给服务器
    [Serializable]
    public class PlayerUpdateMsg : BaseMsg
    {
        public string playerId; // 服务器分配的ID
        public Vec3 position;
        public Quat rotation;
        public long timestamp;
        public PlayerUpdateMsg() { type = MsgType.PlayerUpdate; }
    }

    // 玩家离开消息：客户端发送给服务器
    [Serializable]
    public class PlayerLeaveMsg : BaseMsg
    {
        public string playerId; // 服务器分配的ID
        public long timestamp;
        public PlayerLeaveMsg() { type = MsgType.PlayerLeave; }
    }

    // 服务器分配 PlayerId 的消息：服务器发送给客户端
    [Serializable]
    public class PlayerAssignedIdMsg : BaseMsg
    {
        public string playerId; // 服务器分配的唯一玩家ID
        public string roomId;
        public string playerName;
        public string teamId;
        public long timestamp;
        public PlayerAssignedIdMsg() { type = MsgType.PlayerAssignedId; }
    }

    // 世界状态消息：服务器发送给客户端
    [Serializable]
    public class WorldStateMsg : BaseMsg
    {
        public string roomId;
        public long timestamp;
        public TeamInfo[] teams;

        public WorldStateMsg() { type = MsgType.WorldState; }
    }
    /*
    // Spatial Anchor 消息  ，服务器发给客户端
    [Serializable]
    public class SpatialAnchorMsg : BaseMsg
    {
        public string anchorId; // 空间锚点的UUID  
        public string ownerPlayerId; // (可选) 记录是哪个玩家创建的锚点  
        public long timestamp;
        public SpatialAnchorMsg() { type = MsgType.SpatialAnchor; }
    }
    [Serializable]
    public class BecomeHostMsg : BaseMsg//服务器发给客户端
    {
        public string playerId;
        public long timestamp;
        public BecomeHostMsg() { type = MsgType.BecomeHost; }
    }*/
    [System.Serializable]
    public class ExplosionRange
    {
        public float xMin;
        public float xMax;
        public float zMin;
        public float zMax;
    }
    // ===== 炸弹信息数据结构 =====  
    [System.Serializable]
    public class BombInfo
    {
        public string bombId;              // 炸弹唯一ID  
        public string playerId;     // 放置者ID  
        public string teamId;
        public Vec3 position;              // 位置
        public float totalTime;            // 总时间（如3秒）  
        public float remainingTime;        // 剩余时间  
        public string state;               // "Active" / "Exploding" / "Removed"  
        public string bombType;            // "长条形" / "横条形" / "正方体" / "十字形" 等  
        public string bombLevel;           // "一级" / "二级" 等（影响伤害和范围）  
        public ExplosionRange[] explosionRanges;  // 爆炸范围列表（可能多个，用于合并）  
        public long createTime;            // 创建时间戳（毫秒）  
        public long explosionTimestamp;    // 爆炸时刻（毫秒，为0表示未爆炸）  
        public long serverTimestamp;       // 服务器广播时间戳  
    }
    // ===== 炸弹状态消息 =====  
    [System.Serializable]
    public class BombStateBroadcast
    //BombStateBroadcast
    {
        public string type;                // "BombStateBroadcast"  
        public string roomId;
        public long timestamp;             // 消息时间戳  
        public bool isRetransmit;          // 是否为重传帧  
        public int frameSequenceNumber;    // 帧序号（用于检测丢包）  
        public BombInfo[] bombs;       // 所有炸弹列表  
    }

    // ===== 补包请求 =====  
    [System.Serializable]
    public class MissingFrameRequest
    {
        public string type;                // "MissingFrameRequest"  
        public string clientId;
        public int fromFrameNumber;        // 从第几帧开始丢  
        public int toFrameNumber;          // 到第几帧  
        public long timestamp;
    }

    // ===== 玩家血量同步消息 =====  
    [System.Serializable]
    public class PlayersBloodMsg : BaseMsg
    {
        public string roomId;
        public long timestamp;
        public BloodTeamInfo[] teams;

        public PlayersBloodMsg() { type = "PlayersBlood"; }
    }

    [System.Serializable]
    public class BloodTeamInfo
    {
        public string teamId;
        public string teamName;
        public PlayerBloodInfo[] players;
    }

    [System.Serializable]
    public class PlayerBloodInfo
    {
        public string playerId;
        public string playerName;
        public int blood;           // 当前血量  
        public int maxBlood;        // 最大血量  
    }

    //得分系统
    // ===== 得分相关消息 =====  
    [System.Serializable]
    public class ScoreBroadcast : BaseMsg
    {
        public string roomId;
        public long timestamp;
        public TeamScoreInfo[] teams;  // 各队伍的得分信息  

        public ScoreBroadcast() { type = "Grade"; }
    }

    [System.Serializable]
    public class TeamScoreInfo
    {
        public string teamId;
        public string teamName;
        public int totalScore;         // 队伍总得分（击杀数）  
        public PlayerScoreInfo[] players;
    }

    [System.Serializable]
    public class PlayerScoreInfo
    {
        public string playerId;
        public string playerName;
        public int killCounts;          // 该玩家的击杀数  
        public int deathCounts;         // 该玩家的死亡数  
        public int scores;              // 该玩家的原始得分（死亡分数）  
    }

    //道具
    [System.Serializable]
    public class PropInfo
    {
        public string propId;           // 道具ID  
        public string propType;         // 道具类型：BloodRestore 等  
        public Vec3 position;           // 位置（相对共享锚点）  
        public int restoreAmount;       // 恢复血量  
        public string state;            // "Available" / "Cooldown"
        public long respawnTime;        // 重生间隔（毫秒）  
        public long lastPickupTime;     // 上次拾取时刻  
    }

    [System.Serializable]
    public class PropStateBroadcast : BaseMsg
    {
        public string roomId;
        public long timestamp;
        public PropInfo[] props;
        public PropStateBroadcast() { type = MsgType.PropStateBroadcast; }
    }
    // ===== 地图相关消息结构 =====  
    [System.Serializable]
    public class MapDataMsg : BaseMsg
    {
        public string mapName;
        public string savedAt;
        public MapObjectInfo[] objects;

        public MapDataMsg() { type = MsgType.MapData; }
    }
    [System.Serializable]
    public class ObstacleCollisionMsg : BaseMsg
    {
        public string playerId;
        public string teamId;
        public string roomId;
        public long timestamp;

        public ObstacleCollisionMsg() { type = MsgType.ObstacleCollision; }
    }

    // ===== 无敌状态消息 =====  
    [System.Serializable]
    public class InvincibleStateInfo
    {
        public string playerId;
        public string playerName;
        public bool isInvincible;           // 是否无敌  
        public float invincibleCountdown;   // 无敌倒计时（秒）  
    }

    [System.Serializable]
    public class InvincibleStateMessage : BaseMsg
    {
        public string roomId;
        public long timestamp;
        public InvincibleStateInfo[] invincibleStates;  // 注意：改为数组而不是List  

        public InvincibleStateMessage() { type = MsgType.InvincibleState; }
    }
    /// <summary>  
    /// 服务端 → 拾取者（单播）  
    /// 客户端收到后：激活放置能力，本地标记"持有沉默道具 = true"  
    /// </summary>  
    [Serializable]
    public class SilencePropPickedUpMsg : BaseMsg
    {
        public string roomId;
        public string playerId;
        public long timestamp;
        public SilencePropPickedUpMsg() { type = MsgType.SilencePropPickedUp; }
    }

    /// <summary>  
    /// 客户端 → 服务端  
    /// 松开左手 Trigger 后发送，携带放置位置  
    /// </summary>  
    [Serializable]
    public class SilencePropPlaceMsg : BaseMsg
    {
        public string playerId;
        public string teamId;
        public Vec3 position;
        public long timestamp;
        public SilencePropPlaceMsg() { type = MsgType.SilencePropPlace; }
    }

    /// <summary>  
    /// 服务端 → 所有客户端  
    /// 广播放置结果，客户端据此生成沉默道具实例  
    /// </summary>  
    [Serializable]
    public class SilencePropPlacedMsg : BaseMsg
    {
        public string roomId;
        public string placedByPlayerId;
        public string placedByTeamId;
        public Vec3 position;
        public float effectHalfSize;   // 正方形半边长  
        public float duration;         // 实例存活时间（秒），客户端自行计时销毁  
        public long timestamp;
        public SilencePropPlacedMsg() { type = MsgType.SilencePropPlaced; }
    }
    // ===== 游戏结束消息 =====  
    [System.Serializable]
    public class GameEndMsg : BaseMsg
    {
        public string roomId;
        public long timestamp;
        public float remainingTime;        // 剩余时间（秒）  
        public int remainingRounds;        //剩余轮数  
        public string victoryCondition;    // "BO5" 等  
        public int redTeamVictory;         // 红队胜利局数  
        public int blueTeamVictory;        // 蓝队胜利局数  
        public string winnerTeamId;        // 赢家队伍ID  
        public string winnerTeamName;      // 赢家队伍名称  
        public bool isSeriesEnd;           // 是否系列赛结束  

        public GameEndMsg() { type = MsgType.GameEnd; }
    }

    [System.Serializable]
    public class MapObjectInfo
    {
        public int prefabIndex;
        public string prefabName;
        public Vec3 position;
        public Vec3 rotation;      // 注意：你的JSON中rotation是向量，不是四元数  
        public Vec3 scale;
    }
    // 辅助类用于表示 Vector3 和 Quaternion
    [Serializable]
    public class Vec3
    {
        public float x, y, z;
        public Vec3(Vector3 v) { x = v.x; y = v.y; z = v.z; }
    }

    [Serializable]
    public class Quat
    {
        public float x, y, z, w;
        public Quat(Quaternion q) { x = q.x; y = q.y; z = q.z; w = q.w; }
    }

    // 用于 WorldStateMsg 的辅助结构
    [Serializable]
    public class TeamInfo
    {
        public string teamId;
        public string teamName; // 可选
        public PlayerStateInfo[] players;
    }

    [Serializable]
    public class PlayerStateInfo
    {
        public string playerId;
        public string playerName;
        public Vec3 position;
        public Quat rotation;
        // ... 其他玩家状态，例如血量、得分等
    }
}
