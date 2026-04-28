using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Sushil.Systems
{
    public static class GameOverOverlay
    {
        static GameObject overlayRoot;
        static Text titleText;
        static Text reasonText;
        static Text hintText;
        static Image cardImage;
        static GameOverOverlayDriver driver;
        public static bool IsShowing => driver != null && driver.IsShowing;

        public static void Show(string reason = "You were caught")
        {
            EnsureCreated();
            if (overlayRoot == null || titleText == null || reasonText == null || hintText == null) return;

            GameAnalyticsTracker.RegisterDeath(reason);
            ApplyLargeStyle();

            titleText.text = "GAME\nOVER";
            reasonText.text = BuildReasonText(reason);
            hintText.text = "Press R to Restart";
            Time.timeScale = 0f;
            AudioListener.pause = true;
            overlayRoot.SetActive(true);
            if (driver != null) driver.IsShowing = true;
        }

        public static void Hide()
        {
            if (overlayRoot != null) overlayRoot.SetActive(false);
            if (driver != null) driver.IsShowing = false;
            Time.timeScale = 1f;
            AudioListener.pause = false;
        }

        static void EnsureCreated()
        {
            if (overlayRoot != null && titleText != null && reasonText != null && hintText != null && cardImage != null) return;

            GameObject canvasObj = new GameObject("GameOverCanvas");
            Object.DontDestroyOnLoad(canvasObj);
            var canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = short.MaxValue;

            var scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            canvasObj.AddComponent<GraphicRaycaster>();

            GameObject dimObj = new GameObject("Dim");
            dimObj.transform.SetParent(canvasObj.transform, false);
            var dim = dimObj.AddComponent<Image>();
            dim.color = new Color(0f, 0f, 0f, 1f);
            var dimRect = dim.rectTransform;
            dimRect.anchorMin = Vector2.zero;
            dimRect.anchorMax = Vector2.one;
            dimRect.offsetMin = Vector2.zero;
            dimRect.offsetMax = Vector2.zero;

            GameObject cardObj = new GameObject("Card");
            cardObj.transform.SetParent(dimObj.transform, false);
            cardImage = cardObj.AddComponent<Image>();
            cardImage.color = new Color(0.08f, 0.01f, 0.02f, 0.98f);
            var cardRect = cardImage.rectTransform;
            cardRect.anchorMin = Vector2.zero;
            cardRect.anchorMax = Vector2.one;
            cardRect.pivot = new Vector2(0.5f, 0.5f);
            cardRect.offsetMin = Vector2.zero;
            cardRect.offsetMax = Vector2.zero;

            GameObject titleObj = new GameObject("Title");
            titleObj.transform.SetParent(cardObj.transform, false);
            titleText = titleObj.AddComponent<Text>();
            titleText.font = CreateSpookyFont(180);
            titleText.fontStyle = FontStyle.Bold;
            titleText.alignment = TextAnchor.MiddleCenter;
            titleText.color = new Color(1f, 0.08f, 0.08f, 1f);
            titleText.resizeTextForBestFit = true;
            titleText.resizeTextMinSize = 54;
            titleText.resizeTextMaxSize = 180;
            titleText.horizontalOverflow = HorizontalWrapMode.Wrap;
            titleText.verticalOverflow = VerticalWrapMode.Truncate;
            var titleRect = titleText.rectTransform;
            titleRect.anchorMin = new Vector2(0f, 0.5f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot = new Vector2(0.5f, 0.5f);
            titleRect.offsetMin = new Vector2(20f, -30f);
            titleRect.offsetMax = new Vector2(-20f, -20f);
            var titleShadow = titleObj.AddComponent<Shadow>();
            titleShadow.effectColor = new Color(0f, 0f, 0f, 0.85f);
            titleShadow.effectDistance = new Vector2(7f, -7f);
            var titleOutline = titleObj.AddComponent<Outline>();
            titleOutline.effectColor = new Color(0.25f, 0f, 0f, 1f);
            titleOutline.effectDistance = new Vector2(5f, -5f);

            GameObject reasonObj = new GameObject("Reason");
            reasonObj.transform.SetParent(cardObj.transform, false);
            reasonText = reasonObj.AddComponent<Text>();
            reasonText.font = CreateSpookyFont(70);
            reasonText.alignment = TextAnchor.MiddleCenter;
            reasonText.color = new Color(1f, 0.9f, 0.9f, 1f);
            reasonText.resizeTextForBestFit = true;
            reasonText.resizeTextMinSize = 24;
            reasonText.resizeTextMaxSize = 70;
            reasonText.horizontalOverflow = HorizontalWrapMode.Wrap;
            reasonText.verticalOverflow = VerticalWrapMode.Truncate;
            var reasonRect = reasonText.rectTransform;
            reasonRect.anchorMin = new Vector2(0f, 0.2f);
            reasonRect.anchorMax = new Vector2(1f, 0.5f);
            reasonRect.pivot = new Vector2(0.5f, 0.5f);
            reasonRect.offsetMin = new Vector2(60f, -10f);
            reasonRect.offsetMax = new Vector2(-60f, 6f);
            var reasonShadow = reasonObj.AddComponent<Shadow>();
            reasonShadow.effectColor = new Color(0f, 0f, 0f, 0.75f);
            reasonShadow.effectDistance = new Vector2(3f, -3f);

            GameObject hintObj = new GameObject("Hint");
            hintObj.transform.SetParent(cardObj.transform, false);
            hintText = hintObj.AddComponent<Text>();
            hintText.font = CreateSpookyFont(44);
            hintText.alignment = TextAnchor.LowerCenter;
            hintText.color = new Color(1f, 0.85f, 0.55f, 1f);
            hintText.resizeTextForBestFit = true;
            hintText.resizeTextMinSize = 18;
            hintText.resizeTextMaxSize = 44;
            hintText.horizontalOverflow = HorizontalWrapMode.Wrap;
            hintText.verticalOverflow = VerticalWrapMode.Truncate;
            var hintRect = hintText.rectTransform;
            hintRect.anchorMin = new Vector2(0f, 0f);
            hintRect.anchorMax = new Vector2(1f, 0.2f);
            hintRect.pivot = new Vector2(0.5f, 0f);
            hintRect.offsetMin = new Vector2(20f, 20f);
            hintRect.offsetMax = new Vector2(-20f, 40f);

            driver = canvasObj.AddComponent<GameOverOverlayDriver>();

            overlayRoot = canvasObj;
            overlayRoot.SetActive(false);
        }

        static void ApplyLargeStyle()
        {
            if (titleText == null || reasonText == null || hintText == null) return;

            titleText.font = CreateSpookyFont(200);
            titleText.fontSize = 200;
            reasonText.font = CreateSpookyFont(74);
            reasonText.fontSize = 74;
            hintText.font = CreateSpookyFont(44);
            hintText.fontSize = 44;

            var titleRect = titleText.rectTransform;
            titleRect.anchorMin = new Vector2(0f, 0.5f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.offsetMin = new Vector2(20f, -30f);
            titleRect.offsetMax = new Vector2(-20f, -20f);

            var reasonRect = reasonText.rectTransform;
            reasonRect.anchorMin = new Vector2(0f, 0.2f);
            reasonRect.anchorMax = new Vector2(1f, 0.5f);
            reasonRect.offsetMin = new Vector2(60f, 0f);
            reasonRect.offsetMax = new Vector2(-60f, 12f);

            var hintRect = hintText.rectTransform;
            hintRect.anchorMin = new Vector2(0f, 0f);
            hintRect.anchorMax = new Vector2(1f, 0.2f);
            hintRect.offsetMin = new Vector2(20f, 20f);
            hintRect.offsetMax = new Vector2(-20f, 42f);

            if (cardImage != null)
            {
                cardImage.color = new Color(0.08f, 0.01f, 0.02f, 0.98f);
            }
        }

        class GameOverOverlayDriver : MonoBehaviour
        {
            public bool IsShowing { get; set; }

            void Update()
            {
                if (!IsShowing) return;

                // Subtle pulse to keep the death screen feeling threatening.
                if (titleText != null)
                {
                    float t = 0.78f + 0.22f * Mathf.Sin(Time.unscaledTime * 4.5f);
                    titleText.color = new Color(1f, 0.03f + 0.1f * t, 0.03f + 0.1f * t, 1f);
                }

                if (cardImage != null)
                {
                    float pulse = 0.88f + 0.12f * Mathf.Sin(Time.unscaledTime * 2.2f);
                    cardImage.color = new Color(0.08f * pulse, 0.01f, 0.02f, 0.98f);
                }

                if (WasRestartPressed())
                    RestartCurrentScene();
            }

            void RestartCurrentScene()
            {
                IsShowing = false;
                if (overlayRoot != null) overlayRoot.SetActive(false);
                Time.timeScale = 1f;
                AudioListener.pause = false;

                Scene active = SceneManager.GetActiveScene();
                SceneManager.LoadScene(active.buildIndex);
            }
        }

        static string BuildReasonText(string reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
                return "The Resident caught you.";

            string lower = reason.ToLowerInvariant();
            if (lower.Contains("resident"))
                return "The Resident caught you.";

            return reason;
        }

        static Font CreateSpookyFont(int size)
        {
            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
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
