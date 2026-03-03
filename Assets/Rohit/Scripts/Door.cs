using UnityEngine;

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

    private bool isOpen = false;
    private Quaternion closedLocalRotation;
    private Vector3 closedLocalPosition;
    private float currentOpenAngle;
    private AudioSource audioSource;

    void Start()
    {
        closedLocalPosition = transform.localPosition;
        closedLocalRotation = transform.localRotation;
        audioSource = GetComponent<AudioSource>();
        currentOpenAngle = 0f;
    }

    void Update()
    {
        // Invert configured swing so doors open inward by default across the level.
        float direction = openClockwise ? -1f : 1f;
        float targetAngle = isOpen ? (openAngle * direction) : 0f;
        currentOpenAngle = Mathf.MoveTowards(currentOpenAngle, targetAngle, openSpeed * 100f * Time.deltaTime);

        ApplyHingePose(currentOpenAngle);
    }

    public KeyCode GetInteractKey() => KeyCode.E;

    public string GetPrompt(RohitFPSController player)
    {
        if (isOpen) return "";

        if (!isLocked) return "Press E to Open Door";

        PlayerInventory inventory = player.GetComponent<PlayerInventory>();
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

        PlayerInventory inventory = player.GetComponent<PlayerInventory>();
        if (inventory != null && inventory.HasKey(requiredKey))
        {
            isLocked = false;
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

    private void OpenDoor()
    {
        isOpen = true;
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
}
