// ============================================
// 文件路径：Assets/scripts/Manager/SceneVisibilityManager.cs
// ============================================
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneVisibilityManager : MonoBehaviour
{
    public static SceneVisibilityManager Instance;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// 激活 SampleScene UI（启用 Canvas 和 Camera）
    /// </summary>
    public void ActivateLobbyScene()
    {
        Debug.Log("[SceneVisibilityManager] ★★★ 激活 SampleScene ★★★");

        // 启用 SampleScene
        SetSceneUIActive("SampleScene", true);

        

        Debug.Log("[SceneVisibilityManager] ✅ SampleScene 已激活\n");
    }

    /// <summary>
    /// 激活 GameRoom UI（启用 Canvas 和 Camera）
    /// </summary>
    public void ActivateGameScene()
    {
        Debug.Log("[SceneVisibilityManager] ★★★ 激活 GameRoom ★★★");

        // 启用 GameRoom
        SetSceneUIActive("GameRoom", true);

       

        Debug.Log("[SceneVisibilityManager] ✅ GameRoom 已激活\n");
    }

    /// <summary>
    /// ★ 核心方法：设置场景 UI 状态
    /// 关键：先查根物体，再查子物体！
    /// </summary>
    private void SetSceneUIActive(string sceneName, bool active)
    {
        Scene scene = SceneManager.GetSceneByName(sceneName);

        if (!scene.isLoaded)
        {
            Debug.LogWarning($"[SceneVisibilityManager] ⚠️ 场景未加载: {sceneName}");
            return;
        }

        Debug.Log($"[SceneVisibilityManager] → 设置 {sceneName} UI 为 {(active ? "启用" : "禁用")}");

        GameObject[] rootObjects = scene.GetRootGameObjects();
        bool foundCanvas = false;
        bool foundCamera = false;

        foreach (GameObject root in rootObjects)
        {
            // ★ Canvas - 先查根物体！
            if (!foundCanvas)
            {
                Canvas canvas = root.GetComponent<Canvas>();  // ← 先查根物体
                if (canvas == null)
                    canvas = root.GetComponentInChildren<Canvas>();  // 再查子物体

                if (canvas != null)
                {
                    canvas.enabled = active;
                    Debug.Log($"[SceneVisibilityManager] ✓ Canvas {(active ? "启用" : "禁用")}: {canvas.gameObject.name}");
                    foundCanvas = true;
                }
            }

            // ★ Camera - 先查根物体！
            if (!foundCamera)
            {
                Camera camera = root.GetComponent<Camera>();  // ← 先查根物体
                if (camera == null)
                    camera = root.GetComponentInChildren<Camera>();  // 再查子物体

                if (camera != null)
                {
                    camera.enabled = active;
                    Debug.Log($"[SceneVisibilityManager] ✓ Camera {(active ? "启用" : "禁用")}: {camera.gameObject.name}");
                    foundCamera = true;
                }
            }

            // 找到了就停止
            if (foundCanvas && foundCamera)
                break;
        }

        if (!foundCanvas)
            Debug.LogError($"[SceneVisibilityManager] ❌ 未找到 Canvas: {sceneName}");
        if (!foundCamera)
            Debug.LogError($"[SceneVisibilityManager] ❌ 未找到 Camera: {sceneName}");
    }
}
