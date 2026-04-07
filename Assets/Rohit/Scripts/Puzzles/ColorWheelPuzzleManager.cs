using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public class ColorWheelPuzzleManager : MonoBehaviour
{
    [Header("Wheel Setup")]
    [SerializeField] List<PuzzleWheel> wheels = new List<PuzzleWheel>();
    [SerializeField] bool autoCollectChildWheels = true;
    [SerializeField] bool includeInactiveChildWheels = true;
    [SerializeField] List<int> targetCombination = new List<int> { 0, 0, 0 };
    [SerializeField] bool checkSolvedOnStart = true;

    [Header("Solved State")]
    [SerializeField] bool isSolved;
    [SerializeField] bool lockWheelsWhenSolved = true;
    [SerializeField] string solvedPrompt = "The mechanism is already unlocked";
    [SerializeField] KeyCode resetKey = KeyCode.R;
    [SerializeField] string resetPromptSuffix = "Press R to reset";
    [SerializeField] bool verboseLogging = true;

    [Header("Solved Feedback")]
    [SerializeField] Renderer solvedIndicatorRenderer;
    [SerializeField] bool useIndicatorEmission = true;
    [SerializeField] Color unsolvedIndicatorColor = new Color(0.23f, 0.23f, 0.21f, 1f);
    [SerializeField] Color solvedIndicatorColor = new Color(0.39f, 0.48f, 0.36f, 1f);
    [SerializeField] float solvedEmissionMultiplier = 1.8f;
    [SerializeField] Light solvedIndicatorLight;
    [SerializeField] float unsolvedLightIntensity = 0f;
    [SerializeField] float solvedLightIntensity = 1.3f;
    [SerializeField] AudioSource audioSource;
    [SerializeField] AudioClip solvedSfx;

    [Header("Reward")]
    [SerializeField] PuzzleRewardHandler rewardHandler;
    [Tooltip("Useful when loading a saved scene where the puzzle is already solved and reward has not been granted.")]
    [SerializeField] bool grantRewardIfAlreadySolvedOnStart;

    [Header("Optional Hooks")]
    [SerializeField] UnityEvent onSolved;

    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int ColorId = Shader.PropertyToID("_Color");
    static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    MaterialPropertyBlock indicatorBlock;

    public bool IsSolved => isSolved;
    public bool LockWheelsWhenSolved => lockWheelsWhenSolved;
    public string SolvedPrompt => solvedPrompt;
    public KeyCode ResetKey => resetKey;

    void Reset()
    {
        rewardHandler = GetComponent<PuzzleRewardHandler>();
        audioSource = GetComponent<AudioSource>();
        CollectChildWheels();
    }

    void Awake()
    {
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();
        if (rewardHandler == null)
            rewardHandler = GetComponent<PuzzleRewardHandler>();

        if (autoCollectChildWheels || wheels.Count == 0)
            CollectChildWheels();

        BindWheelsToManager();
        SyncTargetCombinationLengthAndClamp();
    }

    void Start()
    {
        UpdateSolvedIndicatorVisual(isSolved);
        if (isSolved)
        {
            if (lockWheelsWhenSolved)
                SetWheelLockState(true);

            if (grantRewardIfAlreadySolvedOnStart && rewardHandler != null)
                rewardHandler.HandlePuzzleSolved(this);

            return;
        }

        if (checkSolvedOnStart)
            EvaluateSolved();
    }

    void OnValidate()
    {
        solvedEmissionMultiplier = Mathf.Max(0f, solvedEmissionMultiplier);
        solvedLightIntensity = Mathf.Max(0f, solvedLightIntensity);
        unsolvedLightIntensity = Mathf.Max(0f, unsolvedLightIntensity);

        if (autoCollectChildWheels && (wheels == null || wheels.Count == 0))
            CollectChildWheels();

        SyncTargetCombinationLengthAndClamp();
    }

    public void RegisterWheel(PuzzleWheel wheel)
    {
        if (wheel == null)
            return;

        if (wheels == null)
            wheels = new List<PuzzleWheel>();

        if (!wheels.Contains(wheel))
        {
            wheels.Add(wheel);
            if (verboseLogging)
                Debug.Log($"[ColorWheelPuzzle] Registered wheel: {wheel.name}", this);
        }

        wheel.AssignManager(this);
        SyncTargetCombinationLengthAndClamp();
    }

    public void NotifyWheelStateChanged(PuzzleWheel wheel)
    {
        if (isSolved)
            return;

        if (verboseLogging && wheel != null)
            Debug.Log($"[ColorWheelPuzzle] Wheel '{wheel.name}' -> state {wheel.CurrentState}", wheel);

        EvaluateSolved();
    }

    public void ResetPuzzleToStartingState()
    {
        if (wheels == null || wheels.Count == 0)
            return;

        bool changed = false;
        for (int i = 0; i < wheels.Count; i++)
        {
            PuzzleWheel wheel = wheels[i];
            if (wheel == null)
                continue;

            if (wheel.IsAnimating)
                continue;

            wheel.ResetToStartingStateImmediate();
            changed = true;
        }

        if (!changed)
            return;

        isSolved = false;
        SetWheelLockState(false);
        UpdateSolvedIndicatorVisual(false);

        if (verboseLogging)
            Debug.Log("[ColorWheelPuzzle] Puzzle reset to starting state.", this);
    }

    public string WithResetPrompt(string basePrompt)
    {
        string prompt = string.IsNullOrWhiteSpace(basePrompt) ? string.Empty : basePrompt.Trim();
        string suffix = string.IsNullOrWhiteSpace(resetPromptSuffix) ? $"Press {resetKey} to reset" : resetPromptSuffix.Trim();

        if (string.IsNullOrEmpty(suffix))
            return prompt;

        if (prompt.IndexOf(suffix, System.StringComparison.OrdinalIgnoreCase) >= 0)
            return prompt;

        if (string.IsNullOrEmpty(prompt))
            return suffix;

        return prompt + "\n" + suffix;
    }

    public int[] GetCurrentCombination()
    {
        if (wheels == null || wheels.Count == 0)
            return new int[0];

        int[] combo = new int[wheels.Count];
        for (int i = 0; i < wheels.Count; i++)
            combo[i] = wheels[i] != null ? wheels[i].CurrentState : -1;
        return combo;
    }

    [ContextMenu("Collect Child Wheels")]
    public void CollectChildWheels()
    {
        PuzzleWheel[] found = GetComponentsInChildren<PuzzleWheel>(includeInactiveChildWheels);
        wheels = new List<PuzzleWheel>(found);
        BindWheelsToManager();
        SyncTargetCombinationLengthAndClamp();
    }

    [ContextMenu("Use Current Wheel States As Target")]
    public void UseCurrentAsTarget()
    {
        if (wheels == null || wheels.Count == 0)
            return;

        SyncTargetCombinationLengthAndClamp();
        for (int i = 0; i < wheels.Count; i++)
        {
            PuzzleWheel wheel = wheels[i];
            if (wheel == null) continue;
            targetCombination[i] = Mathf.Clamp(wheel.CurrentState, 0, wheel.StateCount - 1);
        }
    }

    [ContextMenu("Force Solve (Debug)")]
    public void ForceSolveDebug()
    {
        if (isSolved)
            return;
        SolveInternal();
    }

    public void SetSolvedFromSave(bool solved, bool grantReward)
    {
        isSolved = solved;
        UpdateSolvedIndicatorVisual(isSolved);

        if (isSolved)
        {
            if (lockWheelsWhenSolved)
                SetWheelLockState(true);

            if (grantReward && rewardHandler != null)
                rewardHandler.HandlePuzzleSolved(this);
        }
        else
        {
            SetWheelLockState(false);
        }
    }

    void EvaluateSolved()
    {
        if (wheels == null || wheels.Count == 0)
            return;

        SyncTargetCombinationLengthAndClamp();
        for (int i = 0; i < wheels.Count; i++)
        {
            PuzzleWheel wheel = wheels[i];
            if (wheel == null)
                return;

            int current = wheel.CurrentState;
            int target = targetCombination[i];
            if (current != target)
                return;
        }

        SolveInternal();
    }

    void SolveInternal()
    {
        if (isSolved)
            return;

        isSolved = true;

        if (lockWheelsWhenSolved)
            SetWheelLockState(true);

        UpdateSolvedIndicatorVisual(true);

        if (audioSource != null && solvedSfx != null)
            audioSource.PlayOneShot(solvedSfx);

        if (verboseLogging)
            Debug.Log("[ColorWheelPuzzle] Puzzle solved.", this);

        if (rewardHandler != null)
            rewardHandler.HandlePuzzleSolved(this);

        onSolved?.Invoke();
    }

    void BindWheelsToManager()
    {
        if (wheels == null) return;

        for (int i = wheels.Count - 1; i >= 0; i--)
        {
            if (wheels[i] == null)
            {
                wheels.RemoveAt(i);
                continue;
            }

            wheels[i].AssignManager(this);
        }
    }

    void SetWheelLockState(bool locked)
    {
        if (wheels == null) return;

        for (int i = 0; i < wheels.Count; i++)
        {
            PuzzleWheel wheel = wheels[i];
            if (wheel == null) continue;
            wheel.SetLocked(locked);
        }
    }

    void SyncTargetCombinationLengthAndClamp()
    {
        if (wheels == null)
            wheels = new List<PuzzleWheel>();

        if (targetCombination == null)
            targetCombination = new List<int>();

        while (targetCombination.Count < wheels.Count)
            targetCombination.Add(0);

        while (targetCombination.Count > wheels.Count)
            targetCombination.RemoveAt(targetCombination.Count - 1);

        for (int i = 0; i < targetCombination.Count; i++)
        {
            PuzzleWheel wheel = wheels[i];
            int maxState = wheel != null ? Mathf.Max(0, wheel.StateCount - 1) : 0;
            targetCombination[i] = Mathf.Clamp(targetCombination[i], 0, maxState);
        }
    }

    void UpdateSolvedIndicatorVisual(bool solved)
    {
        if (solvedIndicatorRenderer != null && solvedIndicatorRenderer.sharedMaterial != null)
        {
            if (indicatorBlock == null)
                indicatorBlock = new MaterialPropertyBlock();

            Color baseColor = solved ? solvedIndicatorColor : unsolvedIndicatorColor;
            Color emission = solved ? solvedIndicatorColor * solvedEmissionMultiplier : Color.black;

            indicatorBlock.Clear();

            Material mat = solvedIndicatorRenderer.sharedMaterial;
            if (mat.HasProperty(BaseColorId)) indicatorBlock.SetColor(BaseColorId, baseColor);
            if (mat.HasProperty(ColorId)) indicatorBlock.SetColor(ColorId, baseColor);
            if (useIndicatorEmission && mat.HasProperty(EmissionColorId)) indicatorBlock.SetColor(EmissionColorId, emission);

            solvedIndicatorRenderer.SetPropertyBlock(indicatorBlock);
        }

        if (solvedIndicatorLight != null)
        {
            solvedIndicatorLight.color = solved ? solvedIndicatorColor : unsolvedIndicatorColor;
            solvedIndicatorLight.intensity = solved ? solvedLightIntensity : unsolvedLightIntensity;
            solvedIndicatorLight.enabled = solvedIndicatorLight.intensity > 0.001f;
        }
    }
}
