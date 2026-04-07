using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Sushil.Systems;

namespace Sushil.AI
{
    public partial class ResidentAI
    {
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

        bool CanSeePlayer()
        {
            if (player == null) return false;
            if (IsPlayerHidden()) return false;

            if (CanSensePlayerInAwarenessRadius())
                return true;

            Vector3 toPlayer = player.position - transform.position;
            float dist = toPlayer.magnitude;
            if (dist > sightRange) return false;

            if (!omnidirectionalVision)
            {
                float angle = Vector3.Angle(transform.forward, toPlayer.normalized);
                if (angle > fovDegrees * 0.5f) return false;
            }

            Vector3 eye = transform.position + Vector3.up * 1.6f;
            Vector3 lateral = player.right;
            lateral.y = 0f;
            if (lateral.sqrMagnitude < 0.0001f)
                lateral = Vector3.right;
            lateral = lateral.normalized * 0.22f;
            Vector3[] targetPoints =
            {
                player.position + Vector3.up * 1.6f,
                player.position + Vector3.up * 1.2f,
                player.position + Vector3.up * 0.8f,
                player.position + Vector3.up * 1.2f + lateral,
                player.position + Vector3.up * 1.2f - lateral
            };

            for (int i = 0; i < targetPoints.Length; i++)
            {
                if (HasLineOfSightToTargetPoint(eye, targetPoints[i]))
                    return true;
            }

            return false;
        }

        bool CanSensePlayerInAwarenessRadius()
        {
            if (!enablePeripheralAwareness || player == null)
                return false;

            Vector3 eye = GetResidentThreatPoint(0.72f);
            Vector3 sensedPoint = GetPlayerClosestBodyPoint(eye);
            Vector3 toPlayer = sensedPoint - eye;
            float playerVerticalSeparation = Mathf.Abs(sensedPoint.y - eye.y);
            if (playerVerticalSeparation > Mathf.Max(0.75f, peripheralAwarenessVerticalTolerance))
                return false;

            Vector3 flatOffset = toPlayer;
            flatOffset.y = 0f;
            if (playerVerticalSeparation <= Mathf.Max(0.45f, closeAwarenessVerticalTolerance) &&
                flatOffset.magnitude <= Mathf.Max(closeAwarenessDistance, killDistance + 1.8f))
                return true;

            float awarenessRange = Mathf.Max(killDistance + 1.0f, sightRange * Mathf.Clamp01(peripheralAwarenessRangeMultiplier));
            if (flatOffset.magnitude > awarenessRange)
                return false;

            Vector3 lateral = player.right;
            lateral.y = 0f;
            if (lateral.sqrMagnitude < 0.0001f)
                lateral = Vector3.right;
            lateral = lateral.normalized * 0.18f;

            Vector3[] targetPoints =
            {
                player.position + Vector3.up * 1.45f,
                player.position + Vector3.up * 1.1f,
                player.position + Vector3.up * 1.1f + lateral,
                player.position + Vector3.up * 1.1f - lateral
            };

            for (int i = 0; i < targetPoints.Length; i++)
            {
                if (HasLineOfSightToTargetPoint(eye, targetPoints[i]))
                    return true;
            }

            return false;
        }

        bool HasLineOfSightToTargetPoint(Vector3 eye, Vector3 target)
        {
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
                if (hitT == transform || hitT.IsChildOf(transform)) continue;

                if (hitT == player) return true;
                if (hitT.IsChildOf(player)) return true;
                if (hitT.GetComponentInParent<RohitFPSController>() != null) return true;
                if (hitT.GetComponentInParent<PlayerDeath>() != null) return true;
                return false;
            }

            return false;
        }

