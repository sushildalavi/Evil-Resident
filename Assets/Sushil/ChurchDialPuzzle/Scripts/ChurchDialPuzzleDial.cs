using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Attach to a dial pivot object. The visible dial mesh should be a child centered on this pivot.
/// Rotation always happens around the local Z axis in discrete steps.
/// </summary>
[DisallowMultipleComponent]
public class ChurchDialPuzzleDial : MonoBehaviour
{
    [Header("Rotation")]
    [Tooltip("Discrete stops around 360 degrees. Default 8 = 45 degrees per step.")]
    [Min(2)]
    [SerializeField] private int totalSteps = 8;

    [Tooltip("Degrees per second while the dial rotates.")]
    [Min(1f)]
    [SerializeField] private float rotateSpeed = 240f;

    [Tooltip("Starting step used when the scene or prefab loads.")]
    [SerializeField] private int startingStep = 0;

    [Tooltip("Flip this if the dial appears to rotate the opposite way in your mesh setup.")]
    [SerializeField] private bool invertRotationDirection = false;

    [Header("Visual Feedback")]
    [SerializeField] private DialSelectionHighlighter selectionHighlighter;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip rotateClickSound;

    [Header("Events")]
    public UnityEvent onRotateStarted;
    public UnityEvent onRotateComplete;

    [SerializeField, HideInInspector] private int currentStep;

    private float currentAngle;
    private float targetAngle;
    private bool isRotating;
    private bool isSelected;
    private bool isSolved;
    private bool interactionLocked;
    private Coroutine rotateRoutine;

    public int TotalSteps => Mathf.Max(2, totalSteps);
    public int CurrentStep => currentStep;
    public float TargetAngle => targetAngle;
    public float RotateSpeed => rotateSpeed;

    void Reset()
    {
        AutoAssignReferences();
        SetStepImmediate(0, true);
    }

    void Awake()
    {
        AutoAssignReferences();
        SetStepImmediate(startingStep, false);
    }

    void OnValidate()
    {
        totalSteps = Mathf.Max(2, totalSteps);
        rotateSpeed = Mathf.Max(1f, rotateSpeed);
        AutoAssignReferences();

        if (Application.isPlaying)
            return;

        currentStep = NormalizeStep(startingStep);
        targetAngle = GetAngleForStep(currentStep);
        currentAngle = targetAngle;
        ApplyAngle(currentAngle);
        ApplyVisualState();
    }

    public bool RotateClockwise()
    {
        return TryRotate(-1);
    }

    public bool RotateCounterClockwise()
    {
        return TryRotate(+1);
    }

    public bool IsAtStep(int step)
    {
        return currentStep == NormalizeStep(step);
    }

    public bool IsRotating()
    {
        return isRotating;
    }

    public void SetSelected(bool selected)
    {
        isSelected = selected;
        ApplyVisualState();
    }

    public void SetSolvedState(bool solved)
    {
        isSolved = solved;
        ApplyVisualState();
    }

    public void SetInteractionLocked(bool locked)
    {
        interactionLocked = locked;
        ApplyVisualState();
    }

    public void SetStepImmediate(int step)
    {
        SetStepImmediate(step, false);
    }

    public void SetStepImmediate(int step, bool updateStartingStep)
    {
        if (rotateRoutine != null)
        {
            StopCoroutine(rotateRoutine);
            rotateRoutine = null;
        }

        currentStep = NormalizeStep(step);
        if (updateStartingStep)
            startingStep = currentStep;

        isRotating = false;
        targetAngle = GetAngleForStep(currentStep);
        currentAngle = targetAngle;
        ApplyAngle(currentAngle);
        ApplyVisualState();
    }

    public void RandomizeStep(bool updateStartingStep)
    {
        SetStepImmediate(Random.Range(0, TotalSteps), updateStartingStep);
    }

    public void SnapToStartingStep()
    {
        SetStepImmediate(startingStep, false);
    }

    public void RefreshVisualReferences()
    {
        AutoAssignReferences();
        ApplyVisualState();
    }

    private bool TryRotate(int deltaSteps)
    {
        if (!enabled || interactionLocked || isSolved || isRotating)
            return false;

        currentStep = NormalizeStep(currentStep + deltaSteps);
        targetAngle = GetAngleForStep(currentStep);

        if (rotateRoutine != null)
            StopCoroutine(rotateRoutine);

        rotateRoutine = StartCoroutine(RotateRoutine(targetAngle));
        return true;
    }

    private IEnumerator RotateRoutine(float destinationAngle)
    {
        isRotating = true;
        onRotateStarted.Invoke();
        PlayRotateSound();

        float fromAngle = currentAngle;
        float delta = Mathf.DeltaAngle(fromAngle, destinationAngle);
        float duration = Mathf.Max(0.05f, Mathf.Abs(delta) / Mathf.Max(1f, rotateSpeed));
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = Mathf.SmoothStep(0f, 1f, t);
            currentAngle = fromAngle + (delta * eased);
            ApplyAngle(currentAngle);
            yield return null;
        }

        currentAngle = destinationAngle;
        ApplyAngle(currentAngle);
        isRotating = false;
        rotateRoutine = null;
        onRotateComplete.Invoke();
    }

    private void ApplyAngle(float angle)
    {
        transform.localRotation = Quaternion.Euler(0f, 0f, angle);
    }

    private float GetAngleForStep(int step)
    {
        float sign = invertRotationDirection ? -1f : 1f;
        return NormalizeStep(step) * (360f / TotalSteps) * sign;
    }

    private int NormalizeStep(int step)
    {
        int normalized = step % TotalSteps;
        if (normalized < 0)
            normalized += TotalSteps;
        return normalized;
    }

    private void ApplyVisualState()
    {
        if (selectionHighlighter == null)
            return;

        selectionHighlighter.SetSolved(isSolved);
        selectionHighlighter.SetInteractionLocked(interactionLocked && !isSolved);
        selectionHighlighter.SetSelected(isSelected && !interactionLocked && !isSolved);
    }

    private void PlayRotateSound()
    {
        if (audioSource != null && rotateClickSound != null)
            audioSource.PlayOneShot(rotateClickSound);
    }

    private void AutoAssignReferences()
    {
        if (selectionHighlighter == null)
            selectionHighlighter = GetComponent<DialSelectionHighlighter>();
    }
}
