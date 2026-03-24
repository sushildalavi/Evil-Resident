using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;

public class FuseDoor : MonoBehaviour, IInteractable
{
    [Header("Fuse Requirement")]
    public FuseBox[] requiredFuseBoxes;
    [Tooltip("If no fuse boxes are assigned, use all FuseBox objects found in the scene.")]
    public bool autoFindFuseBoxesIfUnassigned = true;
    public string lockedPrompt = "Fill the fuses first";
    public string openPrompt = "Press E to Open Door";

    [Header("Open Animation")]
    public float openAngle = 90f;
    public float openSpeed = 2f;
    public bool openClockwise = true;
    public Vector3 hingeLocalOffset = Vector3.zero;

    [Header("Audio (Optional)")]
    public AudioClip openSound;
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

    private bool isOpen = false;
    private Quaternion closedLocalRotation;
    private Vector3 closedLocalPosition;
    private float currentOpenAngle;
    private AudioSource audioSource;
    private Collider[] cachedColliders;
    private NavMeshObstacle navObstacle;
    private NavMeshLink navLinkForwardBack;
    private NavMeshLink navLinkLeftRight;
    private bool isBlockingNow;

    void Start()
    {
        closedLocalPosition = transform.localPosition;
        closedLocalRotation = transform.localRotation;
        audioSource = GetComponent<AudioSource>();
        currentOpenAngle = 0f;

        Transform panel = transform.Find("DoorPanel");
        cachedColliders = panel != null
            ? panel.GetComponentsInChildren<Collider>(true)
            : GetComponentsInChildren<Collider>(true);

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
        float direction = openClockwise ? -1f : 1f;
        float targetAngle = isOpen ? (openAngle * direction) : 0f;
        currentOpenAngle = Mathf.MoveTowards(currentOpenAngle, targetAngle, openSpeed * 100f * Time.deltaTime);

        ApplyHingePose(currentOpenAngle);
        SyncDoorBlockingForCurrentAngle();
        SyncRuntimeNavLink();
    }

    public KeyCode GetInteractKey() => KeyCode.E;

    public string GetPrompt(RohitFPSController player)
    {
        if (isOpen) return "";
        return AreRequiredFuseBoxesFull() ? openPrompt : lockedPrompt;
    }

    public void Interact(RohitFPSController player)
    {
        if (isOpen) return;

        if (!AreRequiredFuseBoxesFull())
        {
            PlaySound(lockedSound);
            Debug.Log(lockedPrompt);
            return;
        }

        OpenDoor();
    }

    public void OpenDoor()
    {
        if (isOpen) return;
        isOpen = true;
        SetDoorBlocking(false);
        if (navLinkForwardBack != null) navLinkForwardBack.activated = true;
        if (navLinkLeftRight != null) navLinkLeftRight.activated = true;
        PlaySound(openSound);
    }

    bool AreRequiredFuseBoxesFull()
    {
        FuseBox[] boxes = requiredFuseBoxes;
        if ((boxes == null || boxes.Length == 0) && autoFindFuseBoxesIfUnassigned)
            boxes = FindObjectsByType<FuseBox>(FindObjectsSortMode.None);

        if (boxes == null || boxes.Length == 0)
            return false;

        int validCount = 0;
        for (int i = 0; i < boxes.Length; i++)
        {
            FuseBox box = boxes[i];
            if (box == null) continue;
            validCount++;
            if (!box.IsFull)
                return false;
        }

        return validCount > 0;
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
            for (int i = 0; i < cachedColliders.Length; i++)
            {
                var c = cachedColliders[i];
                if (c == null) continue;
                c.enabled = block;
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

        if (navLinkForwardBack == null)
        {
            navLinkForwardBack = GetComponent<NavMeshLink>();
            if (navLinkForwardBack == null) navLinkForwardBack = gameObject.AddComponent<NavMeshLink>();
        }
        ConfigureLink(navLinkForwardBack, Vector3.back * depth, Vector3.forward * depth, width, agentType);

        if (!useDualAxisRuntimeLinks)
        {
            if (navLinkLeftRight != null) navLinkLeftRight.enabled = false;
            return;
        }

        if (navLinkLeftRight == null)
        {
            NavMeshLink[] all = GetComponents<NavMeshLink>();
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i] != navLinkForwardBack)
                {
                    navLinkLeftRight = all[i];
                    break;
                }
            }
            if (navLinkLeftRight == null) navLinkLeftRight = gameObject.AddComponent<NavMeshLink>();
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

        bool shouldEnable = isOpen;
        if (navLinkForwardBack != null && navLinkForwardBack.enabled)
            navLinkForwardBack.activated = shouldEnable;
        if (navLinkLeftRight != null && navLinkLeftRight.enabled)
            navLinkLeftRight.activated = shouldEnable;
    }

    void ApplyHingePose(float angleY)
    {
        Quaternion localRot = closedLocalRotation * Quaternion.Euler(0f, angleY, 0f);
        Vector3 hingeLocal = hingeLocalOffset;
        Vector3 localHinge = closedLocalPosition + closedLocalRotation * hingeLocal;
        Vector3 localPos = localHinge - (localRot * hingeLocal);

        transform.localPosition = localPos;
        transform.localRotation = localRot;
    }

    void PlaySound(AudioClip clip)
    {
        if (clip != null && audioSource != null)
            audioSource.PlayOneShot(clip);
    }
}
