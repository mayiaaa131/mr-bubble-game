using UnityEngine;

/// <summary>
/// 挂在每个可选中的预制体根节点上（或自动添加）
/// 选中时显示轮廓高亮（通过放大一个半透明Shell实现，简单可靠）
/// 
/// 如果你的项目有Post Processing Outline，可以替换这个实现
/// </summary>
public class ObjectHighlight : MonoBehaviour
{
    [Header("高亮配置")]
    public Color highlightColor = new Color(0f, 1f, 1f, 0.3f);
    public float highlightScale = 1.05f;

    private GameObject _shell;
    private bool _isHighlighted = false;

    void Awake()
    {
        CreateShell();
    }

    private void CreateShell()
    {
        // 复制所有子Renderer，创建放大的半透明Shell
        _shell = new GameObject("_HighlightShell");
        _shell.transform.SetParent(transform, false);
        _shell.transform.localScale = Vector3.one * highlightScale;

        foreach (var srcRenderer in GetComponentsInChildren<MeshRenderer>())
        {
            if (srcRenderer.gameObject == _shell) continue;

            // 复制MeshFilter
            MeshFilter srcMF = srcRenderer.GetComponent<MeshFilter>();
            if (srcMF == null) continue;

            GameObject shellChild = new GameObject(srcRenderer.name + "_shell");
            shellChild.transform.SetParent(_shell.transform, false);
            shellChild.transform.localPosition = srcRenderer.transform.localPosition;
            shellChild.transform.localRotation = srcRenderer.transform.localRotation;
            shellChild.transform.localScale = srcRenderer.transform.localScale;

            MeshFilter mf = shellChild.AddComponent<MeshFilter>();
            mf.sharedMesh = srcMF.sharedMesh;

            MeshRenderer mr = shellChild.AddComponent<MeshRenderer>();
            Material mat = new Material(Shader.Find("Standard"));
            mat.color = highlightColor;
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.renderQueue = 3000;
            mr.material = mat;
        }

        _shell.SetActive(false);
    }

    public void SetHighlight(bool on)
    {
        if (_isHighlighted == on) return;
        _isHighlighted = on;
        if (_shell != null) _shell.SetActive(on);
    }

    public bool IsHighlighted => _isHighlighted;
}