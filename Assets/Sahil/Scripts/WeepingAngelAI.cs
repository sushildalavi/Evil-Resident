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

    [Header("Facing")]
    public bool facePlayerWhenFrozen = true;
    [Min(30f)] public float turnSpeedDegrees = 360f;

    [Header("Debug")]
    public bool debugDrawLineOfSight;

    [Header("Kill")]
    public bool killPlayerOnReach = true;
    [Min(0.1f)] public float killDistance = 0.8f;
    public string killReason = "The Weeping Angel Reached you.";

    private NavMeshAgent agent;
    private RohitFPSController playerController;
    private Collider[] angelColliders;
    private Transform collisionIgnoredForPlayer;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        angelColliders = GetComponentsInChildren<Collider>(true);
        ResolveReferences();
        ApplySpeedFromPlayerWalk();
        ConfigurePlayerCollisionIgnore();

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

        bool lookedAt = IsLookedAtByPlayer();
        if (lookedAt)
        {
            StopImmediate();

            if (facePlayerWhenFrozen)
                RotateToward(playerTransform.position);

            return;
        }

        MoveTowardPlayer();
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

        if (playerCamera == null && autoFindMainCamera)
            playerCamera = Camera.main;

        if (playerController == null && playerTransform != null)
            playerController = playerTransform.GetComponent<RohitFPSController>();
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

    private bool IsLookedAtByPlayer()
    {
        if (playerCamera == null)
            return false;

        Vector3 cameraPos = playerCamera.transform.position;
        Vector3 targetPoint = transform.position + Vector3.up * visibilityPointHeight;
        Vector3 toTarget = targetPoint - cameraPos;

        float distance = toTarget.magnitude;
        if (distance > maxReactionDistance)
            return false;

        if (distance < 0.001f)
            return true;

        Vector3 dir = toTarget / distance;
        float forwardDot = Vector3.Dot(playerCamera.transform.forward, dir);
        if (forwardDot <= 0f)
            return false;

        float halfFov = playerCamera.fieldOfView * 0.5f + fieldOfViewTolerance;
        float angle = Vector3.Angle(playerCamera.transform.forward, dir);
        if (angle > halfFov)
            return false;

        bool blocked = Physics.Linecast(cameraPos, targetPoint, out RaycastHit hit, lineOfSightObstructionMask, QueryTriggerInteraction.Ignore)
                       && hit.transform != transform
                       && !hit.transform.IsChildOf(transform);

        if (debugDrawLineOfSight)
            Debug.DrawLine(cameraPos, targetPoint, blocked ? Color.red : Color.green);

        return !blocked;
    }

    private void MoveTowardPlayer()
    {
        if (requireLineOfSightToChase && !HasLineOfSightToPlayer())
        {
            StopImmediate();
            return;
        }

        Vector3 toPlayer = playerTransform.position - transform.position;
        toPlayer.y = 0f;

        if (toPlayer.sqrMagnitude <= stoppingDistance * stoppingDistance)
        {
            StopImmediate();
            return;
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
    }

    private void StopImmediate()
    {
        if (agent != null && agent.enabled)
        {
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
            agent.ResetPath();
        }
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
                death.Kill(killReason);
            return true;
        }

        RohitFPSController rohit = playerTransform.GetComponent<RohitFPSController>() ?? playerTransform.GetComponentInParent<RohitFPSController>();
        if (rohit != null && !rohit.isHidden)
        {
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
}
