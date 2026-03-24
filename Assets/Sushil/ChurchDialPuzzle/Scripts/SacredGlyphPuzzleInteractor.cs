using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.Events;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
#if UNITY_EDITOR
using UnityEditor;
#endif

[DisallowMultipleComponent]
public class SacredGlyphPuzzleInteractor : MonoBehaviour
{
    [Header("Core References")]
    [SerializeField] private SacredGlyphPuzzleManager puzzleManager;
    [SerializeField] private PaintingFlipReveal paintingReveal;
    [SerializeField] private Transform interactionPoint;
    [SerializeField] private Transform cameraFocusPoint;

    [Header("Puzzle Mode")]
    [SerializeField] private bool exitPuzzleModeWhenSolved;
    [SerializeField] private int startingSelectedDialIndex;

    [Header("Runtime Rescue")]
    [SerializeField] private bool autoMovePuzzleNearPlayerIfTooFar = true;
    [SerializeField, Min(1f)] private float autoMoveDistanceThreshold = 20f;
    [SerializeField, Min(1f)] private float autoMoveForwardOffset = 5.5f;
    [SerializeField] private float autoMoveVerticalOffset = -0.85f;

    [Header("Solved Reveal")]
    [SerializeField] private bool openMechanismDoorOnSolve = true;
    [SerializeField, Min(0.1f)] private float mechanismDoorOpenDuration = 0.95f;
    [SerializeField] private float mechanismDoorOpenAngle = 108f;
    [SerializeField, Range(0f, 1f)] private float vaultOpenTriggerNormalized = 0.42f;
    [SerializeField] private AnimationCurve mechanismDoorOpenCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Camera Focus")]
    [SerializeField] private bool movePlayerCameraToFocusPoint = false;
    [SerializeField, Min(0.01f)] private float cameraTransitionDuration = 0.25f;
    [SerializeField] private AnimationCurve cameraTransitionCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Cursor")]
    [SerializeField] private bool manageCursorState = true;
    [SerializeField] private CursorLockMode puzzleCursorLockMode = CursorLockMode.Locked;
    [SerializeField] private bool puzzleCursorVisible;

    [Header("Optional State Hooks")]
    [SerializeField] private Behaviour[] disableWhileInPuzzle;
    [SerializeField] private GameObject[] activateWhileInPuzzle;
    [SerializeField] private GameObject[] deactivateWhileInPuzzle;

    [Header("Events")]
    [SerializeField] private UnityEvent onPuzzleModeEntered = new UnityEvent();
    [SerializeField] private UnityEvent onPuzzleModeExited = new UnityEvent();

    private RohitFPSController activePlayer;
    private Transform activeCamera;
    private Coroutine cameraRoutine;
    private Vector3 savedCameraWorldPosition;
    private Quaternion savedCameraWorldRotation;
    private CursorLockMode savedCursorLockMode;
    private bool savedCursorVisible;
    private bool hasSavedCursorState;
    private bool hasSavedCameraPose;
    private bool isInPuzzleMode;
    private int selectedDialIndex;
    private bool minimalPlayableSetupBuilt;
    private Light[] cachedCameraLights;
    private float[] cachedCameraLightIntensities;
    private Light puzzleFillLight;
    private PlayerTorch cachedPlayerTorch;
    private bool cachedTorchWasEnabled;
    private VaultDoorRevealController runtimeVaultDoorController;
    private Transform runtimeMechanismDoorPivot;
    private Coroutine solvedRevealRoutine;
    private bool mechanismDoorOpened;

    public SacredGlyphPuzzleManager PuzzleManager => puzzleManager;
    public PaintingFlipReveal PaintingReveal => paintingReveal;
    public Transform InteractionPoint => interactionPoint != null ? interactionPoint : transform;
    public Transform CameraFocusPoint => cameraFocusPoint;
    public bool IsInPuzzleMode => isInPuzzleMode;
    public int SelectedDialIndex => selectedDialIndex;
    public RohitFPSController ActivePlayer => activePlayer;

    void Reset()
    {
        if (interactionPoint == null)
            interactionPoint = transform;
    }

    void Awake()
    {
        EnsureMinimalPlayableSetup();
        EnsureRuntimeHintOverlay();
    }

    void Start()
    {
        EnsureMinimalPlayableSetup();
        TryAutoMoveNearPlayerIfNeeded();
    }

    void OnEnable()
    {
        if (puzzleManager != null)
            puzzleManager.PuzzleSolved += HandlePuzzleSolved;
    }

    void OnDisable()
    {
        if (puzzleManager != null)
            puzzleManager.PuzzleSolved -= HandlePuzzleSolved;

        ExitPuzzleMode(false);
    }

    void OnValidate()
    {
        cameraTransitionDuration = Mathf.Max(0.01f, cameraTransitionDuration);
        mechanismDoorOpenDuration = Mathf.Max(0.1f, mechanismDoorOpenDuration);
        vaultOpenTriggerNormalized = Mathf.Clamp01(vaultOpenTriggerNormalized);

        if (interactionPoint == null)
            interactionPoint = transform;

#if UNITY_EDITOR
        if (!Application.isPlaying)
            QueueEditorVisualRepair();
#endif
    }

    void Update()
    {
        if (!isInPuzzleMode)
            return;

        HandlePuzzleModeInput();
    }

    public void SetPuzzleManager(SacredGlyphPuzzleManager manager)
    {
        if (puzzleManager != null)
            puzzleManager.PuzzleSolved -= HandlePuzzleSolved;

        puzzleManager = manager;

        if (isActiveAndEnabled && puzzleManager != null)
            puzzleManager.PuzzleSolved += HandlePuzzleSolved;

        UpdateDialSelectionVisuals();
    }

    public void SetPaintingReveal(PaintingFlipReveal reveal)
    {
        paintingReveal = reveal;
        EnsureRuntimeHintOverlay();
    }

    public void SetInteractionPoint(Transform point)
    {
        interactionPoint = point;
    }

    public void SetCameraFocusPoint(Transform focusPoint)
    {
        cameraFocusPoint = focusPoint;
    }

    public bool CanEnterPuzzleMode()
    {
        if (puzzleManager == null || !puzzleManager.HasAllDialReferences)
            return false;

        if (paintingReveal != null && !paintingReveal.IsRevealed)
            return false;

        if (puzzleManager.IsInteractionLocked)
            return false;

        return true;
    }

    public bool TryEnterPuzzleMode(RohitFPSController player)
    {
        if (isInPuzzleMode || !CanEnterPuzzleMode())
            return false;

        if (player == null)
            player = FindFirstObjectByType<RohitFPSController>();

        activePlayer = player;
        activeCamera = player != null ? player.cameraTransform : null;
        isInPuzzleMode = true;
        selectedDialIndex = Mathf.Clamp(startingSelectedDialIndex, 0, 2);

        if (activePlayer != null)
            activePlayer.isInPuzzle = true;

        SaveAndApplyCursorState();
        SaveAndMoveCameraToFocus();
        SetPuzzleLightingState(true);
        SetHookState(true);
        UpdateDialSelectionVisuals();
        onPuzzleModeEntered.Invoke();
        return true;
    }

    public void ExitPuzzleMode()
    {
        ExitPuzzleMode(true);
    }

    public void SelectDial(int dialIndex)
    {
        if (!isInPuzzleMode || puzzleManager == null)
            return;

        if (puzzleManager.GetDial(dialIndex) == null)
            return;

        selectedDialIndex = dialIndex;
        UpdateDialSelectionVisuals();
    }

    public bool RotateSelectedDialClockwise()
    {
        SacredGlyphDial selectedDial = GetSelectedDial();
        if (selectedDial == null || !CanRotateDial())
            return false;

        return selectedDial.RotateClockwise();
    }

    public bool RotateSelectedDialCounterClockwise()
    {
        SacredGlyphDial selectedDial = GetSelectedDial();
        if (selectedDial == null || !CanRotateDial())
            return false;

        return selectedDial.RotateCounterClockwise();
    }

