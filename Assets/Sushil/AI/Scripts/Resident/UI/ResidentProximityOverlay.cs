using UnityEngine;
using UnityEngine.UI;
using UnityEngine.AI;
using Sushil.AI;

namespace Sushil.Systems
{
    public class ResidentProximityOverlay : MonoBehaviour
    {
        static ResidentProximityOverlay instance;
        const string HiddenStateText = "";

        [Header("Distance")]
        public float detectDistance = 18f;
        public float criticalDistance = 5.5f;
        public bool enableScreenPulse = false;

        [Header("Visual Pulse")]
        public Color pulseColor = new Color(0.18f, 0.05f, 0.08f, 1f);
        public float minAlpha = 0f;
        public float maxAlpha = 0.22f;
        public float minPulseSpeed = 1.2f;
        public float maxPulseSpeed = 3.6f;
        [Range(0f, 0.25f)] public float beatAccentStrength = 0.12f;
        [Range(0f, 1f)] public float farSuppression = 0.82f;
        [Range(0f, 1f)] public float chaseFloor = 0.14f;

        [Header("Heartbeat Text")]
        public bool showHeartbeatText = false;
        public string heartbeatLabel = "HEART RATE ELEVATED";
        public float heartbeatTextMinAlpha = 0.12f;
        public float heartbeatTextMaxAlpha = 0.65f;

        Image pulseImage;
        Text heartbeatText;
        Text subText;

        Transform player;
        RohitFPSController rohitPlayer;
        PlayerHide playerHide;
        ResidentAI[] residents;
        float nextRefreshTime;
        // NavMeshPath cannot be `new`'d in a field initializer — Unity throws.
        NavMeshPath _pathBuf;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (instance != null) return;
            GameObject go = new GameObject("ResidentProximityOverlay");
            instance = go.AddComponent<ResidentProximityOverlay>();
            DontDestroyOnLoad(go);
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
            ResolveReferences(true);
            ClearOverlay();
        }

        void Update()
        {
            if (ShouldSuppressOverlay())
            {
                ClearOverlay();
                return;
            }

            if (Time.unscaledTime >= nextRefreshTime)
            {
                ResolveReferences(false);
                nextRefreshTime = Time.unscaledTime + 0.5f;
            }

            if (player == null || residents == null || residents.Length == 0)
            {
                ClearOverlay();
                return;
            }

            float nearest = GetNearestResidentThreatDistance(out bool nearestHasLos);
            bool anyChasing = IsAnyResidentChasing();
            if (nearest < 0f || nearest > detectDistance)
            {
                if (anyChasing && nearest >= 0f && nearest <= detectDistance * 1.25f)
                {
                    // Keep a weaker pulse while actively hunted, even if raw distance is high.
                    float chasePulse = BuildHeartbeatPulse(maxPulseSpeed * 0.85f);
                    float chaseAlpha = Mathf.Lerp(chaseFloor * 0.35f, maxAlpha * 0.55f, chasePulse);
                    float chaseText = Mathf.Lerp(heartbeatTextMinAlpha, heartbeatTextMaxAlpha * 0.85f, chasePulse);
                    SetAlpha(chaseAlpha, chaseText, "HUNTED");
                    return;
                }

                ClearOverlay();
                return;
            }

            float danger = 1f - Mathf.Clamp01((nearest - criticalDistance) / Mathf.Max(0.1f, detectDistance - criticalDistance));
            if (!nearestHasLos) danger *= 0.72f;
            float pulseSpeed = Mathf.Lerp(minPulseSpeed, maxPulseSpeed, danger);
            float pulse = BuildHeartbeatPulse(pulseSpeed * Mathf.Lerp(0.9f, 1.35f, danger));

            // Strong suppression at far range so the effect feels audio-reactive, not constant.
            float weightedDanger = Mathf.Pow(danger, Mathf.Lerp(2.2f, 1.15f, danger));
            float suppressed = weightedDanger * (1f - farSuppression);
            float dangerMix = Mathf.Clamp01(suppressed + (weightedDanger * 0.75f));

            float alpha = Mathf.Lerp(minAlpha, maxAlpha, dangerMix * pulse);
            if (anyChasing)
                alpha = Mathf.Max(alpha, chaseFloor * Mathf.Lerp(0.35f, 1f, dangerMix));
            float textAlpha = Mathf.Lerp(heartbeatTextMinAlpha, heartbeatTextMaxAlpha, danger * pulse);
            string state = danger > 0.75f ? "CRITICAL" : (danger > 0.35f ? "ELEVATED" : "CAUTION");
            SetAlpha(alpha, textAlpha, state);
        }

