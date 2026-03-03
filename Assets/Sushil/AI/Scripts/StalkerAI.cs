using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Sushil.Systems;

namespace Sushil.AI
{
    public class StalkerAI : MonoBehaviour
    {
        public enum State { Patrol, Investigate, Search, Chase }

        [Header("References")]
        public NavMeshAgent agent;
        public Transform player;
        public LayerMask losMask;
        public List<Transform> patrolPoints = new();
        public bool useFreeRoamPatrol = true;
        public float freeRoamRadius = 45f;
        public int roamSampleAttempts = 16;
        public float minWallClearance = 0.75f;

        [Header("Patrol Roaming")]
        public bool patrolAroundPlayer = false;
        [Range(0f, 1f)] public float globalPatrolChance = 0.35f;
        public float localPatrolRadius = 14f;
        public bool roamWholeHouse = true;
        [Range(0f, 1f)] public float patrolPointVisitChance = 0.45f;
        [Range(0f, 1f)] public float lastNoiseRoomBiasChance = 0.6f;
        public float lastNoiseRoomBiasDuration = 25f;
        public float lastNoiseRoomRadius = 10f;

        [Header("Spawn Randomization")]
        public bool randomizeSpawnOnStart = true;
        public float spawnSearchRadius = 60f;
        public int spawnSampleAttempts = 64;
        public float minSpawnDistanceFromPlayer = 14f;
        [Tooltip("Extra guard: keep spawn away from player's initial spawn position.")]
        public bool enforceSpawnAwayFromPlayerSpawn = true;
        public float minSpawnDistanceFromPlayerSpawn = 18f;
        public float minSpawnDistanceFromKeys = 10f;
        public float minSpawnDistanceFromHidingSpots = 8f;
        [Range(0f, 180f)] public float playerViewExclusionHalfAngle = 60f;
        public bool avoidPlayerForwardConeAtSpawn = true;
        [Tooltip("Wait briefly before random spawn so door/nav obstacle states finish initializing.")]
        public bool deferSpawnUntilDoorsReady = true;
        public float spawnInitDelaySeconds = 0.2f;

        [Header("Navigation Collision Fixes")]
        public bool autoAddObstaclesForHideablesAndCupboards = true;
        public bool autoSyncAgentAndColliderToScale = true;
        [Tooltip("If true, NavMeshAgent sizing uses collider's authored values and ignores root transform scale. Keep this on when visual scale is enlarged.")]
        public bool ignoreRootScaleForNavSync = true;
        [Tooltip("Keeps stalker collider world size stable when root scale is large, so it can still pass doors.")]
        public bool normalizeColliderWorldSize = true;
        [Tooltip("Force agent capsule dimensions each run so door traversal remains stable.")]
        public bool enforceAgentSize = true;
        [Range(0.25f, 0.45f)] public float enforcedAgentRadius = 0.40f;
        [Range(1.8f, 2.3f)] public float enforcedAgentHeight = 2.20f;
        public float obstacleExtraPadding = 0.05f;

        [Header("Navigation Stability")]
        public bool keepAgentSnappedToNavMesh = true;
        public float navSnapSearchRadius = 1.6f;
        public float navSnapWarpDistance = 0.45f;
        public float destinationSampleRadius = 2.2f;
        public int destinationRetrySamples = 10;
        public float destinationRetryRadius = 3.2f;

        [Header("Hard Movement Constraints")]
        [Tooltip("Layers treated as hard blockers for stalker movement validation.")]
        public LayerMask blockingGeometryMask = ~0;
        [Tooltip("Rejects destinations that overlap with walls/props/hideable colliders.")]
        public bool rejectDestinationsInsideBlockingGeometry = true;
        [Tooltip("Rejects destinations inside or too close to hideable colliders.")]
        public bool rejectDestinationsInsideHideables = true;
        [Tooltip("Extra clearance from hard geometry to avoid clipping walls.")]
        public float destinationWallClearance = 0.28f;
        [Tooltip("Clearance around hideable colliders the stalker should not enter.")]
        public float destinationHideableClearance = 0.45f;
        [Tooltip("Validates each NavMesh path segment against geometry so AI enters rooms only via valid openings.")]
        public bool validatePathAgainstGeometry = true;
        [Tooltip("Final runtime anti-clip guard: reverts stalker if it crosses blocking geometry in a frame.")]
        public bool enforceRuntimeAntiClip = true;
        [Tooltip("Capsule/line check height for runtime anti-clip.")]
        public float antiClipCheckHeight = 1.0f;
        [Tooltip("Probe radius for runtime anti-clip overlap checks.")]
        public float antiClipProbeRadius = 0.22f;
        [Tooltip("Also prevents crossing navmesh boundaries in a frame (independent of physics colliders).")]
        public bool enforceNavMeshBoundaryAntiClip = true;
        [Tooltip("Minimum penetration depth before anti-clip forces recovery.")]
        public float antiClipPenetrationEpsilon = 0.03f;

        [Header("Hiding Spot Checks (Alpha)")]
        public List<Transform> hidingInspectPoints = new();
        [Range(0f, 1f)] public float checkHidingChance = 0.4f;
        public float inspectPauseMin = 0.8f;
        public float inspectPauseMax = 1.4f;

        [Header("Vision")]
        public float sightRange = 12f;
        public float fovDegrees = 110f;
        [Tooltip("Uses full-world occlusion for LOS so walls always block vision regardless of layer mask setup.")]
        public bool strictWallOcclusionVision = true;
        [Tooltip("If enabled, FOV check is ignored and stalker can detect in all directions (still requires LOS + range).")]
        public bool omnidirectionalVision = true;
        [Tooltip("Continuous visible time required before target is treated as visible.")]
        public float visionAcquireSeconds = 0.08f;
        [Tooltip("Continuous hidden time required before target is treated as lost.")]
        public float visionLoseSeconds = 0.25f;
        public bool alwaysChaseWhenVisible = true;
        public bool sightAlwaysStartsChase = true;
        public bool requireNoiseToChase = true;
        public float noiseChaseWindow = 12f;
        public bool allowProximityChaseWithoutNoise = false;
        public float proximityChaseDistance = 2.2f;
        public bool maintainChaseUntilHidden = true;
        public bool globalNoiseForcesChase = true;
        [Tooltip("If true, sound-tracked position is fuzzy so stalker does not know exact position without sight.")]
        public bool noiseTrackingHasUncertainty = true;
        public float noiseTrackingMinError = 0.8f;
        public float noiseTrackingMaxError = 2.2f;

        [Header("Distraction")]
        public bool distractOnThrowNoise = true;
        public string throwReleaseNoiseType = "throw";
        public string throwImpactNoiseType = "throwImpact";
        public float throwNoiseHearingBoost = 1.8f;
        public float minThrowHearingRadius = 25f;
        public float distractionSightSuppressSeconds = 2.5f;
        
        [Header("Timed Visual Chase")]
        public bool limitVisualChaseTime = true;
        public float maxVisualChaseSeconds = 7f;
        public float chaseMemorySeconds = 2.0f;
        [Tooltip("After LOS is lost, keep hard pursuit for this long before dropping out of chase.")]
        public float lostSightPursuitSeconds = 5.0f;
        [Tooltip("Prevents far-distance chase drop while target is visible/recently seen.")]
        public bool keepChaseWhenRecentlySeen = true;
        [Tooltip("If true, LOS chase will not drop due visual timer while target is being tracked.")]
        public bool relentlessVisualChase = true;

        [Header("Noise Hearing")]
        public float minNoiseIntensity = 1f;
        public float hearingScale = 2.2f;
        public float minHearingRadius = 6f;
        private Vector3 lastNoisePos;
        private float lastNoiseIntensity;
        private bool hasNoise;
        private float lastHeardNoiseTime = -999f;

        [Header("Search Behavior")]
        public float searchDuration = 8f;
        public float searchRadius = 4f;
        [Range(0f, 1f)] public float roomSuspicionPortion = 0.65f;
        public float outwardSearchMultiplier = 2.3f;
        [Range(0f, 1f)] public float fakeLeaveChance = 0.6f;
        public float throwDistractionSearchDuration = 5.5f;
        public bool forceFakeLeaveAfterPlayerHides = true;
        public float hideTriggeredSearchDuration = 7.5f;
        public float lastSeenSearchDurationBoost = 2f;

        [Header("Creep Moments (optional but nice)")]
        [Range(0f, 1f)] public float pauseOutsideChance = 0.25f;
        public float pauseMin = 0.8f;
        public float pauseMax = 1.5f;

        [Header("Kill")]
        public float killDistance = 1.7f;

        [Header("Chase Loss")]
        public bool loseChaseWhenFar = true;
        public float maxChaseDistance = 14f;
        public float farLoseDelay = 0.6f;

        [Header("Movement Speeds")]
        [Tooltip("Speed used during patrol/investigate/search.")]
        public float patrolMoveSpeed = 3.8f;
        [Tooltip("Speed used while chasing the player.")]
        public float chaseMoveSpeed = 6.8f;
        [Tooltip("Higher acceleration during chase prevents slow ramp-up.")]
        public float chaseAcceleration = 16f;

        [Header("Chase Doorway Assist")]
        public bool enableDoorwayAssist = true;
        public float doorwayStuckSeconds = 1.1f;
        public float doorwayAssistRange = 14f;
        public float doorwayAssistSampleRadius = 1.0f;
        public bool doorwayWarpAssist = false;
        public float doorwayWarpStep = 1.25f;
        public int doorwayWarpChecks = 4;
        public bool requireClearPathForWarp = true;
        public bool autoOpenNearbyDoors = false;
        public float autoDoorOpenRange = 2.2f;
        public float autoDoorOpenCooldown = 0.25f;

        [Header("Hide Spot Takedown")]
        public bool killPlayerIfFoundHidden = true;
        public float hiddenTakedownDistance = 1.4f;
        public float hiddenTakedownConfirmSeconds = 0.45f;

        [Header("Motion Animation")]
        public Transform visualRoot;
        public bool autoFindVisualRoot = true;
        public float walkBobAmplitude = 0.03f;
        public float runBobAmplitude = 0.06f;
        public float walkBobSpeed = 3.1f;
        public float runBobSpeed = 6.2f;
        public float walkLimbSwingDegrees = 12f;
        public float runLimbSwingDegrees = 24f;
        public float chaseForwardLeanDegrees = 8f;

