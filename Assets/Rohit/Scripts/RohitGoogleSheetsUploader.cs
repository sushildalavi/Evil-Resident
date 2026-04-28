using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Sushil.AI;
using Sushil.Systems;

public class RohitGoogleSheetsUploader : MonoBehaviour
{
    const string FORM_URL =
        "https://docs.google.com/forms/d/e/1FAIpQLSddm6bMJiB6isB75Egk8VoEgiklER688QyZeiSFwXa_JhFyRA/formResponse";

    static readonly HashSet<string> TrackedScenes = new HashSet<string>
    {
        "Easy Level", "Medium Level", "Hard Level", "Old Hard"
    };

    static RohitGoogleSheetsUploader instance;
    static RohitGoogleSheetsSettings cachedSettings;

    // Run state
    string currentRunId;
    bool uploadSentForThisRun;
    float cachedSurvivalSeconds;
    int lastSceneBuildIndex;

    // Previous-frame overlay/run state for edge detection
    bool wasRunActive;
    bool wasGameOverShowing;
    bool wasEscapeShowing;

    // Cached player/resident snapshot
    Vector3 lastPlayerPos;
    Vector3 lastResidentPos;
    float lastDistance;
    int lastKeyCount;
    string lastDeathReason;

    // First key pickup tracking
    int previousKeyCount;
    float firstKeyPickupTime;
    bool firstKeyRecorded;

    // Resident AI behaviour timers
    float residentRoamSeconds;
    float residentChaseSeconds;

    // Resident zone-presence accumulator
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
        if (settings == null || !settings.uploadEnabled) return;

        bool active = GameAnalyticsTracker.RunActive;
        bool gameOverNow = GameOverOverlay.IsShowing;
        bool escapeNow = EscapeOverlay.IsShowing;
        int sceneBuildIndex = SceneManager.GetActiveScene().buildIndex;

