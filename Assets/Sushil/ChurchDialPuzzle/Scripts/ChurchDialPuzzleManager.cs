using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[System.Serializable]
public class ChurchDialSelectedIndexEvent : UnityEvent<int> { }

[DisallowMultipleComponent]
public class ChurchDialPuzzleManager : MonoBehaviour
{
    [Header("Dial References")]
    [SerializeField] private ChurchDialPuzzleDial dial1;
    [SerializeField] private ChurchDialPuzzleDial dial2;
    [SerializeField] private ChurchDialPuzzleDial dial3;

    [Header("Solution")]
    [SerializeField] private int correctDial1Step = 2;
    [SerializeField] private int correctDial2Step = 5;
    [SerializeField] private int correctDial3Step = 1;

    [Header("State")]
    [SerializeField] private bool lockPuzzleAfterSolve = true;
    [SerializeField] private bool exitPuzzleOnSolve = true;
    [SerializeField] private bool allowReenterAfterSolve = false;
    [SerializeField] private bool randomizeStartingStepsOnAwake = true;
    [SerializeField] private bool avoidSolvedRandomStart = true;

    [Header("Camera Focus")]
    [SerializeField] private Transform cameraFocusPoint;
    [SerializeField] private bool focusPlayerCameraOnEnter = true;
    [Min(0f)]
    [SerializeField] private float cameraTransitionDuration = 0.18f;

    [Header("Input")]
    [SerializeField] private KeyCode selectDial1Key = KeyCode.Alpha1;
    [SerializeField] private KeyCode selectDial2Key = KeyCode.Alpha2;
    [SerializeField] private KeyCode selectDial3Key = KeyCode.Alpha3;
    [SerializeField] private KeyCode rotateClockwiseKey = KeyCode.D;
    [SerializeField] private KeyCode rotateCounterClockwiseKey = KeyCode.A;
    [SerializeField] private KeyCode alternateRotateClockwiseKey = KeyCode.RightArrow;
    [SerializeField] private KeyCode alternateRotateCounterClockwiseKey = KeyCode.LeftArrow;
    [SerializeField] private KeyCode exitPuzzleKey = KeyCode.Escape;

    [Header("HUD")]
    [SerializeField] private GameObject puzzleHudRoot;
    [SerializeField] private Text selectionLabel;
    [SerializeField] private string activePromptText = "1/2/3 Select   A/D Rotate   Esc Exit";
    [SerializeField] private string solvedPromptText = "Combination solved";

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip enterSound;
    [SerializeField] private AudioClip exitSound;
    [SerializeField] private AudioClip selectSound;
    [SerializeField] private AudioClip solvedSound;

    [Header("Solved Feedback")]
    [SerializeField] private Renderer centerLockRenderer;
    [SerializeField] private Light[] solvedLights;
    [SerializeField] private Color solvedGlowColor = new Color(1f, 0.72f, 0.22f, 1f);
    [SerializeField] private float solvedGlowIntensity = 2.4f;

    [Header("Events")]
    public UnityEvent onPuzzleSolved;
    public UnityEvent onPuzzleEntered;
    public UnityEvent onPuzzleExited;
    public ChurchDialSelectedIndexEvent onSelectedDialChanged;

    private static readonly string[] DialLabels = { "Dial 1", "Dial 2", "Dial 3" };

    private ChurchDialPuzzleDial[] dialCache;
    private RohitFPSController activePlayer;
    private Transform activeCameraTransform;
    private Vector3 savedCameraLocalPosition;
    private Quaternion savedCameraLocalRotation;
    private bool hasSavedCameraPose;
    private Coroutine cameraTransitionRoutine;
    private bool isActive;
    private bool isSolved;
    private bool solvedEventFired;
    private int selectedIndex;
    private int enteredOnFrame = -100;