        public State state = State.Patrol;
        private Coroutine routine;

        // ===== NEW (one-shot kill) =====
        private PlayerDeath playerDeath;
        private PlayerHide playerHide;
        private RohitFPSController rohitFPS;
        private bool killTriggered;
        private Vector3 roamCenter;
        private float forcedNextSearchDuration = -1f;
        private bool wasPlayerHiddenLastFrame;
        private float chaseUntilTime = -1f;
        private float ignoreSightUntilTime = -1f;
        private float farChaseTimer = 0f;
        private static readonly HashSet<int> killedRohitInstanceIds = new();
        private Vector3 lastSeenPlayerPos;
        private float lastSeenPlayerTime = -999f;
        private HideableObject[] cachedHideables;
        private bool forceFakeLeaveOnce;
        private float nextNavStabilizeAt;
        private Transform armL;
        private Transform armR;
        private Transform legL;
        private Transform legR;
        private Vector3 visualBaseLocalPos;
        private Quaternion visualBaseLocalRot;
        private Quaternion armLBaseRot;
        private Quaternion armRBaseRot;
        private Quaternion legLBaseRot;
        private Quaternion legRBaseRot;
        private float motionPhase;
        private float chaseStuckTimer;
        private float nextDoorOpenTime;
        private bool hasSuspectedHideSpot;
        private Vector3 suspectedHideSpot;
        private float hiddenTakedownTimer;
        private static readonly Collider[] movementBlockerHits = new Collider[32];
        private static readonly Collider[] capsuleBlockerHits = new Collider[32];
        private readonly List<Collider> cachedHideableColliders = new();
        private Vector3 lastSafePosition;
        private bool hasSafePosition;
        private Door[] cachedDoors;
        private float ignoreAntiClipUntilTime;
        private float visibleAccum;
        private float hiddenAccum;
        private bool stablePlayerVisible;
        private float chaseLostSightTimer;
        private bool hasInvestigateTarget;
        private Vector3 investigateTargetPos;
        private Vector3 playerSpawnPosition;
        private bool hasPlayerSpawnPosition;

        void Reset() { agent = GetComponent<NavMeshAgent>(); }

        void OnEnable() => NoiseSystem.OnNoise += OnNoise;
        void OnDisable() => NoiseSystem.OnNoise -= OnNoise;

        void Start()
        {
            if (agent == null) agent = GetComponent<NavMeshAgent>();
            if (agent == null)
            {
                Debug.LogError("[StalkerAI] Missing NavMeshAgent.");
                enabled = false;
                return;
            }

            // Forced tuning (requested): keep doorway/room entry stable.
            minWallClearance = 0.35f;
            destinationRetrySamples = 16;
            destinationRetryRadius = 4.0f;
            enforcedAgentRadius = 0.30f;
            enforcedAgentHeight = 2.0f;
            enableDoorwayAssist = true;
            doorwayWarpAssist = true;
            doorwayStuckSeconds = 0.25f;
            doorwayAssistRange = Mathf.Max(doorwayAssistRange, 20f);
            doorwayAssistSampleRadius = Mathf.Max(doorwayAssistSampleRadius, 1.25f);
            requireClearPathForWarp = false;
            // Aggressive chase defaults (requested).
            omnidirectionalVision = true;
            sightRange = Mathf.Max(sightRange, 32f);
            requireNoiseToChase = false;
            maintainChaseUntilHidden = true;
            limitVisualChaseTime = false;
            chaseMemorySeconds = Mathf.Max(chaseMemorySeconds, 6f);
            lostSightPursuitSeconds = Mathf.Max(lostSightPursuitSeconds, 8f);
            keepChaseWhenRecentlySeen = true;
            loseChaseWhenFar = false;
            // Slight speed nerf so chase is still threatening but fair.
            chaseMoveSpeed = Mathf.Min(chaseMoveSpeed, 5.2f);
            patrolMoveSpeed = Mathf.Min(patrolMoveSpeed, 3.0f);
            chaseAcceleration = Mathf.Min(chaseAcceleration, 11f);
            // Keep initial spawn noticeably far from player start.
            minSpawnDistanceFromPlayer = Mathf.Max(minSpawnDistanceFromPlayer, 20f);
            minSpawnDistanceFromPlayerSpawn = Mathf.Max(minSpawnDistanceFromPlayerSpawn, 26f);
            spawnSampleAttempts = Mathf.Max(spawnSampleAttempts, 96);
            // Disable restrictive custom geometry gates that can deadlock at narrow door edges.
            validatePathAgainstGeometry = false;
            rejectDestinationsInsideBlockingGeometry = false;
            enforceRuntimeAntiClip = false;

            // Avoid floating weirdness
            agent.baseOffset = 0f;
            CacheHideables();
            SetupNavigationCollisionFixes();
            SetupMotionRig();
            roamCenter = ComputeRoamCenter();
            ResolvePlayerReference();
            if (player != null)
            {
                playerSpawnPosition = player.position;
                hasPlayerSpawnPosition = true;
            }

            // Hard-apply runtime movement envelope after setup.
            if (agent != null)
            {
                agent.radius = enforcedAgentRadius;
                agent.height = enforcedAgentHeight;
            }
            var cc = GetComponent<CapsuleCollider>();
            if (cc != null)
            {
                cc.radius = enforcedAgentRadius;
                cc.height = enforcedAgentHeight;
                var c = cc.center;
                c.y = enforcedAgentHeight * 0.5f;
                cc.center = c;
            }

            agent.speed = patrolMoveSpeed;

            if (deferSpawnUntilDoorsReady)
                StartCoroutine(DelayedSpawnRandomization());
            else
                TryRandomizeSpawn();
            wasPlayerHiddenLastFrame = IsPlayerHidden();
            if (player != null)
            {
                lastSeenPlayerPos = player.position;
                lastSeenPlayerTime = Time.time;
            }

            ChangeState(State.Patrol);
            lastSafePosition = transform.position;
            hasSafePosition = true;
        }

        void Update()
        {
            if (player == null)
            {
                ResolvePlayerReference();
                if (player == null) return;
            }

            StabilizeAgentOnNavMesh();

            // If player is dead/disabled, stop AI cleanly
            if (!player.gameObject.activeInHierarchy)
            {
                if (IsAgentReady()) agent.isStopped = true;
                return;
            }

            bool isHiddenNow = IsPlayerHidden();

            // If player hides during chase, immediately lose them and switch to search.
            if (state == State.Chase && isHiddenNow)
            {
                MarkSuspectedHideSpot();
                lastNoisePos = player.position;
                lastNoiseIntensity = Mathf.Max(lastNoiseIntensity, 6f);
                hasNoise = true;
                if (!killPlayerIfFoundHidden && forceFakeLeaveAfterPlayerHides)
                {
                    forceFakeLeaveOnce = true;
                    forcedNextSearchDuration = Mathf.Max(forcedNextSearchDuration, hideTriggeredSearchDuration + lastSeenSearchDurationBoost);
                }
                else
                {
                    forceFakeLeaveOnce = false;
                    forcedNextSearchDuration = Mathf.Max(forcedNextSearchDuration, hideTriggeredSearchDuration + lastSeenSearchDurationBoost);
                }
                ChangeState(State.Search);
                wasPlayerHiddenLastFrame = isHiddenNow;
                return;
            }

            bool canSee = UpdateStableVision();
            if (canSee)
            {
                lastSeenPlayerPos = player.position;
                lastSeenPlayerTime = Time.time;
                if (state == State.Chase && relentlessVisualChase)
                    StartVisualChaseWindow();
            }
            bool chaseUnlocked = !requireNoiseToChase ||
                                 (Time.time - lastHeardNoiseTime <= noiseChaseWindow) ||
                                 (allowProximityChaseWithoutNoise &&
                                  Vector3.Distance(transform.position, player.position) <= proximityChaseDistance);

            bool justExitedHideInSight = wasPlayerHiddenLastFrame && !isHiddenNow && canSee;
            bool visualChaseAllowed = canSee &&
                                     Time.time >= ignoreSightUntilTime &&
                                     (alwaysChaseWhenVisible || sightAlwaysStartsChase || chaseUnlocked);
            if (state != State.Chase && (justExitedHideInSight || visualChaseAllowed))
            {
                StartVisualChaseWindow();
                ChangeState(State.Chase);
            }

            // One-shot kill only when chasing, close, and with clear line of sight.
            // This prevents unfair kills through walls/closed geometry.
            if (state == State.Chase && !killTriggered &&
                !isHiddenNow &&
                stablePlayerVisible &&
                Vector3.Distance(transform.position, player.position) <= killDistance)
            {
                TryKillTarget(player.gameObject, "Stalker one-shot");
            }

            wasPlayerHiddenLastFrame = isHiddenNow;
            UpdateMotionAnimation();
            TryAutoOpenDoorInFront();
        }

        void LateUpdate()
        {
            EnforceRuntimeNoClip();
        }

        void ChangeState(State newState)
        {
            state = newState;
            if (routine != null) StopCoroutine(routine);
            if (IsAgentReady())
            {
                if (newState == State.Chase)
                {
                    agent.speed = Mathf.Max(0.1f, chaseMoveSpeed);
                    agent.acceleration = Mathf.Max(agent.acceleration, chaseAcceleration);
                }
                else
                {
                    agent.speed = Mathf.Max(0.1f, patrolMoveSpeed);
                }
            }
            if (newState != State.Chase) chaseUntilTime = -1f;
            if (newState != State.Chase) farChaseTimer = 0f;
            if (newState != State.Chase)
            {
                chaseStuckTimer = 0f;
                chaseLostSightTimer = 0f;
            }
            if (newState != State.Search) hiddenTakedownTimer = 0f;

            routine = newState switch
            {
                State.Patrol => StartCoroutine(Patrol()),
                State.Investigate => StartCoroutine(Investigate()),
                State.Search => StartCoroutine(Search()),
                State.Chase => StartCoroutine(Chase()),
                _ => routine
            };
        }

