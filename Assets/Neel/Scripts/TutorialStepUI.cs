using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class TutorialStepUI : MonoBehaviour
{
    const string MovementLine = "Use WASD to move around.";
    const string InteractionLine = "Try interacting with something that stands out.";
    const string HidingLine = "You can hide in some objects, try finding a few around";
    const string DoorLine = "Great, let's move on to the next level by interacting with the door.";
    const string LevelSelectSeenKey = "level_select_intro_seen_v1";
    const string NewTutorial3Line1 = "This is a Weeping Angel";
    const string NewTutorial3Line2 = "Keep looking in it's eyes, it can't move as long as you're looking at it.";
    const string NewTutorial3Line3 = "Beware, it moves fast when you look away";

    [Header("Step Text")]
    [TextArea] public string movementHint = MovementLine;
    [TextArea] public string interactionHint = InteractionLine;
    [TextArea] public string hidingHint = HidingLine;
    [TextArea] public string doorHint = DoorLine;

    [Header("Visuals")]
    public int sortingOrder = 200;
    public Color textColor = new Color(1f, 0.94f, 0.72f, 1f);
    public Color completedTextColor = new Color(0.4f, 1f, 0.45f, 1f);
    public Color backgroundColor = new Color(0f, 0f, 0f, 0.42f);
    public float completedFlashDuration = 0.5f;

    private Text instructionText;
    private CanvasGroup canvasGroup;
    private RohitFPSController player;

    private enum TutorialStep
    {
        Movement = 0,
        Interaction = 1,
        Hiding = 2,
        DoorAndKey = 3,
        Complete = 4
    }

    private TutorialStep currentStep;
    private TutorialStep pendingStep;
    private bool hideEntered;
    private bool hideExited;
    private bool transitioning;
    private float transitionTimer;
    private bool isLevelSelectScene;
    private bool isNewTutorial1Scene;
    private bool isNewTutorial3Scene;
    private bool useScene3TimedSequence;

    private void OnEnable()
    {
        RohitFPSController.OnPrimaryInteraction += HandlePrimaryInteraction;
        RohitFPSController.OnHideEntered += HandleHideEntered;
        RohitFPSController.OnHideExited += HandleHideExited;
    }

    private void OnDisable()
    {
        RohitFPSController.OnPrimaryInteraction -= HandlePrimaryInteraction;
        RohitFPSController.OnHideEntered -= HandleHideEntered;
        RohitFPSController.OnHideExited -= HandleHideExited;
    }

    private void Start()
    {
        string activeSceneName = SceneManager.GetActiveScene().name;
        isLevelSelectScene = activeSceneName == "Level Select";
        isNewTutorial1Scene = activeSceneName == "New Tutorial 1";
        isNewTutorial3Scene = activeSceneName == "New Tutorial 3";
        if (isLevelSelectScene && ShouldSkipLevelSelectPrompt())
        {
            enabled = false;
            return;
        }

        // Force canonical tutorial copy so stale scene overrides cannot change top-banner text.
        movementHint = MovementLine;
        interactionHint = isLevelSelectScene ? "Try interacting with one of the Doors." : InteractionLine;
        hidingHint = HidingLine;
        doorHint = DoorLine;

        if (isNewTutorial3Scene)
        {
            movementHint = NewTutorial3Line1;
            interactionHint = NewTutorial3Line2;
            hidingHint = NewTutorial3Line3;
        }

        BuildUI();
        player = FindFirstObjectByType<RohitFPSController>();
        currentStep = isNewTutorial1Scene ? TutorialStep.Interaction : TutorialStep.Movement;
        ApplyStepText();
        canvasGroup.alpha = 1f;

        if (isLevelSelectScene)
            PersistLevelSelectSeen();

        if (isNewTutorial3Scene)
        {
            useScene3TimedSequence = true;
            StartCoroutine(PlayNewTutorial3Sequence());
        }
    }

    private void Update()
    {
        if (useScene3TimedSequence)
            return;

        if (transitioning)
        {
            transitionTimer -= Time.deltaTime;
            if (transitionTimer <= 0f)
            {
                transitioning = false;
                instructionText.color = textColor;
                currentStep = pendingStep;
                ApplyStepText();
            }
            return;
        }

        if (currentStep == TutorialStep.Complete)
            return;

        if (player == null)
        {
            player = FindFirstObjectByType<RohitFPSController>();
            if (player == null)
                return;
        }

        switch (currentStep)
        {
            case TutorialStep.Movement:
                if (IsAnyMovementPressed())
                    AdvanceTo(TutorialStep.Interaction);
                break;

            case TutorialStep.Hiding:
                if (!isLevelSelectScene && hideEntered && hideExited)
                    AdvanceTo(TutorialStep.DoorAndKey);
                break;
        }
    }

    public void MarkDoorAndKeyComplete()
    {
        AdvanceTo(TutorialStep.Complete);
    }

    private void HandlePrimaryInteraction(RohitFPSController source, IInteractable interactable)
    {
        if (useScene3TimedSequence)
            return;

        if (source == null || source != player)
            return;

        if (currentStep == TutorialStep.Interaction)
        {
            if (isLevelSelectScene)
                AdvanceTo(TutorialStep.Complete);
            else
                AdvanceTo(TutorialStep.Hiding);
        }
    }

    private void HandleHideEntered(RohitFPSController source, HideableObject hideable)
    {
        if (useScene3TimedSequence)
            return;

        if (source == null || source != player)
            return;

        if (currentStep == TutorialStep.Hiding)
            hideEntered = true;
    }

    private void HandleHideExited(RohitFPSController source, HideableObject hideable)
    {
        if (useScene3TimedSequence)
            return;

        if (source == null || source != player)
            return;

        if (currentStep == TutorialStep.Hiding && hideEntered)
            hideExited = true;
    }

    private void AdvanceTo(TutorialStep step)
    {
        if (step == currentStep || transitioning)
            return;
        
        pendingStep = step;
        transitioning = true;
        transitionTimer = Mathf.Max(0.05f, completedFlashDuration);
        if (instructionText != null)
            instructionText.color = completedTextColor;
    }

    private void ApplyStepText()
    {
        if (instructionText == null)
            return;

        switch (currentStep)
        {
            case TutorialStep.Movement:
                instructionText.text = movementHint;
                break;
            case TutorialStep.Interaction:
                instructionText.text = interactionHint;
                break;
            case TutorialStep.Hiding:
                instructionText.text = isLevelSelectScene ? string.Empty : hidingHint;
                break;
            case TutorialStep.DoorAndKey:
                instructionText.text = isLevelSelectScene ? string.Empty : doorHint;
                break;
            default:
                instructionText.text = string.Empty;
                canvasGroup.alpha = 0f;
                break;
        }
    }

    private bool ShouldSkipLevelSelectPrompt()
    {
        return PlayerPrefs.GetInt(LevelSelectSeenKey, 0) == 1;
    }

    private System.Collections.IEnumerator PlayNewTutorial3Sequence()
    {
        currentStep = TutorialStep.Movement;
        ApplyStepText();
        canvasGroup.alpha = 1f;
        yield return new WaitForSeconds(4.5f);

        currentStep = TutorialStep.Interaction;
        ApplyStepText();
        canvasGroup.alpha = 1f;
        yield return new WaitForSeconds(5.5f);

        currentStep = TutorialStep.Hiding;
        ApplyStepText();
        canvasGroup.alpha = 1f;
        yield return new WaitForSeconds(5f);

        currentStep = TutorialStep.Complete;
        ApplyStepText();
    }

    private void PersistLevelSelectSeen()
    {
        PlayerPrefs.SetInt(LevelSelectSeenKey, 1);
        PlayerPrefs.Save();
    }

    private bool IsAnyMovementPressed()
    {
        bool pressed = false;
#if ENABLE_LEGACY_INPUT_MANAGER
        pressed |= Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.D);
#endif
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            pressed |= Keyboard.current.wKey.wasPressedThisFrame;
            pressed |= Keyboard.current.aKey.wasPressedThisFrame;
            pressed |= Keyboard.current.sKey.wasPressedThisFrame;
            pressed |= Keyboard.current.dKey.wasPressedThisFrame;
        }
