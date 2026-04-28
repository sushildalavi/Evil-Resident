using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;
using System.Collections.Generic;
using Sushil.Systems;

public class Door : MonoBehaviour, IInteractable
{
    public enum DoorRotationAxis
    {
        X = 0,
        Y = 1,
        Z = 2
    }

    public enum HingeBoundsAxis
    {
        X = 0,
        Y = 1,
        Z = 2
    }

    [Header("Lock Settings")]
    public KeyType requiredKey;
    [Tooltip("If populated, all listed keys are required to unlock this door.")]
    public List<KeyType> requiredKeys = new List<KeyType>();
    public bool isLocked = true;

    [Header("Open Animation")]
    public float openAngle = 90f;
    public float openSpeed = 2f;
    public bool openClockwise = true;
    public DoorRotationAxis rotationAxis = DoorRotationAxis.Y;
    public Vector3 hingeLocalOffset = Vector3.zero;
    [Tooltip("Rotate this child as the moving door leaf. If empty, tries 'DoorPanel' then name-based auto-detection.")]
    public Transform rotatingPart;
    [Tooltip("If true and rotatingPart is empty, tries to find a child named like 'door' (excluding 'doorway' and 'frame').")]
    public bool autoDetectRotatingPartByName = true;
    [Tooltip("Derive hinge offset from rotating-part renderer bounds so doors with center pivots can swing from an edge.")]
    public bool autoComputeHingeFromBounds = false;
    public HingeBoundsAxis hingeBoundsAxis = HingeBoundsAxis.X;
    public bool hingeAtPositiveEdge = false;

    [Header("Audio (Optional)")]
    public AudioClip unlockSound;
    public AudioClip lockedSound;

    [Header("Win Condition (Optional)")]
    [Tooltip("If enabled, this door behaves like MainDoor when opened by player interaction.")]
    public bool actLikeMainDoor = false;
    public GameObject winUI;

    [Header("AI / Navigation")]
    public bool blockWhenClosed = true;
    public bool unblockWhenOpen = true;
    [Tooltip("If enabled, remove door blocking immediately when the door starts opening.")]
    public bool immediateUnblockOnOpen = true;
    [Range(1f, 89f)] public float openUnblockAngle = 65f;
    public bool autoAddNavObstacle = true;
    public bool autoAddRuntimeNavLink = true;
    [Tooltip("Creates both forward/back and left/right links to handle differently oriented door prefabs.")]
    public bool useDualAxisRuntimeLinks = true;
    public float navLinkDepth = 1.2f;
    public float navLinkWidth = 1.3f;
    public int navLinkAgentTypeID = -1;
    public bool aiCanOpenDoor = false;
    public bool aiBypassLock = false;

    [Header("Fallback Collider")]
    [Tooltip("If this door has no collider, add a BoxCollider automatically so prompts and blocking work.")]
    public bool autoAddColliderIfMissing = true;
    [Tooltip("Also treat colliders on this root GameObject as door blockers to disable when opened.")]
    public bool includeRootCollidersInBlocking = false;
    [Tooltip("Disable non-door colliders overlapping the doorway when this door opens (useful for custom doorway meshes inside existing wall colliders).")]
    public bool clearNearbyBlockingCollidersOnOpen = false;
    public Vector3 doorwayClearHalfExtents = new Vector3(0.7f, 1f, 0.7f);

    private bool isOpen = false;
    private bool wasUnlockedByKey = false;
    private Quaternion closedLocalRotation;
    private Vector3 closedLocalPosition;
    private float currentOpenAngle;
    private AudioSource audioSource;
    private Transform doorPanel;
    private Collider[] cachedColliders;
    private NavMeshObstacle navObstacle;
    private NavMeshLink navLinkForwardBack;
    private NavMeshLink navLinkLeftRight;
    private Transform runtimeNavLinkRoot;
    private bool isBlockingNow;
    private Vector3 traversalCenterLocal;
    private Transform rotatingTarget;
    private Vector3 rotatingClosedLocalPosition;
    private Quaternion rotatingClosedLocalRotation;
    private Vector3 resolvedHingeLocalOffset;
    private Collider[] temporarilyDisabledNearbyColliders;

