using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public class SacredGlyphDial : MonoBehaviour
{
    [Header("Dial Configuration")]
    [Tooltip("How many discrete glyph positions this dial supports around a full 360 degree turn.")]
    [SerializeField, Min(2)] private int totalSteps = 8;

    [Tooltip("The step the dial should start on before any randomization is applied.")]
    [SerializeField] private int startingStep = 0;

    [Tooltip("Enable this if your mesh or pivot orientation causes clockwise input to spin the wrong way.")]
    [SerializeField] private bool invertRotationDirection;

    [Header("Rotation")]
    [Tooltip("Degrees per second while moving toward the next step.")]
    [SerializeField, Min(1f)] private float rotateSpeed = 180f;

    [Tooltip("Curve used to ease each stepped turn.")]
    [SerializeField] private AnimationCurve rotationCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Optional Feedback")]
    [SerializeField] private DialSelectionHighlighter selectionHighlighter;

    [Header("Events")]
    [SerializeField] private UnityEvent onRotationStarted = new UnityEvent();
    [SerializeField] private UnityEvent onRotationCompleted = new UnityEvent();

    private Coroutine rotationRoutine;
    private float currentAngle;
    private float targetAngle;
    private bool isRotating;
    private int currentStep;

    public event Action<SacredGlyphDial> RotationStarted;
    public event Action<SacredGlyphDial> RotationCompleted;

    public int TotalSteps => Mathf.Max(2, totalSteps);
    public int CurrentStep => currentStep;
    public float TargetAngle => targetAngle;
    public float RotateSpeed => Mathf.Max(1f, rotateSpeed);
    public UnityEvent OnRotationStarted => onRotationStarted;
    public UnityEvent OnRotationCompleted => onRotationCompleted;

    void Reset()
    {
        if (selectionHighlighter == null)
            selectionHighlighter = GetComponent<DialSelectionHighlighter>();

        SetStepInstantly(startingStep);
    }

    void Awake()
    {
        SetStepInstantly(startingStep);
    }

    void OnValidate()
    {
        totalSteps = Mathf.Max(2, totalSteps);
        rotateSpeed = Mathf.Max(1f, rotateSpeed);

        if (selectionHighlighter == null)
            selectionHighlighter = GetComponent<DialSelectionHighlighter>();

        if (Application.isPlaying)
            return;

        int normalizedStep = NormalizeStep(startingStep);
        currentStep = normalizedStep;
        targetAngle = GetAngleForStep(normalizedStep);
        currentAngle = targetAngle;
        ApplyAngle(currentAngle);
    }

    public bool RotateClockwise()
    {
        return TryRotate(+1);
    }

    public bool RotateCounterClockwise()
    {
        return TryRotate(-1);
    }

    public bool IsAtStep(int step)
    {
        return currentStep == NormalizeStep(step);
    }

    public bool IsRotating()
    {
        return isRotating;
    }

    public void SetStepInstantly(int step)
    {
        if (rotationRoutine != null)
        {
            StopCoroutine(rotationRoutine);
            rotationRoutine = null;
        }

        currentStep = NormalizeStep(step);
        currentAngle = GetAngleForStep(currentStep);
        targetAngle = currentAngle;
        isRotating = false;
        ApplyAngle(currentAngle);
    }

    public void SetSelectedVisual(bool selected)
    {
        if (selectionHighlighter != null)
            selectionHighlighter.SetSelected(selected);
    }

    public void SetSolvedVisual(bool solved)
    {
        if (selectionHighlighter != null)
            selectionHighlighter.SetSolved(solved);
    }

    public void SetInteractionLocked(bool locked)
    {
        if (selectionHighlighter != null)
            selectionHighlighter.SetInteractionLocked(locked);
    }

    private bool TryRotate(int deltaStep)
    {
        if (!enabled || isRotating)
            return false;

        currentStep = NormalizeStep(currentStep + deltaStep);
        targetAngle = GetAngleForStep(currentStep);

        if (rotationRoutine != null)
            StopCoroutine(rotationRoutine);

        rotationRoutine = StartCoroutine(RotateRoutine(targetAngle));
        return true;
    }

    private IEnumerator RotateRoutine(float destinationAngle)
    {
        isRotating = true;
        RotationStarted?.Invoke(this);
        onRotationStarted.Invoke();

        float fromAngle = currentAngle;
        float deltaAngle = Mathf.DeltaAngle(fromAngle, destinationAngle);
        float duration = Mathf.Max(0.05f, Mathf.Abs(deltaAngle) / RotateSpeed);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float normalized = Mathf.Clamp01(elapsed / duration);
            float eased = rotationCurve != null ? rotationCurve.Evaluate(normalized) : normalized;
            currentAngle = fromAngle + (deltaAngle * eased);
            ApplyAngle(currentAngle);
            yield return null;
        }

        currentAngle = destinationAngle;
        ApplyAngle(currentAngle);
        isRotating = false;
        rotationRoutine = null;

        RotationCompleted?.Invoke(this);
        onRotationCompleted.Invoke();
    }

    private void ApplyAngle(float angle)
    {
        transform.localRotation = Quaternion.Euler(0f, 0f, angle);
    }

    private float GetAngleForStep(int step)
    {
        float sign = invertRotationDirection ? 1f : -1f;
        return NormalizeStep(step) * (360f / TotalSteps) * sign;
    }

    private int NormalizeStep(int step)
    {
        int normalized = step % TotalSteps;
        if (normalized < 0)
            normalized += TotalSteps;
        return normalized;
    }
}
