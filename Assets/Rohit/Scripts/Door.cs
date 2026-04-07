using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;

public class Door : MonoBehaviour, IInteractable
{
    [Header("Lock Settings")]
    public KeyType requiredKey;
    public bool isLocked = true;

    [Header("Open Animation")]
    public float openAngle = 90f;
    public float openSpeed = 2f;
    public bool openClockwise = true;
    public Vector3 hingeLocalOffset = Vector3.zero;

    [Header("Audio (Optional)")]
    public AudioClip unlockSound;
    public AudioClip lockedSound;

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
        doorPanel = transform.Find("DoorPanel");
        cachedColliders = doorPanel != null
            ? doorPanel.GetComponentsInChildren<Collider>(true)
            : GetComponentsInChildren<Collider>(true);
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
        if (inventory != null && inventory.HasKey(requiredKey))
            return $"Press E to Unlock ({requiredKey} Key)";

        return $"Locked - Requires {requiredKey} Key";
    }

    public void Interact(RohitFPSController player)
    {
        if (isOpen) return;

        if (!isLocked)
        {
            OpenDoor();
            return;
        }

        PlayerInventory inventory = player != null ? player.GetComponent<PlayerInventory>() : null;
        if (inventory != null && inventory.HasKey(requiredKey))
        {
            isLocked = false;
            wasUnlockedByKey = true;
            OpenDoor();
            PlaySound(unlockSound);
            Debug.Log($"Unlocked door with {requiredKey} Key!");
        }
        else
        {
            PlaySound(lockedSound);
            Debug.Log($"This door requires the {requiredKey} Key.");
        }
    }

    public void OpenDoor()
    {
        isOpen = true;
        // Clear blockers immediately at the moment door starts opening.
        SetDoorBlocking(false);
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
        // Keep hinge math in local space to avoid skew/offset artifacts under scaled parents.
        Quaternion localRot = closedLocalRotation * Quaternion.Euler(0f, angleY, 0f);
        Vector3 hingeLocal = hingeLocalOffset;
        Vector3 localHinge = closedLocalPosition + closedLocalRotation * hingeLocal;
        Vector3 localPos = localHinge - (localRot * hingeLocal);

        transform.localPosition = localPos;
        transform.localRotation = localRot;
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
