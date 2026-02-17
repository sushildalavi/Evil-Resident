using UnityEngine;
using Sushil.Systems;

namespace Sushil.Demo
{
    [RequireComponent(typeof(Rigidbody))]
    public class ThrowableNoise : MonoBehaviour
    {
        public float impactNoise = 10f;
        public float minImpactSpeed = 2f;
        public float lifeSeconds = 6f;

        bool hasMadeNoise;

        void Start()
        {
            Destroy(gameObject, lifeSeconds);
        }

        void OnCollisionEnter(Collision col)
        {
            if (hasMadeNoise) return;

            float speed = col.relativeVelocity.magnitude;
            if (speed < minImpactSpeed) return;

            hasMadeNoise = true;

            // big noise at impact point
            NoiseSystem.EmitNoise(transform.position, impactNoise, "throwImpact");
        }
    }
}