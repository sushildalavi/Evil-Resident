using UnityEngine;
using UnityEngine.UI;
using Sushil.AI;

public class Tutorial2ResidentPrompts : MonoBehaviour
{
    [Header("Prompt Copy")]
    [TextArea] public string introPrompt = "This is the resident, Maintain your distance from him";
    [TextArea] public string spottedPrompt = "If he sees you, then hide";
    [TextArea] public string hidePrompt = "Make sure he is at a safe distance before getting out";
    [TextArea] public string finalPrompt = "Now collect the key and get out without getting caught";

    [Header("Timing")]
    public float initialDelay = 1.2f;
    public float promptDuration = 4.5f;
    public float safeDistance = 8f;

    [Header("Visuals")]
    public int sortingOrder = 220;
    public Color textColor = new Color(1f, 0.94f, 0.72f, 1f);
    public Color backgroundColor = new Color(0f, 0f, 0f, 0.42f);

    private Text promptText;
    private CanvasGroup canvasGroup;
    private RohitFPSController player;
    private ResidentAI resident;
    private bool finalPromptShown;
    private bool waitingForSafeExit;
    private int messageVersion;
    private float nextResolveAt;

    private void OnEnable()
    {
        RohitFPSController.OnHideEntered += HandleHideEntered;
    }

    private void OnDisable()
    {
        RohitFPSController.OnHideEntered -= HandleHideEntered;
    }

    private void Start()
    {
        BuildUI();
        ResolveReferences(true);
        StartCoroutine(ShowInitialPromptSequence());
    }

    private void Update()
    {
        if (Time.time >= nextResolveAt)
        {
            ResolveReferences(false);
            nextResolveAt = Time.time + 0.5f;
        }

        if (waitingForSafeExit && !finalPromptShown && IsSafeToExit())
        {
            waitingForSafeExit = false;
            finalPromptShown = true;
            ShowTimedMessage(finalPrompt, promptDuration + 1f);
        }
    }

    private System.Collections.IEnumerator ShowInitialPromptSequence()
    {
        float wait = Mathf.Max(0f, initialDelay);
        if (wait > 0f)
            yield return new WaitForSeconds(wait);

        ShowTimedMessage(introPrompt, promptDuration);

        yield return new WaitForSeconds(Mathf.Max(0.1f, promptDuration));
        ShowTimedMessage(spottedPrompt, promptDuration);
    }

    private void HandleHideEntered(RohitFPSController source, HideableObject hideable)
    {
        if (source == null)
            return;

        if (player == null)
            player = source;
        else if (source != player)
            return;

        waitingForSafeExit = true;
        ShowTimedMessage(hidePrompt, promptDuration);
    }

    private bool IsSafeToExit()
    {
        if (player == null || player.isHidden)
            return false;

        if (resident == null)
            return true;

        return Vector3.Distance(player.transform.position, resident.transform.position) >= Mathf.Max(1f, safeDistance);
    }

    private void ResolveReferences(bool force)
    {
        if (force || player == null)
            player = FindFirstObjectByType<RohitFPSController>();

        if (force || resident == null)
            resident = FindFirstObjectByType<ResidentAI>();
    }

    private void ShowTimedMessage(string message, float duration)
    {
        if (promptText == null || canvasGroup == null)
            return;

        messageVersion++;
        promptText.text = message;
        canvasGroup.alpha = 1f;
        StartCoroutine(HideAfterDelay(messageVersion, Mathf.Max(0.1f, duration)));
    }

    private System.Collections.IEnumerator HideAfterDelay(int version, float duration)
    {
        yield return new WaitForSeconds(duration);
        if (version != messageVersion || canvasGroup == null)
            yield break;

        canvasGroup.alpha = 0f;
    }

    private void BuildUI()
    {
        GameObject canvasObj = new GameObject("Tutorial2ResidentPromptCanvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        canvasGroup = canvasObj.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;

        GameObject bgObj = new GameObject("Background");
        bgObj.transform.SetParent(canvasObj.transform, false);
        Image bg = bgObj.AddComponent<Image>();
        bg.color = backgroundColor;

        RectTransform bgRect = bg.rectTransform;
        bgRect.anchorMin = new Vector2(0.2f, 0.84f);
        bgRect.anchorMax = new Vector2(0.8f, 0.94f);
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;

        GameObject textObj = new GameObject("PromptText");
        textObj.transform.SetParent(canvasObj.transform, false);
        promptText = textObj.AddComponent<Text>();
        promptText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        promptText.fontSize = 34;
        promptText.alignment = TextAnchor.MiddleCenter;
        promptText.color = textColor;
        promptText.resizeTextForBestFit = true;
        promptText.resizeTextMinSize = 18;
        promptText.resizeTextMaxSize = 34;

        RectTransform textRect = promptText.rectTransform;
        textRect.anchorMin = new Vector2(0.22f, 0.85f);
        textRect.anchorMax = new Vector2(0.78f, 0.93f);
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
    }
}
