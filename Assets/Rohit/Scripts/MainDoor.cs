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
        // Use local-space hinge math to stay correct under non-uniform parent scaling.
        Quaternion localRot = closedLocalRotation * Quaternion.Euler(0f, angleY, 0f);
        Vector3 hingeLocal = hingeLocalOffset;
        Vector3 localHinge = closedLocalPosition + closedLocalRotation * hingeLocal;
        Vector3 localPos = localHinge - (localRot * hingeLocal);

        transform.localPosition = localPos;
        transform.localRotation = localRot;
    }
}
