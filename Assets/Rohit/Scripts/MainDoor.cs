using UnityEngine;
using Sushil.Systems;
using UnityEngine.AI;
using Unity.AI.Navigation;

public class MainDoor : MonoBehaviour, IInteractable
{
    [Header("Open Animation")]
    public float openAngle = 90f;
    public float openSpeed = 2f;
    public bool openClockwise = true;
    public Vector3 hingeLocalOffset = new Vector3(-0.5f, 0f, 0f);

    [Header("Audio (Optional)")]
    public AudioClip unlockSound;
    public AudioClip lockedSound;

    [Header("Win Condition")]
    public GameObject winUI;

    [Header("AI / Navigation")]
    public bool blockWhenClosed = true;
    public bool unblockWhenOpen = true;
    [Tooltip("If enabled, remove door blocking immediately when the door starts opening.")]
    public bool immediateUnblockOnOpen = true;
    [Range(1f, 89f)] public float openUnblockAngle = 65f;
    public bool autoAddNavObstacle = true;
    public bool autoAddRuntimeNavLink = true;
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
    private NavMeshLink navLink;
    private bool isBlockingNow;

    void Awake()
    {
        // Enforce closed-door blocking before Start() to avoid early-frame AI pass-through.
        if (!isOpen)
        {
            ForceClosedBlockingStateEarly();
        }
    }

    void OnEnable()
    {
        if (!isOpen)
        {
            ForceClosedBlockingStateEarly();
        }
    }

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
        // Only toggle colliders on moving panel so frame/wall collision remains stable.
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

        if (winUI != null)
            winUI.SetActive(false);
    }

    void ForceClosedBlockingStateEarly()
    {
        Collider[] cols = GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < cols.Length; i++)
        {
            Collider c = cols[i];
            if (c == null || c.isTrigger) continue;
            c.enabled = true;
        }

        if (blockWhenClosed)
        {
            NavMeshObstacle obstacle = GetComponent<NavMeshObstacle>();
            if (obstacle == null && autoAddNavObstacle)
            {
                obstacle = gameObject.AddComponent<NavMeshObstacle>();
                obstacle.carving = true;
                obstacle.carveOnlyStationary = true;
            }
            if (obstacle != null)
                obstacle.enabled = true;
        }
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

    public string GetPrompt(RohitFPSController player)
    {
        if (isOpen) return "Freedom awaits...";

        PlayerInventory inventory = player.GetComponent<PlayerInventory>();
        if (inventory != null && inventory.HasAllKeys())
            return "Press E to ESCAPE!";

        int count = inventory != null ? inventory.KeyCount : 0;
        return $"Main Door - Locked ({count}/3 Keys)";
    }

    public void Interact(RohitFPSController player)
    {
        if (isOpen) return;

        PlayerInventory inventory = player.GetComponent<PlayerInventory>();
        if (inventory != null && inventory.HasAllKeys())
        {
            isOpen = true;
            SetDoorBlocking(false);
            if (navLink != null) navLink.activated = true;
            PlaySound(unlockSound);
            Debug.Log("The main door opens! You escaped!");

            if (winUI != null)
                winUI.SetActive(true);

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            EscapeOverlay.Show();
        }
        else
        {
            PlaySound(lockedSound);
            int count = inventory != null ? inventory.KeyCount : 0;
            Debug.Log($"You need all 3 keys to escape! ({count}/3)");
        }
    }

    private void PlaySound(AudioClip clip)
    {
        if (clip != null && audioSource != null)
            audioSource.PlayOneShot(clip);
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
        if (navLink == null) navLink = GetComponent<NavMeshLink>();
        if (navLink == null) navLink = gameObject.AddComponent<NavMeshLink>();

        navLink.startPoint = Vector3.back * Mathf.Max(0.4f, navLinkDepth);
        navLink.endPoint = Vector3.forward * Mathf.Max(0.4f, navLinkDepth);
        navLink.width = Mathf.Max(0.4f, navLinkWidth);
        navLink.bidirectional = true;
        navLink.autoUpdate = true;
        navLink.activated = false;

        if (navLinkAgentTypeID >= 0)
        {
            navLink.agentTypeID = navLinkAgentTypeID;
            return;
        }

        NavMeshAgent anyAgent = FindFirstObjectByType<NavMeshAgent>();
        if (anyAgent != null) navLink.agentTypeID = anyAgent.agentTypeID;
    }

    void SyncRuntimeNavLink()
    {
        if (!autoAddRuntimeNavLink || navLink == null) return;
        navLink.activated = isOpen;
    }

    void ApplyHingePose(float angleY)
    {
        // Use local-space hinge math to stay correct under non-uniform parent scaling.
        Quaternion localRot = closedLocalRotation * Quaternion.Euler(0f, angleY, 0f);
        Vector3 hingeLocal = hingeLocalOffset;
        Vector3 localHinge = closedLocalPosition + closedLocalRotation * hingeLocal;
        Vector3 localPos = localHinge - (localRot * hingeLocal);

        transform.localPosition = localPos;
        transform.localRotation = localRot;
    }
}
