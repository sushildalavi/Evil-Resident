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
        Transform content = CreateContentPanel(activeCanvasRoot.transform);

        bool isMainMenu = scene.name == MainMenuSceneName;

        CreateText(content, "Title", isMainMenu ? "Main Menu" : "Difficulty Select", 72, FontStyle.Bold,
            new Color(0.95f, 0.95f, 0.96f, 1f), new Vector2(0.10f, 0.72f), new Vector2(0.90f, 0.90f));

        if (isMainMenu)
        {
            CreateButton(content, "TutorialButton", "Tutorial", new Vector2(0.5f, 0.46f), LoadTutorial);
            CreateButton(content, "LevelSelectButton", "Level Select", new Vector2(0.5f, 0.30f), LoadDifficultyMenu);
        }
        else
        {
            CreateButton(content, "EasyButton", "Easy", new Vector2(0.5f, 0.50f), LoadEasy);
            CreateButton(content, "MediumButton", "Medium", new Vector2(0.5f, 0.36f), LoadMedium);
            CreateButton(content, "HardButton", "Hard", new Vector2(0.5f, 0.22f), LoadHard);
            CreateButton(content, "BackButton", "Back", new Vector2(0.5f, 0.08f), LoadMainMenu);
        }
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
        panelImage.color = new Color(0f, 0f, 0f, 0.28f);

        RectTransform rect = panelImage.rectTransform;
        rect.anchorMin = new Vector2(0.27f, 0.08f);
        rect.anchorMax = new Vector2(0.73f, 0.92f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        return panelObject.transform;
    }

    static void CreateFullScreenBackground(Transform parent)
    {
        GameObject bgObject = new GameObject("Background", typeof(Image));
        bgObject.transform.SetParent(parent, false);

        Image image = bgObject.GetComponent<Image>();
        image.color = new Color(0.08f, 0.1f, 0.14f, 1f);

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

    static void CreateButton(Transform parent, string name, string label, Vector2 anchorCenter, UnityEngine.Events.UnityAction onClick)
    {
        GameObject buttonObject = new GameObject(name, typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);

        Image image = buttonObject.GetComponent<Image>();
        image.color = new Color(0.18f, 0.21f, 0.27f, 1f);

        Button button = buttonObject.GetComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = new Color(0.18f, 0.21f, 0.27f, 1f);
        colors.highlightedColor = new Color(0.28f, 0.33f, 0.42f, 1f);
        colors.pressedColor = new Color(0.12f, 0.15f, 0.20f, 1f);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color(0.10f, 0.10f, 0.10f, 0.6f);
        colors.fadeDuration = 0.08f;
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
        labelText.color = new Color(0.96f, 0.96f, 0.97f, 1f);

        RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
        buttonRect.anchorMin = anchorCenter;
        buttonRect.anchorMax = anchorCenter;
        buttonRect.pivot = new Vector2(0.5f, 0.5f);
        buttonRect.sizeDelta = new Vector2(500f, 92f);

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
