//------------------------------------
//
// FXMaker
// Created by ismoon - 2012 - ismoontoo@gmail.com
//
// ------------------------------------

using UnityEngine;
using System.Collections;

[AddComponentMenu("FXMaker/NcEffect/NcRotation")]
public class NcRotation : MonoBehaviour  // ✅ 修改点 1：基类改为 MonoBehaviour
{
    public enum RotationMode
    {
        ZiZhuan,        // 自转（自身旋转）
        GongZhuan       // 公转（绕点旋转）
    }

    // Attribute ---------------------------------------------------------------
    public RotationMode m_RotaionMode = RotationMode.ZiZhuan;
    public bool m_bLoop = false;
    public bool m_bWorldSpace = false;
    public Vector3 m_vRotationValue = new Vector3(0, 360, 0);
    private float m_fStartTime = 0.0f;

    // Property ----------------------------------------------------------------
#if UNITY_EDITOR
    /// <summary>
    /// 编辑器中检查属性冲突
    /// </summary>
    public string CheckProperty()  // ✅ 修改点 2：移除 override 关键字
    {
        // ✅ 修改点 3：NcBillboard 类可能不存在，注释掉该检查
        // if (GetComponent<NcBillboard>() != null)
        //     return "SCRIPT_CLASH_ROTATEBILL";
        return "";  // no error
    }
#endif

    /// <summary>
    /// 获取动画播放状态
    /// </summary>
    public int GetAnimationState()  // ✅ 修改点 4：移除 override 关键字
    {
        if (!m_bLoop && Time.time - m_fStartTime > 1.0f)
        {
            return -1;
        }
        return 1;
    }

    // Loop Function -----------------------------------------------------------
    void Start()
    {
        m_fStartTime = Time.time;
    }

    void Update()
    {
        // 检查是否超时且不循环
        if (!m_bLoop && Time.time - m_fStartTime > 1.0f)
            return;

        switch (m_RotaionMode)
        {
            case RotationMode.ZiZhuan:
                // ✅ 自转模式：围绕自身轴旋转
                transform.Rotate(
                    Time.deltaTime * m_vRotationValue.x,
                    Time.deltaTime * m_vRotationValue.y,
                    Time.deltaTime * m_vRotationValue.z,
                    (m_bWorldSpace ? Space.World : Space.Self)
                );
                break;

            case RotationMode.GongZhuan:
                // ✅ 公转模式：绕某个点旋转（可选择本地空间或世界空间）
                Vector3 point = Vector3.zero;
                Vector3 x = Vector3.right;
                Vector3 y = Vector3.up;
                Vector3 z = Vector3.forward;

                // 如果使用本地空间且有父对象，则使用父对象坐标系
                if (!m_bWorldSpace && null != transform.parent)
                {
                    point = transform.parent.position;
                    x = transform.parent.TransformDirection(x);
                    y = transform.parent.TransformDirection(y);
                    z = transform.parent.TransformDirection(z);
                }

                // 执行绕各轴旋转
                transform.RotateAround(point, x, Time.deltaTime * m_vRotationValue.x);
                transform.RotateAround(point, y, Time.deltaTime * m_vRotationValue.y);
                transform.RotateAround(point, z, Time.deltaTime * m_vRotationValue.z);
                break;
        }
    }

    // Control Function --------------------------------------------------------
    // Event Function ----------------------------------------------------------

    /// <summary>
    /// 动态调整旋转速度
    /// </summary>
    public void OnUpdateEffectSpeed(float fSpeedRate, bool bRuntime)  // ✅ 修改点 5：移除 override 关键字
    {
        m_vRotationValue *= fSpeedRate;
    }
}