        float BuildHeartbeatPulse(float speed)
        {
            float t = Time.unscaledTime;
            float cycle = Mathf.Repeat(t * speed, 1f);

            // "lub-dub": one strong beat + one softer trailing beat.
            float beatA = Mathf.Exp(-Mathf.Pow((cycle - 0.16f) / 0.05f, 2f));
            float beatB = 0.72f * Mathf.Exp(-Mathf.Pow((cycle - 0.33f) / 0.06f, 2f));
            float envelope = Mathf.Clamp01(beatA + beatB);

            float baseline = 0.08f + 0.12f * (0.5f + 0.5f * Mathf.Sin(t * speed * 0.55f));
            return Mathf.Clamp01(baseline + envelope * (0.8f + beatAccentStrength));
        }

        bool ShouldSuppressOverlay()
        {
            return !enableScreenPulse ||
                   StartScreenOverlay.IsShowing ||
                   PauseOverlay.IsPaused ||
                   GameOverOverlay.IsShowing ||
                   EscapeOverlay.IsShowing ||
                   IsPlayerHidden();
        }

        void ClearOverlay()
        {
            SetAlpha(0f, 0f, HiddenStateText);
        }

        void ResolveReferences(bool force)
        {
            if (force || player == null)
            {
                var rohit = FindFirstObjectByType<RohitFPSController>();
                if (rohit != null)
                {
                    player = rohit.transform;
                    rohitPlayer = rohit;
                }

                if (player == null)
                {
                    var death = FindFirstObjectByType<PlayerDeath>();
                    if (death != null)
                    {
                        player = death.transform;
                        playerHide = death.GetComponent<PlayerHide>();
                    }
                }

                if (player == null)
                {
                    var tagged = GameObject.FindGameObjectWithTag("Player");
                    if (tagged != null) player = tagged.transform;
                }

                if (player != null)
                {
                    if (rohitPlayer == null) rohitPlayer = player.GetComponent<RohitFPSController>();
                    if (playerHide == null) playerHide = player.GetComponent<PlayerHide>();
                }
            }

            if (force || residents == null || residents.Length == 0)
                residents = FindObjectsByType<ResidentAI>(FindObjectsSortMode.None);
        }

        bool IsPlayerHidden()
        {
            if (rohitPlayer != null && rohitPlayer.isHidden) return true;
            if (playerHide != null && playerHide.IsHidden) return true;
            return false;
        }

        float GetNearestResidentThreatDistance(out bool hasLosToNearest)
        {
            float best = float.MaxValue;
            hasLosToNearest = false;
            bool found = false;

            for (int i = 0; i < residents.Length; i++)
            {
                var s = residents[i];
                if (s == null || !s.gameObject.activeInHierarchy) continue;

                float raw = Vector3.Distance(player.position, s.transform.position);
                bool hasLos = HasLineOfSightToResident(s.transform);
                float pathDistance = GetPathDistance(player.position, s.transform.position);
                float effective = pathDistance >= 0f ? pathDistance : (raw * 2.2f);
                if (!hasLos) effective *= 1.35f;

                if (effective < best)
                {
                    best = effective;
                    hasLosToNearest = hasLos;
                    found = true;
                }
            }

            return found ? best : -1f;
        }

        float GetPathDistance(Vector3 from, Vector3 to)
        {
            var path = _pathBuf ??= new NavMeshPath();
            if (!NavMesh.CalculatePath(from, to, NavMesh.AllAreas, path)) return -1f;
            if (path.status != NavMeshPathStatus.PathComplete || path.corners == null || path.corners.Length < 2) return -1f;

            float sum = 0f;
            for (int i = 1; i < path.corners.Length; i++)
                sum += Vector3.Distance(path.corners[i - 1], path.corners[i]);
            return sum;
        }

