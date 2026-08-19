using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class RoomPlayerManager : MonoBehaviour
{
    /// <summary>
    /// 玩家数据结构（独立，不依赖 TeamAssignManager）
    /// </summary>
    [System.Serializable]
    public class PlayerData
    {
        public string playerId;
        public string playerName;
        public string team;          // "red" or "blue"
        public int killCount;
        public int deathCount;
        public int assistCount;
        public int currentScore;
        public int currentBlood;     // ★ 直接从 JSON 读取
    }

    private Dictionary<string, PlayerData> _players = new();
    private RoomTeamManager _teamManager;
    private RoomInstance _roomInstance;
    private string _roomId;
    private RoomTeamsData _teamData;

    public void Init( RoomTeamManager teamManager, string roomId )
    {
        _teamManager = teamManager;
        _roomInstance = GetComponent<RoomInstance>();
        _roomId = roomId;

        // ★ 关键：直接从 Team JSON 加载所有玩家数据
        LoadPlayersFromTeamJson();
    }

    /// <summary>
    /// ★ 关键：从 Team JSON 直接加载所有玩家数据（包含血量、分数等）
    /// 不依赖任何 Manager，纯粹从 JSON 读取
    /// </summary>
    private void LoadPlayersFromTeamJson( )
    {
        _teamData = TeamJsonFileHandler.Instance.LoadTeamsData(_roomId);
        if (_teamData == null)
        {
            Debug.LogError($"❌ 无法加载 Team JSON: {_roomId}");
            return;
        }

        _players.Clear();

        foreach (TeamInfo team in _teamData.teams)
        {
            string teamType = team.teamId.Contains("Red") || team.teamId.Contains("red") ? "red" : "blue";

            foreach (TeamPlayer jsonPlayer in team.players)
            {
                PlayerData playerData = new PlayerData
                {
                    playerId = jsonPlayer.playerId,
                    playerName = jsonPlayer.playerName,
                    team = teamType,
                    killCount = jsonPlayer.killCount,
                    deathCount = jsonPlayer.deathCount,
                    assistCount = jsonPlayer.assistCount,
                    currentScore = jsonPlayer.currentScore,
                    currentBlood = jsonPlayer.currentBlood  // ★ 直接从 JSON 读取血量
                };

                _players[ jsonPlayer.playerId ] = playerData;
            }
        }

        int redCount = _players.Values.Count(p => p.team == "red");
        int blueCount = _players.Values.Count(p => p.team == "blue");
        Debug.Log($"✓ 从 Team JSON 加载玩家数据: 红队{redCount}人，蓝队{blueCount}人，共{_players.Count}人");
    }

    /// <summary>
    /// 添加玩家到内存（在玩家加入时调用）
    /// </summary>
    public void AddPlayer( string playerId, string playerName, string team )
    {
        PlayerData playerData = new PlayerData
        {
            playerId = playerId,
            playerName = playerName,
            team = team,
            killCount = 0,
            deathCount = 0,
            assistCount = 0,
            currentScore = 100,
            currentBlood = 6  // ★ 初始血量
        };

        _players[ playerId ] = playerData;

        // ★ 关键：立即同步到 Team JSON
        SyncPlayerToTeamJson(playerData);

        Debug.Log($"✓ 玩家 {playerName} (ID={playerId}) 已添加到 {team}队");
    }

    /// <summary>
    /// ★ 击杀事件处理 - 从 Room.json 直接获取计分系数
    /// </summary>
    public void OnKill( string killerId, string victimId, List<string> assistIds )
    {
        // ★ 关键：直接从 Room.json 加载房间数据以获取 scoreCoefficients
        Room room = JsonFileHandler.Instance.GetRoomById(_roomId);

        if (room == null)
        {
            Debug.LogError($"❌ 无法从 Room.json 加载房间数据: {_roomId}");
            return;
        }

        var coefficients = room.scoreCoefficients ?? new Room.ScoreCoefficients();

        Debug.Log($"[RoomPlayerManager] 使用计分系数 - 击杀:{coefficients.killCoefficient}, 死亡:{coefficients.deathCoefficient}, 助攻:{coefficients.assistCoefficient}");

        // 更新击杀者
        if (_players.TryGetValue(killerId, out var killer))
        {
            killer.killCount++;
            killer.currentScore += coefficients.killCoefficient;
            Debug.Log($"  ✓ {killer.playerName} 击杀 +{coefficients.killCoefficient}分（总分：{killer.currentScore}）");
        }

        // 更新受害者
        if (_players.TryGetValue(victimId, out var victim))
        {
            victim.deathCount++;
            victim.currentScore += coefficients.deathCoefficient;
            Debug.Log($"  ✓ {victim.playerName} 死亡 {coefficients.deathCoefficient}分（总分：{victim.currentScore}）");
        }

        // 更新助攻者
        foreach (var aid in assistIds)
        {
            if (_players.TryGetValue(aid, out var assist))
            {
                assist.assistCount++;
                assist.currentScore += coefficients.assistCoefficient;
                Debug.Log($"  ✓ {assist.playerName} 助攻 +{coefficients.assistCoefficient}分（总分：{assist.currentScore}）");
            }
        }

        // ★ 关键：实时同步到 Team JSON
        SyncAllPlayersToTeamJson();

        Debug.Log($"[RoomPlayerManager] 击杀事件处理完成");
    }

    /// <summary>
    /// ★ 更新玩家血量（游戏中实时调用）
    /// </summary>
    public void UpdatePlayerBlood( string playerId, int newBlood )
    {
        if (_players.TryGetValue(playerId, out var player))
        {
            player.currentBlood = Mathf.Max(0, newBlood);  // 血量不能为负
            Debug.Log($"✓ {player.playerName} 血量已更新: {player.currentBlood}");

            // ★ 立即同步到 JSON
            SyncPlayerToTeamJson(player);
        }
    }

    /// <summary>
    /// ★ 单个玩家同步到 Team JSON
    /// </summary>
    private void SyncPlayerToTeamJson( PlayerData playerData )
    {
        if (_teamData == null) return;

        string teamType = playerData.team;
        TeamInfo targetTeam = _teamData.teams.FirstOrDefault(t =>
            (t.teamId.Contains("Red") && teamType == "red") ||
            (t.teamId.Contains("Blue") && teamType == "blue"));

        if (targetTeam != null)
        {
            // 删除已存在的玩家数据
            targetTeam.players.RemoveAll(p => p.playerId == playerData.playerId);

            // 添加更新后的玩家数据
            targetTeam.players.Add(new TeamPlayer(playerData.playerId, playerData.playerName)
            {
                killCount = playerData.killCount,
                deathCount = playerData.deathCount,
                assistCount = playerData.assistCount,
                currentScore = playerData.currentScore,
                currentBlood = playerData.currentBlood  // ★ 同步血量
            });

            targetTeam.alivePlayerCount = targetTeam.players.Count;

            // 保存到 JSON
            TeamJsonFileHandler.Instance.SaveTeamsData(_roomId, _teamData);
        }
    }

    /// <summary>
    /// ★ 同步所有玩家数据到 Team JSON
    /// </summary>
    private void SyncAllPlayersToTeamJson( )
    {
        if (_teamData == null || string.IsNullOrEmpty(_roomId)) return;

        foreach (TeamInfo team in _teamData.teams)
        {
            team.players.Clear();

            string teamType = team.teamId.Contains("Red") || team.teamId.Contains("red") ? "red" : "blue";
            var teamPlayers = _players.Values.Where(p => p.team == teamType).ToList();

            foreach (var player in teamPlayers)
            {
                team.players.Add(new TeamPlayer(player.playerId, player.playerName)
                {
                    killCount = player.killCount,
                    deathCount = player.deathCount,
                    assistCount = player.assistCount,
                    currentScore = player.currentScore,
                    currentBlood = player.currentBlood  // ★ 同步血量
                });
            }
            team.alivePlayerCount = team.players.Count;
        }

        TeamJsonFileHandler.Instance.SaveTeamsData(_roomId, _teamData);

        int redCount = _players.Values.Count(p => p.team == "red");
        int blueCount = _players.Values.Count(p => p.team == "blue");
        Debug.Log($"✓ 玩家数据已同步到 Team JSON（红队{redCount}人，蓝队{blueCount}人）");
    }

    /// <summary>
    /// ★ 生成游戏结果 - 从内存数据中计算
    /// </summary>
    public GameResult GenerateResult( )
    {
        int redTeamScore = 0;
        int blueTeamScore = 0;

        foreach (var player in _players.Values)
        {
            if (player.team == "red")
            {
                redTeamScore += player.currentScore;
            }
            else if (player.team == "blue")
            {
                blueTeamScore += player.currentScore;
            }
        }

        string winningTeam = redTeamScore > blueTeamScore ? "red" : "blue";
        int winningScore = Mathf.Max(redTeamScore, blueTeamScore);
        int losingScore = Mathf.Min(redTeamScore, blueTeamScore);

        GameResult result = new GameResult(
            _roomInstance.roomData.gameId,
            winningTeam,
            winningScore,
            losingScore
        );

        Debug.Log($"[RoomPlayerManager] 游戏结果已生成: {winningTeam}队胜利 ({winningScore} vs {losingScore})");

        return result;
    }

    /// <summary>
    /// 获取指定玩家的数据（用于其他系统查询）
    /// </summary>
    public PlayerData GetPlayer( string playerId )
    {
        return _players.TryGetValue(playerId, out var player) ? player : null;
    }

    /// <summary>
    /// 获取所有玩家列表
    /// </summary>
    public List<PlayerData> GetAllPlayers( )
    {
        return _players.Values.ToList();
    }

    /// <summary>
    /// 获取指定队伍的玩家列表
    /// </summary>
    public List<PlayerData> GetTeamPlayers( string team )
    {
        return _players.Values.Where(p => p.team == team).ToList();
    }
}
