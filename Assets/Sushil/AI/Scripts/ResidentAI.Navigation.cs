using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;

namespace Sushil.AI
{
    public partial class ResidentAI
    {
        NavMeshLink squareFuseCorridorNavLink;
        Coroutine stairDestinationReissueRoutine;

        // Adds a NavMeshLink to bridge the square-fuse corridor gap.
        // Do not rebuild the full NavMesh here: NewLevel contains interactive doors,
        // and rebuilding at scene start captures them in the closed state which can
        // disconnect room interiors from hallways.
        // Call this from Start() when running in the Sahil/Test/NewLevel scene.
        public void EnsureSquareFuseCorridorNavLink()
        {
            if (agent == null) return;

            // Add a NavMeshLink as a belt-and-suspenders backup for any
            // remaining gap between the approach-side and room-side NavMesh islands.
            if (squareFuseCorridorNavLink != null) return;

            if (!TrySampleSquareFuseBridgePoint(0, out var approachPoint))
            {
                Debug.LogWarning("[ResidentAI] SquareFuse NavLink: no NavMesh near approach anchor.");
                return;
            }
            if (!TrySampleSquareFuseBridgePoint(2, out var roomPoint))
            {
                Debug.LogWarning("[ResidentAI] SquareFuse NavLink: no NavMesh near room anchor.");
                return;
            }

            GameObject linkGO = new GameObject("SquareFuseCorridorNavLink");
            Vector3 center = (approachPoint + roomPoint) * 0.5f;
            linkGO.transform.position = center;

            squareFuseCorridorNavLink = linkGO.AddComponent<NavMeshLink>();
            squareFuseCorridorNavLink.startPoint = approachPoint - center;
            squareFuseCorridorNavLink.endPoint   = roomPoint - center;
            squareFuseCorridorNavLink.width          = 1.35f;
            squareFuseCorridorNavLink.bidirectional  = true;
            squareFuseCorridorNavLink.autoUpdate     = true;
            squareFuseCorridorNavLink.activated      = true;
            squareFuseCorridorNavLink.costModifier   = -1f;
            squareFuseCorridorNavLink.agentTypeID    = agent.agentTypeID;
            Debug.Log($"[ResidentAI] SquareFuse runtime link active: {approachPoint} -> {roomPoint} (width {squareFuseCorridorNavLink.width:F2}).");
        }

        bool UpdateRoamStallState(ref Vector3 lastPosition, ref float stallTimer)
        {
            if (!IsAgentReady())
                return true;

            float moved = Vector3.Distance(transform.position, lastPosition);
            bool stalled = !agent.pathPending &&
                           (!agent.hasPath || (moved < 0.03f && agent.velocity.sqrMagnitude < 0.08f * 0.08f));
            stallTimer = stalled ? stallTimer + Time.deltaTime : 0f;
            lastPosition = transform.position;

            float threshold = Mathf.Max(0.25f, roamStuckRepathSeconds);
            if (stallTimer < threshold)
                return false;

            if (TryResolveDoorThresholdStall())
            {
                stallTimer = 0f;
                lastPosition = transform.position;
                return false;
            }

            return true;
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

            if (IsSquareFuseTraverseContext(fallbackDestination) && !HasSquareFuseRuntimeBridge())
            {
                if (TrySampleSquareFuseBridgePoint(2, out var interiorPoint) && agent.SetDestination(interiorPoint))
                    return;
                if (TrySampleSquareFuseBridgePoint(1, out var doorwayPoint) && agent.SetDestination(doorwayPoint))
                    return;
            }

            if (NavMesh.SamplePosition(fallbackDestination, out var sampledFallback, Mathf.Max(destinationSampleRadius, 2.8f), NavMesh.AllAreas))
            {
                agent.SetDestination(sampledFallback.position);
                return;
            }

            agent.ResetPath();
        }

        void QueueShortAdvanceAndReissueDestination(Vector3 shortAdvanceTarget, Vector3 primaryDestination, float delay = 0.18f)
        {
            if (!IsAgentReady())
                return;

            if (!TrySetDestination(shortAdvanceTarget))
                return;

            if (stairDestinationReissueRoutine != null)
                StopCoroutine(stairDestinationReissueRoutine);

            stairDestinationReissueRoutine = StartCoroutine(ReissueDestinationAfterDelay(primaryDestination, delay));
        }

