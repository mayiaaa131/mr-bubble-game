using UnityEngine;
using System;
using System.IO;
using System.Text;

/// <summary>
/// 游戏结束 JSON 写入器
/// 负责把游戏结束消息写入到 JSON 文件
/// 文件路径：Assets/json/gameend/GameEnd_{roomId}.json
/// </summary>
public class GameEndJsonWriter : MonoBehaviour
{
    public static GameEndJsonWriter Instance { get; private set; }

    [SerializeField] private string gameEndFolderPath = "Assets/json/gameend";

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>
    /// 保存游戏结束消息到 JSON 文件
    /// </summary>
    public bool SaveGameEndToFile(string roomId, GameEndMessage gameEndMsg)
    {
        if (gameEndMsg == null)
            return false;

        try
        {
            if (!Directory.Exists(gameEndFolderPath))
                Directory.CreateDirectory(gameEndFolderPath);

            string path = Path.Combine(gameEndFolderPath, $"GameEnd_{roomId}.json");
            string json = JsonUtility.ToJson(gameEndMsg, true);
            File.WriteAllText(path, json, Encoding.UTF8);

            //Debug.Log($"✅ GameEnd 已写入: {path}");
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"❌ 写入游戏结束消息失败: {e.Message}");
            return false;
        }
    }

    /// <summary>
    /// 从文件读取游戏结束消息
    /// </summary>
    public GameEndMessage LoadGameEndFromFile(string roomId)
    {
        try
        {
            string path = Path.Combine(gameEndFolderPath, $"GameEnd_{roomId}.json");
            if (!File.Exists(path))
            {
                Debug.LogWarning($"⚠ 游戏结束文件不存在: {path}");
                return null;
            }

            string json = File.ReadAllText(path, Encoding.UTF8);
            GameEndMessage gameEndMsg = JsonUtility.FromJson<GameEndMessage>(json);
            Debug.Log($"✅ GameEnd 已读取: {path}");
            return gameEndMsg;
        }
        catch (Exception e)
        {
            Debug.LogError($"❌ 读取游戏结束消息失败: {e.Message}");
            return null;
        }
    }
}
