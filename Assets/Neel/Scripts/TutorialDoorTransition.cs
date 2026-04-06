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

    [Header("Scene Transition")]
    [Tooltip("Name of the main game scene to load when the door opens.")]
    public string mainGameSceneName = "NewLevel";
    [Tooltip("Delay in seconds after the door opens before loading the scene.")]
    public float transitionDelay = 1.5f;

    [Header("Audio (Optional)")]
    public AudioClip unlockSound;
    public AudioClip lockedSound;

    bool playerHasHidden;
    bool isOpen;
    bool sceneLoadTriggered;
    float currentAngle;
    float timer;
    Quaternion closedLocalRotation;
    AudioSource audioSource;
    RohitFPSController cachedPlayer;
    Collider[] doorColliders;

    void Start()
    {
        closedLocalRotation = transform.localRotation;
        audioSource = GetComponent<AudioSource>();

        Door existingDoor = GetComponent<Door>();
        if (existingDoor != null)
            Destroy(existingDoor);

        Transform panel = transform.Find("DoorPanel");
        doorColliders = panel != null
            ? panel.GetComponentsInChildren<Collider>(true)
            : GetComponentsInChildren<Collider>(true);

        SetCollidersEnabled(true);
    }

    void Update()
    {
        if (!playerHasHidden)
        {
            if (cachedPlayer == null)
                cachedPlayer = FindFirstObjectByType<RohitFPSController>();
            if (cachedPlayer != null && cachedPlayer.isHidden)
                playerHasHidden = true;
        }

        if (isOpen)
        {
            float direction = openClockwise ? -1f : 1f;
            float target = openAngle * direction;
            currentAngle = Mathf.MoveTowards(currentAngle, target, openSpeed * 100f * Time.deltaTime);

            transform.localRotation = closedLocalRotation * Quaternion.Euler(0f, currentAngle, 0f);

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

        if (!playerHasHidden)
            return "Try hiding in the container first!";

        PlayerInventory inventory = player != null ? player.GetComponent<PlayerInventory>() : null;
        if (inventory != null && inventory.HasKey(requiredKey))
            return $"Press E to Unlock ({requiredKey} Key)";

        return $"Locked - Requires {requiredKey} Key";
    }

    public void Interact(RohitFPSController player)
    {
        if (isOpen) return;

        if (!playerHasHidden) return;

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