        IEnumerator ReissueDestinationAfterDelay(Vector3 primaryDestination, float delay)
        {
            float remaining = Mathf.Max(0.05f, delay);
            while (remaining > 0f)
            {
                if (!IsAgentReady())
                {
                    stairDestinationReissueRoutine = null;
                    yield break;
                }

                remaining -= Time.deltaTime;
                yield return null;
            }

            stairDestinationReissueRoutine = null;
            ReissuePrimaryDestination(primaryDestination);
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

        bool IsTightPassageContext(Vector3 worldPos)
        {
            if (IsSquareFuseTightNavigationZone(worldPos) || IsSquareFuseTightNavigationZone(transform.position))
                return true;
            if (IsOpenDoorwayContext(worldPos) || IsOpenDoorwayContext(transform.position))
                return true;

            Vector3 travelDir = worldPos - transform.position;
            travelDir.y = 0f;
            if (travelDir.sqrMagnitude < 0.04f && !TryGetTravelDirection(out travelDir))
                travelDir = transform.forward;
            travelDir.y = 0f;
            if (travelDir.sqrMagnitude < 0.001f)
                travelDir = Vector3.forward;
            travelDir.Normalize();

            Vector3 right = Vector3.Cross(Vector3.up, travelDir);
            right.y = 0f;
            if (right.sqrMagnitude < 0.001f)
                right = transform.right;
            right.y = 0f;
            if (right.sqrMagnitude < 0.001f)
                right = Vector3.right;
            right.Normalize();

            Vector3[] samples =
            {
                transform.position,
                Vector3.Lerp(transform.position, worldPos, 0.5f),
                worldPos
            };

            float sideProbeDistance = Mathf.Max(0.42f, enforcedAgentRadius + 0.18f);
            float ceilingProbeDistance = Mathf.Max(0.42f, enforcedAgentHeight - 1.2f);
            for (int i = 0; i < samples.Length; i++)
            {
                Vector3 origin = samples[i] + Vector3.up * 0.9f;
                bool leftBlocked = Physics.Raycast(origin, -right, out RaycastHit leftHit, sideProbeDistance, blockingGeometryMask, QueryTriggerInteraction.Ignore) &&
                                   IsHardBlockingCollider(leftHit.collider);
                bool rightBlocked = Physics.Raycast(origin, right, out RaycastHit rightHit, sideProbeDistance, blockingGeometryMask, QueryTriggerInteraction.Ignore) &&
                                    IsHardBlockingCollider(rightHit.collider);
                bool lowCeiling = Physics.Raycast(origin - Vector3.up * 0.25f, Vector3.up, out RaycastHit ceilingHit, ceilingProbeDistance, blockingGeometryMask, QueryTriggerInteraction.Ignore) &&
                                  IsHardBlockingCollider(ceilingHit.collider);

                if ((leftBlocked && rightBlocked) || (lowCeiling && (leftBlocked || rightBlocked)))
                    return true;
            }

            return false;
        }

        bool IsSquareFuseCorridorZone(Vector3 worldPos)
        {
            return worldPos.x >= 3.75f && worldPos.x <= 6.75f &&
                   worldPos.y >= -0.25f && worldPos.y <= 2.85f &&
                   worldPos.z >= -22.85f && worldPos.z <= -18.05f;
        }

        bool IsSquareFuseApproachZone(Vector3 worldPos)
        {
            return worldPos.x >= 3.0f && worldPos.x <= 7.2f &&
                   worldPos.y >= -0.25f && worldPos.y <= 2.95f &&
                   worldPos.z >= -22.95f && worldPos.z <= -16.2f;
        }

        bool IsSquareFuseRoomZone(Vector3 worldPos)
        {
            return worldPos.x >= 4.35f && worldPos.x <= 10.95f &&
                   worldPos.y >= -0.25f && worldPos.y <= 2.35f &&
                   worldPos.z >= -23.6f && worldPos.z <= -19.15f;
        }

        bool IsSquareFuseTightNavigationZone(Vector3 worldPos)
        {
            return IsSquareFuseCorridorZone(worldPos) || IsSquareFuseRoomZone(worldPos);
        }

        bool IsSquareFuseTraverseContext(Vector3 desired)
        {
            return IsSquareFuseApproachZone(transform.position) ||
                   IsSquareFuseApproachZone(desired) ||
                   IsSquareFuseRoomZone(transform.position) ||
                   IsSquareFuseRoomZone(desired);
        }

        bool TryGetSquareFusePortal(out Vector3 center, out Vector3 roomNormal)
        {
            center = transform.position;
            roomNormal = Vector3.zero;
            if (!TrySampleSquareFuseBridgePoint(0, out var hallPoint) ||
                !TrySampleSquareFuseBridgePoint(2, out var roomPoint))
                return false;

            Vector3 across = roomPoint - hallPoint;
            across.y = 0f;
            if (across.sqrMagnitude < 0.01f)
                return false;

            roomNormal = across.normalized;
            center = Vector3.Lerp(hallPoint, roomPoint, 0.5f);
            center.y = Mathf.Min(hallPoint.y, roomPoint.y);
            return true;
        }

        float GetSquareFusePortalSignedDistance(Vector3 worldPos)
        {
            if (!TryGetSquareFusePortal(out var portalCenter, out var portalNormal))
                return 0f;

            Vector3 offset = worldPos - portalCenter;
            offset.y = 0f;
            return Vector3.Dot(offset, portalNormal);
        }

        bool IsSquareFuseInteriorSide(Vector3 worldPos, float tolerance = 0.08f)
        {
            if (!IsSquareFuseTraverseContext(worldPos) && !IsSquareFuseRoomZone(worldPos))
                return false;

            return GetSquareFusePortalSignedDistance(worldPos) >= -Mathf.Abs(tolerance);
        }

        bool IsSquareFusePortalSeparated(Vector3 a, Vector3 b, float threshold = 0.14f)
        {
            if (!TryGetSquareFusePortal(out _, out _))
                return false;

            float sideA = GetSquareFusePortalSignedDistance(a);
            float sideB = GetSquareFusePortalSignedDistance(b);
            return sideA >= threshold && sideB <= -threshold;
        }

        bool IsSquareFusePortalContext(Vector3 worldPos)
        {
            return IsSquareFuseTraverseContext(worldPos) || IsSquareFuseRoomZone(worldPos);
        }

        bool IsSquareFuseDoorwayThresholdZone(Vector3 worldPos, float portalDepth = 1.15f)
        {
            if (!IsSquareFusePortalContext(worldPos))
                return false;

            if (!TryGetSquareFusePortal(out _, out _))
                return IsSquareFuseCorridorZone(worldPos);

            return Mathf.Abs(GetSquareFusePortalSignedDistance(worldPos)) <= Mathf.Abs(portalDepth);
        }

        bool IsSquareFusePortalTraversalActive(Vector3 target)
        {
            if (!HasSquareFuseRuntimeBridge())
                return false;

            bool currentRelevant = IsSquareFusePortalContext(transform.position);
            bool targetRelevant = IsSquareFusePortalContext(target);
            if (!currentRelevant && !targetRelevant)
                return false;

            bool currentInside = IsSquareFuseInteriorSide(transform.position, 0.12f) || IsSquareFuseRoomZone(transform.position);
            bool targetInside = IsSquareFuseInteriorSide(target, 0.12f) || IsSquareFuseRoomZone(target);
            if (currentInside != targetInside)
                return true;

            return IsSquareFuseDoorwayThresholdZone(transform.position) ||
                   IsSquareFuseDoorwayThresholdZone(target);
        }

        bool HasSquareFuseRuntimeBridge()
        {
            return squareFuseCorridorNavLink != null &&
                   squareFuseCorridorNavLink.isActiveAndEnabled &&
                   squareFuseCorridorNavLink.activated;
        }

        float GetSquareFuseBridgeWarpDistance(Vector3 desired)
        {
            const float shortWarpDistance = 1.45f;
            const float longWarpDistance = 2.85f;

            if (!IsSquareFuseTraverseContext(desired) || !TryGetSquareFusePortal(out _, out _))
                return shortWarpDistance;

            float residentSide = GetSquareFusePortalSignedDistance(transform.position);
            float desiredSide = GetSquareFusePortalSignedDistance(desired);
            bool crossingPortal =
                (residentSide <= 0.08f && desiredSide >= 0.08f) ||
                (residentSide >= -0.08f && desiredSide <= -0.08f);

            if (crossingPortal)
                return longWarpDistance;

            if (IsSquareFuseRoomZone(desired) && !IsSquareFuseInteriorSide(transform.position, 0.12f))
                return longWarpDistance;

            return shortWarpDistance;
        }

        public bool IsSquareFuseKillBlocked(Vector3 residentWorldPos, Vector3 targetWorldPos)
        {
            bool playerOrTargetInRoom = IsPlayerOccupyingSquareFuseRoom() ||
                                        IsSquareFuseRoomZone(targetWorldPos) ||
                                        IsSquareFuseInteriorSide(targetWorldPos, 0.14f);
            if (!playerOrTargetInRoom)
                return false;

            if (!IsSquareFusePortalContext(residentWorldPos) &&
                !IsSquareFusePortalContext(targetWorldPos))
                return false;

            if (IsSquareFusePortalSeparated(targetWorldPos, residentWorldPos, 0.08f))
                return true;

            const float residentKillEntryDepth = 1.6f;
            float residentDepth = GetSquareFusePortalSignedDistance(residentWorldPos);
            float targetDepth = GetSquareFusePortalSignedDistance(targetWorldPos);
            return targetDepth >= -0.05f && residentDepth < residentKillEntryDepth;
        }

        bool TryGetSquareFuseBridgeAnchor(int index, out Vector3 anchor)
        {
            switch (index)
            {
                case 0:
                    anchor = new Vector3(5.465f, 0.729f, -18.833f);
                    return true;
                case 1:
                    // Use a walkable doorway center, not the FuseBox wall position.
                    anchor = new Vector3(6.180f, 0.700f, -20.020f);
                    return true;
                case 2:
                    anchor = new Vector3(6.950f, 0.625f, -21.100f);
                    return true;
                case 3:
                    anchor = new Vector3(8.379f, 0.625f, -21.165f);
                    return true;
                default:
                    anchor = transform.position;
                    return false;
            }
        }

        bool TrySampleSquareFuseBridgePoint(int index, out Vector3 point)
        {
            point = transform.position;
            if (!TryGetSquareFuseBridgeAnchor(index, out var anchor))
                return false;

            float sampleRadius = index == 1 ? 0.9f : 1.35f;
            if (NavMesh.SamplePosition(anchor, out var hit, sampleRadius, NavMesh.AllAreas))
            {
                point = hit.position;
                return true;
            }

            anchor.y = transform.position.y;
            if (NavMesh.SamplePosition(anchor, out hit, sampleRadius + 0.55f, NavMesh.AllAreas))
            {
                point = hit.position;
                return true;
            }

            return false;
        }

        int GetClosestSquareFuseBridgeIndex(Vector3 worldPos)
        {
            float bestSqr = float.MaxValue;
            int bestIndex = 0;

            for (int i = 0; i < 4; i++)
            {
                if (!TrySampleSquareFuseBridgePoint(i, out var point))
                    continue;

                float sqr = (point - worldPos).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    bestIndex = i;
                }
            }

            return bestIndex;
        }

        int GetDesiredSquareFuseBridgeIndex(Vector3 desired)
        {
            if (IsSquareFuseRoomZone(desired))
                return 2;

            return GetClosestSquareFuseBridgeIndex(desired);
        }

