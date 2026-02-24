using UnityEngine;
using Sushil.Systems;

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

    private bool isOpen = false;
    private Quaternion closedRotation;
    private Vector3 closedPosition;
    private float currentOpenAngle;
    private AudioSource audioSource;

    void Start()
    {
        closedPosition = transform.position;
        closedRotation = transform.rotation;
        audioSource = GetComponent<AudioSource>();
        currentOpenAngle = 0f;

        if (winUI != null)
            winUI.SetActive(false);
    }

    void Update()
    {
        float direction = openClockwise ? 1f : -1f;
        float targetAngle = isOpen ? (openAngle * direction) : 0f;
        currentOpenAngle = Mathf.MoveTowards(currentOpenAngle, targetAngle, openSpeed * 100f * Time.deltaTime);
        ApplyHingePose(currentOpenAngle);
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

    void ApplyHingePose(float angleY)
    {
        Quaternion q = closedRotation * Quaternion.Euler(0f, angleY, 0f);
        Vector3 hingeWorld = closedPosition + closedRotation * hingeLocalOffset;
        Vector3 pos = hingeWorld - (q * hingeLocalOffset);
        transform.SetPositionAndRotation(pos, q);
    }
}
