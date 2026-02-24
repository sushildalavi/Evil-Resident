using UnityEngine;
using UnityEngine.UI;

public class RohitFPSController : MonoBehaviour
{
    [Header("Movement")]
    public float walkSpeed = 5f;
    public float sprintSpeed = 8f;
    public float jumpHeight = 1.5f;
    public float gravity = -9.81f;

    [Header("Mouse")]
    public float mouseSensitivity = 200f;
    public Transform cameraTransform;

    [Header("Interaction")]
    public float interactDistance = 5f;
    public LayerMask interactableLayer;
    public Text promptText;

    [Header("Key Inventory HUD (Optional)")]
    public Text keyHudText;

    float yVelocity;
    float xRotation = 0f;
    CharacterController controller;
    PlayerInventory inventory;

    [HideInInspector] public bool isHidden = false;
    [HideInInspector] public HideableObject currentHideObject;

    void OnDisable()
    {
        Invoke("ForceEnable", 0.1f);
    }

    void ForceEnable()
    {
        if (!enabled)
            enabled = true;
    }

    void Start()
    {
        controller = GetComponent<CharacterController>();
        inventory = GetComponent<PlayerInventory>();

        if (inventory == null)
            inventory = gameObject.AddComponent<PlayerInventory>();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (promptText != null)
            promptText.gameObject.SetActive(false);

        if (keyHudText != null)
            keyHudText.gameObject.SetActive(true);
    }

    void Update()
    {
        if (!isHidden)
            Move();

        Look();
        HandleInteraction();
        UpdateKeyHud();
    }

    void Move()
    {
        bool isGrounded = controller.isGrounded;

        if (isGrounded && yVelocity < 0)
            yVelocity = -2f;

        float x = Input.GetAxis("Horizontal");
        float z = Input.GetAxis("Vertical");

        Vector3 move = transform.right * x + transform.forward * z;

        float speed = Input.GetKey(KeyCode.LeftShift) ? sprintSpeed : walkSpeed;

        controller.Move(move * speed * Time.deltaTime);

        if (Input.GetKeyDown(KeyCode.Space) && isGrounded)
            yVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);

        yVelocity += gravity * Time.deltaTime;
        controller.Move(Vector3.up * yVelocity * Time.deltaTime);
    }

    void Look()
    {
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * Time.deltaTime;

        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);

        cameraTransform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
        transform.Rotate(Vector3.up * mouseX);
    }

    void HandleInteraction()
    {
        if (isHidden)
        {
            ShowPrompt("Press E to Exit Hiding Spot");

            if (Input.GetKeyDown(KeyCode.E) && currentHideObject != null)
                currentHideObject.Interact(this);

            return;
        }

        Ray ray = new Ray(cameraTransform.position, cameraTransform.forward);
        Debug.DrawRay(ray.origin, ray.direction * interactDistance, Color.red);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, interactDistance, interactableLayer))
        {
            IInteractable interactable = hit.collider.GetComponentInParent<IInteractable>();

            if (interactable != null)
            {
                string prompt = interactable.GetPrompt(this);
                ShowPrompt(prompt);

                KeyCode interactKey = interactable.GetInteractKey();
                if (Input.GetKeyDown(interactKey))
                    interactable.Interact(this);

                return;
            }
        }

        HidePrompt();
    }

    void ShowPrompt(string text)
    {
        if (promptText != null && !string.IsNullOrEmpty(text))
        {
            promptText.gameObject.SetActive(true);
            promptText.text = text;
        }
    }

    void HidePrompt()
    {
        if (promptText != null)
            promptText.gameObject.SetActive(false);
    }

    void UpdateKeyHud()
    {
        if (keyHudText == null || inventory == null) return;

        string circle = inventory.HasCircle ? "<color=green>O</color>" : "<color=red>O</color>";
        string rectangle = inventory.HasRectangle ? "<color=green>▭</color>" : "<color=red>▭</color>";
        string square = inventory.HasSquare ? "<color=green>□</color>" : "<color=red>□</color>";

        keyHudText.text = $"Keys: {circle} {rectangle} {square}";
    }

    public void HideAt(Transform hidePoint, HideableObject hideObject)
    {
        controller.enabled = false;
        transform.position = hidePoint.position;
        controller.enabled = true;

        isHidden = true;
        currentHideObject = hideObject;
    }

    public void ExitHide(Vector3 exitPosition)
    {
        controller.enabled = false;
        transform.position = exitPosition;
        controller.enabled = true;

        isHidden = false;
        currentHideObject = null;
    }
}
