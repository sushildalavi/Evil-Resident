using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class PuzzleWheel : MonoBehaviour, IInteractable
{
    [Header("References")]
    [SerializeField] ColorWheelPuzzleManager puzzleManager;
    [SerializeField] Transform rotatingVisual;
    [SerializeField] AudioSource audioSource;
    [SerializeField] Renderer[] feedbackRenderers;

    [Header("Wheel States")]
    [Min(2)] [SerializeField] int stateCount = 4;
    [SerializeField] int startingState = 0;
    [SerializeField] bool wrapStates = true;
    [Tooltip("Not used by default input, but exposed for future alternate controls.")]
    [SerializeField] bool allowBackwardRotation;

    [Header("Rotation")]
    [SerializeField] Vector3 rotationAxisLocal = Vector3.forward;
    [SerializeField] bool autoDegreesFromStateCount = true;
    [SerializeField] float degreesPerStep = 90f;
    [Min(0.01f)] [SerializeField] float rotateDuration = 0.16f;
    [SerializeField] AnimationCurve rotateCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Interaction")]
    [SerializeField] KeyCode interactKey = KeyCode.E;
    [SerializeField] string rotatePrompt = "Press E to rotate dial";
    [SerializeField] string lockedPrompt = "The dial is locked";
    [SerializeField] AudioClip rotateSfx;
    [SerializeField] AudioClip blockedSfx;

    [Header("Subtle Feedback")]
    [SerializeField] bool pulseOnRotate = true;
    [SerializeField] Color pulseColor = new Color(0.35f, 0.41f, 0.36f, 1f);
    [SerializeField] bool useMaterialColorAsPulse = true;
    [Min(0f)] [SerializeField] float pulseStrength = 0.6f;
    [Min(0.01f)] [SerializeField] float pulseDuration = 0.08f;

    [Header("Blink Light Fallback")]
    [SerializeField] bool useBlinkLightFallback = true;
    [SerializeField] float blinkLightIntensity = 1.75f;
    [SerializeField] float blinkLightRange = 1.3f;

    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int ColorId = Shader.PropertyToID("_Color");
    static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    int currentState;
    bool isAnimating;
    bool isLocked;
    Quaternion baseLocalRotation;
    MaterialPropertyBlock pulseBlock;
    Coroutine pulseRoutine;
    Light blinkLight;

    public int CurrentState => currentState;
    public int StateCount => Mathf.Max(2, stateCount);
    public bool IsLocked => isLocked;

    void Reset()
    {
        rotatingVisual = transform;
        puzzleManager = GetComponentInParent<ColorWheelPuzzleManager>();
        audioSource = GetComponent<AudioSource>();
        feedbackRenderers = GetComponentsInChildren<Renderer>(true);
    }

    void Awake()
    {
        if (rotatingVisual == null)
            rotatingVisual = transform;
        if (puzzleManager == null)
            puzzleManager = GetComponentInParent<ColorWheelPuzzleManager>();
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();
        if (feedbackRenderers == null || feedbackRenderers.Length == 0)
            feedbackRenderers = GetComponentsInChildren<Renderer>(true);

        stateCount = Mathf.Max(2, stateCount);
        currentState = Mathf.Clamp(startingState, 0, stateCount - 1);
        baseLocalRotation = rotatingVisual.localRotation;
        ApplyRotationImmediate();
    }

    void Start()
    {
        if (puzzleManager != null)
            puzzleManager.RegisterWheel(this);
    }

    void OnValidate()
    {
        stateCount = Mathf.Max(2, stateCount);
        startingState = Mathf.Clamp(startingState, 0, stateCount - 1);
        rotateDuration = Mathf.Max(0.01f, rotateDuration);
        pulseDuration = Mathf.Max(0.01f, pulseDuration);
        blinkLightIntensity = Mathf.Max(0f, blinkLightIntensity);
        blinkLightRange = Mathf.Max(0.05f, blinkLightRange);

        if (autoDegreesFromStateCount)
            degreesPerStep = 360f / stateCount;
        else
            degreesPerStep = Mathf.Max(1f, degreesPerStep);

        if (rotatingVisual == null)
            rotatingVisual = transform;
    }

    public void AssignManager(ColorWheelPuzzleManager manager)
    {
        puzzleManager = manager;
    }

    public void SetCurrentStateImmediate(int state)
    {
        currentState = Mathf.Clamp(state, 0, StateCount - 1);
        ApplyRotationImmediate();
    }

    public void SetLocked(bool locked)
    {
        isLocked = locked;
    }

    public KeyCode GetInteractKey() => interactKey;

    public string GetPrompt(RohitFPSController player)
    {
        if (puzzleManager != null && puzzleManager.IsSolved && puzzleManager.LockWheelsWhenSolved)
            return puzzleManager.SolvedPrompt;
        return IsLocked ? lockedPrompt : rotatePrompt;
    }

    public void Interact(RohitFPSController player)
    {
        TryRotateForward();
    }

    public bool TryRotateForward()
    {
        return TryRotateBy(+1);
    }

    public bool TryRotateBackward()
    {
        if (!allowBackwardRotation) return false;
        return TryRotateBy(-1);
    }

    public IEnumerator PlayHintBlinks(int blinkCount, float onDuration, float offDuration)
    {
        blinkCount = Mathf.Max(0, blinkCount);
        if (blinkCount <= 0)
            yield break;

        onDuration = Mathf.Max(0.01f, onDuration);
        offDuration = Mathf.Max(0f, offDuration);

        if (pulseRoutine != null)
        {
            StopCoroutine(pulseRoutine);
            pulseRoutine = null;
        }

        for (int i = 0; i < blinkCount; i++)
        {
            SetPulse(true);
            yield return new WaitForSeconds(onDuration);
            SetPulse(false);

            if (i < blinkCount - 1 || offDuration > 0f)
                yield return new WaitForSeconds(offDuration);
        }
    }

    bool TryRotateBy(int step)
    {
        if (isAnimating || IsLocked)
        {
            PlayOneShot(blockedSfx);
            return false;
        }

        int nextState = GetNextState(currentState, step);
        if (nextState == currentState)
        {
            PlayOneShot(blockedSfx);
            return false;
        }

        StartCoroutine(RotateRoutine(currentState, nextState));
        return true;
    }

    int GetNextState(int fromState, int step)
    {
        int count = StateCount;
        if (wrapStates)
            return (fromState + step % count + count) % count;
        return Mathf.Clamp(fromState + step, 0, count - 1);
    }

    IEnumerator RotateRoutine(int fromState, int toState)
    {
        isAnimating = true;

        float startAngle = StateToAngle(fromState);
        float endAngle = StateToAngle(toState);
        float elapsed = 0f;

        while (elapsed < rotateDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / rotateDuration);
            float curved = rotateCurve != null ? rotateCurve.Evaluate(t) : t;
            float angle = Mathf.LerpAngle(startAngle, endAngle, curved);
            ApplyAngle(angle);
            yield return null;
        }

        currentState = toState;
        ApplyAngle(endAngle);

        PlayOneShot(rotateSfx);
        if (pulseOnRotate)
            StartPulse();

        if (puzzleManager != null)
            puzzleManager.NotifyWheelStateChanged(this);

        isAnimating = false;
    }

    void ApplyRotationImmediate()
    {
        if (rotatingVisual == null)
            return;
        ApplyAngle(StateToAngle(currentState));
    }

    void ApplyAngle(float angle)
    {
        if (rotatingVisual == null) return;
        Vector3 axis = rotationAxisLocal.sqrMagnitude > 0.0001f ? rotationAxisLocal.normalized : Vector3.forward;
        rotatingVisual.localRotation = baseLocalRotation * Quaternion.AngleAxis(angle, axis);
    }

    float StateToAngle(int state)
    {
        float stepDegrees = autoDegreesFromStateCount ? (360f / StateCount) : degreesPerStep;
        return stepDegrees * state;
    }

    void StartPulse()
    {
        if (feedbackRenderers == null || feedbackRenderers.Length == 0)
            return;

        if (pulseRoutine != null)
            StopCoroutine(pulseRoutine);

        pulseRoutine = StartCoroutine(PulseRoutine());
    }

    IEnumerator PulseRoutine()
    {
        SetPulse(true);
        yield return new WaitForSeconds(pulseDuration);
        SetPulse(false);
        pulseRoutine = null;
    }

    void SetPulse(bool enabled)
    {
        if (feedbackRenderers == null || feedbackRenderers.Length == 0)
        {
            UpdateBlinkLight(enabled, pulseColor);
            return;
        }

        if (pulseBlock == null)
            pulseBlock = new MaterialPropertyBlock();

        Color lastAppliedColor = pulseColor;

        for (int i = 0; i < feedbackRenderers.Length; i++)
        {
            Renderer r = feedbackRenderers[i];
            if (r == null) continue;

            pulseBlock.Clear();

            if (enabled && r.sharedMaterial != null)
            {
                Color resolvedPulse = ResolvePulseColor(r);
                Color c = resolvedPulse * Mathf.Max(0f, pulseStrength);
                if (r.sharedMaterial.HasProperty(BaseColorId)) pulseBlock.SetColor(BaseColorId, resolvedPulse);
                if (r.sharedMaterial.HasProperty(ColorId)) pulseBlock.SetColor(ColorId, resolvedPulse);
                if (r.sharedMaterial.HasProperty(EmissionColorId)) pulseBlock.SetColor(EmissionColorId, c);
                lastAppliedColor = resolvedPulse;
            }

            r.SetPropertyBlock(pulseBlock);
        }

        UpdateBlinkLight(enabled, lastAppliedColor);
    }

    void PlayOneShot(AudioClip clip)
    {
        if (clip == null || audioSource == null) return;
        audioSource.PlayOneShot(clip);
    }

    Color ResolvePulseColor(Renderer renderer)
    {
        if (!useMaterialColorAsPulse || renderer == null || renderer.sharedMaterial == null)
            return pulseColor;

        Material mat = renderer.sharedMaterial;
        if (mat.HasProperty(BaseColorId))
            return mat.GetColor(BaseColorId);
        if (mat.HasProperty(ColorId))
            return mat.GetColor(ColorId);
        return pulseColor;
    }

    void UpdateBlinkLight(bool enabled, Color color)
    {
        if (!useBlinkLightFallback)
            return;

        if (enabled)
        {
            EnsureBlinkLight();
            if (blinkLight == null) return;
            blinkLight.color = color;
            blinkLight.intensity = blinkLightIntensity;
            blinkLight.range = blinkLightRange;
            blinkLight.enabled = true;
        }
        else if (blinkLight != null)
        {
            blinkLight.enabled = false;
        }
    }

    void EnsureBlinkLight()
    {
        if (blinkLight != null)
            return;

        Transform existing = transform.Find("WheelHintLight");
        if (existing != null)
            blinkLight = existing.GetComponent<Light>();

        if (blinkLight == null)
        {
            GameObject go = new GameObject("WheelHintLight");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.zero;
            blinkLight = go.AddComponent<Light>();
        }

        if (blinkLight == null)
            return;

        blinkLight.type = LightType.Point;
        blinkLight.shadows = LightShadows.None;
        blinkLight.enabled = false;
    }
}