        void OnNoise(Vector3 pos, float intensity, string type)
        {
            // If already killed player, ignore noise
            if (killTriggered) return;

            if (intensity < minNoiseIntensity) return;

            bool distractionNoise = IsDistractionNoise(type);
            bool forceChaseNoise = globalNoiseForcesChase && IsGlobalPlayerNoise(type);

            float dist = Vector3.Distance(transform.position, pos);
            float hearingRadius = Mathf.Max(minHearingRadius, intensity * hearingScale);
            if (distractionNoise)
            {
                hearingRadius = Mathf.Max(minThrowHearingRadius, hearingRadius * throwNoiseHearingBoost);
            }
            if (!forceChaseNoise && dist > hearingRadius) return;

            Vector3 targetPos = pos;
            float sampleRadius = distractionNoise ? 10f : 3f;
            if (NavMesh.SamplePosition(pos, out var navHit, sampleRadius, NavMesh.AllAreas))
                targetPos = navHit.position;

            lastNoisePos = targetPos;
            lastNoiseIntensity = intensity;
            hasNoise = true;
            lastHeardNoiseTime = Time.time;

            // During chase, thrown noises can pull the stalker off the player briefly.
            if (distractionNoise)
            {
                if (distractOnThrowNoise)
                {
                    // Do not break active eye-contact chase for distraction noises.
                    if (state == State.Chase && CanSeePlayer()) return;

                    ignoreSightUntilTime = Time.time + Mathf.Max(0f, distractionSightSuppressSeconds);

                    if (state == State.Chase)
                        forcedNextSearchDuration = throwDistractionSearchDuration;

                    ChangeState(State.Investigate); // always go investigate thrown noise source
                }
                return;
            }

            // Non-throw noises can force chase even without LOS/range (for manual noise key).
            lastSeenPlayerPos = targetPos;
            lastSeenPlayerTime = Time.time;
            ChangeState(State.Chase);
        }

        void ResolvePlayerReference()
        {
            if (player == null)
            {
                var rohit = FindFirstObjectByType<RohitFPSController>();
                if (rohit != null) player = rohit.transform;
            }

            if (player == null)
            {
                var fps = FindFirstObjectByType<Sushil.Demo.SushilFPSController>();
                if (fps != null) player = fps.transform;
            }

            if (player == null)
            {
                var death = FindFirstObjectByType<PlayerDeath>();
                if (death != null) player = death.transform;
            }

            if (player == null)
            {
                var tagged = GameObject.FindGameObjectWithTag("Player");
                if (tagged != null) player = tagged.transform;
            }

            if (player != null)
            {
                playerDeath = player.GetComponent<PlayerDeath>();
                playerHide = player.GetComponent<PlayerHide>();
                rohitFPS = player.GetComponent<RohitFPSController>();
            }
        }

        public bool TryKillTarget(GameObject target, string reason)
        {
            if (target == null || killTriggered) return false;

            var death = target.GetComponentInParent<PlayerDeath>();
            if (death != null && !death.isDead)
            {
                killTriggered = true;
                death.Kill(reason);
                return true;
            }

            var rohit = target.GetComponentInParent<RohitFPSController>();
            if (rohit != null)
            {
                killTriggered = true;
                KillRohitController(rohit, reason);
                return true;
            }

            return false;
        }

        public static void KillRohitController(RohitFPSController rohit, string reason)
        {
            if (rohit == null) return;
            int rohitId = rohit.GetInstanceID();
            if (killedRohitInstanceIds.Contains(rohitId)) return;
            if (!rohit.enabled) return;

            Debug.Log($"[StalkerAI] {reason} (Rohit fallback)");
            killedRohitInstanceIds.Add(rohitId);

            rohit.enabled = false;
            rohit.CancelInvoke();

            var throwRock = rohit.GetComponent<ThrowRock>();
            if (throwRock != null) throwRock.enabled = false;

            var cc = rohit.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            Sushil.Systems.GameOverOverlay.Show(reason);
        }

        IEnumerator Patrol()
        {
            while (state == State.Patrol)
            {
                if (!IsAgentReady()) { yield return null; continue; }

                if (patrolAroundPlayer && player != null)
                    roamCenter = player.position;

                Vector3 targetPos;
                if (useFreeRoamPatrol || patrolPoints.Count == 0)
                {
                    bool useNoiseRoom = ShouldBiasToLastNoiseRoom();
                    if (useNoiseRoom &&
                        TryGetRandomRoamPoint(lastNoisePos, lastNoiseRoomRadius, out targetPos))
                    {
                        // Intentionally patrol near last heard player noise.
                    }
                    else if (patrolPoints.Count > 0 && Random.value < patrolPointVisitChance)
                    {
                        Transform target = patrolPoints[Random.Range(0, patrolPoints.Count)];
                        targetPos = target.position;
                    }
                    else
                    {
                        Vector3 center = roamCenter;
                        float radius = freeRoamRadius;

                        if (!roamWholeHouse)
                        {
                            radius = patrolAroundPlayer ? freeRoamRadius : Mathf.Min(freeRoamRadius, localPatrolRadius);
                            center = (!patrolAroundPlayer && Random.value >= globalPatrolChance)
                                ? transform.position
                                : roamCenter;
                        }

                        if (!TryGetRandomRoamPoint(center, radius, out targetPos) &&
                            !TryGetRandomRoamPoint(roamCenter, freeRoamRadius, out targetPos))
                        {
                            yield return null;
                            continue;
                        }
                    }
                }
                else
                {
                    Transform target = patrolPoints[Random.Range(0, patrolPoints.Count)];
                    targetPos = target.position;
                }

                agent.isStopped = false;
                if (!TrySetDestination(targetPos)) { yield return null; continue; }

                while (state == State.Patrol && IsAgentReady() && agent.pathPending) yield return null;
                while (state == State.Patrol && IsAgentReady() &&
                       !HasReachedDestination(0.25f))
                    yield return null;

                yield return new WaitForSeconds(Random.Range(0.6f, 1.6f));
            }
        }

        IEnumerator Investigate()
        {
            if (!hasNoise && !hasInvestigateTarget) { ChangeState(State.Patrol); yield break; }
            if (!IsAgentReady()) { ChangeState(State.Patrol); yield break; }

            Vector3 target = hasInvestigateTarget ? investigateTargetPos : lastNoisePos;
            agent.isStopped = false;
            if (!TrySetDestination(target)) { ChangeState(State.Patrol); yield break; }

            while (state == State.Investigate && IsAgentReady() && agent.pathPending) yield return null;
            while (state == State.Investigate && IsAgentReady() &&
                   !HasReachedDestination(0.35f))
            {
                if (stablePlayerVisible)
                {
                    ChangeState(State.Chase);
                    yield break;
                }
                yield return null;
            }

            if (state != State.Investigate) yield break;
            if (stablePlayerVisible)
            {
                ChangeState(State.Chase);
                yield break;
            }
            hasInvestigateTarget = false;
            ChangeState(State.Search);
        }

        IEnumerator Search()
        {
            float elapsed = 0f;
            Vector3 center = hasInvestigateTarget ? investigateTargetPos : lastNoisePos;
            float activeSearchDuration = forcedNextSearchDuration > 0f ? forcedNextSearchDuration : searchDuration;
            forcedNextSearchDuration = -1f;
            bool shouldFakeLeave = forceFakeLeaveOnce || Random.value < fakeLeaveChance;
            if (hasSuspectedHideSpot) shouldFakeLeave = false;
            forceFakeLeaveOnce = false;

            if (hasSuspectedHideSpot && IsAgentReady())
            {
                agent.isStopped = false;
                TrySetDestination(suspectedHideSpot);

                float t0 = 0f;
                while (state == State.Search && t0 < 3f && IsAgentReady() && !HasReachedDestination(0.35f))
                {
                    t0 += Time.deltaTime;
                    TryHandleHiddenTakedown();
                    yield return null;
                }

                // Still check right after reaching/sampling the suspected hide spot.
                TryHandleHiddenTakedown(force: true);
            }

            // Fake leave: step away then return
            if (shouldFakeLeave)
            {
                if (TryGetRandomRoamPoint(center, searchRadius + 3f, out var away))
                {
                    if (IsAgentReady())
                    {
                        agent.isStopped = false;
                        TrySetDestination(away);
                    }
                    yield return new WaitForSeconds(1.0f);
                }

                TrySetDestination(center);
                yield return new WaitForSeconds(0.7f);
            }

            while (state == State.Search && elapsed < activeSearchDuration)
            {
                // Stand still and "listen"
                if (Random.value < pauseOutsideChance)
                {
                    if (IsAgentReady()) agent.isStopped = true;
                    yield return new WaitForSeconds(Random.Range(pauseMin, pauseMax));
                    if (IsAgentReady()) agent.isStopped = false;
                }

                TryHandleHiddenTakedown();

                // Sometimes go check a hiding inspect point
                if (hidingInspectPoints.Count > 0 && Random.value < checkHidingChance)
                {
                    if (TryGetRandomInspectPoint(out var inspectPoint) && IsAgentReady())
                    {
                        agent.isStopped = false;
                        TrySetDestination(inspectPoint.position);
                    }

                    // Wait up to ~2 seconds or until close enough
                    float t = 0f;
                    while (state == State.Search && t < 2.0f && IsAgentReady() &&
                           !HasReachedDestination(0.35f))
                    {
                        t += Time.deltaTime;
                        TryHandleHiddenTakedown();
                        yield return null;
                    }

                    // Pause like inspecting
                    if (IsAgentReady()) agent.isStopped = true;
                    yield return new WaitForSeconds(Random.Range(inspectPauseMin, inspectPauseMax));
                    if (IsAgentReady()) agent.isStopped = false;

                    elapsed += 1f;
                    continue;
                }

                // Normal wandering around the noise center
                float localPhase = activeSearchDuration * Mathf.Clamp01(roomSuspicionPortion);
                float radius = elapsed < localPhase ? searchRadius : (searchRadius * outwardSearchMultiplier);
                if (TryGetRandomRoamPoint(center, radius, out var roamPoint))
                    TrySetDestination(roamPoint);

                yield return new WaitForSeconds(Random.Range(0.6f, 1.2f));
                elapsed += 1f;
            }

            hasNoise = false;
            hasInvestigateTarget = false;
            hasSuspectedHideSpot = false;
            ChangeState(State.Patrol);
        }

