using UnityEngine;
using Sushil.Systems;

namespace Sushil.Demo
{
    public class NoiseTester : MonoBehaviour
    {
        public float intensity = 8f;

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.N))
            {
                NoiseSystem.Emit(transform.position, intensity, "debug_noise");
                Debug.Log("[NoiseTester] Noise emitted");
            }
        }
    }
}