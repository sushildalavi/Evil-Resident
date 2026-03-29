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
        const string AnalyticsFileName = "ResidentAnalytics.csv";
        const string CsvHeader = "timestamp,scene,event,elapsed_seconds,deaths_this_run,total_deaths,details";

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
                filePath = Path.Combine(analyticsDir, AnalyticsFileName);
                return filePath;
            }
        }

        public static void BeginRun()
        {
            runActive = true;
            runStartTime = Time.time;
            deathsThisRun = 0;
            Initialize();
            AppendRow("run_started", "start_screen_dismissed");
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
            ExecuteWrite(() =>
            {
                if (File.Exists(AnalyticsPath)) return;
                File.WriteAllText(AnalyticsPath, CsvHeader + Environment.NewLine);
            }, "create analytics header");
        }

        static void EnsureWritablePath()
        {
            try
            {
                EnsureDirectoryExists();
            }
            catch (Exception ex)
            {
                if (!TrySwitchToFallbackPath(ex))
                {
                    Debug.LogError($"[Analytics] Failed to prepare analytics directory at {AnalyticsPath}. Reason: {ex}");
                    return;
                }

                EnsureDirectoryExists();
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

            ExecuteWrite(() => File.AppendAllText(AnalyticsPath, row + Environment.NewLine), $"append analytics event '{evt}'");
        }

        static void EnsureDirectoryExists()
        {
            string dir = Path.GetDirectoryName(AnalyticsPath);
            if (string.IsNullOrEmpty(dir)) return;

            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
        }

        static void ExecuteWrite(Action writeAction, string operation)
        {
            try
            {
                writeAction();
            }
            catch (Exception ex)
            {
                if (!TrySwitchToFallbackPath(ex))
                {
                    Debug.LogError($"[Analytics] Failed to {operation} at {AnalyticsPath}. Reason: {ex}");
                    return;
                }

                try
                {
                    EnsureDirectoryExists();
                    writeAction();
                }
                catch (Exception fallbackEx)
                {
                    Debug.LogError($"[Analytics] Failed to {operation} at fallback path {AnalyticsPath}. Reason: {fallbackEx}");
                }
            }
        }

        static bool TrySwitchToFallbackPath(Exception ex)
        {
            string fallbackDir = Path.Combine(Application.persistentDataPath, "Sushil", "Analytics");
            string fallbackPath = Path.Combine(fallbackDir, AnalyticsFileName);
            if (string.Equals(AnalyticsPath, fallbackPath, StringComparison.Ordinal))
                return false;

            filePath = fallbackPath;
            pathLogged = false;
            Debug.LogWarning($"[Analytics] Project path not writable, using fallback path: {filePath}. Reason: {ex.Message}");
            return true;
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
    }
}