        bool TryResolveSquareFuseCorridorBridgeDestination(Vector3 desired, out Vector3 resolved, out bool preferWarp)
        {
            resolved = desired;
            preferWarp = false;
            if (!IsSquareFuseTraverseContext(desired) || agent == null)
                return false;
            if (HasSquareFuseRuntimeBridge())
                return false;

            NavMeshPath directPath = new NavMeshPath();
            bool hasDirectPath =
                NavMesh.CalculatePath(transform.position, desired, NavMesh.AllAreas, directPath) &&
                directPath.status != NavMeshPathStatus.PathInvalid;
            if (hasDirectPath && directPath.status == NavMeshPathStatus.PathComplete)
                return false;

            bool residentInside = IsSquareFuseInteriorSide(transform.position, 0.12f) || IsSquareFuseRoomZone(transform.position);
            bool desiredInside = IsSquareFuseInteriorSide(desired, 0.12f) || IsSquareFuseRoomZone(desired);
            if (residentInside == desiredInside &&
                !IsSquareFusePortalSeparated(desired, transform.position, 0.08f))
                return false;

            float warpDistanceLimit = GetSquareFuseBridgeWarpDistance(desired);

            int currentIndex = GetClosestSquareFuseBridgeIndex(transform.position);
            int desiredIndex = GetDesiredSquareFuseBridgeIndex(desired);
            if (currentIndex == desiredIndex)
            {
                if (!TrySampleSquareFuseBridgePoint(desiredIndex, out var samePoint))
                    return false;
                float distToSame = Vector3.Distance(transform.position, samePoint);
                if (distToSame <= 0.3f)
                    return false;

                NavMeshPath samePath = new NavMeshPath();
                bool hasSamePath =
                    NavMesh.CalculatePath(transform.position, samePoint, NavMesh.AllAreas, samePath) &&
                    samePath.status != NavMeshPathStatus.PathInvalid;

                resolved = samePoint;
                preferWarp = distToSame <= warpDistanceLimit &&
                             (!hasSamePath || samePath.status != NavMeshPathStatus.PathComplete);
                return true;
            }

            int step = desiredIndex > currentIndex ? 1 : -1;

            // If the resident hasn't reached the current anchor yet, navigate or warp to it first.
            // This fixes the case where the corridor entrance is a NavMesh gap (baked at radius 0.5
            // but only ~1m wide): the closest anchor is #0 but the path to it is broken, while the
            // next anchor (#1) is 2.48m away — too far for warp — so the resident was permanently stuck.
            if (TrySampleSquareFuseBridgePoint(currentIndex, out var currentAnchorPoint))
            {
                float distToCurrent = Vector3.Distance(transform.position, currentAnchorPoint);
                if (distToCurrent > 0.3f)
                {
                    NavMeshPath toCurrentPath = new NavMeshPath();
                    bool hasToCurrentPath =
                        NavMesh.CalculatePath(transform.position, currentAnchorPoint, NavMesh.AllAreas, toCurrentPath) &&
                        toCurrentPath.status != NavMeshPathStatus.PathInvalid;

                    if (hasToCurrentPath && toCurrentPath.status == NavMeshPathStatus.PathComplete)
                    {
                        resolved = currentAnchorPoint;
                        return true;
                    }

                    if (distToCurrent <= warpDistanceLimit &&
                        (!hasToCurrentPath || toCurrentPath.status != NavMeshPathStatus.PathComplete))
                    {
                        resolved = currentAnchorPoint;
                        preferWarp = true;
                        return true;
                    }
                }
            }

            int immediateIndex = currentIndex + step;
            if (immediateIndex >= 0 &&
                immediateIndex < 4 &&
                TrySampleSquareFuseBridgePoint(immediateIndex, out var immediatePoint))
            {
                float distToImmediate = Vector3.Distance(transform.position, immediatePoint);
                if (distToImmediate > 0.3f)
                {
                    NavMeshPath immediatePath = new NavMeshPath();
                    bool hasImmediatePath =
                        NavMesh.CalculatePath(transform.position, immediatePoint, NavMesh.AllAreas, immediatePath) &&
                        immediatePath.status != NavMeshPathStatus.PathInvalid;
                    if (hasImmediatePath && immediatePath.status == NavMeshPathStatus.PathComplete)
                    {
                        resolved = immediatePoint;
                        return true;
                    }

                    if (distToImmediate <= warpDistanceLimit &&
                        (!hasImmediatePath || immediatePath.status != NavMeshPathStatus.PathComplete))
                    {
                        resolved = immediatePoint;
                        preferWarp = true;
                        return true;
                    }
                }
            }

            NavMeshPath bridgePath = new NavMeshPath();
            for (int i = currentIndex + step; step > 0 ? i <= desiredIndex : i >= desiredIndex; i += step)
            {
                if (!TrySampleSquareFuseBridgePoint(i, out var candidate))
                    continue;
                float distToCandidate = Vector3.Distance(transform.position, candidate);
                if (distToCandidate <= 0.3f)
                    continue;

                bool hasBridgePath =
                    NavMesh.CalculatePath(transform.position, candidate, NavMesh.AllAreas, bridgePath) &&
                    bridgePath.status != NavMeshPathStatus.PathInvalid;
                if (!hasBridgePath)
                {
                    if (distToCandidate <= warpDistanceLimit)
                    {
                        resolved = candidate;
                        preferWarp = true;
                        return true;
                    }

                    continue;
                }

                resolved = candidate;
                preferWarp = bridgePath.status != NavMeshPathStatus.PathComplete &&
                             distToCandidate <= warpDistanceLimit;
                return true;
            }

            return false;
        }

        void TryCompleteSquareFuseOffMeshLink()
        {
            if (!IsAgentReady() || !agent.isOnOffMeshLink)
                return;

            OffMeshLinkData linkData = agent.currentOffMeshLinkData;
            Vector3 start = linkData.startPos;
            Vector3 end = linkData.endPos;
            if (!IsSquareFusePortalContext(start) && !IsSquareFusePortalContext(end))
                return;

            Vector3 intendedDestination = agent.hasPath ? agent.destination : transform.position;
            if (state == State.Chase && player != null)
                intendedDestination = player.position;

            if (!NavMesh.SamplePosition(end, out var endHit, 1.0f, NavMesh.AllAreas))
                return;

            agent.Warp(endHit.position);
            transform.position = endHit.position;
            agent.nextPosition = endHit.position;
            lastSafePosition = endHit.position;
            hasSafePosition = true;
            ignoreAntiClipUntilTime = Time.time + 0.35f;
            agent.CompleteOffMeshLink();
            ReissuePrimaryDestination(intendedDestination);
        }

        bool TryUseSquareFuseCorridorBridge(Vector3 desired, bool allowWarp)
        {
            if (!IsAgentReady())
                return false;

            if (!TryResolveSquareFuseCorridorBridgeDestination(desired, out var bridgeDestination, out bool preferWarp))
                return false;

            if (preferWarp)
            {
                if (IsSquareFuseTraverseContext(desired))
                    return agent.SetDestination(bridgeDestination);

                if (!allowWarp)
                    return false;

                if (!NavMesh.SamplePosition(bridgeDestination, out var warpHit, 0.9f, NavMesh.AllAreas))
                    return false;

                agent.Warp(warpHit.position);
                transform.position = warpHit.position;
                agent.nextPosition = warpHit.position;
                lastSafePosition = warpHit.position;
                hasSafePosition = true;
                ignoreAntiClipUntilTime = Time.time + 0.35f;
                if (TrySetDestination(desired))
                    return true;

                if (IsSquareFuseTraverseContext(desired))
                {
                    if (TrySampleSquareFuseBridgePoint(2, out var interiorPoint) && agent.SetDestination(interiorPoint))
                        return true;
                    if (TrySampleSquareFuseBridgePoint(1, out var doorwayPoint) && agent.SetDestination(doorwayPoint))
                        return true;
                }

                if (NavMesh.SamplePosition(desired, out var sampledDesired, Mathf.Max(destinationSampleRadius, 2.8f), NavMesh.AllAreas))
                    return agent.SetDestination(sampledDesired.position);

                agent.ResetPath();
                return false;
            }

            return agent.SetDestination(bridgeDestination);
        }

