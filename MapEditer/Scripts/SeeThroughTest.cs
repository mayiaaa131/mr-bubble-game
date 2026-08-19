using System.Collections;
using UnityEngine;
using Unity.XR.PXR;
using UnityEngine.Android;

public class SeeThroughTest : MonoBehaviour
{
    private static readonly string[] PicoPermissions = new[]
    {
        "com.picovr.permission.SPATIAL_DATA",
        "com.picovr.permission.SCENE_UNDERSTANDING",
        "com.picovr.permission.SPATIAL_ANCHOR",
        "com.picovr.permission.EYE_TRACKING"
    };

    void Start()
    {
        StartCoroutine(RequestPermissionsAndInit());
    }

    IEnumerator RequestPermissionsAndInit()
    {
        // ── Step 1：Camera权限 ──────────────────────────
        if (!Permission.HasUserAuthorizedPermission(Permission.Camera))
        {
            var camCallbacks = new PermissionCallbacks();
            camCallbacks.PermissionGranted += p => Debug.Log($"[权限]  {p}");
            camCallbacks.PermissionDenied += p => Debug.LogWarning($"[权限]  {p}");
            Permission.RequestUserPermission(Permission.Camera, camCallbacks);
            yield return new WaitForSeconds(2.5f);
        }

        // ── Step 2：PICO专属权限（逐个弹窗）───────────
        foreach (var perm in PicoPermissions)
        {
            if (Permission.HasUserAuthorizedPermission(perm))
            {
                Debug.Log($"[权限] 已有：{perm} ");
                continue;
            }

            bool done = false;

            var callbacks = new PermissionCallbacks();
            callbacks.PermissionGranted += p =>
            {
                Debug.Log($"[权限]  已授权：{p}");
                done = true;
            };
            callbacks.PermissionDenied += p =>
            {
                Debug.LogWarning($"[权限]  被拒绝：{p}");
                done = true;
            };
            callbacks.PermissionDeniedAndDontAskAgain += p =>
            {
                Debug.LogWarning($"[权限]  永久拒绝（需手动设置）：{p}");
                done = true;
            };

            // PICO官方推荐写法，带callbacks才会弹窗
            Permission.RequestUserPermission(perm, callbacks);

            // 等用户点弹窗，超时10秒继续下一个
            float t = 0f;
            while (!done && t < 10f)
            {
                t += Time.deltaTime;
                yield return null;
            }
        }

        // ── Step 3：初始化透视 ──────────────────────────
        yield return new WaitForSeconds(0.3f);
        PXR_Manager.EnableVideoSeeThrough = true;
        Debug.Log("[SeeThroughTest] See Through 已启用 ");
    }

    void OnDestroy()
    {
        PXR_Manager.EnableVideoSeeThrough = false;
    }
}