    void Start()
    {
        // Hard safety defaults so old scene/prefab serialized values cannot regress doorway traversal.
        unblockWhenOpen = true;
        immediateUnblockOnOpen = true;
        autoAddRuntimeNavLink = true;

        closedLocalPosition = transform.localPosition;
        closedLocalRotation = transform.localRotation;
        audioSource = GetComponent<AudioSource>();
        currentOpenAngle = 0f;
        // Only toggle the actual moving panel colliders; keep nearby frame/wall colliders untouched.
        doorPanel = ResolveRotatingPart();
        rotatingTarget = doorPanel != null ? doorPanel : transform;
        rotatingClosedLocalPosition = rotatingTarget.localPosition;
        rotatingClosedLocalRotation = rotatingTarget.localRotation;
        resolvedHingeLocalOffset = hingeLocalOffset;
        if (autoComputeHingeFromBounds && TryComputeHingeOffsetFromBounds(rotatingTarget, out Vector3 autoHinge))
            resolvedHingeLocalOffset += autoHinge;

        cachedColliders = BuildBlockingColliderList();
        EnsureFallbackColliderIfMissing();
        CacheTraversalGeometry();
        navObstacle = GetComponent<NavMeshObstacle>();
        if (autoAddNavObstacle && navObstacle == null)
        {
            navObstacle = gameObject.AddComponent<NavMeshObstacle>();
            navObstacle.carving = true;
            navObstacle.carveOnlyStationary = true;
        }
        if (autoAddRuntimeNavLink)
            EnsureRuntimeNavLink();
        SetDoorBlocking(true);

        if (winUI != null)
            winUI.SetActive(false);
    }

    void Update()
    {
        // Invert configured swing so doors open inward by default across the level.
        float direction = openClockwise ? -1f : 1f;
        float targetAngle = isOpen ? (openAngle * direction) : 0f;
        currentOpenAngle = Mathf.MoveTowards(currentOpenAngle, targetAngle, openSpeed * 100f * Time.deltaTime);

        ApplyHingePose(currentOpenAngle);
        SyncDoorBlockingForCurrentAngle();
        SyncRuntimeNavLink();
    }

    public KeyCode GetInteractKey() => KeyCode.E;

    public bool IsOpen => isOpen;
    public bool IsLocked => isLocked;
    public bool WasUnlockedByKey => wasUnlockedByKey;

    public string GetPrompt(RohitFPSController player)
    {
        if (isOpen) return "";

        if (!isLocked) return "Press E to Open Door";

        PlayerInventory inventory = player != null ? player.GetComponent<PlayerInventory>() : null;
        List<KeyType> lockKeys = GetEffectiveRequiredKeys();

        if (inventory != null && HasAllRequiredKeys(inventory, lockKeys))
            return $"Press E to Unlock ({FormatKeyList(lockKeys)})";

        return $"Locked - Requires {FormatKeyList(lockKeys)}";
    }

    public void Interact(RohitFPSController player)
    {
        if (isOpen) return;

        if (!isLocked)
        {
            OpenDoor();
            TriggerWinConditionIfNeeded();
            return;
        }

        PlayerInventory inventory = player != null ? player.GetComponent<PlayerInventory>() : null;
        List<KeyType> lockKeys = GetEffectiveRequiredKeys();
        if (inventory != null && HasAllRequiredKeys(inventory, lockKeys))
        {
            isLocked = false;
            wasUnlockedByKey = true;
            OpenDoor();
            PlaySound(unlockSound);
            Debug.Log($"Unlocked door with {FormatKeyList(lockKeys)}!");
            TriggerWinConditionIfNeeded();
        }
        else
        {
            PlaySound(lockedSound);
            Debug.Log($"This door requires {FormatKeyList(lockKeys)}.");
        }
    }

    public void OpenDoor()
    {
        isOpen = true;
        // Clear blockers immediately at the moment door starts opening.
        SetDoorBlocking(false);
        if (clearNearbyBlockingCollidersOnOpen)
            DisableNearbyBlockingColliders();
        if (navLinkForwardBack != null) navLinkForwardBack.activated = true;
        if (navLinkLeftRight != null) navLinkLeftRight.activated = true;
    }

    public bool TryOpenForAI()
    {
        if (!aiCanOpenDoor) return false;
        if (isOpen) return true;
        if (isLocked && !aiBypassLock) return false;

        // If bypass is allowed, AI can force this door unlocked.
        if (isLocked && aiBypassLock)
            isLocked = false;

        OpenDoor();
        return true;
    }