        bool HasLineOfSightToResident(Transform resident)
        {
            if (player == null || resident == null) return false;

            Vector3 origin = player.position + Vector3.up * 1.35f;
            Vector3 target = resident.position + Vector3.up * 1.4f;
            Vector3 dir = (target - origin);
            float dist = dir.magnitude;
            if (dist <= 0.05f) return true;
            dir /= dist;

            if (!Physics.Raycast(origin, dir, out RaycastHit hit, dist, ~0, QueryTriggerInteraction.Ignore))
                return true;

            Transform t = hit.collider != null ? hit.collider.transform : null;
            if (t == null) return false;
            if (t == resident || t.IsChildOf(resident)) return true;
            if (t == player || t.IsChildOf(player)) return true;
            return false;
        }

        bool IsAnyResidentChasing()
        {
            if (residents == null) return false;
            for (int i = 0; i < residents.Length; i++)
            {
                var s = residents[i];
                if (s == null || !s.gameObject.activeInHierarchy) continue;
                if (s.state == ResidentAI.State.Chase) return true;
            }
            return false;
        }

        void BuildUI()
        {
            var overlayCanvas = gameObject.AddComponent<Canvas>();
            overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            overlayCanvas.sortingOrder = short.MaxValue - 10;

            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>();

            GameObject pulseObj = new GameObject("Pulse");
            pulseObj.transform.SetParent(transform, false);
            pulseImage = pulseObj.AddComponent<Image>();
            pulseImage.color = new Color(pulseColor.r, pulseColor.g, pulseColor.b, 0f);
            RectTransform pulseRect = pulseImage.rectTransform;
            pulseRect.anchorMin = Vector2.zero;
            pulseRect.anchorMax = Vector2.one;
            pulseRect.offsetMin = Vector2.zero;
            pulseRect.offsetMax = Vector2.zero;

            if (!showHeartbeatText) return;

            heartbeatText = CreateStatusText(
                "HeartbeatText",
                46,
                20,
                46,
                heartbeatLabel,
                new Color(0.92f, 0.88f, 0.88f, 0f),
                new Vector2(40f, 54f),
                new Vector2(-40f, 142f),
                true);

            subText = CreateStatusText(
                "HeartbeatSubText",
                26,
                14,
                26,
                HiddenStateText,
                new Color(0.85f, 0.8f, 0.8f, 0f),
                new Vector2(40f, 26f),
                new Vector2(-40f, 72f));
        }

        Text CreateStatusText(string objectName, int fontSize, int minSize, int maxSize, string content, Color color, Vector2 offsetMin, Vector2 offsetMax, bool addShadow = false)
        {
            GameObject textObj = new GameObject(objectName);
            textObj.transform.SetParent(transform, false);

            Text text = textObj.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.fontStyle = FontStyle.Normal;
            text.alignment = TextAnchor.LowerCenter;
            text.text = content;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = minSize;
            text.resizeTextMaxSize = maxSize;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.color = color;

            RectTransform textRect = text.rectTransform;
            textRect.anchorMin = new Vector2(0f, 0f);
            textRect.anchorMax = new Vector2(1f, 0f);
            textRect.offsetMin = offsetMin;
            textRect.offsetMax = offsetMax;

            if (addShadow)
            {
                var shadow = textObj.AddComponent<Shadow>();
                shadow.effectColor = new Color(0f, 0f, 0f, 0.9f);
                shadow.effectDistance = new Vector2(3f, -3f);
            }

            return text;
        }

        void SetAlpha(float screenAlpha, float textAlpha, string stateText)
        {
            if (pulseImage != null)
            {
                Color c = pulseImage.color;
                c.r = pulseColor.r;
                c.g = pulseColor.g;
                c.b = pulseColor.b;
                c.a = Mathf.Clamp01(screenAlpha);
                pulseImage.color = c;
            }

            if (heartbeatText != null)
            {
                Color tc = heartbeatText.color;
                tc.a = Mathf.Clamp01(textAlpha);
                heartbeatText.color = tc;
            }

            if (subText != null)
            {
                subText.text = stateText;
                Color sc = subText.color;
                sc.a = Mathf.Clamp01(textAlpha * 0.95f);
                subText.color = sc;
            }
        }
    }
}
