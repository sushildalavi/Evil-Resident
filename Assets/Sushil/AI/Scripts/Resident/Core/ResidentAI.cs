using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Sushil.Systems;

namespace Sushil.AI
{
    public partial class ResidentAI : MonoBehaviour
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
        [Tooltip("Minimum distance (m) a newly picked patrol target must be from the resident's current position. Prevents tiny local loops.")]
        public float minPatrolTravelDistance = 6f;
        [Range(0f, 1f)] public float patrolPointVisitChance = 0.45f;
        [Range(0f, 1f)] public float lastNoiseRoomBiasChance = 0.6f;
        public float lastNoiseRoomBiasDuration = 25f;
        public float lastNoiseRoomRadius = 10f;
        [Range(0f, 1f)] public float unlockedKeyDoorBiasChance = 0.58f;
        public float unlockedKeyDoorBiasRadius = 5.5f;
        public float unlockedKeyDoorTraverseRange = 3.6f;
        public bool allowMultiFloorRoam = true;
        [Range(0f, 1f)] public float multiFloorRoamChance = 0.55f;
        public float multiFloorRoamHeight = 7f;

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
        [Tooltip("Keeps resident collider world size stable when root scale is large, so it can still pass doors.")]
        public bool normalizeColliderWorldSize = true;
        [Tooltip("Force agent capsule dimensions each run so door traversal remains stable.")]
        public bool enforceAgentSize = true;
        [Range(0.25f, 0.45f)] public float enforcedAgentRadius = 0.40f;
        [Range(1.8f, 2.3f)] public float enforcedAgentHeight = 2.20f;
        public float obstacleExtraPadding = 0.05f;

        [Header("Navigation Stability")]
        public bool keepAgentSnappedToNavMesh = false;
        public float navSnapSearchRadius = 1.6f;
        public float navSnapWarpDistance = 0.45f;
        public float destinationSampleRadius = 2.2f;
        public int destinationRetrySamples = 10;
        public float destinationRetryRadius = 3.2f;

        [Header("Movement Recovery")]
        [Tooltip("When disabled, the resident will not use runtime warp/snap recovery. This removes the visible twitch/teleport effect but makes it less aggressive about unsticking itself.")]
        public bool allowRuntimeRecoveryWarps = false;

        [Header("Hard Movement Constraints")]
        [Tooltip("Layers treated as hard blockers for resident movement validation.")]
        public LayerMask blockingGeometryMask = ~0;
        [Tooltip("Rejects destinations that overlap with walls/props/hideable colliders.")]
        public bool rejectDestinationsInsideBlockingGeometry = true;
        [Tooltip("Rejects destinations inside or too close to hideable colliders.")]
        public bool rejectDestinationsInsideHideables = true;
        [Tooltip("Extra clearance from hard geometry to avoid clipping walls.")]
        public float destinationWallClearance = 0.28f;
        [Tooltip("Clearance around hideable colliders the resident should not enter.")]
        public float destinationHideableClearance = 0.45f;
        [Tooltip("Validates each NavMesh path segment against geometry so AI enters rooms only via valid openings.")]
        public bool validatePathAgainstGeometry = true;
        [Tooltip("Final runtime anti-clip guard: reverts resident if it crosses blocking geometry in a frame.")]
        public bool enforceRuntimeAntiClip = true;
        [Tooltip("Capsule/line check height for runtime anti-clip.")]
        public float antiClipCheckHeight = 1.0f;
        [Tooltip("Probe radius for runtime anti-clip overlap checks.")]
        public float antiClipProbeRadius = 0.22f;
        [Tooltip("Also prevents crossing navmesh boundaries in a frame (independent of physics colliders).")]
        public bool enforceNavMeshBoundaryAntiClip = true;
        [Tooltip("Minimum penetration depth before anti-clip forces recovery.")]
        public float antiClipPenetrationEpsilon = 0.03f;

        [Header("Vision")]
        public float sightRange = 12f;
        public float fovDegrees = 110f;
        [Tooltip("Uses full-world occlusion for LOS so walls always block vision regardless of layer mask setup.")]
        public bool strictWallOcclusionVision = true;
        [Tooltip("If enabled, FOV check is ignored and resident can detect in all directions (still requires LOS + range).")]
        public bool omnidirectionalVision = true;
        [Tooltip("Short omnidirectional awareness bubble for side/back detection. Uses flat XZ distance so it feels less bugged up close.")]
        public bool enablePeripheralAwareness = true;
        [Range(0.3f, 1f)] public float peripheralAwarenessRangeMultiplier = 0.7f;
        [Tooltip("Maximum vertical difference allowed for the flat awareness bubble.")]
        public float peripheralAwarenessVerticalTolerance = 2.35f;
        [Tooltip("Very close awareness does not require front-facing LOS. This catches players walking right past the resident.")]
        public float closeAwarenessDistance = 3.8f;
        [Tooltip("Maximum root-height difference allowed for the close awareness bubble.")]
        public float closeAwarenessVerticalTolerance = 1.8f;
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
        [Tooltip("If true, sound-tracked position is fuzzy so resident does not know exact position without sight.")]
        public bool noiseTrackingHasUncertainty = true;
        public float noiseTrackingMinError = 0.8f;
        public float noiseTrackingMaxError = 2.2f;

        [Tooltip("Maximum root-height difference allowed for proximity chase without direct sight.")]
        public float proximityChaseVerticalTolerance = 2.1f;
        
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
        public float hideTriggeredSearchDuration = 7.5f;
        public float lastSeenSearchDurationBoost = 2f;

        [Header("Lost Target Search")]
        [Tooltip("When the resident loses sight of the player, search across the whole house instead of only hovering near the last seen spot.")]
        public bool roamWholeHouseWhenPlayerLost = true;
        public float wholeHouseSearchDuration = 14f;
        public float lostSightToSearchDelay = 0.35f;
        [Range(0f, 1f)] public float lostSightLastKnownBias = 0.35f;

        [Header("Creep Moments (optional but nice)")]
        [Range(0f, 1f)] public float pauseOutsideChance = 0.25f;
        public float pauseMin = 0.8f;
        public float pauseMax = 1.5f;

        [Header("Kill")]
        public float killDistance = 1.7f;
        [Tooltip("Maximum root-height difference allowed for direct kill contact.")]
        public float killVerticalTolerance = 1.5f;

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

        [Header("Motion Animation")]
        public Transform visualRoot;
        public bool autoFindVisualRoot = true;
        public float walkBobAmplitude = 0.012f;
        public float runBobAmplitude  = 0.022f;
        public float walkBobSpeed     = 1.1f;
        public float runBobSpeed      = 2.2f;
        public float walkLimbSwingDegrees = 6f;
        public float runLimbSwingDegrees  = 11f;
        public float chaseForwardLeanDegrees = 5f;
        [Header("Stair Motion")]
        public float stairProbeHeight = 1.4f;
        public float stairProbeForwardDistance = 0.7f;
        public float stairVisualLift = 0.1f;
        [Range(0f, 1f)] public float stairSwingReduction = 0.7f;
        public bool enableStairTraverseAssist = true;
        public float stairTraverseAssistSpeed = 1.5f;
        public float stairTraverseProbeDistance = 0.75f;
        public float stairTraverseStallSeconds = 0.22f;
        public float stairTraverseRecoveryCooldown = 0.22f;
        public float roamStuckRepathSeconds = 0.85f;
        public float idleLookAroundMaxAngle = 18f;
        [Header("Kill Attack Animation")]
        public float killAttackWindupSeconds = 0.22f;
        public float killAttackSwingSeconds = 0.16f;
        public float killAttackRecoverySeconds = 0.16f;
        public float killAttackLungeDistance = 0.28f;
        public float killAttackLeanDegrees = 28f;
        public float killAttackRightArmBackDegrees = 62f;
        public float killAttackRightArmStrikeDegrees = 145f;
        public float killAttackLeftArmBraceDegrees = 48f;

        public State state = State.Patrol;
        private Coroutine routine;