        IEnumerator Chase()
        {
            while (state == State.Chase)
            {
                // If player is dead/disabled, stop chasing
                if (player == null || !player.gameObject.activeInHierarchy)
                {
                    if (IsAgentReady()) agent.isStopped = true;
                    yield break;
                }

                // Hiding is the only hard stop when persistent chase is enabled.
                if (IsPlayerHidden())
                {
                    lastNoisePos = player.position;
                    lastNoiseIntensity = Mathf.Max(lastNoiseIntensity, 6f);
                    hasNoise = true;
                    ChangeState(State.Search);
                    yield break;
                }

                bool canSee = stablePlayerVisible;
                bool hasFreshNoise = Time.time - lastHeardNoiseTime <= noiseChaseWindow;

                if (canSee)
                {
                    lastSeenPlayerPos = player.position;
                    lastSeenPlayerTime = Time.time;
                    chaseLostSightTimer = 0f;
                }
                else if (hasFreshNoise)
                {
                    lastSeenPlayerPos = GetNoiseTrackedPosition(lastNoisePos, lastNoiseIntensity);
                    lastSeenPlayerTime = Time.time;
                }
                else
                {
                    chaseLostSightTimer += Time.deltaTime;
                }

                if (IsAgentReady())
                {
                    agent.isStopped = false;
                    Vector3 chaseTarget = canSee
                        ? player.position
                        : lastSeenPlayerPos;
                    bool moved = TrySetDestination(chaseTarget);
                    if (!moved)
                    {
                        // Fallback for doorway edge-cases: push to nearest valid nav point near target.
                        if (NavMesh.SamplePosition(chaseTarget, out var fallbackHit, 2.5f, NavMesh.AllAreas))
                            moved = agent.SetDestination(fallbackHit.position);
                    }
                    bool trackingRecentSeen = (Time.time - lastSeenPlayerTime) <= Mathf.Max(0.8f, chaseMemorySeconds * 1.5f);
                    UpdateDoorwayAssist(canSee || trackingRecentSeen, moved, chaseTarget);
                }

                // If LOS is lost, keep running to last known point for memory duration first.
                float memorySeconds = Mathf.Max(
                    Mathf.Clamp(chaseMemorySeconds, 1.5f, 8f),
                    Mathf.Clamp(lostSightPursuitSeconds, 1.5f, 12f));
                if (!canSee && !hasFreshNoise && chaseLostSightTimer > memorySeconds)
                {
                    investigateTargetPos = lastSeenPlayerPos;
                    hasInvestigateTarget = true;
                    ChangeState(State.Investigate);
                    yield break;
                }

                if (loseChaseWhenFar)
                {
                    bool recentlyTracking = canSee || chaseLostSightTimer < Mathf.Clamp(lostSightPursuitSeconds, 1.5f, 12f);
                    bool allowFarDrop = !keepChaseWhenRecentlySeen || !recentlyTracking;
                    float chaseDistance = Vector3.Distance(transform.position, player.position);
                    if (allowFarDrop && chaseDistance > maxChaseDistance)
                    {
                        farChaseTimer += Time.deltaTime;
                        if (farChaseTimer >= farLoseDelay)
                        {
                            lastNoisePos = lastSeenPlayerPos;
                            lastNoiseIntensity = Mathf.Max(lastNoiseIntensity, 5f);
                            hasNoise = true;
                            ChangeState(State.Search);
                            yield break;
                        }
                    }
                    else
                    {
                        farChaseTimer = 0f;
                    }
                }

                if (limitVisualChaseTime && !relentlessVisualChase && chaseUntilTime > 0f && Time.time >= chaseUntilTime)
                {
                    // Do not hard-drop chase timer while still seeing/recently tracking target.
                    if (!canSee && chaseLostSightTimer > 1.0f)
                    {
                        lastNoisePos = lastSeenPlayerPos;
                        lastNoiseIntensity = Mathf.Max(lastNoiseIntensity, 5f);
                        hasNoise = true;
                        ChangeState(State.Search);
                        yield break;
                    }
                }

                if (!maintainChaseUntilHidden && !canSee)
                {
                    Vector3 lastKnown = lastSeenPlayerPos;
                    yield return new WaitForSeconds(1.1f);

                    if (!stablePlayerVisible)
                    {
                        investigateTargetPos = lastKnown;
                        hasInvestigateTarget = true;
                        ChangeState(State.Investigate);
                        yield break;
                    }
                }

                yield return null;
            }
        }

        void UpdateDoorwayAssist(bool hasChaseTrackNow, bool setDestinationOk, Vector3 assistTarget)
        {
            if (!enableDoorwayAssist || !hasChaseTrackNow || player == null || !IsAgentReady())
            {
                chaseStuckTimer = 0f;
                return;
            }

            float distToTarget = Vector3.Distance(transform.position, assistTarget);
            if (distToTarget > doorwayAssistRange || distToTarget <= killDistance)
            {
                chaseStuckTimer = 0f;
                return;
            }

            // Consider "stuck" when we repeatedly fail to set path or barely move while chasing.
            bool verySlow = agent.velocity.sqrMagnitude < 0.02f * 0.02f;
            bool stuckNow = !setDestinationOk || (!agent.pathPending && verySlow);
            if (!stuckNow)
            {
                chaseStuckTimer = 0f;
                return;
            }

            chaseStuckTimer += Time.deltaTime;
            if (chaseStuckTimer < doorwayStuckSeconds) return;
            chaseStuckTimer = 0f;

            bool assisted = false;
            bool nearOpenDoor = TryGetNearbyOpenDoor(out var openDoor, 4.5f);
            if (nearOpenDoor)
            {
                // First try a normal non-warp pass-through destination near the open doorway.
                assisted = TryAdvanceThroughOpenDoor(openDoor, assistTarget);
                if (!assisted)
                    assisted = TryWarpThroughOpenDoor(openDoor, assistTarget);
            }
            if (!assisted)
            {
                assisted = TryDoorwayAssistAdvance(assistTarget);
                if (!assisted && doorwayWarpAssist)
                    TryDoorwayWarpAdvance(assistTarget);
            }
        }

        bool TryDoorwayAssistAdvance(Vector3 targetPos)
        {
            // Sample multiple points between stalker and player; move to first reachable.
            Vector3 from = transform.position;
            Vector3 to = targetPos;
            const int steps = 7;

            for (int i = 1; i <= steps; i++)
            {
                float t = i / (float)steps;
                Vector3 mid = Vector3.Lerp(from, to, t);
                mid.y = from.y;

                if (!NavMesh.SamplePosition(mid, out var hit, doorwayAssistSampleRadius, NavMesh.AllAreas))
                    continue;

                if (!ResolveReachableDestination(hit.position, out var resolved))
                    continue;

                if (Vector3.Distance(transform.position, resolved) < 0.2f)
                    continue;

                agent.SetDestination(resolved);
                return true;
            }

            // Last fallback: sample near player and push there if it is reachable.
            if (NavMesh.SamplePosition(targetPos, out var nearPlayer, doorwayAssistSampleRadius * 2f, NavMesh.AllAreas) &&
                ResolveReachableDestination(nearPlayer.position, out var finalResolved))
            {
                agent.SetDestination(finalResolved);
                return true;
            }

            return false;
        }

        void TryDoorwayWarpAdvance(Vector3 targetPos)
        {
            if (!IsAgentReady()) return;

            Vector3 from = transform.position;
            Vector3 dir = (targetPos - from);
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.01f) return;
            dir.Normalize();

            int checks = Mathf.Max(1, doorwayWarpChecks);
            float step = Mathf.Max(0.25f, doorwayWarpStep);

            for (int i = 1; i <= checks; i++)
            {
                Vector3 probe = from + dir * (step * i);
                if (!NavMesh.SamplePosition(probe, out var hit, doorwayAssistSampleRadius, NavMesh.AllAreas))
                    continue;
                if (Vector3.Distance(from, hit.position) < 0.45f)
                    continue;

                // Only warp if probe remains in sensible chase range.
                if (Vector3.Distance(hit.position, targetPos) > Mathf.Max(2f, doorwayAssistRange + 2f))
                    continue;

                if (requireClearPathForWarp)
                {
                    Vector3 a = from + Vector3.up * 1.0f;
                    Vector3 b = hit.position + Vector3.up * 1.0f;
                    if (Physics.Linecast(a, b, out RaycastHit blockHit, ~0, QueryTriggerInteraction.Ignore))
                    {
                        Transform ht = blockHit.collider != null ? blockHit.collider.transform : null;
                        if (ht != null)
                        {
                            if (!(ht == transform || ht.IsChildOf(transform)))
                                continue; // blocked by wall/door/props, do not warp through.
                        }
                    }
                }

                agent.Warp(hit.position);
                transform.position = hit.position;
                ignoreAntiClipUntilTime = Time.time + 0.35f;
                return;
            }
        }

        bool TryGetNearbyOpenDoor(out Door door, float range)
        {
            door = null;
            if (cachedDoors == null || cachedDoors.Length == 0)
                cachedDoors = FindObjectsByType<Door>(FindObjectsSortMode.None);
            if (cachedDoors == null || cachedDoors.Length == 0)
                return false;

            float bestSqr = float.MaxValue;
            float rangeSqr = Mathf.Max(0.6f, range) * Mathf.Max(0.6f, range);
            Vector3 p = transform.position;
            p.y = 0f;

            for (int i = 0; i < cachedDoors.Length; i++)
            {
                Door d = cachedDoors[i];
                if (!d.gameObject.activeInHierarchy) continue;
                if (!d.IsOpen) continue;

                Vector3 dpos = d.transform.position;
                dpos.y = 0f;
                float sqr = (dpos - p).sqrMagnitude;
                if (sqr > rangeSqr) continue;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    door = d;
                }
            }

