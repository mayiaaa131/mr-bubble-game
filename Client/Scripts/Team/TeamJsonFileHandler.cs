using UnityEngine;
using System;
using System.IO;
using System.Collections.Generic;
using System.Text;

/// <summary>
/// Team JSON 文件处理器（数据层）
/// 负责所有 Team JSON 文件的读写操作
/// 每个房间一个 Team JSON 文件：Team_room_001.json
/// </summary>
public class TeamJsonFileHandler : MonoBehaviour
{
    private static TeamJsonFileHandler instance;

    [SerializeField] private string teamJsonFolder = "Assets/json/teams";  // Team JSON 文件夹

    private void Awake()
    {
        // 单例模式
        if (instance == null)
        {
            instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// 获取指定房间的 Team JSON 文件路径
    /// </summary>
    private string GetTeamJsonPath(string roomId)
    {
        return Path.Combine(teamJsonFolder, $"Team_{roomId}.json");
    }

    /// <summary>
    /// 将 JSON 字符串写入文件
    /// </summary>
    public void WriteJsonToFile(string path, string jsonContent)
    {
        try
        {
            // 确保目录存在
            string directory = Path.GetDirectoryName(path);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
                Debug.Log($"✓ 创建目录: {directory}");
            }

            File.WriteAllText(path, jsonContent, Encoding.UTF8);
            Debug.Log($"✓ JSON 文件写入成功: {path}");
        }
        catch (Exception e)
        {
            Debug.LogError($"❌ 写入 JSON 文件失败: {path} - {e.Message}");
        }
    }

    /// <summary>
    /// 从文件读取 JSON 字符串
    /// </summary>
    public string ReadJsonFromFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                return File.ReadAllText(path, Encoding.UTF8);
            }
            else
            {
                Debug.LogWarning($"⚠ JSON 文件不存在: {path}");
                return null;
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"❌ 读取 JSON 文件失败: {path} - {e.Message}");
            return null;
        }
    }

    // ==================== Team JSON 操作 ====================

    /// <summary>
    /// 为新创建的房间保存 Team 数据
    /// 包含红队和蓝队的初始化数据
    /// </summary>
    public void SaveTeamsDataForNewRoom(string roomId, string teamRedId, string teamBlueId)
    {
        Debug.Log($"========== 保存 Team JSON: {roomId} ==========");

        try
        {
            // 创建房间的队伍数据容器
            RoomTeamsData roomTeamsData = new RoomTeamsData(roomId);

            // ✓ 红队初始化
            TeamInfo redTeam = new TeamInfo(roomId, teamRedId, "红队");
            redTeam.alivePlayerCount = 0;
            redTeam.totalScore = 0;
            roomTeamsData.teams.Add(redTeam);

            Debug.Log($"✓ 红队已初始化: {teamRedId}");

            // ✓ 蓝队初始化
            TeamInfo blueTeam = new TeamInfo(roomId, teamBlueId, "蓝队");
            blueTeam.alivePlayerCount = 0;
            blueTeam.totalScore = 0;
            roomTeamsData.teams.Add(blueTeam);

            Debug.Log($"✓ 蓝队已初始化: {teamBlueId}");

            // ✓ 序列化并保存
            string json = JsonUtility.ToJson(roomTeamsData, true);
            string path = GetTeamJsonPath(roomId);
            WriteJsonToFile(path, json);

            Debug.Log($"✓ Team JSON 已保存");
            Debug.Log($"  - 文件路径: {path}");
            Debug.Log($"  - 房间ID: {roomId}");
            Debug.Log($"  - 队伍数: {roomTeamsData.teams.Count}");

            Debug.Log($"========== Team JSON 保存完成 ==========");
        }
        catch (Exception e)
        {
            Debug.LogError($"❌ 保存 Team JSON 失败: {e.Message}\n{e.StackTrace}");
        }
    }

    /// <summary>
    /// 加载房间的 Team 数据
    /// </summary>
    public RoomTeamsData LoadTeamsData(string roomId)
    {
        try
        {
            string path = GetTeamJsonPath(roomId);
            string json = ReadJsonFromFile(path);

            if (json == null)
            {
                Debug.LogWarning($"⚠ 无法读取 Team JSON: {roomId}");
                return null;
            }

            RoomTeamsData roomTeamsData = JsonUtility.FromJson<RoomTeamsData>(json);

            if (roomTeamsData == null)
            {
                Debug.LogError($"❌ 解析 Team JSON 失败: {roomId}");
                return null;
            }

            //Debug.Log($"✓ Team JSON 已加载: {roomId}");
            //Debug.Log($"  - 队伍数: {roomTeamsData.teams.Count}");

            return roomTeamsData;
        }
        catch (Exception e)
        {
            Debug.LogError($"❌ 加载 Team JSON 失败: {e.Message}\n{e.StackTrace}");
            return null;
        }
    }

    /// <summary>
    /// 获取指定队伍的数据
    /// </summary>
    public TeamInfo GetTeamInfo(string roomId, string teamId)
    {
        RoomTeamsData roomTeamsData = LoadTeamsData(roomId);

        if (roomTeamsData == null)
        {
            return null;
        }

        foreach (TeamInfo team in roomTeamsData.teams)
        {
            if (team.teamId == teamId)
            {
                return team;
            }
        }

        Debug.LogWarning($"⚠ 未找到队伍: {teamId} (房间: {roomId})");
        return null;
    }

    /// <summary>
    /// 向队伍添加玩家
    /// </summary>
    public bool AddPlayerToTeam(string roomId, string teamId, TeamPlayer player)
    {
        try
        {
            RoomTeamsData roomTeamsData = LoadTeamsData(roomId);

            if (roomTeamsData == null)
            {
                Debug.LogError($"❌ 无法加载房间 {roomId} 的 Team 数据");
                return false;
            }

            // 找到对应的队伍
            bool found = false;
            foreach (TeamInfo team in roomTeamsData.teams)
            {
                if (team.teamId == teamId)
                {
                    // 检查玩家是否已存在
                    bool playerExists = false;
                    foreach (TeamPlayer existingPlayer in team.players)
                    {
                        if (existingPlayer.playerId == player.playerId)
                        {
                            playerExists = true;
                            break;
                        }
                    }

                    if (!playerExists)
                    {
                        team.players.Add(player);
                        team.alivePlayerCount = team.players.Count;
                        found = true;

                        Debug.Log($"✓ 玩家 {player.playerName} (ID={player.playerId}) 已添加到队伍 {teamId}");
                    }
                    else
                    {
                        Debug.LogWarning($"⚠ 玩家 {player.playerId} 已存在于队伍 {teamId}");
                    }

                    break;
                }
            }

            if (!found)
            {
                Debug.LogError($"❌ 未找到队伍: {teamId}");
                return false;
            }

            // 保存更新
            SaveTeamsData(roomId, roomTeamsData);
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"❌ 添加玩家到队伍失败: {e.Message}\n{e.StackTrace}");
            return false;
        }
    }

    /// <summary>
    /// 从队伍移除玩家
    /// </summary>
    public bool RemovePlayerFromTeam(string roomId, string teamId, string playerId)
    {
        try
        {
            RoomTeamsData roomTeamsData = LoadTeamsData(roomId);

            if (roomTeamsData == null)
            {
                Debug.LogError($"❌ 无法加载房间 {roomId} 的 Team 数据");
                return false;
            }

            bool found = false;
            foreach (TeamInfo team in roomTeamsData.teams)
            {
                if (team.teamId == teamId)
                {
                    team.players.RemoveAll(p => p.playerId == playerId);
                    team.alivePlayerCount = team.players.Count;
                    found = true;

                    Debug.Log($"✓ 玩家 {playerId} 已从队伍 {teamId} 移除");
                    break;
                }
            }

            if (!found)
            {
                Debug.LogError($"❌ 未找到队伍: {teamId}");
                return false;
            }

            // 保存更新
            SaveTeamsData(roomId, roomTeamsData);
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"❌ 移除玩家失败: {e.Message}\n{e.StackTrace}");
            return false;
        }
    }

    /// <summary>
    /// 保存房间的 Team 数据
    /// </summary>
    public void SaveTeamsData(string roomId, RoomTeamsData roomTeamsData)
    {
        try
        {
            string json = JsonUtility.ToJson(roomTeamsData, true);
            string path = GetTeamJsonPath(roomId);
            WriteJsonToFile(path, json);

            Debug.Log($"✓ Team 数据已更新: {roomId}");
            Debug.Log($"  - 红队玩家: {(roomTeamsData.teams.Count > 0 ? roomTeamsData.teams[0].players.Count : 0)}");
            Debug.Log($"  - 蓝队玩家: {(roomTeamsData.teams.Count > 1 ? roomTeamsData.teams[1].players.Count : 0)}");
        }
        catch (Exception e)
        {
            Debug.LogError($"❌ 保存 Team 数据失败: {e.Message}\n{e.StackTrace}");
        }
    }

   

    // 单例访问方法
    public static TeamJsonFileHandler Instance
    {
        get
        {
            if (instance == null)
            {
                Debug.LogError("❌ TeamJsonFileHandler 单例未初始化");
            }
            return instance;
        }
    }

    /// <summary>
    /// 更新房间的 Team 数据（修改功能）
    /// 用于同步玩家信息时调用
    /// </summary>
    public void UpdateTeamsData( string roomId, RoomTeamsData roomTeamsData )
    {
        try
        {
            SaveTeamsData(roomId, roomTeamsData);
            Debug.Log($"✓ Team JSON 已更新: {roomId}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ 更新 Team JSON 失败: {e.Message}");
        }
    }

    /// <summary>
    /// 删除房间的所有 Team 数据文件
    /// 在删除房间时调用
    /// </summary>
    public void DeleteTeamJsonFile( string roomId )
    {
        try
        {
            string path = GetTeamJsonPath(roomId);

            if (System.IO.File.Exists(path))
            {
                System.IO.File.Delete(path);
                Debug.Log($"✓ Team JSON 文件已删除: {roomId}");
                Debug.Log($"  - 文件路径: {path}");
            }
            else
            {
                Debug.LogWarning($"⚠ Team JSON 文件不存在: {path}");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ 删除 Team JSON 文件失败: {roomId} - {e.Message}");
        }
    }

    /// <summary>
    /// 清除房间的所有玩家数据（保留队伍结构）
    /// 用于重置房间时调用
    /// </summary>
    public void ClearTeamPlayersData( string roomId )
    {
        try
        {
            RoomTeamsData roomTeamsData = LoadTeamsData(roomId);

            if (roomTeamsData == null)
            {
                Debug.LogWarning($"⚠ 无法加载 Team JSON: {roomId}");
                return;
            }

            foreach (TeamInfo team in roomTeamsData.teams)
            {
                team.players.Clear();
                team.alivePlayerCount = 0;
                Debug.Log($"✓ 队伍 {team.teamName} 的玩家已清空");
            }

            SaveTeamsData(roomId, roomTeamsData);
            Debug.Log($"✓ 房间 {roomId} 的所有玩家数据已清空");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ 清除玩家数据失败: {e.Message}");
        }
    }

}
