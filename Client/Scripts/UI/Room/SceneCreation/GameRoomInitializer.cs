// ============================================
// 文件路径：Assets/scripts/Initializer/GameRoomInitializer.cs
// ============================================
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameRoomInitializer : MonoBehaviour
{
    private void Start( )
    {
        Debug.Log("[GameRoomInitializer] >>> 进入Game场景");

        // ★ 从 ServerRoomManager 获取房间实例
        RoomInstance room = ServerRoomManager.Instance?.GetCurrentRoom();

        if (room == null)
        {
            Debug.LogError("[GameRoomInitializer] ❌ 无法获取房间实例");
            return;
        }

        string roomId = room.roomData.roomId;
        string expectedSceneName = $"Game_{roomId}";
        string currentSceneName = gameObject.scene.name;

        Debug.Log($"[GameRoomInitializer] ✓ 获取房间: {roomId}");
        Debug.Log($"[GameRoomInitializer] 当前场景: {currentSceneName}");
        Debug.Log($"[GameRoomInitializer] 期望场景: {expectedSceneName}");

        // ★ 验证场景名称是否匹配
        if (currentSceneName != expectedSceneName)
        {
            Debug.LogWarning($"[GameRoomInitializer] ⚠ 场景名称不匹配");
        }

        // 加载地图
        room.mapLoader.LoadMap();
        Debug.Log($"[GameRoomInitializer] ✓ 地图已加载: {room.roomData.mapId}");

        // 启动游戏
        room.StartGame();
        Debug.Log($"[GameRoomInitializer] ✅ 游戏已启动");
    }

}
