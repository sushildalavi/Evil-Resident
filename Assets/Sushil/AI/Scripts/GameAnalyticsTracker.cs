using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Sushil.Systems
{
    public static class GameAnalyticsTracker
    {
        static bool runActive;
        static float runStartTime;
        static int deathsThisRun;
        static int totalDeaths;

        public static bool RunActive => runActive;
        public static int DeathsThisRun => deathsThisRun;
        public static int TotalDeaths => totalDeaths;
        public static float ElapsedSeconds => runActive ? Mathf.Max(0f, Time.time - runStartTime) : 0f;

        public static void BeginRun()
        {
            runActive = true;
            runStartTime = Time.time;
            deathsThisRun = 0;
            Initialize();
        }

        public static void Initialize()
        {
        }

        public static void RegisterDeath(string reason)
        {
            if (!runActive) return;
            deathsThisRun++;
            totalDeaths++;
        }

        public static void RegisterEscape()
        {
            if (!runActive) return;
            runActive = false;
        }
    }

    public class GameAnalyticsHUD : MonoBehaviour
    {
        static GameAnalyticsHUD instance;
        Text hudText;
        Canvas canvas;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (instance != null) return;
            var go = new GameObject("GameAnalyticsHUD");
            instance = go.AddComponent<GameAnalyticsHUD>();
            DontDestroyOnLoad(go);
            GameAnalyticsTracker.Initialize();
        }

        void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            BuildUI();
        }

        void Update()
        {
            if (ShouldHideForScene(SceneManager.GetActiveScene().name) ||
                StartScreenOverlay.IsShowing ||
                PauseOverlay.IsPaused ||
                GameOverOverlay.IsShowing ||
                EscapeOverlay.IsShowing ||
                !GameAnalyticsTracker.RunActive)
            {
                if (canvas != null) canvas.enabled = false;
                return;
            }

            if (canvas != null) canvas.enabled = true;
            if (hudText == null) return;

            TimeSpan t = TimeSpan.FromSeconds(GameAnalyticsTracker.ElapsedSeconds);
            hudText.text = $"Time: {t.Minutes:00}:{t.Seconds:00}  Deaths: {GameAnalyticsTracker.TotalDeaths}";
        }

        void BuildUI()
        {
            canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = short.MaxValue - 40;

            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>();

            var textObj = new GameObject("AnalyticsText");
            textObj.transform.SetParent(canvas.transform, false);
            hudText = textObj.AddComponent<Text>();
            hudText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            hudText.fontSize = 28;
            hudText.fontStyle = FontStyle.Bold;
            hudText.alignment = TextAnchor.UpperRight;
            hudText.color = new Color(0.95f, 0.95f, 0.95f, 0.95f);

            var shadow = textObj.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.9f);
            shadow.effectDistance = new Vector2(2f, -2f);

            RectTransform rect = hudText.rectTransform;
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-20f, -20f);
            rect.sizeDelta = new Vector2(520f, 80f);

            canvas.enabled = false;
        }

        static bool ShouldHideForScene(string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
                return false;

            return sceneName == "Level Select" ||
                   sceneName == "New Tutorial 1" ||
                   sceneName == "New Tutorial 2" ||
                   sceneName == "New Tutorial 3";
        }
    }
}
