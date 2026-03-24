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
        Text titleText;
        Text taglineText;
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
                instance.Show();
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
            Show();
        }

        void OnDestroy()
        {
            if (instance == this) instance = null;
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            ApplySceneText();
            Show();
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

            Text title = CreateText(content, "Title",
                "IT SAW YOU", 88, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Color(1f, 0.2f, 0.2f, 1f), new Vector2(0.04f, 0.72f), new Vector2(0.96f, 0.92f));
            AddTextEffects(title.gameObject, new Color(0.18f, 0f, 0f, 1f));
            titleText = title;

            taglineText = CreateText(content, "Tagline",
                "Collect 3 keys. Escape. Don't be seen.",
                38, FontStyle.Normal, TextAnchor.MiddleCenter, new Color(0.92f, 0.92f, 0.95f, 1f),
                new Vector2(0.08f, 0.52f), new Vector2(0.92f, 0.64f));

            startHintText = CreateText(content, "StartHint",
                "Press ENTER or SPACE to begin",
                48, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(1f, 0.85f, 0.35f, 1f),
                new Vector2(0.10f, 0.08f), new Vector2(0.90f, 0.18f));

            ApplySceneText();
        }

        void ApplySceneText()
        {
            if (titleText == null || taglineText == null || startHintText == null)
                return;

            if (SceneManager.GetActiveScene().path == "Assets/Sahil/Test/NewLevel.unity")
            {
                titleText.text = "EVIL RESIDENT";
                taglineText.text = "Escape from the main door";
                startHintText.text = "Press ENTER or SPACE to begin";
                return;
            }

            titleText.text = "IT SAW YOU";
            taglineText.text = "Collect 3 keys. Escape. Don't be seen.";
            startHintText.text = "Press ENTER or SPACE to begin";
        }

        void CreateBackground(Transform parent)
        {
            GameObject bgObj = new GameObject("Background");
            bgObj.transform.SetParent(parent, false);
            var bg = bgObj.AddComponent<Image>();
            bg.color = new Color(0.01f, 0.01f, 0.03f, 0.92f);
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
            image.color = new Color(0.02f, 0.03f, 0.06f, 0.45f);

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
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
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
