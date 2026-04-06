using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class TutorialStepUI : MonoBehaviour
{
    [Header("Timing")]
    public float fadeInTime = 0.4f;
    public float completedShowTime = 0.8f;
    public float delayBetweenSteps = 0.3f;

    Text instructionText;
    Text completedText;
    CanvasGroup canvasGroup;
    RohitFPSController player;

    int currentStep;
    bool stepCompleted;
    float stepTimer;
    bool waitingForNext;
    bool allDone;
    Vector2 lastMousePos;
    float mouseMoveAccum;

    enum Phase { FadeIn, WaitForAction, ShowCompleted, DelayNext }
    Phase phase;

    readonly string[] instructions = new string[]
    {
        "Press  W  to move forward",
        "Press  S  to move backward",
        "Press  A  to move left",
        "Press  D  to move right",
        "Move the  Mouse  to look around",
        "Hold  Left Shift  to sprint",
        "Press  Space  to jump",
        "Press  E  near objects to interact",
        "Press  F  near a hiding spot to hide",
        "Now find the key and escape through the door!"
    };

    void Start()
    {
        BuildUI();
        currentStep = 0;
        phase = Phase.FadeIn;
        stepTimer = 0f;
        canvasGroup.alpha = 0f;

#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null)
            lastMousePos = Mouse.current.position.ReadValue();
#endif
    }

    void Update()
    {
        if (allDone) return;

        if (player == null)
        {
            player = FindFirstObjectByType<RohitFPSController>();
            if (player == null) return;
        }

        switch (phase)
        {
            case Phase.FadeIn:
                stepTimer += Time.deltaTime;
                canvasGroup.alpha = Mathf.Clamp01(stepTimer / fadeInTime);
                instructionText.text = instructions[currentStep];
                completedText.gameObject.SetActive(false);

                if (stepTimer >= fadeInTime)
                {
                    canvasGroup.alpha = 1f;
                    phase = Phase.WaitForAction;
                    mouseMoveAccum = 0f;
                }
                break;

            case Phase.WaitForAction:
                if (CheckStepComplete())
                {
                    phase = Phase.ShowCompleted;
                    stepTimer = 0f;

                    if (currentStep < instructions.Length - 1)
                        completedText.gameObject.SetActive(true);
                }
                break;

            case Phase.ShowCompleted:
                stepTimer += Time.deltaTime;
                if (stepTimer >= completedShowTime)
                {
                    currentStep++;
                    if (currentStep >= instructions.Length)
                    {
                        allDone = true;
                        StartCoroutine(FadeOutAndDisable());
                        return;
                    }
                    phase = Phase.DelayNext;
                    stepTimer = 0f;
                }
                break;

            case Phase.DelayNext:
                stepTimer += Time.deltaTime;
                canvasGroup.alpha = Mathf.Clamp01(1f - (stepTimer / delayBetweenSteps));
                if (stepTimer >= delayBetweenSteps)
                {
                    phase = Phase.FadeIn;
                    stepTimer = 0f;
                }
                break;
        }
    }

    bool CheckStepComplete()
    {
        switch (currentStep)
        {
            case 0: return IsKeyHeld(KeyCode.W);
            case 1: return IsKeyHeld(KeyCode.S);
            case 2: return IsKeyHeld(KeyCode.A);
            case 3: return IsKeyHeld(KeyCode.D);
            case 4: return CheckMouseMoved();
            case 5: return IsKeyHeld(KeyCode.LeftShift);
            case 6: return WasKeyPressed(KeyCode.Space);
            case 7: return WasKeyPressed(KeyCode.E);
            case 8: return player != null && player.isHidden;
            case 9: return false;
            default: return false;
        }
    }

    bool IsKeyHeld(KeyCode key)
    {
        bool held = false;
#if ENABLE_LEGACY_INPUT_MANAGER
        held |= Input.GetKey(key);
#endif
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            switch (key)
            {
                case KeyCode.W: held |= Keyboard.current.wKey.isPressed; break;
                case KeyCode.A: held |= Keyboard.current.aKey.isPressed; break;
                case KeyCode.S: held |= Keyboard.current.sKey.isPressed; break;
                case KeyCode.D: held |= Keyboard.current.dKey.isPressed; break;
                case KeyCode.LeftShift: held |= Keyboard.current.leftShiftKey.isPressed; break;
            }
        }
#endif
        return held;
    }

    bool WasKeyPressed(KeyCode key)
    {
        bool pressed = false;
#if ENABLE_LEGACY_INPUT_MANAGER
        pressed |= Input.GetKeyDown(key);
#endif
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            switch (key)
            {
                case KeyCode.E: pressed |= Keyboard.current.eKey.wasPressedThisFrame; break;
                case KeyCode.F: pressed |= Keyboard.current.fKey.wasPressedThisFrame; break;
                case KeyCode.Space: pressed |= Keyboard.current.spaceKey.wasPressedThisFrame; break;
            }
        }
