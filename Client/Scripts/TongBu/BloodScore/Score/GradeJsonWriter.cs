using UnityEngine;
using System;
using System.IO;
using System.Text;

/// <summary>
/// 积分 JSON 写入器
/// 负责把积分状态写入到 JSON 文件
/// 文件路径：Assets/json/grade/Grade_{roomId}.json
/// </summary>
public class GradeJsonWriter : MonoBehaviour
{
    public static GradeJsonWriter Instance { get; private set; }

    [SerializeField] private string gradeFolderPath = "Assets/json/grade";

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>
    /// 保存积分到 JSON 文件
    /// </summary>
    public bool SaveGradeToFile(string roomId, GradeMessage gradeMsg)
    {
        if (gradeMsg == null)
            return false;

        try
        {
            if (!Directory.Exists(gradeFolderPath))
                Directory.CreateDirectory(gradeFolderPath);

            string path = Path.Combine(gradeFolderPath, $"Grade_{roomId}.json");
            string json = JsonUtility.ToJson(gradeMsg, true);
            File.WriteAllText(path, json, Encoding.UTF8);

            int totalPlayers = GetTotalPlayerCount(gradeMsg);
            //Debug.Log($"✅ Grade 已写入: {path} (包含 {totalPlayers} 个玩家)");
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"❌ 写入积分失败: {e.Message}");
            return false;
        }
    }

    /// <summary>
    /// 从文件读取积分（用于服务器重启恢复）
    /// </summary>
    public GradeMessage LoadGradeFromFile(string roomId)
    {
        try
        {
            string path = Path.Combine(gradeFolderPath, $"Grade_{roomId}.json");
            if (!File.Exists(path))
            {
                Debug.LogWarning($"⚠ 积分文件不存在: {path}");
                return null;
            }

            string json = File.ReadAllText(path, Encoding.UTF8);
            GradeMessage gradeState = JsonUtility.FromJson<GradeMessage>(json);
            Debug.Log($"✅ Grade 已读取: {path}");
            return gradeState;
        }
        catch (Exception e)
        {
            Debug.LogError($"❌ 读取积分失败: {e.Message}");
            return null;
        }
    }

    /// <summary>
    /// 计算消息中的总玩家数
    /// </summary>
    private int GetTotalPlayerCount(GradeMessage msg)
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
