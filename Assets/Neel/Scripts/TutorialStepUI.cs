using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using Sushil.Systems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class TutorialStepUI : MonoBehaviour
{
    const string MovementLine = "Use WASD to move around.";
    const string JumpLine = "Press Space to jump.";
    const string InteractionLine = "Try interacting with something that stands out.";
    const string HidingLine = "You can hide in some objects. Try finding a few around the room.";
    const string DoorLine = "Great. Let's move on to the next level by interacting with the door.";
    const string LevelSelectSeenKey = "level_select_intro_seen_v1";
    const string NewTutorial3Line1 = "This is a Weeping Angel.";
    const string NewTutorial3Line2 = "Keep looking into its eyes. It can't move as long as you're looking at it.";
    const string NewTutorial3Line3 = "You cannot hide from a Weeping Angel.";

    // Tutorial 2 — fuse flow (no movement step; movement was already taught in Tutorial 1).
    const string Tutorial2FusePickupLine = "Find the fuse hidden in this room and pick it up.";
    const string Tutorial2FuseInsertLine = "Now insert the fuse into the fuse box to restore power.";
    const string Tutorial2FuseDoorLine   = "Power is back. Interact with the door to continue.";
    static bool levelSelectPromptShownThisRuntime;

    [Header("Step Text")]
    [TextArea] public string movementHint = MovementLine;
    [TextArea] public string jumpHint = JumpLine;
    [TextArea] public string interactionHint = InteractionLine;
    [TextArea] public string hidingHint = HidingLine;
    [TextArea] public string doorHint = DoorLine;

    [Header("Visuals")]
    public int sortingOrder = 200;
    public Color textColor = Color.white;                                  // pure white when active
    public Color completedTextColor = new Color(0.35f, 1f, 0.45f, 1f);     // bright green flash on completion
    public Color backgroundColor = new Color(0f, 0f, 0f, 0.42f);

    [Header("Transition Timing")]
    [Tooltip("How long the prompt holds in green after completion before fading out.")]
    public float completedGlowDuration = 0.45f;
    [Tooltip("Fade-out duration after the green glow hold.")]
    public float fadeOutDuration = 0.35f;
    [Tooltip("Fade-in duration when the next prompt appears.")]
    public float fadeInDuration = 0.30f;

    private Text instructionText;
    private Outline textOutline;
    private CanvasGroup canvasGroup;
    private RohitFPSController player;

    private enum TutorialStep
    {
        Movement = 0,
        Jump = 1,
        Interaction = 2,
        Hiding = 3,
        DoorAndKey = 4,
        Complete = 5
    }

    private enum TransitionPhase
    {
        Idle,        // active prompt, waiting for player action
        Glow,        // task completed — text turned green, holding visible
        FadeOut,     // green text fading to alpha 0
        Swap,        // single-frame: switch to next prompt text + reset to white
        FadeIn       // new prompt fading in (white)
    }

    private TutorialStep currentStep;
    private TutorialStep pendingStep;
    private TransitionPhase phase = TransitionPhase.Idle;
    private float phaseTimer;
    private bool hideEntered;
    private bool hideExited;
    private bool isLevelSelectScene;
    private bool isNewTutorial1Scene;
    private bool isNewTutorial2Scene;
    private bool isWeepingAngelScene;     // detected by presence of WeepingAngelAI in the scene
    private bool useTimedSequence;
    private int tutorial2InteractionCount;

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
        isNewTutorial2Scene = activeSceneName == "New Tutorial 2";
        // Detect the Weeping Angel scene by checking for the actual component, not by
        // scene-name guessing (the scene was originally Tutorial 3 but is now Tutorial 4).
        isWeepingAngelScene = FindFirstObjectByType<WeepingAngelAI>() != null;
        if (isLevelSelectScene && ShouldSkipLevelSelectPrompt())
        {
            enabled = false;
            return;
        }

        // Force canonical tutorial copy so stale scene/inspector overrides cannot leave
        // outdated text on prefab-saved fields (e.g. the old "Press J to jump.").
        movementHint = MovementLine;
        jumpHint = JumpLine;
        interactionHint = isLevelSelectScene ? "Try interacting with one of the doors." : InteractionLine;
        hidingHint = HidingLine;
        doorHint = DoorLine;

        if (isNewTutorial2Scene)
        {
            // Tutorial 2 is the fuse tutorial: pick up fuse → insert into box → open door.
            interactionHint = Tutorial2FusePickupLine;   // step shown first
            hidingHint      = Tutorial2FuseInsertLine;   // reused as step 2 (no actual hiding)
            doorHint        = Tutorial2FuseDoorLine;     // step 3
        }

        if (isWeepingAngelScene)
        {
            movementHint = NewTutorial3Line1;            // "This is a Weeping Angel."
            interactionHint = NewTutorial3Line2;
            hidingHint = NewTutorial3Line3;
        }

        BuildUI();
        player = FindFirstObjectByType<RohitFPSController>();
        // Tutorial 2 (fuse) skips Movement+Jump and starts on fuse pickup.
        // All other scenes start on Movement → Jump → Interaction → ...
        currentStep = isNewTutorial2Scene ? TutorialStep.Interaction : TutorialStep.Movement;
        ApplyStepText();
        SetActiveAppearance();          // start in white at full alpha
        canvasGroup.alpha = 1f;

        if (isLevelSelectScene)
            PersistLevelSelectSeen();

        if (isWeepingAngelScene)
        {
            useTimedSequence = true;
            StartCoroutine(PlayWeepingAngelSequence());
        }
    }

    private void Update()
    {
        if (PauseOverlay.IsPaused || StartScreenOverlay.IsShowing || GameOverOverlay.IsShowing || EscapeOverlay.IsShowing)
            return;

        TickTransition();

        if (useTimedSequence) return;
        if (phase != TransitionPhase.Idle) return;
        if (currentStep == TutorialStep.Complete) return;

        if (player == null)
        {
            player = FindFirstObjectByType<RohitFPSController>();
            if (player == null) return;
        }

        switch (currentStep)
        {
            case TutorialStep.Movement:
                if (IsAnyMovementPressed())
                    // Level Select skips the jump tutorial — it's just a hub area.
                    AdvanceTo(isLevelSelectScene ? TutorialStep.Interaction : TutorialStep.Jump);
                break;

            case TutorialStep.Jump:
                if (IsJumpPressed())
                    AdvanceTo(TutorialStep.Interaction);
                break;

            case TutorialStep.Hiding:
                // Tutorial 2 reuses the Hiding slot as "insert fuse" — driven by
                // HandlePrimaryInteraction, not hide events.
                if (!isLevelSelectScene && !isNewTutorial2Scene && hideEntered && hideExited)
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
        if (useTimedSequence) return;
        if (source == null || source != player) return;
        if (phase != TransitionPhase.Idle) return;

        // Tutorial 2 fuse flow: each interaction advances to the next prompt.
        // Step 1 (Interaction): pick up the fuse → step 2 (Hiding slot reused for "insert fuse")
        // Step 2 (Hiding): insert fuse into box → step 3 (DoorAndKey: open the door)
        // Step 3 (DoorAndKey): open the door → Complete
        if (isNewTutorial2Scene)
        {
            switch (currentStep)
            {
                case TutorialStep.Interaction: AdvanceTo(TutorialStep.Hiding); return;
                case TutorialStep.Hiding:      AdvanceTo(TutorialStep.DoorAndKey); return;
                case TutorialStep.DoorAndKey:  AdvanceTo(TutorialStep.Complete); return;
            }
            return;
        }

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
        if (useTimedSequence) return;
        if (source == null || source != player) return;
        if (currentStep == TutorialStep.Hiding) hideEntered = true;
    }

    private void HandleHideExited(RohitFPSController source, HideableObject hideable)
    {
        if (useTimedSequence) return;
        if (source == null || source != player) return;
        if (currentStep == TutorialStep.Hiding && hideEntered) hideExited = true;
    }

    private void AdvanceTo(TutorialStep step)
    {
        if (step == currentStep) return;
        if (phase != TransitionPhase.Idle) return;

        pendingStep = step;
        phase = TransitionPhase.Glow;
        phaseTimer = Mathf.Max(0.05f, completedGlowDuration);
        SetCompletedAppearance();
    }

    // Drives the Glow → FadeOut → Swap → FadeIn animation.
    private void TickTransition()
    {
        if (phase == TransitionPhase.Idle) return;
        phaseTimer -= Time.unscaledDeltaTime;

        switch (phase)
        {
            case TransitionPhase.Glow:
                // Subtle pulse on the green so it reads as a confirmation glow.
                if (instructionText != null)
                {
                    float pulse = 0.85f + 0.15f * Mathf.Sin(Time.unscaledTime * 9f);
                    instructionText.color = new Color(
                        completedTextColor.r * pulse,
                        completedTextColor.g,
                        completedTextColor.b * pulse,
                        1f);
                }
                if (phaseTimer <= 0f)
                {
                    phase = TransitionPhase.FadeOut;
                    phaseTimer = Mathf.Max(0.05f, fadeOutDuration);
                }
                break;

            case TransitionPhase.FadeOut:
                {
                    float t = 1f - Mathf.Clamp01(phaseTimer / Mathf.Max(0.05f, fadeOutDuration));
                    if (canvasGroup != null) canvasGroup.alpha = 1f - t;
                    if (phaseTimer <= 0f)
                    {
                        if (canvasGroup != null) canvasGroup.alpha = 0f;
                        phase = TransitionPhase.Swap;
                    }
                }
                break;

            case TransitionPhase.Swap:
                currentStep = pendingStep;
                ApplyStepText();
                SetActiveAppearance();
                if (currentStep == TutorialStep.Complete)
                {
                    // Final step: stay hidden, no fade-in.
                    if (canvasGroup != null) canvasGroup.alpha = 0f;
                    phase = TransitionPhase.Idle;
                }
                else
                {
                    phase = TransitionPhase.FadeIn;
                    phaseTimer = Mathf.Max(0.05f, fadeInDuration);
                }
                break;

            case TransitionPhase.FadeIn:
                {
                    float t = 1f - Mathf.Clamp01(phaseTimer / Mathf.Max(0.05f, fadeInDuration));
                    if (canvasGroup != null) canvasGroup.alpha = t;
                    if (phaseTimer <= 0f)
                    {
                        if (canvasGroup != null) canvasGroup.alpha = 1f;
                        phase = TransitionPhase.Idle;
                    }
                }
                break;
        }
    }

    private void SetActiveAppearance()
    {
        if (instructionText != null) instructionText.color = textColor;
        if (textOutline != null)
            textOutline.effectColor = new Color(0f, 0f, 0f, 0.85f);
    }

    private void SetCompletedAppearance()
    {
        if (instructionText != null) instructionText.color = completedTextColor;
        if (textOutline != null)
            textOutline.effectColor = new Color(0.05f, 0.6f, 0.15f, 0.95f); // green outer glow
    }

    private void ApplyStepText()
    {
        if (instructionText == null) return;

        switch (currentStep)
        {
            case TutorialStep.Movement:
                instructionText.text = movementHint;
                break;
            case TutorialStep.Jump:
                instructionText.text = jumpHint;
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
                break;
        }
    }

    private bool ShouldSkipLevelSelectPrompt()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        return levelSelectPromptShownThisRuntime;
#else
        return PlayerPrefs.GetInt(LevelSelectSeenKey, 0) == 1;
#endif
    }

    // Tutorial 3 (Weeping Angel) is informational, so it auto-advances on a timer
    // but uses the same green-glow + fade transition between lines for consistency.
    private System.Collections.IEnumerator PlayWeepingAngelSequence()
    {
        currentStep = TutorialStep.Movement;
        ApplyStepText();
        SetActiveAppearance();
        canvasGroup.alpha = 1f;
        yield return WaitThenTransition(4.5f, TutorialStep.Interaction);
        yield return WaitThenTransition(5.5f, TutorialStep.Hiding);
        yield return WaitThenTransition(5.0f, TutorialStep.Complete);
    }

    private System.Collections.IEnumerator WaitThenTransition(float holdSeconds, TutorialStep next)
    {
        yield return new WaitForSeconds(holdSeconds);

        // Glow green
        SetCompletedAppearance();
        yield return new WaitForSeconds(Mathf.Max(0.05f, completedGlowDuration));

        // Fade out
        float t = 0f;
        float fadeOut = Mathf.Max(0.05f, fadeOutDuration);
        while (t < fadeOut)
        {
            t += Time.unscaledDeltaTime;
            if (canvasGroup != null) canvasGroup.alpha = 1f - Mathf.Clamp01(t / fadeOut);
            yield return null;
        }
        if (canvasGroup != null) canvasGroup.alpha = 0f;

        // Swap content
        currentStep = next;
        ApplyStepText();
        SetActiveAppearance();
        if (next == TutorialStep.Complete)
            yield break; // stay hidden

        // Fade in
        t = 0f;
        float fadeIn = Mathf.Max(0.05f, fadeInDuration);
        while (t < fadeIn)
        {
            t += Time.unscaledDeltaTime;
            if (canvasGroup != null) canvasGroup.alpha = Mathf.Clamp01(t / fadeIn);
            yield return null;
        }
        if (canvasGroup != null) canvasGroup.alpha = 1f;
    }

    private void PersistLevelSelectSeen()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        levelSelectPromptShownThisRuntime = true;
#else
        PlayerPrefs.SetInt(LevelSelectSeenKey, 1);
        PlayerPrefs.Save();
#endif
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

    private bool IsJumpPressed()
    {
        bool pressed = false;
#if ENABLE_LEGACY_INPUT_MANAGER
        pressed |= Input.GetKeyDown(KeyCode.Space);
#endif
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
            pressed |= Keyboard.current.spaceKey.wasPressedThisFrame;
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

        textOutline = textObj.AddComponent<Outline>();
        textOutline.effectColor = new Color(0f, 0f, 0f, 0.85f);
        textOutline.effectDistance = new Vector2(2f, -2f);
    }
}