        bool TryResolveSquareFusePortalTraversalDestination(Vector3 desired, out Vector3 resolved)
        {
            resolved = desired;
            if (!HasSquareFuseRuntimeBridge())
                return false;
            if (!IsSquareFusePortalContext(transform.position) && !IsSquareFusePortalContext(desired))
                return false;
            if (!TryGetSquareFusePortal(out var center, out var forward))
                return false;

            Vector3 right = Vector3.Cross(Vector3.up, forward);
            right.y = 0f;
            if (right.sqrMagnitude < 0.001f)
                right = Vector3.right;
            right.Normalize();

            float[] depths = { 0.45f, 0.9f, 1.35f, 1.8f, 2.25f };
            float[] widths = { 0f, 0.3f, -0.3f, 0.55f, -0.55f, 0.85f, -0.85f };
            int preferredSide = GetPreferredDoorTraversalSide(center, forward, desired);
            NavMeshPath path = new NavMeshPath();

            bool found = false;
            float bestScore = float.MaxValue;
            Vector3 bestPoint = desired;

            for (int side = -1; side <= 1; side += 2)
            {
                for (int d = 0; d < depths.Length; d++)
                {
                    for (int w = 0; w < widths.Length; w++)
                    {
                        Vector3 probe = center + forward * (depths[d] * side) + right * widths[w];
                        float sampleRadius = 0.7f + (depths[d] * 0.1f);
                        if (!NavMesh.SamplePosition(probe, out var hit, sampleRadius, NavMesh.AllAreas))
                            continue;

                        Vector3 hitOffset = hit.position - center;
                        hitOffset.y = 0f;
                        if (Vector3.Dot(hitOffset, forward) * side < 0.12f)
                            continue;
                        if (!IsDestinationAllowed(hit.position) || IsBodyIntersectingBlocking(hit.position))
                            continue;
                        if (!NavMesh.CalculatePath(transform.position, hit.position, NavMesh.AllAreas, path) ||
                            path.status != NavMeshPathStatus.PathComplete)
                            continue;
                        if (!IsPathAllowed(path))
                            continue;

                        float pathLength = GetPathLength(path);
                        float score = Vector3.SqrMagnitude(hit.position - desired) +
                                      (Mathf.Abs(widths[w]) * 0.2f) +
                                      (pathLength * 0.08f) -
                                      (depths[d] * 0.22f);
                        if (side != preferredSide)
                            score += 1.8f;

                        if (score < bestScore)
                        {
                            bestScore = score;
                            bestPoint = hit.position;
                            found = true;
                        }
                    }
                }
            }

            if (!found)
                return false;

            resolved = bestPoint;
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

            // Doorway nudges help with flat room transitions, but on stairs they can
            // fight the normal chase path and repeatedly push the resident into the
            // landing/railing edge case instead of letting the NavMesh settle.
            bool stairOrElevatedTraversal =
                IsInStairTraversalContext() ||
                IsCurrentlyOnElevatedPath() ||
                Mathf.Abs(assistTarget.y - transform.position.y) > 0.35f;
            if (stairOrElevatedTraversal)
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
            bool nearOpenDoor = TryGetNearbyOpenDoor(out var openDoor, 4.5f, assistTarget, preferUnlockedKeyDoors: true);
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

            if (!assisted && TryUseSquareFuseCorridorBridge(assistTarget, allowWarp: false))
                return;
        }

