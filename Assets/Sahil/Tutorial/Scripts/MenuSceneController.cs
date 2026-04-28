using System;
using System.Collections.Generic;
using Sushil.Systems;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

public class MenuSceneController : MonoBehaviour
{
    const string MainMenuSceneName = "Level Select";
    const string DifficultyMenuSceneName = "Difficulty Select";

    const string TutorialSceneName = "New Tutorial 1";
    const string EasySceneName = "Easy Level";
    const string MediumSceneName = "Medium Level";
    const string HardSceneName = "Hard Level";

    static MenuSceneController instance;

    GameObject activeCanvasRoot;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap()
    {
        if (instance != null)
            return;

        GameObject host = new GameObject("MenuSceneController");
        instance = host.AddComponent<MenuSceneController>();
        DontDestroyOnLoad(host);
    }

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        SceneManager.sceneLoaded += OnSceneLoaded;
        BuildMenuForScene(SceneManager.GetActiveScene());
    }

    void OnDestroy()
    {
        if (instance == this)
            instance = null;

        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        BuildMenuForScene(scene);
    }

    public void LoadTutorial()
    {
        LoadScene(TutorialSceneName);
    }

    public void LoadDifficultyMenu()
    {
        LoadScene(DifficultyMenuSceneName);
    }

    public void LoadEasy()
    {
        LoadScene(EasySceneName);
    }

    public void LoadMedium()
    {
        LoadScene(MediumSceneName);
    }

    public void LoadHard()
    {
        LoadScene(HardSceneName);
    }

    public void LoadMainMenu()
    {
        LoadScene(MainMenuSceneName);
    }

    void BuildMenuForScene(Scene scene)
    {
        CleanupMenuUI();

        if (!IsMenuScene(scene.name))
        {
            Time.timeScale = 1f;
            return;
        }

        LockSceneToUIMode();
        DisableInterferingBehaviourInMenuScene();
        EnsureEventSystemExists(scene);

        activeCanvasRoot = new GameObject("MenuCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        SceneManager.MoveGameObjectToScene(activeCanvasRoot, scene);

        Canvas canvas = activeCanvasRoot.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue - 2;

        CanvasScaler scaler = activeCanvasRoot.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        CreateFullScreenBackground(activeCanvasRoot.transform);
        CreateVignette(activeCanvasRoot.transform);
        Transform content = CreateContentPanel(activeCanvasRoot.transform);
        CreateAccentBar(content, top: true);
        CreateAccentBar(content, top: false);

        bool isMainMenu = scene.name == MainMenuSceneName;

        if (isMainMenu)
        {
            // ── MAIN MENU layout ────────────────────────────────────────────
            CreateText(content, "TeamLabel", "5 GUYS AT FREDDY'S  presents", 22, FontStyle.Italic,
                new Color(0.65f, 0.18f, 0.18f, 0.85f),
                new Vector2(0.10f, 0.90f), new Vector2(0.90f, 0.95f));

            // Game title — HUGE, with flicker + glow
            Text gameTitle = CreateText(content, "GameTitle", "EVIL  RESIDENT", 88, FontStyle.Bold,
                new Color(0.92f, 0.18f, 0.18f, 1f),
                new Vector2(0.04f, 0.72f), new Vector2(0.96f, 0.88f));
            AddBloodGlow(gameTitle.gameObject);
            gameTitle.gameObject.AddComponent<HorrorTitleFlicker>();

            // Subtle horizontal divider lines flanking the "MAIN MENU" label.
            CreateDivider(content, new Vector2(0.10f, 0.665f), new Vector2(0.42f, 0.668f));
            CreateDivider(content, new Vector2(0.58f, 0.665f), new Vector2(0.90f, 0.668f));

            CreateText(content, "MenuLabel", "MAIN  MENU", 26, FontStyle.Bold,
                new Color(0.95f, 0.92f, 0.85f, 0.95f),
                new Vector2(0.10f, 0.625f), new Vector2(0.90f, 0.685f));

            CreateText(content, "Quote", "\"He hears every footstep.\"", 22, FontStyle.Italic,
                new Color(0.78f, 0.78f, 0.82f, 0.85f),
                new Vector2(0.10f, 0.55f), new Vector2(0.90f, 0.60f));

            CreateButton(content, "TutorialButton", "TUTORIAL",
                new Vector2(0.5f, 0.40f), LoadTutorial,
                accentColor: new Color(0.85f, 0.10f, 0.10f, 0.65f));
            CreateButton(content, "StartButton", "START  ▶",
                new Vector2(0.5f, 0.24f), LoadDifficultyMenu,
                accentColor: new Color(0.85f, 0.10f, 0.10f, 0.65f));
        }
        else
        {
            // ── DIFFICULTY SELECT layout ────────────────────────────────────
            // Small back button anchored to the top-left of the card.
            CreateSmallCornerButton(content, "BackButton", "◀  BACK",
                topLeft: true, LoadMainMenu);

            CreateText(content, "TeamLabel", "EVIL  RESIDENT", 22, FontStyle.Italic,
                new Color(0.65f, 0.18f, 0.18f, 0.85f),
                new Vector2(0.20f, 0.90f), new Vector2(0.80f, 0.95f));

            Text title = CreateText(content, "Title", "DIFFICULTY", 80, FontStyle.Bold,
                new Color(0.92f, 0.18f, 0.18f, 1f),
                new Vector2(0.05f, 0.72f), new Vector2(0.95f, 0.88f));
            AddBloodGlow(title.gameObject);
            title.gameObject.AddComponent<HorrorTitleFlicker>();

            CreateText(content, "Quote", "\"Choose your fear.\"", 22, FontStyle.Italic,
                new Color(0.78f, 0.78f, 0.82f, 0.85f),
                new Vector2(0.10f, 0.62f), new Vector2(0.90f, 0.68f));

            // Color-coded difficulty buttons — green / amber / blood-red.
            CreateButton(content, "EasyButton", "EASY",
                new Vector2(0.5f, 0.49f), LoadEasy,
                accentColor: new Color(0.20f, 0.75f, 0.30f, 0.75f));
            CreateButton(content, "MediumButton", "MEDIUM",
                new Vector2(0.5f, 0.34f), LoadMedium,
                accentColor: new Color(0.95f, 0.65f, 0.10f, 0.75f));
            CreateButton(content, "HardButton", "HARD",
                new Vector2(0.5f, 0.19f), LoadHard,
                accentColor: new Color(0.92f, 0.10f, 0.10f, 0.85f));
        }

        // Footer tagline at the bottom of the panel.
        CreateText(content, "Footer", "·  Don't blink.  ·", 18, FontStyle.Italic,
            new Color(0.55f, 0.10f, 0.10f, 0.7f),
            new Vector2(0.10f, 0.02f), new Vector2(0.90f, 0.07f));
    }

    // Thin red horizontal divider line.
    static void CreateDivider(Transform parent, Vector2 anchorMin, Vector2 anchorMax)
    {
        GameObject divObj = new GameObject("Divider", typeof(Image));
        divObj.transform.SetParent(parent, false);
        Image image = divObj.GetComponent<Image>();
        image.color = new Color(0.65f, 0.10f, 0.10f, 0.55f);
        image.raycastTarget = false;
        RectTransform rect = image.rectTransform;
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    // Small button anchored to a corner of the card. Used for compact BACK arrows.
    static void CreateSmallCornerButton(Transform parent, string name, string label,
        bool topLeft, UnityEngine.Events.UnityAction onClick)
    {
        GameObject buttonObject = new GameObject(name, typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);

        Image image = buttonObject.GetComponent<Image>();
        Sprite buttonSprite = DarkButtonSprites.GetSprite(0);
        if (buttonSprite != null)
        {
            image.sprite = buttonSprite;
            image.type = Image.Type.Sliced;
            image.color = Color.white;
        }
        else
        {
            image.color = new Color(0.10f, 0.10f, 0.12f, 1f);
        }
        image.raycastTarget = true; // explicit — must catch the click

        Button button = buttonObject.GetComponent<Button>();
        button.transition = Selectable.Transition.ColorTint;
        button.interactable = true;
        ColorBlock colors = button.colors;
        colors.normalColor      = new Color(0.85f, 0.85f, 0.85f, 0.9f);
        colors.highlightedColor = new Color(1.4f, 0.55f, 0.55f, 1f);
        colors.pressedColor     = new Color(0.65f, 0.10f, 0.10f, 1f);
        colors.selectedColor    = new Color(1.4f, 0.55f, 0.55f, 1f);
        colors.disabledColor    = new Color(0.30f, 0.30f, 0.30f, 0.6f);
        colors.colorMultiplier  = 1f;
        colors.fadeDuration     = 0.10f;
        button.colors = colors;
        button.targetGraphic = image;
        button.onClick.AddListener(onClick);

        GameObject labelObject = new GameObject("Label", typeof(Text));
        labelObject.transform.SetParent(buttonObject.transform, false);
        Text labelText = labelObject.GetComponent<Text>();
        labelText.font = OverlayTypography.GetFont(22);
        labelText.text = label;
        labelText.fontSize = 22;
        labelText.fontStyle = FontStyle.Bold;
        labelText.alignment = TextAnchor.MiddleCenter;
        labelText.color = new Color(0.95f, 0.92f, 0.85f, 1f);
        labelText.raycastTarget = false;

        Shadow labelShadow = labelObject.AddComponent<Shadow>();
        labelShadow.effectColor = new Color(0f, 0f, 0f, 0.95f);
        labelShadow.effectDistance = new Vector2(1.5f, -1.5f);

        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        if (topLeft)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(20f, -20f);
        }
        else
        {
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-20f, -20f);
        }
        rect.sizeDelta = new Vector2(140f, 44f);

        RectTransform textRect = labelText.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
    }

    static void AddBloodGlow(GameObject titleObject)
    {
        // Multiple outlines stack to create a soft red bleed-glow around the text.
        Outline a = titleObject.AddComponent<Outline>();
        a.effectColor = new Color(0.85f, 0.05f, 0.05f, 0.85f);
        a.effectDistance = new Vector2(2f, -2f);

        Outline b = titleObject.AddComponent<Outline>();
        b.effectColor = new Color(0.50f, 0.02f, 0.02f, 0.7f);
        b.effectDistance = new Vector2(4f, -4f);

        Shadow drop = titleObject.AddComponent<Shadow>();
        drop.effectColor = new Color(0f, 0f, 0f, 0.95f);
        drop.effectDistance = new Vector2(6f, -6f);
    }

    static void CreateAccentBar(Transform parent, bool top)
    {
        GameObject barObject = new GameObject(top ? "AccentTop" : "AccentBottom", typeof(Image));
        barObject.transform.SetParent(parent, false);
        Image image = barObject.GetComponent<Image>();
        image.color = new Color(0.85f, 0.10f, 0.10f, 0.85f);
        image.raycastTarget = false;

        RectTransform rect = image.rectTransform;
        if (top)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
        }
        else
        {
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
        }
        rect.sizeDelta = new Vector2(0f, 3f);
        rect.anchoredPosition = Vector2.zero;
    }

    static void CreateVignette(Transform parent)
    {
        // Dark-red ambient cast over the background for a dim, foreboding feel.
        GameObject vignetteObject = new GameObject("Vignette", typeof(Image));
        vignetteObject.transform.SetParent(parent, false);

        Image image = vignetteObject.GetComponent<Image>();
        image.color = new Color(0.05f, 0f, 0f, 0.30f);
        image.raycastTarget = false;

        RectTransform rect = image.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    static bool IsMenuScene(string sceneName)
    {
        return sceneName == MainMenuSceneName || sceneName == DifficultyMenuSceneName;
    }

    void CleanupMenuUI()
    {
        if (activeCanvasRoot != null)
        {
            Destroy(activeCanvasRoot);
            activeCanvasRoot = null;
        }
    }

    static void LockSceneToUIMode()
    {
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    static void DisableInterferingBehaviourInMenuScene()
    {
        string[] blockedTypeNames =
        {
            "RohitFPSController",
            "InteractionUI",
            "TutorialStepUI",
            "LevelSelectDoorTransition",
            "LevelSelectDoorSigns",
            "LevelSelectFirstLaunchPrompts",
            "TutorialDoorSceneTransition"
        };

        HashSet<string> blocked = new HashSet<string>(blockedTypeNames, StringComparer.Ordinal);

        MonoBehaviour[] allBehaviours = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < allBehaviours.Length; i++)
        {
            MonoBehaviour b = allBehaviours[i];
            if (b == null)
                continue;

            if (blocked.Contains(b.GetType().Name))
                b.enabled = false;
        }
    }

    static void EnsureEventSystemExists(Scene scene)
    {
        EventSystem eventSystem = FindFirstObjectByType<EventSystem>();
        if (eventSystem != null)
        {
            SceneManager.MoveGameObjectToScene(eventSystem.gameObject, scene);
            EnsureInputModule(eventSystem.gameObject);
            return;
        }

        GameObject go = new GameObject("EventSystem", typeof(EventSystem));
        EnsureInputModule(go);
        SceneManager.MoveGameObjectToScene(go, scene);
    }

    static void EnsureInputModule(GameObject eventSystemObject)
    {
#if ENABLE_INPUT_SYSTEM
        if (eventSystemObject.GetComponent<InputSystemUIInputModule>() == null)
            eventSystemObject.AddComponent<InputSystemUIInputModule>();
#else
        if (eventSystemObject.GetComponent<StandaloneInputModule>() == null)
            eventSystemObject.AddComponent<StandaloneInputModule>();
#endif
    }

    static Transform CreateContentPanel(Transform parent)
    {
        GameObject panelObject = new GameObject("ContentPanel", typeof(Image));
        panelObject.transform.SetParent(parent, false);

        Image panelImage = panelObject.GetComponent<Image>();
        // Near-black card with subtle red glow border via stacked Outline components.
        panelImage.color = new Color(0.03f, 0.03f, 0.04f, 1f);
        panelImage.raycastTarget = false;

        Outline borderInner = panelObject.AddComponent<Outline>();
        borderInner.effectColor = new Color(0.85f, 0.10f, 0.10f, 0.55f);
        borderInner.effectDistance = new Vector2(2f, -2f);

        Outline borderOuter = panelObject.AddComponent<Outline>();
        borderOuter.effectColor = new Color(0.45f, 0.02f, 0.02f, 0.35f);
        borderOuter.effectDistance = new Vector2(4f, -4f);

        RectTransform rect = panelImage.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(720f, 880f);
        rect.anchoredPosition = Vector2.zero;

        return panelObject.transform;
    }

    static void CreateFullScreenBackground(Transform parent)
    {
        GameObject bgObject = new GameObject("Background", typeof(Image));
        bgObject.transform.SetParent(parent, false);

        Image image = bgObject.GetComponent<Image>();
        // Pure black so the card pops and there's no gameplay bleed-through.
        image.color = new Color(0f, 0f, 0f, 1f);
        image.raycastTarget = false;

        RectTransform rect = image.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    static Text CreateText(Transform parent, string name, string value, int fontSize, FontStyle style, Color color, Vector2 anchorMin, Vector2 anchorMax)
    {
        GameObject textObject = new GameObject(name, typeof(Text));
        textObject.transform.SetParent(parent, false);

        Text text = textObject.GetComponent<Text>();
        text.font = OverlayTypography.GetFont(fontSize);
        text.text = value;
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = color;
        text.resizeTextForBestFit = true;
        text.resizeTextMinSize = Mathf.Max(18, Mathf.RoundToInt(fontSize * 0.55f));
        text.resizeTextMaxSize = fontSize;

        RectTransform rect = text.rectTransform;
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        return text;
    }

    static void CreateButton(Transform parent, string name, string label, Vector2 anchorCenter,
        UnityEngine.Events.UnityAction onClick, Color? accentColor = null)
    {
        GameObject buttonObject = new GameObject(name, typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);

        Image image = buttonObject.GetComponent<Image>();
        // Apply the kΩsmaragd Dark UI sprite. Tweak the index to swap styles.
        Sprite buttonSprite = DarkButtonSprites.GetSprite(0);
        if (buttonSprite != null)
        {
            image.sprite = buttonSprite;
            image.type = Image.Type.Sliced;
            image.color = Color.white;
        }
        else
        {
            image.color = new Color(0.08f, 0.08f, 0.09f, 1f);
        }
        image.raycastTarget = true; // explicit — must catch the click

        // Accent tint on hover/press is derived from the per-button accent color.
        Color accent = accentColor ?? new Color(0.85f, 0.10f, 0.10f, 0.85f);
        Color hover = new Color(
            Mathf.Min(1.5f, 1f + accent.r * 0.4f),
            Mathf.Min(1.5f, 0.55f + accent.g * 0.3f),
            Mathf.Min(1.5f, 0.55f + accent.b * 0.3f), 1f);

        Button button = buttonObject.GetComponent<Button>();
        button.transition = Selectable.Transition.ColorTint;
        button.interactable = true;
        ColorBlock colors = button.colors;
        colors.normalColor      = new Color(1f, 1f, 1f, 1f);
        colors.highlightedColor = hover;
        colors.pressedColor     = new Color(accent.r * 0.75f, accent.g * 0.75f, accent.b * 0.75f, 1f);
        colors.selectedColor    = hover;
        colors.disabledColor    = new Color(0.30f, 0.30f, 0.30f, 0.6f);
        colors.colorMultiplier  = 1f;
        colors.fadeDuration     = 0.10f;
        button.colors = colors;
        button.targetGraphic = image;
        button.onClick.AddListener(onClick);

        GameObject labelObject = new GameObject("Label", typeof(Text));
        labelObject.transform.SetParent(buttonObject.transform, false);

        Text labelText = labelObject.GetComponent<Text>();
        labelText.font = OverlayTypography.GetFont(40);
        labelText.text = label;
        labelText.fontSize = 40;
        labelText.fontStyle = FontStyle.Bold;
        labelText.alignment = TextAnchor.MiddleCenter;
        labelText.color = new Color(0.95f, 0.92f, 0.85f, 1f);
        labelText.raycastTarget = false; // clicks should land on the button, not the text

        Shadow labelShadow = labelObject.AddComponent<Shadow>();
        labelShadow.effectColor = new Color(0f, 0f, 0f, 0.95f);
        labelShadow.effectDistance = new Vector2(2f, -2f);

        RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
        buttonRect.anchorMin = anchorCenter;
        buttonRect.anchorMax = anchorCenter;
        buttonRect.pivot = new Vector2(0.5f, 0.5f);
        buttonRect.sizeDelta = new Vector2(560f, 96f);

        RectTransform textRect = labelText.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
    }

    static void LoadScene(string sceneName)
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(sceneName);
    }
}

// Subtle candle-flicker effect for horror titles. Uses unscaled time so it
// continues to animate when the game is paused (Time.timeScale = 0).
public class HorrorTitleFlicker : MonoBehaviour
{
    Text text;
    Color baseColor;
    float seed;

    void Awake()
    {
        text = GetComponent<Text>();
        if (text != null) baseColor = text.color;
        // Fully-qualified — MenuSceneController.cs has `using System;` which
        // would otherwise make `Random` ambiguous with System.Random.
        seed = UnityEngine.Random.value * 100f;
    }

    void Update()
    {
        if (text == null) return;
        float t = Time.unscaledTime + seed;
        // Mostly steady (~0.85–1.0 brightness) with rare sharp dips.
        float baseFlicker = 0.92f + 0.08f * Mathf.Sin(t * 1.7f);
        float spike = Mathf.PerlinNoise(t * 3.5f, seed) > 0.86f ? 0.65f : 1f;
        float k = baseFlicker * spike;
        text.color = new Color(baseColor.r * k, baseColor.g * k, baseColor.b * k, baseColor.a);
    }
}
