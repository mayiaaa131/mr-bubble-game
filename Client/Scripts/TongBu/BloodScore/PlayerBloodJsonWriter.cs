using UnityEngine;
using System;
using System.IO;
using System.Text;
using System.Collections.Generic;

/// <summary>
/// 玩家血量 JSON 写入器
/// 负责把玩家血量状态写入到 JSON 文件
/// 文件路径：Assets/json/playerblood/PlayerBlood_{roomId}.json
/// </summary>
public class PlayerBloodJsonWriter : MonoBehaviour
{
    public static PlayerBloodJsonWriter Instance { get; private set; }

    [SerializeField] private string playerBloodFolderPath = "Assets/json/playerblood";

    private int lastPlayerCount = 0;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>
    /// 保存玩家血量到 JSON 文件
    /// </summary>
    public bool SavePlayerBloodToFile(string roomId, PlayersBloodMessage bloodMsg)
    {
        if (bloodMsg == null)
            return false;

        try
        {
            if (!Directory.Exists(playerBloodFolderPath))
                Directory.CreateDirectory(playerBloodFolderPath);

            string path = Path.Combine(playerBloodFolderPath, $"PlayerBlood_{roomId}.json");
            string json = JsonUtility.ToJson(bloodMsg, true);
            File.WriteAllText(path, json, Encoding.UTF8);

            lastPlayerCount = GetTotalPlayerCount(bloodMsg);
            //Debug.Log($"✅ PlayerBlood 已写入: {path} (包含 {lastPlayerCount} 个玩家)");
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"❌ 写入玩家血量失败: {e.Message}");
            return false;
        }
    }

    /// <summary>
    /// 从文件读取玩家血量（用于服务器重启恢复）
    /// </summary>
    public PlayersBloodMessage LoadPlayerBloodFromFile(string roomId)
    {
        try
        {
            string path = Path.Combine(playerBloodFolderPath, $"PlayerBlood_{roomId}.json");
            if (!File.Exists(path))
            {
                Debug.LogWarning($"⚠ 玩家血量文件不存在: {path}");
                return null;
            }

            string json = File.ReadAllText(path, Encoding.UTF8);
            PlayersBloodMessage bloodState = JsonUtility.FromJson<PlayersBloodMessage>(json);
            Debug.Log($"✅ PlayerBlood 已读取: {path}");
            return bloodState;
        }
        catch (Exception e)
        {
            Debug.LogError($"❌ 读取玩家血量失败: {e.Message}");
            return null;
        }
    }

    /// <summary>
    /// 计算消息中的总玩家数
    /// </summary>
    private int GetTotalPlayerCount(PlayersBloodMessage msg)
    {
        int count = 0;
        if (msg.teams != null)
        {
            foreach (var team in msg.teams)
            {
                if (team.players != null)
                    count += team.players.Count;
            }
        }
        return count;
    }
}
