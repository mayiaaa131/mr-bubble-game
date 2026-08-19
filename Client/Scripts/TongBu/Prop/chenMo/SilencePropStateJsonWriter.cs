using UnityEngine;
using System;
using System.IO;
using System.Text;

/// <summary>
/// 沉默道具持有状态 JSON 写入器（仅用于调试）
/// 文件路径：Assets/json/silencepropstate/SilencePropState_{roomId}.json
/// 参考 PropStateJsonWriter 写法
/// </summary>
public class SilencePropStateJsonWriter : MonoBehaviour
{
    public static SilencePropStateJsonWriter Instance { get; private set; }

    [SerializeField] private string silencePropStateFolderPath = "Assets/json/silencepropstate";

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>
    /// 保存沉默道具持有状态到 JSON 文件（调试用）
    /// </summary>
    public bool SaveSilencePropStateToFile(string roomId, SilencePropHoldStateMessage msg)
    {
        if (msg == null)
            return false;

        try
        {
            if (!Directory.Exists(silencePropStateFolderPath))
                Directory.CreateDirectory(silencePropStateFolderPath);

            string path = Path.Combine(silencePropStateFolderPath, $"SilencePropState_{roomId}.json");
            string json = JsonUtility.ToJson(msg, true);
            File.WriteAllText(path, json, Encoding.UTF8);

            Debug.Log($"✅ SilencePropState 已写入: {path}");
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"❌ 写入沉默道具状态失败: {e.Message}");
            return false;
        }
    }

    /// <summary>
    /// 从文件读取沉默道具持有状态
    /// </summary>
    public SilencePropHoldStateMessage LoadSilencePropStateFromFile(string roomId)
    {
        try
        {
            string path = Path.Combine(silencePropStateFolderPath, $"SilencePropState_{roomId}.json");
            if (!File.Exists(path))
            {
                Debug.LogWarning($"⚠ 沉默道具状态文件不存在: {path}");
                return null;
            }

            string json = File.ReadAllText(path, Encoding.UTF8);
            SilencePropHoldStateMessage result = JsonUtility.FromJson<SilencePropHoldStateMessage>(json);
            Debug.Log($"✅ SilencePropState 已读取: {path}");
            return result;
        }
        catch (Exception e)
        {
            Debug.LogError($"❌ 读取沉默道具状态失败: {e.Message}");
            return null;
        }
    }
}
