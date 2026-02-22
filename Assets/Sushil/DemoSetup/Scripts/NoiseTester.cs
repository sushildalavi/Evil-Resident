using UnityEngine;
using Sushil.Systems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Sushil.Demo
{
    public class NoiseTester : MonoBehaviour
    {
        [Header("Debug Noise")]
        public KeyCode noiseKey = KeyCode.N;
        public float intensity = 20f;
        public bool emitFromThrowPoint = true;

        PlayerThrow playerThrow;

        void Awake()
        {
            playerThrow = GetComponent<PlayerThrow>();
        }

        void Update()
        {
            if (WasNoisePressed())
            {
                Vector3 pos = transform.position;
                if (emitFromThrowPoint && playerThrow != null)
                {
                    Transform origin = playerThrow.throwOrigin != null ? playerThrow.throwOrigin :
                                       (playerThrow.fallbackCamera != null ? playerThrow.fallbackCamera : null);
                    if (origin == null && Camera.main != null) origin = Camera.main.transform;
                    if (origin != null) pos = origin.position;
                }

                NoiseSystem.Emit(pos, intensity, "debug_noise");
                Debug.Log("[NoiseTester] Noise emitted");
            }
        }

        bool WasNoisePressed()
        {
            bool pressed = false;

#if ENABLE_LEGACY_INPUT_MANAGER
            pressed |= Input.GetKeyDown(noiseKey);
#endif

#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
                pressed |= Keyboard.current.nKey.wasPressedThisFrame;
#endif

            return pressed;
        }
    }
}
