using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections;

public class ReturnToLobbyButton : MonoBehaviour
{
    [SerializeField] private Button returnButton;           // ★ 仅返回（不销毁）
    [SerializeField] private Button returnAndDestroyButton; // ★ 返回+销毁

    private void Start()
    {
        if (returnButton != null)
        {
            returnButton.onClick.AddListener(OnReturnToLobbyOnly);
            Debug.Log("[ReturnToLobbyButton] ✓ 返回按钮已绑定（不销毁服务器）");
        }
        else
        {
            Debug.LogError("[ReturnToLobbyButton] ❌ returnButton 未配置");
        }

        if (returnAndDestroyButton != null)
        {
            returnAndDestroyButton.onClick.AddListener(OnReturnToLobbyAndDestroy);
            Debug.Log("[ReturnToLobbyButton] ✓ 返回+销毁按钮已绑定");
        }
        else
        {
            Debug.LogWarning("[ReturnToLobbyButton] ⚠ returnAndDestroyButton 未配置（可选）");
        }
    }

    /// <summary>
    /// ★ 仅返回大厅，不销毁服务器
    /// </summary>
    private void OnReturnToLobbyOnly()
    {
        Debug.Log("[ReturnToLobbyButton] 🔄 仅返回大厅（保留服务器）");
        StartCoroutine(UnloadGameSceneAndReturnToLobby(destroyRoom: false));
    }

    /// <summary>
    /// ★ 返回大厅并销毁服务器
    /// </summary>
    private void OnReturnToLobbyAndDestroy()
    {
        Debug.Log("[ReturnToLobbyButton] 🔄 返回大厅并销毁服务器");

        // 获取当前房间ID
        Scene activeScene = SceneManager.GetActiveScene();
        string roomId = activeScene.name.Replace("Game_", "");

        // ★ 调用静态方法销毁房间
        RoomGameManager.ShutdownRoom(roomId);

        StartCoroutine(UnloadGameSceneAndReturnToLobby(destroyRoom: true));
    }

    /// <summary>
    /// 卸载游戏场景并返回大厅
    /// </summary>
    private IEnumerator UnloadGameSceneAndReturnToLobby(bool destroyRoom)
    {
        Debug.Log("[ReturnToLobbyButton] → 开始返回大厅流程...");

        // 获取当前活跃场景
        Scene activeScene = SceneManager.GetActiveScene();
        string gameSceneName = activeScene.name;

        // ★ 新增：清空当前场景中的地图物体，防止下次进入时叠加  
        RoomMapLoader mapLoader = FindFirstObjectByType<RoomMapLoader>();
        if (mapLoader != null)
        {
            mapLoader.ClearLoadedMap();
            Debug.Log("[ReturnToLobbyButton] ✓ 地图已清空");
        }
        else
        {
            Debug.LogWarning("[ReturnToLobbyButton] ⚠ 未找到 RoomMapLoader，跳过地图清空");
        }

        yield return new WaitForSeconds(0.2f);

        // 启用大厅 UI
        EnableSampleSceneComponents();

        yield return new WaitForSeconds(0.2f);

        // 卸载游戏场景
        if (!gameSceneName.Contains("SampleScene"))
        {
            yield return SceneManager.UnloadSceneAsync(gameSceneName);
            Debug.Log($"[ReturnToLobbyButton] ✓ {gameSceneName} 已卸载");
        }

        yield return new WaitForSeconds(0.2f);

        // 设置大厅为活跃场景
        Scene sampleScene = SceneManager.GetSceneByName("SampleScene");
        SceneManager.SetActiveScene(sampleScene);

        yield return null;

        Debug.Log("[ReturnToLobbyButton] ✅ 已成功返回大厅");
    }

    /// <summary>
    /// 启用大厅 UI
    /// </summary>
    private void EnableSampleSceneComponents()
    {
        Scene sampleScene = SceneManager.GetSceneByName("SampleScene");

        if (!sampleScene.isLoaded)
        {
            Debug.LogWarning("[ReturnToLobbyButton] ⚠ SampleScene 未加载");
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
                canvas.enabled = true;
                canvas.gameObject.SetActive(true);
                Debug.Log($"[ReturnToLobbyButton] ✓ Canvas 已启用");
            }

            Camera camera = root.GetComponent<Camera>();
            if (camera == null)
                camera = root.GetComponentInChildren<Camera>();

            if (camera != null)
            {
                camera.enabled = true;
                camera.gameObject.SetActive(true);
                Debug.Log($"[ReturnToLobbyButton] ✓ Camera 已启用");
            }
        }
    }

    private void OnDestroy()
    {
        if (returnButton != null)
            returnButton.onClick.RemoveListener(OnReturnToLobbyOnly);

        if (returnAndDestroyButton != null)
            returnAndDestroyButton.onClick.RemoveListener(OnReturnToLobbyAndDestroy);
    }
}