    public bool IsActive => isActive;
    public bool IsSolved => isSolved;
    public bool AllDialsAssigned => dial1 != null && dial2 != null && dial3 != null;
    public int CorrectDial1Step => NormalizeSolution(correctDial1Step, dial1);
    public int CorrectDial2Step => NormalizeSolution(correctDial2Step, dial2);
    public int CorrectDial3Step => NormalizeSolution(correctDial3Step, dial3);
    public int SelectedIndex => Mathf.Clamp(selectedIndex, 0, Mathf.Max(0, Dials.Length - 1));
    public ChurchDialPuzzleDial SelectedDial => IsDialIndexValid(SelectedIndex) ? Dials[SelectedIndex] : null;
    public ChurchDialPuzzleDial Dial1 => dial1;
    public ChurchDialPuzzleDial Dial2 => dial2;
    public ChurchDialPuzzleDial Dial3 => dial3;
    public ChurchDialPuzzleDial[] Dials => dialCache ?? System.Array.Empty<ChurchDialPuzzleDial>();
    public Transform CameraFocusPoint => cameraFocusPoint;

    void Reset()
    {
        AutoAssignReferences();
        RebuildDialCache();
    }

    void Awake()
    {
        RebuildDialCache();
        ClampSolutionsToDialSizes();
        RegisterDialCallbacks();
        InitializeStartingState();
        ApplyDialState();
        UpdateHud();
    }

    void OnValidate()
    {
        AutoAssignReferences();
        RebuildDialCache();
        ClampSolutionsToDialSizes();
        cameraTransitionDuration = Mathf.Max(0f, cameraTransitionDuration);

        if (Application.isPlaying)
            return;

        ApplyDialState();
        UpdateHud();
    }

    void OnDisable()
    {
        ExitPuzzleInternal(false, false);
    }

    void Update()
    {
        if (!isActive)
            return;

        if (lockPuzzleAfterSolve && isSolved)
            return;

        HandleInput();
    }

    public void SetDialReferences(ChurchDialPuzzleDial first, ChurchDialPuzzleDial second, ChurchDialPuzzleDial third)
    {
        UnregisterDialCallbacks();
        dial1 = first;
        dial2 = second;
        dial3 = third;
        RebuildDialCache();
        ClampSolutionsToDialSizes();
        RegisterDialCallbacks();
        ApplyDialState();
        UpdateHud();
    }

    public void SetCameraFocusPoint(Transform focusPoint)
    {
        cameraFocusPoint = focusPoint;
    }

    public void SetCenterLockRenderer(Renderer renderer)
    {
        centerLockRenderer = renderer;
    }

    public void ResetSolvedState()
    {
        isSolved = false;
        solvedEventFired = false;
        ApplyDialState();
        UpdateHud();
    }

    public bool EnterPuzzle(RohitFPSController player)
    {
        if (isActive)
            return false;

        if (!AllDialsAssigned)
        {
            Debug.LogWarning("[ChurchDialPuzzle] Cannot enter puzzle mode because dial references are missing.", this);
            return false;
        }

        if (isSolved && !allowReenterAfterSolve)
            return false;

        activePlayer = player;
        isActive = true;
        enteredOnFrame = Time.frameCount;
        selectedIndex = Mathf.Clamp(selectedIndex, 0, Mathf.Max(0, Dials.Length - 1));

        if (activePlayer != null)
            activePlayer.isInPuzzle = true;

        FocusPlayerCamera();
        ApplyDialState();
        SetHud(true);
        onSelectedDialChanged.Invoke(SelectedIndex);
        PlaySound(enterSound);
        onPuzzleEntered.Invoke();
        return true;
    }

    public void ExitPuzzle()
    {
        ExitPuzzleInternal(true, true);
    }

    public void OnDialRotateComplete()
    {
        if (AnyDialRotating())
            return;

        TryCheckSolved();
    }

    public void SelectDial(int dialIndex)
    {
        if (!IsDialIndexValid(dialIndex))
            return;

        selectedIndex = dialIndex;
        ApplyDialState();
        PlaySound(selectSound);
        onSelectedDialChanged.Invoke(selectedIndex);
    }

