using UnityEngine;

public class MRTransparencyFix_Final : MonoBehaviour
{
    private LineRenderer rayLine;

    void Start()
    {
        rayLine = GetComponent<LineRenderer>();

        // 不动Camera，只配置LineRenderer
        rayLine.material = new Material(Shader.Find("Custom/RayLine_TransparencyProof"));

        // 如果没有自定义Shader，用这个备选
        if (rayLine.material.shader.name.Contains("Hidden"))
        {
            rayLine.material = new Material(Shader.Find("Unlit/Color"));
            rayLine.material.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);
        }

        rayLine.sortingOrder = 1000;
        rayLine.startColor = Color.red;
        rayLine.endColor = Color.red;
    }
}