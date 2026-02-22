using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Sushil.Systems
{
    public class PlayerDeath : MonoBehaviour
    {
        public bool isDead { get; private set; }

        void Awake()
        {
            EnsurePlayerSetup();
            if (GetComponent<PlayerHide>() == null) gameObject.AddComponent<PlayerHide>();
        }

        public void Kill(string reason = "Killed")
        {
            if (isDead) return;
            isDead = true;
            Debug.Log($"[PlayerDeath] {reason}");

            var fps = GetComponent<Sushil.Demo.SushilFPSController>();
            if (fps != null) fps.enabled = false;

            var thrower = GetComponent<Sushil.Demo.PlayerThrow>();
            if (thrower != null) thrower.enabled = false;

            var tester = GetComponent<Sushil.Demo.NoiseTester>();
            if (tester != null) tester.enabled = false;

            var hide = GetComponent<PlayerHide>();
            if (hide != null) hide.enabled = false;

            var cc = GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
        }

        void EnsurePlayerSetup()
        {
            if (!CompareTag("Player")) gameObject.tag = "Player";

            var strayThrowableNoise = GetComponent<Sushil.Demo.ThrowableNoise>();
            if (strayThrowableNoise != null)
            {
                Destroy(strayThrowableNoise);
                Debug.LogWarning("[PlayerDeath] Removed ThrowableNoise from player root. It should only be on thrown objects.");
            }

            if (GetComponent<CharacterController>() == null)
            {
                var cc = gameObject.AddComponent<CharacterController>();
                cc.height = 2f;
                cc.radius = 0.5f;
                cc.center = new Vector3(0f, 1f, 0f);
            }

            var rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = true;
                rb.useGravity = false;
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            var rohitFps = GetComponent<RohitFPSController>();
            if (rohitFps != null) rohitFps.enabled = false;

            var throwRock = GetComponent<ThrowRock>();
            if (throwRock != null) throwRock.enabled = false;

            var cam = GetComponentInChildren<Camera>(true);
            if (cam == null)
            {
                var camGo = new GameObject("PlayerCamera");
                camGo.transform.SetParent(transform, false);
                camGo.transform.localPosition = new Vector3(0f, 1.6f, 0f);
                camGo.transform.localRotation = Quaternion.identity;
                cam = camGo.AddComponent<Camera>();
                camGo.AddComponent<AudioListener>();
                camGo.tag = "MainCamera";
            }

            var fps = GetComponent<Sushil.Demo.SushilFPSController>();
            if (fps == null) fps = gameObject.AddComponent<Sushil.Demo.SushilFPSController>();
            if (fps.cameraTransform == null && cam != null) fps.cameraTransform = cam.transform;
            if (fps.groundCheck == null)
            {
                var groundCheck = transform.Find("GroundCheck");
                if (groundCheck != null) fps.groundCheck = groundCheck;
            }

            var thrower = GetComponent<Sushil.Demo.PlayerThrow>();
            if (thrower == null) thrower = gameObject.AddComponent<Sushil.Demo.PlayerThrow>();
            if (thrower.throwOrigin == null)
            {
                var throwOrigin = transform.Find("ThrowOrigin");
                if (throwOrigin == null) throwOrigin = transform.Find("ThrowPoint");
                if (throwOrigin != null) thrower.throwOrigin = throwOrigin;
            }
            if (thrower.fallbackCamera == null && cam != null) thrower.fallbackCamera = cam.transform;

            if (GetComponent<Sushil.Demo.NoiseTester>() == null)
                gameObject.AddComponent<Sushil.Demo.NoiseTester>();
        }
    }

    public class PlayerHide : MonoBehaviour
    {
        public bool IsHidden { get; private set; }

        Renderer[] cachedRenderers;
        Collider[] cachedColliders;

        void Awake()
        {
            cachedRenderers = GetComponentsInChildren<Renderer>(true);
            cachedColliders = GetComponentsInChildren<Collider>(true);
        }

        void Update()
        {
            if (!WasHidePressed()) return;
            SetHidden(!IsHidden);
        }

        bool WasHidePressed()
        {
            bool pressed = false;
#if ENABLE_LEGACY_INPUT_MANAGER
            pressed |= Input.GetKeyDown(KeyCode.H);
#endif
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null) pressed |= Keyboard.current.hKey.wasPressedThisFrame;
#endif
            return pressed;
        }

        void SetHidden(bool hidden)
        {
            IsHidden = hidden;

            foreach (var r in cachedRenderers)
                r.enabled = !hidden;

            foreach (var c in cachedColliders)
            {
                if (c is CharacterController) continue;
                c.enabled = !hidden;
            }

            Debug.Log(hidden ? "[PlayerHide] Hidden" : "[PlayerHide] Visible");
        }
    }
}
