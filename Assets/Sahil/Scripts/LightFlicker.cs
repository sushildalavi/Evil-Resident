using UnityEngine;

[RequireComponent(typeof(Light))]
public class LightFlicker : MonoBehaviour
{
    [Header("Intensity")]
    [Min(0f)] public float minIntensity = 0.6f;
    [Min(0f)] public float maxIntensity = 1.2f;

    [Header("Flicker")]
    [Min(0f)] public float flickerSpeed = 3f;
    [Range(0f, 1f)] public float flickerAmount = 0.45f;
    public bool smoothFlicker = true;

    [Header("Variation")]
    [Tooltip("If true, this instance gets its own random offset.")]
    public bool randomSeed = true;

    private Light cachedLight;
    private float baseMin;
    private float baseMax;
    private float noiseOffset;
    private float sharpTarget;
    private float sharpTimer;

    private void Awake()
    {
        cachedLight = GetComponent<Light>();
        if (cachedLight == null)
        {
            enabled = false;
            return;
        }

        baseMin = Mathf.Max(0f, minIntensity);
        baseMax = Mathf.Max(baseMin + 0.01f, maxIntensity);

        noiseOffset = randomSeed ? Random.Range(0f, 1000f) : 0f;
        sharpTarget = (baseMin + baseMax) * 0.5f;
    }

    private void OnValidate()
    {
        minIntensity = Mathf.Max(0f, minIntensity);
        maxIntensity = Mathf.Max(minIntensity + 0.01f, maxIntensity);
        flickerSpeed = Mathf.Max(0f, flickerSpeed);
    }

    private void Update()
    {
        if (cachedLight == null)
            return;

        float target;
        if (smoothFlicker)
        {
            float sample = Mathf.PerlinNoise(noiseOffset, Time.time * flickerSpeed);
            target = Mathf.Lerp(baseMin, baseMax, sample);
        }
        else
        {
            sharpTimer -= Time.deltaTime;
            if (sharpTimer <= 0f)
            {
                float stepTime = Mathf.Lerp(0.02f, 0.22f, 1f / (1f + flickerSpeed));
                sharpTimer = stepTime;
                sharpTarget = Random.Range(baseMin, baseMax);
            }

            float chase = Mathf.Lerp(8f, 24f, Mathf.Clamp01(flickerSpeed / 10f));
            target = Mathf.Lerp(cachedLight.intensity, sharpTarget, Time.deltaTime * chase);
        }

        float center = (baseMin + baseMax) * 0.5f;
        target = Mathf.Lerp(center, target, Mathf.Clamp01(flickerAmount));
        cachedLight.intensity = Mathf.Clamp(target, baseMin, baseMax);
    }
}
