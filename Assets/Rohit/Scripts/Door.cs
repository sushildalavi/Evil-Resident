using UnityEngine;

public class Door : MonoBehaviour, IInteractable
{
    [Header("Lock Settings")]
    public KeyType requiredKey;
    public bool isLocked = true;

    [Header("Open Animation")]
    public float openAngle = 90f;
    public float openSpeed = 2f;

    [Header("Audio (Optional)")]
    public AudioClip unlockSound;
    public AudioClip lockedSound;

    private bool isOpen = false;
    private Quaternion closedRotation;
    private Quaternion openRotation;
    private AudioSource audioSource;

    void Start()
    {
        closedRotation = transform.rotation;
        openRotation = closedRotation * Quaternion.Euler(0, openAngle, 0);
        audioSource = GetComponent<AudioSource>();
    }

    void Update()
    {
        Quaternion target = isOpen ? openRotation : closedRotation;
        transform.rotation = Quaternion.Slerp(transform.rotation, target, Time.deltaTime * openSpeed);
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

    private void PlaySound(AudioClip clip)
    {
        if (clip != null && audioSource != null)
            audioSource.PlayOneShot(clip);
    }
}