    public bool RotateSelectedClockwise()
    {
        if (!CanAcceptRotationInput())
            return false;

        return SelectedDial != null && SelectedDial.RotateClockwise();
    }

    public bool RotateSelectedCounterClockwise()
    {
        if (!CanAcceptRotationInput())
            return false;

        return SelectedDial != null && SelectedDial.RotateCounterClockwise();
    }

    public bool AreCurrentStepsSolved()
    {
        if (!AllDialsAssigned)
            return false;

        return dial1.IsAtStep(CorrectDial1Step) &&
               dial2.IsAtStep(CorrectDial2Step) &&
               dial3.IsAtStep(CorrectDial3Step);
    }

    public bool TryCheckSolved()
    {
        if (AnyDialRotating())
            return false;

        if (!AreCurrentStepsSolved())
            return false;

        if (solvedEventFired)
            return true;

        isSolved = true;
        solvedEventFired = true;
        ApplySolvedFeedback();

        if (lockPuzzleAfterSolve || exitPuzzleOnSolve)
            ExitPuzzleInternal(false, true);
        else
            ApplyDialState();

        onPuzzleSolved.Invoke();
        return true;
    }

    public void SetCurrentStepsAsSolution()
    {
        if (!AllDialsAssigned)
            return;

        correctDial1Step = dial1.CurrentStep;
        correctDial2Step = dial2.CurrentStep;
        correctDial3Step = dial3.CurrentStep;
        UpdateHud();
    }

    public void RandomizeDialSteps(bool updateStartingSteps)
    {
        RebuildDialCache();
        if (Dials.Length == 0)
            return;

        const int maxAttempts = 20;
        int attempts = 0;

        do
        {
            attempts++;

            for (int i = 0; i < Dials.Length; i++)
            {
                if (Dials[i] != null)
                    Dials[i].RandomizeStep(updateStartingSteps);
            }
        }
        while (avoidSolvedRandomStart && attempts < maxAttempts && AreCurrentStepsSolved());

        isSolved = false;
        solvedEventFired = false;
        ApplyDialState();
        UpdateHud();
    }

    private void InitializeStartingState()
    {
        isSolved = false;
        solvedEventFired = false;

        if (randomizeStartingStepsOnAwake)
        {
            RandomizeDialSteps(false);
            return;
        }

        for (int i = 0; i < Dials.Length; i++)
        {
            if (Dials[i] != null)
                Dials[i].SnapToStartingStep();
        }
    }

    private void HandleInput()
    {
        if (AnyDialRotating())
            return;

        if (Time.frameCount <= enteredOnFrame)
            return;

        if (WasKeyPressed(exitPuzzleKey))
        {
            ExitPuzzle();
            return;
        }

        if (WasKeyPressed(selectDial1Key))
        {
            SelectDial(0);
            return;
        }

        if (WasKeyPressed(selectDial2Key))
        {
            SelectDial(1);
            return;
        }

        if (WasKeyPressed(selectDial3Key))
        {
            SelectDial(2);
            return;
        }

        if (WasKeyPressed(rotateClockwiseKey) || WasKeyPressed(alternateRotateClockwiseKey))
        {
            RotateSelectedClockwise();
            return;
        }

        if (WasKeyPressed(rotateCounterClockwiseKey) || WasKeyPressed(alternateRotateCounterClockwiseKey))
            RotateSelectedCounterClockwise();
    }

    private bool CanAcceptRotationInput()
    {
        return isActive &&
               !AnyDialRotating() &&
               !(lockPuzzleAfterSolve && isSolved) &&
               SelectedDial != null;
    }

    private void RebuildDialCache()
    {
        dialCache = new[] { dial1, dial2, dial3 };
        selectedIndex = Mathf.Clamp(selectedIndex, 0, Mathf.Max(0, Dials.Length - 1));
    }