        // ===== NEW (one-shot kill) =====
        private PlayerDeath playerDeath;
        private PlayerHide playerHide;
        private RohitFPSController rohitFPS;
        private CharacterController playerCharacterController;
        private Collider playerPrimaryCollider;
        private bool killTriggered;
        private Vector3 roamCenter;
        private float forcedNextSearchDuration = -1f;
        private bool wasPlayerHiddenLastFrame;
        private float chaseUntilTime = -1f;
        private float ignoreSightUntilTime = -1f;
        private static readonly HashSet<int> killedRohitInstanceIds = new();
        private Vector3 lastSeenPlayerPos;
        private float lastSeenPlayerTime = -999f;
        private HideableObject[] cachedHideables;
        private float nextNavStabilizeAt;
        private Transform armL;
        private Transform armR;
        private Transform legL;
        private Transform legR;
        private Transform head;
        private Vector3 visualBaseLocalPos;
        private Quaternion visualBaseLocalRot;
        private Vector3 armLBaseLocalPos;
        private Vector3 armRBaseLocalPos;
        private Vector3 legLBaseLocalPos;
        private Vector3 legRBaseLocalPos;
        private Quaternion headBaseRot;
        private Quaternion armLBaseRot;
        private Quaternion armRBaseRot;
        private Quaternion legLBaseRot;
        private Quaternion legRBaseRot;
        private Transform faceRig;
        private Transform leftPupil;
        private Transform rightPupil;
        private Transform leftBrow;
        private Transform rightBrow;
        private Transform mouthVoid;
        private Transform upperTeeth;
        private Transform lowerTeeth;
        private Vector3 leftPupilBaseLocalPos;
        private Vector3 rightPupilBaseLocalPos;
        private Quaternion leftBrowBaseLocalRot;
        private Quaternion rightBrowBaseLocalRot;
        private Vector3 mouthVoidBaseLocalScale;
        private Vector3 upperTeethBaseLocalPos;
        private Vector3 lowerTeethBaseLocalPos;
        private float faceSeed;
        private float motionPhase;
        private float smoothedMove01;
        private Transform[] clawShaftsL = new Transform[4];
        private Transform[] clawShaftsR = new Transform[4];
        private Quaternion[] clawBasesL  = new Quaternion[4];
        private Quaternion[] clawBasesR  = new Quaternion[4];
        private Coroutine killAttackRoutine;
        private bool killAttackActive;
        private float killAttackStartedAt;
        private float killAttackImpactAt;
        private float killAttackHitAt;
        private float killAttackRecoverAt;
        private float killAttackEndAt;
        private Vector3 killAttackFocusPoint;
        private float chaseStuckTimer;
        private float nextDoorOpenTime;
        private float nextChaseRepathAt;
        private CapsuleCollider residentCapsuleCollider;
        private static readonly Collider[] movementBlockerHits = new Collider[32];
        private static readonly Collider[] capsuleBlockerHits = new Collider[32];
        private readonly List<Collider> cachedHideableColliders = new();
        private Vector3 lastSafePosition;
        private bool hasSafePosition;
        private float nextSoftNoClipRecoveryAt;
        private Door[] cachedDoors;
        private FuseDoor[] cachedFuseDoors;
        private MainDoor[] cachedMainDoors;
        private float ignoreAntiClipUntilTime;
        private float visibleAccum;
        private float hiddenAccum;
        private bool stablePlayerVisible;
        private float chaseLostSightTimer;
        private float chaseFarTimer;
        private float stairTraverseStuckTimer;
        private float nextStairTraverseAssistAt;
        private float generalStuckTimer;
        private Vector3 generalStuckLastPos;
        private bool generalStuckPosInit;
        private float currentNavigationRadius = -1f;
        private bool hasInvestigateTarget;
        private Vector3 investigateTargetPos;
        private Vector3 playerSpawnPosition;
        private bool hasPlayerSpawnPosition;
        private float nextDetectionFeedbackAt;
        private bool wholeHouseSearchMode;
        private Vector3 wholeHouseSearchLastKnownPos;
        private static Material faceVoidSharedMaterial;
        private static Material faceEyeSharedMaterial;

        public bool IndicatorSeesPlayer { get; private set; }
        public bool IndicatorSensesPlayer { get; private set; }
        private static Material facePupilSharedMaterial;
        private static Material faceToothSharedMaterial;

        void Reset() { agent = GetComponent<NavMeshAgent>(); }

        void OnEnable()
        {
            NoiseSystem.OnNoise += OnNoise;
            // Reset so a re-enabled resident can perform kills again.
            killTriggered = false;
            nextChaseRepathAt = 0f;
        }
        void OnDisable() => NoiseSystem.OnNoise -= OnNoise;