#endif
        return pressed;
    }

    bool CheckMouseMoved()
    {
        Vector2 currentPos = Vector2.zero;
        bool hasInput = false;

#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null)
        {
            Vector2 delta = Mouse.current.delta.ReadValue();
            mouseMoveAccum += delta.magnitude;
            hasInput = true;
        }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        if (!hasInput)
        {
            float mx = Input.GetAxis("Mouse X");
            float my = Input.GetAxis("Mouse Y");
            mouseMoveAccum += Mathf.Abs(mx) + Mathf.Abs(my);
        }
#endif
        return mouseMoveAccum > 80f;
    }

    void BuildUI()
    {
        GameObject canvasObj = new GameObject("TutorialStepCanvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        canvasGroup = canvasObj.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;

        GameObject bgObj = new GameObject("Background");
        bgObj.transform.SetParent(canvasObj.transform, false);
        Image bg = bgObj.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.5f);
        RectTransform bgRect = bg.rectTransform;
        bgRect.anchorMin = new Vector2(0.2f, 0.82f);
        bgRect.anchorMax = new Vector2(0.8f, 0.95f);
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;

        GameObject textObj = new GameObject("InstructionText");
        textObj.transform.SetParent(canvasObj.transform, false);
        instructionText = textObj.AddComponent<Text>();
        instructionText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        instructionText.fontSize = 40;
        instructionText.fontStyle = FontStyle.Bold;
        instructionText.alignment = TextAnchor.MiddleCenter;
        instructionText.color = new Color(1f, 0.95f, 0.4f, 1f);
        instructionText.resizeTextForBestFit = true;
        instructionText.resizeTextMinSize = 20;
        instructionText.resizeTextMaxSize = 40;
        instructionText.horizontalOverflow = HorizontalWrapMode.Wrap;

        RectTransform textRect = instructionText.rectTransform;
        textRect.anchorMin = new Vector2(0.2f, 0.83f);
        textRect.anchorMax = new Vector2(0.8f, 0.92f);
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        Shadow shadow = textObj.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.9f);
        shadow.effectDistance = new Vector2(2f, -2f);

        GameObject checkObj = new GameObject("CompletedText");
        checkObj.transform.SetParent(canvasObj.transform, false);
        completedText = checkObj.AddComponent<Text>();
        completedText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        completedText.fontSize = 28;
        completedText.fontStyle = FontStyle.Bold;
        completedText.alignment = TextAnchor.MiddleCenter;
        completedText.color = new Color(0.3f, 1f, 0.3f, 1f);
        completedText.text = "Good!";
        completedText.resizeTextForBestFit = true;
        completedText.resizeTextMinSize = 16;
        completedText.resizeTextMaxSize = 28;

        RectTransform checkRect = completedText.rectTransform;
        checkRect.anchorMin = new Vector2(0.35f, 0.76f);
        checkRect.anchorMax = new Vector2(0.65f, 0.83f);
        checkRect.offsetMin = Vector2.zero;
        checkRect.offsetMax = Vector2.zero;

        completedText.gameObject.SetActive(false);
    }

    System.Collections.IEnumerator FadeOutAndDisable()
    {
        yield return new WaitForSeconds(3f);

        float t = 0f;
        float fadeDuration = 1f;
        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            canvasGroup.alpha = 1f - (t / fadeDuration);
            yield return null;
        }
        canvasGroup.alpha = 0f;
    }
}