    private void RegisterDialCallbacks()
    {
        for (int i = 0; i < Dials.Length; i++)
        {
            ChurchDialPuzzleDial dial = Dials[i];
            if (dial == null)
                continue;

            dial.onRotateComplete.RemoveListener(OnDialRotateComplete);
            dial.onRotateComplete.AddListener(OnDialRotateComplete);
        }
    }

    private void UnregisterDialCallbacks()
    {
        for (int i = 0; i < Dials.Length; i++)
        {
            ChurchDialPuzzleDial dial = Dials[i];
            if (dial == null)
                continue;

            dial.onRotateComplete.RemoveListener(OnDialRotateComplete);
        }
    }

    private void ClampSolutionsToDialSizes()
    {
        if (dial1 != null)
            correctDial1Step = NormalizeSolution(correctDial1Step, dial1);
        if (dial2 != null)
            correctDial2Step = NormalizeSolution(correctDial2Step, dial2);
        if (dial3 != null)
            correctDial3Step = NormalizeSolution(correctDial3Step, dial3);
    }

    private int NormalizeSolution(int value, ChurchDialPuzzleDial dial)
    {
        if (dial == null)
            return 0;

        int steps = Mathf.Max(2, dial.TotalSteps);
        int normalized = value % steps;
        if (normalized < 0)
            normalized += steps;
        return normalized;
    }

    private bool IsDialIndexValid(int index)
    {
        return index >= 0 && index < Dials.Length && Dials[index] != null;
    }

    private bool AnyDialRotating()
    {
        for (int i = 0; i < Dials.Length; i++)
        {
            ChurchDialPuzzleDial dial = Dials[i];
            if (dial != null && dial.IsRotating())
                return true;
        }

        return false;
    }

    private void ApplyDialState()
    {
        for (int i = 0; i < Dials.Length; i++)
        {
            ChurchDialPuzzleDial dial = Dials[i];
            if (dial == null)
                continue;

            bool selected = isActive && i == selectedIndex && !(lockPuzzleAfterSolve && isSolved);
            dial.SetSelected(selected);
            dial.SetSolvedState(isSolved);
            dial.SetInteractionLocked(lockPuzzleAfterSolve && isSolved);
        }

        UpdateHud();
    }

    private void UpdateHud()
    {
        if (selectionLabel == null)
        {
            UpdatePlayerPrompt();
            return;
        }

        if (!isActive)
        {
            selectionLabel.text = string.Empty;
            UpdatePlayerPrompt();
            return;
        }

        if (isSolved)
        {
            selectionLabel.text = solvedPromptText;
            UpdatePlayerPrompt();
            return;
        }

        string label = SelectedIndex >= 0 && SelectedIndex < DialLabels.Length
            ? DialLabels[SelectedIndex]
            : "Dial";

        selectionLabel.text = label + "\n" + activePromptText;
        UpdatePlayerPrompt();
    }

    private void UpdatePlayerPrompt()
    {
        if (activePlayer == null || activePlayer.promptText == null)
            return;

        if (!isActive)
        {
            HidePlayerPrompt();
            return;
        }

        if (puzzleHudRoot != null && puzzleHudRoot.activeInHierarchy)
        {
            HidePlayerPrompt();
            return;
        }

        activePlayer.promptText.gameObject.SetActive(true);

        if (isSolved)
        {
            activePlayer.promptText.text = solvedPromptText;
            return;
        }

        string label = SelectedIndex >= 0 && SelectedIndex < DialLabels.Length
            ? DialLabels[SelectedIndex]
            : "Dial";

        activePlayer.promptText.text = label + "\n" + activePromptText;
    }

    private void SetHud(bool show)
    {
        if (puzzleHudRoot != null)
            puzzleHudRoot.SetActive(show);

        UpdateHud();
    }

    private void HidePlayerPrompt()
    {
        if (activePlayer != null && activePlayer.promptText != null)
            activePlayer.promptText.gameObject.SetActive(false);
    }

