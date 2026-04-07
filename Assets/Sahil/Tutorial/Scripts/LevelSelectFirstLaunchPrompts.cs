using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class LevelSelectFirstLaunchPrompts : MonoBehaviour
{
    const string SeenKey = "level_select_intro_seen_v1";

    [Header("Prompt Text")]
    [TextArea] public string movementHint = "Use WASD to move around.";
    [TextArea] public string interactionHint = "Try interacting with one of the Doors.";

    [Header("Visuals")]
    public int sortingOrder = 200;
    public Color textColor = new Color(1f, 0.94f, 0.72f, 1f);
    public Color completedTextColor = new Color(0.4f, 1f, 0.45f, 1f);
    public Color backgroundColor = new Color(0f, 0f, 0f, 0.42f);
    public float completedFlashDuration = 0.5f;

    private enum Step
    {
        Movement = 0,
        InteractDoor = 1,
        Complete = 2
    }

    private Step currentStep;
    private Step pendingStep;
    private bool transitioning;
    private float transitionTimer;

    private Text instructionText;
    private CanvasGroup canvasGroup;
    private RohitFPSController player;

    private void OnEnable()
    {
        RohitFPSController.OnPrimaryInteraction += HandlePrimaryInteraction;
    }

    private void OnDisable()
    {
        RohitFPSController.OnPrimaryInteraction -= HandlePrimaryInteraction;
    }

    private void Start()
    {
        if (ShouldSkipForWebGLRepeat())
        {
            enabled = false;
            return;
        }

        BuildUI();
        player = FindFirstObjectByType<RohitFPSController>();
        currentStep = Step.Movement;
        ApplyStepText();
        canvasGroup.alpha = 1f;
    }

    private void Update()
    {
        if (instructionText == null || canvasGroup == null)
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

        if (currentStep == Step.Complete)
            return;

        if (player == null)
            player = FindFirstObjectByType<RohitFPSController>();

        if (currentStep == Step.Movement && IsAnyMovementPressed())
            AdvanceTo(Step.InteractDoor);
    }

    void HandlePrimaryInteraction(RohitFPSController source, IInteractable interactable)
    {
        if (source == null || source != player)
            return;

        if (currentStep != Step.InteractDoor)
            return;

        if (interactable is LevelSelectDoorTransition)
            AdvanceTo(Step.Complete);
    }

    void AdvanceTo(Step nextStep)
    {
        if (nextStep == currentStep || transitioning)
            return;

        pendingStep = nextStep;
        transitioning = true;
        transitionTimer = Mathf.Max(0.05f, completedFlashDuration);
        instructionText.color = completedTextColor;

        if (nextStep == Step.Complete)
            PersistSeenFlagForWebGL();
    }

    void ApplyStepText()
    {
        switch (currentStep)
        {
            case Step.Movement:
                instructionText.text = movementHint;
                break;
            case Step.InteractDoor:
                instructionText.text = interactionHint;
                break;
            default:
                instructionText.text = string.Empty;
                canvasGroup.alpha = 0f;
                break;
        }
    }

    bool ShouldSkipForWebGLRepeat()
    {
        if (Application.platform != RuntimePlatform.WebGLPlayer)
            return false;

        return PlayerPrefs.GetInt(SeenKey, 0) == 1;
    }

    void PersistSeenFlagForWebGL()
    {
        if (Application.platform != RuntimePlatform.WebGLPlayer)
            return;

        PlayerPrefs.SetInt(SeenKey, 1);
        PlayerPrefs.Save();
    }

    bool IsAnyMovementPressed()
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

    void BuildUI()
    {
        GameObject canvasObj = new GameObject("LevelSelectPromptCanvas");
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
