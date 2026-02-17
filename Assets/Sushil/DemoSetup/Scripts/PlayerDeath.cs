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
            if (GetComponent<PlayerHide>() == null)
                gameObject.AddComponent<PlayerHide>();
        }

        public void Kill(string reason = "Killed")
        {
            if (isDead) return;
            isDead = true;

            Debug.Log($"[PlayerDeath] {reason}");
            gameObject.SetActive(false); // Alpha: disable player
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
            bool pressed = Input.GetKeyDown(KeyCode.H);

#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
                pressed |= Keyboard.current.hKey.wasPressedThisFrame;
#endif

            return pressed;
        }

        void SetHidden(bool hidden)
        {
            IsHidden = hidden;

            foreach (var r in cachedRenderers)
                r.enabled = !hidden;

            foreach (var c in cachedColliders)
                c.enabled = !hidden;

            Debug.Log(hidden ? "[PlayerHide] Hidden" : "[PlayerHide] Visible");
        }
    }
}
