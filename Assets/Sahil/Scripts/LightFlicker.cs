using UnityEngine;

[RequireComponent(typeof(Light))]
public class LightFlicker : MonoBehaviour
{
    [Header("Intensity")]
    [Min(0f)] public float minIntensity = 0.7f;
    [Min(0f)] public float maxIntensity = 1.2f;
    [Tooltip("When enabled, min/max are treated as multipliers of the light's starting intensity. This preserves your authored brightness.")]
    public bool scaleRangeByInitialIntensity = true;

    [Header("Flicker")]
    [Min(0f)] public float flickerSpeed = 2.5f;
    [Range(0f, 1f)] public float flickerAmount = 0.35f;
    public bool smoothFlicker = true;

    [Header("Variation")]
    [Tooltip("0 means auto-random per instance. Any other value gives deterministic variation.")]
    public int randomSeed = 0;

    [Header("Spikes")]
    public bool allowSharpSpikes = true;
    [Range(0f, 1f)] public float spikeChance = 0.06f;

    [Header("On/Off Flicker")]
    public bool allowOnOffFlicker = true;
    [Range(0f, 1f)] public float offChance = 0.03f;
    [Min(0.01f)] public float offDurationMin = 0.03f;
    [Min(0.01f)] public float offDurationMax = 0.12f;

    private Light cachedLight;
    private float noiseOffset;
    private float chaoticTarget;
    private float chaoticTimer;
    private float spikeMultiplier = 1f;
    private float spikeTimer;
    private float initialIntensity;
    private float offTimer;

    private void Awake()
    {
        cachedLight = GetComponent<Light>();
        if (cachedLight == null)
        {
            enabled = false;
            return;
        }

        // This component is intended for spotlights only.
        if (cachedLight.type != LightType.Spot)
            cachedLight.type = LightType.Spot;

        initialIntensity = Mathf.Max(0.01f, cachedLight.intensity);

        int seed = randomSeed == 0 ? (GetInstanceID() * 397) ^ System.Environment.TickCount : randomSeed;
        Random.InitState(seed);
        noiseOffset = Random.Range(0f, 1000f);
        chaoticTarget = Midpoint;
        Random.InitState(System.Environment.TickCount);
    }

    private void OnValidate()
    {
        minIntensity = Mathf.Max(0f, minIntensity);
        maxIntensity = Mathf.Max(minIntensity + 0.01f, maxIntensity);
        flickerSpeed = Mathf.Max(0f, flickerSpeed);
        spikeChance = Mathf.Clamp01(spikeChance);
        offChance = Mathf.Clamp01(offChance);
        offDurationMin = Mathf.Max(0.01f, offDurationMin);
        offDurationMax = Mathf.Max(offDurationMin, offDurationMax);

        if (cachedLight != null && cachedLight.type != LightType.Spot)
            cachedLight.type = LightType.Spot;
    }

    private void Update()
    {
        if (cachedLight == null)
            return;

        if (allowOnOffFlicker)
            UpdateOffState();

        if (offTimer > 0f)
        {
            cachedLight.intensity = 0f;
            return;
        }

        float baseTarget = smoothFlicker ? ComputeSmoothFlicker() : ComputeChaoticFlicker();

        if (allowSharpSpikes)
            UpdateSpikeState();
        else
            spikeMultiplier = 1f;

        float target = baseTarget * spikeMultiplier;
        cachedLight.intensity = Mathf.Clamp(target, EffectiveMin, EffectiveMax);
    }

    private float ComputeSmoothFlicker()
    {
        float sample = Mathf.PerlinNoise(noiseOffset, Time.time * Mathf.Max(0.05f, flickerSpeed));
        float fullRange = Mathf.Lerp(EffectiveMin, EffectiveMax, sample);
        return Mathf.Lerp(Midpoint, fullRange, flickerAmount);
    }

    private float ComputeChaoticFlicker()
    {
        chaoticTimer -= Time.deltaTime;
        if (chaoticTimer <= 0f)
        {
            float speed01 = Mathf.Clamp01(flickerSpeed / 10f);
            chaoticTimer = Mathf.Lerp(0.14f, 0.03f, speed01);
            chaoticTarget = Random.Range(EffectiveMin, EffectiveMax);
        }

        float chase = Mathf.Lerp(8f, 24f, Mathf.Clamp01(flickerSpeed / 10f));
        float stepped = Mathf.Lerp(cachedLight.intensity, chaoticTarget, Time.deltaTime * chase);
        return Mathf.Lerp(Midpoint, stepped, flickerAmount);
    }

    private void UpdateSpikeState()
    {
        if (spikeTimer > 0f)
        {
            spikeTimer -= Time.deltaTime;
            if (spikeTimer <= 0f)
                spikeMultiplier = 1f;
            return;
        }

        float chancePerFrame = spikeChance * Time.deltaTime * Mathf.Lerp(0.5f, 1.5f, Mathf.Clamp01(flickerSpeed / 10f));
        if (Random.value >= chancePerFrame)
            return;

        bool dip = Random.value < 0.6f;
        spikeMultiplier = dip ? Random.Range(0.72f, 0.9f) : Random.Range(1.08f, 1.22f);
        spikeTimer = Random.Range(0.03f, 0.1f);
    }

    private void UpdateOffState()
    {
        if (offTimer > 0f)
        {
            offTimer -= Time.deltaTime;
            return;
        }

        float chancePerFrame = offChance * Time.deltaTime * Mathf.Lerp(0.5f, 1.5f, Mathf.Clamp01(flickerSpeed / 10f));
        if (Random.value < chancePerFrame)
            offTimer = Random.Range(offDurationMin, offDurationMax);
    }

    private float EffectiveMin => scaleRangeByInitialIntensity ? minIntensity * initialIntensity : minIntensity;
    private float EffectiveMax => scaleRangeByInitialIntensity
        ? Mathf.Max(EffectiveMin + 0.01f, maxIntensity * initialIntensity)
        : Mathf.Max(minIntensity + 0.01f, maxIntensity);
    private float Midpoint => (EffectiveMin + EffectiveMax) * 0.5f;
}
