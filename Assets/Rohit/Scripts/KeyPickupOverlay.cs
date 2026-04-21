using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class KeyPickupOverlay : MonoBehaviour
{
    static KeyPickupOverlay instance;

    Canvas canvas;
    Text messageText;
    Outline outline;
    Shadow shadow;
    Coroutine activeRoutine;
    bool overlaysSuppressed;

    public static void ShowKeyCollected(KeyType keyType)
    {
        EnsureInstance();
        if (instance != null)
            instance.Play($"KEY COLLECTED: {keyType.ToString().ToUpper()}");
    }

    static void EnsureInstance()
    {
        if (instance != null) return;

        if (IsOverlaySuppressedScene(SceneManager.GetActiveScene().name))
            return;

        var existing = FindFirstObjectByType<KeyPickupOverlay>();
        if (existing != null)
        {
            instance = existing;
            return;
        }

        var go = new GameObject("KeyPickupOverlay");
        instance = go.AddComponent<KeyPickupOverlay>();
        DontDestroyOnLoad(go);
    }

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        BuildUI();
        SceneManager.sceneLoaded += OnSceneLoaded;
        UpdateOverlayVisibility(SceneManager.GetActiveScene());
    }

    void OnDestroy()
    {
        if (instance == this)
            instance = null;

        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        UpdateOverlayVisibility(scene);
    }

    void BuildUI()
    {
        if (canvas != null) return;

        canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue - 30;

        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        gameObject.AddComponent<GraphicRaycaster>();

        var textObj = new GameObject("PickupMessage");
        textObj.transform.SetParent(canvas.transform, false);

        messageText = textObj.AddComponent<Text>();
        messageText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        messageText.fontSize = 56;
        messageText.fontStyle = FontStyle.Bold;
        messageText.alignment = TextAnchor.MiddleCenter;
        messageText.resizeTextForBestFit = true;
        messageText.resizeTextMinSize = 24;
        messageText.resizeTextMaxSize = 56;
        messageText.horizontalOverflow = HorizontalWrapMode.Wrap;
        messageText.verticalOverflow = VerticalWrapMode.Truncate;
        messageText.color = new Color(1f, 0.94f, 0.5f, 0f);
        messageText.text = string.Empty;

        shadow = textObj.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.9f);
        shadow.effectDistance = new Vector2(3f, -3f);

        outline = textObj.AddComponent<Outline>();
        outline.effectColor = new Color(0.25f, 0f, 0f, 0.95f);
        outline.effectDistance = new Vector2(2f, -2f);

        RectTransform rect = messageText.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0f, 220f);
        rect.sizeDelta = new Vector2(1400f, 220f);
    }

    void Play(string message)
    {
        if (overlaysSuppressed)
            return;

        if (messageText == null) BuildUI();
        if (activeRoutine != null) StopCoroutine(activeRoutine);
        activeRoutine = StartCoroutine(AnimateMessage(message));
    }

    IEnumerator AnimateMessage(string message)
    {
        messageText.text = message;

        float inTime = 0.15f;
        float holdTime = 1.2f;
        float outTime = 0.4f;

        Vector2 basePos = new Vector2(0f, 220f);

        float t = 0f;
        while (t < inTime)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / inTime);
            float eased = 1f - Mathf.Pow(1f - k, 3f);
            SetVisuals(eased, basePos + new Vector2(0f, Mathf.Lerp(32f, 0f, eased)), Mathf.Lerp(0.88f, 1f, eased));
            yield return null;
        }

        t = 0f;
        while (t < holdTime)
        {
            t += Time.unscaledDeltaTime;
            float pulse = 1f + Mathf.Sin(Time.unscaledTime * 10f) * 0.015f;
            SetVisuals(1f, basePos, pulse);
            yield return null;
        }

        t = 0f;
        while (t < outTime)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / outTime);
            float eased = 1f - k;
            SetVisuals(eased, basePos + new Vector2(0f, Mathf.Lerp(0f, -22f, k)), Mathf.Lerp(1f, 0.92f, k));
            yield return null;
        }

        SetVisuals(0f, basePos, 1f);
        messageText.text = string.Empty;
        activeRoutine = null;
    }

    void SetVisuals(float alpha01, Vector2 pos, float scale)
    {
        Color c = messageText.color;
        c.a = Mathf.Clamp01(alpha01);
        messageText.color = c;

        RectTransform rect = messageText.rectTransform;
        rect.anchoredPosition = pos;
        rect.localScale = Vector3.one * scale;
    }

    void UpdateOverlayVisibility(Scene scene)
    {
        overlaysSuppressed = IsOverlaySuppressedScene(scene.name);

        if (canvas != null)
            canvas.enabled = !overlaysSuppressed;

        if (overlaysSuppressed)
        {
            if (activeRoutine != null)
            {
                StopCoroutine(activeRoutine);
                activeRoutine = null;
            }

            if (messageText != null)
                messageText.text = string.Empty;
        }
    }

    static bool IsOverlaySuppressedScene(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
            return false;

        return sceneName == "Level Select" ||
               sceneName == "Difficulty Select" ||
               sceneName == "New Tutorial 1" ||
               sceneName == "New Tutorial 2" ||
               sceneName == "New Tutorial 3";
    }
}
