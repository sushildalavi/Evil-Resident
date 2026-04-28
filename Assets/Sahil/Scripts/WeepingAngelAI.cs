using UnityEngine;
using UnityEngine.AI;
using Sushil.AI;
using Sushil.Systems;

[DisallowMultipleComponent]
[RequireComponent(typeof(NavMeshAgent))]
public class WeepingAngelAI : MonoBehaviour
{
    [Header("References")]
    public Transform playerTransform;
    public Camera playerCamera;
    public bool autoFindPlayer = true;
    public bool autoFindMainCamera = true;

    [Header("Movement")]
    [Tooltip("Base move speed before multiplier. Auto-filled from RohitFPSController walkSpeed when enabled.")]
    public float baseMoveSpeed = 5f;
    [Tooltip("Runtime speed = baseMoveSpeed * movementSpeedMultiplier")]
    [Min(0f)] public float movementSpeedMultiplier = 0.6f;
    [Min(0.1f)] public float stoppingDistance = 1.25f;
    [Tooltip("Use the player's normal walk speed, never sprint speed, as the movement baseline.")]
    public bool deriveBaseSpeedFromPlayerWalkSpeed = true;
    [Tooltip("Always cap angel speed below player walk speed.")]
    [Range(0.1f, 1f)] public float maxRelativeToPlayerWalkSpeed = 0.8f;

    [Header("Look Detection")]
    [Min(0.5f)] public float maxReactionDistance = 24f;
    [Min(0f)] public float fieldOfViewTolerance = 4f;
    [Tooltip("Geometry that can block line of sight from camera to this enemy.")]
    public LayerMask lineOfSightObstructionMask = ~0;
    [Tooltip("Height offset for the point the camera must see.")]
    public float visibilityPointHeight = 1.15f;
    [Tooltip("If true, angel only chases when it has direct line of sight to player (walls block chase).")]
    public bool requireLineOfSightToChase = true;
    [Tooltip("If camera cannot be resolved, use player body forward as a fallback so freeze logic still works in builds.")]
    public bool allowBodyForwardFallback = true;

    [Header("Facing")]
    public bool facePlayerWhenFrozen = true;
    [Min(30f)] public float turnSpeedDegrees = 360f;

    [Header("Debug")]
    public bool debugDrawLineOfSight;
    [Tooltip("Logs in-front, viewport, line-of-sight, and looked-at state in player builds.")]
    public bool debugLogLookDetectionInBuild;
    [Min(0.1f)] public float debugLookLogInterval = 0.5f;

    [Header("Kill")]
    public bool killPlayerOnReach = true;
    [Min(0.1f)] public float killDistance = 0.8f;
    public string killReason = "The Weeping Angel Reached you.";

    [Header("Audio")]
    [Tooltip("Enable movement and kill audio for this specific angel instance.")]
    public bool enableAngelAudio = false;
    public AudioSource movementAudioSource;
    public AudioClip movementClip;
    [Range(0f, 3f)] public float movementVolume = 0.75f;
    [Range(0.5f, 2f)] public float movementPitch = 1f;
    [Min(0.1f)] public float movementMinDistance = 1.75f;
    [Min(0.2f)] public float movementMaxDistance = 14f;
    public AudioSource killScreamAudioSource;
    public AudioClip killScreamClip;
    [Range(0f, 3f)] public float killScreamVolume = 1f;
    [Range(0.5f, 2f)] public float killScreamPitch = 1f;

    [Header("Animation")]
    [Tooltip("Optional movement/idle animation bridge driven by this AI's existing move/freeze logic.")]
    public WeepingAngelAnimationBridge animationBridge;

    private NavMeshAgent agent;
    private RohitFPSController playerController;
    private Collider[] angelColliders;
    private Transform collisionIgnoredForPlayer;
    private float nextLookDebugLogTime;
    private bool hasPreviousLookDebugState;
    private bool previousInFront;
    private bool previousInViewport;
    private bool previousLineOfSightClear;
    private bool previousLookedAt;
    private float nextMissingReferenceLogTime;
    private bool playedKillScream;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        angelColliders = GetComponentsInChildren<Collider>(true);
        if (animationBridge == null)
            animationBridge = GetComponent<WeepingAngelAnimationBridge>();
        ResolveReferences();
        ApplySpeedFromPlayerWalk();
        ConfigurePlayerCollisionIgnore();
        ConfigureAudioSources();

