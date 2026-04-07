using System;
using System.Collections.Generic;
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
    bool wasEscapeShowing;
    float cachedSurvivalSeconds;
    bool uploadSentForThisRun;
    string currentRunId;
    int lastSceneBuildIndex;

    Vector3 lastPlayerPos;
    Vector3 lastResidentPos;
    float lastDistance;
    int lastKeyCount;
    string lastDeathReason;

    int previousKeyCount;
    float firstKeyPickupTime;
    bool firstKeyRecorded;

    float residentPatrolSeconds;
    float residentInvestigateSeconds;
    float residentSearchSeconds;
    float residentChaseSeconds;

    Dictionary<string, float> residentZoneSeconds = new Dictionary<string, float>();

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
        bool escapeNow = EscapeOverlay.IsShowing;
        int sceneBuildIndex = SceneManager.GetActiveScene().buildIndex;

        bool sceneReloaded = sceneBuildIndex != lastSceneBuildIndex;
        bool overlayDismissed = (wasGameOverShowing && !gameOverNow) || (wasEscapeShowing && !escapeNow);
        bool newRunDetected = (!wasRunActive && active) || sceneReloaded || overlayDismissed;

        if (newRunDetected && active && !gameOverNow && !escapeNow)
        {
            currentRunId = Guid.NewGuid().ToString("N");
            uploadSentForThisRun = false;
            lastDeathReason = "";
            lastPlayerPos = Vector3.zero;
            lastResidentPos = Vector3.zero;
            lastDistance = 0f;
            lastKeyCount = 0;
            previousKeyCount = 0;
            firstKeyPickupTime = -1f;
            firstKeyRecorded = false;
            residentPatrolSeconds = 0f;
            residentInvestigateSeconds = 0f;
            residentSearchSeconds = 0f;
            residentChaseSeconds = 0f;
            residentZoneSeconds.Clear();
        }

        if (active && !gameOverNow && !escapeNow)
        {
            cachedSurvivalSeconds = GameAnalyticsTracker.ElapsedSeconds;
            CachePositions();
            TrackFirstKeyPickup();
            TrackResidentState();
            TrackResidentZone();
        }

        if (gameOverNow && !wasGameOverShowing && !uploadSentForThisRun)
        {
            CachePositions();
            TryReadDeathReason();
            uploadSentForThisRun = true;
            PostRun(settings, escaped: false, cachedSurvivalSeconds);
        }

        if (escapeNow && !wasEscapeShowing && !uploadSentForThisRun)
        {
            CachePositions();
            uploadSentForThisRun = true;
            PostRun(settings, escaped: true, cachedSurvivalSeconds);
        }

        wasRunActive = active;
        wasGameOverShowing = gameOverNow;
        wasEscapeShowing = escapeNow;
        lastSceneBuildIndex = sceneBuildIndex;
    }

    void TrackFirstKeyPickup()
    {
        if (firstKeyRecorded) return;
        int current = PlayerInventory.instance != null ? PlayerInventory.instance.KeyCount : 0;
        if (current > 0 && previousKeyCount == 0)
        {
            firstKeyPickupTime = GameAnalyticsTracker.ElapsedSeconds;
            firstKeyRecorded = true;
        }
        previousKeyCount = current;
    }

    void TrackResidentState()
    {
        var ai = FindFirstObjectByType<ResidentAI>();
        if (ai == null) return;
        float dt = Time.deltaTime;
        switch (ai.state)
        {
            case ResidentAI.State.Patrol:
                residentPatrolSeconds += dt;
                break;
            case ResidentAI.State.Investigate:
                residentInvestigateSeconds += dt;
                break;
            case ResidentAI.State.Search:
                residentSearchSeconds += dt;
                break;
            case ResidentAI.State.Chase:
                residentChaseSeconds += dt;
                break;
        }
    }

    void TrackResidentZone()
    {
        Transform resident = ResolveResidentTransform();
        if (resident == null) return;
        ClassifyZone(resident.position, out string zone, out string _, out string _);
        if (string.IsNullOrEmpty(zone) || zone == "Unknown") return;
        float dt = Time.deltaTime;
        if (residentZoneSeconds.ContainsKey(zone))
            residentZoneSeconds[zone] += dt;
        else
            residentZoneSeconds[zone] = dt;
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
        Debug.Log($"[RohitSheets] Posting: player=({lastPlayerPos.x:F1},{lastPlayerPos.y:F1},{lastPlayerPos.z:F1}) resident=({lastResidentPos.x:F1},{lastResidentPos.y:F1},{lastResidentPos.z:F1}) reason={lastDeathReason} firstKey={firstKeyPickupTime:F1}s patrol={residentPatrolSeconds:F1}s chase={residentChaseSeconds:F1}s");
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

        string firstKeyStr = firstKeyRecorded
            ? firstKeyPickupTime.ToString("F2", CultureInfo.InvariantCulture)
            : "";

        string patrolStr = residentPatrolSeconds.ToString("F2", CultureInfo.InvariantCulture);
        string investigateStr = residentInvestigateSeconds.ToString("F2", CultureInfo.InvariantCulture);
        string searchStr = residentSearchSeconds.ToString("F2", CultureInfo.InvariantCulture);
        string chaseStr = residentChaseSeconds.ToString("F2", CultureInfo.InvariantCulture);
        float roamSeconds = residentPatrolSeconds + residentInvestigateSeconds + residentSearchSeconds;
        string roamStr = roamSeconds.ToString("F2", CultureInfo.InvariantCulture);

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
        string deathZone = "";
        string deathAreaDetail = "";
        string deathFloor = "";
        if (!escaped)
            ClassifyZone(lastPlayerPos, out deathZone, out deathAreaDetail, out deathFloor);

        sb.Append(",\"where_died\":");
        AppendStringArray(sb, new[]
        {
            escaped ? "no" : "yes",
            escaped ? "" : survivalStr,
            escaped ? "" : deathZone, escaped ? "" : deathAreaDetail, escaped ? "" : deathFloor,
            escaped ? "" : detail,
            escaped ? "" : px, escaped ? "" : py, escaped ? "" : pz
        });
        sb.Append(",\"first_key\":");
        AppendStringArray(sb, new[]
        {
            pickedUpKey, firstKeyStr, sceneName, "",
            "", lastKeyCount.ToString(CultureInfo.InvariantCulture), runEndedBeforeKey
        });
        string dominantZone = "";
        float dominantZoneSec = 0f;
        foreach (var kvp in residentZoneSeconds)
        {
            if (kvp.Value > dominantZoneSec)
            {
                dominantZone = kvp.Key;
                dominantZoneSec = kvp.Value;
            }
        }
        string dominantZoneSecStr = dominantZoneSec > 0f
            ? dominantZoneSec.ToString("F2", CultureInfo.InvariantCulture)
            : "";
        string zonesVisitedStr = residentZoneSeconds.Count.ToString(CultureInfo.InvariantCulture);

        sb.Append(",\"resident\":");
        AppendStringArray(sb, new[]
        {
            dominantZone, dominantZoneSecStr, zonesVisitedStr, roamStr, investigateStr, chaseStr, rx, ry, rz, ""
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

    static void ClassifyZone(Vector3 pos, out string zone, out string areaDetail, out string floor)
    {
        float x = pos.x, y = pos.y, z = pos.z;

        if (x == 0f && y == 0f && z == 0f)
        {
            zone = "Unknown";
            areaDetail = "";
            floor = "";
            return;
        }

        floor = y < 0f ? "Basement" : y < 4f ? "Ground Floor" : "First Floor";

        if (x < -15f || z > -4f)
        {
            zone = "Near Escape Door";
            areaDetail = "Exit area northwest of building";
            return;
        }

        if (y < 0f)
        {
            if (x >= 0f && x <= 6f && z >= -17f && z <= -13f)
            {
                zone = "Basement Stairs";
                areaDetail = "Stairwell connecting to ground floor";
            }
            else
            {
                zone = "Basement";
                areaDetail = "Main basement area";
            }
        }
        else if (y < 4f)
        {
            if (x >= -4f && x <= 5f && z >= -19f && z <= -15f)
            {
                zone = "Stairs to First Floor";
                areaDetail = "Central stairwell";
            }
            else if (z < -19f)
            {
                if (x < -3f)
                {
                    zone = "Ground Room West";
                    areaDetail = "West room south side";
                }
                else if (x > 4f)
                {
                    zone = "Ground Room East";
                    areaDetail = "East room south side";
                }
                else
                {
                    zone = "Ground Corridor";
                    areaDetail = "South corridor near spawn";
                }
            }
            else if (z < -15f)
            {
                if (x < -4f)
                {
                    zone = "Ground Room West";
                    areaDetail = "West room north side";
                }
                else if (x > 5f)
                {
                    zone = "Ground Room East";
                    areaDetail = "East room north side";
                }
                else
                {
                    zone = "Stairs to First Floor";
                    areaDetail = "Near stairwell";
                }
            }
            else
            {
                if (x < -2f)
                {
                    zone = "Ground West Wing";
                    areaDetail = "Northwest open area";
                }
                else if (x > 2f)
                {
                    zone = "Ground East Wing";
                    areaDetail = "Northeast open area";
                }
                else
                {
                    zone = "Ground North Corridor";
                    areaDetail = "Central north corridor";
                }
            }
        }
        else
        {
            if (z <= -21.5f)
            {
                zone = "First Floor South Corridor";
                areaDetail = "South bridge";
            }
            else if (z < -16.5f)
            {
                if (x < 2f)
                {
                    zone = "First Floor West Room";
                    areaDetail = "West bedroom";
                }
                else
                {
                    zone = "First Floor East Room";
                    areaDetail = "East bedroom";
                }
            }
            else
            {
                if (x < 0f)
                {
                    zone = "First Floor West Room";
                    areaDetail = "West room north side";
                }
                else if (x > 4f)
                {
                    zone = "First Floor East Room";
                    areaDetail = "East room north side";
                }
                else
                {
                    zone = "First Floor North Corridor";
                    areaDetail = "North bridge";
                }
            }
        }
    }

    static Transform ResolvePlayerTransform()
    {
        var rohit = FindFirstObjectByType<RohitFPSController>();
        if (rohit != null) return rohit.transform;
        GameObject tagged = GameObject.FindGameObjectWithTag("Player");
        return tagged != null ? tagged.transform : null;
    }

    static Transform ResolveResidentTransform()
    {
        var ai = FindFirstObjectByType<ResidentAI>();
        return ai != null ? ai.transform : null;
    }
}
