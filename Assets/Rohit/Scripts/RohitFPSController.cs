using UnityEngine;
using UnityEngine.UI;
using UnityEngine.AI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class RohitFPSController : MonoBehaviour
{
    [Header("Movement")]
    public float walkSpeed = 5f;
    public float sprintSpeed = 8f;
    public float jumpHeight = 1.5f;
    public float gravity = -9.81f;
    public float acceleration = 26f;
    public float deceleration = 20f;
    [Range(0f, 1f)] public float airControl = 0.45f;
    public float groundStickForce = 2f;
    [Header("Collision")]
    public bool useCapsulePreCast = true;
    public LayerMask collisionMask = ~0;
    public float collisionSkin = 0.03f;
    public bool useNavMeshBoundaryGuard = true;
    public float navBoundarySkin = 0.06f;

    [Header("Mouse")]
    [Range(50f, 400f)]
    public float mouseSensitivity = 200f;
    [Tooltip("Scale used for Input System raw mouse delta.")]
    public float inputSystemMouseScale = 0.0015f;
    public Transform cameraTransform;

    [Header("Interaction")]
    public float interactDistance = 5f;
    public float keyInteractDistance = 10f;
    public float keyProximityRadius = 2.5f;
    public LayerMask interactableLayer;
    [Tooltip("Solid geometry mask used to block interaction through walls.")]
    public LayerMask interactionOcclusionMask = ~0;
    public Text promptText;

    [Header("Camera Anti-Clip")]
    [Tooltip("Lower near clip reduces wall cutaway when standing close to walls.")]
    public float gameplayNearClip = 0.03f;

    [Header("Key Inventory HUD (Optional)")]
    public Text keyHudText;

    float yVelocity;
    Vector3 horizontalVelocity;
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
        // Ensure stairs/ramp can be walked without requiring a jump.
        controller.stepOffset = Mathf.Max(controller.stepOffset, 0.4f);
        controller.slopeLimit = Mathf.Max(controller.slopeLimit, 55f);
        inventory = GetComponent<PlayerInventory>();

        if (inventory == null)
            inventory = gameObject.AddComponent<PlayerInventory>();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (promptText != null)
            promptText.gameObject.SetActive(false);

        if (keyHudText != null)
            keyHudText.gameObject.SetActive(true);

        if (cameraTransform != null)
        {
            Camera cam = cameraTransform.GetComponent<Camera>();
            if (cam != null)
                cam.nearClipPlane = Mathf.Clamp(gameplayNearClip, 0.01f, 0.1f);
        }
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
            yVelocity = -Mathf.Abs(groundStickForce);

        Vector2 moveInput = GetMoveInput();
        Vector3 input = new Vector3(moveInput.x, 0f, moveInput.y);
        input = Vector3.ClampMagnitude(input, 1f);

        float targetSpeed = IsSprintHeld() ? sprintSpeed : walkSpeed;
        Vector3 desiredHorizontal = transform.TransformDirection(input) * targetSpeed;

        float moveRate = input.sqrMagnitude > 0.0001f ? acceleration : deceleration;
        if (!isGrounded) moveRate *= Mathf.Clamp01(airControl);
        horizontalVelocity = Vector3.MoveTowards(horizontalVelocity, desiredHorizontal, moveRate * Time.deltaTime);

        if (WasJumpPressed() && isGrounded)
            yVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);

        yVelocity += gravity * Time.deltaTime;

        Vector3 finalVelocity = horizontalVelocity + Vector3.up * yVelocity;
        SafeMove(finalVelocity * Time.deltaTime);
    }

    void SafeMove(Vector3 delta)
    {
        if (delta.sqrMagnitude <= 0.0000001f || controller == null)
            return;

        if (useNavMeshBoundaryGuard)
            delta = ClampDeltaByNavMesh(delta);

        if (!useCapsulePreCast)
        {
            controller.Move(delta);
            return;
        }

        Vector3 dir = delta.normalized;
        float dist = delta.magnitude;
        GetControllerCapsule(out Vector3 p1, out Vector3 p2, out float radius);

        if (Physics.CapsuleCast(p1, p2, radius, dir, out RaycastHit hit, dist + collisionSkin, collisionMask, QueryTriggerInteraction.Ignore))
        {
            Transform hitT = hit.collider != null ? hit.collider.transform : null;
            if (hitT != null && (hitT == transform || hitT.IsChildOf(transform)))
            {
                controller.Move(delta);
                return;
            }

            if (IsWalkableSlopeHit(hit, delta))
            {
                controller.Move(delta);
                return;
            }

            float allowed = Mathf.Max(0f, hit.distance - collisionSkin);
            controller.Move(dir * allowed);
            return;
        }

        controller.Move(delta);
    }

    void GetControllerCapsule(out Vector3 p1, out Vector3 p2, out float radius)
    {
        radius = Mathf.Max(0.02f, controller.radius * 0.95f);
        float height = Mathf.Max(controller.height, radius * 2f + 0.01f);
        Vector3 center = transform.TransformPoint(controller.center);
        Vector3 up = transform.up;
        float half = (height * 0.5f) - radius;
        p1 = center + up * half;
        p2 = center - up * half;
    }

    bool IsWalkableSlopeHit(RaycastHit hit, Vector3 delta)
    {
        if (controller == null) return false;

        float slopeAngle = Vector3.Angle(hit.normal, transform.up);
        bool withinSlopeLimit = slopeAngle <= controller.slopeLimit + 0.5f;
        bool notPushingDownHard = Vector3.Dot(delta.normalized, transform.up) > -0.35f;
        return withinSlopeLimit && notPushingDownHard;
    }

    Vector3 ClampDeltaByNavMesh(Vector3 delta)
    {
        if (delta.sqrMagnitude <= 0.0000001f) return delta;

        Vector3 origin = transform.position;
        if (!NavMesh.SamplePosition(origin, out var fromHit, 1.5f, NavMesh.AllAreas))
            return delta;

        Vector3 target = origin + delta;
        if (!NavMesh.Raycast(fromHit.position, target, out var navHit, NavMesh.AllAreas))
            return delta;

        Vector3 allowed = navHit.position - origin;
        float allowedMag = Mathf.Max(0f, allowed.magnitude - Mathf.Max(0.01f, navBoundarySkin));
        if (allowedMag <= 0f) return Vector3.zero;
        return delta.normalized * Mathf.Min(delta.magnitude, allowedMag);
    }

    void Look()
    {
        Vector2 lookInput = GetLookInput(out bool fromInputSystem);
        float mouseX;
        float mouseY;
        if (fromInputSystem)
        {
            mouseX = lookInput.x * mouseSensitivity * inputSystemMouseScale;
            mouseY = lookInput.y * mouseSensitivity * inputSystemMouseScale;
        }
        else
        {
            float scale = 0.015f;
            mouseX = lookInput.x * mouseSensitivity * scale;
            mouseY = lookInput.y * mouseSensitivity * scale;
        }

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

            if (WasKeyPressed(KeyCode.E) && currentHideObject != null)
                currentHideObject.Interact(this);

            return;
        }

        Ray ray = new Ray(cameraTransform.position, cameraTransform.forward);
        Debug.DrawRay(ray.origin, ray.direction * interactDistance, Color.red);

        // Proximity-based key pickup so prompt is consistent regardless of key height/look angle.
        if (TryFindNearbyKey(out KeyItem nearbyKey))
        {
            if (HasLineOfSightToInteractable(nearbyKey, keyInteractDistance))
            {
                string prompt = nearbyKey.GetPrompt(this);
                ShowPrompt(prompt);

                if (WasKeyPressed(nearbyKey.GetInteractKey()))
                    nearbyKey.Interact(this);

                return;
            }
        }

        if (TryFindInteractable(ray, out IInteractable interactable))
        {
            string prompt = interactable.GetPrompt(this);
            ShowPrompt(prompt);

            KeyCode interactKey = interactable.GetInteractKey();
            if (WasKeyPressed(interactKey))
                interactable.Interact(this);

            return;
        }

        HidePrompt();
    }

    bool TryFindInteractable(Ray ray, out IInteractable interactable)
    {
        interactable = null;

        float maxDistance = Mathf.Max(interactDistance, keyInteractDistance);
        RaycastHit[] hits = Physics.RaycastAll(ray, maxDistance, ~0, QueryTriggerInteraction.Collide);
        if (hits == null || hits.Length == 0) return false;

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        IInteractable fallbackInteractable = null;

        for (int i = 0; i < hits.Length; i++)
        {
            Collider col = hits[i].collider;
            if (col == null) continue;

            IInteractable candidate = col.GetComponentInParent<IInteractable>();
            if (candidate != null)
            {
                float allowedDistance = interactDistance;
                if (candidate is KeyItem)
                    allowedDistance = Mathf.Max(interactDistance, keyInteractDistance);

                if (hits[i].distance > allowedDistance)
                    continue;

                bool inMask = interactableLayer.value != 0 && IsLayerInMask(col.gameObject.layer, interactableLayer);
                if (inMask)
                {
                    if (HasLineOfSightToInteractable(candidate, allowedDistance))
                    {
                        interactable = candidate;
                        return true;
                    }
                    continue;
                }

                // Fallback when layer setup is imperfect in scene objects.
                if (fallbackInteractable == null && HasLineOfSightToInteractable(candidate, allowedDistance))
                    fallbackInteractable = candidate;
                continue;
            }

            // A solid non-interactable object blocks interaction behind it.
            if (!col.isTrigger) break;
        }

        if (fallbackInteractable != null)
        {
            interactable = fallbackInteractable;
            return true;
        }

        return false;
    }

    bool TryFindNearbyKey(out KeyItem keyItem)
    {
        keyItem = null;

        KeyItem[] allKeys = FindObjectsByType<KeyItem>(FindObjectsSortMode.None);
        if (allKeys == null || allKeys.Length == 0) return false;

        float bestSqr = float.MaxValue;
        Vector3 playerPos = transform.position;
        float radius = Mathf.Max(0.1f, keyProximityRadius);
        float radiusSqr = radius * radius;

        for (int i = 0; i < allKeys.Length; i++)
        {
            KeyItem candidate = allKeys[i];
            if (candidate == null || !candidate.gameObject.activeInHierarchy) continue;

            Vector3 keyPos = candidate.transform.position;

            // Ignore height difference to keep pickup prompt uniform for bobbing/floating keys.
            Vector2 playerXZ = new Vector2(playerPos.x, playerPos.z);
            Vector2 keyXZ = new Vector2(keyPos.x, keyPos.z);
            float sqr = (playerXZ - keyXZ).sqrMagnitude;
            if (sqr > radiusSqr) continue;

            if (sqr < bestSqr)
            {
                bestSqr = sqr;
                keyItem = candidate;
            }
        }

        return keyItem != null;
    }

    bool HasLineOfSightToInteractable(IInteractable interactable, float maxDistance)
    {
        if (interactable == null || cameraTransform == null) return false;
        Component comp = interactable as Component;
        if (comp == null) return false;

        Collider targetCollider = comp.GetComponentInChildren<Collider>();
        if (targetCollider == null) targetCollider = comp.GetComponent<Collider>();
        if (targetCollider == null) return false;

        Vector3 origin = cameraTransform.position;
        Vector3 target = targetCollider.bounds.center;
        Vector3 dir = target - origin;
        float dist = dir.magnitude;
        if (dist <= 0.001f || dist > maxDistance) return false;

        if (!Physics.Raycast(origin, dir.normalized, out RaycastHit hit, dist + 0.05f, interactionOcclusionMask, QueryTriggerInteraction.Ignore))
            return true;

        Transform ht = hit.collider != null ? hit.collider.transform : null;
        if (ht == null) return false;
        return ht == comp.transform || ht.IsChildOf(comp.transform) || comp.transform.IsChildOf(ht);
    }

    bool IsLayerInMask(int layer, LayerMask mask)
    {
        return (mask.value & (1 << layer)) != 0;
    }

    Vector2 GetMoveInput()
    {
        float x = 0f;
        float y = 0f;

#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            if (Keyboard.current.aKey.isPressed) x -= 1f;
            if (Keyboard.current.dKey.isPressed) x += 1f;
            if (Keyboard.current.sKey.isPressed) y -= 1f;
            if (Keyboard.current.wKey.isPressed) y += 1f;
            return new Vector2(x, y);
        }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        x += Input.GetAxisRaw("Horizontal");
        y += Input.GetAxisRaw("Vertical");