        bool ShouldForceProximityChase(bool isHiddenNow)
        {
            if (isHiddenNow || player == null)
                return false;

            Vector3 residentPoint = GetResidentThreatPoint(0.5f);
            Vector3 playerPoint = GetPlayerClosestBodyPoint(residentPoint);
            Vector3 residentFlat = residentPoint;
            residentFlat.y = 0f;
            Vector3 playerFlat = playerPoint;
            playerFlat.y = 0f;

            float horizontalDistance = Vector3.Distance(residentFlat, playerFlat);
            float playerVerticalSeparation = Mathf.Abs(playerPoint.y - residentPoint.y);
            if (playerVerticalSeparation > Mathf.Max(0.55f, proximityChaseVerticalTolerance))
                return false;

            float emergencyCloseThreatDistance = Mathf.Max(1.45f, killDistance + 0.2f);
            if (horizontalDistance <= emergencyCloseThreatDistance)
                return true;

            if (!allowProximityChaseWithoutNoise)
                return false;

            return horizontalDistance <= Mathf.Max(proximityChaseDistance, killDistance + 1.25f);
        }

        float GetPlayerVerticalSeparation()
        {
            if (player == null)
                return float.PositiveInfinity;

            Vector3 residentPoint = GetResidentThreatPoint(0.5f);
            Vector3 playerPoint = GetPlayerClosestBodyPoint(residentPoint);
            return Mathf.Abs(playerPoint.y - residentPoint.y);
        }

        Vector3 GetResidentThreatPoint(float height01)
        {
            float bodyHeight = Mathf.Max(1.6f, enforcedAgentHeight);
            float sampleHeight = Mathf.Clamp(bodyHeight * Mathf.Clamp01(height01), 0.9f, 1.2f);
            return transform.position + Vector3.up * sampleHeight;
        }

        Vector3 GetPlayerClosestBodyPoint(Vector3 fromPoint)
        {
            if (player == null)
                return fromPoint;

            if (playerCharacterController != null)
                return playerCharacterController.bounds.ClosestPoint(fromPoint);

            if (playerPrimaryCollider != null && playerPrimaryCollider.enabled)
                return playerPrimaryCollider.ClosestPoint(fromPoint);

            return player.position + Vector3.up * 0.9f;
        }

        bool IsPlayerOccupyingSquareFuseRoom()
        {
            if (player == null)
                return false;

            if (IsSquareFuseRoomZone(player.position) || IsSquareFuseInteriorSide(player.position))
                return true;

            Vector3 forward = player.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude > 0.001f)
                forward.Normalize();
            else
                forward = Vector3.forward;

            if (playerCharacterController != null && IsSquareFuseRoomBounds(playerCharacterController.bounds, forward))
                return true;

            if (playerPrimaryCollider != null &&
                playerPrimaryCollider.enabled &&
                IsSquareFuseRoomBounds(playerPrimaryCollider.bounds, forward))
                return true;

            Vector3 forwardProbe = player.position + Vector3.up * 1.35f + forward * 0.75f;
            return IsSquareFuseRoomZone(forwardProbe) || IsSquareFuseInteriorSide(forwardProbe);
        }

        bool IsSquareFuseRoomBounds(Bounds bounds, Vector3 forward)
        {
            Vector3 center = bounds.center;
            Vector3 extents = bounds.extents;
            float lowY = bounds.min.y + Mathf.Min(0.18f, extents.y);
            float midY = center.y;
            float highY = bounds.max.y - Mathf.Min(0.12f, extents.y);

            Vector3[] samples =
            {
                center,
                new Vector3(center.x, lowY, center.z),
                new Vector3(center.x, highY, center.z),
                center + forward * 0.45f,
                center + forward * 0.9f,
                center - forward * 0.2f,
                new Vector3(bounds.max.x, lowY, bounds.max.z),
                new Vector3(bounds.min.x, lowY, bounds.min.z),
                new Vector3(bounds.max.x, midY, bounds.min.z),
                new Vector3(bounds.min.x, midY, bounds.max.z)
            };

            for (int i = 0; i < samples.Length; i++)
            {
                if (IsSquareFuseRoomZone(samples[i]) || IsSquareFuseInteriorSide(samples[i]))
                    return true;
            }

            return false;
        }

        bool IsSahilTestNewLevel()
        {
            string path = SceneManager.GetActiveScene().path;
            return path == "Assets/Sahil/Test/NewLevel.unity" ||
                   path == "Assets/Sahil/Test/NewNewLevel.unity";
        }

        bool IsPlayerHidden()
        {
            if (playerHide != null && playerHide.IsHidden) return true;
            if (rohitFPS != null && rohitFPS.isHidden) return true;
            return false;
        }

    }
}
