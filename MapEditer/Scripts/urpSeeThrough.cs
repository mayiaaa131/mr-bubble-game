using System.Collections;
using UnityEngine;
using Unity.XR.PXR;
using UnityEngine.Android;
using UnityEngine.Rendering.Universal;  // 必须加这个

public class urpSeeThrough : MonoBehaviour
{
    private static readonly string[] PicoPermissions = new[]
    {
        "com.picovr.permission.SPATIAL_DATA",
        "com.picovr.permission.SCENE_UNDERSTANDING",
        "com.picovr.permission.SPATIAL_ANCHOR",
        "com.picovr.permission.EYE_TRACKING"
    };

    private Camera mainCamera;

    void Start()
    {
        mainCamera = Camera.main;
        StartCoroutine(RequestPermissionsAndInit());
    }

    IEnumerator RequestPermissionsAndInit()
    {
        // Step 1: Camera权限
        if (!Permission.HasUserAuthorizedPermission(Permission.Camera))
        {
            var camCallbacks = new PermissionCallbacks();
            camCallbacks.PermissionGranted += p => Debug.Log($"[权限] {p}");
            camCallbacks.PermissionDenied += p => Debug.LogWarning($"[权限] {p}");
            Permission.RequestUserPermission(Permission.Camera, camCallbacks);
            yield return new WaitForSeconds(2.5f);
        }

        // Step 2: PICO专有权限
        foreach (var perm in PicoPermissions)
        {
            if (Permission.HasUserAuthorizedPermission(perm))
            {
                Debug.Log($"[权限] 已有: {perm}");
                continue;
            }

            bool done = false;

            var callbacks = new PermissionCallbacks();
            callbacks.PermissionGranted += p =>
            {
                Debug.Log($"[权限] 获得权限: {p}");
                done = true;
            };
            callbacks.PermissionDenied += p =>
            {
                Debug.LogWarning($"[权限] 拒绝权限: {p}");
                done = true;
            };
            callbacks.PermissionDeniedAndDontAskAgain += p =>
            {
                Debug.LogWarning($"[权限] 拒绝权限且不再询问: {p}");
                done = true;
            };

            Permission.RequestUserPermission(perm, callbacks);

            float t = 0f;
            while (!done && t < 10f)
            {
                t += Time.deltaTime;
                yield return null;
            }
        }

        //  Step 3: URP特殊配置（关键！）
        yield return new WaitForSeconds(0.3f);
        ConfigureURPForSeeThrough();

        // Step 4: 启用透视
        PXR_Manager.EnableVideoSeeThrough = true;
        Debug.Log("[SeeThroughTest] See Through 已启用（URP模式）");
    }

    /// <summary>
    ///  关键方法：为URP配置透视
    /// </summary>
    private void ConfigureURPForSeeThrough()
    {
        if (mainCamera == null)
        {
            Debug.LogError("[URP Config] 主摄像机未找到!");
            return;
        }

        // 1️⃣ 配置主摄像机（Main Camera）
        mainCamera.clearFlags = CameraClearFlags.SolidColor;
        mainCamera.backgroundColor = new Color(0, 0, 0, 0); // 透明黑
        mainCamera.depth = 0;

        // 2️⃣ 获取URP相机组件并禁用干扰特性
        var urpCamera = mainCamera.GetComponent<UniversalAdditionalCameraData>();
        if (urpCamera != null)
        {
            //  关闭后处理，避免与透视冲突
            urpCamera.renderPostProcessing = false;

            //  设为Base Camera，不要设为Overlay
            urpCamera.renderType = CameraRenderType.Base;

            //  禁用MSAA（多重采样抗锯齿）可能会干扰
            urpCamera.antialiasing = AntialiasingMode.None;

            Debug.Log("[URP Config] Main Camera 已配置");
        }
        else
        {
            Debug.LogWarning("[URP Config] 未找到 UniversalAdditionalCameraData 组件，尝试添加...");
            // 如果没有，自动添加
            urpCamera = mainCamera.gameObject.AddComponent<UniversalAdditionalCameraData>();
            // 递归调用进行配置
            ConfigureURPForSeeThrough();
        }

        // 3️⃣ 关闭其他可能的Overlay Camera
        DisableOverlayCameras();
    }

    /// <summary>
    /// 禁用所有Overlay Camera（防止覆盖）
    /// </summary>
    private void DisableOverlayCameras()
    {
        var allCameras = FindObjectsOfType<Camera>();
        foreach (var cam in allCameras)
        {
            if (cam == mainCamera) continue;

            var urpCam = cam.GetComponent<UniversalAdditionalCameraData>();
            if (urpCam != null && urpCam.renderType == CameraRenderType.Overlay)
            {
                cam.enabled = false;
                Debug.Log($"[URP Config] 禁用Overlay Camera: {cam.gameObject.name}");
            }
        }
    }

    void OnDestroy()
    {
        PXR_Manager.EnableVideoSeeThrough = false;
    }
}