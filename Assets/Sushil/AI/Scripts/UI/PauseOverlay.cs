using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
#endif

namespace Sushil.Systems
{
    public class PauseOverlay : MonoBehaviour
    {
        const string MainMenuScenePath = "Assets/Sahil/Tutorial/Level Select.unity";
        const string MainMenuSceneName = "Level Select";

        static PauseOverlay instance;
        public static bool IsPaused => instance != null && instance.isPaused;

        // External entry point so other UI/triggers can open the pause overlay.
        public static void RequestShow()
        {
            if (instance == null) return;
            if (StartScreenOverlay.IsShowing || GameOverOverlay.IsShowing || EscapeOverlay.IsShowing) return;
            if (!instance.isPaused) instance.TogglePause();
        }

        public static void RequestHide()
        {
            if (instance == null) return;
            if (instance.isPaused) instance.TogglePause();
        }

        Canvas canvas;
        GameObject root;
        Text titleText;
        Text hintText;
        bool isPaused;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (instance == null)
            {
                var go = new GameObject("PauseOverlay");
                instance = go.AddComponent<PauseOverlay>();
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
            SetVisible(false);
        }

        void Update()
        {
            if (WasPausePressed())
            {
                if (StartScreenOverlay.IsShowing || GameOverOverlay.IsShowing || EscapeOverlay.IsShowing) return;
                TogglePause();
            }

            if (!isPaused) return;

            if (WasRestartPressed())
                RestartCurrentScene();
        }

