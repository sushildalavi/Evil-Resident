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
        public float minSpawnDistanceFromKeys = 10f;
        public float minSpawnDistanceFromHidingSpots = 8f;
        [Range(0f, 180f)] public float playerViewExclusionHalfAngle = 60f;
        public bool avoidPlayerForwardConeAtSpawn = true;

        [Header("Navigation Collision Fixes")]
        public bool autoAddObstaclesForHideablesAndCupboards = true;
        public bool autoSyncAgentAndColliderToScale = true;
        [Tooltip("If true, NavMeshAgent sizing uses collider's authored values and ignores root transform scale. Keep this on when visual scale is enlarged.")]
        public bool ignoreRootScaleForNavSync = true;
        [Tooltip("Force agent capsule dimensions each run so door traversal remains stable.")]
        public bool enforceAgentSize = true;
        [Range(0.35f, 0.45f)] public float enforcedAgentRadius = 0.40f;
        [Range(2.0f, 2.3f)] public float enforcedAgentHeight = 2.20f;
        public float obstacleExtraPadding = 0.05f;

        [Header("Hiding Spot Checks (Alpha)")]
        public List<Transform> hidingInspectPoints = new();
        [Range(0f, 1f)] public float checkHidingChance = 0.4f;
        public float inspectPauseMin = 0.8f;
        public float inspectPauseMax = 1.4f;

        [Header("Vision")]
        public float sightRange = 12f;
        public float fovDegrees = 110f;
        public bool alwaysChaseWhenVisible = true;
        public bool sightAlwaysStartsChase = true;
        public bool requireNoiseToChase = true;
        public float noiseChaseWindow = 12f;
        public bool allowProximityChaseWithoutNoise = false;
        public float proximityChaseDistance = 2.2f;
        public bool maintainChaseUntilHidden = true;
        public bool globalNoiseForcesChase = true;

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

            // Avoid floating weirdness
            agent.baseOffset = 0f;
            CacheHideables();
            SetupNavigationCollisionFixes();
            roamCenter = ComputeRoamCenter();
            ResolvePlayerReference();
            TryRandomizeSpawn();
            wasPlayerHiddenLastFrame = IsPlayerHidden();
            if (player != null)
            {
                lastSeenPlayerPos = player.position;
                lastSeenPlayerTime = Time.time;
            }

            ChangeState(State.Patrol);
        }

        void Update()
        {
            if (player == null)
            {
                ResolvePlayerReference();
                if (player == null) return;
            }

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
                lastNoisePos = player.position;
                lastNoiseIntensity = Mathf.Max(lastNoiseIntensity, 6f);
                hasNoise = true;
                if (forceFakeLeaveAfterPlayerHides)
                {
                    forceFakeLeaveOnce = true;
                    forcedNextSearchDuration = Mathf.Max(forcedNextSearchDuration, hideTriggeredSearchDuration);
                }
                ChangeState(State.Search);
                wasPlayerHiddenLastFrame = isHiddenNow;
                return;
            }

            bool canSee = CanSeePlayer();
            if (canSee)
            {
                lastSeenPlayerPos = player.position;
                lastSeenPlayerTime = Time.time;
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
                CanSeePlayer() &&
                Vector3.Distance(transform.position, player.position) <= killDistance)
            {
                TryKillTarget(player.gameObject, "Stalker one-shot");
            }

            wasPlayerHiddenLastFrame = isHiddenNow;
        }

        void ChangeState(State newState)
        {
            state = newState;
            if (routine != null) StopCoroutine(routine);
            if (newState != State.Chase) chaseUntilTime = -1f;
            if (newState != State.Chase) farChaseTimer = 0f;

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
            if (!hasNoise) { ChangeState(State.Patrol); yield break; }
            if (!IsAgentReady()) { ChangeState(State.Patrol); yield break; }

            agent.isStopped = false;
            if (!TrySetDestination(lastNoisePos)) { ChangeState(State.Patrol); yield break; }

            while (state == State.Investigate && IsAgentReady() && agent.pathPending) yield return null;
            while (state == State.Investigate && IsAgentReady() &&
                   !HasReachedDestination(0.35f))
                yield return null;

            if (state != State.Investigate) yield break;
            ChangeState(State.Search);
        }

        IEnumerator Search()
        {
            float elapsed = 0f;
            Vector3 center = lastNoisePos;
            float activeSearchDuration = forcedNextSearchDuration > 0f ? forcedNextSearchDuration : searchDuration;
            forcedNextSearchDuration = -1f;
            bool shouldFakeLeave = forceFakeLeaveOnce || Random.value < fakeLeaveChance;
            forceFakeLeaveOnce = false;

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

                bool canSee = CanSeePlayer();
                bool hasFreshNoise = Time.time - lastHeardNoiseTime <= noiseChaseWindow;

                if (canSee)
                {
                    lastSeenPlayerPos = player.position;
                    lastSeenPlayerTime = Time.time;
                }
                else if (hasFreshNoise)
                {
                    lastSeenPlayerPos = lastNoisePos;
                    lastSeenPlayerTime = Time.time;
                }

                if (IsAgentReady())
                {
                    agent.isStopped = false;
                    Vector3 chaseTarget = canSee
                        ? player.position
                        : lastSeenPlayerPos;
                    TrySetDestination(chaseTarget);
                }

                // If player breaks LOS (behind walls/inside rooms), stalker pushes to last known
                // spot briefly, then gives up unless new noise keeps coming.
                if (!canSee && !hasFreshNoise && Time.time - lastSeenPlayerTime > chaseMemorySeconds)
                {
                    lastNoisePos = lastSeenPlayerPos;
                    lastNoiseIntensity = Mathf.Max(lastNoiseIntensity, 5f);
                    hasNoise = true;
                    ChangeState(State.Search);
                    yield break;
                }

                if (loseChaseWhenFar)
                {
                    float chaseDistance = Vector3.Distance(transform.position, player.position);
                    if (chaseDistance > maxChaseDistance)
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

                if (limitVisualChaseTime && chaseUntilTime > 0f && Time.time >= chaseUntilTime)
                {
                    lastNoisePos = lastSeenPlayerPos;
                    lastNoiseIntensity = Mathf.Max(lastNoiseIntensity, 5f);
                    hasNoise = true;
                    ChangeState(State.Search);
                    yield break;
                }

                if (!maintainChaseUntilHidden && !canSee)
                {
                    Vector3 lastKnown = lastSeenPlayerPos;
                    yield return new WaitForSeconds(1.1f);

                    if (!CanSeePlayer())
                    {
                        lastNoisePos = lastKnown;
                        lastNoiseIntensity = 6f;
                        hasNoise = true;
                        ChangeState(State.Search);
                        yield break;
                    }
                }

                yield return null;
            }
        }

        void StartVisualChaseWindow()
        {
            if (!limitVisualChaseTime) { chaseUntilTime = -1f; return; }
            chaseUntilTime = Time.time + Mathf.Max(0.5f, maxVisualChaseSeconds);
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

            for (int i = 0; i < Mathf.Max(8, spawnSampleAttempts); i++)
            {
                Vector3 candidate = center + Random.insideUnitSphere * Mathf.Max(8f, spawnSearchRadius);
                candidate.y = center.y;
                if (!NavMesh.SamplePosition(candidate, out var hit, 8f, NavMesh.AllAreas))
                    continue;

                Vector3 pos = hit.position;
                if (!IsSpawnPositionValid(pos, keys, cachedHideables)) continue;

                best = pos;
                found = true;
                break;
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

        bool IsSpawnPositionValid(Vector3 pos, KeyItem[] keys, HideableObject[] hideables)
        {
            if (player != null)
            {
                Vector3 flatPlayer = player.position;
                flatPlayer.y = pos.y;
                if (Vector3.Distance(pos, flatPlayer) < Mathf.Max(0.5f, minSpawnDistanceFromPlayer))
                    return false;

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
        }

        void SetupNavigationCollisionFixes()
        {
            if (autoSyncAgentAndColliderToScale)
                SyncAgentAndColliderToScale();

            if (enforceAgentSize && agent != null)
            {
                agent.radius = Mathf.Clamp(enforcedAgentRadius, 0.35f, 0.45f);
                agent.height = Mathf.Clamp(enforcedAgentHeight, 2.0f, 2.3f);
            }

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
            return agent.SetDestination(destination);
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

            float angle = Vector3.Angle(transform.forward, toPlayer.normalized);
            if (angle > fovDegrees * 0.5f) return false;

            Vector3 eye = transform.position + Vector3.up * 1.6f;
            Vector3 target = player.position + Vector3.up * 1.2f;
            Vector3 dir = (target - eye).normalized;
            float rayDist = Mathf.Min(sightRange, Vector3.Distance(eye, target) + 0.5f);

            RaycastHit[] hits = Physics.RaycastAll(eye, dir, rayDist, losMask, QueryTriggerInteraction.Ignore);
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
