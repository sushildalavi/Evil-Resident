using UnityEngine;
using Sushil.Systems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Sushil.Demo
{
    public class NoiseTester : MonoBehaviour
    {
        public float intensity = 8f;

        void Update()
        {
            if (WasNoisePressed())
            {
                NoiseSystem.Emit(transform.position, intensity, "debug_noise");
                Debug.Log("[NoiseTester] Noise emitted");
            }
        }

        bool WasNoisePressed()
        {
            bool pressed = Input.GetKeyDown(KeyCode.N) || Input.GetMouseButtonDown(0);

#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
                pressed |= Keyboard.current.nKey.wasPressedThisFrame;

            if (Mouse.current != null)
                pressed |= Mouse.current.leftButton.wasPressedThisFrame;
#endif

            return pressed;
        }
    }
}
