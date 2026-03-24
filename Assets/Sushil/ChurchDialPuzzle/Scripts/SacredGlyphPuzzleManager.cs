using System;
using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public class SacredGlyphPuzzleManager : MonoBehaviour
{
    [Header("Dial References")]
    [SerializeField] private SacredGlyphDial dial1;
    [SerializeField] private SacredGlyphDial dial2;
    [SerializeField] private SacredGlyphDial dial3;

    [Header("Correct Combination")]
    [SerializeField] private int correctDial1Step = 1;
    [SerializeField] private int correctDial2Step = 3;
    [SerializeField] private int correctDial3Step = 5;

    [Header("Startup")]
    [SerializeField] private bool lockInteractionAfterSolve = true;
    [SerializeField] private bool randomizeStartingSteps = false;
    [SerializeField] private bool avoidSolvedStart = true;

    [Header("Events")]
    [SerializeField] private UnityEvent onPuzzleSolved = new UnityEvent();

    private SacredGlyphDial[] dialCache = Array.Empty<SacredGlyphDial>();
    private bool subscribedToDials;
    private bool isSolved;
    private bool solvedEventFired;
    private bool startupInitialized;

    public event Action PuzzleSolved;

    public SacredGlyphDial Dial1 => dial1;
    public SacredGlyphDial Dial2 => dial2;
    public SacredGlyphDial Dial3 => dial3;
    public SacredGlyphDial[] Dials => dialCache;
    public bool HasAllDialReferences => dial1 != null && dial2 != null && dial3 != null;
    public bool IsSolved => isSolved;
    public bool LockInteractionAfterSolve => lockInteractionAfterSolve;
    public bool IsInteractionLocked => lockInteractionAfterSolve && isSolved;
    public UnityEvent OnPuzzleSolvedEvent => onPuzzleSolved;
    public int CorrectDial1Step => NormalizeForDial(correctDial1Step, dial1);
    public int CorrectDial2Step => NormalizeForDial(correctDial2Step, dial2);
    public int CorrectDial3Step => NormalizeForDial(correctDial3Step, dial3);

    void Reset()
    {
        RebuildDialCache();
    }

    void Awake()
    {
        RebuildDialCache();
        ClampSolutionValues();
    }

    void OnEnable()
    {
        RebuildDialCache();
        RegisterDialCallbacks();
        ApplyDialVisualState();
    }

    void Start()
    {
        InitializeStartingState();
    }

    void OnDisable()
    {
        UnregisterDialCallbacks();
    }

    void OnValidate()
    {
        RebuildDialCache();
        ClampSolutionValues();

        if (Application.isPlaying)
            return;

        ApplyDialVisualState();
    }

    public void SetDialReferences(SacredGlyphDial first, SacredGlyphDial second, SacredGlyphDial third)
    {
        UnregisterDialCallbacks();
        dial1 = first;
        dial2 = second;
        dial3 = third;
        RebuildDialCache();
        ClampSolutionValues();
        RegisterDialCallbacks();
        ApplyDialVisualState();
    }

    public SacredGlyphDial GetDial(int index)
    {
        if (index < 0 || index >= dialCache.Length)
            return null;

        return dialCache[index];
    }

    public bool AreAllDialsIdle()
    {
        for (int i = 0; i < dialCache.Length; i++)
        {
            SacredGlyphDial dial = dialCache[i];
            if (dial != null && dial.IsRotating())
                return false;
        }

        return true;
    }

    public bool AreCurrentStepsSolved()
    {
        return HasAllDialReferences &&
               dial1.IsAtStep(CorrectDial1Step) &&
               dial2.IsAtStep(CorrectDial2Step) &&
               dial3.IsAtStep(CorrectDial3Step);
    }

    public bool TryCheckSolved()
    {
        if (!HasAllDialReferences || !AreAllDialsIdle())
            return false;

        if (!AreCurrentStepsSolved())
            return false;

        isSolved = true;
        ApplyDialVisualState();

        if (solvedEventFired)
            return true;

        solvedEventFired = true;
        onPuzzleSolved.Invoke();
        PuzzleSolved?.Invoke();
        return true;
    }

    public void RandomizeDialSteps()
    {
        if (!HasAllDialReferences)
            return;

        const int maxAttempts = 24;
        int attempts = 0;

        do
        {
            attempts++;

            for (int i = 0; i < dialCache.Length; i++)
            {
                SacredGlyphDial dial = dialCache[i];
                if (dial == null)
                    continue;

                dial.SetStepInstantly(UnityEngine.Random.Range(0, dial.TotalSteps));
            }
        }
        while (avoidSolvedStart && attempts < maxAttempts && AreCurrentStepsSolved());

        SyncSolvedStateWithoutEvents();
    }

    public void SetAllDialsToZero()
    {
        SetAllDialSteps(0, 0, 0, false);
    }

    public void SetAllDialSteps(int step1, int step2, int step3, bool evaluateSolved)
    {
        if (dial1 != null)
            dial1.SetStepInstantly(step1);

        if (dial2 != null)
            dial2.SetStepInstantly(step2);

        if (dial3 != null)
            dial3.SetStepInstantly(step3);

        if (evaluateSolved)
            TryCheckSolved();
        else
            SyncSolvedStateWithoutEvents();
    }

    public void SolveInstantly()
    {
        SetAllDialSteps(CorrectDial1Step, CorrectDial2Step, CorrectDial3Step, false);
        isSolved = false;
        solvedEventFired = false;
        ApplyDialVisualState();
        TryCheckSolved();
    }

    public void ResetSolvedState()
    {
        isSolved = false;
        solvedEventFired = false;
        ApplyDialVisualState();
    }

    public void SetCurrentStepsAsSolution()
    {
        if (!HasAllDialReferences)
            return;

        correctDial1Step = dial1.CurrentStep;
        correctDial2Step = dial2.CurrentStep;
        correctDial3Step = dial3.CurrentStep;
    }

    private void InitializeStartingState()
    {
        if (startupInitialized)
            return;

        startupInitialized = true;

        if (randomizeStartingSteps)
            RandomizeDialSteps();
        else
            SyncSolvedStateWithoutEvents();
    }

    private void HandleDialRotationCompleted(SacredGlyphDial dial)
    {
        if (!AreAllDialsIdle())
            return;

        TryCheckSolved();
    }

    private void SyncSolvedStateWithoutEvents()
    {
        isSolved = AreCurrentStepsSolved();
        solvedEventFired = isSolved;
        ApplyDialVisualState();
    }

    private void ApplyDialVisualState()
    {
        for (int i = 0; i < dialCache.Length; i++)
        {
            SacredGlyphDial dial = dialCache[i];
            if (dial == null)
                continue;

            dial.SetSolvedVisual(isSolved);
            dial.SetInteractionLocked(IsInteractionLocked);
        }
    }

    private void ClampSolutionValues()
    {
        correctDial1Step = NormalizeForDial(correctDial1Step, dial1);
        correctDial2Step = NormalizeForDial(correctDial2Step, dial2);
        correctDial3Step = NormalizeForDial(correctDial3Step, dial3);
    }

    private int NormalizeForDial(int value, SacredGlyphDial dial)
    {
        int stepCount = dial != null ? dial.TotalSteps : 8;
        int normalized = value % stepCount;
        if (normalized < 0)
            normalized += stepCount;
        return normalized;
    }

    private void RebuildDialCache()
    {
        dialCache = new[] { dial1, dial2, dial3 };
    }

    private void RegisterDialCallbacks()
    {
        if (subscribedToDials)
            UnregisterDialCallbacks();

        for (int i = 0; i < dialCache.Length; i++)
        {
            SacredGlyphDial dial = dialCache[i];
            if (dial == null)
                continue;

            dial.RotationCompleted += HandleDialRotationCompleted;
        }

        subscribedToDials = true;
    }

    private void UnregisterDialCallbacks()
    {
        if (!subscribedToDials)
            return;

        for (int i = 0; i < dialCache.Length; i++)
        {
            SacredGlyphDial dial = dialCache[i];
            if (dial == null)
                continue;

            dial.RotationCompleted -= HandleDialRotationCompleted;
        }

        subscribedToDials = false;
    }
}