#endif
        return new Vector2(x, y);
    }

    Vector2 GetLookInput(out bool fromInputSystem)
    {
        fromInputSystem = false;
        float x = 0f;
        float y = 0f;

#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null)
        {
            Vector2 d = Mouse.current.delta.ReadValue();
            fromInputSystem = true;
            return new Vector2(d.x, d.y);
        }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        x += Input.GetAxis("Mouse X");
        y += Input.GetAxis("Mouse Y");
#endif
        return new Vector2(x, y);
    }

    bool IsSprintHeld()
    {
        bool held = false;
#if ENABLE_LEGACY_INPUT_MANAGER
        held |= Input.GetKey(KeyCode.LeftShift);
#endif
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null) held |= Keyboard.current.leftShiftKey.isPressed;
#endif
        return held;
    }

    bool WasJumpPressed()
    {
        return WasKeyPressed(KeyCode.Space);
    }

    bool WasKeyPressed(KeyCode key)
    {
        bool pressed = false;
#if ENABLE_LEGACY_INPUT_MANAGER
        pressed |= Input.GetKeyDown(key);
#endif
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            switch (key)
            {
                case KeyCode.E: pressed |= Keyboard.current.eKey.wasPressedThisFrame; break;
                case KeyCode.F: pressed |= Keyboard.current.fKey.wasPressedThisFrame; break;
                case KeyCode.P: pressed |= Keyboard.current.pKey.wasPressedThisFrame; break;
                case KeyCode.G: pressed |= Keyboard.current.gKey.wasPressedThisFrame; break;
                case KeyCode.N: pressed |= Keyboard.current.nKey.wasPressedThisFrame; break;
                case KeyCode.R: pressed |= Keyboard.current.rKey.wasPressedThisFrame; break;
                case KeyCode.Space: pressed |= Keyboard.current.spaceKey.wasPressedThisFrame; break;
                case KeyCode.Return: pressed |= Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.numpadEnterKey.wasPressedThisFrame; break;
                case KeyCode.Escape: pressed |= Keyboard.current.escapeKey.wasPressedThisFrame; break;
                default: break;
            }
        }
#endif
        return pressed;
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

        string circle = inventory.HasCircle ? "<color=green>[O]</color>" : "<color=red>[O]</color>";
        string rectangle = inventory.HasRectangle ? "<color=green>[R]</color>" : "<color=red>[R]</color>";
        string square = inventory.HasSquare ? "<color=green>[S]</color>" : "<color=red>[S]</color>";

        keyHudText.text = $"Keys: {circle} {rectangle} {square}";
    }

    public void HideAt(Transform hidePoint, HideableObject hideObject)
    {
        controller.enabled = false;
        transform.position = hidePoint.position;
        controller.enabled = true;

        horizontalVelocity = Vector3.zero;
        yVelocity = 0f;
        isHidden = true;
        currentHideObject = hideObject;
    }

    public void ExitHide(Vector3 exitPosition)
    {
        controller.enabled = false;
        transform.position = exitPosition;
        controller.enabled = true;

        horizontalVelocity = Vector3.zero;
        yVelocity = 0f;
        isHidden = false;
        currentHideObject = null;
    }
}