#endif
        return pressed;
    }

    private void BuildUI()
    {
        GameObject canvasObj = new GameObject("TutorialStepCanvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        canvasGroup = canvasObj.AddComponent<CanvasGroup>();

        GameObject bgObj = new GameObject("Background");
        bgObj.transform.SetParent(canvasObj.transform, false);
        Image bg = bgObj.AddComponent<Image>();
        bg.color = backgroundColor;

        RectTransform bgRect = bg.rectTransform;
        bgRect.anchorMin = new Vector2(0.22f, 0.84f);
        bgRect.anchorMax = new Vector2(0.78f, 0.94f);
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;

        GameObject textObj = new GameObject("InstructionText");
        textObj.transform.SetParent(canvasObj.transform, false);
        instructionText = textObj.AddComponent<Text>();
        instructionText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        instructionText.fontSize = 36;
        instructionText.alignment = TextAnchor.MiddleCenter;
        instructionText.color = textColor;
        instructionText.resizeTextForBestFit = true;
        instructionText.resizeTextMinSize = 18;
        instructionText.resizeTextMaxSize = 36;

        RectTransform textRect = instructionText.rectTransform;
        textRect.anchorMin = new Vector2(0.24f, 0.85f);
        textRect.anchorMax = new Vector2(0.76f, 0.93f);
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        Shadow shadow = textObj.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.85f);
        shadow.effectDistance = new Vector2(2f, -2f);
    }
}