            return door != null;
        }

        bool TryWarpThroughOpenDoor(Door door, Vector3 targetPos)
        {
            if (door == null || !IsAgentReady()) return false;
            Vector3 center = door.transform.position;
            float[] radii = { 0.9f, 1.3f, 1.7f };
            float bestScore = float.MaxValue;
            bool found = false;
            Vector3 best = center;

            var path = new NavMeshPath();
            for (int r = 0; r < radii.Length; r++)
            {
                float rad = radii[r];
                for (int i = 0; i < 12; i++)
                {
                    float ang = (Mathf.PI * 2f * i) / 12f;
                    Vector3 candidate = center + new Vector3(Mathf.Cos(ang) * rad, 0f, Mathf.Sin(ang) * rad);
                    if (!NavMesh.SamplePosition(candidate, out var hit, 0.8f, NavMesh.AllAreas))
                        continue;
                    if (!IsDestinationAllowed(hit.position))
                        continue;
                    if (!NavMesh.CalculatePath(transform.position, hit.position, NavMesh.AllAreas, path) ||
                        path.status != NavMeshPathStatus.PathComplete)
                        continue;

                    float score = Vector3.SqrMagnitude(hit.position - targetPos);
                    if (score < bestScore)
                    {
                        bestScore = score;
                        best = hit.position;
                        found = true;
                    }
                }
            }

            if (!found) return false;

            agent.Warp(best);
            transform.position = best;
            agent.nextPosition = best;
            agent.ResetPath();
            ignoreAntiClipUntilTime = Time.time + 0.35f;
            return agent.SetDestination(targetPos);
        }

        bool TryAdvanceThroughOpenDoor(Door door, Vector3 targetPos)
        {
            if (door == null || !IsAgentReady()) return false;

            Vector3 from = transform.position;
            Vector3 toTarget = targetPos - from;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude < 0.01f) return false;
            toTarget.Normalize();

            // Build points around door center and prefer points on the "inside" toward target.
            Vector3 center = door.transform.position;
            Vector3 forward = door.transform.forward;
            Vector3 right = door.transform.right;
            float[] depths = { 0.4f, 0.9f, 1.3f };
            float[] widths = { 0f, 0.45f, -0.45f, 0.8f, -0.8f };
            var path = new NavMeshPath();

            float bestScore = float.MaxValue;
            bool found = false;
            Vector3 best = center;

            for (int d = 0; d < depths.Length; d++)
            {
                for (int w = 0; w < widths.Length; w++)
                {
                    // Try both sides of the door plane so we can pass through whichever side is reachable.
                    for (int side = -1; side <= 1; side += 2)
                    {
                        Vector3 probe = center + forward * (depths[d] * side) + right * widths[w];
                        if (!NavMesh.SamplePosition(probe, out var hit, 0.9f, NavMesh.AllAreas))
                            continue;
                        if (!NavMesh.CalculatePath(from, hit.position, NavMesh.AllAreas, path) ||
                            path.status != NavMeshPathStatus.PathComplete)
                            continue;

                        // Prefer points that move us through the door and toward player target.
                        Vector3 dir = (hit.position - from);
                        dir.y = 0f;
                        if (dir.sqrMagnitude < 0.02f) continue;
                        float heading = 1f - Mathf.Clamp01(Vector3.Dot(dir.normalized, toTarget));
                        float goalDist = Vector3.SqrMagnitude(hit.position - targetPos);
                        float score = heading * 12f + goalDist;

                        if (score < bestScore)
                        {
                            bestScore = score;
                            best = hit.position;
                            found = true;
                        }
                    }
                }
            }

            if (!found) return false;
            return agent.SetDestination(best);
        }

        void TryAutoOpenDoorInFront()
        {
            if (!autoOpenNearbyDoors || Time.time < nextDoorOpenTime || player == null) return;
            if (state != State.Chase && state != State.Search && state != State.Investigate) return;

            Vector3 origin = transform.position + Vector3.up * 1.0f;
            Vector3 dirToPlayer = (player.position - transform.position);
            dirToPlayer.y = 0f;
            Vector3 forward = dirToPlayer.sqrMagnitude > 0.01f ? dirToPlayer.normalized : transform.forward;

            if (!Physics.Raycast(origin, forward, out var hit, Mathf.Max(0.5f, autoDoorOpenRange), ~0, QueryTriggerInteraction.Ignore))
                return;

            Door door = hit.collider != null ? hit.collider.GetComponentInParent<Door>() : null;
            if (door == null) return;

            bool opened = door.TryOpenForAI();
            if (opened)
                nextDoorOpenTime = Time.time + Mathf.Max(0.05f, autoDoorOpenCooldown);
        }

        void MarkSuspectedHideSpot()
        {
            if (!GetCurrentHideSpotPosition(out Vector3 hidePos))
                hidePos = player != null ? player.position : transform.position;

            if (NavMesh.SamplePosition(hidePos, out var hit, 2.5f, NavMesh.AllAreas))
                hidePos = hit.position;

            suspectedHideSpot = hidePos;
            hasSuspectedHideSpot = true;
        }

        bool GetCurrentHideSpotPosition(out Vector3 pos)
        {
            pos = Vector3.zero;
            if (rohitFPS != null && rohitFPS.isHidden && rohitFPS.currentHideObject != null)
            {
                pos = rohitFPS.currentHideObject.transform.position;
                return true;
            }
            if (playerHide != null && playerHide.IsHidden && player != null)
            {
                pos = player.position;
                return true;
            }
            return false;
        }

        void TryHandleHiddenTakedown(bool force = false)
        {
            if (!killPlayerIfFoundHidden || player == null) return;
            if (!IsPlayerHidden()) { hiddenTakedownTimer = 0f; return; }
            if (!GetCurrentHideSpotPosition(out Vector3 hidePos)) { hiddenTakedownTimer = 0f; return; }

            float d = Vector3.Distance(transform.position, hidePos);
            if (d > Mathf.Max(0.6f, hiddenTakedownDistance))
            {
                hiddenTakedownTimer = 0f;
                return;
            }

            hiddenTakedownTimer += force ? hiddenTakedownConfirmSeconds : Time.deltaTime;
            if (hiddenTakedownTimer < hiddenTakedownConfirmSeconds) return;
            hiddenTakedownTimer = 0f;

            // We "open/check" the hide area and kill if player is still hiding inside.
            TryKillTarget(player.gameObject, "Watcher found you hiding");
        }

        void StartVisualChaseWindow()
        {
            if (!limitVisualChaseTime) { chaseUntilTime = -1f; return; }
            chaseUntilTime = Time.time + Mathf.Max(0.5f, maxVisualChaseSeconds);
        }

        bool UpdateStableVision()
        {
            bool rawVisible = CanSeePlayer();
            if (rawVisible)
            {
                visibleAccum += Time.deltaTime;
                hiddenAccum = 0f;
                if (!stablePlayerVisible && visibleAccum >= Mathf.Max(0f, visionAcquireSeconds))
                    stablePlayerVisible = true;
            }
            else
            {
                hiddenAccum += Time.deltaTime;
                visibleAccum = 0f;
                if (stablePlayerVisible && hiddenAccum >= Mathf.Max(0f, visionLoseSeconds))
                    stablePlayerVisible = false;
            }

            return stablePlayerVisible;
        }

        bool IsDistractionNoise(string noiseType)
        {
            string incoming = NormalizeNoiseType(noiseType);
            if (incoming.Contains("throw") || incoming.Contains("rock"))
                return true;

            return incoming == NormalizeNoiseType(throwReleaseNoiseType) ||
                   incoming == NormalizeNoiseType(throwImpactNoiseType);
        }

        string NormalizeNoiseType(string noiseType)
        {
            return string.IsNullOrWhiteSpace(noiseType)
                ? string.Empty
                : noiseType.Trim().ToLowerInvariant();
        }

        bool IsGlobalPlayerNoise(string noiseType)
        {
            string t = NormalizeNoiseType(noiseType);
            if (string.IsNullOrEmpty(t)) return false;
            // "noise"/debug key events should globally alert and force chase.
            return t == "noise" || t == "debugnoise" || t.Contains("manualnoise") || t.Contains("playernoise");
        }

        Vector3 GetNoiseTrackedPosition(Vector3 sourcePos, float intensity)
        {
            if (!noiseTrackingHasUncertainty) return sourcePos;

            float errorMin = Mathf.Max(0f, noiseTrackingMinError);
            float errorMax = Mathf.Max(errorMin, noiseTrackingMaxError);
            float hearingFactor = Mathf.InverseLerp(minNoiseIntensity, Mathf.Max(minNoiseIntensity + 0.01f, minNoiseIntensity + 12f), intensity);
            float jitter = Mathf.Lerp(errorMax, errorMin, hearingFactor);
            Vector2 rnd = Random.insideUnitCircle * jitter;
            Vector3 noisy = sourcePos + new Vector3(rnd.x, 0f, rnd.y);

            if (NavMesh.SamplePosition(noisy, out var hit, 2.5f, NavMesh.AllAreas))
                return hit.position;
            return sourcePos;
        }

        bool TryGetRandomInspectPoint(out Transform point)
        {
            point = null;
            if (hidingInspectPoints == null || hidingInspectPoints.Count == 0) return false;

            List<Transform> valid = new List<Transform>();
            for (int i = 0; i < hidingInspectPoints.Count; i++)
            {
                if (hidingInspectPoints[i] != null) valid.Add(hidingInspectPoints[i]);
            }
            if (valid.Count == 0) return false;

            point = valid[Random.Range(0, valid.Count)];
            return point != null;
        }

        bool TryGetRandomRoamPoint(Vector3 center, float radius, out Vector3 point)
        {
            point = center;
            float tryRadius = Mathf.Max(radius, 2f);

            for (int i = 0; i < Mathf.Max(1, roamSampleAttempts); i++)
            {
                Vector3 candidate = center + Random.insideUnitSphere * tryRadius;
                candidate.y = center.y;

                if (!NavMesh.SamplePosition(candidate, out var navHit, 6f, NavMesh.AllAreas))
                    continue;

                if (minWallClearance > 0f && NavMesh.FindClosestEdge(navHit.position, out var edgeHit, NavMesh.AllAreas))
                {
                    if (edgeHit.distance < minWallClearance) continue;
                }

                point = navHit.position;
                return true;
            }

            return false;
        }

        Vector3 ComputeRoamCenter()
        {
            if (patrolPoints == null || patrolPoints.Count == 0)
                return transform.position;

            Vector3 sum = Vector3.zero;
            int valid = 0;
            for (int i = 0; i < patrolPoints.Count; i++)
            {
                if (patrolPoints[i] == null) continue;
                sum += patrolPoints[i].position;
                valid++;
            }

            if (valid == 0) return transform.position;
            return sum / valid;
        }

        void TryRandomizeSpawn()
        {
            if (!randomizeSpawnOnStart) return;
            if (agent == null || !agent.enabled || !agent.gameObject.activeInHierarchy) return;

            if (!agent.isOnNavMesh)
            {
                if (!NavMesh.SamplePosition(transform.position, out var startHit, 8f, NavMesh.AllAreas))
                    return;
                transform.position = startHit.position;
            }

            KeyItem[] keys = FindObjectsByType<KeyItem>(FindObjectsSortMode.None);
            Vector3 center = roamCenter;
            if (center == Vector3.zero) center = transform.position;

            Vector3 best = transform.position;
            bool found = false;
            float bestScore = -1f;
            Vector3 flatPlayerSpawn = hasPlayerSpawnPosition ? playerSpawnPosition : Vector3.zero;
            if (hasPlayerSpawnPosition) flatPlayerSpawn.y = transform.position.y;

            for (int i = 0; i < Mathf.Max(8, spawnSampleAttempts); i++)
            {
                Vector3 candidate = center + Random.insideUnitSphere * Mathf.Max(8f, spawnSearchRadius);
                candidate.y = center.y;
                if (!NavMesh.SamplePosition(candidate, out var hit, 8f, NavMesh.AllAreas))
                    continue;

                Vector3 pos = hit.position;
                if (!IsSpawnPositionValid(pos, keys, cachedHideables)) continue;

                float score = 0f;
                if (player != null)
                {
                    Vector3 fp = player.position;
                    fp.y = pos.y;
                    score += Vector3.Distance(pos, fp);
                }
                if (enforceSpawnAwayFromPlayerSpawn && hasPlayerSpawnPosition)
                {
                    Vector3 fs = flatPlayerSpawn;
                    fs.y = pos.y;
                    score += Vector3.Distance(pos, fs) * 1.2f;
                }

                if (!found || score > bestScore)
                {
                    bestScore = score;
                    best = pos;
                    found = true;
                }
            }

            if (!found) return;

            bool previousUpdatePosition = agent.updatePosition;
            bool previousUpdateRotation = agent.updateRotation;
            agent.updatePosition = false;
            agent.updateRotation = false;
            agent.Warp(best);
            transform.position = best;
            agent.nextPosition = best;
            agent.updatePosition = previousUpdatePosition;
            agent.updateRotation = previousUpdateRotation;
            roamCenter = ComputeRoamCenter();
        }

        IEnumerator DelayedSpawnRandomization()
        {
            yield return null; // let all Start() methods run first
            if (spawnInitDelaySeconds > 0f)
                yield return new WaitForSeconds(spawnInitDelaySeconds);

            CacheHideables(); // refresh hideables + doors after scene init
            TryRandomizeSpawn();
        }

        bool IsSpawnPositionValid(Vector3 pos, KeyItem[] keys, HideableObject[] hideables)
        {
            if (player != null)
            {
                Vector3 flatPlayer = player.position;
                flatPlayer.y = pos.y;
                if (Vector3.Distance(pos, flatPlayer) < Mathf.Max(0.5f, minSpawnDistanceFromPlayer))
                    return false;

                if (enforceSpawnAwayFromPlayerSpawn && hasPlayerSpawnPosition)
                {
                    Vector3 flatSpawn = playerSpawnPosition;
                    flatSpawn.y = pos.y;
                    if (Vector3.Distance(pos, flatSpawn) < Mathf.Max(0.5f, minSpawnDistanceFromPlayerSpawn))
                        return false;
                }

                if (avoidPlayerForwardConeAtSpawn)
                {
                    Vector3 toSpawn = pos - flatPlayer;
                    if (toSpawn.sqrMagnitude > 0.001f)
                    {
                        Vector3 forward = player.forward;
                        forward.y = 0f;
                        if (forward.sqrMagnitude > 0.001f)
                        {
                            float angle = Vector3.Angle(forward.normalized, toSpawn.normalized);
                            if (angle <= Mathf.Clamp(playerViewExclusionHalfAngle, 0f, 180f))
                                return false;
                        }
                    }
                }
            }

            if (hideables != null && hideables.Length > 0)
            {
                float minHideDist = Mathf.Max(0.5f, minSpawnDistanceFromHidingSpots);
                for (int i = 0; i < hideables.Length; i++)
                {
                    var hide = hideables[i];
                    if (hide == null || !hide.gameObject.activeInHierarchy) continue;

                    Vector3 hidePos = hide.transform.position;
                    hidePos.y = pos.y;
                    if (Vector3.Distance(pos, hidePos) < minHideDist)
                        return false;

                    var hideCollider = hide.GetComponentInChildren<Collider>();
                    if (hideCollider != null)
                    {
                        Vector3 closest = hideCollider.ClosestPoint(pos);
                        if ((closest - pos).sqrMagnitude < 0.01f)
                            return false; // candidate is inside/very near a hiding collider
                    }
                }
            }

            if (keys != null && keys.Length > 0)
            {
                float minKeyDist = Mathf.Max(0.5f, minSpawnDistanceFromKeys);
                for (int i = 0; i < keys.Length; i++)
                {
                    if (keys[i] == null) continue;
                    Vector3 keyPos = keys[i].transform.position;
                    keyPos.y = pos.y;
                    if (Vector3.Distance(pos, keyPos) < minKeyDist)
                        return false;
                }
            }

            // Do not spawn in closed/disconnected rooms.
            // Candidate must have a complete nav path to player start.
            if (player != null)
            {
                var path = new NavMeshPath();
                if (!NavMesh.CalculatePath(pos, player.position, NavMesh.AllAreas, path) ||
                    path.status != NavMeshPathStatus.PathComplete)
                {
                    return false;
                }

                if (PathCrossesLockedClosedDoor(path))
                    return false;
            }

            if (minWallClearance > 0f && NavMesh.FindClosestEdge(pos, out var edgeHit, NavMesh.AllAreas))
            {
                if (edgeHit.distance < minWallClearance) return false;
            }

            return true;
        }

        void CacheHideables()
        {
            cachedHideables = FindObjectsByType<HideableObject>(FindObjectsSortMode.None);
            cachedDoors = FindObjectsByType<Door>(FindObjectsSortMode.None);
            cachedHideableColliders.Clear();
            if (cachedHideables == null) return;

            for (int i = 0; i < cachedHideables.Length; i++)
            {
                var hide = cachedHideables[i];
                if (hide == null) continue;
                Collider[] cols = hide.GetComponentsInChildren<Collider>();
                for (int j = 0; j < cols.Length; j++)
                {
                    Collider c = cols[j];
                    if (c == null || c.isTrigger) continue;
                    cachedHideableColliders.Add(c);
                }
            }
        }

        bool PathCrossesLockedClosedDoor(NavMeshPath path)
        {
            if (path == null || path.corners == null || path.corners.Length < 2) return false;
            if (cachedDoors == null || cachedDoors.Length == 0) return false;

            Vector3[] corners = path.corners;
            for (int i = 0; i < corners.Length - 1; i++)
            {
                Vector3 a = corners[i] + Vector3.up * 1.0f;
                Vector3 b = corners[i + 1] + Vector3.up * 1.0f;
                Vector3 dir = b - a;
                float len = dir.magnitude;
                if (len <= 0.001f) continue;
                Ray ray = new Ray(a, dir / len);

                for (int d = 0; d < cachedDoors.Length; d++)
                {
                    Door door = cachedDoors[d];
                    if (door == null || !door.gameObject.activeInHierarchy) continue;
                    if (!door.IsLocked || door.IsOpen) continue; // only reject locked closed doors

                    Collider[] cols = door.GetComponentsInChildren<Collider>(false);
                    for (int c = 0; c < cols.Length; c++)
                    {
                        Collider col = cols[c];
                        if (col == null || !col.enabled || col.isTrigger) continue;
                        if (col.Raycast(ray, out _, len))
                            return true;
                    }
                }
            }

            return false;
        }

        void SetupNavigationCollisionFixes()
        {
            if (autoSyncAgentAndColliderToScale)
                SyncAgentAndColliderToScale();

            if (enforceAgentSize && agent != null)
            {
                agent.radius = Mathf.Clamp(enforcedAgentRadius, 0.25f, 0.45f);
                agent.height = Mathf.Clamp(enforcedAgentHeight, 1.8f, 2.3f);
            }

            if (normalizeColliderWorldSize)
                NormalizeColliderSizeToAgent();

            if (!autoAddObstaclesForHideablesAndCupboards) return;

            // Ensure hideable containers are treated as blocking obstacles for nav.
            if (cachedHideables != null)
            {
                for (int i = 0; i < cachedHideables.Length; i++)
                {
                    var hide = cachedHideables[i];
                    if (hide == null) continue;
                    EnsureNavObstacleOnObject(hide.gameObject);
                }
            }

            // Additional pass for cupboards/containers based on scene naming.
            Collider[] allColliders = FindObjectsByType<Collider>(FindObjectsSortMode.None);
            for (int i = 0; i < allColliders.Length; i++)
            {
                var col = allColliders[i];
                if (col == null || col.isTrigger || !col.gameObject.activeInHierarchy) continue;

                if (col.transform == transform || col.transform.IsChildOf(transform)) continue;
                if (player != null && (col.transform == player || col.transform.IsChildOf(player))) continue;

                string n = col.gameObject.name.ToLowerInvariant();
                if (n.Contains("cupboard") ||
                    n.Contains("hidecontainer") ||
                    n.Contains("container"))
                    EnsureNavObstacleOnObject(col.gameObject);
            }
        }

        void SetupMotionRig()
        {
            if (visualRoot == null && autoFindVisualRoot)
            {
                Transform direct = transform.Find("Visual");
                if (direct != null) visualRoot = direct;
                else if (transform.childCount > 0) visualRoot = transform.GetChild(0);
            }

            if (visualRoot == null) return;

            visualBaseLocalPos = visualRoot.localPosition;
            visualBaseLocalRot = visualRoot.localRotation;

            armL = FindChildRecursive(visualRoot, "Arm_L");
            armR = FindChildRecursive(visualRoot, "Arm_R");
            legL = FindChildRecursive(visualRoot, "Leg_L");
            legR = FindChildRecursive(visualRoot, "Leg_R");

            if (armL != null) armLBaseRot = armL.localRotation;
            if (armR != null) armRBaseRot = armR.localRotation;
            if (legL != null) legLBaseRot = legL.localRotation;
            if (legR != null) legRBaseRot = legR.localRotation;
        }

        Transform FindChildRecursive(Transform root, string childName)
        {
            if (root == null) return null;
            if (root.name == childName) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindChildRecursive(root.GetChild(i), childName);
                if (found != null) return found;
            }
            return null;
        }

        void UpdateMotionAnimation()
        {
            if (visualRoot == null) return;

            float dt = Mathf.Max(Time.deltaTime, 0.0001f);
            float velocity = 0f;
            float speedRef = 1f;
            if (IsAgentReady())
            {
                velocity = agent.velocity.magnitude;
                speedRef = Mathf.Max(0.1f, agent.speed);
            }

            float move01 = Mathf.Clamp01(velocity / speedRef);
            bool chasing = state == State.Chase;
            float bobAmp = Mathf.Lerp(0.003f, chasing ? runBobAmplitude : walkBobAmplitude, move01);
            float bobSpeed = Mathf.Lerp(1.25f, chasing ? runBobSpeed : walkBobSpeed, move01);
            motionPhase += dt * bobSpeed;

            float bobY = Mathf.Sin(motionPhase * 2f) * bobAmp;
            visualRoot.localPosition = visualBaseLocalPos + new Vector3(0f, bobY, 0f);

            float lean = -Mathf.Lerp(0f, chaseForwardLeanDegrees, chasing ? move01 : move01 * 0.45f);
            visualRoot.localRotation = visualBaseLocalRot * Quaternion.Euler(lean, 0f, 0f);

            float limbSwing = Mathf.Sin(motionPhase) * move01;
            float swingDeg = (chasing ? runLimbSwingDegrees : walkLimbSwingDegrees) * limbSwing;
            float legDeg = swingDeg * 0.9f;

            if (armL != null) armL.localRotation = armLBaseRot * Quaternion.Euler(swingDeg, 0f, 0f);
            if (armR != null) armR.localRotation = armRBaseRot * Quaternion.Euler(-swingDeg, 0f, 0f);
            if (legL != null) legL.localRotation = legLBaseRot * Quaternion.Euler(-legDeg, 0f, 0f);
            if (legR != null) legR.localRotation = legRBaseRot * Quaternion.Euler(legDeg, 0f, 0f);
        }

        void SyncAgentAndColliderToScale()
        {
            float xzScale = 1f;
            float yScale = 1f;
            if (!ignoreRootScaleForNavSync)
            {
                xzScale = Mathf.Max(0.01f, Mathf.Max(transform.lossyScale.x, transform.lossyScale.z));
                yScale = Mathf.Max(0.01f, transform.lossyScale.y);
            }

            var capsule = GetComponent<CapsuleCollider>();
            if (capsule != null)
            {
                float targetRadius = capsule.radius * xzScale;
                float targetHeight = capsule.height * yScale;

                agent.radius = Mathf.Max(agent.radius, targetRadius);
                agent.height = Mathf.Max(agent.height, targetHeight);
            }
        }

        void NormalizeColliderSizeToAgent()
        {
            if (agent == null) return;
            var capsule = GetComponent<CapsuleCollider>();
            if (capsule == null) return;

            float xzScale = Mathf.Max(0.01f, Mathf.Max(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.z)));
            float yScale = Mathf.Max(0.01f, Mathf.Abs(transform.lossyScale.y));

            float desiredWorldRadius = Mathf.Clamp(agent.radius, 0.25f, 0.45f);
            float desiredWorldHeight = Mathf.Clamp(agent.height, 1.8f, 2.3f);

            capsule.radius = desiredWorldRadius / xzScale;
            capsule.height = desiredWorldHeight / yScale;

            // Keep feet close to floor in world space.
            Vector3 c = capsule.center;
            c.y = (desiredWorldHeight * 0.5f) / yScale;
            capsule.center = c;
        }

        void EnsureNavObstacleOnObject(GameObject go)
        {
            if (go == null) return;
            var col = go.GetComponent<Collider>();
            if (col == null || col.isTrigger) return;

            var obstacle = go.GetComponent<NavMeshObstacle>();
            if (obstacle == null) obstacle = go.AddComponent<NavMeshObstacle>();

            obstacle.carving = true;
            obstacle.carveOnlyStationary = true;

            if (col is CapsuleCollider capsule)
            {
                obstacle.shape = NavMeshObstacleShape.Capsule;
                obstacle.center = capsule.center;
                float xzScale = Mathf.Max(Mathf.Abs(go.transform.lossyScale.x), Mathf.Abs(go.transform.lossyScale.z));
                float yScale = Mathf.Abs(go.transform.lossyScale.y);
                obstacle.radius = capsule.radius * xzScale + obstacleExtraPadding;
                obstacle.height = capsule.height * yScale + (obstacleExtraPadding * 2f);
                return;
            }

            obstacle.shape = NavMeshObstacleShape.Box;
            Vector3 size = Vector3.one;
            Vector3 center = Vector3.zero;

            if (col is BoxCollider box)
            {
                Vector3 scale = go.transform.lossyScale;
                size = new Vector3(
                    Mathf.Abs(box.size.x * scale.x),
                    Mathf.Abs(box.size.y * scale.y),
                    Mathf.Abs(box.size.z * scale.z)
                ) + Vector3.one * obstacleExtraPadding;
                center = box.center;
            }
            else
            {
                Bounds b = col.bounds;
                size = b.size + Vector3.one * obstacleExtraPadding;
                center = go.transform.InverseTransformPoint(b.center);
            }

            obstacle.center = center;
            obstacle.size = size;
        }

        bool ShouldBiasToLastNoiseRoom()
        {
            if (lastHeardNoiseTime <= -998f) return false;
            if (Time.time - lastHeardNoiseTime > lastNoiseRoomBiasDuration) return false;
            return Random.value < lastNoiseRoomBiasChance;
        }

        bool IsAgentReady()
        {
            return agent != null &&
                   agent.enabled &&
                   agent.gameObject.activeInHierarchy &&
                   agent.isOnNavMesh;
        }

        bool TrySetDestination(Vector3 destination)
        {
            if (!IsAgentReady()) return false;
            if (!ResolveReachableDestination(destination, out Vector3 resolved))
                return false;
            return agent.SetDestination(resolved);
        }

        bool ResolveReachableDestination(Vector3 desired, out Vector3 resolved)
        {
            resolved = desired;
            if (agent == null) return false;
            bool strictGeometryValidation = state != State.Chase;

            if (NavMesh.SamplePosition(desired, out var desiredHit, destinationSampleRadius, NavMesh.AllAreas))
                desired = desiredHit.position;

            NavMeshPath path = new NavMeshPath();
            // Direct target first when legal.
            if (IsDestinationAllowed(desired) &&
                NavMesh.CalculatePath(transform.position, desired, NavMesh.AllAreas, path) &&
                path.status == NavMeshPathStatus.PathComplete &&
                (!strictGeometryValidation || IsPathAllowed(path)))
            {
                resolved = desired;
                return true;
            }

            // Retry around target to find a valid approach point (doorway assist).
            float bestScore = float.MaxValue;
            bool found = false;
            Vector3 best = desired;
            int tries = Mathf.Max(1, destinationRetrySamples);
            float retryRadius = Mathf.Max(0.5f, destinationRetryRadius);
            for (int i = 0; i < tries; i++)
            {
                Vector3 candidate = desired + Random.insideUnitSphere * retryRadius;
                candidate.y = desired.y;
                if (!NavMesh.SamplePosition(candidate, out var hit, destinationSampleRadius, NavMesh.AllAreas))
                    continue;

                if (!NavMesh.CalculatePath(transform.position, hit.position, NavMesh.AllAreas, path) ||
                    path.status != NavMeshPathStatus.PathComplete)
                    continue;

                if (!IsDestinationAllowed(hit.position) ||
                    (strictGeometryValidation && !IsPathAllowed(path)))
                    continue;

                // Prefer candidate closest to original desired point.
                float score = Vector3.SqrMagnitude(hit.position - desired);
                if (score < bestScore)
                {
                    bestScore = score;
                    best = hit.position;
                    found = true;
                }
            }

            if (!found) return false;
            resolved = best;
            return true;
        }

        void StabilizeAgentOnNavMesh()
        {
            if (!keepAgentSnappedToNavMesh || agent == null || !agent.enabled) return;
            if (Time.time < nextNavStabilizeAt) return;
            nextNavStabilizeAt = Time.time + 0.15f;

            if (agent.isOnNavMesh) return;

            if (NavMesh.SamplePosition(transform.position, out var hit, navSnapSearchRadius, NavMesh.AllAreas))
            {
                float d = Vector3.Distance(transform.position, hit.position);
                if (d >= navSnapWarpDistance && IsDestinationAllowed(hit.position))
                {
                    agent.Warp(hit.position);
                    transform.position = hit.position;
                }
            }
        }

        void EnforceRuntimeNoClip()
        {
            if (!enforceRuntimeAntiClip || !IsAgentReady()) return;
            if (Time.time < ignoreAntiClipUntilTime) return;

            Vector3 current = transform.position;
            if (!hasSafePosition)
            {
                lastSafePosition = current;
                hasSafePosition = true;
                return;
            }

            bool insideBlocking = IsInsideBlockingGeometry(current, antiClipProbeRadius);
            bool bodyInsideBlocking = IsBodyIntersectingBlocking(current);
            bool insideHideable = rejectDestinationsInsideHideables && IsInsideHideableCollider(current, destinationHideableClearance);
            bool crossedNavBoundary = enforceNavMeshBoundaryAntiClip && CrossedNavBoundary(lastSafePosition, current);
            float moved = Vector3.Distance(lastSafePosition, current);
            bool hardNavViolation = crossedNavBoundary && moved > 0.45f; // ignore tiny nav jitter
            // Keep anti-clip strict for real penetrations, but avoid doorway false-positives.
            bool hardWallViolation = bodyInsideBlocking || (insideBlocking && moved > 0.35f);

            if (!hardWallViolation && !insideHideable && !hardNavViolation)
            {
                lastSafePosition = current;
                return;
            }

            Vector3 fallback = lastSafePosition;
            if (!TryFindSafeRecoveryPoint(current, fallback, out var safeHitPos))
            {
                agent.ResetPath();
                return;
            }

            fallback = safeHitPos;
            agent.Warp(fallback);
            transform.position = fallback;
            agent.nextPosition = fallback;
            agent.ResetPath();
            lastSafePosition = fallback;
        }

        bool TryFindSafeRecoveryPoint(Vector3 current, Vector3 preferredFallback, out Vector3 safePos)
        {
            safePos = preferredFallback;

            // 1) Try preferred safe point first.
            if (NavMesh.SamplePosition(preferredFallback, out var hit, 1.8f, NavMesh.AllAreas) &&
                !IsInsideBlockingGeometry(hit.position, antiClipProbeRadius) &&
                !IsBodyIntersectingBlocking(hit.position))
            {
                safePos = hit.position;
                return true;
            }

            // 2) Probe nearby navmesh points around current to escape embedded walls.
            const int steps = 10;
            float baseRadius = Mathf.Max(0.8f, antiClipProbeRadius * 6f);
            for (int i = 0; i < steps; i++)
            {
                float r = baseRadius + i * 0.25f;
                Vector2 rnd = Random.insideUnitCircle.normalized * r;
                Vector3 probe = current + new Vector3(rnd.x, 0f, rnd.y);
                if (!NavMesh.SamplePosition(probe, out var pHit, 1.8f, NavMesh.AllAreas))
                    continue;
                if (IsInsideBlockingGeometry(pHit.position, antiClipProbeRadius))
                    continue;
                if (IsBodyIntersectingBlocking(pHit.position))
                    continue;

                safePos = pHit.position;
                return true;
            }

            return false;
        }

        bool CrossedNavBoundary(Vector3 fromWorld, Vector3 toWorld)
        {
            if (!NavMesh.SamplePosition(fromWorld, out var fromHit, 1.6f, NavMesh.AllAreas))
                return false;

            // If destination not near navmesh, treat this step as invalid for anti-clip purposes.
            if (!NavMesh.SamplePosition(toWorld, out var toHit, 1.6f, NavMesh.AllAreas))
                return true;

            return NavMesh.Raycast(fromHit.position, toHit.position, out _, NavMesh.AllAreas);
        }

        bool IsPathAllowed(NavMeshPath path)
        {
            if (!validatePathAgainstGeometry || path == null || path.corners == null || path.corners.Length < 2)
                return true;

            Vector3[] corners = path.corners;
            for (int i = 0; i < corners.Length - 1; i++)
            {
                if (IsSegmentBlocked(corners[i], corners[i + 1]))
                    return false;
            }

            return true;
        }

        bool IsSegmentBlocked(Vector3 a, Vector3 b)
        {
            return IsSegmentBlockedAtHeight(a, b, 1.0f);
        }

        bool IsSegmentBlockedAtHeight(Vector3 a, Vector3 b, float height)
        {
            Vector3 from = a + Vector3.up * Mathf.Max(0.3f, height);
            Vector3 to = b + Vector3.up * Mathf.Max(0.3f, height);
            Vector3 dir = to - from;
            float len = dir.magnitude;
            if (len <= 0.15f) return false;
            dir /= len;

            // Inset the trace slightly to avoid grazing hits on path-corner boundary points.
            const float inset = 0.08f;
            from += dir * inset;
            to -= dir * inset;
            len = Vector3.Distance(from, to);
            if (len <= 0.01f) return false;

            if (!Physics.Linecast(from, to, out RaycastHit hit, blockingGeometryMask, QueryTriggerInteraction.Ignore))
                return false;

            // Ignore near-endpoint grazes around doorway lips/corners.
            if (hit.distance <= 0.06f || hit.distance >= len - 0.06f)
                return false;

            return IsHardBlockingCollider(hit.collider);
        }

        bool IsDestinationAllowed(Vector3 position)
        {
            if (rejectDestinationsInsideHideables && IsInsideHideableCollider(position, destinationHideableClearance))
                return false;

            if (!rejectDestinationsInsideBlockingGeometry)
                return true;

            Vector3 probe = position + Vector3.up * 1.0f;
            int count = Physics.OverlapSphereNonAlloc(
                probe,
                Mathf.Max(0.05f, destinationWallClearance),
                movementBlockerHits,
                blockingGeometryMask,
                QueryTriggerInteraction.Ignore
            );

            for (int i = 0; i < count; i++)
            {
                Collider c = movementBlockerHits[i];
                if (c == null) continue;
                if (IsHardBlockingCollider(c)) return false;
            }

            return true;
        }

        bool IsInsideBlockingGeometry(Vector3 position, float probeRadius)
        {
            Vector3 probe = position + Vector3.up * Mathf.Max(0.3f, antiClipCheckHeight);
            int count = Physics.OverlapSphereNonAlloc(
                probe,
                Mathf.Max(0.05f, probeRadius),
                movementBlockerHits,
                blockingGeometryMask,
                QueryTriggerInteraction.Ignore
            );

            for (int i = 0; i < count; i++)
            {
                Collider c = movementBlockerHits[i];
                if (c == null) continue;
                if (IsHardBlockingCollider(c)) return true;
            }

            return false;
        }

        bool IsBodyIntersectingBlocking(Vector3 worldPos)
        {
            var stalkerCapsule = GetComponent<CapsuleCollider>();
            if (stalkerCapsule == null) return false;

            float radius = 0.35f;
            float height = 2.0f;
            if (stalkerCapsule != null)
            {
                float xzScale = Mathf.Max(0.01f, Mathf.Max(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.z)));
                float yScale = Mathf.Max(0.01f, Mathf.Abs(transform.lossyScale.y));
                radius = Mathf.Max(0.12f, stalkerCapsule.radius * xzScale * 0.92f);
                height = Mathf.Max(radius * 2f + 0.01f, stalkerCapsule.height * yScale * 0.98f);
            }
            else if (agent != null)
            {
                radius = Mathf.Max(0.12f, agent.radius * 0.92f);
                height = Mathf.Max(radius * 2f + 0.01f, agent.height * 0.98f);
            }

            Vector3 center = worldPos + Vector3.up * Mathf.Max(0.2f, antiClipCheckHeight);
            float half = Mathf.Max(0.01f, (height * 0.5f) - radius);
            Vector3 p1 = center + Vector3.up * half;
            Vector3 p2 = center - Vector3.up * half;

            int count = Physics.OverlapCapsuleNonAlloc(
                p1,
                p2,
                radius,
                capsuleBlockerHits,
                blockingGeometryMask,
                QueryTriggerInteraction.Ignore
            );

            for (int i = 0; i < count; i++)
            {
                Collider c = capsuleBlockerHits[i];
                if (c == null) continue;
                if (!IsHardBlockingCollider(c)) continue;

                // Only treat as stuck if penetration is meaningful, not just touching.
                if (Physics.ComputePenetration(
                    stalkerCapsule, worldPos, transform.rotation,
                    c, c.transform.position, c.transform.rotation,
                    out _, out float distance))
                {
                    if (distance >= Mathf.Max(0.001f, antiClipPenetrationEpsilon))
                        return true;
                }
            }

            return false;
        }

        bool IsInsideHideableCollider(Vector3 point, float clearance)
        {
            if (cachedHideableColliders.Count == 0) return false;
            float sqr = Mathf.Max(0.01f, clearance * clearance);
            Vector3 probe = point + Vector3.up * 0.5f;

            for (int i = 0; i < cachedHideableColliders.Count; i++)
            {
                Collider c = cachedHideableColliders[i];
                if (c == null || !c.gameObject.activeInHierarchy) continue;
                Vector3 closest = c.ClosestPoint(probe);
                if ((closest - probe).sqrMagnitude <= sqr)
                    return true;
            }

            return false;
        }

        bool IsHardBlockingCollider(Collider c)
        {
            if (c == null || c.isTrigger) return false;
            Transform t = c.transform;
            if (t == transform || t.IsChildOf(transform)) return false;
            if (player != null && (t == player || t.IsChildOf(player))) return false;
            Door door = t.GetComponentInParent<Door>();
            if (door != null && door.IsOpen) return false;

            // Hideables are always treated as blocked for stalker destinations.
            if (t.GetComponentInParent<HideableObject>() != null) return true;

            // Ignore tiny pickup colliders so they don't invalidate path checks.
            if (t.GetComponentInParent<KeyItem>() != null) return false;

            return true;
        }

        bool HasReachedDestination(float padding)
        {
            if (!IsAgentReady()) return false;
            if (agent.pathPending) return false;
            if (!agent.hasPath) return true;

            float stopDist = agent.stoppingDistance + padding;
            Vector3 delta = agent.destination - transform.position;
            delta.y = 0f;
            return delta.sqrMagnitude <= stopDist * stopDist;
        }

        bool CanSeePlayer()
        {
            if (player == null) return false;
            if (IsPlayerHidden()) return false;

            Vector3 toPlayer = player.position - transform.position;
            float dist = toPlayer.magnitude;
            if (dist > sightRange) return false;

            if (!omnidirectionalVision)
            {
                float angle = Vector3.Angle(transform.forward, toPlayer.normalized);
                if (angle > fovDegrees * 0.5f) return false;
            }

            Vector3 eye = transform.position + Vector3.up * 1.6f;
            Vector3 target = player.position + Vector3.up * 1.2f;
            Vector3 dir = (target - eye).normalized;
            float rayDist = Mathf.Min(sightRange, Vector3.Distance(eye, target) + 0.5f);

            int mask = strictWallOcclusionVision ? ~0 : losMask.value;
            if (!strictWallOcclusionVision && mask == 0) mask = ~0;
            RaycastHit[] hits = Physics.RaycastAll(eye, dir, rayDist, mask, QueryTriggerInteraction.Ignore);
            if (hits.Length == 0) return false;
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            for (int i = 0; i < hits.Length; i++)
            {
                Transform hitT = hits[i].collider.transform;
                if (hitT == transform || hitT.IsChildOf(transform)) continue; // ignore stalker self hits

                if (hitT == player) return true;
                if (hitT.IsChildOf(player)) return true;
                if (hitT.GetComponentInParent<RohitFPSController>() != null) return true;
                if (hitT.GetComponentInParent<PlayerDeath>() != null) return true;

                // First non-self blocker before player means no LOS.
                return false;
            }

            return false;
        }

        bool IsPlayerHidden()
        {
            if (playerHide != null && playerHide.IsHidden) return true;
            if (rohitFPS != null && rohitFPS.isHidden) return true;
            return false;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(lastNoisePos, lastNoiseIntensity);
        }
    }
}
