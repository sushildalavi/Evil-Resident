using System;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Sushil.AI;
using Sushil.Systems;

public class RohitGoogleSheetsUploader : MonoBehaviour
{
    static RohitGoogleSheetsUploader instance;
    static RohitGoogleSheetsSettings cachedSettings;

    bool wasRunActive;
    bool wasGameOverShowing;
    float cachedSurvivalSeconds;
    bool uploadSentForThisRun;
    string currentRunId;

    Vector3 lastPlayerPos;
    Vector3 lastResidentPos;
    float lastDistance;
    int lastKeyCount;
    string lastDeathReason;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (instance != null) return;
        var go = new GameObject("RohitGoogleSheetsUploader");
        instance = go.AddComponent<RohitGoogleSheetsUploader>();
        DontDestroyOnLoad(go);
    }

    void Update()
    {
        var settings = LoadSettings();
        if (settings == null || !settings.uploadEnabled ||
            string.IsNullOrWhiteSpace(settings.webAppUrl) ||
            string.IsNullOrWhiteSpace(settings.sharedSecret))
            return;

        bool active = GameAnalyticsTracker.RunActive;
        bool gameOverNow = GameOverOverlay.IsShowing;

        if (!wasRunActive && active)
        {
            currentRunId = Guid.NewGuid().ToString("N");
            uploadSentForThisRun = false;
            lastDeathReason = "";
        }

        if (active && !gameOverNow)
        {
            cachedSurvivalSeconds = GameAnalyticsTracker.ElapsedSeconds;
            CachePositions();
        }

        if (gameOverNow && !wasGameOverShowing && active && !uploadSentForThisRun)
        {
            CachePositions();
            TryReadDeathReason();
            uploadSentForThisRun = true;
            PostRun(settings, escaped: false, cachedSurvivalSeconds);
        }

        if (wasRunActive && !active && !uploadSentForThisRun)
        {
            uploadSentForThisRun = true;
            PostRun(settings, escaped: true, cachedSurvivalSeconds);
        }

        wasRunActive = active;
        wasGameOverShowing = gameOverNow;
    }

    void CachePositions()
    {
        Transform player = ResolvePlayerTransform();
        Transform resident = ResolveResidentTransform();
        if (player != null)
            lastPlayerPos = player.position;
        if (resident != null)
            lastResidentPos = resident.position;
        if (player != null && resident != null)
            lastDistance = Vector3.Distance(lastPlayerPos, lastResidentPos);
        if (PlayerInventory.instance != null)
            lastKeyCount = PlayerInventory.instance.KeyCount;
    }

    void TryReadDeathReason()
    {
        var reasonObj = GameObject.Find("GameOverCanvas/Dim/Card/Reason");
        if (reasonObj != null)
        {
            var txt = reasonObj.GetComponent<Text>();
            if (txt != null && !string.IsNullOrWhiteSpace(txt.text))
                lastDeathReason = txt.text;
        }
        if (string.IsNullOrEmpty(lastDeathReason))
            lastDeathReason = "caught";
    }

    static RohitGoogleSheetsSettings LoadSettings()
    {
        if (cachedSettings != null) return cachedSettings;
        cachedSettings = Resources.Load<RohitGoogleSheetsSettings>("RohitGoogleSheetsAnalytics");
        return cachedSettings;
    }

    void PostRun(RohitGoogleSheetsSettings cfg, bool escaped, float survivalSeconds)
    {
        string json = BuildPayloadJson(cfg, escaped, survivalSeconds);
        Debug.Log($"[RohitSheets] Posting: player=({lastPlayerPos.x:F1},{lastPlayerPos.y:F1},{lastPlayerPos.z:F1}) resident=({lastResidentPos.x:F1},{lastResidentPos.y:F1},{lastResidentPos.z:F1}) reason={lastDeathReason}");
        byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
        var req = new UnityWebRequest(cfg.webAppUrl.Trim(), UnityWebRequest.kHttpVerbPOST);
        req.uploadHandler = new UploadHandlerRaw(bodyRaw);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        var op = req.SendWebRequest();
        op.completed += _ =>
        {
            try
            {
                if (req.result != UnityWebRequest.Result.Success)
                    Debug.LogWarning($"[RohitSheets] POST failed: {req.error} {req.downloadHandler?.text}");
                else
                    Debug.Log($"[RohitSheets] OK: {req.downloadHandler?.text}");
            }
            finally
            {
                req.Dispose();
            }
        };
    }

    string BuildPayloadJson(RohitGoogleSheetsSettings cfg, bool escaped, float survivalSeconds)
    {
        string sceneName = SceneManager.GetActiveScene().name;
        string platform = Application.platform.ToString();
        string utc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
        string runId = string.IsNullOrEmpty(currentRunId) ? Guid.NewGuid().ToString("N") : currentRunId;
        string survivalStr = survivalSeconds.ToString("F2", CultureInfo.InvariantCulture);
        string distStr = lastDistance.ToString("F2", CultureInfo.InvariantCulture);

        string outcome = escaped ? "escape" : "death";
        string detail = escaped ? "main_door_unlocked" : lastDeathReason;
        string pickedUpKey = lastKeyCount > 0 ? "yes" : "no";
        string runEndedBeforeKey = lastKeyCount == 0 ? "yes" : "no";
        int deathsThisRun = GameAnalyticsTracker.DeathsThisRun;

        string px = lastPlayerPos.x.ToString("F2", CultureInfo.InvariantCulture);
        string py = lastPlayerPos.y.ToString("F2", CultureInfo.InvariantCulture);
        string pz = lastPlayerPos.z.ToString("F2", CultureInfo.InvariantCulture);
        string rx = lastResidentPos.x.ToString("F2", CultureInfo.InvariantCulture);
        string ry = lastResidentPos.y.ToString("F2", CultureInfo.InvariantCulture);
        string rz = lastResidentPos.z.ToString("F2", CultureInfo.InvariantCulture);

        var sb = new StringBuilder(1024);
        sb.Append("{\"secret\":").Append(JsonString(cfg.sharedSecret)).Append(',');
        sb.Append("\"universal\":{");
        sb.Append("\"run_id\":").Append(JsonString(runId)).Append(',');
        sb.Append("\"recorded_at_utc\":").Append(JsonString(utc)).Append(',');
        sb.Append("\"platform\":").Append(JsonString(platform)).Append(',');
        sb.Append("\"scene_active\":").Append(JsonString(sceneName));
        sb.Append("},\"escape_vs_death\":");
        AppendStringArray(sb, new[]
        {
            outcome, survivalStr,
            deathsThisRun.ToString(CultureInfo.InvariantCulture),
            px, py, pz, "", detail, "", distStr
        });
        sb.Append(",\"where_died\":");
        AppendStringArray(sb, new[]
        {
            escaped ? "no" : "yes",
            escaped ? "" : survivalStr,
            escaped ? "" : "", escaped ? "" : "", escaped ? "" : "",
            escaped ? "" : detail,
            escaped ? "" : px, escaped ? "" : py, escaped ? "" : pz
        });
        sb.Append(",\"first_key\":");
        AppendStringArray(sb, new[]
        {
            pickedUpKey, "", sceneName, "",
            "", lastKeyCount.ToString(CultureInfo.InvariantCulture), runEndedBeforeKey
        });
        sb.Append(",\"resident\":");
        AppendStringArray(sb, new[]
        {
            "", "", "", "", "", "", rx, ry, rz, ""
        });
        sb.Append('}');
        return sb.ToString();
    }

    static void AppendStringArray(StringBuilder sb, string[] items)
    {
        sb.Append('[');
        for (int i = 0; i < items.Length; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append(JsonString(items[i] ?? ""));
        }
        sb.Append(']');
    }

    static string JsonString(string s) => '"' + JsonEscape(s) + '"';

    static string JsonEscape(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r");
    }

    static Transform ResolvePlayerTransform()
    {
        var rohit = FindFirstObjectByType<RohitFPSController>();
        if (rohit != null) return rohit.transform;
        var pd = FindFirstObjectByType<PlayerDeath>();
        if (pd != null) return pd.transform;
        GameObject tagged = GameObject.FindGameObjectWithTag("Player");
        return tagged != null ? tagged.transform : null;
    }

    static Transform ResolveResidentTransform()
    {
        var ai = FindFirstObjectByType<ResidentAI>();
        return ai != null ? ai.transform : null;
    }
}
