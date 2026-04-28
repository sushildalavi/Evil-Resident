using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Sushil.AI;

namespace Sushil.Systems
{
    // Enemy indicator — shipped-quality aesthetic pass.
    //
    // Hierarchy (all auto-built):
    //   Canvas (ScreenSpaceOverlay)
    //     MarkerRoot (RectTransform + CanvasGroup = assemblyGroup)     ← master gate
    //       OnScreen (CanvasGroup)
    //         Halo      (soft state-colored outer glow)
    //         TickRing  (thin ring + 4 cardinal ticks, slowly rotating)
    //         CoreDot   (filled center dot)
    //         DistanceShadow + Distance (bracketed "• 6M •" style)
    //       OffScreen (CanvasGroup)
    //         Halo      (glow behind chevron)
    //         Chevron   (thick stroke arrow)
    //         DistanceShadow + Distance
    //
    // assemblyGroup is the ONE choke point for visibility — if it is 0,
    // nothing draws. Distance text living inside each sub-group makes
    // "orphan text without marker" structurally impossible.
    public class ResidentStartPointer : MonoBehaviour
    {
        enum PresentationMode { Hidden, OnScreen, OffScreen }
        enum FloorRelation { Same, Upstairs, Downstairs }
        enum ThreatLevel { Safe, Sensed, Seen }

        static ResidentStartPointer instance;
        static Sprite haloSprite;
        static Sprite tickRingSprite;
        static Sprite coreDotSprite;
        static Sprite chevronSprite;

        // ------------------------------ Inspector ------------------------------

        [Header("Anchor")]
        public float worldAnchorHeight = 1.6f;

        [Header("On-Screen Marker")]
        [Range(18f, 80f)] public float onScreenSize = 36f;
        [Tooltip("Halo scale relative to the main ring.")]
        [Range(1f, 3f)] public float haloScale = 2.1f;

        [Header("Off-Screen Arrow")]
        [Range(18f, 80f)] public float offScreenSize = 40f;
        [Range(16f, 160f)] public float screenEdgePadding = 64f;

        [Header("Scale Clamp")]
        [Range(10f, 48f)] public float minSize = 22f;
        [Range(30f, 120f)] public float maxSize = 56f;

        [Header("Distance Text")]
        public bool showDistanceText = true;
        public bool usePathDistance = true;
        [Tooltip("Extra displayed distance added per floor gap when a complete NavMesh path is unavailable.")]
        public float fallbackFloorDistancePenalty = 9f;
        [Range(10f, 28f)] public float textSize = 14f;
        [Range(2f, 40f)] public float textOffset = 22f;
        public bool useBracketedText = true;

        [Header("Visibility")]
        public float maxDisplayDistance = 999f;
        public float hideWhenCloserThan = 0f;
        public bool keepVisibleWhileOccluded = true;
        public bool keepVisibleWhileHiding = true;

        [Header("Opacity")]
        [Range(0f, 1f)] public float visibleAlpha = 1f;
        [Range(0f, 1f)] public float occludedAlpha = 0.55f;
        [Range(0f, 1f)] public float hidingAlpha = 0.35f;
        [Range(0f, 1f)] public float haloMaxAlpha = 0.55f;
        public LayerMask occlusionMask = ~0;

        [Header("State Colors")]
        public Color safeColor = new Color(0.2f, 0.95f, 0.35f, 1f);
        public Color sensedColor = new Color(1f, 0.58f, 0.12f, 1f);
        public Color seenColor = new Color(1f, 0.08f, 0.1f, 1f);

        [Header("Text Style")]
        public Color textColor = new Color(1f, 0.96f, 0.88f, 0.92f);
        public Color textShadowColor = new Color(0f, 0f, 0f, 0.85f);

        [Header("Floor Indicator")]
        [Tooltip("How far above/below the player (meters) the resident must be to show a floor glyph.")]
        public float floorHeightThreshold = 1.0f;
        [Tooltip("Show the upstairs/downstairs chevron when the resident is on a different floor.")]
        public bool showFloorGlyph = true;
        [Tooltip("Size of the floor glyph as a fraction of the main marker size.")]
        [Range(0.4f, 2f)] public float floorGlyphScale = 1.3f;
        [Tooltip("Vertical offset of the floor glyph from the marker center, in marker-radii.")]
        [Range(0.7f, 2.5f)] public float floorGlyphOffset = 1.35f;
        public Color floorGlyphColor = new Color(1f, 0.78f, 0.2f, 1f);
        [Tooltip("Also prefix the distance text with ↑ / ↓ / nothing based on floor relation (redundant to the glyph).")]
        public bool addFloorArrowToText = true;

        [Header("Animation")]
        [Range(0.5f, 3f)] public float pulsePeriod = 1.4f;
        [Range(0f, 0.18f)] public float pulseAmount = 0.06f;
        [Range(0f, 0.35f)] public float chasePulseAccent = 0.18f;
        [Range(-60f, 60f)] public float tickRingSpinDegPerSec = 14f;
        [Range(0f, 0.7f)] public float haloPulseAmount = 0.35f;
        [Range(0f, 0.3f)] public float floorGlyphBobAmount = 0.1f;
        [Range(0f, 5f)] public float floorGlyphBobSpeed = 2.4f;
        [Range(1f, 30f)] public float smoothingSpeed = 12f;

        // ------------------------------ Runtime UI ------------------------------

