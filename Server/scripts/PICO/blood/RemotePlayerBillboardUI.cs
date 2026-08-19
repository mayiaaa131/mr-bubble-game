using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RemotePlayerBillboardUI : MonoBehaviour
{
    private Camera _mainCamera;
    private Transform _uiTransform;

    // 水平翻转选项  
    [SerializeField] private bool flipHorizontal = true;

    void Start()
    {
        _mainCamera = Camera.main;
        _uiTransform = transform;

        if (_mainCamera == null)
        {
            Debug.LogError("[BillboardUI] 未找到主摄像机！");
            enabled = false;
        }
    }

    void LateUpdate()
    {
        if (_mainCamera == null) return;

        // 计算从UI到摄像机的方向  
        Vector3 directionToCamera = _mainCamera.transform.position - _uiTransform.position;

        // 让UI面向摄像机  
        _uiTransform.rotation = Quaternion.LookRotation(directionToCamera, Vector3.up);


        // 应用水平翻转  
        if (flipHorizontal)
        {
            Vector3 scale = _uiTransform.localScale;
            scale.x = -Mathf.Abs(scale.x);  // 强制X轴为负，实现水平翻转  
            _uiTransform.localScale = scale;
        }
    }
}
