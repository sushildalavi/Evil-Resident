using UnityEngine;

namespace Sushil.Demo
{
    public class SushilFPSController : MonoBehaviour
    {
    }

    public class NoiseTester : MonoBehaviour
    {
    }

    public class PlayerThrow : MonoBehaviour
    {
    }

    public class ThrowableNoise : MonoBehaviour
    {
        public float impactNoiseIntensity = 18f;
        public float minImpactSpeed = 0.5f;
        public float lifeSeconds = 8f;
        public string noiseType = "throwImpact";

        public static ThrowableNoise ConfigureOnObject(
            GameObject target,
            float impactIntensity,
            float minimumImpactSpeed,
            float projectileLifeSeconds,
            string impactNoiseType)
        {
            if (target == null) return null;

            var noise = target.GetComponent<ThrowableNoise>();
            if (noise == null)
                noise = target.AddComponent<ThrowableNoise>();

            noise.impactNoiseIntensity = impactIntensity;
            noise.minImpactSpeed = minimumImpactSpeed;
            noise.lifeSeconds = projectileLifeSeconds;
            noise.noiseType = string.IsNullOrWhiteSpace(impactNoiseType) ? "throwImpact" : impactNoiseType;
            return noise;
        }
    }
}