    private void ApplySolvedFeedback()
    {
        PlaySound(solvedSound);

        if (centerLockRenderer != null)
        {
            Material material = centerLockRenderer.material;
            if (material != null)
            {
                if (material.HasProperty("_EmissionColor"))
                {
                    material.EnableKeyword("_EMISSION");
                    material.SetColor("_EmissionColor", solvedGlowColor * solvedGlowIntensity);
                }
                else if (material.HasProperty("_BaseColor"))
                {
                    material.SetColor("_BaseColor", solvedGlowColor);
                }
                else if (material.HasProperty("_Color"))
                {
                    material.SetColor("_Color", solvedGlowColor);
                }
            }
        }

        if (solvedLights == null)
            return;

        for (int i = 0; i < solvedLights.Length; i++)
        {
            if (solvedLights[i] == null)
                continue;

            solvedLights[i].enabled = true;
            solvedLights[i].color = solvedGlowColor;
        }
    }

    private void ExitPuzzleInternal(bool playExitSound, bool invokeEvent)
    {
        bool wasActive = isActive;
        isActive = false;

        RestorePlayerCameraImmediate();
        HidePlayerPrompt();
        ReleaseActivePlayer();
        ApplyDialState();
        SetHud(false);

        if (playExitSound)
            PlaySound(exitSound);

        if (invokeEvent && wasActive)
            onPuzzleExited.Invoke();
    }

    private void ReleaseActivePlayer()
    {
        if (activePlayer == null)
            return;

        activePlayer.isInPuzzle = false;
        activePlayer = null;
    }

    private void FocusPlayerCamera()
    {
        if (!focusPlayerCameraOnEnter || activePlayer == null || activePlayer.cameraTransform == null || cameraFocusPoint == null)
            return;

        activeCameraTransform = activePlayer.cameraTransform;
        savedCameraLocalPosition = activeCameraTransform.localPosition;
        savedCameraLocalRotation = activeCameraTransform.localRotation;
        hasSavedCameraPose = true;

        if (cameraTransitionRoutine != null)
            StopCoroutine(cameraTransitionRoutine);

        if (cameraTransitionDuration <= 0.01f)
        {
            activeCameraTransform.SetPositionAndRotation(cameraFocusPoint.position, cameraFocusPoint.rotation);
            return;
        }

        cameraTransitionRoutine = StartCoroutine(MoveCameraToFocusPoint(cameraFocusPoint.position, cameraFocusPoint.rotation));
    }

    private void RestorePlayerCameraImmediate()
    {
        if (!hasSavedCameraPose || activeCameraTransform == null)
            return;

        if (cameraTransitionRoutine != null)
        {
            StopCoroutine(cameraTransitionRoutine);
            cameraTransitionRoutine = null;
        }

        activeCameraTransform.localPosition = savedCameraLocalPosition;
        activeCameraTransform.localRotation = savedCameraLocalRotation;
        activeCameraTransform = null;
        hasSavedCameraPose = false;
    }

    private IEnumerator MoveCameraToFocusPoint(Vector3 targetPosition, Quaternion targetRotation)
    {
        if (activeCameraTransform == null)
            yield break;

        Vector3 startPosition = activeCameraTransform.position;
        Quaternion startRotation = activeCameraTransform.rotation;
        float elapsed = 0f;
        float duration = Mathf.Max(0.01f, cameraTransitionDuration);

        while (elapsed < duration && activeCameraTransform != null)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = Mathf.SmoothStep(0f, 1f, t);
            activeCameraTransform.SetPositionAndRotation(
                Vector3.Lerp(startPosition, targetPosition, eased),
                Quaternion.Slerp(startRotation, targetRotation, eased));
            yield return null;
        }

        if (activeCameraTransform != null)
            activeCameraTransform.SetPositionAndRotation(targetPosition, targetRotation);