        void Start()
        {
            if (agent == null) agent = GetComponent<NavMeshAgent>();
            if (agent == null)
            {
                Debug.LogError("[ResidentAI] Missing NavMeshAgent.");
                enabled = false;
                return;
            }

            // Forced tuning (requested): keep doorway/room entry stable.
            randomizeSpawnOnStart = false;
            // Recovery warps must stay on so the resident can unstick itself from
            // doorframes and geometry. StabilizeAgentOnNavMesh is separately gated
            // by keepAgentSnappedToNavMesh so no visible snap-teleport occurs.
            allowRuntimeRecoveryWarps = true;
            keepAgentSnappedToNavMesh = false;
            // Runtime scale/collider normalization causes visible "growing/shrinking"
            // artifacts near door collisions in this project setup.
            autoSyncAgentAndColliderToScale = false;
            normalizeColliderWorldSize = false;
            minWallClearance = 0.45f;
            destinationRetrySamples = 16;
            destinationRetryRadius = 4.0f;
            enforcedAgentRadius = 0.30f;
            enforcedAgentHeight = 2.0f;
            enableDoorwayAssist = true;
            doorwayWarpAssist = false;
            doorwayStuckSeconds = 0.25f;
            doorwayAssistRange = Mathf.Max(doorwayAssistRange, 20f);
            doorwayAssistSampleRadius = Mathf.Max(doorwayAssistSampleRadius, 1.25f);
            requireClearPathForWarp = true;
            // Resident does NOT open doors on its own — that's a player-only mechanic.
            // The AI navigates only through doors that are already open.
            autoOpenNearbyDoors = false;
            autoDoorOpenRange = Mathf.Max(autoDoorOpenRange, 2.6f);
            autoDoorOpenCooldown = Mathf.Min(autoDoorOpenCooldown, 0.15f);
            destinationWallClearance = Mathf.Max(destinationWallClearance, 0.34f);
            antiClipProbeRadius = Mathf.Max(antiClipProbeRadius, 0.28f);
            // Remove bait-like behavior: no bias to noise rooms or unlocked key doors.
            lastNoiseRoomBiasChance = 0f;
            unlockedKeyDoorBiasChance = 0f;
            patrolPointVisitChance = 0f;
            // Use hard difficulty as baseline and scale down easy/medium slightly so
            // they stay tense but fair.
            const float hardPatrolSpeed = 1.6f;
            const float hardChaseSpeed = 1.85f;
            patrolMoveSpeed = hardPatrolSpeed;
            chaseMoveSpeed = hardChaseSpeed;
            omnidirectionalVision = false;
            enablePeripheralAwareness = true;
            peripheralAwarenessRangeMultiplier = 0.76f;
            peripheralAwarenessVerticalTolerance = Mathf.Clamp(peripheralAwarenessVerticalTolerance, 1.9f, 2.2f);
            closeAwarenessDistance = Mathf.Max(closeAwarenessDistance, 4.6f);
            closeAwarenessVerticalTolerance = Mathf.Clamp(closeAwarenessVerticalTolerance, 1.25f, 1.45f);
            sightAlwaysStartsChase = true;
            allowProximityChaseWithoutNoise = true;
            proximityChaseVerticalTolerance = Mathf.Clamp(proximityChaseVerticalTolerance, 2.2f, 2.45f);
            limitVisualChaseTime = true;
            keepChaseWhenRecentlySeen = true;
            relentlessVisualChase = false;
            loseChaseWhenFar = true;
            // Hard-difficulty baseline. Medium and Easy cap these down further below.
            sightRange = Mathf.Clamp(sightRange, 28f, 32f);
            fovDegrees = Mathf.Max(fovDegrees, 150f);
            strictWallOcclusionVision = true;
            proximityChaseDistance = Mathf.Clamp(proximityChaseDistance, 5.5f, 6.5f);
            visionAcquireSeconds = Mathf.Clamp(visionAcquireSeconds, 0.06f, 0.1f);
            visionLoseSeconds = Mathf.Clamp(visionLoseSeconds, 0.2f, 0.28f);
            chaseMemorySeconds = Mathf.Clamp(chaseMemorySeconds, 5.0f, 6.0f);
            lostSightPursuitSeconds = Mathf.Clamp(lostSightPursuitSeconds, 5.5f, 6.5f);
            lostSightToSearchDelay = Mathf.Clamp(lostSightToSearchDelay, 0.35f, 0.6f);
            maxChaseDistance = Mathf.Clamp(maxChaseDistance, 18f, 22f);
            farLoseDelay = Mathf.Max(farLoseDelay, 1.5f);
            roamWholeHouse = true;
            allowMultiFloorRoam = true;
            roamSampleAttempts = Mathf.Max(roamSampleAttempts, 28);
            freeRoamRadius = Mathf.Max(freeRoamRadius, 60f);
            unlockedKeyDoorBiasChance = Mathf.Max(unlockedKeyDoorBiasChance, 0.58f);
            unlockedKeyDoorBiasRadius = Mathf.Max(unlockedKeyDoorBiasRadius, 5.5f);
            unlockedKeyDoorTraverseRange = Mathf.Max(unlockedKeyDoorTraverseRange, 3.6f);
            searchRadius = Mathf.Max(searchRadius, 6.5f);
            // Cap multi-floor chance: too high a value wastes most sample attempts on a
            // non-existent floor, leaving the resident stationary during patrol.
            multiFloorRoamChance = Mathf.Clamp(multiFloorRoamChance, 0.2f, 0.42f);
            multiFloorRoamHeight = Mathf.Max(multiFloorRoamHeight, 7f);
            pauseOutsideChance = 0f;
            stairVisualLift = Mathf.Max(stairVisualLift, 0.1f);
            stairSwingReduction = Mathf.Clamp(stairSwingReduction, 0.55f, 0.8f);
            stairTraverseAssistSpeed = Mathf.Max(stairTraverseAssistSpeed, 1.5f);
            stairTraverseProbeDistance = Mathf.Max(stairTraverseProbeDistance, 0.75f);
            stairTraverseStallSeconds = Mathf.Clamp(stairTraverseStallSeconds, 0.16f, 0.3f);
            stairTraverseRecoveryCooldown = Mathf.Clamp(stairTraverseRecoveryCooldown, 0.12f, 0.28f);
            killAttackWindupSeconds = Mathf.Max(killAttackWindupSeconds, 0.22f);
            killAttackSwingSeconds = Mathf.Max(killAttackSwingSeconds, 0.16f);
            killAttackLungeDistance = Mathf.Max(killAttackLungeDistance, 0.28f);
            killAttackLeanDegrees = Mathf.Max(killAttackLeanDegrees, 28f);
            killAttackRightArmBackDegrees = Mathf.Max(killAttackRightArmBackDegrees, 62f);
            killAttackRightArmStrikeDegrees = Mathf.Max(killAttackRightArmStrikeDegrees, 145f);
            killAttackLeftArmBraceDegrees = Mathf.Max(killAttackLeftArmBraceDegrees, 48f);
            chaseForwardLeanDegrees = Mathf.Min(chaseForwardLeanDegrees, 1.4f);
            // Keep the takedown range tight even if the scene has a larger serialized value.
            killDistance = Mathf.Clamp(killDistance, 0.9f, 1.0f);
            killVerticalTolerance = Mathf.Clamp(killVerticalTolerance, 0.75f, 0.95f);
            roamWholeHouseWhenPlayerLost = false;
            wholeHouseSearchDuration = Mathf.Min(wholeHouseSearchDuration, 10f);
            lostSightLastKnownBias = Mathf.Max(lostSightLastKnownBias, 0.95f);
            if (IsDifficultyVariantScene())
            {
                // Difficulty-variant scenes should feel more house-driven than corridor-driven.
                patrolPointVisitChance = Mathf.Min(patrolPointVisitChance, 0.18f);
                lastNoiseRoomBiasChance = Mathf.Max(lastNoiseRoomBiasChance, 0.82f);
                lastNoiseRoomRadius = Mathf.Max(lastNoiseRoomRadius, 12f);
                unlockedKeyDoorBiasChance = Mathf.Max(unlockedKeyDoorBiasChance, 0.9f);
                unlockedKeyDoorBiasRadius = Mathf.Max(unlockedKeyDoorBiasRadius, 7f);
                unlockedKeyDoorTraverseRange = Mathf.Max(unlockedKeyDoorTraverseRange, 4.2f);
                multiFloorRoamChance = Mathf.Clamp(multiFloorRoamChance, 0.35f, 0.55f);
                multiFloorRoamHeight = Mathf.Max(multiFloorRoamHeight, 8.8f);
                roomSuspicionPortion = Mathf.Max(roomSuspicionPortion, 0.82f);
                roamSampleAttempts = Mathf.Max(roamSampleAttempts, 36);
            }
            if (IsSahilTestNewLevel())
            {
                sightRange = Mathf.Clamp(sightRange, 28f, 32f);
                visionAcquireSeconds = Mathf.Clamp(visionAcquireSeconds, 0.06f, 0.1f);
                visionLoseSeconds = Mathf.Clamp(visionLoseSeconds, 0.2f, 0.26f);
                allowProximityChaseWithoutNoise = true;
                proximityChaseDistance = Mathf.Clamp(proximityChaseDistance, 5.5f, 6.5f);
                chaseMemorySeconds = Mathf.Clamp(chaseMemorySeconds, 5.0f, 6.0f);
                lostSightPursuitSeconds = Mathf.Clamp(lostSightPursuitSeconds, 5.5f, 6.5f);
                lostSightToSearchDelay = Mathf.Clamp(lostSightToSearchDelay, 0.4f, 0.6f);
                roamWholeHouseWhenPlayerLost = false;
                wholeHouseSearchDuration = Mathf.Min(wholeHouseSearchDuration, 10f);
                lostSightLastKnownBias = Mathf.Max(lostSightLastKnownBias, 0.95f);
                maxChaseDistance = Mathf.Clamp(maxChaseDistance, 18f, 22f);
                farLoseDelay = Mathf.Max(farLoseDelay, 1.5f);
                killDistance = Mathf.Clamp(killDistance, 0.98f, 1.08f);
                killVerticalTolerance = Mathf.Clamp(killVerticalTolerance, 0.95f, 1.1f);
                multiFloorRoamChance = Mathf.Clamp(multiFloorRoamChance, 0.3f, 0.52f);
                multiFloorRoamHeight = Mathf.Max(multiFloorRoamHeight, 8.5f);
                closeAwarenessDistance = Mathf.Max(closeAwarenessDistance, 4.8f);
                closeAwarenessVerticalTolerance = Mathf.Clamp(closeAwarenessVerticalTolerance, 1.35f, 1.55f);
                proximityChaseVerticalTolerance = Mathf.Clamp(proximityChaseVerticalTolerance, 2.25f, 2.5f);
                // Add a runtime NavMeshLink so the resident can walk through the narrow
                // corridor (x=4.5–5.5) that is too tight to bake NavMesh at radius 0.5.
                EnsureSquareFuseCorridorNavLink();
            }
            if (IsMediumLevelScene())
            {
                patrolMoveSpeed = hardPatrolSpeed * 0.85f;
                chaseMoveSpeed = hardChaseSpeed * 0.85f;
                sightRange = Mathf.Min(sightRange, 26f);                  // was 19 — now nearly hard's 28
                proximityChaseDistance = Mathf.Min(proximityChaseDistance, 5.5f);  // was 4.1
                closeAwarenessDistance = Mathf.Min(closeAwarenessDistance, 5.0f);  // was 4.0
                chaseMemorySeconds = Mathf.Min(chaseMemorySeconds, 4.5f);          // was 2.8
                lostSightPursuitSeconds = Mathf.Min(lostSightPursuitSeconds, 5.0f);// was 3.2
                maxChaseDistance = Mathf.Min(maxChaseDistance, 17f);               // was 11.5
                farLoseDelay = Mathf.Min(farLoseDelay, 1.3f);                      // was 0.9
            }
            else if (IsEasyLevelScene())
            {
                patrolMoveSpeed = hardPatrolSpeed * 0.68f;
                chaseMoveSpeed = hardChaseSpeed * 0.68f;
                sightRange = Mathf.Min(sightRange, 16f);
                fovDegrees = Mathf.Min(fovDegrees, 118f);
                proximityChaseDistance = Mathf.Min(proximityChaseDistance, 3.2f);
                closeAwarenessDistance = Mathf.Min(closeAwarenessDistance, 3.2f);
                peripheralAwarenessVerticalTolerance = Mathf.Min(peripheralAwarenessVerticalTolerance, 1.45f);
                proximityChaseVerticalTolerance = Mathf.Min(proximityChaseVerticalTolerance, 1.65f);
                chaseMemorySeconds = Mathf.Min(chaseMemorySeconds, 2.0f);
                lostSightPursuitSeconds = Mathf.Min(lostSightPursuitSeconds, 2.4f);
                maxChaseDistance = Mathf.Min(maxChaseDistance, 9.5f);
                farLoseDelay = Mathf.Min(farLoseDelay, 0.65f);
                killDistance = Mathf.Min(killDistance, 0.70f);
                killVerticalTolerance = Mathf.Min(killVerticalTolerance, 0.65f);
            }
            if (IsTutorialResidentScene())
            {
                patrolMoveSpeed = 0.85f;
                chaseMoveSpeed = 1.0f;
                sightRange = Mathf.Min(sightRange, 13f);
                fovDegrees = Mathf.Min(fovDegrees, 105f);
                proximityChaseDistance = Mathf.Min(proximityChaseDistance, 2.6f);
                closeAwarenessDistance = Mathf.Min(closeAwarenessDistance, 2.7f);
                chaseMemorySeconds = Mathf.Min(chaseMemorySeconds, 1.5f);
                lostSightPursuitSeconds = Mathf.Min(lostSightPursuitSeconds, 1.8f);
                maxChaseDistance = Mathf.Min(maxChaseDistance, 8f);
                farLoseDelay = Mathf.Min(farLoseDelay, 0.45f);
                killDistance = Mathf.Min(killDistance, 0.60f);
                killVerticalTolerance = Mathf.Min(killVerticalTolerance, 0.55f);
            }
            // Keep hard geometry validation enabled so chase logic cannot cut through walls.
            validatePathAgainstGeometry = true;
            rejectDestinationsInsideBlockingGeometry = true;
            enforceRuntimeAntiClip = true;
            if (!allowRuntimeRecoveryWarps)
            {
                keepAgentSnappedToNavMesh = false;
            }

            // Avoid floating weirdness
            agent.baseOffset = 0f;
            CacheHideables();
            SetupNavigationCollisionFixes();
            if (IsBasementStairIssueScene())
                EnsureBasementStairNavLinks();
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
                agent.autoTraverseOffMeshLink = true;
            }
            residentCapsuleCollider = GetComponent<CapsuleCollider>();
            var cc = residentCapsuleCollider;
            if (cc != null)
            {
                cc.radius = enforcedAgentRadius;
                cc.height = enforcedAgentHeight;
                var c = cc.center;
                c.y = enforcedAgentHeight * 0.5f;
                cc.center = c;
            }

            DisableVisualBodyColliders();

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

        bool IsDifficultyVariantScene()
        {
            string path = SceneManager.GetActiveScene().path;
            return path == "Assets/Sushil/Easy Level.unity" ||
                   path == "Assets/Sahil/Test/Easy Level.unity" ||
                   path == "Assets/Sahil/Test/Medium Level.unity" ||
                   path == "Assets/Sahil/Test/Hard Level.unity" ||
                   path == "Assets/Sahil/Test/Old Hard.unity" ||
                   path == "Assets/Sahil/Test/Difficult Level.unity";
        }

