using UnityEngine;
using UnityEngine.Rendering.Universal;

public class EnablePostProcessing : MonoBehaviour
{
    void Start()
    {
        // 获取当前摄像机
        Camera camera = GetComponent<Camera>();

        if (camera == null)
        {
            Debug.LogError("此脚本必须挂在有Camera组件的GameObject上！");
            return;
        }

        // 获取或创建 UniversalAdditionalCameraData
        var cameraData = camera.GetUniversalAdditionalCameraData();

        if (cameraData == null)
        {
            Debug.LogError("无法获取UniversalAdditionalCameraData！");
            return;
        }

        // ⭐ 启用后处理！
        cameraData.renderPostProcessing = true;

        Debug.Log("✅ 后处理已启用！");
    }
}