        bool TryResolveDoorThresholdStall()
        {
            if (!IsAgentReady() || agent.pathPending || !agent.hasPath)
                return false;

            if (agent.remainingDistance <= Mathf.Max(0.4f, agent.stoppingDistance + 0.15f))
                return false;

            Vector3 targetPos = agent.destination;
            float doorRange = Mathf.Max(2.6f, unlockedKeyDoorTraverseRange);
            bool hasOpenDoor = TryGetNearbyOpenDoor(out var openDoor, doorRange, targetPos, preferUnlockedKeyDoors: true);
            if (hasOpenDoor)
            {
                if (TryAdvanceThroughOpenDoor(openDoor, targetPos))
                    return true;

                if (doorwayWarpAssist && TryWarpThroughOpenDoor(openDoor, targetPos))
                    return true;
            }

            if (TryUseSquareFuseCorridorBridge(targetPos, allowWarp: false))
                return true;

            if (TryResolvePartialPathAdvance(targetPos, out var partialAdvance))
                return TrySetDestination(partialAdvance);

            if (TryResolveProgressiveDestination(targetPos, out var progressiveAdvance))
                return TrySetDestination(progressiveAdvance);

            return false;
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

        bool TryGetOpenDoorwayContext(Vector3 worldPos, out Door door)
        {
            door = null;
            if (cachedDoors == null || cachedDoors.Length == 0)
                cachedDoors = FindObjectsByType<Door>(FindObjectsSortMode.None);
            if (cachedDoors == null || cachedDoors.Length == 0)
                return false;

            for (int i = 0; i < cachedDoors.Length; i++)
            {
                Door candidate = cachedDoors[i];
                if (candidate == null || !candidate.gameObject.activeInHierarchy || !candidate.IsOpen)
                    continue;

                Vector3 center = candidate.GetDoorwayCenter();
                if (Mathf.Abs(worldPos.y - center.y) > 2.4f)
                    continue;

                Vector3 forward = candidate.GetDoorwayForward();
                Vector3 right = candidate.GetDoorwayRight();
                if (forward.sqrMagnitude < 0.001f || right.sqrMagnitude < 0.001f)
                    continue;

                Vector3 offset = worldPos - center;
                offset.y = 0f;

                float forwardDistance = Mathf.Abs(Vector3.Dot(offset, forward));
                float rightDistance = Mathf.Abs(Vector3.Dot(offset, right));
                float depthAllowance = Mathf.Max(0.95f, candidate.navLinkDepth + 0.55f);
                float widthAllowance = Mathf.Max(0.55f, (candidate.navLinkWidth * 0.5f) + 0.18f);

                if (forwardDistance > depthAllowance || rightDistance > widthAllowance)
                    continue;

                door = candidate;
                return true;
            }

            return false;
        }

        bool IsOpenDoorwayContext(Vector3 worldPos)
        {
            return TryGetOpenDoorwayContext(worldPos, out _);
        }

        bool TryGetNearbyOpenDoor(out Door door, float range, Vector3 towardTarget, bool preferUnlockedKeyDoors)
        {
            door = null;
            if (cachedDoors == null || cachedDoors.Length == 0)
                cachedDoors = FindObjectsByType<Door>(FindObjectsSortMode.None);
            if (cachedDoors == null || cachedDoors.Length == 0)
                return false;

            float bestScore = float.MaxValue;
            float rangeSqr = Mathf.Max(0.6f, range) * Mathf.Max(0.6f, range);
            Vector3 p = transform.position;
            p.y = 0f;
            Vector3 targetFlat = towardTarget;
            targetFlat.y = 0f;
            Vector3 towardDir = targetFlat - p;
            bool hasTargetDir = towardDir.sqrMagnitude > 0.01f;
            if (hasTargetDir)
                towardDir.Normalize();

            for (int i = 0; i < cachedDoors.Length; i++)
            {
                Door d = cachedDoors[i];
                if (d == null || !d.gameObject.activeInHierarchy) continue;
                if (!d.IsOpen) continue;

                Vector3 dpos = d.GetDoorwayCenter();
                dpos.y = 0f;
                float sqr = (dpos - p).sqrMagnitude;
                if (sqr > rangeSqr) continue;

                float score = sqr;
                if (preferUnlockedKeyDoors && d.WasUnlockedByKey)
                    score -= 4f;

                if (hasTargetDir)
                {
                    Vector3 toDoor = dpos - p;
                    if (toDoor.sqrMagnitude > 0.001f)
                    {
                        float alignmentPenalty = 1f - Mathf.Clamp01(Vector3.Dot(toDoor.normalized, towardDir));
                        score += alignmentPenalty * 5f;
                    }

                    score += Vector3.Distance(dpos, targetFlat) * 0.12f;
                }

                if (score < bestScore)
                {
                    bestScore = score;
                    door = d;
                }
            }

            return door != null;
        }

        float GetFlatDistanceToSegment(Vector3 point, Vector3 a, Vector3 b)
        {
            point.y = 0f;
            a.y = 0f;
            b.y = 0f;

            Vector3 ab = b - a;
            float denom = ab.sqrMagnitude;
            if (denom <= 0.0001f)
                return Vector3.Distance(point, a);

            float t = Mathf.Clamp01(Vector3.Dot(point - a, ab) / denom);
            Vector3 closest = a + (ab * t);
            return Vector3.Distance(point, closest);
        }

        int GetPreferredDoorTraversalSide(Vector3 center, Vector3 forward, Vector3 desired)
        {
            Vector3 desiredOffset = desired - center;
            desiredOffset.y = 0f;
            float desiredDot = Vector3.Dot(desiredOffset, forward);
            if (Mathf.Abs(desiredDot) > 0.2f)
                return desiredDot >= 0f ? 1 : -1;

            Vector3 selfOffset = transform.position - center;
            selfOffset.y = 0f;
            float selfDot = Vector3.Dot(selfOffset, forward);
            if (Mathf.Abs(selfDot) > 0.2f)
                return selfDot >= 0f ? -1 : 1;

            return 1;
        }

        bool TryGetBestDoorTraversalPoint(Door door, Vector3 desired, bool favorDesiredSide, bool requireCompletePath, out Vector3 point, out float traversalScore)
        {
            point = desired;
            traversalScore = 0f;
            if (door == null)
                return false;

            Vector3 center = door.GetDoorwayCenter();
            Vector3 forward = door.GetDoorwayForward();
            Vector3 right = door.GetDoorwayRight();
            if (forward.sqrMagnitude < 0.001f || right.sqrMagnitude < 0.001f)
                return false;

            float shallowDepth = Mathf.Clamp(unlockedKeyDoorTraverseRange * 0.42f, 0.8f, 1.45f);
            float mediumDepth = Mathf.Clamp(unlockedKeyDoorTraverseRange * 0.68f, shallowDepth + 0.2f, 2.15f);
            float deepDepth = Mathf.Clamp(unlockedKeyDoorTraverseRange, mediumDepth + 0.25f, 3.35f);
            float[] depths = { shallowDepth, mediumDepth, deepDepth };
            float[] widths = { 0f, 0.38f, -0.38f, 0.7f, -0.7f, 1.0f, -1.0f };

            int preferredSide = GetPreferredDoorTraversalSide(center, forward, desired);
            NavMeshPath path = requireCompletePath ? new NavMeshPath() : null;

            bool found = false;
            float bestScore = float.MaxValue;
            Vector3 bestPoint = center;
            float bestTraversalScore = 0f;

            for (int side = -1; side <= 1; side += 2)
            {
                float sideOpenScore = 0f;
                bool sideFound = false;
                float sideBestScore = float.MaxValue;
                Vector3 sideBestPoint = center;

                for (int d = 0; d < depths.Length; d++)
                {
                    for (int w = 0; w < widths.Length; w++)
                    {
                        Vector3 probe = center + forward * (depths[d] * side) + right * widths[w];
                        float sampleRadius = 0.9f + (depths[d] * 0.1f);
                        if (!NavMesh.SamplePosition(probe, out var hit, sampleRadius, NavMesh.AllAreas))
                            continue;

                        Vector3 hitOffset = hit.position - center;
                        hitOffset.y = 0f;
                        if (Vector3.Dot(hitOffset, forward) * side < 0.18f)
                            continue;
                        if (!IsDestinationAllowed(hit.position) || IsBodyIntersectingBlocking(hit.position))
                            continue;

                        float pathLength = 0f;
                        if (requireCompletePath)
                        {
                            if (!NavMesh.CalculatePath(transform.position, hit.position, NavMesh.AllAreas, path) ||
                                path.status != NavMeshPathStatus.PathComplete)
                                continue;
                            if (!IsPathAllowed(path))
                                continue;

                            pathLength = GetPathLength(path);
                        }

                        float openness = Mathf.Max(0.15f, 1f + (depths[d] * 0.9f) - (Mathf.Abs(widths[w]) * 0.18f));
                        sideOpenScore += openness;

                        float score = Vector3.SqrMagnitude(hit.position - desired) -
                                      (depths[d] * 0.55f) +
                                      (Mathf.Abs(widths[w]) * 0.24f) +
                                      (pathLength * 0.08f);
                        if (favorDesiredSide && side != preferredSide)
                            score += 2.4f;

                        if (score < sideBestScore)
                        {
                            sideBestScore = score;
                            sideBestPoint = hit.position;
                            sideFound = true;
                        }
                    }
                }

                if (!sideFound)
                    continue;

                float finalScore = sideBestScore - (sideOpenScore * 0.12f);
                if (favorDesiredSide && side != preferredSide)
                    finalScore += 1.25f;

                if (finalScore < bestScore)
                {
                    bestScore = finalScore;
                    bestPoint = sideBestPoint;
                    bestTraversalScore = sideOpenScore;
                    found = true;
                }
            }

            if (!found)
                return false;

            point = bestPoint;
            traversalScore = bestTraversalScore;
            return true;
        }

        bool TryResolveOpenDoorTraversalDestination(Vector3 desired, bool preferUnlockedKeyDoors, bool favorDesiredSide, out Vector3 resolved)
        {
            resolved = desired;
            if (cachedDoors == null || cachedDoors.Length == 0)
                cachedDoors = FindObjectsByType<Door>(FindObjectsSortMode.None);
            if (cachedDoors == null || cachedDoors.Length == 0)
                return false;

            Vector3 fromFlat = transform.position;
            fromFlat.y = 0f;
            Vector3 desiredFlat = desired;
            desiredFlat.y = 0f;
            float desiredDoorRange = Mathf.Max(2.6f, unlockedKeyDoorTraverseRange + 1.8f);
            float pathDoorRange = Mathf.Max(1.2f, agent != null ? agent.radius * 3.2f : 1.4f);

            float bestScore = float.MaxValue;
            bool found = false;
            Vector3 best = desired;

            for (int i = 0; i < cachedDoors.Length; i++)
            {
                Door door = cachedDoors[i];
                if (door == null || !door.gameObject.activeInHierarchy)
                    continue;
                if (!door.IsOpen || door.IsLocked)
                    continue;

                Vector3 center = door.GetDoorwayCenter();
                Vector3 centerFlat = center;
                centerFlat.y = 0f;
                float distToDesired = Vector3.Distance(centerFlat, desiredFlat);
                float distToSegment = GetFlatDistanceToSegment(centerFlat, fromFlat, desiredFlat);
                if (distToDesired > desiredDoorRange && distToSegment > pathDoorRange)
                    continue;

                if (!TryGetBestDoorTraversalPoint(door, desired, favorDesiredSide, requireCompletePath: true, out var candidate, out float traversalScore))
                    continue;

                float score = Vector3.SqrMagnitude(candidate - desired) +
                              (distToDesired * 0.3f) +
                              (distToSegment * 0.85f) -
                              (traversalScore * 0.18f);
                if (preferUnlockedKeyDoors && door.WasUnlockedByKey)
                    score -= 4.5f;

                if (score < bestScore)
                {
                    bestScore = score;
                    best = candidate;
                    found = true;
                }
            }

            if (!found)
                return false;

            resolved = best;
            return true;
        }

        bool TryResolvePartialPathAdvance(Vector3 desired, out Vector3 resolved)
        {
            resolved = desired;
            if (agent == null)
                return false;

            NavMeshPath partialPath = new NavMeshPath();
            if (!NavMesh.CalculatePath(transform.position, desired, NavMesh.AllAreas, partialPath))
                return false;
            if (partialPath.status != NavMeshPathStatus.PathPartial || partialPath.corners == null || partialPath.corners.Length < 2)
                return false;

            NavMeshPath completePath = new NavMeshPath();
            for (int i = partialPath.corners.Length - 1; i >= 1; i--)
            {
                Vector3 baseCorner = partialPath.corners[i];
                Vector3 towardDesired = desired - baseCorner;
                towardDesired.y = 0f;
                Vector3[] probes =
                {
                    baseCorner,
                    towardDesired.sqrMagnitude > 0.01f ? baseCorner + towardDesired.normalized * 0.45f : baseCorner,
                    towardDesired.sqrMagnitude > 0.01f ? baseCorner - towardDesired.normalized * 0.3f : baseCorner
                };

                for (int p = 0; p < probes.Length; p++)
                {
                    if (!NavMesh.SamplePosition(probes[p], out var hit, 1.2f, NavMesh.AllAreas))
                        continue;
                    if (Vector3.Distance(transform.position, hit.position) < 0.4f)
                        continue;
                    if (!IsDestinationAllowed(hit.position) || IsBodyIntersectingBlocking(hit.position))
                        continue;
                    if (!NavMesh.CalculatePath(transform.position, hit.position, NavMesh.AllAreas, completePath) ||
                        completePath.status != NavMeshPathStatus.PathComplete)
                        continue;
                    if (!IsPathAllowed(completePath))
                        continue;

                    resolved = hit.position;
                    return true;
                }
            }

            return false;
        }

        bool TryResolveProgressiveDestination(Vector3 desired, out Vector3 resolved)
        {
            resolved = desired;
            if (agent == null)
                return false;

            Vector3 from = transform.position;
            Vector3 flatToDesired = desired - from;
            flatToDesired.y = 0f;
            if (flatToDesired.sqrMagnitude < 1.2f * 1.2f)
                return false;
            if (Mathf.Abs(desired.y - from.y) > 0.55f)
                return false;

            Vector3 forward = flatToDesired.normalized;
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            if (right.sqrMagnitude < 0.001f)
                right = transform.right;
            right.y = 0f;
            if (right.sqrMagnitude < 0.001f)
                right = Vector3.right;
            right.Normalize();

            float[] fractions = { 0.94f, 0.82f, 0.7f, 0.58f, 0.46f, 0.34f, 0.24f };
            float[] widths = { 0f, 0.45f, -0.45f, 0.9f, -0.9f };

            NavMeshPath path = new NavMeshPath();
            float bestScore = float.MaxValue;
            bool found = false;
            Vector3 best = desired;

            for (int f = 0; f < fractions.Length; f++)
            {
                for (int w = 0; w < widths.Length; w++)
                {
                    Vector3 probe = Vector3.Lerp(from, desired, fractions[f]) + right * widths[w];
                    probe.y = Mathf.Lerp(from.y, desired.y, fractions[f]);
                    float sampleRadius = 1.0f + (Mathf.Abs(widths[w]) * 0.2f);
                    if (!NavMesh.SamplePosition(probe, out var hit, sampleRadius, NavMesh.AllAreas))
                        continue;
                    if (Vector3.Distance(transform.position, hit.position) < 0.45f)
                        continue;
                    if (!IsDestinationAllowed(hit.position) || IsBodyIntersectingBlocking(hit.position))
                        continue;
                    if (!NavMesh.CalculatePath(transform.position, hit.position, NavMesh.AllAreas, path) ||
                        path.status != NavMeshPathStatus.PathComplete)
                        continue;
                    if (!IsPathAllowed(path))
                        continue;

                    float score = Vector3.SqrMagnitude(hit.position - desired) -
                                  (fractions[f] * 6.5f) +
                                  (Mathf.Abs(widths[w]) * 0.18f);
                    if (score < bestScore)
                    {
                        bestScore = score;
                        best = hit.position;
                        found = true;
                    }
                }
            }

            if (!found)
                return false;

            resolved = best;
            return true;
        }

        bool TryWarpThroughOpenDoor(Door door, Vector3 targetPos)
        {
            if (door == null || !IsAgentReady()) return false;
            if (!TryGetBestDoorTraversalPoint(door, targetPos, favorDesiredSide: true, requireCompletePath: false, out var best, out _))
                return false;

            agent.Warp(best);
            transform.position = best;
            agent.nextPosition = best;
            agent.ResetPath();
            ignoreAntiClipUntilTime = Time.time + 0.35f;
            return TrySetDestination(targetPos) || TrySetDestination(best);
        }

        bool TryAdvanceThroughOpenDoor(Door door, Vector3 targetPos)
        {
            if (door == null || !IsAgentReady()) return false;
            if (!TryGetBestDoorTraversalPoint(door, targetPos, favorDesiredSide: true, requireCompletePath: true, out var best, out _))
                return false;
            return TrySetDestination(best);
        }

        void TryAutoOpenDoorInFront()
        {
            // Resident must never open player doors on its own.
            return;
        }

        bool ShouldBiasToUnlockedKeyDoorRoom()
        {
            if (cachedDoors == null || cachedDoors.Length == 0)
                cachedDoors = FindObjectsByType<Door>(FindObjectsSortMode.None);
            if (cachedDoors == null || cachedDoors.Length == 0)
                return false;

            for (int i = 0; i < cachedDoors.Length; i++)
            {
                Door door = cachedDoors[i];
                if (door == null || !door.gameObject.activeInHierarchy)
                    continue;
                if (!door.IsOpen || door.IsLocked || !door.WasUnlockedByKey)
                    continue;

                return Random.value < unlockedKeyDoorBiasChance;
            }

            return false;
        }

        bool TryGetUnlockedKeyDoorRoomTarget(out Vector3 point)
        {
            point = transform.position;
            if (cachedDoors == null || cachedDoors.Length == 0)
                cachedDoors = FindObjectsByType<Door>(FindObjectsSortMode.None);
            if (cachedDoors == null || cachedDoors.Length == 0)
                return false;

            int startIndex = Random.Range(0, cachedDoors.Length);
            float roomDepth = Mathf.Max(1.35f, unlockedKeyDoorTraverseRange * 0.72f);
            float bestScore = float.MaxValue;
            bool found = false;
            Vector3 best = point;

            for (int offset = 0; offset < cachedDoors.Length; offset++)
            {
                Door door = cachedDoors[(startIndex + offset) % cachedDoors.Length];
                if (door == null || !door.gameObject.activeInHierarchy)
                    continue;
                if (!door.IsOpen || door.IsLocked || !door.WasUnlockedByKey)
                    continue;

                Vector3 center = door.GetDoorwayCenter();
                Vector3 forward = door.GetDoorwayForward();
                if (forward.sqrMagnitude < 0.001f)
                    continue;

                if (TryGetBestDoorTraversalPoint(door, center, favorDesiredSide: false, requireCompletePath: true, out var interiorPoint, out float traversalScore))
                {
                    float score = Vector3.SqrMagnitude(interiorPoint - transform.position) - (traversalScore * 0.25f);
                    if (score < bestScore)
                    {
                        bestScore = score;
                        best = interiorPoint;
                        found = true;
                    }
                    continue;
                }

                Vector3 candidateA = center + forward * roomDepth;
                Vector3 candidateB = center - forward * roomDepth;
                if ((TryGetRandomRoamPoint(candidateA, unlockedKeyDoorBiasRadius, out var candidatePointA) ||
                     ResolveReachableDestination(candidateA, out candidatePointA)))
                {
                    float scoreA = Vector3.SqrMagnitude(candidatePointA - transform.position);
                    if (scoreA < bestScore)
                    {
                        bestScore = scoreA;
                        best = candidatePointA;
                        found = true;
                    }
                }

                if ((TryGetRandomRoamPoint(candidateB, unlockedKeyDoorBiasRadius, out var candidatePointB) ||
                     ResolveReachableDestination(candidateB, out candidatePointB)))
                {
                    float scoreB = Vector3.SqrMagnitude(candidatePointB - transform.position);
                    if (scoreB < bestScore)
                    {
                        bestScore = scoreB;
                        best = candidatePointB;
                        found = true;
                    }
                }
            }

            if (!found)
                return false;

            point = best;
            return true;
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
                {
                    float verticalOffset = Random.Range(
                        Mathf.Max(2.2f, multiFloorRoamHeight * 0.35f),
                        Mathf.Max(2.6f, multiFloorRoamHeight));

                    bool preferUpstairs = ShouldBiasRoamUpstairs(center);
                    candidate.y = center.y + (preferUpstairs ? verticalOffset : -verticalOffset);
                }
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

        bool ShouldBiasRoamUpstairs(Vector3 center)
        {
            const float floorDelta = 2.2f;

            if (player != null)
            {
                float playerDelta = player.position.y - center.y;
                if (playerDelta > floorDelta)
                    return Random.value < 0.94f;
                if (playerDelta < -floorDelta)
                    return Random.value < 0.18f;
            }

            if (lastSeenPlayerTime > -998f)
            {
                float lastSeenDelta = lastSeenPlayerPos.y - center.y;
                if (lastSeenDelta > floorDelta)
                    return Random.value < 0.88f;
                if (lastSeenDelta < -floorDelta)
                    return Random.value < 0.22f;
            }

            return Random.value < 0.72f;
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
            cachedFuseDoors = FindObjectsByType<FuseDoor>(FindObjectsSortMode.None);
            cachedMainDoors = FindObjectsByType<MainDoor>(FindObjectsSortMode.None);
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

        bool PathCrossesClosedDoor(NavMeshPath path)
        {
            if (path == null || path.corners == null || path.corners.Length < 2) return false;
            if (cachedDoors == null) cachedDoors = FindObjectsByType<Door>(FindObjectsSortMode.None);
            if (cachedFuseDoors == null) cachedFuseDoors = FindObjectsByType<FuseDoor>(FindObjectsSortMode.None);
            if (cachedMainDoors == null) cachedMainDoors = FindObjectsByType<MainDoor>(FindObjectsSortMode.None);

            bool hasAnyDoor =
                (cachedDoors != null && cachedDoors.Length > 0) ||
                (cachedFuseDoors != null && cachedFuseDoors.Length > 0) ||
                (cachedMainDoors != null && cachedMainDoors.Length > 0);
            if (!hasAnyDoor) return false;

            Vector3[] corners = path.corners;
            for (int i = 0; i < corners.Length - 1; i++)
            {
                Vector3 a = corners[i] + Vector3.up * 1.0f;
                Vector3 b = corners[i + 1] + Vector3.up * 1.0f;
                Vector3 dir = b - a;
                float len = dir.magnitude;
                if (len <= 0.001f) continue;
                Ray ray = new Ray(a, dir / len);

                if (SegmentHitsClosedDoor(ray, len, cachedDoors))
                    return true;

                if (SegmentHitsClosedDoor(ray, len, cachedFuseDoors))
                    return true;

                if (SegmentHitsClosedDoor(ray, len, cachedMainDoors))
                    return true;
            }

            return false;
        }

        static bool SegmentHitsClosedDoor<TDoor>(Ray ray, float len, TDoor[] doors) where TDoor : MonoBehaviour
        {
            if (doors == null || doors.Length == 0) return false;

            for (int d = 0; d < doors.Length; d++)
            {
                TDoor door = doors[d];
                if (door == null || !door.gameObject.activeInHierarchy) continue;

                bool isClosed =
                    (door is Door standardDoor && !standardDoor.IsOpen) ||
                    (door is FuseDoor fuseDoor && !fuseDoor.IsOpen) ||
                    (door is MainDoor mainDoor && !mainDoor.IsOpen);
                if (!isClosed) continue;

                Collider[] cols = door.GetComponentsInChildren<Collider>(false);
                for (int c = 0; c < cols.Length; c++)
                {
                    Collider col = cols[c];
                    if (col == null || !col.enabled || col.isTrigger) continue;
                    if (col.Raycast(ray, out _, len))
                        return true;
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
            if (IsSquareFusePortalTraversalActive(agent.hasPath ? agent.destination : transform.position))
            {
                generalStuckPosInit = false;
                generalStuckTimer = 0f;
                return;
            }
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
            return TrySetDestination(destination, out _);
        }

        bool TrySetDestination(Vector3 destination, out Vector3 resolvedDestination)
        {
            resolvedDestination = destination;
            if (!IsAgentReady()) return false;
            if (!ResolveReachableDestination(destination, out resolvedDestination))
                return false;
            return agent.SetDestination(resolvedDestination);
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
                !PathCrossesClosedDoor(path) &&
                (!strictGeometryValidation || IsPathAllowed(path)))
            {
                resolved = desired;
                return true;
            }

            if (TryResolveOpenDoorTraversalDestination(desired, preferUnlockedKeyDoors: true, favorDesiredSide: true, out var doorResolved))
            {
                resolved = doorResolved;
                return true;
            }

            if (TryResolveSquareFusePortalTraversalDestination(desired, out var squareFusePortalResolved))
            {
                resolved = squareFusePortalResolved;
                return true;
            }

            if (TryResolveSquareFuseCorridorBridgeDestination(desired, out var squareFuseBridgeResolved, out _))
            {
                resolved = squareFuseBridgeResolved;
                return true;
            }

            if (TryResolvePartialPathAdvance(desired, out var partialResolved))
            {
                resolved = partialResolved;
                return true;
            }

            if (TryResolveProgressiveDestination(desired, out var progressiveResolved))
            {
                resolved = progressiveResolved;
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
                    PathCrossesClosedDoor(path) ||
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
            if (IsSquareFusePortalTraversalActive(agent.hasPath ? agent.destination : transform.position))
                return;
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

            bool stairProfile = ShouldUseStairNavigationProfile();
            Vector3 navTarget = agent.hasPath ? agent.destination : transform.position;
            bool tightPassageProfile = IsTightPassageContext(navTarget);
            bool squareFuseTight = IsSquareFuseTightNavigationZone(transform.position) || IsSquareFuseTightNavigationZone(navTarget);
            float targetRadius = enforcedAgentRadius;
            if (squareFuseTight)
                targetRadius = Mathf.Min(enforcedAgentRadius, 0.16f);
            else if (tightPassageProfile)
                targetRadius = Mathf.Min(enforcedAgentRadius, 0.2f);
            else if (stairProfile)
                targetRadius = Mathf.Min(enforcedAgentRadius, 0.28f);

            float targetHeight = enforcedAgentHeight;
            if (tightPassageProfile)
                targetHeight = Mathf.Min(enforcedAgentHeight, squareFuseTight ? 1.62f : 1.76f);
            else if (stairProfile)
                targetHeight = Mathf.Min(enforcedAgentHeight, 1.9f);
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
            bool  tightPassage    = IsTightPassageContext(agent.destination);

            // Only activate the assist when on stairs OR traversing an elevated path.
            // Previously we required stairBlend >= 0.24, but mid-staircase the forward
            // probe often returns 0 (agent already elevated, next step same height as
            // current position). Using path corners as the fallback detection catches this.
            if (!onStairs && !tightPassage)
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

            float assistThreshold = tightPassage
                ? Mathf.Max(0.12f, stairTraverseStallSeconds * 0.75f)
                : stairTraverseStallSeconds;
            if (Time.time < nextStairTraverseAssistAt || stairTraverseStuckTimer < assistThreshold)
                return;

            stairTraverseStuckTimer = 0f;
            nextStairTraverseAssistAt = Time.time + stairTraverseRecoveryCooldown;

            // Remember the original destination so we can restore it after nudging.
            // If we only call SetDestination(cornerRecoveryTarget), the patrol coroutine
            // sees agent.destination == corner, and when the agent arrives there it thinks
            // the whole trip is done — picking a fresh random point instead of continuing.
            Vector3 originalDestination = agent.destination;
            bool squareFuseTraverse =
                IsSquareFuseTraverseContext(originalDestination) ||
                IsSquareFuseTraverseContext(current);

            if (tightPassage && TryResolveProgressiveDestination(originalDestination, out Vector3 progressiveTarget))
            {
                if (squareFuseTraverse)
                {
                    TrySetDestination(progressiveTarget);
                    return;
                }

                if (!allowRuntimeRecoveryWarps)
                {
                    QueueShortAdvanceAndReissueDestination(progressiveTarget, originalDestination, 0.2f);
                    return;
                }

                if (NavMesh.SamplePosition(progressiveTarget, out var progressiveHit, 1.0f, NavMesh.AllAreas))
                {
                    float advanceDistance = Vector3.Distance(current, progressiveHit.position);
                    if (advanceDistance <= Mathf.Max(0.9f, stairTraverseProbeDistance * 1.35f))
                    {
                        agent.Warp(progressiveHit.position);
                        transform.position = progressiveHit.position;
                        agent.nextPosition = progressiveHit.position;
                        lastSafePosition = progressiveHit.position;
                        hasSafePosition = true;
                        ignoreAntiClipUntilTime = Time.time + 0.35f;
                        ReissuePrimaryDestination(originalDestination);
                        return;
                    }
                }

                TrySetDestination(progressiveTarget);
                return;
            }

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
                if (squareFuseTraverse)
                {
                    TrySetDestination(nudgeTarget);
                    return;
                }

                if (!allowRuntimeRecoveryWarps)
                {
                    QueueShortAdvanceAndReissueDestination(nudgeTarget, originalDestination, 0.18f);
                    return;
                }

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

            if (IsSquareFusePortalTraversalActive(agent.hasPath ? agent.destination : current))
            {
                lastSafePosition = current;
                hasSafePosition = true;
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

            if (PathCrossesClosedDoor(path))
                return false;

            Vector3[] corners = path.corners;
            for (int i = 0; i < corners.Length - 1; i++)
            {
                // Skip wall checks for stair/elevated segments — the linecast at 1.0m height
                // can falsely hit overhead floor geometry in stairwells and reject valid paths.
                if (Mathf.Abs(corners[i + 1].y - corners[i].y) > 0.18f)
                    continue;
                if (IsTightPassageContext((corners[i] + corners[i + 1]) * 0.5f))
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
            bool tightPassageContext = IsTightPassageContext(position);
            bool squareFuseTight = IsSquareFuseTightNavigationZone(position);
            float effectiveWallClearance = elevatedTargetContext
                ? Mathf.Max(0.12f, destinationWallClearance * 0.45f)
                : closeChaseEntryContext
                    ? Mathf.Max(0.14f, destinationWallClearance * 0.55f)
                    : tightPassageContext
                        ? squareFuseTight
                            ? Mathf.Max(0.04f, destinationWallClearance * 0.14f)
                            : Mathf.Max(0.08f, destinationWallClearance * 0.28f)
                        : destinationWallClearance;
            Vector3 probe = position + Vector3.up * (squareFuseTight ? 0.6f : tightPassageContext ? 0.75f : 1.0f);
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

        bool IsDirectKillLineBlocked(Vector3 from, Vector3 to)
        {
            Vector3 dir = to - from;
            float len = dir.magnitude;
            if (len <= 0.12f)
                return false;

            dir /= len;
            const float inset = 0.05f;
            from += dir * inset;
            to -= dir * inset;
            len = Vector3.Distance(from, to);
            if (len <= 0.02f)
                return false;

            if (!Physics.Linecast(from, to, out RaycastHit hit, blockingGeometryMask, QueryTriggerInteraction.Ignore))
                return false;

            if (hit.distance <= 0.04f || hit.distance >= len - 0.04f)
                return false;

            return IsHardBlockingCollider(hit.collider);
        }

        public bool IsKillContactClear(Vector3 sourcePoint, Vector3 targetPoint)
        {
            return !IsDirectKillLineBlocked(sourcePoint, targetPoint);
        }

        public bool IsCloseKillReachable(Vector3 targetPoint, float directDistance)
        {
            if (!NavMesh.SamplePosition(transform.position, out var selfHit, 1.5f, NavMesh.AllAreas))
                return false;
            if (!NavMesh.SamplePosition(targetPoint, out var targetHit, 1.5f, NavMesh.AllAreas))
                return false;
            if (IsSquareFuseKillBlocked(selfHit.position, targetHit.position))
                return false;

            if (IsPlayerOccupyingSquareFuseRoom() &&
                !IsSquareFuseInteriorSide(transform.position) &&
                (IsSquareFuseTraverseContext(player != null ? player.position : targetHit.position) ||
                 IsSquareFuseTraverseContext(targetHit.position)))
                return false;

            if (IsSquareFusePortalSeparated(targetHit.position, selfHit.position))
                return false;

            bool selfInSquareFuseRoom = IsSquareFuseRoomZone(selfHit.position);
            bool targetInSquareFuseRoom = IsSquareFuseRoomZone(targetHit.position);
            if (IsSquareFuseTraverseContext(targetHit.position) &&
                selfInSquareFuseRoom != targetInSquareFuseRoom)
                return false;

            if (IsDirectKillLineBlocked(GetResidentThreatPoint(0.5f), targetPoint))
                return false;

            NavMeshPath closePath = new NavMeshPath();
            if (!NavMesh.CalculatePath(selfHit.position, targetHit.position, NavMesh.AllAreas, closePath) ||
                closePath.status != NavMeshPathStatus.PathComplete)
                return false;

            if (PathCrossesClosedDoor(closePath))
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
            bool tightPassageContext = IsTightPassageContext(worldPos);
            bool squareFuseTight = IsSquareFuseTightNavigationZone(worldPos);
            float penetrationThreshold = elevatedTargetContext
                ? Mathf.Max(0.12f, antiClipPenetrationEpsilon * 2.5f)
                : closeChaseEntryContext
                    ? Mathf.Max(0.04f, antiClipPenetrationEpsilon * 1.4f)
                    : tightPassageContext
                        ? squareFuseTight
                            ? Mathf.Max(0.085f, antiClipPenetrationEpsilon * 2.2f)
                            : Mathf.Max(0.06f, antiClipPenetrationEpsilon * 1.8f)
                        : Mathf.Max(0.001f, antiClipPenetrationEpsilon);

            float radius = 0.35f;
            float height = 2.0f;
            if (residentCapsule != null)
            {
                float xzScale = Mathf.Max(0.01f, Mathf.Max(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.z)));
                float yScale = Mathf.Max(0.01f, Mathf.Abs(transform.lossyScale.y));
                float radiusScale = elevatedTargetContext ? 0.76f : closeChaseEntryContext ? 0.88f : squareFuseTight ? 0.5f : tightPassageContext ? 0.64f : 0.92f;
                float heightScale = squareFuseTight ? 0.66f : tightPassageContext ? 0.78f : 0.98f;
                radius = Mathf.Max(0.12f, residentCapsule.radius * xzScale * radiusScale);
                height = Mathf.Max(radius * 2f + 0.01f, residentCapsule.height * yScale * heightScale);
            }
            else if (agent != null)
            {
                float radiusScale = elevatedTargetContext ? 0.76f : closeChaseEntryContext ? 0.88f : squareFuseTight ? 0.5f : tightPassageContext ? 0.64f : 0.92f;
                float heightScale = squareFuseTight ? 0.66f : tightPassageContext ? 0.78f : 0.98f;
                radius = Mathf.Max(0.12f, agent.radius * radiusScale);
                height = Mathf.Max(radius * 2f + 0.01f, agent.height * heightScale);
            }

            Vector3 center = worldPos + Vector3.up * Mathf.Max(0.14f, squareFuseTight ? antiClipCheckHeight * 0.5f : tightPassageContext ? antiClipCheckHeight * 0.72f : antiClipCheckHeight);
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
            if (door != null)
                return !door.IsOpen;

            FuseDoor fuseDoor = t.GetComponentInParent<FuseDoor>();
            if (fuseDoor != null)
                return !fuseDoor.IsOpen;

            MainDoor mainDoor = t.GetComponentInParent<MainDoor>();
            if (mainDoor != null)
                return !mainDoor.IsOpen;

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
