using UnityEngine;
using UnityEngine.SceneManagement;

public class TutorialDoorTransition : MonoBehaviour, IInteractable
{
    [Header("Lock Settings")]
    public KeyType requiredKey;

    [Header("Open Animation")]
    public float openAngle = 90f;
    public float openSpeed = 2f;
    public bool openClockwise = true;
    [Tooltip("Which edge of the DoorPanel the hinge is on.")]
    public bool hingeOnRightEdge = false;

    [Header("Scene Transition")]
    [Tooltip("Name of the main game scene to load when the door opens.")]
    public string mainGameSceneName = "NewLevel";
    [Tooltip("Delay in seconds after the door opens before loading the scene.")]
    public float transitionDelay = 1.5f;

    [Header("Tutorial Requirements")]
    [Tooltip("Drag all hideable objects the player must use before the door unlocks.")]
    public HideableObject[] requiredHideSpots;

    [Header("Audio (Optional)")]
    public AudioClip unlockSound;
    public AudioClip lockedSound;

    bool allHidesDone;
    bool isOpen;
    bool sceneLoadTriggered;
    float currentAngle;
    float timer;
    AudioSource audioSource;
    RohitFPSController cachedPlayer;
    Collider[] doorColliders;
    bool[] hideCompleted;
    Transform hingePivot;

    void Start()
    {
        audioSource = GetComponent<AudioSource>();

        Door existingDoor = GetComponent<Door>();
        if (existingDoor != null)
            Destroy(existingDoor);

        Transform panel = transform.Find("DoorPanel");
        doorColliders = panel != null
            ? panel.GetComponentsInChildren<Collider>(true)
            : GetComponentsInChildren<Collider>(true);

        SetCollidersEnabled(true);

        if (panel != null)
        {
            float halfWidth = panel.localScale.x / 2f;
            Vector3 hingeOffset = hingeOnRightEdge
                ? new Vector3(halfWidth, 0f, 0f)
                : new Vector3(-halfWidth, 0f, 0f);

            Vector3 hingeLocalPos = panel.localPosition + hingeOffset;
            hingeLocalPos.y = 0f;

            GameObject hingeObj = new GameObject("_HingePivot");
            hingeObj.transform.SetParent(transform, false);
            hingeObj.transform.localPosition = hingeLocalPos;
            hingeObj.transform.localRotation = Quaternion.identity;

            panel.SetParent(hingeObj.transform, true);
            hingePivot = hingeObj.transform;
        }

        if (requiredHideSpots != null)
            hideCompleted = new bool[requiredHideSpots.Length];
        else
            hideCompleted = new bool[0];
    }

    void Update()
    {
        if (!allHidesDone)
        {
            if (cachedPlayer == null)
                cachedPlayer = FindFirstObjectByType<RohitFPSController>();

            if (cachedPlayer != null && cachedPlayer.isHidden && cachedPlayer.currentHideObject != null)
            {
                for (int i = 0; i < requiredHideSpots.Length; i++)
                {
                    if (!hideCompleted[i] && cachedPlayer.currentHideObject == requiredHideSpots[i])
                        hideCompleted[i] = true;
                }
            }

            allHidesDone = true;
            for (int i = 0; i < hideCompleted.Length; i++)
            {
                if (!hideCompleted[i])
                {
                    allHidesDone = false;
                    break;
                }
            }
        }

        if (isOpen)
        {
            float direction = openClockwise ? -1f : 1f;
            float target = openAngle * direction;
            currentAngle = Mathf.MoveTowards(currentAngle, target, openSpeed * 100f * Time.deltaTime);

            if (hingePivot != null)
                hingePivot.localRotation = Quaternion.Euler(0f, currentAngle, 0f);

            if (!sceneLoadTriggered)
            {
                timer -= Time.deltaTime;
                if (timer <= 0f)
                {
                    sceneLoadTriggered = true;
                    SceneManager.LoadScene(mainGameSceneName);
                }
            }
        }
    }

    public KeyCode GetInteractKey() => KeyCode.E;

    public string GetPrompt(RohitFPSController player)
    {
        if (isOpen) return "";

        if (!allHidesDone)
        {
            int done = 0;
            for (int i = 0; i < hideCompleted.Length; i++)
                if (hideCompleted[i]) done++;
            return $"Hide in all spots first! ({done}/{hideCompleted.Length})";
        }

        PlayerInventory inventory = player != null ? player.GetComponent<PlayerInventory>() : null;
        if (inventory != null && inventory.HasKey(requiredKey))
            return $"Press E to Unlock ({requiredKey} Key)";

        return $"Locked - Requires {requiredKey} Key";
    }

    public void Interact(RohitFPSController player)
    {
        if (isOpen) return;

        if (!allHidesDone) return;

        PlayerInventory inventory = player != null ? player.GetComponent<PlayerInventory>() : null;
        if (inventory != null && inventory.HasKey(requiredKey))
        {
            isOpen = true;
            timer = transitionDelay;
            SetCollidersEnabled(false);
            PlaySound(unlockSound);
        }
        else
        {
            PlaySound(lockedSound);
        }
    }

    void SetCollidersEnabled(bool enabled)
    {
        if (doorColliders == null) return;
        for (int i = 0; i < doorColliders.Length; i++)
        {
            if (doorColliders[i] != null)
                doorColliders[i].enabled = enabled;
        }
    }

    void PlaySound(AudioClip clip)
    {
        if (clip != null && audioSource != null)
            audioSource.PlayOneShot(clip);
    }
}
