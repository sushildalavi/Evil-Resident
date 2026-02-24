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

        [Header("Hiding Spot Checks (Alpha)")]
        public List<Transform> hidingInspectPoints = new();
        [Range(0f, 1f)] public float checkHidingChance = 0.4f;
        public float inspectPauseMin = 0.8f;
        public float inspectPauseMax = 1.4f;

        [Header("Vision")]
        public float sightRange = 12f;
        public float fovDegrees = 110f;
        public bool sightAlwaysStartsChase = true;
        public bool requireNoiseToChase = true;
        public float noiseChaseWindow = 12f;
        public bool allowProximityChaseWithoutNoise = false;
        public float proximityChaseDistance = 2.2f;
        public bool maintainChaseUntilHidden = true;

        [Header("Distraction")]
        public bool distractOnThrowNoise = true;
        public string throwReleaseNoiseType = "throw";
        public string throwImpactNoiseType = "throwImpact";
        public float throwNoiseHearingBoost = 1.8f;
        public float minThrowHearingRadius = 25f;
        
        [Header("Timed Visual Chase")]
        public bool limitVisualChaseTime = true;
        public float maxVisualChaseSeconds = 7f;

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

        [Header("Creep Moments (optional but nice)")]
        [Range(0f, 1f)] public float pauseOutsideChance = 0.25f;
        public float pauseMin = 0.8f;
        public float pauseMax = 1.5f;

        [Header("Kill")]
        public float killDistance = 1.7f;

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
            roamCenter = transform.position;
            ResolvePlayerReference();
            wasPlayerHiddenLastFrame = IsPlayerHidden();

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
                ChangeState(State.Search);
                wasPlayerHiddenLastFrame = isHiddenNow;
                return;
            }

            bool canSee = CanSeePlayer();
            bool chaseUnlocked = !requireNoiseToChase ||
                                 (Time.time - lastHeardNoiseTime <= noiseChaseWindow) ||
                                 (allowProximityChaseWithoutNoise &&
                                  Vector3.Distance(transform.position, player.position) <= proximityChaseDistance);

            bool justExitedHideInSight = wasPlayerHiddenLastFrame && !isHiddenNow && canSee;
            bool visualChaseAllowed = canSee && (sightAlwaysStartsChase || chaseUnlocked);
            if (state != State.Chase && (justExitedHideInSight || visualChaseAllowed))
            {
                StartVisualChaseWindow();
                ChangeState(State.Chase);
            }

            // ===== NEW: One-shot kill =====
            if (state == State.Chase && !killTriggered &&
                !isHiddenNow &&
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

            float dist = Vector3.Distance(transform.position, pos);
            float hearingRadius = Mathf.Max(minHearingRadius, intensity * hearingScale);
            if (IsDistractionNoise(type))
            {
                hearingRadius = Mathf.Max(minThrowHearingRadius, hearingRadius * throwNoiseHearingBoost);
            }
            if (dist > hearingRadius) return;

            Vector3 targetPos = pos;
            if (NavMesh.SamplePosition(pos, out var navHit, 3f, NavMesh.AllAreas))
                targetPos = navHit.position;

            lastNoisePos = targetPos;
            lastNoiseIntensity = intensity;
            hasNoise = true;
            lastHeardNoiseTime = Time.time;

            // During chase, thrown noises can pull the stalker off the player briefly.
            if (IsDistractionNoise(type))
            {
                if (distractOnThrowNoise)
                {
                    if (state == State.Chase)
                        forcedNextSearchDuration = throwDistractionSearchDuration;

                    ChangeState(State.Investigate); // always go investigate thrown noise source
                }
                return;
            }

            bool noiseLikelyFromPlayer = player != null &&
                                         Vector3.Distance(player.position, targetPos) <= 1.5f &&
                                         !IsPlayerHidden();

            if (noiseLikelyFromPlayer)
                ChangeState(State.Chase);
            else
                ChangeState(State.Investigate);
        }

        void ResolvePlayerReference()
        {
            if (player == null)
            {
                var rohit = FindObjectOfType<RohitFPSController>();
                if (rohit != null) player = rohit.transform;
            }

            if (player == null)
            {
                var fps = FindObjectOfType<Sushil.Demo.SushilFPSController>();
                if (fps != null) player = fps.transform;
            }

            if (player == null)
            {
                var death = FindObjectOfType<PlayerDeath>();
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

            Debug.Log($"[StalkerAI] {reason} (Rohit fallback)");

            rohit.enabled = false;
            rohit.CancelInvoke();

            var throwRock = rohit.GetComponent<ThrowRock>();
            if (throwRock != null) throwRock.enabled = false;

            var cc = rohit.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        IEnumerator Patrol()
        {
            while (state == State.Patrol)
            {
                if (!IsAgentReady()) { yield return null; continue; }
                if (player != null) roamCenter = player.position;

                Vector3 targetPos;
                if (useFreeRoamPatrol || patrolPoints.Count == 0)
                {
                    if (!TryGetRandomRoamPoint(roamCenter, freeRoamRadius, out targetPos))
                    {
                        yield return null;
                        continue;
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

            // Fake leave: step away then return
            if (Random.value < fakeLeaveChance)
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

                if (IsAgentReady())
                {
                    agent.isStopped = false;
                    TrySetDestination(player.position);
                }

                if (limitVisualChaseTime && chaseUntilTime > 0f && Time.time >= chaseUntilTime)
                {
                    lastNoisePos = player.position;
                    lastNoiseIntensity = Mathf.Max(lastNoiseIntensity, 5f);
                    hasNoise = true;
                    ChangeState(State.Search);
                    yield break;
                }

                if (!maintainChaseUntilHidden && !CanSeePlayer())
                {
                    Vector3 lastKnown = player.position;
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
            return noiseType == throwReleaseNoiseType || noiseType == throwImpactNoiseType;
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
