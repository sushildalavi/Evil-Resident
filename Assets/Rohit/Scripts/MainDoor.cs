using UnityEngine;

public class MainDoor : MonoBehaviour, IInteractable
{
    [Header("Open Animation")]
    public float openAngle = 90f;
    public float openSpeed = 2f;

    [Header("Audio (Optional)")]
    public AudioClip unlockSound;
    public AudioClip lockedSound;

    [Header("Win Condition")]
    public GameObject winUI;

    private bool isOpen = false;
    private Quaternion closedRotation;
    private Quaternion openRotation;
    private AudioSource audioSource;

    void Start()
    {
        closedRotation = transform.rotation;
        openRotation = closedRotation * Quaternion.Euler(0, openAngle, 0);
        audioSource = GetComponent<AudioSource>();

        if (winUI != null)
            winUI.SetActive(false);
    }

    void Update()
    {
        Quaternion target = isOpen ? openRotation : closedRotation;
        transform.rotation = Quaternion.Slerp(transform.rotation, target, Time.deltaTime * openSpeed);
    }

    public KeyCode GetInteractKey() => KeyCode.E;

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
            PlaySound(unlockSound);
            Debug.Log("The main door opens! You escaped!");

            if (winUI != null)
                winUI.SetActive(true);

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
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
}
