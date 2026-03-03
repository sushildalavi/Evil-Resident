using UnityEngine;
using UnityEngine.AI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Sushil.Demo
{
    [RequireComponent(typeof(CharacterController))]
    public class SushilFPSController : MonoBehaviour
    {
        [Header("Movement")]
        public float walkSpeed = 5f;
        public float sprintSpeed = 8f;
        public float jumpHeight = 1.5f;
        public float gravity = -9.81f;
        [Header("Collision")]
        public bool useCapsulePreCast = true;
        public LayerMask collisionMask = ~0;
        public float collisionSkin = 0.03f;
        public bool useNavMeshBoundaryGuard = true;
        public float navBoundarySkin = 0.06f;

        [Header("Look")]
        public float mouseSensitivity = 200f;
        public float inputSystemMouseScale = 0.0015f;
        public Transform cameraTransform;
        public bool lockCursorOnStart = true;

        [Header("Ground Check")]
        public Transform groundCheck;
        public float groundDistance = 0.4f;
        public LayerMask groundMask = ~0;

        [Header("Crouch")]
        public bool enableCrouch = true;
        public float crouchHeight = 1.2f;
        public float crouchSpeed = 3.2f;

        float yVelocity;
        float xRotation;
        float standHeight;
        Vector3 standCenter;
        bool isCursorLocked = true;
        bool isCrouching;
        CharacterController controller;

        void Start()
        {
            controller = GetComponent<CharacterController>();
            standHeight = controller.height;
            standCenter = controller.center;

            if (lockCursorOnStart) LockCursor(true);
            else LockCursor(false);

            if (cameraTransform == null && Camera.main != null) cameraTransform = Camera.main.transform;
            if (groundCheck == null) groundCheck = transform;
        }

        void Update()
        {
            UpdateCursorLock();
            if (!isCursorLocked) return;

            Move();
            Look();
        }

        void Move()
        {
            bool grounded = IsGrounded();
            if (grounded && yVelocity < 0f) yVelocity = -2f;

            isCrouching = enableCrouch && IsCrouchHeld();
            UpdateCrouchShape();

            Vector2 moveInput = GetMoveInput();
            Vector3 move = transform.right * moveInput.x + transform.forward * moveInput.y;
            float speed = isCrouching ? crouchSpeed : (IsSprintHeld() ? sprintSpeed : walkSpeed);
            SafeMove(move * speed * Time.deltaTime);

            if (WasJumpPressed() && grounded)
                yVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);

            yVelocity += gravity * Time.deltaTime;
            SafeMove(Vector3.up * yVelocity * Time.deltaTime);
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
                mouseX = lookInput.x * mouseSensitivity * Time.deltaTime;
                mouseY = lookInput.y * mouseSensitivity * Time.deltaTime;
            }

            xRotation -= mouseY;
            xRotation = Mathf.Clamp(xRotation, -90f, 90f);

            if (cameraTransform != null)
                cameraTransform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);

            transform.Rotate(Vector3.up * mouseX);
        }

        bool IsGrounded()
        {
            if (groundCheck == null) return controller.isGrounded;
            return Physics.CheckSphere(groundCheck.position, groundDistance, groundMask, QueryTriggerInteraction.Ignore);
        }

        void UpdateCrouchShape()
        {
            if (!enableCrouch) return;

            if (isCrouching)
            {
                controller.height = crouchHeight;
                controller.center = new Vector3(standCenter.x, crouchHeight * 0.5f, standCenter.z);
            }
            else
            {
                controller.height = standHeight;
                controller.center = standCenter;
            }
        }

        void UpdateCursorLock()
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.Escape))
                LockCursor(!isCursorLocked);
#endif
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                LockCursor(!isCursorLocked);
#endif
        }

        void LockCursor(bool locked)
        {
            isCursorLocked = locked;
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        Vector2 GetMoveInput()
        {
            float x = 0f;
            float y = 0f;

#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
                x = 0f;
                y = 0f;
                if (Keyboard.current.aKey.isPressed) x -= 1f;
                if (Keyboard.current.dKey.isPressed) x += 1f;
                if (Keyboard.current.sKey.isPressed) y -= 1f;
                if (Keyboard.current.wKey.isPressed) y += 1f;
                return new Vector2(x, y).normalized;
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            x = Input.GetAxisRaw("Horizontal");
            y = Input.GetAxisRaw("Vertical");
#endif
            return new Vector2(x, y).normalized;
        }

        Vector2 GetLookInput(out bool fromInputSystem)
        {
            fromInputSystem = false;
            float x = 0f;
            float y = 0f;

#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null)
            {
                Vector2 delta = Mouse.current.delta.ReadValue();
                fromInputSystem = true;
                return new Vector2(delta.x, delta.y);
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            x = Input.GetAxis("Mouse X");
            y = Input.GetAxis("Mouse Y");
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
            bool pressed = false;
#if ENABLE_LEGACY_INPUT_MANAGER
            pressed |= Input.GetKeyDown(KeyCode.Space);
#endif
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null) pressed |= Keyboard.current.spaceKey.wasPressedThisFrame;
#endif
            return pressed;
        }

        bool IsCrouchHeld()
        {
            bool held = false;
#if ENABLE_LEGACY_INPUT_MANAGER
            held |= Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.C);
#endif
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
                held |= Keyboard.current.leftCtrlKey.isPressed || Keyboard.current.cKey.isPressed;
#endif
            return held;
        }

        void OnDrawGizmosSelected()
        {
            if (groundCheck == null) return;
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(groundCheck.position, groundDistance);
        }
    }
}
