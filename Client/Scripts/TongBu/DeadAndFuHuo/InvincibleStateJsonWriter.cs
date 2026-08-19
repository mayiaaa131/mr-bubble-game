using UnityEngine;
using System;
using System.IO;
using System.Text;
using System.Collections.Generic;

/// <summary>
/// 无敌状态 JSON 写入器
/// 负责把玩家无敌状态写入到 JSON 文件
/// 文件路径：Assets/json/invinciblestate/InvincibleState_{roomId}.json
/// </summary>
public class InvincibleStateJsonWriter : MonoBehaviour
{
    public static InvincibleStateJsonWriter Instance { get; private set; }

    [SerializeField] private string invincibleStateFolderPath = "Assets/json/invinciblestate";

    private int lastInvincibleCount = 0;

    private void Awake( )
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>
    /// 保存无敌状态到 JSON 文件
    /// </summary>
    public bool SaveInvincibleStateToFile( string roomId, InvincibleStateMessage invincibleMsg )
    {
        if (invincibleMsg == null)
            return false;

        try
        {
            if (!Directory.Exists(invincibleStateFolderPath))
                Directory.CreateDirectory(invincibleStateFolderPath);

            string path = Path.Combine(invincibleStateFolderPath, $"InvincibleState_{roomId}.json");
            string json = JsonUtility.ToJson(invincibleMsg, true);
            File.WriteAllText(path, json, Encoding.UTF8);

            lastInvincibleCount = invincibleMsg.invincibleStates.Count;
            //Debug.Log($"✅ InvincibleState 已写入: {path} (包含 {lastInvincibleCount} 个玩家)");
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"❌ 写入无敌状态失败: {e.Message}");
            return false;
        }
    }

    /// <summary>
    /// 从文件读取无敌状态（用于服务器重启恢复）
    /// </summary>
    public InvincibleStateMessage LoadInvincibleStateFromFile( string roomId )
    {
        try
        {
            string path = Path.Combine(invincibleStateFolderPath, $"InvincibleState_{roomId}.json");
            if (!File.Exists(path))
            {
                Debug.LogWarning($"⚠ 无敌状态文件不存在: {path}");
                return null;
            }

            string json = File.ReadAllText(path, Encoding.UTF8);
            InvincibleStateMessage invincibleState = JsonUtility.FromJson<InvincibleStateMessage>(json);
            Debug.Log($"✅ InvincibleState 已读取: {path}");
            return invincibleState;
        }
        catch (Exception e)
        {
            Debug.LogError($"❌ 读取无敌状态失败: {e.Message}");
            return null;
        }
    }

    /// <summary>
    /// 计算消息中的总无敌玩家数
    /// </summary>
    private int GetTotalInvinciblePlayerCount( InvincibleStateMessage msg )
    {
        int count = 0;
        if (msg.invincibleStates != null)
        {
            count = msg.invincibleStates.Count;
        }
        return count;
    }
}
