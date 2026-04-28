using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Sushil.Systems
{
    // Tiny "Press Q for Main Menu" hint pinned to the bottom-right corner.
    // Always available in gameplay + tutorial scenes; suppressed in menu scenes
    // and while pause / game-over / escape / start-screen overlays are up.
    // WebGL-safe — pure built-in components, no external assets.
    public class MainMenuHint : MonoBehaviour
    {
        const string MainMenuScenePath = "Assets/Sahil/Tutorial/Level Select.unity";
        const string MainMenuSceneName = "Level Select";

        static MainMenuHint instance;

        Canvas canvas;
        GameObject root;
        Text hintText;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (instance != null) return;
            var go = new GameObject("MainMenuHint");
            instance = go.AddComponent<MainMenuHint>();
        }

        void Awake()
        {
            if (instance != null && instance != this) { Destroy(gameObject); return; }
            instance = this;
            DontDestroyOnLoad(gameObject);
            BuildUI();
            SceneManager.sceneLoaded += OnSceneLoaded;
            RefreshVisibility();
        }

        void OnDestroy()
        {
            if (instance == this) instance = null;
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            RefreshVisibility();
        }

        void Update()
        {
            // Continuously update visibility so the hint hides during pauses, etc.
            bool shouldShow = ShouldShow();
            if (root != null && root.activeSelf != shouldShow)
                root.SetActive(shouldShow);

            if (!shouldShow) return;

            if (WasMainMenuPressed())
                GoToMainMenu();
        }

        bool ShouldShow()
        {
            if (PauseOverlay.IsPaused) return false;
            if (StartScreenOverlay.IsShowing) return false;
            if (GameOverOverlay.IsShowing) return false;
            if (EscapeOverlay.IsShowing) return false;
            return !IsMenuScene(SceneManager.GetActiveScene());
        }

        void RefreshVisibility()
        {
            if (root != null) root.SetActive(ShouldShow());
        }

        static bool IsMenuScene(Scene scene)
        {
            if (scene == null || string.IsNullOrEmpty(scene.name)) return false;
            string n = scene.name.ToLowerInvariant();
            return n == "level select" ||
                   n == "difficulty select" ||
                   n == "main menu" ||
                   n.Contains("mainmenu");
        }

        void GoToMainMenu()
        {
            // Make sure time/cursor are restored before scene swap.
            Time.timeScale = 1f;
            AudioListener.pause = false;
            int idx = SceneUtility.GetBuildIndexByScenePath(MainMenuScenePath);
            if (idx >= 0)
                SceneManager.LoadScene(idx);
            else
                SceneManager.LoadScene(MainMenuSceneName);
        }

        static bool WasMainMenuPressed()
        {
            bool pressed = false;
#if ENABLE_LEGACY_INPUT_MANAGER
            pressed |= Input.GetKeyDown(KeyCode.Q);
#endif
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null) pressed |= Keyboard.current.qKey.wasPressedThisFrame;
#endif
            return pressed;
        }

        void BuildUI()
        {
            canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // Below the pause overlay (sortingOrder MaxValue - 2) but above gameplay HUD.
            canvas.sortingOrder = short.MaxValue - 6;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>();

            root = new GameObject("HintRoot", typeof(RectTransform));
            root.transform.SetParent(transform, false);
            var rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(1f, 0f);
            rootRect.anchorMax = new Vector2(1f, 0f);
            rootRect.pivot = new Vector2(1f, 0f);
            rootRect.anchoredPosition = new Vector2(-16f, 12f);
            rootRect.sizeDelta = new Vector2(240f, 44f); // taller to fit two lines

            // "Press Tab to Pause" sits above "Press Q for Main Menu".
            var pauseHint = CreateHintLine(root.transform, "PauseHint",
                "Press  Tab  to Pause", new Vector2(0f, 22f));
            hintText = CreateHintLine(root.transform, "MainMenuHint",
                "Press  Q  for Main Menu", new Vector2(0f, 0f));
        }

        Text CreateHintLine(Transform parent, string name, string label, Vector2 anchoredPosition)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(1f, 0f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = new Vector2(240f, 20f);

            var text = go.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = label;
            text.fontSize = 14;
            text.fontStyle = FontStyle.Italic;
            text.alignment = TextAnchor.LowerRight;
            text.color = new Color(0.75f, 0.75f, 0.78f, 0.55f);
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;

            var shadow = go.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.85f);
            shadow.effectDistance = new Vector2(1f, -1f);

            return text;
        }
    }
}
