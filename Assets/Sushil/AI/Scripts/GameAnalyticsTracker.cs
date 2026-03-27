using System;
using System.Globalization;
using System.IO;
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
        static string filePath;
        static bool pathLogged;

        public static bool RunActive => runActive;
        public static int DeathsThisRun => deathsThisRun;
        public static int TotalDeaths => totalDeaths;
        public static float ElapsedSeconds => runActive ? Mathf.Max(0f, Time.time - runStartTime) : 0f;
        public static string AnalyticsPath
        {
            get
            {
                if (!string.IsNullOrEmpty(filePath)) return filePath;

                string analyticsDir = Path.Combine(Application.dataPath, "Sushil", "Analytics");
                filePath = Path.Combine(analyticsDir, "ResidentAnalytics.csv");
                return filePath;
            }
        }

        public static void BeginRun()
        {
            runActive = true;
            runStartTime = Time.time;
            deathsThisRun = 0;
            Initialize();
        }

        public static void Initialize()
        {
            EnsureCsvHeader();
        }

        public static void RegisterDeath(string reason)
        {
            if (!runActive) return;
            deathsThisRun++;
            totalDeaths++;
            AppendRow("death", reason);
        }

        public static void RegisterEscape()
        {
            if (!runActive) return;
            AppendRow("escape", "main_door_unlocked");
            runActive = false;
        }

        static void EnsureCsvHeader()
        {
            EnsureWritablePath();

            if (File.Exists(AnalyticsPath)) return;
            string header = "timestamp,scene,event,elapsed_seconds,deaths_this_run,total_deaths,details";
            File.WriteAllText(AnalyticsPath, header + Environment.NewLine);
        }

        static void EnsureWritablePath()
        {
            string dir = Path.GetDirectoryName(AnalyticsPath);
            if (string.IsNullOrEmpty(dir)) return;

            try
            {
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
            }
            catch (Exception ex)
            {
                // Build outputs can be read-only near Application.dataPath.
                string fallbackDir = Path.Combine(Application.persistentDataPath, "Sushil", "Analytics");
                Directory.CreateDirectory(fallbackDir);
                filePath = Path.Combine(fallbackDir, "ResidentAnalytics.csv");
                Debug.LogWarning($"[Analytics] Project path not writable, using fallback path: {filePath}. Reason: {ex.Message}");
            }

            if (!pathLogged)
            {
                pathLogged = true;
                Debug.Log($"[Analytics] Analytics file path: {AnalyticsPath}");
            }
        }

        static void AppendRow(string evt, string details)
        {
            EnsureCsvHeader();

            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
            string scene = SceneManager.GetActiveScene().name;
            string elapsed = ElapsedSeconds.ToString("F2", CultureInfo.InvariantCulture);

            string row = string.Join(",",
                SanitizeForCsv(timestamp),
                SanitizeForCsv(scene),
                SanitizeForCsv(evt),
                elapsed,
                deathsThisRun.ToString(CultureInfo.InvariantCulture),
                totalDeaths.ToString(CultureInfo.InvariantCulture),
                SanitizeForCsv(details));

            File.AppendAllText(AnalyticsPath, row + Environment.NewLine);
        }

        static string SanitizeForCsv(string value)
        {
            if (string.IsNullOrEmpty(value)) return "\"\"";
            string v = value.Replace("\r", " ").Replace("\n", " ").Replace("\"", "\"\"");
            return "\"" + v + "\"";
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
            if (StartScreenOverlay.IsShowing || PauseOverlay.IsPaused || GameOverOverlay.IsShowing || EscapeOverlay.IsShowing || !GameAnalyticsTracker.RunActive)
            {
                if (canvas != null) canvas.enabled = false;
                return;
            }

            if (canvas != null) canvas.enabled = true;
            if (hudText == null) return;

            TimeSpan t = TimeSpan.FromSeconds(GameAnalyticsTracker.ElapsedSeconds);
            hudText.text = $"Time: {t.Minutes:00}:{t.Seconds:00}  Deaths: {GameAnalyticsTracker.DeathsThisRun}";
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
    }
}
