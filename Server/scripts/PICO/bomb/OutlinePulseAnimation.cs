using UnityEngine;

/// <summary>
/// 让爆炸范围线框做呼吸脉冲效果
/// </summary>
public class OutlinePulseAnimation : MonoBehaviour
{
    private LineRenderer lineRenderer;
    private Color baseColor;
    private Color brightColor;
    private float pulseSpeed;

    // 偏移让每个范围的动画不完全同步，看起来更自然
    private float timeOffset;

    public void Setup(LineRenderer lr, Color baseCol, Color brightCol, float speed)
    {
        lineRenderer = lr;
        baseColor = baseCol;
        brightColor = brightCol;
        pulseSpeed = speed;
        timeOffset = Random.Range(0f, Mathf.PI * 2f);  // 随机相位
    }

    void Update()
    {
        if (lineRenderer == null) return;

        float t = (Mathf.Sin(Time.time * pulseSpeed + timeOffset) + 1f) / 2f;  // 0~1

        Color currentColor = Color.Lerp(baseColor, brightColor, t);
        float currentAlpha = Mathf.Lerp(0.4f, 1f, t);
        float currentWidth = Mathf.Lerp(0.05f, 0.12f, t);  // 线宽也跟着脉冲

        lineRenderer.colorGradient = new Gradient()
        {
            colorKeys = new GradientColorKey[]
            {
                new GradientColorKey(currentColor, 0f),
                new GradientColorKey(currentColor, 1f),
            },
            alphaKeys = new GradientAlphaKey[]
            {
                new GradientAlphaKey(currentAlpha, 0f),
                new GradientAlphaKey(currentAlpha, 1f),
            }
        };

        lineRenderer.widthMultiplier = currentWidth;
    }
}