        cameraTransitionRoutine = null;
    }

    private void PlaySound(AudioClip clip)
    {
        if (audioSource != null && clip != null)
            audioSource.PlayOneShot(clip);
    }

    private bool WasKeyPressed(KeyCode key)
    {
        bool pressed = false;
#if ENABLE_LEGACY_INPUT_MANAGER
        pressed |= Input.GetKeyDown(key);
#endif
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            pressed |= key switch
            {
                KeyCode.A => Keyboard.current.aKey.wasPressedThisFrame,
                KeyCode.D => Keyboard.current.dKey.wasPressedThisFrame,
                KeyCode.Alpha1 => Keyboard.current.digit1Key.wasPressedThisFrame,
                KeyCode.Alpha2 => Keyboard.current.digit2Key.wasPressedThisFrame,
                KeyCode.Alpha3 => Keyboard.current.digit3Key.wasPressedThisFrame,
                KeyCode.LeftArrow => Keyboard.current.leftArrowKey.wasPressedThisFrame,
                KeyCode.RightArrow => Keyboard.current.rightArrowKey.wasPressedThisFrame,
                KeyCode.Escape => Keyboard.current.escapeKey.wasPressedThisFrame,
                _ => false,
            };
        }
#endif
        return pressed;
    }

    private void AutoAssignReferences()
    {
        Transform root = ResolveLikelyRoot();

        if (dial1 == null)
        {
            Transform t = root.Find("Dial1Pivot");
            if (t != null)
                dial1 = t.GetComponent<ChurchDialPuzzleDial>();
        }

        if (dial2 == null)
        {
            Transform t = root.Find("Dial2Pivot");
            if (t != null)
                dial2 = t.GetComponent<ChurchDialPuzzleDial>();
        }

        if (dial3 == null)
        {
            Transform t = root.Find("Dial3Pivot");
            if (t != null)
                dial3 = t.GetComponent<ChurchDialPuzzleDial>();
        }

        if (cameraFocusPoint == null)
            cameraFocusPoint = root.Find("CameraFocusPoint");

        if (centerLockRenderer == null)
        {
            Transform centerLock = root.Find("CenterLock");
            if (centerLock != null)
                centerLockRenderer = centerLock.GetComponentInChildren<Renderer>(true);
        }
    }

    private Transform ResolveLikelyRoot()
    {
        Transform current = transform;
        Transform fallback = transform;

        while (current != null)
        {
            if (LooksLikePuzzleRoot(current))
                return current;

            fallback = current;
            current = current.parent;
        }

        return fallback;
    }

    private bool LooksLikePuzzleRoot(Transform target)
    {
        if (target == null)
            return false;

        return target.Find("Dial1Pivot") != null ||
               target.Find("Dial2Pivot") != null ||
               target.Find("Dial3Pivot") != null ||
               target.Find("PuzzleWallPanel") != null ||
               target.Find("PaintingInteractable") != null;
    }

#if UNITY_EDITOR
    [ContextMenu("Debug/Print Current Steps")]
    private void DebugPrintSteps()
    {
        Debug.Log($"[ChurchDialPuzzle] Current steps -> Dial1:{dial1?.CurrentStep} Dial2:{dial2?.CurrentStep} Dial3:{dial3?.CurrentStep}", this);
        Debug.Log($"[ChurchDialPuzzle] Solution -> Dial1:{CorrectDial1Step} Dial2:{CorrectDial2Step} Dial3:{CorrectDial3Step}", this);
    }

    [ContextMenu("Debug/Force Solve")]
    private void DebugForceSolve()
    {
        dial1?.SetStepImmediate(CorrectDial1Step, true);
        dial2?.SetStepImmediate(CorrectDial2Step, true);
        dial3?.SetStepImmediate(CorrectDial3Step, true);
        isSolved = false;
        solvedEventFired = false;
        TryCheckSolved();
    }

    [ContextMenu("Debug/Reset All Dials")]
    private void DebugReset()
    {
        dial1?.SetStepImmediate(0, true);
        dial2?.SetStepImmediate(0, true);
        dial3?.SetStepImmediate(0, true);
        isSolved = false;
        solvedEventFired = false;
        ApplyDialState();
    }
#endif
}
