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

        [Header("Hiding Spot Checks (Alpha)")]
        public List<Transform> hidingInspectPoints = new();
        [Range(0f, 1f)] public float checkHidingChance = 0.4f;
        public float inspectPauseMin = 0.8f;
        public float inspectPauseMax = 1.4f;

        [Header("Vision")]
        public float sightRange = 12f;
        public float fovDegrees = 110f;

        [Header("Noise Hearing")]
        public float minNoiseIntensity = 1f;
        private Vector3 lastNoisePos;
        private float lastNoiseIntensity;
        private bool hasNoise;

        [Header("Search Behavior")]
        public float searchDuration = 8f;
        public float searchRadius = 4f;
        [Range(0f, 1f)] public float fakeLeaveChance = 0.6f;

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
        private bool killTriggered;

        void Reset() { agent = GetComponent<NavMeshAgent>(); }

        void OnEnable() => NoiseSystem.OnNoise += OnNoise;
        void OnDisable() => NoiseSystem.OnNoise -= OnNoise;

        void Start()
        {
            if (agent == null) agent = GetComponent<NavMeshAgent>();

            // Avoid floating weirdness
            agent.baseOffset = 0f;

            if (player == null)
            {
                var p = GameObject.FindGameObjectWithTag("Player");
                if (p != null) player = p.transform;
            }

            if (player != null)
                playerDeath = player.GetComponent<PlayerDeath>(); // NEW

            ChangeState(State.Patrol);
        }

        void Update()
        {
            if (player == null) return;

            // If player is dead/disabled, stop AI cleanly
            if (!player.gameObject.activeInHierarchy)
            {
                agent.isStopped = true;
                return;
            }

            if (CanSeePlayer() && state != State.Chase)
                ChangeState(State.Chase);

            // ===== NEW: One-shot kill =====
            if (state == State.Chase && !killTriggered &&
                Vector3.Distance(transform.position, player.position) <= killDistance)
            {
                killTriggered = true;

                Debug.Log("[StalkerAI] PLAYER KILLED (one-shot)");

                if (playerDeath != null)
                    playerDeath.Kill("Stalker one-shot");
                else
                    player.gameObject.SetActive(false); // fallback if you forgot to add PlayerDeath
            }
        }

        void ChangeState(State newState)
        {
            state = newState;
            if (routine != null) StopCoroutine(routine);

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
            if (dist > intensity) return;

            lastNoisePos = pos;
            lastNoiseIntensity = intensity;
            hasNoise = true;

            if (state != State.Chase)
                ChangeState(State.Investigate);
        }

        IEnumerator Patrol()
        {
            while (state == State.Patrol)
            {
                if (patrolPoints.Count == 0) { yield return null; continue; }

                Transform target = patrolPoints[Random.Range(0, patrolPoints.Count)];
                agent.isStopped = false;
                agent.SetDestination(target.position);

                while (state == State.Patrol && agent.pathPending) yield return null;
                while (state == State.Patrol && agent.remainingDistance > agent.stoppingDistance + 0.25f)
                    yield return null;

                yield return new WaitForSeconds(Random.Range(0.6f, 1.6f));
            }
        }

        IEnumerator Investigate()
        {
            if (!hasNoise) { ChangeState(State.Patrol); yield break; }

            agent.isStopped = false;
            agent.SetDestination(lastNoisePos);

            while (state == State.Investigate && agent.pathPending) yield return null;
            while (state == State.Investigate && agent.remainingDistance > agent.stoppingDistance + 0.35f)
                yield return null;

            if (state != State.Investigate) yield break;
            ChangeState(State.Search);
        }

        IEnumerator Search()
        {
            float elapsed = 0f;
            Vector3 center = lastNoisePos;

            // Fake leave: step away then return
            if (Random.value < fakeLeaveChance)
            {
                Vector3 away = center + Random.insideUnitSphere * (searchRadius + 3f);
                away.y = center.y;

                if (NavMesh.SamplePosition(away, out var hit, 4f, NavMesh.AllAreas))
                {
                    agent.isStopped = false;
                    agent.SetDestination(hit.position);
                    yield return new WaitForSeconds(1.0f);
                }

                agent.SetDestination(center);
                yield return new WaitForSeconds(0.7f);
            }

            while (state == State.Search && elapsed < searchDuration)
            {
                // Stand still and "listen"
                if (Random.value < pauseOutsideChance)
                {
                    agent.isStopped = true;
                    yield return new WaitForSeconds(Random.Range(pauseMin, pauseMax));
                    agent.isStopped = false;
                }

                // Sometimes go check a hiding inspect point
                if (hidingInspectPoints.Count > 0 && Random.value < checkHidingChance)
                {
                    Transform inspectPoint = hidingInspectPoints[Random.Range(0, hidingInspectPoints.Count)];
                    agent.isStopped = false;
                    agent.SetDestination(inspectPoint.position);

                    // Wait up to ~2 seconds or until close enough
                    float t = 0f;
                    while (state == State.Search && t < 2.0f &&
                           agent.remainingDistance > agent.stoppingDistance + 0.35f)
                    {
                        t += Time.deltaTime;
                        yield return null;
                    }

                    // Pause like inspecting
                    agent.isStopped = true;
                    yield return new WaitForSeconds(Random.Range(inspectPauseMin, inspectPauseMax));
                    agent.isStopped = false;

                    elapsed += 1f;
                    continue;
                }

                // Normal wandering around the noise center
                Vector3 rnd = center + Random.insideUnitSphere * searchRadius;
                rnd.y = center.y;

                if (NavMesh.SamplePosition(rnd, out var hit2, 4f, NavMesh.AllAreas))
                    agent.SetDestination(hit2.position);

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
                    agent.isStopped = true;
                    yield break;
                }

                agent.isStopped = false;
                agent.SetDestination(player.position);

                if (!CanSeePlayer())
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

        bool CanSeePlayer()
        {
            if (player == null) return false;

            Vector3 toPlayer = player.position - transform.position;
            float dist = toPlayer.magnitude;
            if (dist > sightRange) return false;

            float angle = Vector3.Angle(transform.forward, toPlayer.normalized);
            if (angle > fovDegrees * 0.5f) return false;

            Vector3 eye = transform.position + Vector3.up * 1.6f;
            Vector3 target = player.position + Vector3.up * 1.2f;

            if (Physics.Raycast(eye, (target - eye).normalized, out var hit, sightRange, losMask))
                return hit.collider.CompareTag("Player");

            return false;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(lastNoisePos, lastNoiseIntensity);
        }
    }
}