using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Sushil.Demo
{
    public class PlayerThrow : MonoBehaviour
    {
        public GameObject throwablePrefab;
        public Transform throwOrigin;
        public Transform fallbackCamera;
        public float throwForce = 10f;
        public float throwUpward = 2f;
        public float cooldown = 0.6f;

        [Header("Thrown Noise")]
        public float impactNoise = 18f;
        public float minImpactSpeed = 0.75f;
        public float projectileLifeSeconds = 6f;
        public string noiseType = "throwImpact";

        float lastThrowTime;

        void Update()
        {
            if (Time.time - lastThrowTime < cooldown) return;

            if (WasThrowPressed())
            {
                Throw();
                lastThrowTime = Time.time;
            }
        }

        bool WasThrowPressed()
        {
            bool pressed = false;

#if ENABLE_LEGACY_INPUT_MANAGER
            pressed |= Input.GetKeyDown(KeyCode.G);
#endif

#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
                pressed |= Keyboard.current.gKey.wasPressedThisFrame;
#endif

            return pressed;
        }

        void Throw()
        {
            Transform origin = ResolveThrowOrigin();
            if (origin == null) return;

            GameObject obj = throwablePrefab != null
                ? Instantiate(throwablePrefab, origin.position, Quaternion.identity)
                : CreateFallbackThrowable(origin.position);

            var rb = obj.GetComponent<Rigidbody>();
            if (rb == null) return;

            Vector3 dir = origin.forward * throwForce + Vector3.up * throwUpward;
            rb.AddForce(dir, ForceMode.Impulse);

            ThrowableNoise.ConfigureOnObject(obj, impactNoise, minImpactSpeed, projectileLifeSeconds, noiseType);
        }

        GameObject CreateFallbackThrowable(Vector3 pos)
        {
            var obj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            obj.name = "RuntimeThrowable";
            obj.transform.position = pos;
            obj.transform.localScale = Vector3.one * 0.25f;
            var rb = obj.AddComponent<Rigidbody>();
            rb.mass = 1f;
            Debug.LogWarning("[PlayerThrow] throwablePrefab is missing. Using runtime fallback sphere.");
            return obj;
        }

        Transform ResolveThrowOrigin()
        {
            if (throwOrigin != null) return throwOrigin;
            if (fallbackCamera != null) return fallbackCamera;
            if (Camera.main != null) return Camera.main.transform;
            return transform;
        }
    }
}