        void TogglePause()
        {
            isPaused = !isPaused;
            SetVisible(isPaused);

            if (isPaused)
            {
                EnsureEventSystemExists();   // gameplay scenes often have no EventSystem; buttons need one
                Time.timeScale = 0f;
                AudioListener.pause = true;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else
            {
                Time.timeScale = 1f;
                AudioListener.pause = false;
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        static void EnsureEventSystemExists()
        {
            if (EventSystem.current != null) return;
            var existing = FindFirstObjectByType<EventSystem>();
            if (existing != null) { EventSystem.current = existing; return; }

            var go = new GameObject("EventSystem", typeof(EventSystem));
            DontDestroyOnLoad(go);
#if ENABLE_INPUT_SYSTEM
            go.AddComponent<InputSystemUIInputModule>();
#else
            go.AddComponent<StandaloneInputModule>();
#endif
        }

        void RestartCurrentScene()
        {
            isPaused = false;
            SetVisible(false);
            Time.timeScale = 1f;
            AudioListener.pause = false;

            Scene active = SceneManager.GetActiveScene();
            SceneManager.LoadScene(active.buildIndex);
        }

        void GoToMainMenu()
        {
            isPaused = false;
            SetVisible(false);
            Time.timeScale = 1f;
            AudioListener.pause = false;

            int buildIndex = SceneUtility.GetBuildIndexByScenePath(MainMenuScenePath);
            if (buildIndex >= 0)
                SceneManager.LoadScene(buildIndex);
            else
                SceneManager.LoadScene(MainMenuSceneName);
        }

        void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        void SetVisible(bool visible)
        {
            if (root != null) root.SetActive(visible);
        }

        void BuildUI()
        {
            if (canvas != null && root != null) return;

            canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = short.MaxValue - 2;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>();

            root = new GameObject("PauseRoot", typeof(RectTransform));
            root.transform.SetParent(transform, false);
            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            // Fully OPAQUE black background — gameplay is completely hidden behind.
            GameObject dimObj = new GameObject("Dim");
            dimObj.transform.SetParent(root.transform, false);
            var dim = dimObj.AddComponent<Image>();
            dim.color = new Color(0f, 0f, 0f, 1f);
            dim.raycastTarget = false; // never intercept button clicks
            var dimRect = dim.rectTransform;
            dimRect.anchorMin = Vector2.zero;
            dimRect.anchorMax = Vector2.one;
            dimRect.offsetMin = Vector2.zero;
            dimRect.offsetMax = Vector2.zero;

            // Subtle blood-red vignette across the whole screen for atmosphere.
            GameObject vignetteObj = new GameObject("Vignette");
            vignetteObj.transform.SetParent(root.transform, false);
            var vignette = vignetteObj.AddComponent<Image>();
            vignette.color = new Color(0.07f, 0f, 0f, 0.35f);
            vignette.raycastTarget = false;
            var vignetteRect = vignette.rectTransform;
            vignetteRect.anchorMin = Vector2.zero;
            vignetteRect.anchorMax = Vector2.one;
            vignetteRect.offsetMin = Vector2.zero;
            vignetteRect.offsetMax = Vector2.zero;

            // Centered card panel — black with stacked red glow outlines (horror style).
            var cardObj = new GameObject("Card", typeof(RectTransform));
            cardObj.transform.SetParent(root.transform, false);
            var cardRect = cardObj.GetComponent<RectTransform>();
            cardRect.anchorMin = new Vector2(0.5f, 0.5f);
            cardRect.anchorMax = new Vector2(0.5f, 0.5f);
            cardRect.pivot = new Vector2(0.5f, 0.5f);
            cardRect.sizeDelta = new Vector2(620f, 720f);
            var card = cardObj.AddComponent<Image>();
            card.color = new Color(0.03f, 0.03f, 0.04f, 1f);
            card.raycastTarget = false; // background graphic, buttons handle their own clicks

            var cardGlowInner = cardObj.AddComponent<Outline>();
            cardGlowInner.effectColor = new Color(0.85f, 0.10f, 0.10f, 0.65f);
            cardGlowInner.effectDistance = new Vector2(2f, -2f);
            var cardGlowOuter = cardObj.AddComponent<Outline>();
            cardGlowOuter.effectColor = new Color(0.45f, 0.02f, 0.02f, 0.40f);
            cardGlowOuter.effectDistance = new Vector2(4f, -4f);

            CreateAccentBar(cardObj.transform, top: true);
            CreateAccentBar(cardObj.transform, top: false);

            // Small subtitle for atmosphere
            CreateText(cardObj.transform, "Subtitle", "GAME  HALTED", 18, FontStyle.Italic,
                TextAnchor.MiddleCenter, new Color(0.65f, 0.18f, 0.18f, 0.85f),
                new Vector2(0f, 0.87f), new Vector2(1f, 0.93f),
                Vector2.zero, Vector2.zero);

            // Title with stacked outlines for blood-glow + flicker animation
            titleText = CreateText(cardObj.transform, "Title", "PAUSED", 78, FontStyle.Bold,
                TextAnchor.MiddleCenter, new Color(0.92f, 0.18f, 0.18f, 1f),
                new Vector2(0f, 0.74f), new Vector2(1f, 0.86f),
                Vector2.zero, Vector2.zero);
            AddBloodGlow(titleText.gameObject);
            titleText.gameObject.AddComponent<HorrorTitleFlicker>();

            // Creepy quote between title and buttons
            CreateText(cardObj.transform, "Quote",
                "\"He's still listening...\"",
                22, FontStyle.Italic, TextAnchor.MiddleCenter,
                new Color(0.78f, 0.78f, 0.82f, 0.85f),
                new Vector2(0f, 0.65f), new Vector2(1f, 0.72f),
                Vector2.zero, Vector2.zero);

            // Three buttons. RESUME = primary (red), RESTART = secondary (cream),
            // MAIN MENU = tertiary (muted) — visual hierarchy via accent colors.
            CreateButton(cardObj.transform, "RESUME  (Tab)",
                new Vector2(0.08f, 0.50f), new Vector2(0.92f, 0.62f),
                () => { if (isPaused) TogglePause(); },
                new Color(0.92f, 0.18f, 0.18f, 0.9f));   // primary red
            CreateButton(cardObj.transform, "RESTART  (R)",
                new Vector2(0.08f, 0.36f), new Vector2(0.92f, 0.48f),
                RestartCurrentScene,
                new Color(0.95f, 0.65f, 0.10f, 0.75f));  // amber
            CreateButton(cardObj.transform, "MAIN MENU",
                new Vector2(0.08f, 0.22f), new Vector2(0.92f, 0.34f),
                GoToMainMenu,
                new Color(0.55f, 0.55f, 0.62f, 0.6f));   // muted gray

            // Compact controls hint at the card footer
            hintText = CreateText(cardObj.transform, "Hint",
                "WASD Move   ·   Space Jump   ·   Mouse Look   ·   E Interact   ·   F Hide",
                15, FontStyle.Normal, TextAnchor.MiddleCenter,
                new Color(0.55f, 0.55f, 0.62f, 0.85f),
                new Vector2(0.04f, 0.04f), new Vector2(0.96f, 0.13f),
                Vector2.zero, Vector2.zero);
        }

        static void AddBloodGlow(GameObject go)
        {
            var a = go.AddComponent<Outline>();
            a.effectColor = new Color(0.85f, 0.05f, 0.05f, 0.85f);
            a.effectDistance = new Vector2(2f, -2f);
            var b = go.AddComponent<Outline>();
            b.effectColor = new Color(0.50f, 0.02f, 0.02f, 0.7f);
            b.effectDistance = new Vector2(4f, -4f);
            var drop = go.AddComponent<Shadow>();
            drop.effectColor = new Color(0f, 0f, 0f, 0.95f);
            drop.effectDistance = new Vector2(5f, -5f);
        }

        static void CreateAccentBar(Transform parent, bool top)
        {
            var barObj = new GameObject(top ? "AccentTop" : "AccentBottom", typeof(RectTransform));
            barObj.transform.SetParent(parent, false);
            var rect = barObj.GetComponent<RectTransform>();
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
            var img = barObj.AddComponent<Image>();
            img.color = new Color(0.85f, 0.10f, 0.10f, 0.9f);
            img.raycastTarget = false;
        }

        void CreateButton(Transform parent, string label,
            Vector2 anchorMin, Vector2 anchorMax,
            UnityEngine.Events.UnityAction onClick,
            Color? accentColor = null)
        {
            var btnObj = new GameObject(label + "Button", typeof(RectTransform));
            btnObj.transform.SetParent(parent, false);
            var rect = btnObj.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = new Vector2(8f, 6f);
            rect.offsetMax = new Vector2(-8f, -6f);

            var bg = btnObj.AddComponent<Image>();
            Sprite buttonSprite = DarkButtonSprites.GetSprite(0);
            if (buttonSprite != null)
            {
                bg.sprite = buttonSprite;
                bg.type = Image.Type.Sliced;
                bg.color = Color.white;
            }
            else
            {
                bg.color = new Color(0.13f, 0.13f, 0.16f, 1f);
            }
            bg.raycastTarget = true; // explicit — must catch the click

            // Per-button accent (visual hierarchy: primary/secondary/tertiary).
            Color accent = accentColor ?? new Color(0.85f, 0.10f, 0.10f, 0.85f);
            Color hover = new Color(
                Mathf.Min(1.5f, 1f + accent.r * 0.4f),
                Mathf.Min(1.5f, 0.55f + accent.g * 0.3f),
                Mathf.Min(1.5f, 0.55f + accent.b * 0.3f), 1f);

            var btn = btnObj.AddComponent<Button>();
            btn.targetGraphic = bg;            // required for color transitions to render
            btn.transition = Selectable.Transition.ColorTint;
            btn.interactable = true;
            var colors = btn.colors;
            colors.normalColor      = new Color(1f, 1f, 1f, 1f);
            colors.highlightedColor = hover;
            colors.pressedColor     = new Color(accent.r * 0.75f, accent.g * 0.75f, accent.b * 0.75f, 1f);
            colors.selectedColor    = hover;
            colors.disabledColor    = new Color(0.30f, 0.30f, 0.30f, 0.6f);
            colors.colorMultiplier  = 1f;
            colors.fadeDuration     = 0.10f;
            btn.colors = colors;
            btn.onClick.AddListener(onClick);

            var labelText = CreateText(btnObj.transform, "Label", label, 28, FontStyle.Bold,
                TextAnchor.MiddleCenter, new Color(0.96f, 0.94f, 0.88f, 1f),
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            labelText.raycastTarget = false;

            var labelShadow = labelText.gameObject.AddComponent<Shadow>();
            labelShadow.effectColor = new Color(0f, 0f, 0f, 0.95f);
            labelShadow.effectDistance = new Vector2(2f, -2f);
        }

        static Text CreateText(
            Transform parent,
            string name,
            string text,
            int fontSize,
            FontStyle style,
            TextAnchor anchor,
            Color color,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 offsetMin,
            Vector2 offsetMax)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            Text t = obj.AddComponent<Text>();
            t.font = CreateSpookyFont(fontSize);
            t.fontSize = fontSize;
            t.fontStyle = style;
            t.alignment = anchor;
            t.color = color;
            t.text = text;
            t.resizeTextForBestFit = true;
            t.resizeTextMinSize = Mathf.Max(14, Mathf.RoundToInt(fontSize * 0.55f));
            t.resizeTextMaxSize = fontSize;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Truncate;

            RectTransform rect = t.rectTransform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            return t;
        }

        static Font CreateSpookyFont(int size)
        {
            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        bool WasPausePressed()
        {
            bool pressed = false;
#if ENABLE_LEGACY_INPUT_MANAGER
            pressed |= Input.GetKeyDown(KeyCode.Tab);
#endif
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
                pressed |= Keyboard.current.tabKey.wasPressedThisFrame;
#endif
            return pressed;
        }

        bool WasRestartPressed()
        {
            bool pressed = false;
#if ENABLE_LEGACY_INPUT_MANAGER
            pressed |= Input.GetKeyDown(KeyCode.R);
#endif
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null) pressed |= Keyboard.current.rKey.wasPressedThisFrame;
#endif
            return pressed;
        }
    }
}
