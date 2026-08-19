using UnityEngine;
using System;
using System.IO;
using System.Text;
using System.Collections.Generic;

/// <summary>
/// 炸弹状态 JSON 写入器（v2 修复版）
/// 文件路径：Assets/json/bombstate/BombState_{roomId}.json
///
/// ★ 修复目标：
/// 1. 顶层字段顺序与老版本 BombStateMessage 完全一致
///    （type / roomId / timestamp / isRetransmit / frameSequenceNumber / bombs）
/// 2. 每颗炸弹字段顺序与老版本 BombData 完全一致，在末尾追加可读性描述字段
/// 3. 从炸弹（isAbsorbed=true）不跳过，完整输出其 explosionRanges
/// 4. 保留新版本的升级摘要信息
/// </summary>
public class BombStateJsonWriter : MonoBehaviour
{
    public static BombStateJsonWriter Instance { get; private set; }

    [SerializeField] private string bombStateFolderPath = "Assets/json/bombstate";

    private int lastBombCount = 0;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
        DontDestroyOnLoad(gameObject);
    }

    // ══════════════════════════════════════════════════════════════════
    // ★ 输出包装类：字段顺序严格对齐老版本，末尾追加摘要和描述字段
    // ══════════════════════════════════════════════════════════════════

    /// <summary>
    /// 顶层输出结构
    /// ★ 前6个字段与老版本 BombStateMessage 完全一致，之后追加升级摘要
    /// </summary>
    [Serializable]
    private class BombStateOutput
    {
        // ── ★ 与老版本完全相同的核心字段（顺序一致）──────────────
        public string type;
        public string roomId;
        public long timestamp;
        public bool isRetransmit;
        public long frameSequenceNumber;
        public List<BombEntryOutput> bombs;

        // ── ★ 新增：升级摘要字段（追加在 bombs 后面）────────────
        public int _total_master_bombs;
        public int _total_absorbed_bombs;
        public int _level1_count;
        public int _level2_count;
        public int _level3_count;

        public BombStateOutput()
        {
            bombs = new List<BombEntryOutput>();
        }
    }

    /// <summary>
    /// 单颗炸弹输出结构
    /// ★ 前17个字段与老版本 BombData 完全一致（含顺序），末尾追加可读性描述
    /// ★ 从炸弹（isAbsorbed=true）也完整输出 explosionRanges
    /// </summary>
    [Serializable]
    private class BombEntryOutput
    {
        public string bombId;
        public string playerId;
        public string teamId;
        public GSPosition position;
        public float totalTime;
        public float remainingTime;
        public string state;
        public string bombType;
        public string bombLevel;
        public List<ExplosionRange> explosionRanges;
        public long createTime;
        public long explosionTimestamp;
        public long serverTimestamp;
        public int mergeCount;
        public List<string> mergedBombIds;
        public bool isMaster;
        public string mergeGroupId;   // ★ 替换 isAbsorbed  

        // 可读性描述字段  
        public string _desc_role;
        public string _desc_level;
        public string _desc_timer;
        public string _desc_damage;
        public string _desc_ranges;
        public string _desc_mergedIds;
        public string _desc_position;
    }

    // ══════════════════════════════════════════════════════════════════
    // ★ 核心方法：保存炸弹状态
    // ══════════════════════════════════════════════════════════════════

    public bool SaveBombStateToFile(string roomId, BombStateMessage bombStateMsg)
    {
        if (bombStateMsg == null)
            return false;

        try
        {
            if (!Directory.Exists(bombStateFolderPath))
                Directory.CreateDirectory(bombStateFolderPath);

            string path = Path.Combine(bombStateFolderPath, $"BombState_{roomId}.json");

            BombStateOutput output = BuildOutput(bombStateMsg);

            string json = JsonUtility.ToJson(output, prettyPrint: true);
            File.WriteAllText(path, json, Encoding.UTF8);

            lastBombCount = bombStateMsg.bombs.Count;



            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[BombStateJsonWriter] ❌ 写入炸弹状态失败: {e.Message}");
            return false;
        }
    }

    // ══════════════════════════════════════════════════════════════════
    // ★ 私有方法：构建输出对象
    // ══════════════════════════════════════════════════════════════════

    private BombStateOutput BuildOutput(BombStateMessage source)
    {
        long now = System.DateTime.Now.Ticks / 10000;
        string timeStr = System.DateTime.Now.ToString("HH:mm:ss.fff");

        // ★ 顶层字段：严格按老版本顺序赋值，timestamp 使用原始值
        BombStateOutput output = new BombStateOutput
        {
            type = source.type,
            roomId = source.roomId,
            timestamp = System.DateTime.Now.Ticks,
            isRetransmit = source.isRetransmit,
            frameSequenceNumber = source.frameSequenceNumber,
        };

        // ── ★ 关键：遍历所有炸弹，主炸弹和从炸弹都完整统计 ────────
        int level1 = 0, level2 = 0, level3 = 0;
        int masterCount = 0, slaveCount = 0;

        foreach (BombData bomb in source.bombs)
        {
            if (!bomb.isMaster)
            {
                slaveCount++;
                continue; // 从炸弹不参与等级统计
            }

            masterCount++;
            switch (bomb.mergeCount)
            {
                case 1: level1++; break;
                case 2: level2++; break;
                case 3: level3++; break;
                default: level3++; break;
            }
        }

        output._total_master_bombs = masterCount;
        output._total_absorbed_bombs = slaveCount; // 字段名保持兼容，含义改为"从炸弹数"
        output._level1_count = level1;
        output._level2_count = level2;
        output._level3_count = level3;

        // ── ★ 构建每颗炸弹条目（主炸弹 + 从炸弹全部输出）────────────
        foreach (BombData bomb in source.bombs)
        {
            output.bombs.Add(BuildBombEntry(bomb, now));
        }

        return output;
    }

    /// <summary>
    /// 构建单颗炸弹输出条目
    /// ★ 核心修复：从炸弹（isAbsorbed=true）也完整赋值 explosionRanges，不丢弃
    /// </summary>
    private BombEntryOutput BuildBombEntry(BombData bomb, long now)
    {
        BombEntryOutput entry = new BombEntryOutput
        {
            // ── ★ 原始字段：严格按 BombData 定义顺序赋值 ────────────
            bombId = bomb.bombId,
            playerId = bomb.playerId,
            teamId = bomb.teamId,
            position = bomb.position,
            totalTime = bomb.totalTime,
            remainingTime = bomb.remainingTime,
            state = bomb.state,
            bombType = bomb.bombType,
            bombLevel = bomb.bombLevel,
            explosionRanges = bomb.explosionRanges ?? new List<ExplosionRange>(), // ★ 从炸弹也完整保留
            createTime = bomb.createTime,
            explosionTimestamp = bomb.explosionTimestamp,
            serverTimestamp = now,
            mergeCount = bomb.mergeCount,
            mergedBombIds = bomb.mergedBombIds ?? new List<string>(),
            isMaster = bomb.isMaster,
            mergeGroupId = bomb.mergeGroupId  // ★ 替换 isAbsorbed 
        };

        // ── 可读性描述字段 ────────────────────────────────────────────

        // 1. 角色描述
        bool isSlave = !bomb.isMaster && !string.IsNullOrEmpty(bomb.mergeGroupId);
        if (isSlave)
            entry._desc_role = $"从炸弹（所属组: {bomb.mergeGroupId}，独立存在显示范围）";
        else if (bomb.isMaster && bomb.mergeCount >= 3)
            entry._desc_role = "主炸弹（三级封顶，不再合并）";
        else if (bomb.isMaster && bomb.mergeCount > 1)
            entry._desc_role = $"主炸弹（{bomb.mergeCount}颗合并，可读取并集范围）";
        else
            entry._desc_role = "独立炸弹（一级）";

        // 2. 等级描述
        entry._desc_level = bomb.mergeCount switch
        {
            1 => "一级炸弹（普通·3s）",
            2 => "二级炸弹（2颗合并·2s）",
            3 => "三级炸弹（3颗合并·1s·最高级·封顶）",
            _ => $"特殊等级（mergeCount={bomb.mergeCount}）"
        };

        // 3. 倒计时描述
        if (isSlave)
            entry._desc_timer = $"同步主炸弹倒计时：{bomb.remainingTime:F2}s（组ID: {bomb.mergeGroupId}）";
        else if (bomb.state == "Active")
        {
            float elapsedSec = (now - bomb.createTime) / 1000f;
            float calc = Mathf.Max(0f, bomb.totalTime - elapsedSec);
            entry._desc_timer = $"剩余 {calc:F2}s / 总计 {bomb.totalTime:F0}s";
        }

        // 4. 伤害描述
        int damage = bomb.mergeCount switch { 2 => 2, 3 => 3, _ => 1 };
        entry._desc_damage = $"💥 爆炸伤害：{damage} 滴血";

        // 5. ★ 范围描述（主/从炸弹分别说明）
        int rangeCount = entry.explosionRanges?.Count ?? 0;
        if (isSlave)
            entry._desc_ranges = $"从炸弹独立范围：{rangeCount} 块（客户端独立渲染）";
        else
            entry._desc_ranges = rangeCount > 0
                ? $"并集爆炸区域：{rangeCount} 块（含所有成员范围）"
                : "爆炸区域：未计算";

        // 6. 已吸收炸弹 ID 列表
        entry._desc_mergedIds = (bomb.mergedBombIds != null && bomb.mergedBombIds.Count > 0)
            ? $"已吸收炸弹：[{string.Join(", ", bomb.mergedBombIds)}]"
            : "已吸收炸弹：无";

        // 7. 位置描述
        entry._desc_position = bomb.position != null
            ? $"位置：({bomb.position.x:F2}, {bomb.position.y:F2}, {bomb.position.z:F2})"
            : "位置：未知";

        return entry;
    }

    // ══════════════════════════════════════════════════════════════════
    // 读取方法（保持与老版本完全兼容）
    // ══════════════════════════════════════════════════════════════════

    /// <summary>
    /// 从文件读取炸弹状态（服务器重启恢复用）
    /// ★ 读取时仍用原始 BombStateMessage 格式，向后兼容
    /// </summary>
    public BombStateMessage LoadBombStateFromFile(string roomId)
    {
        try
        {
            string path = Path.Combine(bombStateFolderPath, $"BombState_{roomId}.json");
            if (!File.Exists(path))
            {
                Debug.LogWarning($"[BombStateJsonWriter] ⚠ 炸弹状态文件不存在: {path}");
                return null;
            }

            string json = File.ReadAllText(path, Encoding.UTF8);
            BombStateMessage bombState = JsonUtility.FromJson<BombStateMessage>(json);
            Debug.Log($"[BombStateJsonWriter] ✅ BombState 已读取: {path}");
            return bombState;
        }
        catch (Exception e)
        {
            Debug.LogError($"[BombStateJsonWriter] ❌ 读取炸弹状态失败: {e.Message}");
            return null;
        }
    }
}
