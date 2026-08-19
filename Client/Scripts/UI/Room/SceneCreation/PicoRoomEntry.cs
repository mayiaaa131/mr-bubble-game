using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;

public class PicoRoomEntry : MonoBehaviour
{
    [Header("进入游戏按钮")]
    [SerializeField] private Button enterGameButton;

    private Room currentRoom = null;

    private void Start()
    {
        BindAllButtons();
    }

    private void OnDestroy()
    {
        if (enterGameButton != null)
            enterGameButton.onClick.RemoveListener(OnClickEnterGame);
    }

    /// <summary>
    /// 绑定进入游戏按钮
    /// </summary>
    private void BindAllButtons()
    {
        Debug.Log("[PicoRoomEntry] 开始绑定按钮");

        if (enterGameButton != null)
        {
            enterGameButton.onClick.AddListener(OnClickEnterGame);
            Debug.Log("[PicoRoomEntry] ✓ 进入游戏按钮已绑定");
        }
        else
        {
            Debug.LogError("[PicoRoomEntry] ❌ enterGameButton 未配置");
        }
    }

    /// <summary>
    /// 点击进入游戏按钮
    /// </summary>
    private void OnClickEnterGame()
    {
        Debug.Log("[PicoRoomEntry] >>> 进入游戏按钮被点击");

        currentRoom = RoomDetailManager.Instance?.GetCurrentRoom();
        if (currentRoom == null)
        {
            Debug.LogError("[PicoRoomEntry] ❌ 无法获取当前房间");
            return;
        }

        if (string.IsNullOrEmpty(currentRoom.mapId) || currentRoom.mapId == "map_001")
        {
            Debug.LogWarning("[PicoRoomEntry] ⚠ 地图未选择或地图ID为默认值");
            return;
        }

        // ★ 步骤1：同步玩家数据
        SyncPlayersData();

        // ★ 步骤2：提取房间号
        int roomIndex = int.Parse(currentRoom.roomId.Replace("room_", ""));

        // ★ 步骤3：启动房间（调用静态方法）
        RoomGameManager.StartRoom(currentRoom.roomId, roomIndex);

        // ★ 步骤4：禁用大厅UI
        DisableSampleSceneComponents();

        // ★ 步骤5：加载游戏场景
        LoadGameScene(currentRoom.roomId);
    }

    /// <summary>
    /// 同步玩家数据到 Team JSON
    /// </summary>
    private void SyncPlayersData()
    {
        try
        {
            Debug.Log("[PicoRoomEntry] → 同步玩家数据到 Team JSON");

            RoomDetailUIController controller = FindFirstObjectByType<RoomDetailUIController>();
            if (controller == null)
            {
                Debug.LogWarning("[PicoRoomEntry] ⚠ 未找到 RoomDetailUIController");
                return;
            }

            var redTeamPanelField = typeof(RoomDetailUIController).GetField(
                "redTeamPanel",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            var blueTeamPanelField = typeof(RoomDetailUIController).GetField(
                "blueTeamPanel",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (redTeamPanelField != null && blueTeamPanelField != null)
            {
                Transform redTeamPanel = (Transform)redTeamPanelField.GetValue(controller);
                Transform blueTeamPanel = (Transform)blueTeamPanelField.GetValue(controller);

                if (redTeamPanel != null && blueTeamPanel != null)
                {
                    RoomDetailManager.Instance.SyncPlayersToTeamJson(redTeamPanel, blueTeamPanel);
                    Debug.Log("[PicoRoomEntry] ✓ 玩家数据已同步");
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[PicoRoomEntry] ❌ 同步失败: {e.Message}");
        }
    }

    /// <summary>
    /// 禁用大厅 UI
    /// </summary>
    private void DisableSampleSceneComponents()
    {
        Scene sampleScene = SceneManager.GetSceneByName("SampleScene");

        if (!sampleScene.isLoaded)
        {
            Debug.LogWarning("[PicoRoomEntry] ⚠️ SampleScene 未加载");
            return;
        }

        GameObject[] rootObjects = sampleScene.GetRootGameObjects();

        foreach (GameObject root in rootObjects)
        {
            Canvas canvas = root.GetComponent<Canvas>();
            if (canvas == null)
                canvas = root.GetComponentInChildren<Canvas>();

            if (canvas != null)
            {
                canvas.enabled = false;
                canvas.gameObject.SetActive(false);
                Debug.Log($"[PicoRoomEntry] ✓ Canvas 已禁用");
            }

            Camera camera = root.GetComponent<Camera>();
            if (camera == null)
                camera = root.GetComponentInChildren<Camera>();

            if (camera != null)
            {
                camera.enabled = false;
                camera.gameObject.SetActive(false);
                Debug.Log($"[PicoRoomEntry] ✓ Camera 已禁用");
            }
        }
    }

    /// <summary>
    /// 加载游戏场景
    /// </summary>
    private void LoadGameScene(string roomId)
    {
        string gameSceneName = $"Game_{roomId}";
        StartCoroutine(LoadSceneAsync(gameSceneName));
    }

    private IEnumerator LoadSceneAsync(string sceneName)
    {
        Debug.Log($"[PicoRoomEntry] → 异步加载场景: {sceneName}");

        AsyncOperation loadOp = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
        yield return new WaitUntil(() => loadOp.isDone);

        Debug.Log($"[PicoRoomEntry] ✓ {sceneName} 加载完成");

        Scene gameScene = SceneManager.GetSceneByName(sceneName);
        if (gameScene.isLoaded)
        {
            SceneManager.SetActiveScene(gameScene);
            Debug.Log($"[PicoRoomEntry] ✅ {sceneName} 已激活");
        }
    }
}
