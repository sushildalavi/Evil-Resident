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
    [Tooltip("Extra multiplier applied only on WebGL builds to match editor look feel.")]
    [Range(0.05f, 2f)] public float webGLMouseMultiplier = 0.35f;
    [Tooltip("Clamp raw pointer delta to reduce single-frame browser spikes.")]
    [Range(5f, 500f)] public float maxMouseDeltaPerFrame = 60f;
    public Transform cameraTransform;

    [Header("Interaction")]
    public float interactDistance = 1.5f;
    public float keyInteractDistance = 2f;
    public float keyProximityRadius = 1f;
    public float fuseProximityRadius = 0.5f;
    [Tooltip("Small wall dials are easy to miss with a pure ray hit, so give them a little extra usable range.")]
    public float puzzleWheelInteractDistance = 2f;
    [Tooltip("Player proximity radius used to keep dial prompts stable near the puzzle wall.")]
    public float puzzleWheelProximityRadius = 1.5f;
    [Range(0.75f, 0.999f)]
    [Tooltip("How close to the center of view a puzzle dial must be before the prompt snaps to it.")]
    public float puzzleWheelPromptViewDot = 0.88f;
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
    Vector3 defaultCameraLocalPos;
    Quaternion defaultCameraLocalRot;
    bool hasDefaultCameraPose;
    Vector3 preHidePosition;
    Quaternion preHideRotation;
    bool hasPreHidePosition;
    bool hasPreHideRotation;
    CharacterController controller;
    PlayerInventory inventory;

    [HideInInspector] public bool isHidden = false;
    [HideInInspector] public HideableObject currentHideObject;
    [HideInInspector] public bool isInPuzzle = false;

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
        interactDistance = 1.5f;
        keyInteractDistance = 2f;
        keyProximityRadius = 1f;
        fuseProximityRadius = 0.5f;
        puzzleWheelInteractDistance = 2f;
        puzzleWheelProximityRadius = 1.5f;

        CollectibleHUD.EnsureExists();

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
            keyHudText.gameObject.SetActive(!CollectibleStatusHUD.Exists);

        if (cameraTransform != null)
        {
            Camera cam = cameraTransform.GetComponent<Camera>();
            if (cam != null)
                cam.nearClipPlane = Mathf.Clamp(gameplayNearClip, 0.01f, 0.1f);

            defaultCameraLocalPos = cameraTransform.localPosition;
            defaultCameraLocalRot = cameraTransform.localRotation;
            hasDefaultCameraPose = true;
        }
    }

    void Update()
    {
        if (!isHidden && !isInPuzzle)
            Move();

        if (!isInPuzzle)
            Look();

        if (!isInPuzzle)
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
            Vector2 clampedLook = Vector2.ClampMagnitude(lookInput, Mathf.Max(5f, maxMouseDeltaPerFrame));
            float webScale = Application.platform == RuntimePlatform.WebGLPlayer ? webGLMouseMultiplier : 1f;
            mouseX = clampedLook.x * mouseSensitivity * inputSystemMouseScale * webScale;
            mouseY = clampedLook.y * mouseSensitivity * inputSystemMouseScale * webScale;
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
            string hidePrompt = currentHideObject != null
                ? currentHideObject.GetPrompt(this)
                : "Press F to Exit Hiding Spot";
            ShowPrompt(hidePrompt);

            KeyCode hideKey = currentHideObject != null ? currentHideObject.GetInteractKey() : KeyCode.F;
            if (WasKeyPressed(hideKey) && currentHideObject != null)
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

        // Proximity-based fuse pickup to avoid missing small fuse colliders.
        if (TryFindNearbyFuse(out FusePickup nearbyFuse))
        {
            if (HasLineOfSightToInteractable(nearbyFuse, keyInteractDistance))
            {
                string prompt = nearbyFuse.GetPrompt(this);
                ShowPrompt(prompt);

                if (WasKeyPressed(nearbyFuse.GetInteractKey()))
                    nearbyFuse.Interact(this);

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

        if (TryFindNearbyPuzzleWheel(out PuzzleWheel nearbyWheel))
        {
            string prompt = nearbyWheel.GetPrompt(this);
            ShowPrompt(prompt);

            if (WasKeyPressed(nearbyWheel.GetInteractKey()))
                nearbyWheel.Interact(this);

            return;
        }

        HidePrompt();
    }

    bool TryFindInteractable(Ray ray, out IInteractable interactable)
    {
        interactable = null;

        float maxDistance = Mathf.Max(Mathf.Max(interactDistance, keyInteractDistance), puzzleWheelInteractDistance);
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
                if (candidate is FusePickup)
                    continue;

                float allowedDistance = interactDistance;
                if (candidate is KeyItem)
                    allowedDistance = Mathf.Max(interactDistance, keyInteractDistance);
                else if (candidate is PuzzleWheel)
                    allowedDistance = Mathf.Max(interactDistance, puzzleWheelInteractDistance);

                if (hits[i].distance > allowedDistance)
                    continue;

                if (IsDirectHitOnInteractable(candidate, col.transform))
                {
                    interactable = candidate;
                    return true;
                }

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
        float radius = Mathf.Max(0.01f, keyProximityRadius);
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

    bool TryFindNearbyPuzzleWheel(out PuzzleWheel wheel)
    {
        wheel = null;

        if (cameraTransform == null)
            return false;

        PuzzleWheel[] allWheels = FindObjectsByType<PuzzleWheel>(FindObjectsSortMode.None);
        if (allWheels == null || allWheels.Length == 0)
            return false;

        Vector3 origin = cameraTransform.position;
        Vector3 forward = cameraTransform.forward;
        float maxDistance = Mathf.Max(interactDistance, puzzleWheelInteractDistance);
        float minViewDot = Mathf.Clamp(puzzleWheelPromptViewDot, 0.75f, 0.999f);
        float proximityRadius = Mathf.Max(0.01f, puzzleWheelProximityRadius);
        float proximityRadiusSqr = proximityRadius * proximityRadius;
        float bestScore = float.NegativeInfinity;

        for (int i = 0; i < allWheels.Length; i++)
        {
            PuzzleWheel candidate = allWheels[i];
            if (candidate == null || !candidate.gameObject.activeInHierarchy)
                continue;

            if (!TryGetInteractableFocusPoint(candidate, origin, out Vector3 focusPoint, out float dist))
                continue;

            if (dist > maxDistance)
                continue;

            Vector3 playerToWheel = candidate.transform.position - transform.position;
            playerToWheel.y = 0f;
            if (playerToWheel.sqrMagnitude > proximityRadiusSqr)
                continue;

            Vector3 toTarget = (focusPoint - origin).normalized;
            float dot = Vector3.Dot(forward, toTarget);
            if (dot < minViewDot)
                continue;

            if (!HasLineOfSightToInteractable(candidate, maxDistance))
                continue;

            float score = (dot * 120f) - (dist * 1.75f) - playerToWheel.magnitude;
            if (score > bestScore)
            {
                bestScore = score;
                wheel = candidate;
            }
        }

        return wheel != null;
    }

    bool TryFindNearbyFuse(out FusePickup fusePickup)
    {
        fusePickup = null;

        FusePickup[] allFuses = FindObjectsByType<FusePickup>(FindObjectsSortMode.None);
        if (allFuses == null || allFuses.Length == 0) return false;

        float bestSqr = float.MaxValue;
        Vector3 playerPos = transform.position;
        float radius = Mathf.Max(0.01f, fuseProximityRadius);
        float radiusSqr = radius * radius;

        for (int i = 0; i < allFuses.Length; i++)
        {
            FusePickup candidate = allFuses[i];
            if (candidate == null || !candidate.gameObject.activeInHierarchy) continue;

            Vector3 fusePos = candidate.transform.position;

            // Keep XZ-only distance so uneven floor height does not block pickup prompt.
            Vector2 playerXZ = new Vector2(playerPos.x, playerPos.z);
            Vector2 fuseXZ = new Vector2(fusePos.x, fusePos.z);
            float sqr = (playerXZ - fuseXZ).sqrMagnitude;
            if (sqr > radiusSqr) continue;

            if (sqr < bestSqr)
            {
                bestSqr = sqr;
                fusePickup = candidate;
            }
        }

        return fusePickup != null;
    }

    bool HasLineOfSightToInteractable(IInteractable interactable, float maxDistance)
    {
        if (interactable == null || cameraTransform == null) return false;
        Component comp = interactable as Component;
        if (comp == null) return false;

        Collider targetCollider = GetPrimaryInteractableCollider(comp);
        if (targetCollider == null) return false;

        Vector3 origin = cameraTransform.position;
        Bounds bounds = targetCollider.bounds;

        Vector3 closestPoint = targetCollider.ClosestPoint(origin);
        if (closestPoint != origin && HasLineOfSightToPoint(comp, origin, closestPoint, maxDistance))
            return true;

        Vector3 center = bounds.center;
        if (HasLineOfSightToPoint(comp, origin, center, maxDistance))
            return true;

        float verticalOffset = bounds.extents.y * 0.6f;
        if (verticalOffset > 0.02f)
        {
            if (HasLineOfSightToPoint(comp, origin, center + (Vector3.up * verticalOffset), maxDistance))
                return true;
            if (HasLineOfSightToPoint(comp, origin, center - (Vector3.up * verticalOffset), maxDistance))
                return true;
        }

        float sideOffset = Mathf.Max(bounds.extents.x, bounds.extents.z) * 0.45f;
        if (sideOffset > 0.02f)
        {
            Vector3 side = cameraTransform.right * sideOffset;
            if (HasLineOfSightToPoint(comp, origin, center + side, maxDistance))
                return true;
            if (HasLineOfSightToPoint(comp, origin, center - side, maxDistance))
                return true;
        }

        return false;
    }

    bool HasLineOfSightToPoint(Component comp, Vector3 origin, Vector3 target, float maxDistance)
    {
        Vector3 dir = target - origin;
        float dist = dir.magnitude;
        if (dist <= 0.001f || dist > maxDistance)
            return false;

        if (!Physics.Raycast(origin, dir.normalized, out RaycastHit hit, dist + 0.05f, interactionOcclusionMask, QueryTriggerInteraction.Ignore))
            return true;

        Transform ht = hit.collider != null ? hit.collider.transform : null;
        if (ht == null)
            return false;

        return ht == comp.transform || ht.IsChildOf(comp.transform) || comp.transform.IsChildOf(ht);
    }

    bool TryGetInteractableFocusPoint(IInteractable interactable, Vector3 origin, out Vector3 focusPoint, out float distance)
    {
        focusPoint = Vector3.zero;
        distance = 0f;

        Component comp = interactable as Component;
        if (comp == null)
            return false;

        Collider targetCollider = GetPrimaryInteractableCollider(comp);
        if (targetCollider == null)
            return false;

        focusPoint = targetCollider.ClosestPoint(origin);
        if ((focusPoint - origin).sqrMagnitude <= 0.0001f)
            focusPoint = targetCollider.bounds.center;

        distance = Vector3.Distance(origin, focusPoint);
        return distance > 0.001f;
    }

    Collider GetPrimaryInteractableCollider(Component comp)
    {
        if (comp == null)
            return null;

        Collider targetCollider = comp.GetComponentInChildren<Collider>();
        if (targetCollider == null)
            targetCollider = comp.GetComponent<Collider>();
        return targetCollider;
    }

    bool IsDirectHitOnInteractable(IInteractable interactable, Transform hitTransform)
    {
        if (interactable == null || hitTransform == null)
            return false;

        Component comp = interactable as Component;
        if (comp == null)
            return false;

        return hitTransform == comp.transform || hitTransform.IsChildOf(comp.transform) || comp.transform.IsChildOf(hitTransform);
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
        if (keyHudText != null && CollectibleStatusHUD.Exists)
        {
            if (keyHudText.gameObject.activeSelf)
                keyHudText.gameObject.SetActive(false);
            return;
        }

        if (keyHudText == null || inventory == null || !keyHudText.gameObject.activeInHierarchy) return;

        string circle = inventory.HasCircle ? "<color=green>[O]</color>" : "<color=red>[O]</color>";
        string rectangle = inventory.HasRectangle ? "<color=green>[R]</color>" : "<color=red>[R]</color>";
        string square = inventory.HasSquare ? "<color=green>[S]</color>" : "<color=red>[S]</color>";

        keyHudText.text = $"Keys: {circle} {rectangle} {square}";
    }

    public void HideAt(Transform hidePoint, HideableObject hideObject)
    {
        preHidePosition = transform.position;
        hasPreHidePosition = true;
        preHideRotation = transform.rotation;
        hasPreHideRotation = true;
        float oppositeYaw = preHideRotation.eulerAngles.y + 180f;

        controller.enabled = false;
        if (hidePoint != null)
            transform.SetPositionAndRotation(hidePoint.position, hidePoint.rotation);
        else
        {
            if (hideObject != null)
                transform.SetPositionAndRotation(hideObject.transform.position, hideObject.transform.rotation);
        }
        controller.enabled = true;

        Transform effectiveHiddenCameraPoint = hideObject != null ? hideObject.GetEffectiveHiddenCameraPoint() : null;
        if (effectiveHiddenCameraPoint != null && cameraTransform != null)
        {
            transform.rotation = Quaternion.Euler(0f, oppositeYaw, 0f);

            // Keep camera attached to player pivot so hidden yaw rotates in-place (no orbit/radius).
            float hiddenCamY = cameraTransform.localPosition.y;
            if (hasDefaultCameraPose)
                hiddenCamY = defaultCameraLocalPos.y;
            cameraTransform.localPosition = new Vector3(0f, hiddenCamY, 0f);

            Vector3 localCamOffset = cameraTransform.localPosition;
            Vector3 worldCamOffset = transform.TransformVector(localCamOffset);
            transform.position = effectiveHiddenCameraPoint.position - worldCamOffset;

            xRotation = NormalizePitch(effectiveHiddenCameraPoint.eulerAngles.x);
            cameraTransform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
        }
        else
        {
            transform.rotation = Quaternion.Euler(0f, oppositeYaw, 0f);
            if (cameraTransform != null)
            {
                float hiddenCamY = cameraTransform.localPosition.y;
                if (hasDefaultCameraPose)
                    hiddenCamY = defaultCameraLocalPos.y;
                cameraTransform.localPosition = new Vector3(0f, hiddenCamY, 0f);
                cameraTransform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
            }
        }

        horizontalVelocity = Vector3.zero;
        yVelocity = 0f;
        isHidden = true;
        currentHideObject = hideObject;
        hideObject?.SetExteriorOnlyRenderersVisible(false);
    }

    public void ExitHide(Vector3 exitPosition)
    {
        HideableObject exiting = currentHideObject;

        controller.enabled = false;
        transform.position = exitPosition;
        if (hasPreHideRotation)
            transform.rotation = preHideRotation;
        controller.enabled = true;

        if (hasDefaultCameraPose && cameraTransform != null)
        {
            cameraTransform.localPosition = defaultCameraLocalPos;
            cameraTransform.localRotation = defaultCameraLocalRot;
            xRotation = NormalizePitch(cameraTransform.localEulerAngles.x);
        }

        horizontalVelocity = Vector3.zero;
        yVelocity = 0f;
        isHidden = false;
        currentHideObject = null;
        hasPreHidePosition = false;
        hasPreHideRotation = false;

        exiting?.SetExteriorOnlyRenderersVisible(true);
    }

    public Vector3 ResolveSafeExitPosition(HideableObject hideObject, Vector3 requestedExitPosition)
    {
        if (hasPreHidePosition)
        {
            float upNudge = controller != null ? Mathf.Max(0.1f, controller.skinWidth + 0.05f) : 0.15f;
            Vector3 rawPreHide = preHidePosition;
            if (!WouldControllerOverlapAt(rawPreHide))
                return rawPreHide;

            Vector3 liftedRawPreHide = rawPreHide + Vector3.up * upNudge;
            if (!WouldControllerOverlapAt(liftedRawPreHide))
                return liftedRawPreHide;

            Vector3 snappedPreHide = SnapToNavMesh(rawPreHide);
            if (!WouldControllerOverlapAt(snappedPreHide))
                return snappedPreHide;

            Vector3 liftedSnappedPreHide = snappedPreHide + Vector3.up * upNudge;
            if (!WouldControllerOverlapAt(liftedSnappedPreHide))
                return liftedSnappedPreHide;

            // Final fallback: prefer previous known player location over risky side offsets.
            return rawPreHide;
        }

        Vector3[] candidates;

        if (hideObject != null)
        {
            Transform t = hideObject.transform;
            candidates = new[]
            {
                requestedExitPosition,
                t.position + t.forward * 1.2f,
                t.position - t.forward * 1.2f,
                t.position + t.right * 1.2f,
                t.position - t.right * 1.2f,
                t.position + Vector3.up * 0.2f
            };
        }
        else
        {
            candidates = new[] { requestedExitPosition };
        }

        for (int i = 0; i < candidates.Length; i++)
        {
            Vector3 candidate = SnapToNavMesh(candidates[i]);
            if (!WouldControllerOverlapAt(candidate))
                return candidate;
        }

        return transform.position;
    }

    Vector3 SnapToNavMesh(Vector3 position)
    {
        if (!useNavMeshBoundaryGuard)
            return position;

        if (NavMesh.SamplePosition(position, out var hit, 2f, NavMesh.AllAreas))
            return hit.position;

        return position;
    }

    bool WouldControllerOverlapAt(Vector3 worldPosition)
    {
        if (controller == null)
            return false;

        float radius = Mathf.Max(0.02f, controller.radius * 0.95f);
        float height = Mathf.Max(controller.height, radius * 2f + 0.01f);

        Vector3 center = worldPosition + transform.TransformVector(controller.center);
        Vector3 up = transform.up;
        float half = (height * 0.5f) - radius;
        Vector3 p1 = center + up * half;
        Vector3 p2 = center - up * half;

        Collider[] overlaps = Physics.OverlapCapsule(
            p1,
            p2,
            radius,
            collisionMask,
            QueryTriggerInteraction.Ignore
        );

        for (int i = 0; i < overlaps.Length; i++)
        {
            Collider c = overlaps[i];
            if (c == null) continue;

            Transform hitT = c.transform;
            if (hitT == transform || hitT.IsChildOf(transform))
                continue;

            return true;
        }

        return false;
    }

    static float NormalizePitch(float eulerX)
    {
        if (eulerX > 180f) eulerX -= 360f;
        return Mathf.Clamp(eulerX, -90f, 90f);
    }
}
