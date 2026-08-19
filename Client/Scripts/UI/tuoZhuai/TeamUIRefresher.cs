using UnityEngine;
using TMPro;

public static class TeamUIRefresher
{
    /// <summary>
    /// 重新排列指定队伍容器内所有玩家的显示名称和内部数据
    /// </summary>
    /// <param name="container">队伍容器 Transform</param>
    /// <param name="teamId">"red" 或 "blue"</param>
    public static void RefreshTeamPlayerNames(Transform container, string teamId)
    {
        if (container == null) return;

        string prefix = (teamId == "red") ? "红队玩家" : "蓝队玩家";
        int index = 1;

        for (int i = 0; i < container.childCount; i++)
        {
            Transform child = container.GetChild(i);
            DraggablePlayerButton btn = child.GetComponent<DraggablePlayerButton>();
            if (btn == null) continue;

            string newName = $"{prefix}{index}";

            // ★ 更新内部数据
            btn.playerName = newName;
            child.gameObject.name = $"{newName}_{teamId}";

            // ★ 更新 UI 文字
            TextMeshProUGUI label = child.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null)
                label.text = newName;

            // ★ 同步到 TeamAssignManager 中的数据
            var playerInfo = TeamAssignManager.Instance.GetTeam(teamId)
                .Find(p => p.playerId == btn.playerId);
            if (playerInfo != null)
                playerInfo.playerName = newName;

            Debug.Log($"[RefreshTeamPlayerNames] {teamId} 第{index}位 → {newName}");
            index++;
        }
    }
}