        Canvas canvas;
        RectTransform rootRect;
        CanvasGroup assemblyGroup;

        // OnScreen assembly
        CanvasGroup onScreenGroup;
        RectTransform onScreenRect;          // sized/scaled by pulse
        Image onScreenHalo;                  // big soft blob
        RectTransform onScreenHaloRect;
        Image onScreenTickRing;              // thin ring + tick marks, rotates
        RectTransform onScreenTickRect;
        Image onScreenCoreDot;               // inner filled dot
        RectTransform onScreenCoreRect;
        RectTransform onScreenTextRect;
        Text onScreenText;
        RectTransform onScreenTextShadowRect;
        Text onScreenTextShadow;

        // OffScreen assembly
        CanvasGroup offScreenGroup;
        RectTransform offScreenRect;         // sized/scaled by pulse, rotated to point
        Image offScreenHalo;
        RectTransform offScreenHaloRect;
        Image offScreenChevron;
        RectTransform offScreenChevronRect;
        RectTransform offScreenTextRect;
        Text offScreenText;
        RectTransform offScreenTextShadowRect;
        Text offScreenTextShadow;

        // Floor glyph (up/down chevron) for upstairs/downstairs indication
        RectTransform onScreenFloorRect;
        Image onScreenFloorGlyph;
        RectTransform offScreenFloorRect;
        Image offScreenFloorGlyph;

        // ------------------------------ Runtime state ------------------------------

        Transform player;
        Camera cam;
        RohitFPSController rohitPlayer;
        PlayerHide playerHide;
        ResidentAI[] residents;
        float nextRefreshTime;
        float nextForceRefreshTime;
        // NavMeshPath cannot be `new`'d in a field initializer — Unity throws.
        NavMeshPath _distPathBuf;

        float pulseTimer;
        float tickRotation;

        PresentationMode mode;
        TargetData target;
        ViewportData view;

        Vector2 smoothedAnchor;
        float smoothedAssemblyAlpha;
        float smoothedOnAlpha;
        float smoothedOffAlpha;
        float smoothedArrowRotation;
        Color smoothedColor = Color.white;
        float smoothedSizeMultiplier = 1f;

        float targetAssemblyAlpha;
        float targetOnAlpha;
        float targetOffAlpha;
        float targetArrowRotation;
        Color targetColor;
        float targetSizeMultiplier = 1f;

        struct TargetData
        {
            public bool Valid;
            public ResidentAI Enemy;
            public Vector3 WorldAnchor;
            public float Distance;
            public ResidentAI.State AIState;
            public ThreatLevel Threat;
            public bool Occluded;
            public bool PlayerHiding;
            public FloorRelation Floor;
        }

        struct ViewportData
        {
            public Vector2 Anchored;
            public bool BehindCamera;
            public bool ExitsFrustum;
        }

        // ------------------------------ Bootstrap ------------------------------

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (instance != null) return;
            GameObject go = new GameObject("ResidentStartPointer");
            instance = go.AddComponent<ResidentStartPointer>();
        }

