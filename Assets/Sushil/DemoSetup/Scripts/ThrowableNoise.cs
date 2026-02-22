using UnityEngine;
using Sushil.Systems;

namespace Sushil.Demo
{
    [RequireComponent(typeof(Rigidbody))]
    public class ThrowableNoise : MonoBehaviour
    {
        [Header("Noise")]
        public float impactNoise = 10f;
        public float minImpactSpeed = 2f;
        public string noiseType = "throwImpact";

        [Header("Lifetime")]
        public float lifeSeconds = 6f;

        bool hasMadeNoise;
        bool validProjectile;

        void Awake()
        {
            // Safety: this script should only live on thrown objects, never on the player root.
            if (GetComponent<CharacterController>() != null || GetComponent<Sushil.Systems.PlayerDeath>() != null)
            {
                validProjectile = false;
                enabled = false;
                Debug.LogWarning("[ThrowableNoise] Disabled on player object to prevent destroying/disrupting player.");
                return;
            }

            validProjectile = true;
        }

        void Start()
        {
            if (!validProjectile) return;
            if (lifeSeconds > 0f) Destroy(gameObject, lifeSeconds);
        }

        void OnCollisionEnter(Collision col)
        {
            if (!validProjectile) return;
            if (hasMadeNoise) return;
            if (col.relativeVelocity.magnitude < minImpactSpeed) return;

            hasMadeNoise = true;
            NoiseSystem.Emit(transform.position, impactNoise, noiseType);
        }

        public static ThrowableNoise ConfigureOnObject(
            GameObject target,
            float impactNoise,
            float minImpactSpeed,
            float lifeSeconds,
            string noiseType)
        {
            var emitter = target.GetComponent<ThrowableNoise>();
            if (emitter == null) emitter = target.AddComponent<ThrowableNoise>();

            emitter.impactNoise = impactNoise;
            emitter.minImpactSpeed = minImpactSpeed;
            emitter.lifeSeconds = lifeSeconds;
            emitter.noiseType = string.IsNullOrEmpty(noiseType) ? "throwImpact" : noiseType;
            return emitter;
        }
    }
}
