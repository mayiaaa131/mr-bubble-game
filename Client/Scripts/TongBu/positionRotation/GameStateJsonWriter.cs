using UnityEngine;
using System;
using System.IO;
using System.Text;
using System.Collections.Generic;

/// <summary>
/// 游戏状态 JSON 写入器（非单例版本）
/// ★ 改造核心：
/// 1. 删除 public static Instance
/// 2. 由 RoomGameManager 创建一个实例
/// 3. 所有调用改为通过注入的引用
/// </summary>
public class GameStateJsonWriter : MonoBehaviour
{
    [SerializeField] private string gameStateFolderPath = "Assets/json/gamestate";

    private GameStateData currentGameState;
    private string currentRoomId;
    private bool isInitialized = false;

    /// <summary>
    /// 初始化游戏状态（由RoomGameManager调用）
    /// 从 Team JSON 读取队伍和玩家初始结构
    /// </summary>
    public void InitFromTeamJson(string roomId)
    {
        if (isInitialized) return;

        Debug.Log($"========== GameStateJsonWriter.InitFromTeamJson 开始 ==========");
        Debug.Log($"房间ID: {roomId}");

        currentRoomId = roomId;
        currentGameState = new GameStateData(roomId);

        RoomTeamsData source = TeamJsonFileHandler.Instance.LoadTeamsData(roomId);

        if (source == null)
        {
            Debug.LogError($"❌ GameStateJsonWriter: 无法读取 Team JSON: {roomId}");
            return;
        }

        Debug.Log($"✓ 已读取 Team JSON，共 {source.teams.Count} 支队伍");

        foreach (TeamInfo team in source.teams)
        {
            GameStateTeam gsTeam = new GameStateTeam(team.teamId, team.teamName);

            foreach (TeamPlayer player in team.players)
            {
                GameStatePlayer gsPlayer = new GameStatePlayer(player.playerId, player.playerName);
                gsTeam.players.Add(gsPlayer);
                Debug.Log($"  → 添加玩家: {player.playerName} (ID={player.playerId})");
            }

            currentGameState.teams.Add(gsTeam);
            Debug.Log($"✓ 队伍 {team.teamName} 已添加，共 {gsTeam.players.Count} 个玩家");
        }

        SaveToFile();

        isInitialized = true;
        Debug.Log($"✅ GameStateJsonWriter 初始化完成: {roomId}");
        Debug.Log($"  - 队伍数: {currentGameState.teams.Count}");
        Debug.Log($"========== GameStateJsonWriter.InitFromTeamJson 完成 ==========");
    }

    /// <summary>
    /// 更新某玩家的位置和旋转
    /// </summary>
    public void UpdatePlayerTransform(string playerId,
        float px, float py, float pz,
        float rx, float ry, float rz, float rw)
    {
        if (currentGameState == null)
        {
            Debug.LogWarning("⚠ GameState 未初始化，忽略位置更新");
            return;
        }

        foreach (GameStateTeam team in currentGameState.teams)
        {
            GameStatePlayer player = team.players.Find(p => p.playerId == playerId);
            if (player != null)
            {
                player.position = new GSPosition(px, py, pz);
                player.rotation = new GSRotation(rx, ry, rz, rw);
                return;
            }
        }

        Debug.LogWarning($"⚠ 找不到玩家 {playerId}，无法更新位置");
    }

    /// <summary>
    /// 写入 WorldState_room_001.json 文件
    /// </summary>
    public void SaveToFile()
    {
        if (currentGameState == null) return;

        currentGameState.timestamp = System.DateTime.Now.Ticks;

        try
        {
            if (!Directory.Exists(gameStateFolderPath))
                Directory.CreateDirectory(gameStateFolderPath);

            string path = Path.Combine(gameStateFolderPath,
                          $"WorldState_{currentRoomId}.json");
            string json = JsonUtility.ToJson(currentGameState, true);
            File.WriteAllText(path, json, Encoding.UTF8);
            //Debug.Log($"✅ WorldState 已写入: {path}");
        }
        catch (Exception e)
        {
            //Debug.LogError($"❌ 写入失败: {e.Message}");
        }
    }

    /// <summary>
    /// 获取当前状态的 JSON 字符串（用于广播给客户端）
    /// </summary>
    public string GetCurrentStateJson()
    {
        if (currentGameState == null) return "{}";
        currentGameState.timestamp = System.DateTime.Now.Ticks;
        return JsonUtility.ToJson(currentGameState, true);
    }

    /// <summary>
    /// 获取当前游戏状态（供其他Manager访问）
    /// </summary>
    public GameStateData GetCurrentGameState()
    {
        return currentGameState;
    }

    /// <summary>
    /// 清空游戏状态
    /// </summary>
    public void ClearGameState()
    {
        try
        {
            currentGameState = null;
            currentRoomId = "";
            isInitialized = false;

            Debug.Log("[GameStateJsonWriter] ✅ 游戏状态已清空");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[GameStateJsonWriter] ❌ 清空失败: {ex.Message}");
        }
    }



    /// <summary>
    /// 检查是否已初始化
    /// </summary>
    public bool IsInitialized() => isInitialized;

    /// <summary>
    /// 获取当前房间ID
    /// </summary>
    public string GetCurrentRoomId() => currentRoomId;
}