    private void HandlePuzzleSolved()
    {
        StartSolvedRevealSequence();
        UpdateDialSelectionVisuals();

        if (exitPuzzleModeWhenSolved && isInPuzzleMode)
            ExitPuzzleMode();
    }

    private void HandlePuzzleModeInput()
    {
        if (WasExitPressed())
        {
            ExitPuzzleMode();
            return;
        }

        if (puzzleManager == null || puzzleManager.IsInteractionLocked)
            return;

        if (!puzzleManager.AreAllDialsIdle())
            return;

        if (WasDialSelectPressed(0))
        {
            SelectDial(0);
            return;
        }

        if (WasDialSelectPressed(1))
        {
            SelectDial(1);
            return;
        }

        if (WasDialSelectPressed(2))
        {
            SelectDial(2);
            return;
        }

        if (WasRotateCounterClockwisePressed())
        {
            RotateSelectedDialCounterClockwise();
            return;
        }

        if (WasRotateClockwisePressed())
            RotateSelectedDialClockwise();
    }

    private bool CanRotateDial()
    {
        return isInPuzzleMode &&
               puzzleManager != null &&
               !puzzleManager.IsInteractionLocked &&
               puzzleManager.AreAllDialsIdle();
    }

    private SacredGlyphDial GetSelectedDial()
    {
        return puzzleManager != null ? puzzleManager.GetDial(selectedDialIndex) : null;
    }

    private void UpdateDialSelectionVisuals()
    {
        if (puzzleManager == null)
            return;

        for (int i = 0; i < 3; i++)
        {
            SacredGlyphDial dial = puzzleManager.GetDial(i);
            if (dial == null)
                continue;

            bool isSelected = isInPuzzleMode &&
                              !puzzleManager.IsInteractionLocked &&
                              i == selectedDialIndex;
            dial.SetSelectedVisual(isSelected);
        }
    }

    private void ExitPuzzleMode(bool invokeExitEvent)
    {
        if (!isInPuzzleMode && activePlayer == null && activeCamera == null)
            return;

        if (cameraRoutine != null)
        {
            StopCoroutine(cameraRoutine);
            cameraRoutine = null;
        }

        RestoreCameraPose();
        RestoreCursorState();
        SetPuzzleLightingState(false);
        SetHookState(false);

        if (activePlayer != null)
            activePlayer.isInPuzzle = false;

        isInPuzzleMode = false;
        UpdateDialSelectionVisuals();
        activePlayer = null;
        activeCamera = null;

        if (invokeExitEvent)
            onPuzzleModeExited.Invoke();
    }

    private void SaveAndMoveCameraToFocus()
    {
        if (!movePlayerCameraToFocusPoint || activeCamera == null || cameraFocusPoint == null)
            return;

        savedCameraWorldPosition = activeCamera.position;
        savedCameraWorldRotation = activeCamera.rotation;
        hasSavedCameraPose = true;

        if (cameraRoutine != null)
            StopCoroutine(cameraRoutine);

        cameraRoutine = StartCoroutine(MoveCameraRoutine(activeCamera, activeCamera.position, activeCamera.rotation, cameraFocusPoint.position, cameraFocusPoint.rotation));
    }

    private void RestoreCameraPose()
    {
        if (!hasSavedCameraPose || activeCamera == null)
            return;

        if (cameraRoutine != null)
            StopCoroutine(cameraRoutine);

        cameraRoutine = StartCoroutine(MoveCameraRoutine(activeCamera, activeCamera.position, activeCamera.rotation, savedCameraWorldPosition, savedCameraWorldRotation));
        hasSavedCameraPose = false;
    }

    private IEnumerator MoveCameraRoutine(Transform cameraTransform, Vector3 startPosition, Quaternion startRotation, Vector3 endPosition, Quaternion endRotation)
    {
        float elapsed = 0f;
        float duration = Mathf.Max(0.01f, cameraTransitionDuration);

        while (elapsed < duration && cameraTransform != null)
        {
            elapsed += Time.unscaledDeltaTime;
            float normalized = Mathf.Clamp01(elapsed / duration);
            float eased = cameraTransitionCurve != null ? cameraTransitionCurve.Evaluate(normalized) : normalized;
            cameraTransform.position = Vector3.Lerp(startPosition, endPosition, eased);
            cameraTransform.rotation = Quaternion.Slerp(startRotation, endRotation, eased);
            yield return null;
        }

        if (cameraTransform != null)
        {
            cameraTransform.position = endPosition;
            cameraTransform.rotation = endRotation;
        }

        cameraRoutine = null;
    }

    private void SaveAndApplyCursorState()
    {
        if (!manageCursorState)
            return;

        savedCursorLockMode = Cursor.lockState;
        savedCursorVisible = Cursor.visible;
        hasSavedCursorState = true;

        Cursor.lockState = puzzleCursorLockMode;
        Cursor.visible = puzzleCursorVisible;
    }

    private void RestoreCursorState()
    {
        if (!manageCursorState || !hasSavedCursorState)
            return;

        Cursor.lockState = savedCursorLockMode;
        Cursor.visible = savedCursorVisible;
        hasSavedCursorState = false;
    }

    private void SetHookState(bool inPuzzle)
    {
        SetBehavioursEnabled(disableWhileInPuzzle, !inPuzzle);
        SetObjectsActive(activateWhileInPuzzle, inPuzzle);
        SetObjectsActive(deactivateWhileInPuzzle, !inPuzzle);
    }

