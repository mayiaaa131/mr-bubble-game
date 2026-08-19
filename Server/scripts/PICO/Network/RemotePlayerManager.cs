using BubbleBattle.Network;
using UnityEngine;
using System.Collections.Generic;

public class RemotePlayerManager : MonoBehaviour
{
    [SerializeField] private GameObject playerPrefab; // 玩家模型预制体
    [SerializeField] private Transform playersParent; // 放置远程玩家的父对象（可选）

    private Dictionary<string, GameObject> remotePlayersDict = new();
    private HashSet<string> currentPlayerIds = new();

    void Start()
    {
        // 订阅事件
        WebSocketClient.Instance.OnWorldStateReceived += HandleWorldState;
    }

    private void HandleWorldState(WorldStateMsg worldState)
    {
        currentPlayerIds.Clear();

        // 遍历所有队伍和玩家
        foreach (var team in worldState.teams)
        {
            foreach (var playerState in team.players)
            {
                // 跳过本地玩家
                if (playerState.playerId == WebSocketClient.Instance.PlayerId)
                    continue;

                currentPlayerIds.Add(playerState.playerId);

                // 显示或更新其他玩家
                UpdateRemotePlayer(playerState);
            }
        }

        // 移除不在列表中的玩家（玩家离开了）
        RemoveDisconnectedPlayers();
    }

    private void UpdateRemotePlayer(PlayerStateInfo playerState)
    {
        if (remotePlayersDict.ContainsKey(playerState.playerId))
        {
            // 更新现有玩家位置和旋转
            var playerObj = remotePlayersDict[playerState.playerId];
            playerObj.transform.position = new Vector3(
                playerState.position.x,
                playerState.position.y,
                playerState.position.z
            );
            playerObj.transform.rotation = new Quaternion(
                playerState.rotation.x,
                playerState.rotation.y,
                playerState.rotation.z,
                playerState.rotation.w
            );
        }
        else
        {
            // 创建新玩家对象
            CreateRemotePlayer(playerState);
        }
    }

    private void CreateRemotePlayer(PlayerStateInfo playerState)
    {
        if (playerPrefab == null)
        {
            Debug.LogError("[RemotePlayerManager] playerPrefab 未指定！");
            return;
        }

        // 实例化玩家对象
        GameObject newPlayerObj = Instantiate(
            playerPrefab,
            new Vector3(playerState.position.x, playerState.position.y, playerState.position.z),
            new Quaternion(playerState.rotation.x, playerState.rotation.y, playerState.rotation.z, playerState.rotation.w),
            playersParent
        );

        newPlayerObj.name = playerState.playerName;

        // 添加标签或组件以标识这是远程玩家
        newPlayerObj.tag = "RemotePlayer";

        // 如果有TextMesh，显示玩家名字
        var textMesh = newPlayerObj.GetComponentInChildren<TextMesh>();
        if (textMesh != null)
        {
            textMesh.text = playerState.playerName;
        }

        // 保存到字典
        remotePlayersDict[playerState.playerId] = newPlayerObj;

        Debug.Log($"[RemotePlayerManager] 创建远程玩家: {playerState.playerName} ({playerState.playerId})");
    }

    private void RemoveDisconnectedPlayers()
    {
        var playerIdsToRemove = new List<string>();

        // 找出需要移除的玩家
        foreach (var playerId in remotePlayersDict.Keys)
        {
            if (!currentPlayerIds.Contains(playerId))
            {
                playerIdsToRemove.Add(playerId);
            }
        }

        // 移除玩家对象
        foreach (var playerId in playerIdsToRemove)
        {
            if (remotePlayersDict.TryGetValue(playerId, out var playerObj))
            {
                Debug.Log($"[RemotePlayerManager] 移除远程玩家: {playerId}");
                Destroy(playerObj);
                remotePlayersDict.Remove(playerId);
            }
        }
    }

    void OnDestroy()
    {
        if (WebSocketClient.Instance != null)
        {
            WebSocketClient.Instance.OnWorldStateReceived -= HandleWorldState;
        }
    }
}