        agent.stoppingDistance = stoppingDistance;
        agent.updateUpAxis = true;
    }

    private void OnValidate()
    {
        stoppingDistance = Mathf.Max(0.1f, stoppingDistance);
        maxReactionDistance = Mathf.Max(0.5f, maxReactionDistance);
        movementSpeedMultiplier = Mathf.Max(0f, movementSpeedMultiplier);
        turnSpeedDegrees = Mathf.Max(30f, turnSpeedDegrees);
        maxRelativeToPlayerWalkSpeed = Mathf.Clamp(maxRelativeToPlayerWalkSpeed, 0.1f, 1f);
        killDistance = Mathf.Max(0.1f, killDistance);
        movementMinDistance = Mathf.Max(0.1f, movementMinDistance);
        movementMaxDistance = Mathf.Max(movementMinDistance + 0.1f, movementMaxDistance);
        movementPitch = Mathf.Clamp(movementPitch, 0.5f, 2f);
        killScreamPitch = Mathf.Clamp(killScreamPitch, 0.5f, 2f);
    }

    private void OnDisable()
    {
        StopMovementAudio();
        UpdateAnimationState(false);
    }

    private void Update()
    {
        ResolveReferences();
        ApplySpeedFromPlayerWalk();
        ConfigurePlayerCollisionIgnore();

        if (playerTransform == null)
        {
            StopImmediate();
            return;
        }

        if (killPlayerOnReach && TryKillPlayerIfReached())
        {
            StopImmediate();
            return;
        }

        float currentSpeed = Mathf.Max(0f, baseMoveSpeed * movementSpeedMultiplier);
        if (playerController != null)
            currentSpeed = Mathf.Min(currentSpeed, Mathf.Max(0.1f, playerController.walkSpeed * maxRelativeToPlayerWalkSpeed));

        agent.speed = currentSpeed;
        agent.stoppingDistance = stoppingDistance;

        bool lookedAt = IsLookedAtByPlayer(out bool inFront, out bool inViewport, out bool lineOfSightClear);
        MaybeLogLookDetection(inFront, inViewport, lineOfSightClear, lookedAt);
        if (lookedAt)
        {
            StopImmediate();

            if (facePlayerWhenFrozen)
                RotateToward(playerTransform.position);

            return;
        }

        bool moved = MoveTowardPlayer();
        UpdateMovementAudio(moved);
        UpdateAnimationState(moved);
    }

    private void ResolveReferences()
    {
        if (playerTransform == null && autoFindPlayer)
        {
            if (playerController == null)
                playerController = FindFirstObjectByType<RohitFPSController>();

            if (playerController != null)
                playerTransform = playerController.transform;
            else
            {
                GameObject taggedPlayer = GameObject.FindGameObjectWithTag("Player");
                if (taggedPlayer != null)
                    playerTransform = taggedPlayer.transform;
            }
        }

        if (playerController == null && playerTransform != null)
            playerController = playerTransform.GetComponent<RohitFPSController>() ?? playerTransform.GetComponentInParent<RohitFPSController>();

        if (playerTransform == null && playerController != null)
            playerTransform = playerController.transform;

        if (playerController != null)
        {
            Camera controllerCamera = ResolveCameraFromController(playerController);
            if (controllerCamera != null)
                playerCamera = controllerCamera;
        }

        if (playerCamera == null && autoFindMainCamera)
            playerCamera = Camera.main;

        if (playerCamera == null)
            playerCamera = FindFirstObjectByType<Camera>();

        MaybeLogMissingReferences();
    }

    private void ApplySpeedFromPlayerWalk()
    {
        if (!deriveBaseSpeedFromPlayerWalkSpeed)
            return;

        if (playerController == null && playerTransform != null)
            playerController = playerTransform.GetComponent<RohitFPSController>();

        if (playerController != null && playerController.walkSpeed > 0.01f)
            baseMoveSpeed = playerController.walkSpeed;
    }

    private bool IsLookedAtByPlayer(out bool inFront, out bool inViewport, out bool lineOfSightClear)
    {
        inFront = false;
        inViewport = false;
        lineOfSightClear = false;

        if (playerCamera == null)
            return IsLookedAtByPlayerBodyForwardFallback(out inFront, out inViewport, out lineOfSightClear);

        Vector3 cameraPos = playerCamera.transform.position;
        Vector3 targetPoint = transform.position + Vector3.up * visibilityPointHeight;
        Vector3 toTarget = targetPoint - cameraPos;

        float distance = toTarget.magnitude;
        if (distance > maxReactionDistance)
            return false;

        if (distance < 0.001f)
        {
            inFront = true;
            inViewport = true;
            lineOfSightClear = true;
            return true;
        }

        Vector3 dir = toTarget / distance;
        float forwardDot = Vector3.Dot(playerCamera.transform.forward, dir);
        if (forwardDot <= 0f)
            return false;
        inFront = true;

        float halfFov = playerCamera.fieldOfView * 0.5f + fieldOfViewTolerance;
        float angle = Vector3.Angle(playerCamera.transform.forward, dir);
        if (angle > halfFov)
            return false;

        Vector3 viewportPoint = playerCamera.WorldToViewportPoint(targetPoint);
        inViewport = viewportPoint.z > 0f &&
                     viewportPoint.x >= 0f && viewportPoint.x <= 1f &&
                     viewportPoint.y >= 0f && viewportPoint.y <= 1f;
        if (!inViewport)
            return false;

        bool blocked = Physics.Linecast(cameraPos, targetPoint, out RaycastHit hit, lineOfSightObstructionMask, QueryTriggerInteraction.Ignore)
                       && hit.transform != null
                       && hit.transform != transform
                       && !hit.transform.IsChildOf(transform)
                       && !IsPlayerOrChild(hit.transform)
                       && !IsCameraOrChild(hit.transform);

        if (debugDrawLineOfSight)
            Debug.DrawLine(cameraPos, targetPoint, blocked ? Color.red : Color.green);

        lineOfSightClear = !blocked;
        return lineOfSightClear;
    }

    private bool IsLookedAtByPlayerBodyForwardFallback(out bool inFront, out bool inViewport, out bool lineOfSightClear)
    {
        inFront = false;
        inViewport = false;
        lineOfSightClear = false;

        if (!allowBodyForwardFallback || playerTransform == null)
            return false;

        Vector3 eyePos = GetPlayerAimPoint();
        Vector3 targetPoint = transform.position + Vector3.up * visibilityPointHeight;
        Vector3 toTarget = targetPoint - eyePos;

        float distance = toTarget.magnitude;
        if (distance > maxReactionDistance)
            return false;
        if (distance < 0.001f)
        {
            inFront = true;
            inViewport = true;
            lineOfSightClear = true;
            return true;
        }

        Vector3 dir = toTarget / distance;
        Vector3 forward = playerTransform.forward;
        float forwardDot = Vector3.Dot(forward, dir);
        if (forwardDot <= 0f)
            return false;
        inFront = true;

        float fallbackHalfFov = 50f + fieldOfViewTolerance;
        float angle = Vector3.Angle(forward, dir);
        if (angle > fallbackHalfFov)
            return false;

        inViewport = true;
        bool blocked = Physics.Linecast(eyePos, targetPoint, out RaycastHit hit, lineOfSightObstructionMask, QueryTriggerInteraction.Ignore)
                       && hit.transform != null
                       && hit.transform != transform
                       && !hit.transform.IsChildOf(transform)
                       && !IsPlayerOrChild(hit.transform)
                       && !IsCameraOrChild(hit.transform);
        lineOfSightClear = !blocked;
        return lineOfSightClear;
    }

    private bool MoveTowardPlayer()
    {
        if (requireLineOfSightToChase && !HasLineOfSightToPlayer())
        {
            StopImmediate();
            return false;
        }

        Vector3 toPlayer = playerTransform.position - transform.position;
        toPlayer.y = 0f;

        if (toPlayer.sqrMagnitude <= stoppingDistance * stoppingDistance)
        {
            StopImmediate();
            return false;
        }

        if (agent.enabled && agent.isOnNavMesh)
        {
            agent.isStopped = false;
            agent.SetDestination(playerTransform.position);
        }
        else
        {
            float step = Mathf.Max(0f, baseMoveSpeed * movementSpeedMultiplier) * Time.deltaTime;
            Vector3 next = Vector3.MoveTowards(transform.position, playerTransform.position, step);
            transform.position = next;
        }

        RotateToward(playerTransform.position);
        return true;
    }

    private void StopImmediate()
    {
        if (agent != null && agent.enabled)
        {
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
            agent.ResetPath();
        }

        StopMovementAudio();
        UpdateAnimationState(false);
    }

    private void UpdateAnimationState(bool shouldMove)
    {
        if (animationBridge != null)
            animationBridge.SetMoving(shouldMove);
    }

    private void RotateToward(Vector3 worldTarget)
    {
        Vector3 flatDirection = worldTarget - transform.position;
        flatDirection.y = 0f;
        if (flatDirection.sqrMagnitude < 0.0001f)
            return;

        Quaternion targetRotation = Quaternion.LookRotation(flatDirection.normalized, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, turnSpeedDegrees * Time.deltaTime);
    }

    private bool TryKillPlayerIfReached()
    {
        if (playerTransform == null)
            return false;

        Vector3 selfFlat = transform.position;
        selfFlat.y = 0f;
        Vector3 playerFlat = playerTransform.position;
        playerFlat.y = 0f;
        float effectiveKillDistance = Mathf.Max(killDistance, GetHorizontalTouchDistance() + 0.05f);
        if ((playerFlat - selfFlat).sqrMagnitude > effectiveKillDistance * effectiveKillDistance)
            return false;

        PlayerDeath death = playerTransform.GetComponent<PlayerDeath>() ?? playerTransform.GetComponentInParent<PlayerDeath>();
        if (death != null)
        {
            if (!death.isDead)
            {
                PlayKillScream2D();
                death.Kill(killReason);
            }
            return true;
        }

        RohitFPSController rohit = playerTransform.GetComponent<RohitFPSController>() ?? playerTransform.GetComponentInParent<RohitFPSController>();
        if (rohit != null && !rohit.isHidden)
        {
            PlayKillScream2D();
            ResidentAI.KillRohitController(rohit, killReason);
            return true;
        }

        return false;
    }

    private float GetHorizontalTouchDistance()
    {
        float angelRadius = 0.35f;
        Collider selfCollider = GetComponentInChildren<Collider>();
        if (selfCollider != null)
            angelRadius = Mathf.Max(selfCollider.bounds.extents.x, selfCollider.bounds.extents.z);

        float playerRadius = 0.35f;
        CharacterController cc = playerTransform.GetComponent<CharacterController>() ?? playerTransform.GetComponentInChildren<CharacterController>();
        if (cc != null)
            playerRadius = cc.radius;
        else
        {
            Collider playerCollider = playerTransform.GetComponentInChildren<Collider>();
            if (playerCollider != null)
                playerRadius = Mathf.Max(playerCollider.bounds.extents.x, playerCollider.bounds.extents.z);
        }

        return angelRadius + playerRadius;
    }

    private void ConfigurePlayerCollisionIgnore()
    {
        if (playerTransform == null || angelColliders == null || angelColliders.Length == 0)
            return;

        Transform playerRoot = playerTransform.root != null ? playerTransform.root : playerTransform;
        if (collisionIgnoredForPlayer == playerRoot)
            return;

        Collider[] playerColliders = playerRoot.GetComponentsInChildren<Collider>(true);
        if (playerColliders == null || playerColliders.Length == 0)
            return;

        for (int i = 0; i < angelColliders.Length; i++)
        {
            Collider angelCollider = angelColliders[i];
            if (angelCollider == null)
                continue;

            for (int j = 0; j < playerColliders.Length; j++)
            {
                Collider playerCollider = playerColliders[j];
                if (playerCollider == null)
                    continue;

                Physics.IgnoreCollision(angelCollider, playerCollider, true);
            }
        }

        collisionIgnoredForPlayer = playerRoot;
    }

    private bool HasLineOfSightToPlayer()
    {
        if (playerTransform == null)
            return false;

        Vector3 from = transform.position + Vector3.up * visibilityPointHeight;
        Vector3 to = GetPlayerAimPoint();
        Vector3 dir = to - from;
        float distance = dir.magnitude;
        if (distance <= 0.001f)
            return true;

        if (!Physics.Linecast(from, to, out RaycastHit hit, lineOfSightObstructionMask, QueryTriggerInteraction.Ignore))
            return true;

        Transform hitTransform = hit.transform;
        if (hitTransform == null)
            return true;

        if (hitTransform == transform || hitTransform.IsChildOf(transform))
            return true;

        if (hitTransform == playerTransform || hitTransform.IsChildOf(playerTransform))
            return true;

        return false;
    }

    private Vector3 GetPlayerAimPoint()
    {
        CharacterController cc = playerTransform.GetComponent<CharacterController>() ?? playerTransform.GetComponentInChildren<CharacterController>();
        if (cc != null)
            return cc.bounds.center;

        Camera cam = playerTransform.GetComponentInChildren<Camera>();
        if (cam != null)
            return cam.transform.position;

        return playerTransform.position + Vector3.up * 1.1f;
    }

    private bool IsPlayerOrChild(Transform candidate)
    {
        if (candidate == null || playerTransform == null)
            return false;

        return candidate == playerTransform || candidate.IsChildOf(playerTransform);
    }

    private bool IsCameraOrChild(Transform candidate)
    {
        if (candidate == null || playerCamera == null)
            return false;

        Transform cameraTransform = playerCamera.transform;
        return candidate == cameraTransform || candidate.IsChildOf(cameraTransform);
    }

    private static Camera ResolveCameraFromController(RohitFPSController controller)
    {
        if (controller == null)
            return null;

        if (controller.cameraTransform != null)
        {
            Camera direct = controller.cameraTransform.GetComponent<Camera>();
            if (direct != null)
                return direct;
        }

        return controller.GetComponentInChildren<Camera>(true);
    }

    private void MaybeLogMissingReferences()
    {
        if (Application.isEditor)
            return;

        if (Time.unscaledTime < nextMissingReferenceLogTime)
            return;

        if (playerTransform == null || playerCamera == null)
        {
            string playerInfo = playerTransform != null ? playerTransform.name : "null";
            string cameraInfo = playerCamera != null ? playerCamera.name : "null";
            string controllerInfo = playerController != null ? playerController.name : "null";
            Debug.LogWarning($"[WeepingAngelAI:{name}] Missing reference(s) in build. player={playerInfo}, camera={cameraInfo}, controller={controllerInfo}", this);
        }

        nextMissingReferenceLogTime = Time.unscaledTime + 5f;
    }

    private void MaybeLogLookDetection(bool inFront, bool inViewport, bool lineOfSightClear, bool lookedAt)
    {
        if (Application.isEditor || !debugLogLookDetectionInBuild)
            return;

        bool changed = !hasPreviousLookDebugState
                       || previousInFront != inFront
                       || previousInViewport != inViewport
                       || previousLineOfSightClear != lineOfSightClear
                       || previousLookedAt != lookedAt;

        if (changed || Time.unscaledTime >= nextLookDebugLogTime)
        {
            Debug.Log($"[WeepingAngelAI:{name}] inFront={inFront} inViewport={inViewport} lineOfSightClear={lineOfSightClear} isLookedAt={lookedAt}", this);
            nextLookDebugLogTime = Time.unscaledTime + Mathf.Max(0.1f, debugLookLogInterval);
        }

        previousInFront = inFront;
        previousInViewport = inViewport;
        previousLineOfSightClear = lineOfSightClear;
        previousLookedAt = lookedAt;
        hasPreviousLookDebugState = true;
    }

    private void ConfigureAudioSources()
    {
        if (!enableAngelAudio)
            return;

        if (movementAudioSource == null)
            movementAudioSource = gameObject.AddComponent<AudioSource>();

        movementAudioSource.clip = movementClip;
        movementAudioSource.playOnAwake = false;
        movementAudioSource.loop = true;
        movementAudioSource.spatialBlend = 1f;
        movementAudioSource.volume = Mathf.Max(0f, movementVolume);
        movementAudioSource.pitch = movementPitch;
        movementAudioSource.minDistance = movementMinDistance;
        movementAudioSource.maxDistance = movementMaxDistance;

        if (killScreamAudioSource == null)
            killScreamAudioSource = gameObject.AddComponent<AudioSource>();

        killScreamAudioSource.playOnAwake = false;
        killScreamAudioSource.loop = false;
        killScreamAudioSource.spatialBlend = 0f;
        killScreamAudioSource.ignoreListenerPause = true;
        killScreamAudioSource.clip = killScreamClip;
        killScreamAudioSource.volume = Mathf.Max(0f, killScreamVolume);
        killScreamAudioSource.pitch = killScreamPitch;
    }

    private void UpdateMovementAudio(bool shouldPlay)
    {
        if (!enableAngelAudio || movementClip == null)
            return;

        if (movementAudioSource == null)
            ConfigureAudioSources();
        if (movementAudioSource == null)
            return;

        movementAudioSource.clip = movementClip;
        movementAudioSource.volume = Mathf.Max(0f, movementVolume);
        movementAudioSource.pitch = movementPitch;
        movementAudioSource.minDistance = movementMinDistance;
        movementAudioSource.maxDistance = movementMaxDistance;

        if (shouldPlay)
        {
            if (!movementAudioSource.isPlaying)
                movementAudioSource.Play();
        }
        else
        {
            StopMovementAudio();
        }
    }

    private void StopMovementAudio()
    {
        if (movementAudioSource != null && movementAudioSource.isPlaying)
            movementAudioSource.Pause();
    }

    private void PlayKillScream2D()
    {
        if (!enableAngelAudio || playedKillScream || killScreamClip == null)
            return;

        if (killScreamAudioSource == null)
            ConfigureAudioSources();
        if (killScreamAudioSource == null)
            return;

        killScreamAudioSource.spatialBlend = 0f;
        killScreamAudioSource.ignoreListenerPause = true;
        killScreamAudioSource.pitch = killScreamPitch;
        killScreamAudioSource.PlayOneShot(killScreamClip, Mathf.Max(0f, killScreamVolume));
        playedKillScream = true;
    }
}