    private void SetBehavioursEnabled(Behaviour[] behaviours, bool enabledState)
    {
        if (behaviours == null)
            return;

        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] != null)
                behaviours[i].enabled = enabledState;
        }
    }

    private void SetObjectsActive(GameObject[] targets, bool activeState)
    {
        if (targets == null)
            return;

        for (int i = 0; i < targets.Length; i++)
        {
            if (targets[i] != null)
                targets[i].SetActive(activeState);
        }
    }

    private bool WasDialSelectPressed(int index)
    {
        switch (index)
        {
            case 0:
                return WasAlpha1Pressed();
            case 1:
                return WasAlpha2Pressed();
            case 2:
                return WasAlpha3Pressed();
            default:
                return false;
        }
    }

    private bool WasRotateClockwisePressed()
    {
        bool pressed = false;

#if ENABLE_LEGACY_INPUT_MANAGER
        pressed |= Input.GetKeyDown(KeyCode.D);
        pressed |= Input.GetKeyDown(KeyCode.RightArrow);
#endif
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
            pressed |= Keyboard.current.dKey.wasPressedThisFrame || Keyboard.current.rightArrowKey.wasPressedThisFrame;
#endif

        return pressed;
    }

    private bool WasRotateCounterClockwisePressed()
    {
        bool pressed = false;

#if ENABLE_LEGACY_INPUT_MANAGER
        pressed |= Input.GetKeyDown(KeyCode.A);
        pressed |= Input.GetKeyDown(KeyCode.LeftArrow);
#endif
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
            pressed |= Keyboard.current.aKey.wasPressedThisFrame || Keyboard.current.leftArrowKey.wasPressedThisFrame;
#endif

        return pressed;
    }

    private bool WasExitPressed()
    {
        bool pressed = false;

#if ENABLE_LEGACY_INPUT_MANAGER
        pressed |= Input.GetKeyDown(KeyCode.Escape);
#endif
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
            pressed |= Keyboard.current.escapeKey.wasPressedThisFrame;
#endif

        return pressed;
    }

    private bool WasAlpha1Pressed()
    {
        bool pressed = false;

#if ENABLE_LEGACY_INPUT_MANAGER
        pressed |= Input.GetKeyDown(KeyCode.Alpha1);
#endif
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
            pressed |= Keyboard.current.digit1Key.wasPressedThisFrame || Keyboard.current.numpad1Key.wasPressedThisFrame;
#endif

        return pressed;
    }

    private bool WasAlpha2Pressed()
    {
        bool pressed = false;

#if ENABLE_LEGACY_INPUT_MANAGER
        pressed |= Input.GetKeyDown(KeyCode.Alpha2);
#endif
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
            pressed |= Keyboard.current.digit2Key.wasPressedThisFrame || Keyboard.current.numpad2Key.wasPressedThisFrame;
#endif

        return pressed;
    }

    private bool WasAlpha3Pressed()
    {
        bool pressed = false;

#if ENABLE_LEGACY_INPUT_MANAGER
        pressed |= Input.GetKeyDown(KeyCode.Alpha3);
#endif
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
            pressed |= Keyboard.current.digit3Key.wasPressedThisFrame || Keyboard.current.numpad3Key.wasPressedThisFrame;
#endif

        return pressed;
    }

    private void EnsureRuntimeHintOverlay()
    {
        PuzzleHintOverlay overlay = GetComponent<PuzzleHintOverlay>();
        if (overlay == null)
            overlay = gameObject.AddComponent<PuzzleHintOverlay>();

        overlay.SetPuzzleInteractor(this);

        if (paintingReveal != null)
            overlay.SetPaintingInteractable(paintingReveal.transform);
    }

    private void EnsureMinimalPlayableSetup()
    {
        if (minimalPlayableSetupBuilt)
            return;

        Transform root = transform;
        Transform paintingInteractableTransform = paintingReveal != null ? paintingReveal.transform : root.Find("PaintingInteractable");
        Transform paintingVisualTransform = root.Find("PaintingVisual");
        Transform puzzleWallPanelTransform = root.Find("PuzzleWallPanel");
        Transform centerLockTransform = root.Find("CenterLock");
        Transform vaultDoorRootTransform = root.Find("VaultDoorRoot");
        Transform interactionTransform = interactionPoint != null ? interactionPoint : root.Find("InteractionPoint");
        Transform cameraTransformPoint = cameraFocusPoint != null ? cameraFocusPoint : root.Find("CameraFocusPoint");

        if (paintingInteractableTransform == null || paintingVisualTransform == null || puzzleWallPanelTransform == null)
            return;

        float paintingWidth = 1.45f;
        float paintingHeight = 2.05f;
        float paintingThickness = 0.08f;
        float paintingCenterY = 1.55f;
        float paintingFrontZ = 0.12f;

        Transform hingePivot = FindOrCreateChild(root, "BasicPaintingHingePivot");
        hingePivot.localPosition = new Vector3(-paintingWidth * 0.5f, paintingCenterY, paintingFrontZ);
        hingePivot.localRotation = Quaternion.identity;
        hingePivot.localScale = Vector3.one;

        // Keep the legacy PaintingVisual node out of the way and use a dedicated
        // door object under a clean hinge pivot so the swing point is reliable.
        paintingVisualTransform.localPosition = new Vector3(0f, paintingCenterY, -0.3f);
        paintingVisualTransform.localRotation = Quaternion.identity;
        paintingVisualTransform.localScale = Vector3.one;

        SetSiblingChildrenActive(paintingVisualTransform, string.Empty, false);

        Transform basicDoor = FindOrCreateChild(hingePivot, "BasicPaintingDoor");
        basicDoor.localPosition = new Vector3(paintingWidth * 0.5f, 0f, 0f);
        basicDoor.localRotation = Quaternion.identity;
        basicDoor.localScale = Vector3.one;

        paintingInteractableTransform.localPosition = new Vector3(0f, paintingCenterY, paintingFrontZ + 0.04f);
        paintingInteractableTransform.localRotation = Quaternion.identity;
        paintingInteractableTransform.localScale = Vector3.one;

        BoxCollider paintingCollider = paintingInteractableTransform.GetComponent<BoxCollider>();
        if (paintingCollider == null)
            paintingCollider = paintingInteractableTransform.gameObject.AddComponent<BoxCollider>();

        paintingCollider.isTrigger = false;
        paintingCollider.center = Vector3.zero;
        paintingCollider.size = new Vector3(1.65f, 2.2f, 0.28f);

        BuildBasicPaintingDoor(basicDoor, paintingWidth, paintingHeight, paintingThickness);
        BuildBasicPanel(puzzleWallPanelTransform);

        Transform dial1Pivot = root.Find("Dial1Pivot");
        Transform dial2Pivot = root.Find("Dial2Pivot");
        Transform dial3Pivot = root.Find("Dial3Pivot");

        SetupDialVisual(dial1Pivot, new Vector3(-0.48f, 1.78f, 0.04f));
        SetupDialVisual(dial2Pivot, new Vector3(0f, 1.36f, 0.04f));
        SetupDialVisual(dial3Pivot, new Vector3(0.48f, 1.78f, 0.04f));

        if (centerLockTransform != null)
        {
            centerLockTransform.localPosition = new Vector3(0f, 0.86f, 0.08f);
            centerLockTransform.localRotation = Quaternion.identity;
            centerLockTransform.localScale = Vector3.one;
            BuildBasicCenterLock(centerLockTransform);
        }

        runtimeMechanismDoorPivot = SetupPuzzleMechanismHinge(
            root,
            puzzleWallPanelTransform,
            dial1Pivot,
            dial2Pivot,
            dial3Pivot,
            centerLockTransform);
        mechanismDoorOpened = false;

        if (interactionTransform != null)
        {
            interactionTransform.localPosition = new Vector3(0f, 1.45f, 1.35f);
            interactionTransform.localRotation = Quaternion.identity;
        }

        if (cameraTransformPoint != null)
        {
            cameraTransformPoint.localPosition = new Vector3(0f, 1.48f, 1.7f);
            cameraTransformPoint.localRotation = Quaternion.LookRotation(Vector3.back, Vector3.up);
        }

        if (vaultDoorRootTransform != null)
            runtimeVaultDoorController = BuildBasicVault(vaultDoorRootTransform);

        movePlayerCameraToFocusPoint = false;
        EnsurePlayablePuzzleState(dial1Pivot, dial2Pivot, dial3Pivot);
        EnsurePuzzleFillLight(root);

        if (paintingReveal != null)
        {
            paintingReveal.SetRevealTarget(hingePivot);
            paintingReveal.SetPuzzleInteractor(this);
            paintingReveal.SetAutoEnterPuzzleAfterReveal(false);
            paintingReveal.SetRevealMode(PaintingFlipReveal.RevealMode.FlipOpenOnHinge);
            paintingReveal.SetRevealDuration(1.05f);
            paintingReveal.SetOpenAngle(118f);
            paintingReveal.SetOnlyRevealOnce(true);
        }

        minimalPlayableSetupBuilt = true;
    }

    private void SetupDialVisual(Transform dialPivot, Vector3 localPosition)
    {
        if (dialPivot == null)
            return;

        dialPivot.localPosition = localPosition;
        dialPivot.localRotation = Quaternion.identity;
        dialPivot.localScale = Vector3.one;

        Transform dialMesh = dialPivot.Find("Dial1Mesh");
        if (dialMesh == null) dialMesh = dialPivot.Find("Dial2Mesh");
        if (dialMesh == null) dialMesh = dialPivot.Find("Dial3Mesh");
        if (dialMesh == null)
            dialMesh = FindOrCreateChild(dialPivot, "DialMesh");

        dialMesh.localPosition = Vector3.zero;
        dialMesh.localRotation = Quaternion.identity;
        dialMesh.localScale = Vector3.one;

        SetSiblingChildrenActive(dialMesh, "BasicPlayableDial", false);
        Transform group = FindOrCreateChild(dialMesh, "BasicPlayableDial");
        ClearChildren(group);

        CreatePrimitiveVisual(group, PrimitiveType.Cylinder, "Rim",
            new Vector3(0f, 0f, 0.01f),
            Quaternion.Euler(90f, 0f, 0f),
            new Vector3(0.68f, 0.06f, 0.68f),
            new Color(0.67f, 0.56f, 0.25f),
            0.1f);

        CreatePrimitiveVisual(group, PrimitiveType.Cylinder, "Face",
            new Vector3(0f, 0f, 0.03f),
            Quaternion.Euler(90f, 0f, 0f),
            new Vector3(0.56f, 0.04f, 0.56f),
            new Color(0.18f, 0.18f, 0.2f),
            0.03f);

        CreatePrimitiveVisual(group, PrimitiveType.Cylinder, "Core",
            new Vector3(0f, 0f, 0.055f),
            Quaternion.Euler(90f, 0f, 0f),
            new Vector3(0.12f, 0.08f, 0.12f),
            new Color(0.74f, 0.62f, 0.24f),
            0.2f);

        for (int i = 0; i < 8; i++)
        {
            float angle = i * Mathf.PI * 2f / 8f;
            Vector3 studPosition = new Vector3(Mathf.Cos(angle) * 0.36f, Mathf.Sin(angle) * 0.36f, 0.06f);
            CreatePrimitiveVisual(group, PrimitiveType.Sphere, $"Stud_{i}",
                studPosition,
                Quaternion.identity,
                new Vector3(0.06f, 0.06f, 0.03f),
                new Color(0.8f, 0.7f, 0.3f),
                0.15f);
        }

        Transform parent = dialPivot.parent != null ? dialPivot.parent : transform;
        Transform pointer = FindOrCreateChild(parent, $"{dialPivot.name}_Pointer");
        pointer.localPosition = localPosition + new Vector3(0f, 0.44f, 0.12f);
        pointer.localRotation = Quaternion.identity;
        pointer.localScale = Vector3.one;
        ClearChildren(pointer);

        CreatePrimitiveVisual(pointer, PrimitiveType.Cube, "PointerStem",
            new Vector3(0f, -0.06f, 0f),
            Quaternion.identity,
            new Vector3(0.06f, 0.14f, 0.04f),
            new Color(0.86f, 0.76f, 0.3f),
            0.25f);

        CreatePrimitiveVisual(pointer, PrimitiveType.Cube, "PointerTip",
            new Vector3(0f, -0.15f, 0f),
            Quaternion.Euler(0f, 0f, 45f),
            new Vector3(0.11f, 0.11f, 0.04f),
            new Color(0.94f, 0.85f, 0.42f),
            0.35f);
    }

    private void BuildBasicPaintingDoor(Transform paintingVisualTransform, float width, float height, float thickness)
    {
        SetSiblingChildrenActive(paintingVisualTransform, "BasicPlayablePainting", false);
        Transform group = FindOrCreateChild(paintingVisualTransform, "BasicPlayablePainting");
        ClearChildren(group);

        CreatePrimitiveVisual(group, PrimitiveType.Cube, "DoorBody",
            Vector3.zero,
            Quaternion.identity,
            new Vector3(width, height, thickness),
            new Color(0.14f, 0.10f, 0.08f),
            0f);

        float stileWidth = 0.09f;
        float railHeight = 0.1f;
        float frameZ = (thickness * 0.5f) + 0.01f;
        float canvasZ = (thickness * 0.5f) + 0.018f;

        CreatePrimitiveVisual(group, PrimitiveType.Cube, "ShadowInset",
            new Vector3(0f, 0f, frameZ - 0.012f),
            Quaternion.identity,
            new Vector3(width - 0.16f, height - 0.16f, 0.012f),
            new Color(0.06f, 0.04f, 0.04f),
            0f);

        CreatePrimitiveVisual(group, PrimitiveType.Cube, "FrameLeft",
            new Vector3((-width * 0.5f) + (stileWidth * 0.5f), 0f, frameZ),
            Quaternion.identity,
            new Vector3(stileWidth, height - 0.04f, 0.018f),
            new Color(0.74f, 0.62f, 0.24f),
            0.01f);

        CreatePrimitiveVisual(group, PrimitiveType.Cube, "FrameRight",
            new Vector3((width * 0.5f) - (stileWidth * 0.5f), 0f, frameZ),
            Quaternion.identity,
            new Vector3(stileWidth, height - 0.04f, 0.018f),
            new Color(0.74f, 0.62f, 0.24f),
            0.02f);

        CreatePrimitiveVisual(group, PrimitiveType.Cube, "FrameTop",
            new Vector3(0f, (height * 0.5f) - (railHeight * 0.5f), frameZ),
            Quaternion.identity,
            new Vector3(width - (stileWidth * 2f), railHeight, 0.018f),
            new Color(0.74f, 0.62f, 0.24f),
            0.02f);

        CreatePrimitiveVisual(group, PrimitiveType.Cube, "FrameBottom",
            new Vector3(0f, (-height * 0.5f) + (railHeight * 0.5f), frameZ),
            Quaternion.identity,
            new Vector3(width - (stileWidth * 2f), railHeight, 0.018f),
            new Color(0.74f, 0.62f, 0.24f),
            0.02f);

        CreatePrimitiveVisual(group, PrimitiveType.Cube, "Canvas",
            new Vector3(0f, 0f, canvasZ),
            Quaternion.identity,
            new Vector3(width - 0.24f, height - 0.24f, 0.014f),
            new Color(0.31f, 0.10f, 0.10f),
            0f);

        CreatePrimitiveVisual(group, PrimitiveType.Cube, "CanvasBarVertical",
            new Vector3(0f, 0f, canvasZ + 0.012f),
            Quaternion.identity,
            new Vector3(0.06f, height - 0.54f, 0.01f),
            new Color(0.78f, 0.68f, 0.26f),
            0.02f);

        CreatePrimitiveVisual(group, PrimitiveType.Cube, "CanvasBarHorizontal",
            new Vector3(0f, 0f, canvasZ + 0.014f),
            Quaternion.identity,
            new Vector3(width - 0.56f, 0.06f, 0.01f),
            new Color(0.78f, 0.68f, 0.26f),
            0.02f);

        CreatePrimitiveVisual(group, PrimitiveType.Cylinder, "CenterMedallion",
            new Vector3(0f, 0f, canvasZ + 0.018f),
            Quaternion.Euler(90f, 0f, 0f),
            new Vector3(0.14f, 0.024f, 0.14f),
            new Color(0.80f, 0.70f, 0.30f),
            0.03f);

        CreatePrimitiveVisual(group, PrimitiveType.Cube, "HandlePlate",
            new Vector3(width * 0.41f, 0f, canvasZ + 0.014f),
            Quaternion.identity,
            new Vector3(0.065f, 0.30f, 0.012f),
            new Color(0.18f, 0.18f, 0.2f),
            0f);

        CreatePrimitiveVisual(group, PrimitiveType.Cylinder, "HandleBase",
            new Vector3(width * 0.39f, 0f, canvasZ + 0.024f),
            Quaternion.Euler(90f, 0f, 0f),
            new Vector3(0.04f, 0.028f, 0.04f),
            new Color(0.76f, 0.64f, 0.28f),
            0.03f);

        CreatePrimitiveVisual(group, PrimitiveType.Cube, "HandleGrip",
            new Vector3(width * 0.44f, 0f, canvasZ + 0.03f),
            Quaternion.identity,
            new Vector3(0.035f, 0.20f, 0.018f),
            new Color(0.72f, 0.58f, 0.23f),
            0.02f);

        for (int i = 0; i < 3; i++)
        {
            float hingeY = Mathf.Lerp(height * 0.34f, -height * 0.34f, i / 2f);
            CreatePrimitiveVisual(group, PrimitiveType.Cube, $"HingePlate_{i}",
                new Vector3((-width * 0.5f) + 0.038f, hingeY, frameZ - 0.004f),
                Quaternion.identity,
                new Vector3(0.03f, 0.18f, 0.012f),
                new Color(0.60f, 0.50f, 0.22f),
                0.01f);

            CreatePrimitiveVisual(group, PrimitiveType.Cylinder, $"Hinge_{i}",
                new Vector3((-width * 0.5f) + 0.01f, hingeY, 0f),
                Quaternion.identity,
                new Vector3(0.03f, 0.09f, 0.03f),
                new Color(0.65f, 0.54f, 0.24f),
                0.02f);
        }
    }

    private void BuildBasicPanel(Transform panelTransform)
    {
        panelTransform.localPosition = new Vector3(0f, 1.55f, -0.02f);
        panelTransform.localRotation = Quaternion.identity;
        panelTransform.localScale = Vector3.one;

        SetSiblingChildrenActive(panelTransform, "BasicPlayablePanel", false);
        Transform group = FindOrCreateChild(panelTransform, "BasicPlayablePanel");
        ClearChildren(group);

        CreatePrimitiveVisual(group, PrimitiveType.Cube, "Slab",
            Vector3.zero,
            Quaternion.identity,
            new Vector3(1.8f, 2.3f, 0.16f),
            new Color(0.34f, 0.34f, 0.36f),
            0.02f);

        CreatePrimitiveVisual(group, PrimitiveType.Cube, "Inset",
            new Vector3(0f, 0f, 0.05f),
            Quaternion.identity,
            new Vector3(1.45f, 1.95f, 0.05f),
            new Color(0.1f, 0.1f, 0.12f),
            0f);
    }

    private Transform SetupPuzzleMechanismHinge(
        Transform root,
        Transform panelTransform,
        Transform dial1Pivot,
        Transform dial2Pivot,
        Transform dial3Pivot,
        Transform centerLockTransform)
    {
        if (root == null || panelTransform == null)
            return null;

        Transform hingePivot = FindOrCreateChild(root, "PuzzleMechanismDoorPivot");
        hingePivot.localPosition = new Vector3(-0.92f, 1.55f, -0.02f);
        hingePivot.localRotation = Quaternion.identity;
        hingePivot.localScale = Vector3.one;

        ReparentKeepingWorld(panelTransform, hingePivot);
        ReparentKeepingWorld(dial1Pivot, hingePivot);
        ReparentKeepingWorld(dial2Pivot, hingePivot);
        ReparentKeepingWorld(dial3Pivot, hingePivot);
        ReparentKeepingWorld(centerLockTransform, hingePivot);
        ReparentKeepingWorld(root.Find("Dial1Pivot_Pointer"), hingePivot);
        ReparentKeepingWorld(root.Find("Dial2Pivot_Pointer"), hingePivot);
        ReparentKeepingWorld(root.Find("Dial3Pivot_Pointer"), hingePivot);

        Transform hingeDecor = FindOrCreateChild(hingePivot, "MechanismHinges");
        ClearChildren(hingeDecor);

        for (int i = 0; i < 3; i++)
        {
            float hingeY = Mathf.Lerp(0.72f, -0.72f, i / 2f);
            CreatePrimitiveVisual(hingeDecor, PrimitiveType.Cylinder, $"MechanismHinge_{i}",
                new Vector3(0f, hingeY, 0.06f),
                Quaternion.identity,
                new Vector3(0.06f, 0.18f, 0.06f),
                new Color(0.58f, 0.50f, 0.24f),
                0f);
        }

        return hingePivot;
    }

    private void BuildBasicCenterLock(Transform centerLockTransform)
    {
        SetSiblingChildrenActive(centerLockTransform, "BasicPlayableCenterLock", false);
        Transform group = FindOrCreateChild(centerLockTransform, "BasicPlayableCenterLock");
        ClearChildren(group);

        CreatePrimitiveVisual(group, PrimitiveType.Cylinder, "LockBase",
            Vector3.zero,
            Quaternion.Euler(90f, 0f, 0f),
            new Vector3(0.22f, 0.05f, 0.22f),
            new Color(0.6f, 0.52f, 0.2f),
            0.18f);

        CreatePrimitiveVisual(group, PrimitiveType.Cube, "LockSlot",
            new Vector3(0f, 0f, 0.05f),
            Quaternion.identity,
            new Vector3(0.06f, 0.24f, 0.02f),
            new Color(0.08f, 0.08f, 0.08f),
            0f);
    }

    private VaultDoorRevealController BuildBasicVault(Transform vaultDoorRootTransform)
    {
        vaultDoorRootTransform.localPosition = new Vector3(0f, 1.34f, -0.32f);
        vaultDoorRootTransform.localRotation = Quaternion.identity;
        vaultDoorRootTransform.localScale = Vector3.one;

        Transform leftDoor = FindOrCreateChild(vaultDoorRootTransform, "VaultDoorLeft");
        Transform rightDoor = FindOrCreateChild(vaultDoorRootTransform, "VaultDoorRight");
        Transform singleDoor = FindOrCreateChild(vaultDoorRootTransform, "VaultSingleDoor");
        Transform slidingPanel = FindOrCreateChild(vaultDoorRootTransform, "VaultSlidingPanel");
        Transform vaultInterior = FindOrCreateChild(vaultDoorRootTransform, "VaultInterior");
        Transform housing = FindOrCreateChild(vaultDoorRootTransform, "VaultHousing");

        leftDoor.gameObject.SetActive(false);
        rightDoor.gameObject.SetActive(false);
        slidingPanel.gameObject.SetActive(false);
        singleDoor.gameObject.SetActive(true);

        leftDoor.localPosition = new Vector3(-0.32f, 0f, 0f);
        rightDoor.localPosition = new Vector3(0.32f, 0f, 0f);
        singleDoor.localPosition = new Vector3(-0.56f, 0f, 0.04f);
        slidingPanel.localPosition = Vector3.zero;
        vaultInterior.localPosition = new Vector3(0f, 0f, -0.78f);

        SetSiblingChildrenActive(housing, "BasicVaultHousing", false);
        Transform housingGroup = FindOrCreateChild(housing, "BasicVaultHousing");
        ClearChildren(housingGroup);

        CreatePrimitiveVisual(housingGroup, PrimitiveType.Cube, "WallFrame",
            Vector3.zero,
            Quaternion.identity,
            new Vector3(1.95f, 1.92f, 0.26f),
            new Color(0.20f, 0.20f, 0.22f),
            0f);

        CreatePrimitiveVisual(housingGroup, PrimitiveType.Cube, "HingeColumn",
            new Vector3(-0.78f, 0f, 0.08f),
            Quaternion.identity,
            new Vector3(0.20f, 1.76f, 0.18f),
            new Color(0.16f, 0.16f, 0.18f),
            0f);

        CreatePrimitiveVisual(housingGroup, PrimitiveType.Cube, "Threshold",
            new Vector3(0f, -0.82f, 0.08f),
            Quaternion.identity,
            new Vector3(1.30f, 0.12f, 0.18f),
            new Color(0.32f, 0.30f, 0.28f),
            0f);

        CreatePrimitiveVisual(housingGroup, PrimitiveType.Cylinder, "OpeningRing",
            new Vector3(0f, 0f, 0.08f),
            Quaternion.Euler(90f, 0f, 0f),
            new Vector3(1.18f, 0.12f, 1.18f),
            new Color(0.66f, 0.56f, 0.24f),
            0.01f);

        CreatePrimitiveVisual(housingGroup, PrimitiveType.Cylinder, "InnerOpening",
            new Vector3(0f, 0f, 0.1f),
            Quaternion.Euler(90f, 0f, 0f),
            new Vector3(0.98f, 0.08f, 0.98f),
            new Color(0.09f, 0.09f, 0.1f),
            0f);

        BuildBasicVaultDoor(singleDoor);

        SetSiblingChildrenActive(vaultInterior, "BasicPlayableInterior", false);
        Transform interiorGroup = FindOrCreateChild(vaultInterior, "BasicPlayableInterior");
        ClearChildren(interiorGroup);

        CreatePrimitiveVisual(interiorGroup, PrimitiveType.Cube, "BackWall",
            new Vector3(0f, 0f, -0.25f),
            Quaternion.identity,
            new Vector3(1.12f, 1.36f, 0.08f),
            new Color(0.08f, 0.08f, 0.1f),
            0f);

        CreatePrimitiveVisual(interiorGroup, PrimitiveType.Cube, "LeftWall",
            new Vector3(-0.54f, 0f, -0.08f),
            Quaternion.identity,
            new Vector3(0.08f, 1.34f, 0.42f),
            new Color(0.12f, 0.12f, 0.14f),
            0f);

        CreatePrimitiveVisual(interiorGroup, PrimitiveType.Cube, "RightWall",
            new Vector3(0.54f, 0f, -0.08f),
            Quaternion.identity,
            new Vector3(0.08f, 1.34f, 0.42f),
            new Color(0.12f, 0.12f, 0.14f),
            0f);

        CreatePrimitiveVisual(interiorGroup, PrimitiveType.Cube, "Ceiling",
            new Vector3(0f, 0.64f, -0.08f),
            Quaternion.identity,
            new Vector3(1.08f, 0.08f, 0.42f),
            new Color(0.12f, 0.12f, 0.14f),
            0f);

        CreatePrimitiveVisual(interiorGroup, PrimitiveType.Cube, "Floor",
            new Vector3(0f, -0.58f, -0.04f),
            Quaternion.identity,
            new Vector3(1.04f, 0.08f, 0.45f),
            new Color(0.24f, 0.22f, 0.18f),
            0f);

        Transform locker = FindOrCreateChild(interiorGroup, "KeyLocker");
        locker.localPosition = new Vector3(0f, 0.02f, -0.02f);
        locker.localRotation = Quaternion.identity;
        locker.localScale = Vector3.one;
        ClearChildren(locker);

        CreatePrimitiveVisual(locker, PrimitiveType.Cube, "CabinetBack",
            new Vector3(0f, 0f, -0.12f),
            Quaternion.identity,
            new Vector3(0.94f, 1.02f, 0.04f),
            new Color(0.17f, 0.17f, 0.19f),
            0f);

        CreatePrimitiveVisual(locker, PrimitiveType.Cube, "CabinetTop",
            new Vector3(0f, 0.49f, 0.02f),
            Quaternion.identity,
            new Vector3(0.94f, 0.04f, 0.28f),
            new Color(0.42f, 0.35f, 0.16f),
            0.01f);

        CreatePrimitiveVisual(locker, PrimitiveType.Cube, "CabinetBottom",
            new Vector3(0f, -0.49f, 0.02f),
            Quaternion.identity,
            new Vector3(0.94f, 0.04f, 0.28f),
            new Color(0.42f, 0.35f, 0.16f),
            0.01f);

        CreatePrimitiveVisual(locker, PrimitiveType.Cube, "CabinetLeft",
            new Vector3(-0.45f, 0f, 0.02f),
            Quaternion.identity,
            new Vector3(0.04f, 0.98f, 0.28f),
            new Color(0.42f, 0.35f, 0.16f),
            0.01f);

        CreatePrimitiveVisual(locker, PrimitiveType.Cube, "CabinetRight",
            new Vector3(0.45f, 0f, 0.02f),
            Quaternion.identity,
            new Vector3(0.04f, 0.98f, 0.28f),
            new Color(0.42f, 0.35f, 0.16f),
            0.01f);

        for (int i = 0; i < 3; i++)
        {
            float shelfY = 0.28f - (i * 0.28f);
            CreatePrimitiveVisual(locker, PrimitiveType.Cube, $"Shelf_{i}",
                new Vector3(0f, shelfY, 0.03f),
                Quaternion.identity,
                new Vector3(0.82f, 0.04f, 0.28f),
                new Color(0.56f, 0.47f, 0.18f),
                0.01f);
        }

        for (int i = 0; i < 2; i++)
        {
            float dividerX = i == 0 ? -0.27f : 0.27f;
            CreatePrimitiveVisual(locker, PrimitiveType.Cube, $"Divider_{i}",
                new Vector3(dividerX, 0f, 0.03f),
                Quaternion.identity,
                new Vector3(0.04f, 0.8f, 0.28f),
                new Color(0.56f, 0.47f, 0.18f),
                0.01f);
        }

        CreatePrimitiveVisual(locker, PrimitiveType.Cube, "HookRail",
            new Vector3(0f, 0.37f, 0.06f),
            Quaternion.identity,
            new Vector3(0.76f, 0.04f, 0.04f),
            new Color(0.70f, 0.60f, 0.26f),
            0.02f);

        for (int i = 0; i < 5; i++)
        {
            float hookX = -0.30f + (i * 0.15f);
            CreatePrimitiveVisual(locker, PrimitiveType.Cylinder, $"Hook_{i}",
                new Vector3(hookX, 0.31f, 0.08f),
                Quaternion.Euler(90f, 0f, 0f),
                new Vector3(0.022f, 0.04f, 0.022f),
                new Color(0.74f, 0.62f, 0.24f),
                0.01f);
        }

        CreatePrimitiveVisual(locker, PrimitiveType.Cube, "KeyTray",
            new Vector3(0.27f, -0.28f, 0.07f),
            Quaternion.identity,
            new Vector3(0.18f, 0.05f, 0.18f),
            new Color(0.74f, 0.62f, 0.24f),
            0.03f);

        CreatePrimitiveVisual(locker, PrimitiveType.Cube, "KeyTrayBack",
            new Vector3(0.27f, -0.28f, -0.01f),
            Quaternion.identity,
            new Vector3(0.18f, 0.16f, 0.04f),
            new Color(0.18f, 0.18f, 0.2f),
            0f);

        CreatePrimitiveVisual(locker, PrimitiveType.Cylinder, "KeyPlaceholderRing",
            new Vector3(0.27f, -0.23f, 0.11f),
            Quaternion.Euler(90f, 0f, 0f),
            new Vector3(0.05f, 0.01f, 0.05f),
            new Color(0.84f, 0.72f, 0.28f),
            0.02f);

        CreatePrimitiveVisual(locker, PrimitiveType.Cube, "KeyPlaceholderStem",
            new Vector3(0.27f, -0.28f, 0.11f),
            Quaternion.identity,
            new Vector3(0.02f, 0.12f, 0.01f),
            new Color(0.84f, 0.72f, 0.28f),
            0.02f);

        vaultInterior.gameObject.SetActive(false);

        VaultDoorRevealController vaultController = vaultDoorRootTransform.GetComponent<VaultDoorRevealController>();
        if (vaultController == null)
            vaultController = vaultDoorRootTransform.gameObject.AddComponent<VaultDoorRevealController>();

        vaultController.SetRevealMode(VaultDoorRevealController.RevealMode.SingleRotateDoor);
        vaultController.SetSingleDoor(singleDoor);
        vaultController.SetSlidingPanel(slidingPanel);
        vaultController.SetOpenAngle(-142f);
        vaultController.SetOpenDuration(2.35f);
        vaultController.SetOpenDelay(0.4f);
        vaultController.SetActivateOnOpen(new GameObject[] { vaultInterior.gameObject });
        vaultController.ResetToClosedState();
        return vaultController;
    }

    private void BuildBasicDoorLeaf(Transform doorTransform, string groupName)
    {
        SetSiblingChildrenActive(doorTransform, groupName, false);
        Transform group = FindOrCreateChild(doorTransform, groupName);
        ClearChildren(group);

        CreatePrimitiveVisual(group, PrimitiveType.Cube, "DoorSlab",
            Vector3.zero,
            Quaternion.identity,
            new Vector3(0.62f, 1.16f, 0.12f),
            new Color(0.28f, 0.28f, 0.3f),
            0.02f);

        CreatePrimitiveVisual(group, PrimitiveType.Cylinder, "DoorBoss",
            new Vector3(0f, 0f, 0.065f),
            Quaternion.Euler(90f, 0f, 0f),
            new Vector3(0.11f, 0.03f, 0.11f),
            new Color(0.74f, 0.62f, 0.24f),
            0.16f);
    }

    private void BuildBasicVaultDoor(Transform hingeTransform)
    {
        hingeTransform.localRotation = Quaternion.identity;
        hingeTransform.localScale = Vector3.one;

        SetSiblingChildrenActive(hingeTransform, "VaultDoorLeaf", false);
        Transform doorLeaf = FindOrCreateChild(hingeTransform, "VaultDoorLeaf");
        doorLeaf.localPosition = new Vector3(0.56f, 0f, 0.04f);
        doorLeaf.localRotation = Quaternion.identity;
        doorLeaf.localScale = Vector3.one;
        ClearChildren(doorLeaf);

        CreatePrimitiveVisual(doorLeaf, PrimitiveType.Cylinder, "DoorBackPlate",
            new Vector3(0f, 0f, -0.01f),
            Quaternion.Euler(90f, 0f, 0f),
            new Vector3(1.08f, 0.13f, 1.08f),
            new Color(0.18f, 0.18f, 0.20f),
            0f);

        CreatePrimitiveVisual(doorLeaf, PrimitiveType.Cylinder, "DoorRim",
            new Vector3(0f, 0f, 0.08f),
            Quaternion.Euler(90f, 0f, 0f),
            new Vector3(1.12f, 0.08f, 1.12f),
            new Color(0.72f, 0.62f, 0.28f),
            0.01f);

        CreatePrimitiveVisual(doorLeaf, PrimitiveType.Cylinder, "DoorFace",
            new Vector3(0f, 0f, 0.06f),
            Quaternion.Euler(90f, 0f, 0f),
            new Vector3(1.00f, 0.11f, 1.00f),
            new Color(0.76f, 0.76f, 0.72f),
            0f);

        CreatePrimitiveVisual(doorLeaf, PrimitiveType.Cylinder, "InnerRing",
            new Vector3(0f, 0f, 0.11f),
            Quaternion.Euler(90f, 0f, 0f),
            new Vector3(0.62f, 0.04f, 0.62f),
            new Color(0.68f, 0.58f, 0.26f),
            0.01f);

        CreatePrimitiveVisual(doorLeaf, PrimitiveType.Cylinder, "DoorHub",
            new Vector3(0f, 0f, 0.14f),
            Quaternion.Euler(90f, 0f, 0f),
            new Vector3(0.20f, 0.10f, 0.20f),
            new Color(0.58f, 0.50f, 0.24f),
            0.01f);

        CreatePrimitiveVisual(doorLeaf, PrimitiveType.Cube, "SpokeHorizontal",
            new Vector3(0f, 0f, 0.13f),
            Quaternion.identity,
            new Vector3(1.2f, 0.08f, 0.04f),
            new Color(0.58f, 0.50f, 0.24f),
            0f);

        CreatePrimitiveVisual(doorLeaf, PrimitiveType.Cube, "SpokeVertical",
            new Vector3(0f, 0f, 0.13f),
            Quaternion.identity,
            new Vector3(0.08f, 1.2f, 0.04f),
            new Color(0.58f, 0.50f, 0.24f),
            0f);

        CreatePrimitiveVisual(doorLeaf, PrimitiveType.Cube, "SpokeDiagonalA",
            new Vector3(0f, 0f, 0.125f),
            Quaternion.Euler(0f, 0f, 45f),
            new Vector3(0.86f, 0.06f, 0.035f),
            new Color(0.56f, 0.48f, 0.22f),
            0f);

        CreatePrimitiveVisual(doorLeaf, PrimitiveType.Cube, "SpokeDiagonalB",
            new Vector3(0f, 0f, 0.125f),
            Quaternion.Euler(0f, 0f, -45f),
            new Vector3(0.86f, 0.06f, 0.035f),
            new Color(0.56f, 0.48f, 0.22f),
            0f);

        for (int i = 0; i < 6; i++)
        {
            float angle = i * Mathf.PI * 2f / 6f;
            Vector3 boltPosition = new Vector3(Mathf.Cos(angle) * 0.76f, Mathf.Sin(angle) * 0.76f, 0.14f);
            CreatePrimitiveVisual(doorLeaf, PrimitiveType.Cylinder, $"Bolt_{i}",
                boltPosition,
                Quaternion.Euler(90f, 0f, 0f),
                new Vector3(0.06f, 0.04f, 0.06f),
                new Color(0.7f, 0.62f, 0.28f),
                0.01f);
        }

        for (int i = 0; i < 3; i++)
        {
            float hingeY = Mathf.Lerp(0.52f, -0.52f, i / 2f);
            CreatePrimitiveVisual(doorLeaf, PrimitiveType.Cube, $"HingeStrap_{i}",
                new Vector3(-0.49f, hingeY, 0.09f),
                Quaternion.identity,
                new Vector3(0.12f, 0.08f, 0.03f),
                new Color(0.54f, 0.46f, 0.20f),
                0f);
        }

        for (int i = 0; i < 3; i++)
        {
            float hingeY = Mathf.Lerp(0.52f, -0.52f, i / 2f);
            CreatePrimitiveVisual(hingeTransform, PrimitiveType.Cylinder, $"DoorHinge_{i}",
                new Vector3(0f, hingeY, 0.02f),
                Quaternion.identity,
                new Vector3(0.05f, 0.14f, 0.05f),
                new Color(0.58f, 0.50f, 0.24f),
                0f);
        }
    }

    private void StartSolvedRevealSequence()
    {
        if (solvedRevealRoutine != null || mechanismDoorOpened)
            return;

        if (!openMechanismDoorOnSolve || runtimeMechanismDoorPivot == null)
        {
            OpenRuntimeVaultIfAvailable();
            return;
        }

        solvedRevealRoutine = StartCoroutine(SolvedRevealRoutine());
    }

    private IEnumerator SolvedRevealRoutine()
    {
        if (runtimeMechanismDoorPivot == null)
        {
            OpenRuntimeVaultIfAvailable();
            solvedRevealRoutine = null;
            yield break;
        }

        Quaternion closedRotation = runtimeMechanismDoorPivot.localRotation;
        Quaternion targetRotation = closedRotation * Quaternion.Euler(0f, mechanismDoorOpenAngle, 0f);
        bool vaultTriggered = false;
        float elapsed = 0f;

        while (elapsed < mechanismDoorOpenDuration)
        {
            elapsed += Time.deltaTime;
            float normalized = Mathf.Clamp01(elapsed / Mathf.Max(0.1f, mechanismDoorOpenDuration));
            float eased = mechanismDoorOpenCurve != null ? mechanismDoorOpenCurve.Evaluate(normalized) : normalized;
            runtimeMechanismDoorPivot.localRotation = Quaternion.Slerp(closedRotation, targetRotation, eased);

            if (!vaultTriggered && normalized >= vaultOpenTriggerNormalized)
            {
                OpenRuntimeVaultIfAvailable();
                vaultTriggered = true;
            }

            yield return null;
        }

        runtimeMechanismDoorPivot.localRotation = targetRotation;
        mechanismDoorOpened = true;

        if (!vaultTriggered)
            OpenRuntimeVaultIfAvailable();

        solvedRevealRoutine = null;
    }

    private void OpenRuntimeVaultIfAvailable()
    {
        if (runtimeVaultDoorController == null)
        {
            Transform vaultRoot = transform.Find("VaultDoorRoot");
            if (vaultRoot != null)
                runtimeVaultDoorController = vaultRoot.GetComponent<VaultDoorRevealController>();
        }

        if (runtimeVaultDoorController != null)
            runtimeVaultDoorController.OpenVault();
    }

    private void ReparentKeepingWorld(Transform child, Transform newParent)
    {
        if (child == null || newParent == null || child == newParent)
            return;

        if (child.parent == newParent)
            return;

        child.SetParent(newParent, true);
    }

    private Transform FindOrCreateChild(Transform parent, string name)
    {
        Transform child = parent.Find(name);
        if (child != null)
            return child;

        GameObject childObject = new GameObject(name);
        childObject.transform.SetParent(parent, false);
        return childObject.transform;
    }

    private void ClearChildren(Transform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
            Destroy(parent.GetChild(i).gameObject);
    }

    private void SetSiblingChildrenActive(Transform parent, string keepName, bool activeState)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child.name == keepName)
                continue;

            child.gameObject.SetActive(activeState);
        }
    }

    private GameObject CreatePrimitiveVisual(
        Transform parent,
        PrimitiveType primitiveType,
        string objectName,
        Vector3 localPosition,
        Quaternion localRotation,
        Vector3 localScale,
        Color color,
        float emission)
    {
        GameObject visual = GameObject.CreatePrimitive(primitiveType);
        visual.name = objectName;
        visual.transform.SetParent(parent, false);
        visual.transform.localPosition = localPosition;
        visual.transform.localRotation = localRotation;
        visual.transform.localScale = localScale;

        Collider collider = visual.GetComponent<Collider>();
        if (collider != null)
            Destroy(collider);

        Renderer renderer = visual.GetComponent<Renderer>();
        if (renderer != null)
            renderer.sharedMaterial = CreateRuntimeMaterial($"{name}_{objectName}_Mat", color, emission);

        return visual;
    }

    private Material CreateRuntimeMaterial(string materialName, Color baseColor, float emission)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");
        if (shader == null)
            shader = Shader.Find("Diffuse");

        Material material = new Material(shader);
        material.name = materialName;

        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", baseColor);
        if (material.HasProperty("_Color"))
            material.SetColor("_Color", baseColor);

        if (emission > 0f)
        {
            Color emissionColor = baseColor * emission * 0.025f;
            if (material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", emissionColor);
            }
        }

        return material;
    }

    private void TryAutoMoveNearPlayerIfNeeded()
    {
        if (!autoMovePuzzleNearPlayerIfTooFar)
            return;

        RohitFPSController player = FindFirstObjectByType<RohitFPSController>();
        if (player == null)
            return;

        Vector3 referencePoint = interactionPoint != null ? interactionPoint.position : transform.position;
        if (Vector3.Distance(referencePoint, player.transform.position) <= autoMoveDistanceThreshold)
            return;

        Vector3 forward = player.transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.001f)
            forward = Vector3.forward;

        forward.Normalize();

        Vector3 targetPosition = player.transform.position + forward * autoMoveForwardOffset;
        targetPosition.y += autoMoveVerticalOffset;

        transform.position = targetPosition;
        transform.rotation = Quaternion.LookRotation(-forward, Vector3.up);
    }

    private void EnsurePuzzleFillLight(Transform root)
    {
        Transform lightTransform = FindOrCreateChild(root, "PuzzleFillLight");
        lightTransform.localPosition = new Vector3(0f, 1.65f, 1.15f);
        lightTransform.localRotation = Quaternion.LookRotation(Vector3.back, Vector3.up);

        Light fill = lightTransform.GetComponent<Light>();
        if (fill == null)
            fill = lightTransform.gameObject.AddComponent<Light>();

        fill.type = LightType.Directional;
        fill.color = new Color(1f, 0.92f, 0.82f);
        fill.intensity = 0.85f;
        fill.range = 1f;
        fill.spotAngle = 30f;
        fill.shadows = LightShadows.None;
        fill.enabled = false;
        puzzleFillLight = fill;
    }

    private void SetPuzzleLightingState(bool inPuzzle)
    {
        if (puzzleFillLight == null)
        {
            Transform fillTransform = transform.Find("PuzzleFillLight");
            if (fillTransform != null)
                puzzleFillLight = fillTransform.GetComponent<Light>();
        }

        if (activeCamera != null)
        {
            if (inPuzzle)
            {
                cachedCameraLights = activeCamera.GetComponentsInChildren<Light>(true);
                cachedCameraLightIntensities = cachedCameraLights != null
                    ? new float[cachedCameraLights.Length]
                    : null;

                if (cachedCameraLights != null)
                {
                    for (int i = 0; i < cachedCameraLights.Length; i++)
                    {
                        if (cachedCameraLights[i] == null)
                            continue;

                        cachedCameraLightIntensities[i] = cachedCameraLights[i].intensity;
                        cachedCameraLights[i].intensity = 0f;
                    }
                }
            }
            else if (cachedCameraLights != null && cachedCameraLightIntensities != null)
            {
                for (int i = 0; i < cachedCameraLights.Length; i++)
                {
                    if (cachedCameraLights[i] != null)
                        cachedCameraLights[i].intensity = cachedCameraLightIntensities[i];
                }

                cachedCameraLights = null;
                cachedCameraLightIntensities = null;
            }
        }

        if (activePlayer != null)
        {
            if (inPuzzle)
            {
                cachedPlayerTorch = activePlayer.GetComponent<PlayerTorch>();
                if (cachedPlayerTorch != null && cachedPlayerTorch.torchLight != null)
                {
                    cachedTorchWasEnabled = cachedPlayerTorch.torchLight.enabled;
                    cachedPlayerTorch.torchLight.enabled = false;
                }
            }
            else if (cachedPlayerTorch != null && cachedPlayerTorch.torchLight != null)
            {
                cachedPlayerTorch.torchLight.enabled = cachedTorchWasEnabled;
                cachedPlayerTorch = null;
                cachedTorchWasEnabled = false;
            }
        }

        if (puzzleFillLight != null)
            puzzleFillLight.enabled = inPuzzle;
    }

    private void EnsurePlayablePuzzleState(Transform dial1Pivot, Transform dial2Pivot, Transform dial3Pivot)
    {
        if (puzzleManager == null)
            return;

        SacredGlyphDial dial1 = dial1Pivot != null ? dial1Pivot.GetComponent<SacredGlyphDial>() : null;
        SacredGlyphDial dial2 = dial2Pivot != null ? dial2Pivot.GetComponent<SacredGlyphDial>() : null;
        SacredGlyphDial dial3 = dial3Pivot != null ? dial3Pivot.GetComponent<SacredGlyphDial>() : null;

        if (dial1 == null || dial2 == null || dial3 == null)
            return;

        if (!puzzleManager.HasAllDialReferences)
            puzzleManager.SetDialReferences(dial1, dial2, dial3);

        if (!puzzleManager.AreCurrentStepsSolved())
            return;

        dial1.SetStepInstantly((puzzleManager.CorrectDial1Step + 1) % dial1.TotalSteps);
        dial2.SetStepInstantly(puzzleManager.CorrectDial2Step);
        dial3.SetStepInstantly(puzzleManager.CorrectDial3Step);
        puzzleManager.ResetSolvedState();
    }

