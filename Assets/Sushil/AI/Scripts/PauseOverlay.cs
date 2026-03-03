using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Sushil.Systems
{
    public class PauseOverlay : MonoBehaviour
    {
        static PauseOverlay instance;
        public static bool IsPaused => instance != null && instance.isPaused;

        Canvas canvas;
        GameObject root;
        Text titleText;
        Text bodyText;
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
            if (WasEscapePressed())
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
                Time.timeScale = 0f;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else
            {
                Time.timeScale = 1f;
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        void RestartCurrentScene()
        {
            isPaused = false;
            SetVisible(false);
            Time.timeScale = 1f;

            Scene active = SceneManager.GetActiveScene();
            SceneManager.LoadScene(active.buildIndex);
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

            root = new GameObject("PauseRoot");
            root.transform.SetParent(transform, false);

            GameObject dimObj = new GameObject("Dim");
            dimObj.transform.SetParent(root.transform, false);
            var dim = dimObj.AddComponent<Image>();
            dim.color = new Color(0f, 0f, 0f, 0.75f);
            var dimRect = dim.rectTransform;
            dimRect.anchorMin = Vector2.zero;
            dimRect.anchorMax = Vector2.one;
            dimRect.offsetMin = Vector2.zero;
            dimRect.offsetMax = Vector2.zero;

            GameObject cardObj = new GameObject("Card");
            cardObj.transform.SetParent(root.transform, false);
            var card = cardObj.AddComponent<Image>();
            card.color = new Color(0.08f, 0.08f, 0.11f, 0.96f);
            var cardRect = card.rectTransform;
            cardRect.anchorMin = new Vector2(0.5f, 0.5f);
            cardRect.anchorMax = new Vector2(0.5f, 0.5f);
            cardRect.pivot = new Vector2(0.5f, 0.5f);
            cardRect.sizeDelta = new Vector2(760f, 420f);

            titleText = CreateText(card.transform, "Title", "PAUSED", 82, FontStyle.Bold,
                TextAnchor.UpperCenter, new Color(1f, 0.25f, 0.25f, 1f),
                new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(20f, -130f), new Vector2(-20f, -20f));

            bodyText = CreateText(card.transform, "Body",
                "Esc  Resume   |   R  Restart\n\n" +
                "WASD Move   Shift Sprint   Space Jump   Mouse Look\n" +
                "F Pick Key   E Interact / Hide   N Make Noise",
                26, FontStyle.Normal, TextAnchor.MiddleCenter, Color.white,
                new Vector2(0f, 0f), new Vector2(1f, 1f),
                new Vector2(30f, 30f), new Vector2(-30f, -120f));
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

        bool WasEscapePressed()
        {
            bool pressed = false;
#if ENABLE_LEGACY_INPUT_MANAGER
            pressed |= Input.GetKeyDown(KeyCode.Escape);
#endif
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null) pressed |= Keyboard.current.escapeKey.wasPressedThisFrame;
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