        bool IsEasyLevelScene()
        {
            string path = SceneManager.GetActiveScene().path;
            return path == "Assets/Sushil/Easy Level.unity" ||
                   path == "Assets/Sahil/Test/Easy Level.unity";
        }

        bool IsMediumLevelScene()
        {
            string path = SceneManager.GetActiveScene().path;
            return path == "Assets/Sahil/Test/Medium Level.unity";
        }

        bool IsBasementStairIssueScene()
        {
            string path = SceneManager.GetActiveScene().path;
            return path == "Assets/Sahil/Test/Medium Level.unity" ||
                   path == "Assets/Sahil/Test/Hard Level.unity" ||
                   path == "Assets/Sahil/Test/Old Hard.unity" ||
                   path == "Assets/Sahil/Test/Difficult Level.unity";
        }

        bool IsTutorialResidentScene()
        {
            string path = SceneManager.GetActiveScene().path;
            return path == "Assets/Sahil/Tutorial/New Tutorial 1.unity" ||
                   path == "Assets/Sahil/Tutorial/New Tutorial 2.unity" ||
                   path == "Assets/Sahil/Tutorial/New Tutorial 3.unity";
        }

        bool ShouldRelocateSearchAwayFromHideSpot()
        {
            string path = SceneManager.GetActiveScene().path;
            return path == "Assets/Sushil/Easy Level.unity" ||
                   path == "Assets/Sahil/Test/Easy Level.unity" ||
                   path == "Assets/Sahil/Test/Medium Level.unity" ||
                   path == "Assets/Sahil/Test/Hard Level.unity" ||
                   path == "Assets/Sahil/Test/Old Hard.unity";
        }

        void Update()
        {
            if (player == null)
            {
                ResolvePlayerReference();
                if (player == null) return;
            }

            ApplyDynamicNavigationProfile();
            ApplyStairTraverseAssist();
            // Push open any closed-but-unlocked door we're walking into. This is the
            // primary defence against the resident getting permanently stuck at a door
            // the player closed (or never opened).
            TryAutoOpenDoorInFront();
            if (allowRuntimeRecoveryWarps)
            {
                StabilizeAgentOnNavMesh();
                TryGeneralStuckRecovery();
            }

            if (killAttackActive)
            {
                UpdateKillAttackFacing();
                UpdateMotionAnimation();
                return;
            }

            // If player is dead/disabled, stop AI cleanly
            if (!player.gameObject.activeInHierarchy)
            {
                SafeSetStopped(true);
                return;
            }

            bool isHiddenNow = IsPlayerHidden();

            // If player hides during chase, immediately lose them and resume a normal search.
            if (state == State.Chase && isHiddenNow)
            {
                // No lingering near hiding spots: immediately drop to patrol.
                hasNoise = false;
                hasInvestigateTarget = false;
                wholeHouseSearchMode = false;
                ChangeState(State.Patrol);
                wasPlayerHiddenLastFrame = isHiddenNow;
                return;
            }

            bool rawCanSee = CanSeePlayer();
            bool canSee = UpdateStableVision();
            if (!canSee && rawCanSee && sightAlwaysStartsChase && Time.time >= ignoreSightUntilTime)
            {
                stablePlayerVisible = true;
                visibleAccum = Mathf.Max(visibleAccum, Mathf.Max(Time.deltaTime, visionAcquireSeconds));
                hiddenAccum = 0f;
                canSee = true;
            }
            bool proximityChaseAllowed = ShouldForceProximityChase(isHiddenNow);
            IndicatorSeesPlayer = !isHiddenNow && (canSee || rawCanSee);
            IndicatorSensesPlayer = !IndicatorSeesPlayer &&
                                    !isHiddenNow &&
                                    (proximityChaseAllowed ||
                                     state == State.Chase ||
                                     state == State.Search ||
                                     state == State.Investigate);
            if (canSee)
            {
                lastSeenPlayerPos = player.position;
                lastSeenPlayerTime = Time.time;
                if (state == State.Chase && relentlessVisualChase)
                    StartVisualChaseWindow();
            }

            bool confirmedSight = canSee;
            bool justExitedHideInSight = wasPlayerHiddenLastFrame && !isHiddenNow && (confirmedSight || proximityChaseAllowed);
            bool visualChaseAllowed = !isHiddenNow && (confirmedSight || proximityChaseAllowed) && Time.time >= ignoreSightUntilTime;
            if (state != State.Chase && (justExitedHideInSight || visualChaseAllowed))
            {
                lastSeenPlayerPos = player.position;
                lastSeenPlayerTime = Time.time;
                StartVisualChaseWindow();
                NotifyDetectionFeedback();
                ChangeState(State.Chase);
            }

            // Kill should be reliable at close range once the resident has reached the player.
            Vector3 residentContactPoint = GetResidentThreatPoint(0.5f);
            Vector3 playerContactPoint = GetPlayerClosestBodyPoint(residentContactPoint);
            Vector3 residentFlat = residentContactPoint;
            residentFlat.y = 0f;
            Vector3 playerFlat = playerContactPoint;
            playerFlat.y = 0f;
            float contactDistanceToPlayer = Vector3.Distance(residentContactPoint, playerContactPoint);
            float horizontalContactDistance = Vector3.Distance(residentFlat, playerFlat);
            float playerVerticalSeparation = Mathf.Abs(playerContactPoint.y - residentContactPoint.y);
            bool squareFuseKillBlocked = IsSquareFuseKillBlocked(residentContactPoint, playerContactPoint);

            // All thresholds scale from killDistance so easy mode (killDistance=0.84) is
            // tighter than hard mode (killDistance≈1.0). Note: contact distances are
            // measured to the closest point on the player's capsule bounds — the player
            // CENTER is ~0.4-0.5m further away than these numbers suggest.
            bool lethalContact =
                contactDistanceToPlayer <= killDistance * 0.95f ||
                horizontalContactDistance <= killDistance * 0.9f;
            bool chaseOrSightThreat = state == State.Chase || rawCanSee || stablePlayerVisible || proximityChaseAllowed;

            // Point-blank override: skip all geometry/path checks when the resident has
            // physically closed the gap. Being stuck in a doorframe or slightly off the
            // NavMesh must not prevent the kill at this range.
            bool pointBlankKill = (chaseOrSightThreat || lethalContact) &&
                horizontalContactDistance < killDistance * 0.6f &&
                playerVerticalSeparation <= Mathf.Max(0.4f, killVerticalTolerance);

            bool closeVisibleKill = !pointBlankKill &&
                chaseOrSightThreat &&
                horizontalContactDistance <= killDistance &&
                contactDistanceToPlayer <= killDistance + 0.15f &&
                IsKillContactClear(residentContactPoint, playerContactPoint) &&
                IsKillContactPathStrictlyClear(playerContactPoint);

            if (!killTriggered &&
                !squareFuseKillBlocked &&
                !isHiddenNow &&
                playerVerticalSeparation <= Mathf.Max(0.4f, killVerticalTolerance) &&
                (pointBlankKill ||
                 (contactDistanceToPlayer <= killDistance + 0.05f &&
                  IsCloseKillReachable(playerContactPoint, contactDistanceToPlayer)) ||
                 closeVisibleKill) &&
                (chaseOrSightThreat || lethalContact))
            {
                NotifyImmediateResidentThreat(rawCanSee || canSee || closeVisibleKill);
                TryKillTarget(player.gameObject, "Resident one-shot");
            }

            wasPlayerHiddenLastFrame = isHiddenNow;
            UpdateMotionAnimation();
        }

        void LateUpdate()
        {
            if (enforceRuntimeAntiClip)
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
            if (newState != State.Chase)
            {
                chaseStuckTimer = 0f;
                chaseLostSightTimer = 0f;
                chaseFarTimer = 0f;
            }
            if (newState != State.Search)
            {
                wholeHouseSearchMode = false;
            }

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
            if (!isActiveAndEnabled) return;
            // If already killed player, ignore noise
            if (killTriggered) return;

            if (intensity < minNoiseIntensity) return;

            bool forceChaseNoise = globalNoiseForcesChase && IsGlobalPlayerNoise(type);

            float dist = Vector3.Distance(transform.position, pos);
            float hearingRadius = Mathf.Max(minHearingRadius, intensity * hearingScale);
            if (!forceChaseNoise && dist > hearingRadius) return;

            Vector3 targetPos = pos;
            if (NavMesh.SamplePosition(pos, out var navHit, 3f, NavMesh.AllAreas))
                targetPos = navHit.position;

            lastNoisePos = targetPos;
            lastNoiseIntensity = intensity;
            hasNoise = true;
            lastHeardNoiseTime = Time.time;

            investigateTargetPos = targetPos;
            hasInvestigateTarget = true;

            if (state != State.Chase)
                ChangeState(State.Investigate);
        }

        void BeginWholeHouseSearch(Vector3 lastKnownPos, float minimumDuration = -1f)
        {
            wholeHouseSearchMode = roamWholeHouseWhenPlayerLost;
            wholeHouseSearchLastKnownPos = lastKnownPos;
            investigateTargetPos = lastKnownPos;
            hasInvestigateTarget = true;
            lastNoisePos = lastKnownPos;
            lastNoiseIntensity = Mathf.Max(lastNoiseIntensity, minNoiseIntensity + 1f);
            hasNoise = true;

            float requestedDuration = minimumDuration > 0f ? minimumDuration : wholeHouseSearchDuration;
            forcedNextSearchDuration = Mathf.Max(forcedNextSearchDuration, requestedDuration);
        }

