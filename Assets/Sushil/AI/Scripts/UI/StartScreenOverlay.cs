using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Sushil.Systems
{
    public class StartScreenOverlay : MonoBehaviour
    {
        static StartScreenOverlay instance;
        public static bool IsShowing => instance != null && instance.showing;

        Canvas canvas;
        GameObject root;
        bool showing;
        Text productionText;
        Text titleText;
        Text taglineText;
        Text riddleText;
        Text startHintText;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (instance == null)
            {
                var go = new GameObject("StartScreenOverlay");
                instance = go.AddComponent<StartScreenOverlay>();
            }
            else
            {
                instance.RefreshForCurrentScene();
            }
        }

        void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);
            BuildUI();
            SceneManager.sceneLoaded += OnSceneLoaded;
            RefreshForCurrentScene();
        }

        void OnDestroy()
        {
            if (instance == this) instance = null;
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            ApplySceneText();
            RefreshForCurrentScene();
        }

        void Update()
        {
            if (!showing) return;
            if (WasStartPressed())
                HideAndStart();
        }

        void Show()
        {
            if (root == null) BuildUI();
            showing = true;
            if (root != null) root.SetActive(true);
            Time.timeScale = 0f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        void HideAndStart()
        {
            showing = false;
            if (root != null) root.SetActive(false);
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            GameAnalyticsTracker.BeginRun();
        }

        void RefreshForCurrentScene()
        {
            Scene activeScene = SceneManager.GetActiveScene();
            if (ShouldSuppressOverlayInScene(activeScene))
            {
                HideImmediate();
                return;
            }

            Show();
        }

        void HideImmediate()
        {
            showing = false;
            if (root != null) root.SetActive(false);

            Scene activeScene = SceneManager.GetActiveScene();
            if (IsMenuScene(activeScene.name))
            {
                Time.timeScale = 0f;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                return;
            }

            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        void BuildUI()
        {
            if (canvas != null && root != null) return;

            canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = short.MaxValue - 1;

            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>();

            root = new GameObject("StartRoot", typeof(RectTransform));
            root.transform.SetParent(transform, false);
            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;
            rootRect.localScale = Vector3.one;

            CreateBackground(root.transform);
            Transform content = CreateContentContainer(root.transform);

            productionText = CreateText(content, "Production",
                "5 Guys at Freddy's presents :",
                24, FontStyle.Normal, TextAnchor.MiddleCenter,
                new Color(0.88f, 0.90f, 0.94f, 0.96f), new Vector2(0.14f, 0.82f), new Vector2(0.86f, 0.89f));
            AddTextEffects(productionText.gameObject, new Color(0.03f, 0.05f, 0.08f, 0.95f));

            Text title = CreateText(content, "Title",
                "IT SAW YOU", 96, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Color(1f, 0.2f, 0.2f, 1f), new Vector2(0.08f, 0.60f), new Vector2(0.92f, 0.80f));
            AddTextEffects(title.gameObject, new Color(0.18f, 0f, 0f, 1f));
            titleText = title;

            taglineText = CreateText(content, "Tagline",
                "Collect 3 keys. Escape. Don't be seen.",
                34, FontStyle.Normal, TextAnchor.MiddleCenter, new Color(0.92f, 0.92f, 0.95f, 1f),
                new Vector2(0.10f, 0.44f), new Vector2(0.90f, 0.53f));
            AddTextEffects(taglineText.gameObject, new Color(0.04f, 0.05f, 0.08f, 0.95f));

            riddleText = CreateText(content, "Riddle",
                string.Empty,
                28, FontStyle.Normal, TextAnchor.MiddleCenter, new Color(0.82f, 0.84f, 0.90f, 0.95f),
                new Vector2(0.14f, 0.28f), new Vector2(0.86f, 0.40f), 1.15f);
            AddTextEffects(riddleText.gameObject, new Color(0.02f, 0.04f, 0.08f, 0.95f));

            startHintText = CreateText(content, "StartHint",
                "Press ENTER or SPACE to begin",
                44, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(1f, 0.86f, 0.36f, 1f),
                new Vector2(0.12f, 0.08f), new Vector2(0.88f, 0.17f));
            AddTextEffects(startHintText.gameObject, new Color(0.18f, 0.11f, 0.02f, 1f));

            ApplySceneText();
        }

        void ApplySceneText()
        {
            if (titleText == null || taglineText == null || riddleText == null || startHintText == null)
                return;

            Scene activeScene = SceneManager.GetActiveScene();
            string scenePath = activeScene.path;
            string sceneName = activeScene.name;
            string sceneNameLower = string.IsNullOrWhiteSpace(sceneName) ? string.Empty : sceneName.ToLowerInvariant();

            titleText.gameObject.SetActive(true);
            startHintText.gameObject.SetActive(true);
            taglineText.gameObject.SetActive(true);
            riddleText.gameObject.SetActive(true);

            if (productionText != null)
                productionText.gameObject.SetActive(false);

            if (sceneName == "Level Select" || sceneName == "Difficulty Select")
            {
                if (productionText != null)
                    productionText.gameObject.SetActive(true);
                titleText.text = "The Evil Resident";
                taglineText.gameObject.SetActive(true);
                taglineText.text = sceneName == "Difficulty Select"
                    ? "Choose your challenge."
                    : "Pick where to begin.";
                riddleText.gameObject.SetActive(false);
                riddleText.text = string.Empty;
                startHintText.gameObject.SetActive(true);
                startHintText.text = "Press SPACE to start";
                return;
            }

            taglineText.gameObject.SetActive(true);
            riddleText.gameObject.SetActive(true);
            startHintText.gameObject.SetActive(true);

            if (scenePath == "Assets/Neel/Tutorial.unity")
            {
                titleText.text = "TUTORIAL";
                taglineText.text = "Learn the controls step by step.";
                riddleText.gameObject.SetActive(false);
                startHintText.text = "Press ENTER or SPACE to begin";
                return;
            }

            if (scenePath.StartsWith("Assets/Sahil/Test/") ||
                scenePath == "Assets/Sushil/Easy Level.unity")
            {
                titleText.text = "Evil Resident";
                if (sceneNameLower.Contains("easy"))
                {
                    titleText.text = "Evil Resident - Easy";
                    taglineText.text = "Escape from the main door. Easy mode.";
                }
                else if (sceneNameLower.Contains("medium"))
                {
                    titleText.text = "Evil Resident - Medium";
                    taglineText.text = "Escape from the main door. Medium mode.";
                }
                else if (sceneNameLower.Contains("difficult") || sceneNameLower.Contains("hard"))
                {
                    titleText.text = "Evil Resident - Hard";
                    taglineText.text = "Escape from the main door. Hard mode.";
                }
                else
                    taglineText.text = "Escape from the main door.";
                if (IsEasyScene(scenePath, sceneNameLower))
                {
                    riddleText.gameObject.SetActive(false);
                    riddleText.text = string.Empty;
                }
                else
                {
                    riddleText.gameObject.SetActive(true);
                    riddleText.text = "Walls here do not have ears, but they might show something... ;)";
                }
                startHintText.text = "Press ENTER or SPACE to begin";
                return;
            }

            titleText.text = "IT SAW YOU";
            taglineText.text = "Collect 3 keys. Escape. Don't be seen.";
            riddleText.gameObject.SetActive(false);
            riddleText.text = string.Empty;
            startHintText.text = "Press ENTER or SPACE to begin";
        }

        bool ShouldSuppressOverlayInScene(Scene scene)
        {
            string sceneName = string.IsNullOrWhiteSpace(scene.name) ? string.Empty : scene.name.ToLowerInvariant();
            return sceneName.Contains("tutorial") ||
                   sceneName == "level select" ||
                   sceneName == "difficulty select";
        }

        static bool IsMenuScene(string sceneName)
        {
            return sceneName == "Level Select" || sceneName == "Difficulty Select";
        }

        bool IsEasyScene(string scenePath, string sceneNameLower)
        {
            return scenePath == "Assets/Sahil/Test/Easy Level.unity" ||
                   scenePath == "Assets/Sushil/Easy Level.unity" ||
                   sceneNameLower.Contains("easy");
        }

        void CreateBackground(Transform parent)
        {
            GameObject bgObj = new GameObject("Background");
            bgObj.transform.SetParent(parent, false);
            var bg = bgObj.AddComponent<Image>();
            bg.color = new Color(0.01f, 0.01f, 0.03f, 1f);
            var bgRect = bg.rectTransform;
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;
        }

        Transform CreateContentContainer(Transform parent)
        {
            var obj = new GameObject("ContentContainer");
            obj.transform.SetParent(parent, false);
            var image = obj.AddComponent<Image>();
            image.color = new Color(0.02f, 0.03f, 0.06f, 0f);

            var rect = image.rectTransform;
            rect.anchorMin = new Vector2(0.06f, 0.03f);
            rect.anchorMax = new Vector2(0.94f, 0.97f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return obj.transform;
        }

        Text CreateText(
            Transform parent,
            string name,
            string content,
            int fontSize,
            FontStyle style,
            TextAnchor anchor,
            Color color,
            Vector2 anchorMin,
            Vector2 anchorMax,
            float lineSpacing = 1f)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            Text text = obj.AddComponent<Text>();
            text.font = OverlayTypography.GetFont(fontSize);
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.alignment = anchor;
            text.color = color;
            text.text = content;
            text.lineSpacing = lineSpacing;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = Mathf.Max(12, Mathf.RoundToInt(fontSize * 0.55f));
            text.resizeTextMaxSize = fontSize;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;

            RectTransform rect = text.rectTransform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return text;
        }

        void AddTextEffects(GameObject textObj, Color outlineColor)
        {
            var shadow = textObj.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.85f);
            shadow.effectDistance = new Vector2(3f, -3f);

            var outline = textObj.AddComponent<Outline>();
            outline.effectColor = outlineColor;
            outline.effectDistance = new Vector2(2f, -2f);
        }

        bool WasStartPressed()
        {
            bool pressed = false;
#if ENABLE_LEGACY_INPUT_MANAGER
            pressed |= Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space);
#endif
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
                pressed |= Keyboard.current.enterKey.wasPressedThisFrame ||
                           Keyboard.current.numpadEnterKey.wasPressedThisFrame ||
                           Keyboard.current.spaceKey.wasPressedThisFrame;
#endif
            return pressed;
        }
    }
}
