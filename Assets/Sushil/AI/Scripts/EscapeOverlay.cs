using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Sushil.Systems
{
    public static class EscapeOverlay
    {
        static GameObject root;
        static Text title;
        static Text quote;
        static Text hint;
        static EscapeOverlayDriver driver;
        static readonly string[] quotes =
        {
            "He heard your footsteps. He missed your final one.",
            "The Resident blinked. You didn't.",
            "Doors are loud. Freedom is louder.",
            "You left with your pulse. He kept the silence."
        };

        public static bool IsShowing => driver != null && driver.IsShowing;

        public static void Show()
        {
            EnsureCreated();
            if (root == null) return;

            GameAnalyticsTracker.RegisterEscape();
            if (title != null) title.text = "CONGRATS,\nYOU ESCAPED";
            if (quote != null) quote.text = quotes[Random.Range(0, quotes.Length)];
            if (hint != null) hint.text = "Press R to play again";

            root.SetActive(true);
            if (driver != null) driver.IsShowing = true;

            Time.timeScale = 0f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        public static void Hide()
        {
            if (root != null) root.SetActive(false);
            if (driver != null) driver.IsShowing = false;
        }

        static void EnsureCreated()
        {
            if (root != null && title != null && quote != null && hint != null) return;

            var canvasObj = new GameObject("EscapeCanvas");
            Object.DontDestroyOnLoad(canvasObj);

            var canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = short.MaxValue - 1;

            var scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            canvasObj.AddComponent<GraphicRaycaster>();

            var dimObj = new GameObject("Dim");
            dimObj.transform.SetParent(canvasObj.transform, false);
            var dim = dimObj.AddComponent<Image>();
            dim.color = new Color(0.01f, 0.08f, 0.03f, 0.92f);
            var dimRect = dim.rectTransform;
            dimRect.anchorMin = Vector2.zero;
            dimRect.anchorMax = Vector2.one;
            dimRect.offsetMin = Vector2.zero;
            dimRect.offsetMax = Vector2.zero;

            title = CreateText(dimObj.transform, "Title", 150, FontStyle.Bold,
                new Color(0.72f, 1f, 0.82f, 1f), TextAnchor.MiddleCenter,
                new Vector2(0f, 0.56f), new Vector2(1f, 1f), new Vector2(40f, -70f), new Vector2(-40f, -40f));
            quote = CreateText(dimObj.transform, "Quote", 52, FontStyle.Italic,
                new Color(0.92f, 1f, 0.95f, 1f), TextAnchor.MiddleCenter,
                new Vector2(0.08f, 0.30f), new Vector2(0.92f, 0.56f), Vector2.zero, Vector2.zero);
            hint = CreateText(dimObj.transform, "Hint", 44, FontStyle.Bold,
                new Color(1f, 0.95f, 0.72f, 1f), TextAnchor.LowerCenter,
                new Vector2(0f, 0.04f), new Vector2(1f, 0.26f), new Vector2(30f, 20f), new Vector2(-30f, 0f));

            var titleOutline = title.gameObject.AddComponent<Outline>();
            titleOutline.effectColor = new Color(0f, 0.2f, 0.08f, 0.95f);
            titleOutline.effectDistance = new Vector2(4f, -4f);

            var titleShadow = title.gameObject.AddComponent<Shadow>();
            titleShadow.effectColor = new Color(0f, 0f, 0f, 0.8f);
            titleShadow.effectDistance = new Vector2(5f, -5f);

            driver = canvasObj.AddComponent<EscapeOverlayDriver>();

            root = canvasObj;
            root.SetActive(false);
        }

        static Text CreateText(
            Transform parent,
            string name,
            int size,
            FontStyle style,
            Color color,
            TextAnchor anchor,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 offsetMin,
            Vector2 offsetMax)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<Text>();
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.fontSize = size;
            t.fontStyle = style;
            t.color = color;
            t.alignment = anchor;
            t.resizeTextForBestFit = true;
            t.resizeTextMinSize = Mathf.Max(16, Mathf.RoundToInt(size * 0.4f));
            t.resizeTextMaxSize = size;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Truncate;
            t.text = string.Empty;

            var rect = t.rectTransform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            return t;
        }

        class EscapeOverlayDriver : MonoBehaviour
        {
            public bool IsShowing { get; set; }

            void Update()
            {
                if (!IsShowing) return;

                if (title != null)
                {
                    float pulse = 0.85f + 0.15f * Mathf.Sin(Time.unscaledTime * 3.6f);
                    title.color = new Color(0.72f * pulse, 1f, 0.82f * pulse, 1f);
                }

                if (WasRestartPressed())
                    Restart();
            }

            void Restart()
            {
                IsShowing = false;
                if (root != null) root.SetActive(false);
                Time.timeScale = 1f;
                var active = SceneManager.GetActiveScene();
                SceneManager.LoadScene(active.buildIndex);
            }
        }

        static bool WasRestartPressed()
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
