using UnityEngine;
using System;
using System.IO;
using System.Text;

/// <summary>
/// 道具状态 JSON 写入器
/// 负责把房间内的所有道具状态写入到 JSON 文件
/// 文件路径：Assets/json/propstate/PropState_{roomId}.json
/// </summary>
public class PropStateJsonWriter : MonoBehaviour
{
    public static PropStateJsonWriter Instance { get; private set; }

    [SerializeField] private string propStateFolderPath = "Assets/json/propstate";

    private int lastPropCount = 0;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>
    /// 保存道具状态到 JSON 文件
    /// </summary>
    public bool SavePropStateToFile(string roomId, PropStateMessage propStateMsg)
    {
        if (propStateMsg == null)
            return false;

        try
        {
            if (!Directory.Exists(propStateFolderPath))
                Directory.CreateDirectory(propStateFolderPath);

            string path = Path.Combine(propStateFolderPath, $"PropState_{roomId}.json");
            string json = JsonUtility.ToJson(propStateMsg, true);
            File.WriteAllText(path, json, Encoding.UTF8);

            lastPropCount = propStateMsg.props.Count;
            //Debug.Log($"✅ PropState 已写入: {path} (包含 {propStateMsg.props.Count} 个道具)");
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"❌ 写入道具状态失败: {e.Message}");
            return false;
        }
    }

    /// <summary>
    /// 从文件读取道具状态
    /// </summary>
    public PropStateMessage LoadPropStateFromFile(string roomId)
    {
        try
        {
            string path = Path.Combine(propStateFolderPath, $"PropState_{roomId}.json");
            if (!File.Exists(path))
            {
                Debug.LogWarning($"⚠ 道具状态文件不存在: {path}");
                return null;
            }

            string json = File.ReadAllText(path, Encoding.UTF8);
            PropStateMessage propState = JsonUtility.FromJson<PropStateMessage>(json);
            Debug.Log($"✅ PropState 已读取: {path}");
            return propState;
        }
        catch (Exception e)
        {
            Debug.LogError($"❌ 读取道具状态失败: {e.Message}");
            return null;
        }
    }
}
