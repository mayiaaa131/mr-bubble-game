using UnityEngine;

public class FloatingObject : MonoBehaviour
{
    [Header("浮动参数")]
    [Tooltip("浮动幅度（上下各多少单位）")]
    public float amplitude = 0.3f;

    [Tooltip("浮动频率（越小越缓慢）")]
    public float frequency = 0.8f;

    [Tooltip("相位偏移（让多个物体不同步）")]
    public float phaseOffset = 0f;

    [Header("缓动参数")]
    [Tooltip("启用呼吸感（频率轻微随时间变化）")]
    public bool breathingEffect = true;

    [Tooltip("呼吸变化强度")]
    [Range(0f, 0.5f)]
    public float breathingIntensity = 0.15f;

    [Header("旋转参数")]
    [Tooltip("旋转幅度（度）")]
    public float rotationAmplitude = 8f;

    [Tooltip("旋转频率")]
    public float rotationFrequency = 0.5f;

    [Tooltip("是否允许三轴随机旋转（关闭则只摇摆Z轴）")]
    public bool fullAxisRotation = true;

    private Vector3 startPosition;
    private Quaternion startRotation;
    private float timeOffset;

    // 每个轴独立的随机频率和相位，产生不规则感
    private Vector3 rotFrequencyMult;
    private Vector3 rotPhaseOffset;

    void Start()
    {
        startPosition = transform.position;
        startRotation = transform.rotation;

        timeOffset = Random.Range(0f, Mathf.PI * 2f) + phaseOffset;

        // 为每个轴生成略有差异的频率倍数，避免三轴同步
        rotFrequencyMult = new Vector3(
            Random.Range(0.8f, 1.2f),
            Random.Range(0.6f, 1.0f),
            Random.Range(0.9f, 1.3f)
        );

        // 每个轴随机相位
        rotPhaseOffset = new Vector3(
            Random.Range(0f, Mathf.PI * 2f),
            Random.Range(0f, Mathf.PI * 2f),
            Random.Range(0f, Mathf.PI * 2f)
        );
    }

    void Update()
    {
        float t = Time.time + timeOffset;

        // ── 浮动 ──────────────────────────────────────
        float currentFrequency = frequency;
        if (breathingEffect)
        {
            currentFrequency += Mathf.Sin(t * 0.3f) * frequency * breathingIntensity;
        }

        float wave1 = Mathf.Sin(t * currentFrequency);
        float wave2 = Mathf.Sin(t * currentFrequency * 1.3f) * 0.2f;
        float offsetY = (wave1 + wave2) * amplitude;

        transform.position = startPosition + new Vector3(0f, offsetY, 0f);

        // ── 旋转 ──────────────────────────────────────
        float rotX = 0f;
        float rotY = 0f;

        float rotZ = Mathf.Sin(t * rotationFrequency * rotFrequencyMult.z + rotPhaseOffset.z)
                   * rotationAmplitude;

        if (fullAxisRotation)
        {
            // X轴：轻微前后点头
            rotX = Mathf.Sin(t * rotationFrequency * rotFrequencyMult.x + rotPhaseOffset.x)
                 * rotationAmplitude * 0.5f;

            // Y轴：更慢的左右偏转，幅度最小，不抢镜
            rotY = Mathf.Sin(t * rotationFrequency * rotFrequencyMult.y + rotPhaseOffset.y)
                 * rotationAmplitude * 0.3f;
        }

        transform.rotation = startRotation * Quaternion.Euler(rotX, rotY, rotZ);
    }

    public void ResetBaseTransform()
    {
        startPosition = transform.position;
        startRotation = transform.rotation;
    }
}