#if UNITY_EDITOR
    private void QueueEditorVisualRepair()
    {
        if (EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        Transform root = transform.root;
        if (root == null || !NeedsEditorVisualRepair(root))
            return;

        string sessionKey = $"SacredGlyphRepairQueued_{root.GetInstanceID()}";
        if (SessionState.GetBool(sessionKey, false))
            return;

        SessionState.SetBool(sessionKey, true);
        EditorApplication.delayCall += () =>
        {
            SessionState.SetBool(sessionKey, false);

            if (this == null)
                return;

            Transform currentRoot = transform.root;
            if (currentRoot == null || !NeedsEditorVisualRepair(currentRoot))
                return;

            InvokeEditorAutoSetup(currentRoot.gameObject);
        };
    }

    private static bool NeedsEditorVisualRepair(Transform root)
    {
        if (root == null)
            return false;

        Transform paintingVisual = root.Find("PaintingVisual");
        Transform dial1Mesh = root.Find("Dial1Pivot/Dial1Mesh");

        if (paintingVisual == null || dial1Mesh == null)
            return false;

        Transform paintingGenerated = paintingVisual.Find("GeneratedDefault");
        Transform dialGenerated = dial1Mesh.Find("GeneratedDefault");

        if (paintingGenerated == null || dialGenerated == null)
            return true;

        if (paintingGenerated.Find("CanvasArt") == null)
            return true;

        if (dialGenerated.Find("SecondaryRim") == null)
            return true;

        return false;
    }

    private static void InvokeEditorAutoSetup(GameObject rootObject)
    {
        if (rootObject == null)
            return;

        System.Type editorType = null;
        Assembly[] assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
        for (int i = 0; i < assemblies.Length; i++)
        {
            editorType = assemblies[i].GetType("SacredGlyphPuzzleAutoSetupEditor");
            if (editorType != null)
                break;
        }

        if (editorType == null)
            return;

        MethodInfo runMethod = editorType.GetMethod("Run", BindingFlags.Public | BindingFlags.Static);
        if (runMethod == null)
            return;

        runMethod.Invoke(null, new object[] { rootObject, false });
    }
#endif
}