        void Awake()
        {
            if (instance != null && instance != this) { Destroy(gameObject); return; }
            instance = this;
            DontDestroyOnLoad(gameObject);
            BuildUI();
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        void OnDestroy()
        {
            if (instance == this) instance = null;
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        void OnSceneLoaded(Scene s, LoadSceneMode m)
        {
            ResolveReferences(true);
            nextRefreshTime = 0f; // force re-resolve on next LateUpdate too
        }
        void Start() => ResolveReferences(true);

        // ------------------------------ Main pipeline ------------------------------

        void LateUpdate()
        {
            if (Time.unscaledTime >= nextRefreshTime)
            {
                ResolveReferences(false);
                nextRefreshTime = Time.unscaledTime + 0.5f;
            }

            float smoothT = 1f - Mathf.Exp(-Mathf.Max(1f, smoothingSpeed) * Time.unscaledDeltaTime);
            tickRotation += tickRingSpinDegPerSec * Time.unscaledDeltaTime;

            targetAssemblyAlpha = 0f;
            targetOnAlpha = 0f;
            targetOffAlpha = 0f;

            if (!ApplyVisibilityRules(out target))
            {
                mode = PresentationMode.Hidden;
                ApplySmoothing(smoothT);
                return;
            }

            ComputeViewportPosition(target, out view);
            mode = DeterminePresentationMode(target, view);

            UpdateOnScreenIndicator(target, view);
            UpdateOffScreenIndicator(target, view);
            UpdateThreatVisuals(target);
            UpdateDistanceText(target);
            ApplyOcclusionStyle(target);
            ApplySmoothing(smoothT);
        }

        // ------------------------------ 1. Visibility ------------------------------

        bool ApplyVisibilityRules(out TargetData data)
        {
            data = default;

            if (IsGameplayUIBlocking()) return false;
            if (player == null || cam == null || !cam.isActiveAndEnabled) return false;

            ResidentAI nearest = PickNearest(out float distance);
            if (nearest == null)
            {
                ResolveReferences(true);
                nearest = PickNearest(out distance);
            }
            if (nearest == null) return false;
            if (distance < hideWhenCloserThan || distance > maxDisplayDistance) return false;

            data.Valid = true;
            data.Enemy = nearest;
            data.Distance = distance;
            data.WorldAnchor = nearest.transform.position + Vector3.up * worldAnchorHeight;
            data.AIState = nearest.state;
            data.Threat = GetThreatLevel(nearest);
            data.Occluded = IsOccluded(nearest);
            data.PlayerHiding = IsPlayerHidden();

            float heightDelta = nearest.transform.position.y - player.position.y;
            if (heightDelta > floorHeightThreshold) data.Floor = FloorRelation.Upstairs;
            else if (heightDelta < -floorHeightThreshold) data.Floor = FloorRelation.Downstairs;
            else data.Floor = FloorRelation.Same;

            if (data.PlayerHiding && !keepVisibleWhileHiding) return false;
            if (data.Occluded && !keepVisibleWhileOccluded) return false;

            return true;
        }

        // ------------------------------ 2. Viewport ------------------------------

        void ComputeViewportPosition(TargetData t, out ViewportData v)
        {
            Vector3 screen = cam.WorldToScreenPoint(t.WorldAnchor);
            bool behindCamera = screen.z < 0f;

            Vector2 center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            float maxX = Mathf.Max(20f, center.x - screenEdgePadding);
            float maxY = Mathf.Max(20f, center.y - screenEdgePadding);

            Vector2 raw;
            if (behindCamera)
            {
                // Threat is behind the camera. Pin to the BOTTOM edge with X reflecting
                // which side the target is on. Gives an unambiguous "behind you" read
                // instead of letting the inverted projection land anywhere on screen.
                Vector3 toTarget = t.WorldAnchor - cam.transform.position;
                float rightDot = Vector3.Dot(cam.transform.right, toTarget);
                float sideX = Mathf.Abs(rightDot) < 0.001f ? 0f : Mathf.Sign(rightDot);
                raw.x = sideX * maxX * 0.75f;
                raw.y = -maxY;
            }
            else
            {
                raw = new Vector2(screen.x, screen.y) - center;
            }

            bool clampedX = Mathf.Abs(raw.x) > maxX;
            bool clampedY = Mathf.Abs(raw.y) > maxY;

            v.BehindCamera = behindCamera;
            v.ExitsFrustum = behindCamera || clampedX || clampedY;
            v.Anchored = new Vector2(
                Mathf.Clamp(raw.x, -maxX, maxX),
                Mathf.Clamp(raw.y, -maxY, maxY));
        }

        // ------------------------------ 3. Presentation mode ------------------------------

        PresentationMode DeterminePresentationMode(TargetData t, ViewportData v)
        {
            if (!t.Valid) return PresentationMode.Hidden;
            return v.ExitsFrustum ? PresentationMode.OffScreen : PresentationMode.OnScreen;
        }

        // ------------------------------ 4. On-screen ------------------------------

        void UpdateOnScreenIndicator(TargetData t, ViewportData v)
        {
            targetAssemblyAlpha = 1f;
            targetOnAlpha = (mode == PresentationMode.OnScreen) ? 1f : 0f;
        }

        // ------------------------------ 5. Off-screen ------------------------------

        void UpdateOffScreenIndicator(TargetData t, ViewportData v)
        {
            targetOffAlpha = (mode == PresentationMode.OffScreen) ? 1f : 0f;
            if (mode == PresentationMode.OffScreen && v.Anchored.sqrMagnitude > 0.1f)
                targetArrowRotation = Mathf.Atan2(v.Anchored.y, v.Anchored.x) * Mathf.Rad2Deg - 90f;
        }

        // ------------------------------ 6. Threat visuals ------------------------------

        void UpdateThreatVisuals(TargetData t)
        {
            targetColor = GetThreatColor(t.Threat);

            pulseTimer += Time.unscaledDeltaTime / Mathf.Max(0.1f, pulsePeriod);
            float phase = Mathf.Sin(pulseTimer * Mathf.PI * 2f);
            float amt = pulseAmount + (t.Threat == ThreatLevel.Seen ? chasePulseAccent : 0f);
            targetSizeMultiplier = 1f + phase * amt;
        }

        // ------------------------------ 7. Distance text ------------------------------

        void UpdateDistanceText(TargetData t)
        {
            string label;
            if (!showDistanceText)
            {
                label = string.Empty;
            }
            else
            {
                string prefix = string.Empty;
                if (addFloorArrowToText)
                {
                    if (t.Floor == FloorRelation.Upstairs)   prefix = "UP ";
                    else if (t.Floor == FloorRelation.Downstairs) prefix = "DN ";
                }
                string core = $"{t.Distance:0}M";
                label = useBracketedText ? $"•  {prefix}{core}  •" : $"{prefix}{core}";
            }

            SetLabel(onScreenText, onScreenTextShadow, label);
            SetLabel(offScreenText, offScreenTextShadow, label);

            PositionLabel(onScreenTextRect, onScreenTextShadowRect, onScreenSize);
            PositionLabel(offScreenTextRect, offScreenTextShadowRect, offScreenSize);
        }

        static void SetLabel(Text main, Text shadow, string label)
        {
            if (main != null) main.text = label;
            if (shadow != null) shadow.text = label;
        }

        void PositionLabel(RectTransform main, RectTransform shadow, float ownerSize)
        {
            Vector2 pos = new Vector2(0f, -(ownerSize * 0.5f + textOffset));
            if (main != null) main.anchoredPosition = pos;
            if (shadow != null) shadow.anchoredPosition = pos + new Vector2(1f, -1f);
        }

        // ------------------------------ 8. Occlusion / hiding opacity ------------------------------

        void ApplyOcclusionStyle(TargetData t)
        {
            float alpha = visibleAlpha;
            if (t.Occluded) alpha = Mathf.Min(alpha, occludedAlpha);
            if (t.PlayerHiding) alpha = Mathf.Min(alpha, hidingAlpha);
            targetAssemblyAlpha = alpha;
        }

        // ------------------------------ 9. Smoothing / apply ------------------------------

        void ApplySmoothing(float smoothT)
        {
            smoothedAssemblyAlpha = Mathf.Lerp(smoothedAssemblyAlpha, targetAssemblyAlpha, smoothT);
            smoothedOnAlpha = Mathf.Lerp(smoothedOnAlpha, targetOnAlpha, smoothT);
            smoothedOffAlpha = Mathf.Lerp(smoothedOffAlpha, targetOffAlpha, smoothT);
            smoothedArrowRotation = Mathf.LerpAngle(smoothedArrowRotation, targetArrowRotation, smoothT);
            smoothedColor = Color.Lerp(smoothedColor, targetColor, smoothT);
            smoothedSizeMultiplier = Mathf.Lerp(smoothedSizeMultiplier, targetSizeMultiplier, smoothT);

            if (target.Valid)
                smoothedAnchor = Vector2.Lerp(smoothedAnchor, view.Anchored, smoothT);

            if (assemblyGroup != null) assemblyGroup.alpha = smoothedAssemblyAlpha;

            bool shouldBeActive = smoothedAssemblyAlpha > 0.01f || targetAssemblyAlpha > 0.01f;
            if (rootRect != null && rootRect.gameObject.activeSelf != shouldBeActive)
                rootRect.gameObject.SetActive(shouldBeActive);
            if (!shouldBeActive) return;

            if (onScreenGroup != null) onScreenGroup.alpha = smoothedOnAlpha;
            if (offScreenGroup != null) offScreenGroup.alpha = smoothedOffAlpha;

            rootRect.anchoredPosition = smoothedAnchor;
            offScreenRect.localRotation = Quaternion.Euler(0f, 0f, smoothedArrowRotation);

            float sizeMul = smoothedSizeMultiplier;
            float onSize = Mathf.Clamp(onScreenSize * sizeMul, minSize, maxSize);
            float offSize = Mathf.Clamp(offScreenSize * sizeMul, minSize, maxSize);
            onScreenRect.sizeDelta = new Vector2(onSize, onSize);
            offScreenRect.sizeDelta = new Vector2(offSize, offSize);

            // Halo rectangles scale relative to their parent ring.
            if (onScreenHaloRect != null)
                onScreenHaloRect.sizeDelta = new Vector2(onSize * haloScale, onSize * haloScale);
            if (offScreenHaloRect != null)
                offScreenHaloRect.sizeDelta = new Vector2(offSize * haloScale * 0.85f, offSize * haloScale * 0.85f);

            // Tick ring slowly rotates in the opposite of the arrow rotation, so it feels like a scanner.
            if (onScreenTickRect != null)
                onScreenTickRect.localRotation = Quaternion.Euler(0f, 0f, tickRotation);

            // Colors
            if (onScreenTickRing != null) onScreenTickRing.color = smoothedColor;
            if (onScreenCoreDot != null) onScreenCoreDot.color = smoothedColor;
            if (offScreenChevron != null) offScreenChevron.color = smoothedColor;

            // Halos have their own pulsing alpha layered over the base color.
            float haloPhase = 0.5f + 0.5f * Mathf.Sin(pulseTimer * Mathf.PI * 2f);
            float haloA = Mathf.Clamp01(haloMaxAlpha * (1f - haloPulseAmount + haloPulseAmount * haloPhase));
            if (onScreenHalo != null)
                onScreenHalo.color = new Color(smoothedColor.r, smoothedColor.g, smoothedColor.b, haloA);
            if (offScreenHalo != null)
                offScreenHalo.color = new Color(smoothedColor.r, smoothedColor.g, smoothedColor.b, haloA);

            ApplyFloorGlyphs(onSize, offSize, smoothT);
            ApplyTextStyle();
        }

        void ApplyFloorGlyphs(float onSize, float offSize, float smoothT)
        {
            if (!showFloorGlyph) target.Floor = FloorRelation.Same;

            // Bob animation on the glyph so it reads as "alive" and attention-grabbing.
            float bob = Mathf.Sin(Time.unscaledTime * floorGlyphBobSpeed) * floorGlyphBobAmount;

            ApplyFloorGlyph(onScreenFloorRect, onScreenFloorGlyph, onSize, bob);
            ApplyFloorGlyph(offScreenFloorRect, offScreenFloorGlyph, offSize, bob);
        }

        void ApplyFloorGlyph(RectTransform rect, Image img, float ownerSize, float bob)
        {
            if (rect == null || img == null) return;

            float targetAlpha;
            Vector2 targetPos;
            Quaternion targetRot;
            float size = ownerSize * floorGlyphScale;

            switch (target.Floor)
            {
                case FloorRelation.Upstairs:
                    targetAlpha = 1f;
                    targetPos = new Vector2(0f, ownerSize * floorGlyphOffset + bob * ownerSize);
                    targetRot = Quaternion.identity;           // chevron tip points UP
                    break;
                case FloorRelation.Downstairs:
                    targetAlpha = 1f;
                    targetPos = new Vector2(0f, -(ownerSize * floorGlyphOffset) - bob * ownerSize);
                    targetRot = Quaternion.Euler(0f, 0f, 180f); // chevron tip points DOWN
                    break;
                default:
                    targetAlpha = 0f;
                    targetPos = rect.anchoredPosition;          // hold last position while fading
                    targetRot = rect.localRotation;
                    break;
            }

            rect.sizeDelta = new Vector2(size, size);
            rect.anchoredPosition = Vector2.Lerp(rect.anchoredPosition, targetPos, smoothingSpeed * Time.unscaledDeltaTime);
            rect.localRotation = Quaternion.Slerp(rect.localRotation, targetRot, smoothingSpeed * Time.unscaledDeltaTime);

            float currentA = img.color.a;
            float newA = Mathf.Lerp(currentA, targetAlpha, 1f - Mathf.Exp(-Mathf.Max(1f, smoothingSpeed) * Time.unscaledDeltaTime));
            img.color = new Color(floorGlyphColor.r, floorGlyphColor.g, floorGlyphColor.b, newA);
        }

        void ApplyTextStyle()
        {
            int fs = Mathf.RoundToInt(textSize);
            if (onScreenText != null) { onScreenText.fontSize = fs; onScreenText.color = textColor; }
            if (offScreenText != null) { offScreenText.fontSize = fs; offScreenText.color = textColor; }
            if (onScreenTextShadow != null) { onScreenTextShadow.fontSize = fs; onScreenTextShadow.color = textShadowColor; }
            if (offScreenTextShadow != null) { offScreenTextShadow.fontSize = fs; offScreenTextShadow.color = textShadowColor; }
        }

        // ------------------------------ Helpers ------------------------------

        ThreatLevel GetThreatLevel(ResidentAI resident)
        {
            if (resident == null)
                return ThreatLevel.Safe;

            float rawDistance = player != null
                ? Vector3.Distance(player.position, resident.transform.position)
                : float.PositiveInfinity;

            if (resident.IndicatorSeesPlayer || HasResidentLiveSight(resident, rawDistance))
                return ThreatLevel.Seen;

            if (resident.IndicatorSensesPlayer ||
                resident.state == ResidentAI.State.Search ||
                resident.state == ResidentAI.State.Investigate ||
                resident.state == ResidentAI.State.Chase ||
                HasResidentLiveSense(resident, rawDistance))
                return ThreatLevel.Sensed;

            return ThreatLevel.Safe;
        }

        bool HasResidentLiveSight(ResidentAI resident, float rawDistance)
        {
            if (resident == null || player == null || IsPlayerHidden())
                return false;

            float sightRange = Mathf.Max(2.5f, resident.sightRange);
            if (rawDistance > sightRange)
                return false;

            Vector3 toPlayer = player.position - resident.transform.position;
            toPlayer.y = 0f;
            if (toPlayer.sqrMagnitude < 0.001f)
                return true;

            Vector3 residentForward = resident.transform.forward;
            residentForward.y = 0f;
            if (residentForward.sqrMagnitude < 0.001f)
                residentForward = Vector3.forward;

            float angle = Vector3.Angle(residentForward.normalized, toPlayer.normalized);
            if (!resident.omnidirectionalVision && angle > resident.fovDegrees * 0.5f)
                return false;

            return HasAnyClearResidentToPlayerLine(resident);
        }

        bool HasResidentLiveSense(ResidentAI resident, float rawDistance)
        {
            if (resident == null || player == null || IsPlayerHidden())
                return false;

            float senseRange = Mathf.Max(
                resident.closeAwarenessDistance,
                resident.proximityChaseDistance,
                resident.killDistance + 1.5f);
            return rawDistance <= senseRange && HasAnyClearResidentToPlayerLine(resident);
        }

        bool HasAnyClearResidentToPlayerLine(ResidentAI resident)
        {
            if (resident == null || player == null)
                return false;

            Vector3 origin = resident.transform.position + Vector3.up * 1.35f;
            Vector3 lateral = player.right;
            lateral.y = 0f;
            if (lateral.sqrMagnitude < 0.0001f)
                lateral = Vector3.right;
            lateral = lateral.normalized * 0.18f;

            Vector3[] targets =
            {
                player.position + Vector3.up * 1.45f,
                player.position + Vector3.up * 1.1f,
                player.position + Vector3.up * 0.75f,
                player.position + Vector3.up * 1.1f + lateral,
                player.position + Vector3.up * 1.1f - lateral
            };

            for (int i = 0; i < targets.Length; i++)
            {
                if (HasClearResidentToPlayerLine(resident, origin, targets[i]))
                    return true;
            }

            return false;
        }

        bool HasClearResidentToPlayerLine(ResidentAI resident, Vector3 origin, Vector3 target)
        {
            Vector3 dir = target - origin;
            float dist = dir.magnitude;
            if (dist <= 0.05f)
                return true;

            dir /= dist;
            RaycastHit[] hits = Physics.RaycastAll(origin, dir, dist + 0.05f, ~0, QueryTriggerInteraction.Ignore);
            if (hits.Length == 0)
                return true;

            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            for (int i = 0; i < hits.Length; i++)
            {
                Collider col = hits[i].collider;
                if (col == null)
                    continue;

                Transform tr = col.transform;
                if (tr == resident.transform || tr.IsChildOf(resident.transform))
                    continue;
                if (tr == player || tr.IsChildOf(player) ||
                    tr.GetComponentInParent<RohitFPSController>() != null ||
                    tr.GetComponentInParent<PlayerDeath>() != null)
                    return true;

                return false;
            }

            return false;
        }

        Color GetThreatColor(ThreatLevel threat)
        {
            return threat switch
            {
                ThreatLevel.Seen => seenColor,
                ThreatLevel.Sensed => sensedColor,
                _ => safeColor
            };
        }

        ResidentAI PickNearest(out float bestDistance)
        {
            bestDistance = float.PositiveInfinity;
            ResidentAI best = null;
            if (residents == null || player == null) return null;

            for (int i = 0; i < residents.Length; i++)
            {
                var r = residents[i];
                if (r == null || !r.gameObject.activeInHierarchy) continue;
                float d = GetResidentDisplayDistance(r);
                if (d < bestDistance) { bestDistance = d; best = r; }
            }
            if (best == null) bestDistance = -1f;
            return best;
        }

        float GetResidentDisplayDistance(ResidentAI resident)
        {
            if (resident == null || player == null)
                return float.PositiveInfinity;

            float raw = Vector3.Distance(player.position, resident.transform.position);
            if (!usePathDistance)
                return raw;

            NavMeshPath path = _distPathBuf ??= new NavMeshPath();
            if (NavMesh.CalculatePath(player.position, resident.transform.position, NavMesh.AllAreas, path) &&
                path.status == NavMeshPathStatus.PathComplete &&
                path.corners != null &&
                path.corners.Length > 1)
            {
                float sum = 0f;
                for (int i = 1; i < path.corners.Length; i++)
                    sum += Vector3.Distance(path.corners[i - 1], path.corners[i]);
                return Mathf.Max(raw, sum);
            }

            float floorGap = Mathf.Max(0f, Mathf.Abs(resident.transform.position.y - player.position.y) - floorHeightThreshold);
            float floorPenalty = Mathf.Ceil(floorGap / Mathf.Max(0.5f, floorHeightThreshold)) * fallbackFloorDistancePenalty;
            return raw + floorPenalty;
        }

        bool IsOccluded(ResidentAI nearest)
        {
            if (nearest == null || cam == null) return false;
            Vector3 from = cam.transform.position;
            Vector3 to = nearest.transform.position + Vector3.up * 1.2f;
            Vector3 dir = to - from;
            float len = dir.magnitude;
            if (len <= 0.1f) return false;
            dir /= len;

            if (!Physics.Raycast(from, dir, out RaycastHit hit, len, occlusionMask, QueryTriggerInteraction.Ignore))
                return false;

            Transform tr = hit.collider != null ? hit.collider.transform : null;
            if (tr == null) return false;
            if (tr == nearest.transform || tr.IsChildOf(nearest.transform)) return false;
            return true;
        }

        bool IsPlayerHidden()
        {
            if (rohitPlayer != null && rohitPlayer.isHidden) return true;
            if (playerHide != null && playerHide.IsHidden) return true;
            return false;
        }

        bool IsGameplayUIBlocking()
        {
            return PauseOverlay.IsPaused
                || GameOverOverlay.IsShowing
                || EscapeOverlay.IsShowing;
        }

        void ResolveReferences(bool force)
        {
            if (!force && Time.unscaledTime >= nextForceRefreshTime)
            {
                force = true;
                nextForceRefreshTime = Time.unscaledTime + 2f;
            }

            // Camera: try every possible lookup — tag is often wrong or missing
            if (force || cam == null || !cam.isActiveAndEnabled)
            {
                Camera found = Camera.main;
                if (found == null) found = FindFirstObjectByType<Camera>();
                if (found == null)
                {
                    Camera[] all = FindObjectsByType<Camera>(FindObjectsSortMode.None);
                    if (all != null)
                        foreach (var c in all) { if (c != null && c.isActiveAndEnabled) { found = c; break; } }
                }
                if (found != null) cam = found;
            }

            // Player: try Rohit, then PlayerDeath, then PlayerHide, then "Player" tag
            if (force || player == null)
            {
                var rohit = FindFirstObjectByType<RohitFPSController>();
                if (rohit != null) { player = rohit.transform; rohitPlayer = rohit; }

                if (player == null)
                {
                    var death = FindFirstObjectByType<PlayerDeath>();
                    if (death != null) player = death.transform;
                }

                if (player == null)
                {
                    var hide = FindFirstObjectByType<PlayerHide>();
                    if (hide != null) player = hide.transform;
                }

                if (player == null)
                {
                    var tagged = GameObject.FindGameObjectWithTag("Player");
                    if (tagged != null) player = tagged.transform;
                }

                if (player != null)
                {
                    if (rohitPlayer == null) rohitPlayer = player.GetComponent<RohitFPSController>();
                    if (playerHide  == null) playerHide  = player.GetComponent<PlayerHide>();
                }
            }

            // Residents: re-fetch if empty or any entry was destroyed
            bool needsResidentRefresh = force || residents == null || residents.Length == 0;
            if (!needsResidentRefresh)
                foreach (var r in residents) { if (r == null) { needsResidentRefresh = true; break; } }
            if (needsResidentRefresh)
                residents = FindObjectsByType<ResidentAI>(FindObjectsSortMode.None);
        }

        // ------------------------------ UI construction ------------------------------

        void BuildUI()
        {
            canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = short.MaxValue;

            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = 1f;
            gameObject.AddComponent<GraphicRaycaster>();

            rootRect = CreateRect("MarkerRoot", transform);
            rootRect.anchorMin = new Vector2(0.5f, 0.5f);
            rootRect.anchorMax = new Vector2(0.5f, 0.5f);
            rootRect.pivot = new Vector2(0.5f, 0.5f);
            rootRect.sizeDelta = Vector2.zero;

            assemblyGroup = rootRect.gameObject.AddComponent<CanvasGroup>();
            assemblyGroup.alpha = 0f;
            assemblyGroup.blocksRaycasts = false;
            assemblyGroup.interactable = false;

            BuildOnScreenGroup();
            BuildOffScreenGroup();

            onScreenGroup.alpha = 0f;
            offScreenGroup.alpha = 0f;
            rootRect.gameObject.SetActive(false);
        }

        void BuildOnScreenGroup()
        {
            onScreenGroup = CreateSubGroup("OnScreen", rootRect);

            // Size anchor: an empty rect sized to onScreenSize; children pin to its center.
            onScreenRect = CreateRect("Size", onScreenGroup.transform);
            onScreenRect.anchorMin = new Vector2(0.5f, 0.5f);
            onScreenRect.anchorMax = new Vector2(0.5f, 0.5f);
            onScreenRect.pivot = new Vector2(0.5f, 0.5f);
            onScreenRect.sizeDelta = new Vector2(onScreenSize, onScreenSize);

            onScreenHaloRect = AddImage("Halo", onScreenRect, GetHaloSprite(), out onScreenHalo,
                new Vector2(onScreenSize * haloScale, onScreenSize * haloScale));
            onScreenHalo.color = new Color(safeColor.r, safeColor.g, safeColor.b, 0f);

            onScreenTickRect = AddImage("TickRing", onScreenRect, GetTickRingSprite(), out onScreenTickRing,
                new Vector2(onScreenSize, onScreenSize));
            onScreenTickRing.color = safeColor;

            onScreenCoreRect = AddImage("Core", onScreenRect, GetCoreDotSprite(), out onScreenCoreDot,
                new Vector2(onScreenSize * 0.32f, onScreenSize * 0.32f));
            onScreenCoreDot.color = safeColor;

            onScreenFloorRect = AddImage("FloorGlyph", onScreenRect, GetChevronSprite(), out onScreenFloorGlyph,
                new Vector2(onScreenSize * floorGlyphScale, onScreenSize * floorGlyphScale));
            onScreenFloorGlyph.color = new Color(floorGlyphColor.r, floorGlyphColor.g, floorGlyphColor.b, 0f);

            CreateLabel("Distance", onScreenGroup.transform,
                out onScreenTextRect, out onScreenText,
                out onScreenTextShadowRect, out onScreenTextShadow);
        }

        void BuildOffScreenGroup()
        {
            offScreenGroup = CreateSubGroup("OffScreen", rootRect);

            offScreenRect = CreateRect("Size", offScreenGroup.transform);
            offScreenRect.anchorMin = new Vector2(0.5f, 0.5f);
            offScreenRect.anchorMax = new Vector2(0.5f, 0.5f);
            offScreenRect.pivot = new Vector2(0.5f, 0.5f);
            offScreenRect.sizeDelta = new Vector2(offScreenSize, offScreenSize);

            offScreenHaloRect = AddImage("Halo", offScreenRect, GetHaloSprite(), out offScreenHalo,
                new Vector2(offScreenSize * haloScale * 0.85f, offScreenSize * haloScale * 0.85f));
            offScreenHalo.color = new Color(safeColor.r, safeColor.g, safeColor.b, 0f);

            offScreenChevronRect = AddImage("Chevron", offScreenRect, GetChevronSprite(), out offScreenChevron,
                new Vector2(offScreenSize, offScreenSize));
            offScreenChevron.color = safeColor;

            // Floor glyph for off-screen: sibling of the rotating chevron rect (so it doesn't rotate with the arrow).
            offScreenFloorRect = AddImage("FloorGlyph", offScreenGroup.transform, GetChevronSprite(), out offScreenFloorGlyph,
                new Vector2(offScreenSize * floorGlyphScale, offScreenSize * floorGlyphScale));
            offScreenFloorGlyph.color = new Color(floorGlyphColor.r, floorGlyphColor.g, floorGlyphColor.b, 0f);

            CreateLabel("Distance", offScreenGroup.transform,
                out offScreenTextRect, out offScreenText,
                out offScreenTextShadowRect, out offScreenTextShadow);
        }

        RectTransform CreateRect(string name, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            return rt;
        }

        CanvasGroup CreateSubGroup(string name, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasGroup));
            go.transform.SetParent(parent, false);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = Vector2.zero;
            CanvasGroup cg = go.GetComponent<CanvasGroup>();
            cg.blocksRaycasts = false;
            cg.interactable = false;
            cg.alpha = 0f;
            return cg;
        }

