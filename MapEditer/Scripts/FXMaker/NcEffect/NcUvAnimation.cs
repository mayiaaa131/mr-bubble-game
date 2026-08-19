//------------------------------------
//
// FXMaker
// Created by ismoon - 2012 - ismoontoo@gmail.com
//
// ------------------------------------

using UnityEngine;
using System.Collections;

[AddComponentMenu("FXMaker/NcEffect/NcUvAnimation")]
public class NcUvAnimation : MonoBehaviour
{
    // Attribute ---------------------------------------------------------------
    public float m_fScrollSpeedX = 1.0f;
    public float m_fScrollSpeedY = 0.0f;

    public float m_fTilingX = 1;
    public float m_fTilingY = 1;

    public float m_fOffsetX = 0;
    public float m_fOffsetY = 0;

    public bool m_bFixedTileSize = false;
    public bool m_bRepeat = true;
    public bool m_bAutoDestruct = false;

    protected Vector3 m_OriginalScale = new Vector3();
    protected Vector2 m_OriginalTiling = new Vector2();
    protected Vector2 m_EndOffset = new Vector2();
    protected Vector2 m_RepeatOffset = new Vector2();
    protected Renderer m_Renderer;

    // Property ----------------------------------------------------------------
    public void SetFixedTileSize(bool bFixedTileSize)
    {
        m_bFixedTileSize = bFixedTileSize;
    }

#if UNITY_EDITOR
    /// <summary>
    /// 编辑器中检查属性，确保没有重复的组件和有效的材质
    /// </summary>
    public string CheckProperty()
    {
        if (1 < gameObject.GetComponents(GetType()).Length)
            return "SCRIPT_WARNING_DUPLICATE";

        if (1 < GetEditingUvComponentCount())
            return "SCRIPT_DUPER_EDITING_UV";

        if (m_Renderer == null || m_Renderer.sharedMaterial == null)
            return "SCRIPT_EMPTY_MATERIAL";

        return "";	// no error
    }

    /// <summary>
    /// 获取编辑中的 UV 动画组件数量
    /// </summary>
    private int GetEditingUvComponentCount()
    {
        NcUvAnimation[] uvAnimations = gameObject.GetComponents<NcUvAnimation>();
        return uvAnimations.Length;
    }
#endif

    /// <summary>
    /// 获取动画播放状态
    /// </summary>
    public int GetAnimationState()
    {
        if (enabled == false || gameObject.activeSelf == false)
            return -1;
        return 1;
    }

    // Loop Function -----------------------------------------------------------
    void Start()
    {
        // ✅ 修改点 1：替换已弃用的 renderer 属性
        m_Renderer = GetComponent<Renderer>();

        if (m_Renderer == null || m_Renderer.sharedMaterial == null || m_Renderer.sharedMaterial.mainTexture == null)
        {
            enabled = false;
        }
        else
        {
            // ✅ 修改点 2：使用 m_Renderer 替代 renderer
            m_Renderer.material.mainTextureScale = new Vector2(m_fTilingX, m_fTilingY);

            // 计算 0~1 范围内的重复偏移值
            float offset;
            offset = m_fOffsetX + m_fTilingX;
            m_RepeatOffset.x = offset - (int)(offset);
            if (m_RepeatOffset.x < 0)
                m_RepeatOffset.x += 1;

            offset = m_fOffsetY + m_fTilingY;
            m_RepeatOffset.y = offset - (int)(offset);
            if (m_RepeatOffset.y < 0)
                m_RepeatOffset.y += 1;

            m_EndOffset.x = 1 - (m_fTilingX - (int)(m_fTilingX) + ((m_fTilingX - (int)(m_fTilingX)) < 0 ? 1 : 0));
            m_EndOffset.y = 1 - (m_fTilingY - (int)(m_fTilingY) + ((m_fTilingY - (int)(m_fTilingY)) < 0 ? 1 : 0));
        }
    }

    void Update()
    {
        if (m_Renderer == null || m_Renderer.sharedMaterial == null || m_Renderer.sharedMaterial.mainTexture == null)
            return;

        // 如果启用了固定贴图大小，根据物体缩放调整 Tiling
        if (m_bFixedTileSize)
        {
            if (m_fScrollSpeedX != 0 && m_OriginalScale.x != 0)
                m_fTilingX = m_OriginalTiling.x * (transform.lossyScale.x / m_OriginalScale.x);
            if (m_fScrollSpeedY != 0 && m_OriginalScale.y != 0)
                m_fTilingY = m_OriginalTiling.y * (transform.lossyScale.y / m_OriginalScale.y);

            // ✅ 修改点 3：使用 m_Renderer 替代 renderer
            m_Renderer.material.mainTextureScale = new Vector2(m_fTilingX, m_fTilingY);
        }

        // 更新纹理偏移
        m_fOffsetX += Time.deltaTime * m_fScrollSpeedX;
        m_fOffsetY += Time.deltaTime * m_fScrollSpeedY;

        // 如果不重复，检查是否超出范围
        if (m_bRepeat == false)
        {
            m_RepeatOffset.x += Time.deltaTime * m_fScrollSpeedX;
            if (m_RepeatOffset.x < 0 || 1 < m_RepeatOffset.x)
            {
                m_fOffsetX = m_EndOffset.x;
                if (m_bAutoDestruct)
                {
                    Destroy(gameObject);
                    return;
                }
                enabled = false;
            }

            m_RepeatOffset.y += Time.deltaTime * m_fScrollSpeedY;
            if (m_RepeatOffset.y < 0 || 1 < m_RepeatOffset.y)
            {
                m_fOffsetY = m_EndOffset.y;
                if (m_bAutoDestruct)
                {
                    Destroy(gameObject);
                    return;
                }
                enabled = false;
            }
        }

        // 应用纹理偏移到材质
        m_Renderer.material.mainTextureOffset = new Vector2(m_fOffsetX, m_fOffsetY);
    }

    // Control Function --------------------------------------------------------
    // Event Function ----------------------------------------------------------

    /// <summary>
    /// 更新动画速度
    /// </summary>
    public void OnUpdateEffectSpeed(float fSpeedRate, bool bRuntime)
    {
        m_fScrollSpeedX *= fSpeedRate;
        m_fScrollSpeedY *= fSpeedRate;
    }

    /// <summary>
    /// 更新工具数据
    /// </summary>
    public void OnUpdateToolData()
    {
        m_OriginalScale = transform.lossyScale;
        m_OriginalTiling.x = m_fTilingX;
        m_OriginalTiling.y = m_fTilingY;
    }
}