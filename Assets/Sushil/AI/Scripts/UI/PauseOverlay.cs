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

        void RestartCurrentScene()
        {
            isPaused = false;
            SetVisible(false);
            Time.timeScale = 1f;
            AudioListener.pause = false;

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

            root = new GameObject("PauseRoot", typeof(RectTransform));
            root.transform.SetParent(transform, false);
            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            GameObject dimObj = new GameObject("Dim");
            dimObj.transform.SetParent(root.transform, false);
            var dim = dimObj.AddComponent<Image>();
            dim.color = new Color(0.01f, 0.01f, 0.02f, 1f);
            var dimRect = dim.rectTransform;
            dimRect.anchorMin = Vector2.zero;
            dimRect.anchorMax = Vector2.one;
            dimRect.offsetMin = Vector2.zero;
            dimRect.offsetMax = Vector2.zero;

            titleText = CreateText(root.transform, "Title", "PAUSED", 92, FontStyle.Bold,
                TextAnchor.MiddleCenter, new Color(1f, 0.25f, 0.25f, 1f),
                new Vector2(0.15f, 0.60f), new Vector2(0.85f, 0.78f),
                Vector2.zero, Vector2.zero);

            bodyText = CreateText(root.transform, "Body",
                "Tab  Resume   |   R  Restart\n\n" +
                "WASD Move   Mouse Look\n" +
                "E Pick Up / Interact   F Hide",
                26, FontStyle.Normal, TextAnchor.MiddleCenter, Color.white,
                new Vector2(0.20f, 0.28f), new Vector2(0.80f, 0.56f),
                Vector2.zero, Vector2.zero);
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
            if (Keyboard.current != null) pressed |= Keyboard.current.tabKey.wasPressedThisFrame;
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
