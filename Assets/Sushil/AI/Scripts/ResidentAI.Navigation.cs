using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Sushil.AI
{
    public partial class ResidentAI
    {
        bool UpdateRoamStallState(ref Vector3 lastPosition, ref float stallTimer)
        {
            if (!IsAgentReady())
                return true;

            float moved = Vector3.Distance(transform.position, lastPosition);
            bool stalled = !agent.pathPending &&
                           (!agent.hasPath || (moved < 0.03f && agent.velocity.sqrMagnitude < 0.08f * 0.08f));
            stallTimer = stalled ? stallTimer + Time.deltaTime : 0f;
            lastPosition = transform.position;
            return stallTimer >= Mathf.Max(0.25f, roamStuckRepathSeconds);
        }

        bool TryGetNearbyWallNormal(float checkDistance, out Vector3 wallNormal)
        {
            wallNormal = Vector3.zero;
            Vector3 origin = transform.position + Vector3.up * 1.0f;
            Vector3 forward = transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.001f)
                forward = Vector3.forward;
            forward.Normalize();

            Vector3 right = transform.right;
            right.y = 0f;
            if (right.sqrMagnitude < 0.001f)
                right = Vector3.right;
            right.Normalize();

            Vector3[] directions =
            {
                forward,
                -forward,
                right,
                -right,
                (forward + right).normalized,
                (forward - right).normalized,
                (-forward + right).normalized,
                (-forward - right).normalized
            };

            float closestHit = checkDistance;
            bool found = false;
            for (int i = 0; i < directions.Length; i++)
            {
                if (!Physics.Raycast(origin, directions[i], out RaycastHit hit, checkDistance, blockingGeometryMask, QueryTriggerInteraction.Ignore))
                    continue;
                if (!IsHardBlockingCollider(hit.collider))
                    continue;
                if (hit.distance >= closestHit)
                    continue;

                closestHit = hit.distance;
                wallNormal = hit.normal;
                found = true;
            }

            return found;
        }

        bool TryGetTravelDirection(out Vector3 direction)
        {
            direction = Vector3.zero;

            if (IsAgentReady())
            {
                Vector3 desired = agent.desiredVelocity;
                desired.y = 0f;
                if (desired.sqrMagnitude > 0.0025f)
                {
                    direction = desired.normalized;
                    return true;
                }

                Vector3 steering = agent.steeringTarget - transform.position;
                steering.y = 0f;
                if (steering.sqrMagnitude > 0.01f)
                {
                    direction = steering.normalized;
                    return true;
                }

                if (agent.hasPath && agent.path != null && agent.path.corners != null)
                {
                    Vector3[] corners = agent.path.corners;
                    for (int i = 1; i < corners.Length; i++)
                    {
                        Vector3 towardCorner = corners[i] - transform.position;
                        towardCorner.y = 0f;
                        if (towardCorner.sqrMagnitude <= 0.01f)
                            continue;

                        direction = towardCorner.normalized;
                        return true;
                    }
                }

                Vector3 toDestination = agent.destination - transform.position;
                toDestination.y = 0f;
                if (toDestination.sqrMagnitude > 0.01f)
                {
                    direction = toDestination.normalized;
                    return true;
                }
            }

            if (state == State.Chase && player != null)
            {
                Vector3 toPlayer = player.position - transform.position;
                toPlayer.y = 0f;
                if (toPlayer.sqrMagnitude > 0.01f)
                {
                    direction = toPlayer.normalized;
                    return true;
                }
            }

            Vector3 toLastSeen = lastSeenPlayerPos - transform.position;
            toLastSeen.y = 0f;
            if (toLastSeen.sqrMagnitude > 0.01f)
            {
                direction = toLastSeen.normalized;
                return true;
            }

            return false;
        }

        void ReissuePrimaryDestination(Vector3 fallbackDestination)
        {
            if (!IsAgentReady())
                return;

            if (state == State.Chase && player != null && TrySetDestination(player.position))
                return;

            if (TrySetDestination(fallbackDestination))
                return;

            agent.SetDestination(fallbackDestination);
        }

        bool IsInStairTraversalContext()
        {
            if (GetStairBlend() >= 0.16f || IsCurrentlyOnElevatedPath())
                return true;

            if (player == null)
                return false;

            Vector3 toPlayer = player.position - transform.position;
            float horizontalDistance = new Vector2(toPlayer.x, toPlayer.z).magnitude;
            return Mathf.Abs(toPlayer.y) > 0.45f && horizontalDistance <= 8.5f;
        }

        float GetPrimaryVerticalTraversalDelta()
        {
            if (state == State.Chase && player != null)
                return player.position.y - transform.position.y;

            if (IsAgentReady())
                return agent.destination.y - transform.position.y;

            return 0f;
        }

        bool IsCloseChaseEntryContext(Vector3 worldPos)
        {
            if (state != State.Chase || player == null || IsPlayerHidden())
                return false;

            float verticalToPlayer = Mathf.Abs(worldPos.y - player.position.y);
            if (verticalToPlayer > Mathf.Min(0.9f, Mathf.Max(0.55f, proximityChaseVerticalTolerance)))
                return false;

            Vector3 worldPosFlat = worldPos;
            worldPosFlat.y = 0f;
            Vector3 playerFlat = player.position;
            playerFlat.y = 0f;
            if (Vector3.Distance(worldPosFlat, playerFlat) > 1.55f)
                return false;

            Vector3 residentFlat = transform.position;
            residentFlat.y = 0f;
            if (Vector3.Distance(residentFlat, playerFlat) > Mathf.Max(3.4f, doorwayAssistRange * 0.18f))
                return false;

            return true;
        }

        IEnumerator IdleLookAround(float duration, float maxAngle)
        {
            duration = Mathf.Max(0f, duration);
            if (duration <= 0f)
                yield break;

            if (TryGetNearbyWallNormal(0.7f, out var wallNormal))
            {
                Vector3 away = -wallNormal;
                away.y = 0f;
                if (away.sqrMagnitude > 0.001f)
                {
                    Quaternion targetRotation = Quaternion.LookRotation(away.normalized, Vector3.up);
                    float wallTurnEndAt = Time.time + Mathf.Min(duration, 0.2f);
                    while ((state == State.Patrol || state == State.Search) && Time.time < wallTurnEndAt)
                    {
                        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 8f);
                        yield return null;
                    }
                }
                yield break;
            }

            float startedAt = Time.time;
            float endAt = startedAt + duration;
            float baseYaw = transform.eulerAngles.y;
            float angle = Mathf.Max(4f, maxAngle);

            while ((state == State.Patrol || state == State.Search) && Time.time < endAt)
            {
                float t = Mathf.InverseLerp(startedAt, endAt, Time.time);
                float sweep = Mathf.Sin(t * Mathf.PI * 2f) * angle;
                Quaternion targetRotation = Quaternion.Euler(0f, baseYaw + sweep, 0f);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 4.5f);
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
                assisted = TryAdvanceThroughOpenDoor(openDoor, assistTarget);
                if (!assisted && doorwayWarpAssist)
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
            Vector3 from = transform.position;
            Vector3 to = targetPos;
            const int steps = 7;
            bool elevatedAssist = Mathf.Abs(targetPos.y - from.y) > 0.35f;
            float sampleRadius = elevatedAssist
                ? doorwayAssistSampleRadius * 1.5f
                : doorwayAssistSampleRadius;

            for (int i = 1; i <= steps; i++)
            {
                float t = i / (float)steps;
                Vector3 mid = Vector3.Lerp(from, to, t);
                mid.y = elevatedAssist ? Mathf.Lerp(from.y, targetPos.y, t) : from.y;

                if (!NavMesh.SamplePosition(mid, out var hit, sampleRadius, NavMesh.AllAreas))
                    continue;

                if (!ResolveReachableDestination(hit.position, out var resolved))
                    continue;

                if (Vector3.Distance(transform.position, resolved) < 0.2f)
                    continue;

                agent.SetDestination(resolved);
                return true;
            }

            if (NavMesh.SamplePosition(targetPos, out var nearPlayer, sampleRadius * 2f, NavMesh.AllAreas) &&
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
            Vector3 dir = targetPos - from;
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
                                continue;
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
            return TrySetDestination(targetPos);
        }

        bool TryAdvanceThroughOpenDoor(Door door, Vector3 targetPos)
        {
            if (door == null || !IsAgentReady()) return false;

            Vector3 from = transform.position;
            Vector3 toTarget = targetPos - from;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude < 0.01f) return false;
            toTarget.Normalize();

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
                    for (int side = -1; side <= 1; side += 2)
                    {
                        Vector3 probe = center + forward * (depths[d] * side) + right * widths[w];
                        if (!NavMesh.SamplePosition(probe, out var hit, 0.9f, NavMesh.AllAreas))
                            continue;
                        if (!NavMesh.CalculatePath(from, hit.position, NavMesh.AllAreas, path) ||
                            path.status != NavMeshPathStatus.PathComplete)
                            continue;

                        Vector3 dir = hit.position - from;
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
            return TrySetDestination(best);
        }

        void TryAutoOpenDoorInFront()
        {
            if (!autoOpenNearbyDoors || Time.time < nextDoorOpenTime || player == null) return;
            if (state != State.Chase && state != State.Search && state != State.Investigate) return;

            Vector3 origin = transform.position + Vector3.up * 1.0f;
            Vector3 dirToPlayer = player.position - transform.position;
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
                bool sampleDifferentFloor = allowMultiFloorRoam && Random.value < multiFloorRoamChance;
                if (sampleDifferentFloor)
                    candidate.y = center.y + Random.Range(-multiFloorRoamHeight, multiFloorRoamHeight);
                else
                    candidate.y = center.y;

                float sampleRadius = sampleDifferentFloor ? 8.5f : 6f;
                if (!NavMesh.SamplePosition(candidate, out var navHit, sampleRadius, NavMesh.AllAreas))
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
                    if (!door.IsLocked || door.IsOpen) continue;

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

            if (cachedHideables != null)
            {
                for (int i = 0; i < cachedHideables.Length; i++)
                {
                    var hide = cachedHideables[i];
                    if (hide == null) continue;
                    EnsureNavObstacleOnObject(hide.gameObject);
                }
            }

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

        float GetStairBlend()
        {
            Vector3 forward = transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.001f)
                return 0f;

            forward.Normalize();
            Vector3 right = transform.right;
            right.y = 0f;
            if (right.sqrMagnitude < 0.001f)
                right = Vector3.right;
            right.Normalize();

            if (!TryGetGroundHeight(transform.position, out float baseHeight, out float baseSlope))
                return 0f;

            float strongestStep = 0f;
            int foundSamples = 0;
            Vector3[] sampleOffsets =
            {
                forward * stairProbeForwardDistance,
                forward * stairProbeForwardDistance + right * 0.22f,
                forward * stairProbeForwardDistance - right * 0.22f
            };

            for (int i = 0; i < sampleOffsets.Length; i++)
            {
                if (!TryGetGroundHeight(transform.position + sampleOffsets[i], out float sampleHeight, out _))
                    continue;

                strongestStep = Mathf.Max(strongestStep, Mathf.Abs(sampleHeight - baseHeight));
                foundSamples++;
            }

            if (foundSamples == 0)
                return 0f;

            float stepBlend = Mathf.InverseLerp(0.04f, 0.24f, strongestStep);
            float slopeBlend = Mathf.InverseLerp(6f, 24f, baseSlope);
            return Mathf.Clamp01(Mathf.Max(stepBlend, slopeBlend * 0.85f));
        }

        bool TryGetGroundHeight(Vector3 worldCenter, out float groundHeight, out float slopeAngle)
        {
            groundHeight = 0f;
            slopeAngle = 0f;

            Vector3 origin = worldCenter + Vector3.up * Mathf.Max(0.4f, stairProbeHeight);
            float rayLength = Mathf.Max(1.2f, stairProbeHeight * 2.5f);
            if (!Physics.Raycast(origin, Vector3.down, out RaycastHit hit, rayLength, blockingGeometryMask, QueryTriggerInteraction.Ignore))
                return false;
            if (!IsHardBlockingCollider(hit.collider))
                return false;

            groundHeight = hit.point.y;
            slopeAngle = Vector3.Angle(hit.normal, Vector3.up);
            return true;
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

        // Returns true when the agent's current NavMesh path contains any segment
        // with a vertical change > 0.14 m — i.e. it is on a staircase or ramp.
        // Used as a fallback when GetStairBlend() returns < 0.24 mid-staircase
        // (the forward height-probe sees no change once the agent is already climbing).
        bool IsCurrentlyOnElevatedPath()
        {
            if (!IsAgentReady() || !agent.hasPath || agent.path == null) return false;
            Vector3[] corners = agent.path.corners;
            if (corners == null || corners.Length < 2) return false;
            for (int i = 0; i < corners.Length - 1; i++)
            {
                if (Mathf.Abs(corners[i + 1].y - corners[i].y) > 0.14f)
                    return true;
            }
            return false;
        }

        // ── GENERAL STUCK RECOVERY ────────────────────────────────────────────
        // Completely independent of stair detection.
        // If the agent has a valid path and a destination that is far away, but
        // hasn't moved more than 0.25 m in the last 0.4 s, warp to the next
        // NavMesh-sampled path corner and re-issue the original destination.
        // This is the industry-standard "unstuck nudge" and handles stairs,
        // doorways, tight corridors — anything that causes NavMesh agents to stall.
        void TryGeneralStuckRecovery()
        {
            if (!IsAgentReady()) { generalStuckPosInit = false; return; }
            if (killAttackActive)  { generalStuckPosInit = false; return; }
            if (IsInStairTraversalContext()) { generalStuckPosInit = false; generalStuckTimer = 0f; return; }
            if (agent.pathPending) return;
            if (!agent.hasPath)    { generalStuckPosInit = false; return; }
            if (agent.remainingDistance < 1.2f) { generalStuckPosInit = false; return; }

            Vector3 pos = transform.position;

            if (!generalStuckPosInit)
            {
                generalStuckLastPos  = pos;
                generalStuckPosInit  = true;
                generalStuckTimer    = 0f;
                return;
            }

            float moved = Vector3.Distance(pos, generalStuckLastPos);
            if (moved >= 0.25f)
            {
                generalStuckLastPos = pos;
                generalStuckTimer   = 0f;
                return;
            }

            generalStuckTimer += Time.deltaTime;
            if (generalStuckTimer < 0.4f) return;

            // --- Stuck confirmed.  Warp to next usable path corner. ---
            generalStuckTimer   = 0f;
            generalStuckLastPos = pos;

            Vector3[] corners = agent.path.corners;
            if (corners == null || corners.Length < 2) return;

            Vector3 savedDest = agent.destination;

            for (int i = 1; i < corners.Length; i++)
            {
                Vector3 target = corners[i];

                // Don't warp to a corner we're already past.
                float dist = Vector3.Distance(pos, target);
                if (dist < 0.3f) continue;

                // Clamp single-jump distance so we don't skip across the whole level.
                if (dist > 3.0f)
                    target = pos + (target - pos).normalized * 2.5f;

                if (!NavMesh.SamplePosition(target, out var hit, 1.8f, NavMesh.AllAreas))
                    continue;

                // Warp, seed safe-pos, suppress anti-clip for 1.5 s, restore destination.
                agent.Warp(hit.position);
                transform.position   = hit.position;
                agent.nextPosition   = hit.position;
                lastSafePosition     = hit.position;
                hasSafePosition      = true;
                ignoreAntiClipUntilTime = Time.time + 1.5f;
                ReissuePrimaryDestination(savedDest);
                return;
            }
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
            bool elevatedTargetContext = IsElevatedTargetContext(desired);
            bool closeChaseEntryContext = IsCloseChaseEntryContext(desired);
            bool strictGeometryValidation = !(elevatedTargetContext || closeChaseEntryContext);

            if (NavMesh.SamplePosition(desired, out var desiredHit, destinationSampleRadius, NavMesh.AllAreas))
                desired = desiredHit.position;

            NavMeshPath path = new NavMeshPath();
            if (IsDestinationAllowed(desired) &&
                !IsBodyIntersectingBlocking(desired) &&
                NavMesh.CalculatePath(transform.position, desired, NavMesh.AllAreas, path) &&
                path.status == NavMeshPathStatus.PathComplete &&
                (!strictGeometryValidation || IsPathAllowed(path)))
            {
                resolved = desired;
                return true;
            }

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
                    IsBodyIntersectingBlocking(hit.position) ||
                    (strictGeometryValidation && !IsPathAllowed(path)))
                    continue;

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
            if (IsInStairTraversalContext()) return;
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

        bool ShouldUseStairNavigationProfile()
        {
            if (agent == null) return false;
            if (GetStairBlend() >= 0.16f) return true;
            if (IsCloseChaseEntryContext(player != null ? player.position : transform.position)) return true;

            NavMeshPath currentPath = agent.path;
            if (currentPath != null && currentPath.corners != null)
            {
                Vector3[] corners = currentPath.corners;
                for (int i = 0; i < corners.Length - 1; i++)
                {
                    if (Mathf.Abs(corners[i + 1].y - corners[i].y) > 0.18f)
                        return true;
                }
            }

            if (player == null) return false;

            Vector3 toPlayer = player.position - transform.position;
            float horizontalDistance = new Vector2(toPlayer.x, toPlayer.z).magnitude;
            return Mathf.Abs(toPlayer.y) > 0.55f && horizontalDistance <= 8.5f;
        }

        void ApplyDynamicNavigationProfile()
        {
            if (agent == null || !agent.enabled) return;

            float targetRadius = enforcedAgentRadius;
            if (ShouldUseStairNavigationProfile())
                targetRadius = Mathf.Min(enforcedAgentRadius, 0.22f);

            float targetHeight = enforcedAgentHeight;
            bool radiusMatches = Mathf.Abs(agent.radius - targetRadius) <= 0.005f;
            bool heightMatches = Mathf.Abs(agent.height - targetHeight) <= 0.01f;
            bool cachedMatches = Mathf.Abs(currentNavigationRadius - targetRadius) <= 0.005f;
            if (radiusMatches && heightMatches && cachedMatches)
                return;

            agent.radius = targetRadius;
            agent.height = targetHeight;
            NormalizeColliderSizeToAgent();
            currentNavigationRadius = targetRadius;
        }

        void ApplyStairTraverseAssist()
        {
            if (!enableStairTraverseAssist || !IsAgentReady())
            {
                stairTraverseStuckTimer = 0f;
                return;
            }

            if (killAttackActive || agent.pathPending || !agent.hasPath)
            {
                stairTraverseStuckTimer = 0f;
                return;
            }

            if (agent.remainingDistance <= Mathf.Max(0.15f, agent.stoppingDistance + 0.05f))
            {
                stairTraverseStuckTimer = 0f;
                return;
            }

            float stairBlend      = GetStairBlend();
            bool  elevatedPath    = IsCurrentlyOnElevatedPath();
            bool  onStairs        = stairBlend >= 0.24f || elevatedPath;

            // Only activate the assist when on stairs OR traversing an elevated path.
            // Previously we required stairBlend >= 0.24, but mid-staircase the forward
            // probe often returns 0 (agent already elevated, next step same height as
            // current position). Using path corners as the fallback detection catches this.
            if (!onStairs)
            {
                stairTraverseStuckTimer = 0f;
                return;
            }

            if (!TryGetTravelDirection(out Vector3 desiredDirection))
                desiredDirection = transform.forward;

            // On stairs the agent should be near full speed.  Treat anything below
            // 45 % of target speed as "stuck" — this catches slow-crawl situations
            // where the agent creeps along the stair surface but never actually climbs.
            float targetSpeed   = IsAgentReady() ? Mathf.Max(0.5f, agent.speed) : 0.5f;
            float stuckThreshold = targetSpeed * 0.45f;
            bool nearlyStopped = agent.velocity.sqrMagnitude < stuckThreshold * stuckThreshold;
            stairTraverseStuckTimer = nearlyStopped
                ? stairTraverseStuckTimer + Time.deltaTime
                : Mathf.Max(0f, stairTraverseStuckTimer - (Time.deltaTime * 2f));

            if (!nearlyStopped)
                return;

            Vector3 current = transform.position;

            if (Time.time < nextStairTraverseAssistAt || stairTraverseStuckTimer < stairTraverseStallSeconds)
                return;

            stairTraverseStuckTimer = 0f;
            nextStairTraverseAssistAt = Time.time + stairTraverseRecoveryCooldown;

            // Remember the original destination so we can restore it after nudging.
            // If we only call SetDestination(cornerRecoveryTarget), the patrol coroutine
            // sees agent.destination == corner, and when the agent arrives there it thinks
            // the whole trip is done — picking a fresh random point instead of continuing.
            Vector3 originalDestination = agent.destination;

            Vector3 nudgeTarget;
            bool foundNudge = TryFindStairPathCornerRecoveryTarget(current, out nudgeTarget) ||
                              TryFindStairRecoveryTarget(current, desiredDirection, out nudgeTarget);

            if (!foundNudge)
                return;

            float desiredVerticalDelta = originalDestination.y - current.y;

            // Warp the agent one short validated step on the stair, then immediately
            // re-issue the original destination so pathfinding continues from the
            // un-stuck position. Never accept a recovery point that moves downward
            // while the active target is above the Resident.
            if (NavMesh.SamplePosition(nudgeTarget, out var warpHit, 1.2f, NavMesh.AllAreas))
            {
                float warpDistance = Vector3.Distance(current, warpHit.position);
                if (warpDistance > Mathf.Max(0.95f, stairTraverseProbeDistance * 1.45f))
                    return;

                if (desiredVerticalDelta > 0.18f && warpHit.position.y < current.y + 0.03f)
                {
                    TrySetDestination(nudgeTarget);
                    return;
                }

                if (desiredVerticalDelta < -0.18f && warpHit.position.y > current.y - 0.03f)
                {
                    TrySetDestination(nudgeTarget);
                    return;
                }

                agent.Warp(warpHit.position);
                transform.position = warpHit.position;
                agent.nextPosition = warpHit.position;
                // Ignore anti-clip for 1.2 s so the agent can walk several stair steps
                // before the boundary check re-arms.  lastSafePosition is kept fresh
                // throughout this window (see EnforceRuntimeNoClip early-return path).
                ignoreAntiClipUntilTime = Time.time + 1.6f;
                lastSafePosition = warpHit.position;
                hasSafePosition  = true;
                ReissuePrimaryDestination(originalDestination);
            }
        }

        bool TryFindStairPathCornerRecoveryTarget(Vector3 current, out Vector3 recoveryTarget)
        {
            recoveryTarget = current;
            if (!IsAgentReady() || !agent.hasPath || agent.path == null || agent.path.corners == null)
                return false;

            Vector3[] corners = agent.path.corners;
            if (corners.Length < 2)
                return false;

            for (int i = 1; i < corners.Length; i++)
            {
                Vector3 corner = corners[i];
                float distance = Vector3.Distance(current, corner);
                if (distance < 0.28f)
                    continue;

                bool elevatedCorner = Mathf.Abs(corner.y - current.y) > 0.18f;
                float maxCornerDistance = elevatedCorner
                    ? Mathf.Max(2.8f, stairTraverseProbeDistance * 3.6f)
                    : Mathf.Max(1.0f, stairTraverseProbeDistance * 1.35f);
                if (distance > maxCornerDistance)
                    continue;

                float sampleRadius = elevatedCorner ? 1.8f : 1.0f;
                float probeScale = elevatedCorner ? 0.22f : 0.5f;
                if (TryResolveSafeShortAdvance(corner, out recoveryTarget, sampleRadius, probeScale))
                    return true;
            }

            return false;
        }

        bool TryFindStairRecoveryTarget(Vector3 current, Vector3 desiredDirection, out Vector3 safeRecoveryTarget)
        {
            safeRecoveryTarget = current;
            if (desiredDirection.sqrMagnitude < 0.001f)
                return false;

            Vector3 forward = desiredDirection.normalized;
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            float forwardDistance = Mathf.Max(0.75f, stairTraverseProbeDistance * 1.35f);
            float sideDistance = Mathf.Max(0.2f, enforcedAgentRadius + 0.12f);

            Vector3[] probes =
            {
                current + forward * forwardDistance + Vector3.up * 0.35f,
                current + (forward * forwardDistance) + (right * sideDistance) + Vector3.up * 0.35f,
                current + (forward * forwardDistance) - (right * sideDistance) + Vector3.up * 0.35f,
                current + (forward * (forwardDistance * 0.65f)) + Vector3.up * 0.35f,
                current + (forward * (forwardDistance * 0.65f)) + (right * sideDistance * 1.1f) + Vector3.up * 0.35f,
                current + (forward * (forwardDistance * 0.65f)) - (right * sideDistance * 1.1f) + Vector3.up * 0.35f,
            };

            for (int i = 0; i < probes.Length; i++)
            {
                if (TryResolveSafeShortAdvance(probes[i], out safeRecoveryTarget, 1.0f, 0.5f))
                    return true;
            }

            return false;
        }

        bool TryResolveSafeShortAdvance(Vector3 desired, out Vector3 safeTarget, float sampleRadius, float probeScale)
        {
            safeTarget = transform.position;
            if (!IsAgentReady())
                return false;

            bool stairContext = GetStairBlend() >= 0.24f || IsCurrentlyOnElevatedPath();

            if (!NavMesh.SamplePosition(desired, out var hit, Mathf.Max(0.2f, sampleRadius), NavMesh.AllAreas))
                return false;

            Vector3 candidate = hit.position;
            float desiredVerticalDelta = GetPrimaryVerticalTraversalDelta();
            if (desiredVerticalDelta > 0.18f && candidate.y < transform.position.y - 0.02f)
                return false;
            if (desiredVerticalDelta < -0.18f && candidate.y > transform.position.y + 0.14f)
                return false;

            float verticalDelta = Mathf.Abs(candidate.y - transform.position.y);
            bool elevatedAdvance = stairContext || verticalDelta > 0.18f;
            float maxAdvanceDistance = elevatedAdvance
                ? Mathf.Max(0.9f, stairTraverseProbeDistance * 3.4f)
                : Mathf.Max(0.22f, stairTraverseProbeDistance * 1.35f);
            if (Vector3.Distance(transform.position, candidate) > maxAdvanceDistance)
                return false;
            if (elevatedAdvance)
            {
                // For stair / elevated moves CalculatePath is the authoritative check.
                // IsPathAllowed is intentionally skipped here: its geometry linecasts at
                // 1.0 m height hit overhead floor slabs in stairwells and falsely reject
                // perfectly valid stair paths.
                NavMeshPath shortPath = new NavMeshPath();
                if (!NavMesh.CalculatePath(transform.position, candidate, NavMesh.AllAreas, shortPath) ||
                    shortPath.status != NavMeshPathStatus.PathComplete)
                    return false;
            }
            else
            {
                if (CrossedNavBoundary(transform.position, candidate))
                    return false;
                if (IsSegmentBlocked(transform.position, candidate))
                    return false;
            }
            if (!stairContext && !elevatedAdvance && IsInsideBlockingGeometry(candidate, Mathf.Max(0.05f, antiClipProbeRadius * probeScale)))
                return false;
            if (IsBodyIntersectingBlocking(candidate))
                return false;

            safeTarget = candidate;
            return true;
        }

        void EnforceRuntimeNoClip()
        {
            if (!enforceRuntimeAntiClip || !IsAgentReady()) return;
            if (Time.time < ignoreAntiClipUntilTime)
            {
                // Keep lastSafePosition fresh during the ignore window so that when the
                // window expires the stale floor position is not used as the reference
                // point for CrossedNavBoundary — which would warp the agent back down.
                lastSafePosition = transform.position;
                hasSafePosition  = true;
                return;
            }

            Vector3 current = transform.position;
            if (!hasSafePosition)
            {
                lastSafePosition = current;
                hasSafePosition = true;
                return;
            }

            bool stairTraversal = GetStairBlend() >= 0.24f || IsCurrentlyOnElevatedPath();
            if (stairTraversal)
            {
                // During ANY stair/ramp traversal let the NavMeshAgent move freely.
                // All geometry checks (body intersection, boundary crossing) can produce
                // false positives on stair geometry and teleport the agent back down.
                lastSafePosition = current;
                hasSafePosition  = true;
                return;
            }

            float clipProbeRadius = antiClipProbeRadius;
            bool insideBlocking = IsInsideBlockingGeometry(current, clipProbeRadius);
            bool bodyInsideBlocking = IsBodyIntersectingBlocking(current);
            bool insideHideable = rejectDestinationsInsideHideables && IsInsideHideableCollider(current, destinationHideableClearance);
            bool crossedNavBoundary = enforceNavMeshBoundaryAntiClip &&
                                      CrossedNavBoundary(lastSafePosition, current);
            float moved = Vector3.Distance(lastSafePosition, current);
            bool hardNavViolation = crossedNavBoundary && moved > 0.28f;
            float hardWallMoveThreshold = stairTraversal ? 0.16f : 0.3f;
            hardWallMoveThreshold = Mathf.Min(hardWallMoveThreshold, 0.18f);
            bool hardWallViolation = stairTraversal
                ? bodyInsideBlocking || hardNavViolation
                : bodyInsideBlocking || (insideBlocking && moved > hardWallMoveThreshold);

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

        void DisableVisualBodyColliders()
        {
            Collider[] allColliders = GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < allColliders.Length; i++)
            {
                Collider collider = allColliders[i];
                if (collider == null)
                    continue;

                bool isRootCollider = collider.transform == transform;
                bool keepRootCapsule = isRootCollider && collider is CapsuleCollider;
                bool keepRootTrigger = isRootCollider && collider.isTrigger;
                if (keepRootCapsule || keepRootTrigger)
                    continue;

                collider.enabled = false;
            }
        }

        bool TryFindSafeRecoveryPoint(Vector3 current, Vector3 preferredFallback, out Vector3 safePos)
        {
            safePos = preferredFallback;

            if (NavMesh.SamplePosition(preferredFallback, out var hit, 1.8f, NavMesh.AllAreas) &&
                !IsInsideBlockingGeometry(hit.position, antiClipProbeRadius) &&
                !IsBodyIntersectingBlocking(hit.position))
            {
                safePos = hit.position;
                return true;
            }

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
                // Skip wall checks for stair/elevated segments — the linecast at 1.0m height
                // can falsely hit overhead floor geometry in stairwells and reject valid paths.
                if (Mathf.Abs(corners[i + 1].y - corners[i].y) > 0.18f)
                    continue;

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

            const float inset = 0.08f;
            from += dir * inset;
            to -= dir * inset;
            len = Vector3.Distance(from, to);
            if (len <= 0.01f) return false;

            if (!Physics.Linecast(from, to, out RaycastHit hit, blockingGeometryMask, QueryTriggerInteraction.Ignore))
                return false;

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

            bool elevatedTargetContext = IsElevatedTargetContext(position);
            bool closeChaseEntryContext = IsCloseChaseEntryContext(position);
            float effectiveWallClearance = elevatedTargetContext
                ? Mathf.Max(0.12f, destinationWallClearance * 0.45f)
                : closeChaseEntryContext
                    ? Mathf.Max(0.14f, destinationWallClearance * 0.55f)
                    : destinationWallClearance;
            Vector3 probe = position + Vector3.up * 1.0f;
            int count = Physics.OverlapSphereNonAlloc(
                probe,
                Mathf.Max(0.05f, effectiveWallClearance),
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

        bool IsElevatedTargetContext(Vector3 worldPos)
        {
            return GetStairBlend() >= 0.24f || Mathf.Abs(worldPos.y - transform.position.y) > 0.55f;
        }

        bool IsStairLikePosition(Vector3 worldPos)
        {
            if (!TryGetGroundHeight(worldPos, out float baseHeight, out float baseSlope))
                return false;

            float sampleDistance = Mathf.Max(0.28f, stairProbeForwardDistance * 0.85f);
            Vector3[] sampleOffsets =
            {
                Vector3.forward * sampleDistance,
                Vector3.back * sampleDistance,
                Vector3.right * sampleDistance,
                Vector3.left * sampleDistance
            };

            float strongestStep = 0f;
            int foundSamples = 0;
            for (int i = 0; i < sampleOffsets.Length; i++)
            {
                if (!TryGetGroundHeight(worldPos + sampleOffsets[i], out float sampleHeight, out _))
                    continue;

                strongestStep = Mathf.Max(strongestStep, Mathf.Abs(sampleHeight - baseHeight));
                foundSamples++;
            }

            float stepBlend = Mathf.InverseLerp(0.03f, 0.18f, strongestStep);
            float slopeBlend = Mathf.InverseLerp(7f, 22f, baseSlope);
            if (foundSamples == 0)
                return slopeBlend >= 0.35f;

            return Mathf.Max(stepBlend, slopeBlend * 0.85f) >= 0.24f;
        }

        bool IsStrictStairOccupantPosition(Vector3 worldPos)
        {
            if (!TryGetGroundHeight(worldPos, out float baseHeight, out float baseSlope))
                return false;

            if (baseSlope >= 15f)
                return true;

            float sampleDistance = Mathf.Max(0.14f, stairProbeForwardDistance * 0.32f);
            Vector3[] sampleOffsets =
            {
                Vector3.forward * sampleDistance,
                Vector3.back * sampleDistance,
                Vector3.right * sampleDistance,
                Vector3.left * sampleDistance,
            };

            int steppedSamples = 0;
            float strongestStep = 0f;
            for (int i = 0; i < sampleOffsets.Length; i++)
            {
                if (!TryGetGroundHeight(worldPos + sampleOffsets[i], out float sampleHeight, out _))
                    continue;

                float delta = Mathf.Abs(sampleHeight - baseHeight);
                strongestStep = Mathf.Max(strongestStep, delta);
                if (delta >= 0.045f)
                    steppedSamples++;
            }

            return steppedSamples >= 2 && strongestStep >= 0.06f;
        }

        bool TryGetFirstVerticalPathTransition(NavMeshPath path, out Vector3 transitionStart, out Vector3 transitionEnd)
        {
            transitionStart = Vector3.zero;
            transitionEnd = Vector3.zero;
            if (path == null || path.corners == null || path.corners.Length < 2)
                return false;

            Vector3[] corners = path.corners;
            for (int i = 0; i < corners.Length - 1; i++)
            {
                if (Mathf.Abs(corners[i + 1].y - corners[i].y) <= 0.08f)
                    continue;

                transitionStart = corners[i];
                transitionEnd = corners[i + 1];
                return true;
            }

            return false;
        }

        float GetPathLength(NavMeshPath path)
        {
            if (path == null || path.corners == null || path.corners.Length < 2)
                return 0f;

            float length = 0f;
            Vector3[] corners = path.corners;
            for (int i = 0; i < corners.Length - 1; i++)
                length += Vector3.Distance(corners[i], corners[i + 1]);

            return length;
        }

        public bool IsCloseKillReachable(Vector3 targetPoint, float directDistance)
        {
            if (!NavMesh.SamplePosition(transform.position, out var selfHit, 1.5f, NavMesh.AllAreas))
                return false;
            if (!NavMesh.SamplePosition(targetPoint, out var targetHit, 1.5f, NavMesh.AllAreas))
                return false;

            NavMeshPath closePath = new NavMeshPath();
            if (!NavMesh.CalculatePath(selfHit.position, targetHit.position, NavMesh.AllAreas, closePath) ||
                closePath.status != NavMeshPathStatus.PathComplete)
                return false;

            if (PathCrossesLockedClosedDoor(closePath))
                return false;

            float pathLength = GetPathLength(closePath);
            float direct3D = Mathf.Max(0f, directDistance);
            float directFlat = Vector2.Distance(
                new Vector2(selfHit.position.x, selfHit.position.z),
                new Vector2(targetHit.position.x, targetHit.position.z));
            float verticalDelta = Mathf.Abs(targetHit.position.y - selfHit.position.y);
            bool selfOnStairs = GetStairBlend() >= 0.2f || IsCurrentlyOnElevatedPath() || IsStairLikePosition(selfHit.position);
            bool targetOnStairs = IsStairLikePosition(targetHit.position);
            bool selfStrictStairs = IsStrictStairOccupantPosition(selfHit.position);
            bool targetStrictStairs = IsStrictStairOccupantPosition(targetHit.position);

            if (selfOnStairs != targetOnStairs && verticalDelta > 0.08f)
                return false;
            if (selfStrictStairs != targetStrictStairs && verticalDelta > 0.06f)
                return false;

            if (verticalDelta > 0.08f && TryGetFirstVerticalPathTransition(closePath, out Vector3 transitionStart, out Vector3 transitionEnd))
            {
                float selfToTransition = Vector2.Distance(
                    new Vector2(selfHit.position.x, selfHit.position.z),
                    new Vector2(transitionStart.x, transitionStart.z));
                float targetToTransition = Vector2.Distance(
                    new Vector2(targetHit.position.x, targetHit.position.z),
                    new Vector2(transitionEnd.x, transitionEnd.z));

                if (targetHit.position.y > selfHit.position.y + 0.08f &&
                    selfToTransition > Mathf.Max(0.16f, killDistance * 0.28f))
                    return false;

                if (selfHit.position.y > targetHit.position.y + 0.08f &&
                    targetToTransition > Mathf.Max(0.16f, killDistance * 0.28f))
                    return false;
            }

            float allowedPathLength = Mathf.Max(killDistance + 0.45f, direct3D + 0.55f);
            if (verticalDelta > 0.3f)
                allowedPathLength = Mathf.Max(allowedPathLength, directFlat + 0.75f);

            return pathLength <= allowedPathLength;
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
            var residentCapsule = GetComponent<CapsuleCollider>();
            if (residentCapsule == null) return false;
            bool elevatedTargetContext = IsElevatedTargetContext(worldPos);
            bool closeChaseEntryContext = IsCloseChaseEntryContext(worldPos);
            float penetrationThreshold = elevatedTargetContext
                ? Mathf.Max(0.12f, antiClipPenetrationEpsilon * 2.5f)
                : closeChaseEntryContext
                    ? Mathf.Max(0.04f, antiClipPenetrationEpsilon * 1.4f)
                    : Mathf.Max(0.001f, antiClipPenetrationEpsilon);

            float radius = 0.35f;
            float height = 2.0f;
            if (residentCapsule != null)
            {
                float xzScale = Mathf.Max(0.01f, Mathf.Max(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.z)));
                float yScale = Mathf.Max(0.01f, Mathf.Abs(transform.lossyScale.y));
                float radiusScale = elevatedTargetContext ? 0.76f : closeChaseEntryContext ? 0.88f : 0.92f;
                radius = Mathf.Max(0.12f, residentCapsule.radius * xzScale * radiusScale);
                height = Mathf.Max(radius * 2f + 0.01f, residentCapsule.height * yScale * 0.98f);
            }
            else if (agent != null)
            {
                float radiusScale = elevatedTargetContext ? 0.76f : closeChaseEntryContext ? 0.88f : 0.92f;
                radius = Mathf.Max(0.12f, agent.radius * radiusScale);
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

                if (Physics.ComputePenetration(
                    residentCapsule, worldPos, transform.rotation,
                    c, c.transform.position, c.transform.rotation,
                    out _, out float distance))
                {
                    if (distance >= penetrationThreshold)
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

            if (t.GetComponentInParent<HideableObject>() != null) return true;
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
    }
}