        RectTransform AddImage(string name, Transform parent, Sprite sprite, out Image image, Vector2 size)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.raycastTarget = false;
            image.preserveAspect = true;
            return rt;
        }

        void CreateLabel(string name, Transform parent,
                         out RectTransform mainRect, out Text main,
                         out RectTransform shadowRect, out Text shadow)
        {
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            GameObject shadowGo = new GameObject(name + "Shadow", typeof(RectTransform));
            shadowGo.transform.SetParent(parent, false);
            shadowRect = shadowGo.GetComponent<RectTransform>();
            shadowRect.anchorMin = new Vector2(0.5f, 0.5f);
            shadowRect.anchorMax = new Vector2(0.5f, 0.5f);
            shadowRect.pivot = new Vector2(0.5f, 1f);
            shadowRect.sizeDelta = new Vector2(140f, 24f);
            shadow = shadowGo.AddComponent<Text>();
            shadow.font = font;
            shadow.fontSize = Mathf.RoundToInt(textSize);
            shadow.alignment = TextAnchor.UpperCenter;
            shadow.color = textShadowColor;
            shadow.raycastTarget = false;
            shadow.horizontalOverflow = HorizontalWrapMode.Overflow;

            GameObject mainGo = new GameObject(name, typeof(RectTransform));
            mainGo.transform.SetParent(parent, false);
            mainRect = mainGo.GetComponent<RectTransform>();
            mainRect.anchorMin = new Vector2(0.5f, 0.5f);
            mainRect.anchorMax = new Vector2(0.5f, 0.5f);
            mainRect.pivot = new Vector2(0.5f, 1f);
            mainRect.sizeDelta = new Vector2(140f, 24f);
            main = mainGo.AddComponent<Text>();
            main.font = font;
            main.fontSize = Mathf.RoundToInt(textSize);
            main.alignment = TextAnchor.UpperCenter;
            main.color = textColor;
            main.raycastTarget = false;
            main.horizontalOverflow = HorizontalWrapMode.Overflow;
        }

        // ------------------------------ Procedural sprites ------------------------------

        static Sprite GetHaloSprite()
        {
            if (haloSprite != null) return haloSprite;

            const int size = 128;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            float cx = (size - 1) * 0.5f;
            float cy = (size - 1) * 0.5f;
            float maxDist = cx;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - cx;
                    float dy = y - cy;
                    float r = Mathf.Sqrt(dx * dx + dy * dy) / maxDist;
                    // Gaussian-ish falloff, concentrated toward center.
                    float alpha = Mathf.Exp(-r * r * 3.4f);
                    alpha = Mathf.Clamp01(alpha * 0.9f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            tex.Apply();
            haloSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            return haloSprite;
        }

        static Sprite GetTickRingSprite()
        {
            if (tickRingSprite != null) return tickRingSprite;

            const int size = 128;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            float cx = (size - 1) * 0.5f;
            float cy = (size - 1) * 0.5f;
            float ringMid = size * 0.39f;
            float ringHalfThickness = size * 0.035f;
            float tickStart = size * 0.42f;
            float tickEnd = size * 0.48f;
            float tickAngularHalfWidth = 0.06f; // radians

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - cx;
                    float dy = y - cy;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);

                    float ring = Mathf.Clamp01(1f - Mathf.Abs(r - ringMid) / ringHalfThickness);

                    float tick = 0f;
                    if (r >= tickStart && r <= tickEnd)
                    {
                        float angle = Mathf.Atan2(dy, dx); // -PI..PI
                        // 4 cardinal ticks at 0, 90, 180, 270.
                        for (int t = 0; t < 4; t++)
                        {
                            float target = t * (Mathf.PI * 0.5f) - Mathf.PI;
                            // Normalize angular distance.
                            float delta = Mathf.Abs(Mathf.DeltaAngle(angle * Mathf.Rad2Deg, target * Mathf.Rad2Deg)) * Mathf.Deg2Rad;
                            if (delta < tickAngularHalfWidth)
                            {
                                float k = 1f - delta / tickAngularHalfWidth;
                                tick = Mathf.Max(tick, k);
                            }
                        }
                    }

                    float alpha = Mathf.Clamp01(Mathf.Max(ring, tick));
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            tex.Apply();
            tickRingSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            return tickRingSprite;
        }

        static Sprite GetCoreDotSprite()
        {
            if (coreDotSprite != null) return coreDotSprite;

            const int size = 64;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            float cx = (size - 1) * 0.5f;
            float cy = (size - 1) * 0.5f;
            float r0 = size * 0.46f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - cx;
                    float dy = y - cy;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float alpha = Mathf.Clamp01(1f - Mathf.Max(0f, r - r0 * 0.85f) / (r0 * 0.15f));
                    // Slight inner highlight
                    if (r < r0 * 0.25f) alpha = Mathf.Max(alpha, 1f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            tex.Apply();
            coreDotSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            return coreDotSprite;
        }

        // Chevron: thicker, crisper arrow pointing up with a short inner highlight.
        static Sprite GetChevronSprite()
        {
            if (chevronSprite != null) return chevronSprite;

            const int size = 128;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            float cx = (size - 1) * 0.5f;
            float outerHalf = size * 0.46f;
            float strokeThickness = size * 0.16f;

            for (int y = 0; y < size; y++)
            {
                float t = y / (float)(size - 1);
                float outer = Mathf.Lerp(outerHalf, 2f, t);
                float inner = Mathf.Max(0f, outer - strokeThickness);
                float topFade = 1f - Mathf.SmoothStep(0.92f, 1f, t);
                float bottomFade = Mathf.SmoothStep(0f, 0.18f, t);

                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Abs(x - cx);
                    float edgeOut = Mathf.Clamp01(outer - dx);
                    float edgeIn = Mathf.Clamp01(dx - inner);
                    float alpha = Mathf.Clamp01(edgeOut * edgeIn * topFade * bottomFade);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            tex.Apply();
            chevronSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            return chevronSprite;
        }
    }
}
