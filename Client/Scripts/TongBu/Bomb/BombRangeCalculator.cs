using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 炸弹爆炸范围计算工具
/// </summary>
public static class BombRangeCalculator
{
    /// <summary>
    /// 根据炸弹类型和位置计算爆炸范围
    /// </summary>
    public static List<ExplosionRange> CalculateExplosionRanges(
        string bombType,
        Vector3 position)
    {
        List<ExplosionRange> ranges = new List<ExplosionRange>();

        switch (bombType)
        {
            case "长条形":
                ranges.Add(new ExplosionRange(
                    position.x - 2f,
                    position.x + 2f,
                    position.z - 0.3f,
                    position.z + 0.3f
                ));
                Debug.Log($"[炸弹范围] 长条形 @ ({position.x}, {position.z}): " +
                         $"X[{position.x - 2f}, {position.x + 3f}] Y[{position.z - 1f}, {position.z + 1f}]");
                break;

            case "横条形":
                ranges.Add(new ExplosionRange(
                    position.x - 0.3f,
                    position.x + 0.3f,
                    position.z - 2f,
                    position.z + 2f
                ));
                Debug.Log($"[炸弹范围] 横条形 @ ({position.x}, {position.z}): " +
                         $"X[{position.x - 1f}, {position.x + 1f}] Y[{position.z - 3f}, {position.z + 3f}]");
                break;

            case "正方体":
                ranges.Add(new ExplosionRange(
                    position.x - 1.2f,
                    position.x + 1.2f,
                    position.z - 1.2f,
                    position.z + 1.2f
                ));
                Debug.Log($"[炸弹范围] 正方体 @ ({position.x}, {position.z}): " +
                         $"X[{position.x - 1f}, {position.x + 1f}] Y[{position.z - 1f}, {position.z + 1f}]");
                break;

            case "十字形":
                // 水平条：X轴 ±4，Y轴 ±2
                ranges.Add(new ExplosionRange(
                    position.x - 1.5f,
                    position.x + 1.5f,
                    position.z - 0.2f,
                    position.z + 0.2f
                ));

                // 竖直条：X轴 ±2，Y轴 ±4
                ranges.Add(new ExplosionRange(
                    position.x - 0.2f,
                    position.x + 0.2f,
                    position.z - 1.5f,
                    position.z + 1.5f
                ));

                Debug.Log($"[炸弹范围] 十字形 @ ({position.x}, {position.z}): " +
                         $"水平 X[{position.x - 2f}, {position.x + 2f}] Y[{position.z - 1f}, {position.z + 1f}] + " +
                         $"竖直 X[{position.x - 1f}, {position.x + 1f}] Y[{position.z - 2f}, {position.z + 2f}]");
                break;

            default:
                Debug.LogWarning($"⚠ 未知炸弹类型: {bombType}");
                ranges.Add(new ExplosionRange(
                    position.x - 1f,
                    position.x + 1f,
                    position.z - 1f,
                    position.z + 1f
                ));
                break;
        }

        return ranges;
    }

    /// <summary>
    /// 判断某个位置是否在爆炸范围内
    /// </summary>
    public static bool IsInExplosionRange(Vector3 checkPosition, List<ExplosionRange> ranges)
    {
        foreach (ExplosionRange range in ranges)
        {
            if (checkPosition.x >= range.xMin && checkPosition.x <= range.xMax &&
                checkPosition.z >= range.zMin && checkPosition.z <= range.zMax)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// 获取范围内的所有玩家
    /// </summary>
    public static List<string> GetPlayersInRange(
        List<ExplosionRange> ranges,
        Dictionary<string, Vector3> playerPositions)
    {
        List<string> affectedPlayers = new List<string>();

        foreach (var kvp in playerPositions)
        {
            string playerId = kvp.Key;
            Vector3 playerPos = kvp.Value;

            if (IsInExplosionRange(playerPos, ranges))
            {
                affectedPlayers.Add(playerId);
                Debug.Log($"  → 玩家 {playerId} 在爆炸范围内 @ ({playerPos.x}, {playerPos.y})");
            }
        }

        return affectedPlayers;
    }

    /// <summary>
    /// 判断两组爆炸范围是否存在重叠
    /// </summary>
    public static bool HasOverlap(List<ExplosionRange> rangesA, List<ExplosionRange> rangesB)
    {
        foreach (var a in rangesA)
        {
            foreach (var b in rangesB)
            {
                // 矩形AABB重叠检测
                bool overlapX = a.xMin <= b.xMax && a.xMax >= b.xMin;
                bool overlapZ = a.zMin <= b.zMax && a.zMax >= b.zMin;
                if (overlapX && overlapZ)
                    return true;
            }
        }
        return false;
    }

    /// <summary>
    /// 合并两组爆炸范围（取并集，返回包含所有矩形的列表）
    /// </summary>
    public static List<ExplosionRange> MergeRanges(List<ExplosionRange> rangesA, List<ExplosionRange> rangesB)
    {
        var merged = new List<ExplosionRange>(rangesA);
        foreach (var r in rangesB)
        {
            // 避免重复添加完全相同的范围
            bool duplicate = merged.Exists(m =>
                m.xMin == r.xMin && m.xMax == r.xMax &&
                m.zMin == r.zMin && m.zMax == r.zMax);
            if (!duplicate)
                merged.Add(r);
        }
        return merged;
    }


    /// <summary>
    /// 根据炸弹等级获取伤害值
    /// </summary>
    public static int GetDamageByBombLevel(string bombLevel)
    {
        return bombLevel switch
        {
            "一级" => 1,
            "二级" => 2,
            "三级" => 3,
            _ => 1
        };
    }
}