        void NotifyDetectionFeedback()
        {
            if (Time.time < nextDetectionFeedbackAt)
                return;
            nextDetectionFeedbackAt = Time.time + 2.25f;
        }

        public void NotifyImmediateResidentThreat(bool seen)
        {
            IndicatorSeesPlayer = seen;
            IndicatorSensesPlayer = !seen;
            nextDetectionFeedbackAt = Time.time + 1.25f;
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
                playerCharacterController = player.GetComponent<CharacterController>();
                if (playerCharacterController == null)
                    playerCharacterController = player.GetComponentInChildren<CharacterController>();
                playerPrimaryCollider = player.GetComponent<Collider>();
                if (playerPrimaryCollider == null)
                    playerPrimaryCollider = player.GetComponentInChildren<Collider>();
            }
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
                    bool useUnlockedDoorRoom = ShouldBiasToUnlockedKeyDoorRoom();
                    bool useNoiseRoom = !useUnlockedDoorRoom && ShouldBiasToLastNoiseRoom();
                    if (useUnlockedDoorRoom &&
                        TryGetUnlockedKeyDoorRoomTarget(out targetPos))
                    {
                        // Prefer rooms behind doors the player has already unlocked.
                    }
                    else if (useNoiseRoom &&
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
                            !TryGetRandomRoamPoint(roamCenter, freeRoamRadius, out targetPos) &&
                            !TryGetRandomRoamPoint(transform.position, Mathf.Max(localPatrolRadius, 8f), out targetPos) &&
                            !TryGetRandomRoamPoint(transform.position, Mathf.Max(3f, localPatrolRadius * 0.5f), out targetPos))
                        {
                            if (IsAgentReady())
                            {
                                SafeSetStopped(false);
                                SafeResetPath();
                            }
                            yield return new WaitForSeconds(0.15f);
                            continue;
                        }
                    }
                }
                else
                {
                    Transform target = patrolPoints[Random.Range(0, patrolPoints.Count)];
                    targetPos = target.position;
                }

                SafeSetStopped(false);
                if (!TrySetDestination(targetPos)) { yield return null; continue; }

                while (state == State.Patrol && IsAgentReady() && agent.pathPending) yield return null;
                Vector3 lastPatrolPos = transform.position;
                float patrolStallTimer = 0f;
                while (state == State.Patrol && IsAgentReady() &&
                       !HasReachedDestination(0.25f))
                {
                    if (UpdateRoamStallState(ref lastPatrolPos, ref patrolStallTimer))
                        break;
                    yield return null;
                }

                yield return null;
            }
        }

        IEnumerator Investigate()
        {
            if (!hasNoise && !hasInvestigateTarget) { ChangeState(State.Patrol); yield break; }
            if (!IsAgentReady()) { ChangeState(State.Patrol); yield break; }

            Vector3 target = hasInvestigateTarget ? investigateTargetPos : lastNoisePos;
            SafeSetStopped(false);
            if (!TrySetDestination(target)) { ChangeState(State.Patrol); yield break; }

            while (state == State.Investigate && IsAgentReady() && agent.pathPending) yield return null;
            Vector3 lastInvestigatePos = transform.position;
            float investigateStallTimer = 0f;
            while (state == State.Investigate && IsAgentReady() &&
                   !HasReachedDestination(0.35f))
            {
                if (UpdateRoamStallState(ref lastInvestigatePos, ref investigateStallTimer))
                    break;
                if (stablePlayerVisible || ShouldForceProximityChase(IsPlayerHidden()))
                {
                    lastSeenPlayerPos = player.position;
                    lastSeenPlayerTime = Time.time;
                    ChangeState(State.Chase);
                    yield break;
                }
                yield return null;
            }

            if (state != State.Investigate) yield break;
            if (stablePlayerVisible || ShouldForceProximityChase(IsPlayerHidden()))
            {
                lastSeenPlayerPos = player.position;
                lastSeenPlayerTime = Time.time;
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
            bool searchingWholeHouse = wholeHouseSearchMode && roamWholeHouseWhenPlayerLost;
            if (searchingWholeHouse)
                activeSearchDuration = Mathf.Max(activeSearchDuration, wholeHouseSearchDuration);

            if (searchingWholeHouse && wholeHouseSearchLastKnownPos != Vector3.zero && IsAgentReady())
            {
                SafeSetStopped(false);
                if (TrySetDestination(wholeHouseSearchLastKnownPos))
                {
                    float approachTime = 0f;
                    Vector3 lastApproachPos = transform.position;
                    float approachStallTimer = 0f;
                    while (state == State.Search && approachTime < 2.25f && IsAgentReady() && !HasReachedDestination(0.45f))
                    {
                        approachTime += Time.deltaTime;
                        if (UpdateRoamStallState(ref lastApproachPos, ref approachStallTimer))
                            break;
                        yield return null;
                    }
                }
            }

            while (state == State.Search && elapsed < activeSearchDuration)
            {
                // Normal wandering around the noise center
                Vector3 unlockedDoorRoomPoint = transform.position;
                bool usedUnlockedDoorRoom = ShouldBiasToUnlockedKeyDoorRoom() &&
                                            TryGetUnlockedKeyDoorRoomTarget(out unlockedDoorRoomPoint);
                if (usedUnlockedDoorRoom)
                {
                    TrySetDestination(unlockedDoorRoomPoint);
                }
                else if (searchingWholeHouse)
                {
                    Vector3 searchCenter = (Random.value < lostSightLastKnownBias)
                        ? wholeHouseSearchLastKnownPos
                        : roamCenter;
                    float radius = Mathf.Max(freeRoamRadius, searchRadius * outwardSearchMultiplier);

                    if (TryGetRandomRoamPoint(searchCenter, radius, out var roamPoint) ||
                        TryGetRandomRoamPoint(roamCenter, Mathf.Max(radius, freeRoamRadius), out roamPoint))
                    {
                        TrySetDestination(roamPoint);
                    }
                }
                else
                {
                    float localPhase = activeSearchDuration * Mathf.Clamp01(roomSuspicionPortion);
                    float radius = elapsed < localPhase ? searchRadius : (searchRadius * outwardSearchMultiplier);
                    if (TryGetRandomRoamPoint(center, radius, out var roamPoint))
                        TrySetDestination(roamPoint);
                }

                float searchWait = Random.Range(0.6f, 1.2f);
                yield return new WaitForSeconds(searchWait);
                elapsed += searchWait;
            }

            hasNoise = false;
            hasInvestigateTarget = false;
            wholeHouseSearchMode = false;
            ChangeState(State.Patrol);
        }

        IEnumerator Chase()
        {
            while (state == State.Chase)
            {
                // If player is dead/disabled, stop chasing
                if (player == null || !player.gameObject.activeInHierarchy)
                {
                    SafeSetStopped(true);
                    yield break;
                }

                // Hiding is the only hard stop when persistent chase is enabled.
                if (IsPlayerHidden())
                {
                    hasNoise = false;
                    hasInvestigateTarget = false;
                    wholeHouseSearchMode = false;
                    ChangeState(State.Patrol);
                    yield break;
                }

                bool rawCanSee = CanSeePlayer();
                bool canSee = stablePlayerVisible || rawCanSee;
                Vector3 residentFlat = transform.position;
                residentFlat.y = 0f;
                Vector3 playerFlat = player.position;
                playerFlat.y = 0f;
                float horizontalDistanceToPlayer = Vector3.Distance(residentFlat, playerFlat);
                if (rawCanSee)
                {
                    stablePlayerVisible = true;
                    visibleAccum = Mathf.Max(visibleAccum, Mathf.Max(Time.deltaTime, visionAcquireSeconds));
                    hiddenAccum = 0f;
                }

                if (canSee)
                {
                    lastSeenPlayerPos = player.position;
                    lastSeenPlayerTime = Time.time;
                    chaseLostSightTimer = 0f;
                    if (relentlessVisualChase)
                        StartVisualChaseWindow();
                }
                else
                {
                    chaseLostSightTimer += Time.deltaTime;
                }

                if (loseChaseWhenFar && !canSee && horizontalDistanceToPlayer > maxChaseDistance)
                    chaseFarTimer += Time.deltaTime;
                else
                    chaseFarTimer = 0f;

                if (IsAgentReady())
                {
                    SafeSetStopped(false);
                    Vector3 chaseTarget = canSee ? player.position : lastSeenPlayerPos;
                    // Throttle expensive re-pathing to ~10 Hz; UpdateDoorwayAssist still
                    // runs every frame so doorway nudges remain responsive.
                    bool moved = agent.hasPath;
                    if (Time.time >= nextChaseRepathAt)
                    {
                        nextChaseRepathAt = Time.time + 0.1f;
                        moved = TrySetDestination(chaseTarget) ||
                                TryUseSquareFuseCorridorBridge(chaseTarget, allowWarp: false);
                        if (!moved && NavMesh.SamplePosition(chaseTarget, out var fallbackHit, 2.5f, NavMesh.AllAreas))
                            moved = TrySetDestination(fallbackHit.position);
                    }
                    UpdateDoorwayAssist(true, moved, chaseTarget);
                }

                if (loseChaseWhenFar && chaseFarTimer >= Mathf.Max(0.1f, farLoseDelay))
                {
                    BeginWholeHouseSearch(lastSeenPlayerPos, searchDuration * 0.85f);
                    ChangeState(State.Search);
                    yield break;
                }

                float lastSeenMemorySeconds = Mathf.Max(
                    Mathf.Max(0.25f, chaseMemorySeconds),
                    Mathf.Max(0.25f, lostSightPursuitSeconds));

                if (!canSee &&
                    chaseLostSightTimer >= Mathf.Max(0.05f, lostSightToSearchDelay) &&
                    chaseLostSightTimer >= lastSeenMemorySeconds)
                {
                    BeginWholeHouseSearch(lastSeenPlayerPos);
                    ChangeState(State.Search);
                    yield break;
                }

                yield return null;
            }
        }

        void TryRandomizeSpawn()
        {
            if (!randomizeSpawnOnStart) return;
            if (agent == null || !agent.enabled || !agent.gameObject.activeInHierarchy) return;

            if (!agent.isOnNavMesh)
            {
                if (!NavMesh.SamplePosition(transform.position, out var startHit, 8f, NavMesh.AllAreas))
                    return;
                if (Vector3.Distance(transform.position, startHit.position) > 0.12f)
                    return;
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

            // Runtime spawn randomization was removed because it looks like teleporting
            // when the Resident is visible near scene start.
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

                if (PathCrossesClosedDoor(path))
                    return false;
            }

            if (minWallClearance > 0f && NavMesh.FindClosestEdge(pos, out var edgeHit, NavMesh.AllAreas))
            {
                if (edgeHit.distance < minWallClearance) return false;
            }

            return true;
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

            armL = FindChildRecursive(visualRoot, "Arm_L");
            armR = FindChildRecursive(visualRoot, "Arm_R");
            legL = FindChildRecursive(visualRoot, "Leg_L");
            legR = FindChildRecursive(visualRoot, "Leg_R");
            head = FindChildRecursive(visualRoot, "Head");
            NormalizeHumanoidRig();

            if (visualRoot == null) return;
            visualBaseLocalPos = visualRoot.localPosition;
            visualBaseLocalRot = visualRoot.localRotation;

            if (armL != null) { armLBaseLocalPos = armL.localPosition; armLBaseRot = armL.localRotation; }
            if (armR != null) { armRBaseLocalPos = armR.localPosition; armRBaseRot = armR.localRotation; }
            if (legL != null) { legLBaseLocalPos = legL.localPosition; legLBaseRot = legL.localRotation; }
            if (legR != null) { legRBaseLocalPos = legR.localPosition; legRBaseRot = legR.localRotation; }
            if (head != null) headBaseRot = head.localRotation;

            SetupFaceRig();

        }

        void NormalizeHumanoidRig()
        {
            if (visualRoot == null) return;

            Transform chest    = FindChildRecursive(visualRoot, "Chest");
            Transform headPart = FindChildRecursive(visualRoot, "Head");

            if (chest != null)
            {
                chest.localPosition = new Vector3(0f, 1.22f, 0f);
                chest.localScale    = new Vector3(0.34f, 0.50f, 0.22f);
                chest.localRotation = Quaternion.identity;
            }
            if (headPart != null)
            {
                headPart.localPosition = new Vector3(0f, 1.82f, 0f);
                headPart.localScale    = new Vector3(0.22f, 0.26f, 0.22f);
                headPart.localRotation = Quaternion.identity;
            }
            if (armL != null)
            {
                armL.localPosition = new Vector3(-0.24f, 0.94f, 0f);
                armL.localScale    = new Vector3(0.085f, 1.12f, 0.095f);
                armL.localRotation = Quaternion.Euler(0f, 0f, 8f);
            }
            if (armR != null)
            {
                armR.localPosition = new Vector3(0.24f, 0.94f, 0f);
                armR.localScale    = new Vector3(0.085f, 1.12f, 0.095f);
                armR.localRotation = Quaternion.Euler(0f, 0f, -8f);
            }
            Vector3 legLocalScale = new Vector3(0.105f, 1.54f, 0.11f);
            float legLocalX = 0.105f;
            float legLocalY = 0.30f;
            if (legL != null)
            {
                legL.localPosition = new Vector3(-legLocalX, legLocalY, 0f);
                legL.localScale    = legLocalScale;
                legL.localRotation = Quaternion.identity;
            }
            if (legR != null)
            {
                legR.localPosition = new Vector3(legLocalX, legLocalY, 0f);
                legR.localScale    = legLocalScale;
                legR.localRotation = Quaternion.identity;
            }
        }

        void SetupFaceRig()
        {
            if (head == null)
                return;

            faceSeed = Mathf.Abs(GetInstanceID() * 0.173f);
            faceRig = head.Find("FaceRig");
            if (faceRig == null)
            {
                faceRig = new GameObject("FaceRig").transform;
                faceRig.SetParent(head, false);
            }

            faceRig.localPosition = Vector3.zero;
            faceRig.localRotation = Quaternion.identity;
            faceRig.localScale = Vector3.one;

            EnsureFacePrimitive(faceRig, "EyeSocket_L", PrimitiveType.Cube,
                new Vector3(-0.17f, 0.09f, 0.39f), Quaternion.Euler(0f, -6f, 0f), new Vector3(0.20f, 0.12f, 0.05f),
                GetFaceVoidMaterial());
            EnsureFacePrimitive(faceRig, "EyeSocket_R", PrimitiveType.Cube,
                new Vector3(0.17f, 0.09f, 0.39f), Quaternion.Euler(0f, 6f, 0f), new Vector3(0.20f, 0.12f, 0.05f),
                GetFaceVoidMaterial());
            EnsureFacePrimitive(faceRig, "Eye_L", PrimitiveType.Sphere,
                new Vector3(-0.17f, 0.09f, 0.44f), Quaternion.identity, new Vector3(0.13f, 0.12f, 0.09f),
                GetFaceEyeMaterial());
            EnsureFacePrimitive(faceRig, "Eye_R", PrimitiveType.Sphere,
                new Vector3(0.17f, 0.09f, 0.44f), Quaternion.identity, new Vector3(0.13f, 0.12f, 0.09f),
                GetFaceEyeMaterial());

            leftPupil = EnsureFacePrimitive(faceRig, "Pupil_L", PrimitiveType.Sphere,
                new Vector3(-0.17f, 0.09f, 0.49f), Quaternion.identity, new Vector3(0.05f, 0.05f, 0.03f),
                GetFacePupilMaterial());
            rightPupil = EnsureFacePrimitive(faceRig, "Pupil_R", PrimitiveType.Sphere,
                new Vector3(0.17f, 0.09f, 0.49f), Quaternion.identity, new Vector3(0.05f, 0.05f, 0.03f),
                GetFacePupilMaterial());

            leftBrow = EnsureFacePrimitive(faceRig, "Brow_L", PrimitiveType.Cube,
                new Vector3(-0.17f, 0.21f, 0.41f), Quaternion.Euler(0f, 0f, -18f), new Vector3(0.22f, 0.04f, 0.05f),
                GetFaceVoidMaterial());
            rightBrow = EnsureFacePrimitive(faceRig, "Brow_R", PrimitiveType.Cube,
                new Vector3(0.17f, 0.21f, 0.41f), Quaternion.Euler(0f, 0f, 18f), new Vector3(0.22f, 0.04f, 0.05f),
                GetFaceVoidMaterial());

            mouthVoid = EnsureFacePrimitive(faceRig, "MouthVoid", PrimitiveType.Cube,
                new Vector3(0f, -0.18f, 0.43f), Quaternion.identity, new Vector3(0.36f, 0.06f, 0.08f),
                GetFaceVoidMaterial());

            upperTeeth = EnsureFaceAnchor(faceRig, "UpperTeeth", new Vector3(0f, -0.12f, 0.47f));
            lowerTeeth = EnsureFaceAnchor(faceRig, "LowerTeeth", new Vector3(0f, -0.22f, 0.47f));

            EnsureFacePrimitive(upperTeeth, "FangOuter_L", PrimitiveType.Cube,
                new Vector3(-0.13f, -0.06f, 0f), Quaternion.Euler(0f, 0f, 10f), new Vector3(0.045f, 0.14f, 0.04f),
                GetFaceToothMaterial());
            EnsureFacePrimitive(upperTeeth, "FangInner_L", PrimitiveType.Cube,
                new Vector3(-0.05f, -0.05f, 0f), Quaternion.identity, new Vector3(0.035f, 0.11f, 0.03f),
                GetFaceToothMaterial());
            EnsureFacePrimitive(upperTeeth, "FangInner_R", PrimitiveType.Cube,
                new Vector3(0.05f, -0.05f, 0f), Quaternion.identity, new Vector3(0.035f, 0.11f, 0.03f),
                GetFaceToothMaterial());
            EnsureFacePrimitive(upperTeeth, "FangOuter_R", PrimitiveType.Cube,
                new Vector3(0.13f, -0.06f, 0f), Quaternion.Euler(0f, 0f, -10f), new Vector3(0.045f, 0.14f, 0.04f),
                GetFaceToothMaterial());

            EnsureFacePrimitive(lowerTeeth, "FangOuter_L", PrimitiveType.Cube,
                new Vector3(-0.11f, 0.05f, 0f), Quaternion.Euler(0f, 0f, -10f), new Vector3(0.04f, 0.11f, 0.035f),
                GetFaceToothMaterial());
            EnsureFacePrimitive(lowerTeeth, "FangInner_L", PrimitiveType.Cube,
                new Vector3(-0.04f, 0.04f, 0f), Quaternion.identity, new Vector3(0.032f, 0.09f, 0.03f),
                GetFaceToothMaterial());
            EnsureFacePrimitive(lowerTeeth, "FangInner_R", PrimitiveType.Cube,
                new Vector3(0.04f, 0.04f, 0f), Quaternion.identity, new Vector3(0.032f, 0.09f, 0.03f),
                GetFaceToothMaterial());
            EnsureFacePrimitive(lowerTeeth, "FangOuter_R", PrimitiveType.Cube,
                new Vector3(0.11f, 0.05f, 0f), Quaternion.Euler(0f, 0f, 10f), new Vector3(0.04f, 0.11f, 0.035f),
                GetFaceToothMaterial());

            if (leftPupil != null) leftPupilBaseLocalPos = leftPupil.localPosition;
            if (rightPupil != null) rightPupilBaseLocalPos = rightPupil.localPosition;
            if (leftBrow != null) leftBrowBaseLocalRot = leftBrow.localRotation;
            if (rightBrow != null) rightBrowBaseLocalRot = rightBrow.localRotation;
            if (mouthVoid != null) mouthVoidBaseLocalScale = mouthVoid.localScale;
            if (upperTeeth != null) upperTeethBaseLocalPos = upperTeeth.localPosition;
            if (lowerTeeth != null) lowerTeethBaseLocalPos = lowerTeeth.localPosition;
        }

        Transform EnsureFaceAnchor(Transform parent, string name, Vector3 localPosition)
        {
            Transform anchor = parent.Find(name);
            if (anchor == null)
            {
                anchor = new GameObject(name).transform;
                anchor.SetParent(parent, false);
            }

            anchor.localPosition = localPosition;
            anchor.localRotation = Quaternion.identity;
            anchor.localScale = Vector3.one;
            return anchor;
        }

        Transform EnsureFacePrimitive(Transform parent, string name, PrimitiveType primitiveType, Vector3 localPosition, Quaternion localRotation, Vector3 localScale, Material material)
        {
            Transform child = parent.Find(name);
            if (child == null)
            {
                GameObject primitive = GameObject.CreatePrimitive(primitiveType);
                primitive.name = name;
                child = primitive.transform;
                child.SetParent(parent, false);
            }

            child.localPosition = localPosition;
            child.localRotation = localRotation;
            child.localScale = localScale;

            Collider primitiveCollider = child.GetComponent<Collider>();
            if (primitiveCollider != null)
                primitiveCollider.enabled = false;

            Renderer renderer = child.GetComponent<Renderer>();
            if (renderer != null && material != null)
            {
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }

            return child;
        }

        static Material GetFaceVoidMaterial()
        {
            if (faceVoidSharedMaterial == null)
                faceVoidSharedMaterial = CreateFaceMaterial("ResidentFaceVoid", new Color(0.04f, 0.01f, 0.01f, 1f), Color.black, 0.05f);
            return faceVoidSharedMaterial;
        }

        static Material GetFaceEyeMaterial()
        {
            if (faceEyeSharedMaterial == null)
                faceEyeSharedMaterial = CreateFaceMaterial("ResidentFaceEye", new Color(0.88f, 0.82f, 0.76f, 1f), new Color(0.08f, 0.02f, 0.02f), 0.12f);
            return faceEyeSharedMaterial;
        }

        static Material GetFacePupilMaterial()
        {
            if (facePupilSharedMaterial == null)
                facePupilSharedMaterial = CreateFaceMaterial("ResidentFacePupil", new Color(0.48f, 0.03f, 0.03f, 1f), new Color(1.25f, 0.08f, 0.08f), 0.08f);
            return facePupilSharedMaterial;
        }

        static Material GetFaceToothMaterial()
        {
            if (faceToothSharedMaterial == null)
                faceToothSharedMaterial = CreateFaceMaterial("ResidentFaceTooth", new Color(0.76f, 0.70f, 0.59f, 1f), Color.black, 0.02f);
            return faceToothSharedMaterial;
        }

        static Material CreateFaceMaterial(string materialName, Color baseColor, Color emissionColor, float smoothness)
        {
            Shader shader = Shader.Find("Standard");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("HDRP/Lit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Unlit/Color");

            Material material = new Material(shader);
            material.name = materialName;

            if (material.HasProperty("_Color"))
                material.SetColor("_Color", baseColor);
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", baseColor);
            if (material.HasProperty("_Smoothness"))
                material.SetFloat("_Smoothness", smoothness);
            if (material.HasProperty("_Metallic"))
                material.SetFloat("_Metallic", 0f);
            if (emissionColor.maxColorComponent > 0.001f && material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", emissionColor);
            }

            return material;
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
            if (killAttackActive)
            {
                UpdateKillAttackAnimation();
                UpdateFaceAnimation(1f, true, dt);
                return;
            }

            float velocity = IsAgentReady() ? agent.velocity.magnitude : 0f;
            float speedRef = IsAgentReady() ? Mathf.Max(0.1f, agent.speed) : 1f;
            // Smooth move01 so the animation eases in/out rather than snapping
            float targetMove01 = Mathf.Clamp01(velocity / speedRef);
            smoothedMove01 = Mathf.MoveTowards(smoothedMove01, targetMove01, dt * 2.2f);
            float move01   = smoothedMove01;
            bool  chasing  = state == State.Chase;

            float stairBlend = GetStairBlend();
            float swingScale = Mathf.Lerp(1f, Mathf.Clamp01(1f - stairSwingReduction), stairBlend);
            float stairLift  = stairVisualLift * stairBlend;

            // Phase drives all cyclic motion — always ticks (even idle) for micro-sway
            float bobSpeed = Mathf.Lerp(walkBobSpeed * 0.35f,
                                        chasing ? runBobSpeed : walkBobSpeed, move01);
            motionPhase += dt * bobSpeed;

            // ── Body root ────────────────────────────────────────────────────
            // Double-frequency vertical bob (two footfalls per stride cycle)
            float bobAmp = Mathf.Lerp(0.001f, chasing ? runBobAmplitude : walkBobAmplitude, move01)
                         * Mathf.Lerp(1f, 0.78f, stairBlend);
            float bobY = Mathf.Sin(motionPhase * 2f) * bobAmp;

            // Lateral hip sway — minimal, uncanny stillness of the creature
            float hipSway = Mathf.Sin(motionPhase) * move01 * 0.010f;

            // Persistent idle micro-sway: very slow breathing-like motion even when still.
            float idleSwayPhase = Time.time * 0.45f;
            float idleSway = Mathf.Sin(idleSwayPhase) * (1f - move01) * 0.004f;

            visualRoot.localPosition = visualBaseLocalPos + new Vector3(hipSway + idleSway, bobY + stairLift, 0f);

            // Keep the torso mostly upright instead of permanently slanted.
            float idleLean     = 0f;
            float moveLean     = -Mathf.Lerp(0f, chaseForwardLeanDegrees, chasing ? move01 : move01 * 0.18f);
            float totalLean    = moveLean + idleLean;

            // Keep lateral tilt extremely subtle so the body reads straight.
            float idleTilt     = Mathf.Sin(idleSwayPhase * 0.7f) * (1f - move01) * 0.25f;
            float bodyTilt     = Mathf.Sin(motionPhase) * move01 * 0.35f + idleTilt;

            visualRoot.localRotation = visualBaseLocalRot * Quaternion.Euler(totalLean, 0f, bodyTilt);

            // ── Limbs ────────────────────────────────────────────────────────
            // Keep the cube limbs anchored at the shoulders/hips; otherwise the
            // center-pivot primitives read like they are detaching from the body.
            float stride   = Mathf.Sin(motionPhase);
            float swingMag = (chasing ? runLimbSwingDegrees : walkLimbSwingDegrees) * move01 * swingScale * 0.82f;

            // Counter-swing: L arm forward with R leg forward
            float armSwingL =  stride * swingMag * 0.72f;
            float armSwingR = -stride * swingMag * 0.72f;
            float legSwingL = -stride * swingMag * 0.78f;
            float legSwingR =  stride * swingMag * 0.78f;

            // Chase: arms come forward, but not so far that they clip into the chest.
            float chaseReach = chasing ? Mathf.Lerp(0f, 8f, move01) : 0f;

            // A-pose splay: small outward bias keeps wrists clear of the torso.
            const float armSplay = -9f;
            float legRoll = Mathf.Lerp(0.75f, 2f, move01) * swingScale;
            float stepLiftScale = (chasing ? 0.020f : 0.013f) * move01 * swingScale;

            if (armL != null)
            {
                Quaternion armLAnimatedRot = armLBaseRot * Quaternion.Euler(armSwingL + chaseReach, 0f, armSplay);
                armL.localRotation = armLAnimatedRot;
                armL.localPosition = GetAnchoredLimbPosition(armLBaseLocalPos, armLBaseRot, armLAnimatedRot, Mathf.Abs(armL.localScale.y));
            }
            if (armR != null)
            {
                Quaternion armRAnimatedRot = armRBaseRot * Quaternion.Euler(armSwingR + chaseReach, 0f, -armSplay);
                armR.localRotation = armRAnimatedRot;
                armR.localPosition = GetAnchoredLimbPosition(armRBaseLocalPos, armRBaseRot, armRAnimatedRot, Mathf.Abs(armR.localScale.y));
            }
            if (legL != null)
            {
                Quaternion legLAnimatedRot = legLBaseRot * Quaternion.Euler(legSwingL, 0f, legRoll);
                Vector3 legLAnimatedPos = GetAnchoredLimbPosition(legLBaseLocalPos, legLBaseRot, legLAnimatedRot, Mathf.Abs(legL.localScale.y));
                legL.localRotation = legLAnimatedRot;
                legL.localPosition = legLAnimatedPos + new Vector3(0f, stairLift * 0.14f + Mathf.Max(0f, -stride) * stepLiftScale, 0f);
            }
            if (legR != null)
            {
                Quaternion legRAnimatedRot = legRBaseRot * Quaternion.Euler(legSwingR, 0f, -legRoll);
                Vector3 legRAnimatedPos = GetAnchoredLimbPosition(legRBaseLocalPos, legRBaseRot, legRAnimatedRot, Mathf.Abs(legR.localScale.y));
                legR.localRotation = legRAnimatedRot;
                legR.localPosition = legRAnimatedPos + new Vector3(0f, stairLift * 0.14f + Mathf.Max(0f, stride) * stepLiftScale, 0f);
            }

            UpdateClawAnimation(move01, chasing);
            UpdateFaceAnimation(move01, chasing, dt);
        }

        void UpdateFaceAnimation(float move01, bool chasing, float dt)
        {
            if (head == null || leftPupil == null || rightPupil == null)
                return;

            Vector3 lookTarget = GetFaceLookTarget();
            Vector3 toTarget = lookTarget - head.position;
            if (toTarget.sqrMagnitude < 0.0001f)
                toTarget = transform.forward;

            Transform parent = head.parent != null ? head.parent : transform;
            Vector3 parentLocalDir = parent.InverseTransformDirection(toTarget.normalized);
            float planarMagnitude = Mathf.Max(0.0001f, Mathf.Sqrt((parentLocalDir.x * parentLocalDir.x) + (parentLocalDir.z * parentLocalDir.z)));
            float yaw = Mathf.Atan2(parentLocalDir.x, parentLocalDir.z) * Mathf.Rad2Deg;
            float pitch = -Mathf.Atan2(parentLocalDir.y, planarMagnitude) * Mathf.Rad2Deg;

            float threat = killAttackActive
                ? 1f
                : state == State.Chase
                    ? Mathf.Lerp(0.58f, 0.95f, move01)
                    : state == State.Search || state == State.Investigate
                        ? 0.34f
                        : 0.14f;

            float yawClamp = Mathf.Lerp(idleLookAroundMaxAngle * 0.45f, 34f, threat);
            float downClamp = Mathf.Lerp(12f, 20f, threat);
            float upClamp = Mathf.Lerp(18f, 28f, threat);
            Quaternion targetHeadRot = headBaseRot * Quaternion.Euler(
                Mathf.Clamp(pitch, -downClamp, upClamp),
                Mathf.Clamp(yaw, -yawClamp, yawClamp),
                -Mathf.Clamp(yaw * 0.14f, 0f - 5f, 5f));
            head.localRotation = Quaternion.Slerp(head.localRotation, targetHeadRot, dt * Mathf.Lerp(3f, 10f, threat));

            Vector3 headLocalDir = head.InverseTransformDirection(toTarget.normalized);
            Vector3 pupilOffset = new Vector3(
                Mathf.Clamp(headLocalDir.x, -0.85f, 0.85f) * 0.026f,
                Mathf.Clamp(headLocalDir.y, -0.75f, 0.75f) * 0.020f,
                Mathf.Clamp(headLocalDir.z, -0.4f, 1f) * 0.006f);
            leftPupil.localPosition = Vector3.Lerp(leftPupil.localPosition, leftPupilBaseLocalPos + pupilOffset, dt * 12f);
            rightPupil.localPosition = Vector3.Lerp(rightPupil.localPosition, rightPupilBaseLocalPos + pupilOffset, dt * 12f);

            if (leftBrow != null && rightBrow != null)
            {
                float browTwitch = Mathf.Sin((Time.time * 1.7f) + faceSeed) * Mathf.Lerp(1f, 3f, threat);
                leftBrow.localRotation = leftBrowBaseLocalRot * Quaternion.Euler(0f, 0f, -browTwitch);
                rightBrow.localRotation = rightBrowBaseLocalRot * Quaternion.Euler(0f, 0f, browTwitch);
            }

            float jawNoise = Mathf.Sin((Time.time * Mathf.Lerp(1.2f, 5.6f, threat)) + (faceSeed * 0.35f)) * 0.5f + 0.5f;
            float mouthOpen = 0.018f + (threat * 0.06f) + (jawNoise * threat * 0.018f);
            if (mouthVoid != null)
            {
                mouthVoid.localScale = new Vector3(
                    mouthVoidBaseLocalScale.x,
                    mouthVoidBaseLocalScale.y + mouthOpen,
                    mouthVoidBaseLocalScale.z);
            }
            if (upperTeeth != null)
                upperTeeth.localPosition = upperTeethBaseLocalPos + new Vector3(0f, mouthOpen * 0.18f, 0f);
            if (lowerTeeth != null)
                lowerTeeth.localPosition = lowerTeethBaseLocalPos + new Vector3(0f, mouthOpen * -0.22f, 0f);
        }

        Vector3 GetFaceLookTarget()
        {
            if (killAttackActive)
                return killAttackFocusPoint;

            bool playerVisibleThreat = player != null &&
                                      !IsPlayerHidden() &&
                                      (state == State.Chase || stablePlayerVisible || (Time.time - lastSeenPlayerTime) <= 0.35f);
            if (playerVisibleThreat)
                return GetPlayerClosestBodyPoint(head != null ? head.position : transform.position);

            if (state == State.Investigate && hasInvestigateTarget)
                return investigateTargetPos + Vector3.up * 1.5f;

            if ((state == State.Search || wholeHouseSearchMode) && lastSeenPlayerTime > -998f)
                return lastSeenPlayerPos + Vector3.up * 1.55f;

            if (IsAgentReady())
            {
                if (agent.hasPath)
                    return agent.steeringTarget + Vector3.up * 1.45f;
                if (agent.desiredVelocity.sqrMagnitude > 0.01f)
                    return transform.position + (agent.desiredVelocity.normalized * 4f) + (Vector3.up * 1.45f);
            }

            float idleYaw = Mathf.Sin((Time.time * 0.75f) + faceSeed) * idleLookAroundMaxAngle;
            float idlePitch = Mathf.Cos((Time.time * 0.42f) + (faceSeed * 0.31f)) * 8f;
            Vector3 idleDirection = Quaternion.Euler(idlePitch, idleYaw, 0f) * transform.forward;
            return (head != null ? head.position : transform.position + Vector3.up * 1.6f) + (idleDirection * 4f);
        }

        Vector3 GetAnchoredLimbPosition(Vector3 baseLocalPos, Quaternion baseRot, Quaternion animatedRot, float localLength)
        {
            float halfLength = Mathf.Max(0.01f, localLength * 0.5f);
            Vector3 anchor = baseLocalPos + (baseRot * Vector3.up) * halfLength;
            return anchor - (animatedRot * Vector3.up) * halfLength;
        }

        // ── CLAW ANIMATION ────────────────────────────────────────────────────
        // Three states:
        //   Idle  – slow independent per-claw twitch (organic, unsettling)
        //   Walk  – subtle flex in sync with stride
        //   Chase – claws spread wide and tilt forward (predatory threat display)
        void UpdateClawAnimation(float move01, bool chasing)
        {
            if (clawShaftsL == null || clawShaftsL.Length < 4) return;

            for (int i = 0; i < 4; i++)
            {
                if (clawShaftsL[i] == null && clawShaftsR[i] == null) continue;

                // Unique slow phase per claw — no two claws twitch at the same rate
                float uniqueSpeed = 0.55f + i * 0.18f;
                float phaseOff    = i * 1.571f;   // 90° spacing

                // IDLE TWITCH: independent slow oscillation, strongest when still
                float idleTwitch = Mathf.Sin(Time.time * uniqueSpeed + phaseOff)
                                 * (1f - move01) * 7f;

                // WALK FLEX: claws curl lightly with each stride
                float walkFlex = Mathf.Sin(motionPhase * 2f + phaseOff * 0.5f)
                               * move01 * 3.5f;

                // CHASE SPREAD: claws tilt forward + outer pair fans wider
                float outerFactor   = (i == 0 || i == 3) ? 1.0f : 0.55f;
                float chaseForward  = chasing ? Mathf.Lerp(0f, 18f * outerFactor, move01) : 0f;
                float chaseSpreadZ  = chasing ? Mathf.Lerp(0f,  9f * outerFactor, move01) : 0f;

                float totalX = idleTwitch + walkFlex + chaseForward;
                float totalZ = chaseSpreadZ;

                if (clawShaftsL[i] != null)
                    clawShaftsL[i].localRotation = clawBasesL[i] * Quaternion.Euler(totalX, 0f,  totalZ);
                if (clawShaftsR[i] != null)
                    clawShaftsR[i].localRotation = clawBasesR[i] * Quaternion.Euler(totalX, 0f, -totalZ);
            }
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(lastNoisePos, lastNoiseIntensity);
        }
    }
}