    public Vector3 GetDoorwayCenter()
    {
        Quaternion closedWorldRotation = GetClosedWorldRotation();
        Vector3 closedWorldPosition = GetClosedWorldPosition();
        return closedWorldPosition + (closedWorldRotation * traversalCenterLocal);
    }

    public Vector3 GetDoorwayForward()
    {
        Vector3 forward = GetClosedWorldRotation() * Vector3.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.001f)
            forward = transform.forward;
        forward.y = 0f;
        return forward.sqrMagnitude > 0.001f ? forward.normalized : Vector3.forward;
    }

    public Vector3 GetDoorwayRight()
    {
        Vector3 right = GetClosedWorldRotation() * Vector3.right;
        right.y = 0f;
        if (right.sqrMagnitude < 0.001f)
            right = transform.right;
        right.y = 0f;
        return right.sqrMagnitude > 0.001f ? right.normalized : Vector3.right;
    }

    void SyncDoorBlockingForCurrentAngle()
    {
        bool shouldBlock;
        if (!blockWhenClosed && !unblockWhenOpen) return;

        if (!isOpen)
        {
            shouldBlock = blockWhenClosed;
        }
        else
        {
            // For this project: once opening starts, never block traversal at this doorway.
            shouldBlock = false;
        }

        SetDoorBlocking(shouldBlock);
    }

    void SetDoorBlocking(bool block)
    {
        if (isBlockingNow == block) return;
        isBlockingNow = block;

        if (cachedColliders != null)
        {
            // Once the doorway is unblocked, disable the moving panel colliders
            // so the player can pass through the opened door cleanly.
            bool keepPanelCollidersActive = block;
            for (int i = 0; i < cachedColliders.Length; i++)
            {
                var c = cachedColliders[i];
                if (c == null) continue;
                c.enabled = keepPanelCollidersActive;
            }
        }

        if (navObstacle != null)
            navObstacle.enabled = block;

        if (block)
            RestoreNearbyBlockingColliders();
    }

    void EnsureRuntimeNavLink()
    {
        float depth = Mathf.Max(0.4f, navLinkDepth);
        float width = Mathf.Max(0.4f, navLinkWidth);
        int agentType = ResolveAgentTypeId();
        EnsureRuntimeNavLinkRoot();
        if (runtimeNavLinkRoot == null) return;

        if (navLinkForwardBack == null)
        {
            navLinkForwardBack = runtimeNavLinkRoot.GetComponent<NavMeshLink>();
            if (navLinkForwardBack == null)
                navLinkForwardBack = runtimeNavLinkRoot.gameObject.AddComponent<NavMeshLink>();
        }
        ConfigureLink(navLinkForwardBack, Vector3.back * depth, Vector3.forward * depth, width, agentType);

        if (!useDualAxisRuntimeLinks)
        {
            if (navLinkLeftRight != null) navLinkLeftRight.enabled = false;
            return;
        }

        if (navLinkLeftRight == null)
        {
            NavMeshLink[] all = runtimeNavLinkRoot.GetComponents<NavMeshLink>();
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i] != navLinkForwardBack)
                {
                    navLinkLeftRight = all[i];
                    break;
                }
            }
            if (navLinkLeftRight == null)
                navLinkLeftRight = runtimeNavLinkRoot.gameObject.AddComponent<NavMeshLink>();
        }
        ConfigureLink(navLinkLeftRight, Vector3.left * depth, Vector3.right * depth, width, agentType);
    }

    int ResolveAgentTypeId()
    {
        if (navLinkAgentTypeID >= 0) return navLinkAgentTypeID;
        NavMeshAgent anyAgent = FindFirstObjectByType<NavMeshAgent>();
        return anyAgent != null ? anyAgent.agentTypeID : 0;
    }

    void ConfigureLink(NavMeshLink link, Vector3 start, Vector3 end, float width, int agentType)
    {
        if (link == null) return;
        link.enabled = true;
        link.startPoint = start;
        link.endPoint = end;
        link.width = width;
        link.bidirectional = true;
        link.autoUpdate = true;
        link.activated = false;
        link.agentTypeID = agentType;
    }

    void SyncRuntimeNavLink()
    {
        if (!autoAddRuntimeNavLink) return;
        SyncRuntimeNavLinkRootPose();

        bool shouldEnable = isOpen && (!isLocked || aiBypassLock);
        if (navLinkForwardBack != null && navLinkForwardBack.enabled)
            navLinkForwardBack.activated = shouldEnable;
        if (navLinkLeftRight != null && navLinkLeftRight.enabled)
            navLinkLeftRight.activated = shouldEnable;
    }

    void ApplyHingePose(float angleY)
    {
        if (rotatingTarget == null)
            return;

        // Keep hinge math in local space to avoid skew/offset artifacts under scaled parents.
        Vector3 axis = GetRotationAxisVector(rotationAxis);
        Quaternion localRot = rotatingClosedLocalRotation * Quaternion.AngleAxis(angleY, axis);
        Vector3 hingeLocal = resolvedHingeLocalOffset;
        Vector3 localHinge = rotatingClosedLocalPosition + rotatingClosedLocalRotation * hingeLocal;
        Vector3 localPos = localHinge - (localRot * hingeLocal);

        rotatingTarget.localPosition = localPos;
        rotatingTarget.localRotation = localRot;
    }

    private void PlaySound(AudioClip clip)
    {
        if (clip != null && audioSource != null)
            audioSource.PlayOneShot(clip);
    }

    void CacheTraversalGeometry()
    {
        bool hasBounds = false;
        Bounds combined = new Bounds();

        if (cachedColliders != null)
        {
            for (int i = 0; i < cachedColliders.Length; i++)
            {
                Collider c = cachedColliders[i];
                if (c == null) continue;
                if (!hasBounds)
                {
                    combined = c.bounds;
                    hasBounds = true;
                }
                else
                {
                    combined.Encapsulate(c.bounds);
                }
            }
        }

        if (hasBounds)
        {
            traversalCenterLocal = transform.InverseTransformPoint(combined.center);
            return;
        }

        traversalCenterLocal = doorPanel != null ? doorPanel.localPosition : Vector3.zero;
    }

    void EnsureFallbackColliderIfMissing()
    {
        if (!autoAddColliderIfMissing)
            return;

        bool hasCollider = false;
        if (cachedColliders != null)
        {
            for (int i = 0; i < cachedColliders.Length; i++)
            {
                if (cachedColliders[i] != null)
                {
                    hasCollider = true;
                    break;
                }
            }
        }

        if (hasCollider)
            return;

        Transform target = rotatingTarget != null ? rotatingTarget : transform;
        if (target == null)
            return;

        BoxCollider box = target.GetComponent<BoxCollider>();
        if (box == null)
            box = target.gameObject.AddComponent<BoxCollider>();

        box.isTrigger = false;
        FitBoxColliderToRenderers(box, target);

        cachedColliders = BuildBlockingColliderList();
    }

    static void FitBoxColliderToRenderers(BoxCollider box, Transform target)
    {
        if (box == null || target == null)
            return;

        Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
        if (renderers == null || renderers.Length == 0)
            return;

        bool hasBounds = false;
        Bounds worldBounds = new Bounds();
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer r = renderers[i];
            if (r == null)
                continue;

            if (!hasBounds)
            {
                worldBounds = r.bounds;
                hasBounds = true;
            }
            else
            {
                worldBounds.Encapsulate(r.bounds);
            }
        }

        if (!hasBounds)
            return;

        Vector3 ext = worldBounds.extents;
        Vector3 center = worldBounds.center;
        Vector3 min = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
        Vector3 max = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);

        for (int ix = -1; ix <= 1; ix += 2)
        {
            for (int iy = -1; iy <= 1; iy += 2)
            {
                for (int iz = -1; iz <= 1; iz += 2)
                {
                    Vector3 worldCorner = center + new Vector3(ext.x * ix, ext.y * iy, ext.z * iz);
                    Vector3 local = target.InverseTransformPoint(worldCorner);
                    min = Vector3.Min(min, local);
                    max = Vector3.Max(max, local);
                }
            }
        }

        box.center = (min + max) * 0.5f;
        box.size = Vector3.Max(max - min, new Vector3(0.05f, 0.05f, 0.05f));
    }

    Transform ResolveRotatingPart()
    {
        if (rotatingPart != null)
            return rotatingPart;

        Transform namedPanel = transform.Find("DoorPanel");
        if (namedPanel != null)
            return namedPanel;

        if (!autoDetectRotatingPartByName)
            return null;

        Transform[] allChildren = transform.GetComponentsInChildren<Transform>(true);
        Transform fallback = null;
        for (int i = 0; i < allChildren.Length; i++)
        {
            Transform candidate = allChildren[i];
            if (candidate == null || candidate == transform)
                continue;

            string n = candidate.name.ToLowerInvariant();
            bool looksLikeDoor = n.Contains("door");
            bool looksLikeFrame = n.Contains("frame") || n.Contains("doorway");
            if (!looksLikeDoor || looksLikeFrame)
                continue;

            if (n == "door")
                return candidate;

            if (fallback == null)
                fallback = candidate;
        }

        return fallback;
    }

    bool TryComputeHingeOffsetFromBounds(Transform target, out Vector3 hingeOffset)
    {
        hingeOffset = Vector3.zero;
        if (target == null)
            return false;

        Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
        if (renderers == null || renderers.Length == 0)
            return false;

        bool hasBounds = false;
        Bounds worldBounds = new Bounds();
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer r = renderers[i];
            if (r == null)
                continue;

            if (!hasBounds)
            {
                worldBounds = r.bounds;
                hasBounds = true;
            }
            else
            {
                worldBounds.Encapsulate(r.bounds);
            }
        }

        if (!hasBounds)
            return false;

        Vector3 ext = worldBounds.extents;
        Vector3 center = worldBounds.center;
        Vector3 min = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
        Vector3 max = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);

        for (int ix = -1; ix <= 1; ix += 2)
        {
            for (int iy = -1; iy <= 1; iy += 2)
            {
                for (int iz = -1; iz <= 1; iz += 2)
                {
                    Vector3 worldCorner = center + new Vector3(ext.x * ix, ext.y * iy, ext.z * iz);
                    Vector3 local = target.InverseTransformPoint(worldCorner);
                    min = Vector3.Min(min, local);
                    max = Vector3.Max(max, local);
                }
            }
        }

        float edge = 0f;
        switch (hingeBoundsAxis)
        {
            case HingeBoundsAxis.X:
                edge = hingeAtPositiveEdge ? max.x : min.x;
                hingeOffset.x = edge;
                break;
            case HingeBoundsAxis.Y:
                edge = hingeAtPositiveEdge ? max.y : min.y;
                hingeOffset.y = edge;
                break;
            case HingeBoundsAxis.Z:
                edge = hingeAtPositiveEdge ? max.z : min.z;
                hingeOffset.z = edge;
                break;
        }

        return true;
    }

    static Vector3 GetRotationAxisVector(DoorRotationAxis axis)
    {
        switch (axis)
        {
            case DoorRotationAxis.X: return Vector3.right;
            case DoorRotationAxis.Z: return Vector3.forward;
            default: return Vector3.up;
        }
    }

    List<KeyType> GetEffectiveRequiredKeys()
    {
        List<KeyType> keys = new List<KeyType>();

        if (requiredKeys != null)
        {
            for (int i = 0; i < requiredKeys.Count; i++)
            {
                KeyType key = requiredKeys[i];
                if (!keys.Contains(key))
                    keys.Add(key);
            }
        }

        if (keys.Count == 0)
            keys.Add(requiredKey);

        return keys;
    }

    static bool HasAllRequiredKeys(PlayerInventory inventory, List<KeyType> keys)
    {
        if (inventory == null || keys == null || keys.Count == 0)
            return false;

        for (int i = 0; i < keys.Count; i++)
        {
            if (!inventory.HasKey(keys[i]))
                return false;
        }

        return true;
    }

    static string FormatKeyList(List<KeyType> keys)
    {
        if (keys == null || keys.Count == 0)
            return "Unknown Key";

        if (keys.Count == 1)
            return $"{keys[0]} Key";

        string value = "";
        for (int i = 0; i < keys.Count; i++)
        {
            string token = $"{keys[i]} Key";
            if (i == 0)
            {
                value = token;
                continue;
            }

            value += i == keys.Count - 1 ? $" and {token}" : $", {token}";
        }

        return value;
    }

    void TriggerWinConditionIfNeeded()
    {
        if (!actLikeMainDoor)
            return;

        if (winUI != null)
            winUI.SetActive(true);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        EscapeOverlay.Show();
    }

    void DisableNearbyBlockingColliders()
    {
        Vector3 center = GetDoorwayCenter();
        Vector3 half = new Vector3(
            Mathf.Max(0.05f, doorwayClearHalfExtents.x),
            Mathf.Max(0.05f, doorwayClearHalfExtents.y),
            Mathf.Max(0.05f, doorwayClearHalfExtents.z));

        Collider[] overlaps = Physics.OverlapBox(center, half, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
        if (overlaps == null || overlaps.Length == 0)
        {
            temporarilyDisabledNearbyColliders = null;
            return;
        }

        Collider[] disabled = new Collider[overlaps.Length];
        int count = 0;

        for (int i = 0; i < overlaps.Length; i++)
        {
            Collider c = overlaps[i];
            if (c == null || !c.enabled || c.isTrigger)
                continue;

            Transform t = c.transform;
            if (t == null)
                continue;

            if (t == transform || t.IsChildOf(transform))
                continue;

            bool alreadyManaged = false;
            if (cachedColliders != null)
            {
                for (int k = 0; k < cachedColliders.Length; k++)
                {
                    if (cachedColliders[k] == c)
                    {
                        alreadyManaged = true;
                        break;
                    }
                }
            }
            if (alreadyManaged)
                continue;

            c.enabled = false;
            disabled[count++] = c;
        }

        if (count == 0)
        {
            temporarilyDisabledNearbyColliders = null;
            return;
        }

        temporarilyDisabledNearbyColliders = new Collider[count];
        for (int i = 0; i < count; i++)
            temporarilyDisabledNearbyColliders[i] = disabled[i];
    }

    void RestoreNearbyBlockingColliders()
    {
        if (temporarilyDisabledNearbyColliders == null || temporarilyDisabledNearbyColliders.Length == 0)
            return;

        for (int i = 0; i < temporarilyDisabledNearbyColliders.Length; i++)
        {
            Collider c = temporarilyDisabledNearbyColliders[i];
            if (c != null)
                c.enabled = true;
        }

        temporarilyDisabledNearbyColliders = null;
    }

    Collider[] BuildBlockingColliderList()
    {
        Transform target = rotatingTarget != null ? rotatingTarget : transform;
        Collider[] targetColliders = target != null
            ? target.GetComponentsInChildren<Collider>(true)
            : null;

        Collider[] rootColliders = includeRootCollidersInBlocking ? GetComponents<Collider>() : null;

        int targetCount = targetColliders != null ? targetColliders.Length : 0;
        int rootCount = rootColliders != null ? rootColliders.Length : 0;
        if (targetCount == 0 && rootCount == 0)
            return target != transform ? GetComponents<Collider>() : targetColliders;

        Collider[] merged = new Collider[targetCount + rootCount];
        int idx = 0;

        for (int i = 0; i < targetCount; i++)
        {
            Collider c = targetColliders[i];
            if (c == null) continue;
            merged[idx++] = c;
        }

        for (int i = 0; i < rootCount; i++)
        {
            Collider c = rootColliders[i];
            if (c == null) continue;

            bool exists = false;
            for (int j = 0; j < idx; j++)
            {
                if (merged[j] == c)
                {
                    exists = true;
                    break;
                }
            }

            if (!exists)
                merged[idx++] = c;
        }

        if (idx == merged.Length)
            return merged;

        Collider[] trimmed = new Collider[idx];
        for (int i = 0; i < idx; i++)
            trimmed[i] = merged[i];
        return trimmed;
    }

    Quaternion GetClosedWorldRotation()
    {
        return transform.parent != null
            ? transform.parent.rotation * closedLocalRotation
            : closedLocalRotation;
    }

    Vector3 GetClosedWorldPosition()
    {
        return transform.parent != null
            ? transform.parent.TransformPoint(closedLocalPosition)
            : closedLocalPosition;
    }

    void EnsureRuntimeNavLinkRoot()
    {
        if (runtimeNavLinkRoot == null)
        {
            string rootName = $"{gameObject.name}_RuntimeNavLinkRoot";
            GameObject rootObject = new GameObject(rootName);
            Transform parent = transform.parent;
            if (parent != null)
                rootObject.transform.SetParent(parent, false);
            runtimeNavLinkRoot = rootObject.transform;
        }

        SyncRuntimeNavLinkRootPose();
    }

    void SyncRuntimeNavLinkRootPose()
    {
        if (runtimeNavLinkRoot == null) return;

        Vector3 forward = GetDoorwayForward();
        if (forward.sqrMagnitude < 0.001f)
            forward = Vector3.forward;

        runtimeNavLinkRoot.position = GetDoorwayCenter();
        runtimeNavLinkRoot.rotation = Quaternion.LookRotation(forward, Vector3.up);
    }

}