        if (IsNewRunStarting(active, gameOverNow, escapeNow, sceneBuildIndex))
            ResetRunState();

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
            PostRun(escaped: false);
        }

        if (escapeNow && !wasEscapeShowing && !uploadSentForThisRun)
        {
            CachePositions();
            uploadSentForThisRun = true;
            PostRun(escaped: true);
        }

        wasRunActive = active;
        wasGameOverShowing = gameOverNow;
        wasEscapeShowing = escapeNow;
        lastSceneBuildIndex = sceneBuildIndex;
    }

    bool IsNewRunStarting(bool active, bool gameOverNow, bool escapeNow, int sceneBuildIndex)
    {
        bool sceneReloaded = sceneBuildIndex != lastSceneBuildIndex;
        bool overlayDismissed = (wasGameOverShowing && !gameOverNow)
                             || (wasEscapeShowing && !escapeNow);
        bool newRun = (!wasRunActive && active) || sceneReloaded || overlayDismissed;
        return newRun && active && !gameOverNow && !escapeNow;
    }

    void ResetRunState()
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

        residentRoamSeconds = 0f;
        residentChaseSeconds = 0f;
        residentZoneSeconds.Clear();
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

        if (ai.state == ResidentAI.State.Chase)
            residentChaseSeconds += dt;
        else
            residentRoamSeconds += dt;
    }

    void TrackResidentZone()
    {
        Transform resident = ResolveResidentTransform();
        if (resident == null) return;

        ClassifyZone(resident.position, out string zone, out _, out _);
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

        if (player != null) lastPlayerPos = player.position;
        if (resident != null) lastResidentPos = resident.position;
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

    void PostRun(bool escaped)
    {
        string sceneName = SceneManager.GetActiveScene().name;
        if (!TrackedScenes.Contains(sceneName)) return;

        string runId = string.IsNullOrEmpty(currentRunId) ? Guid.NewGuid().ToString("N") : currentRunId;
        string platform = Application.platform.ToString();
        string outcome = escaped ? "escape" : "death";
        string survivalStr = cachedSurvivalSeconds.ToString("F2", CultureInfo.InvariantCulture);
        string distStr = lastDistance.ToString("F2", CultureInfo.InvariantCulture);
        string deathReason = escaped ? "" : lastDeathReason;

        string deathZone = "";
        if (!escaped)
            ClassifyZone(lastPlayerPos, out deathZone, out _, out _);

        string px = escaped ? "" : lastPlayerPos.x.ToString("F2", CultureInfo.InvariantCulture);
        string py = escaped ? "" : lastPlayerPos.y.ToString("F2", CultureInfo.InvariantCulture);
        string pz = escaped ? "" : lastPlayerPos.z.ToString("F2", CultureInfo.InvariantCulture);

        string pickedUpKey = lastKeyCount > 0 ? "yes" : "no";
        string firstKeyStr = firstKeyRecorded
            ? firstKeyPickupTime.ToString("F2", CultureInfo.InvariantCulture)
            : "";

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
            ? dominantZoneSec.ToString("F2", CultureInfo.InvariantCulture) : "";
        string zonesVisitedStr = residentZoneSeconds.Count.ToString(CultureInfo.InvariantCulture);
        string roamStr = residentRoamSeconds.ToString("F2", CultureInfo.InvariantCulture);
        string chaseStr = residentChaseSeconds.ToString("F2", CultureInfo.InvariantCulture);

        Debug.Log($"[RohitSheets] Posting: outcome={outcome} zone={deathZone} survival={survivalStr}s dominant={dominantZone}");

        WWWForm form = new WWWForm();
        form.AddField("entry.332868507",  runId);
        form.AddField("entry.928468147",  platform);
        form.AddField("entry.2064445553", sceneName);
        form.AddField("entry.1017402579", outcome);
        form.AddField("entry.153072461",  survivalStr);
        form.AddField("entry.359604092",  deathReason);
        form.AddField("entry.391092436",  escaped ? "" : deathZone);
        form.AddField("entry.1277514908", px);
        form.AddField("entry.943805827",  py);
        form.AddField("entry.470553641",  pz);
        form.AddField("entry.830856127",  distStr);
        form.AddField("entry.1975546752", pickedUpKey);
        form.AddField("entry.461548707",  firstKeyStr);
        form.AddField("entry.548395472",  lastKeyCount.ToString(CultureInfo.InvariantCulture));
        form.AddField("entry.342081516",  dominantZone);
        form.AddField("entry.1664393630", dominantZoneSecStr);
        form.AddField("entry.1404079762", zonesVisitedStr);
        form.AddField("entry.435574418",  roamStr);
        form.AddField("entry.54314593",   chaseStr);

        StartCoroutine(SubmitForm(form));
    }

    IEnumerator SubmitForm(WWWForm form)
    {
        using (UnityWebRequest www = UnityWebRequest.Post(FORM_URL, form))
        {
            yield return www.SendWebRequest();
            if (www.result != UnityWebRequest.Result.Success)
                Debug.LogWarning($"[RohitSheets] POST failed: {www.error}");
            else
                Debug.Log("[RohitSheets] Form submitted OK");
        }
    }

    static void ClassifyZone(Vector3 pos, out string zone, out string areaDetail, out string floor)
    {
        float x = pos.x, y = pos.y, z = pos.z;
        areaDetail = "";

        if (x == 0f && y == 0f && z == 0f)
        {
            zone = "Unknown";
            floor = "";
            return;
        }

        floor = y < 0f ? "Basement" : y < 4f ? "Ground" : "First Floor";

        if (x < -15f || z > -4f) { zone = "Near Escape Door"; return; }

        if (y < 0f)
        {
            zone = "Basement";
        }
        else if (y < 4f)
        {
            if (x >= -4f && x <= 5f && z >= -19f && z <= -15f)
                zone = "Stairwell";
            else if (z < -19f)
            {
                if (x < -3f) zone = "Ground Room West";
                else if (x > 4f) zone = "Ground Room East";
                else zone = "Ground Corridor";
            }
            else if (z < -15f)
            {
                if (x < -4f) zone = "Ground Room West";
                else if (x > 5f) zone = "Ground Room East";
                else zone = "Stairwell";
            }
            else
                zone = "Ground North Wing";
        }
        else
        {
            if (x <= 0.5f) zone = "First Floor West";
            else if (x >= 3.5f) zone = "First Floor East";
            else zone = "First Floor Corridor";
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

    static RohitGoogleSheetsSettings LoadSettings()
    {
        if (cachedSettings != null) return cachedSettings;
        cachedSettings = Resources.Load<RohitGoogleSheetsSettings>("RohitGoogleSheetsAnalytics");
        return cachedSettings;
    }
}
