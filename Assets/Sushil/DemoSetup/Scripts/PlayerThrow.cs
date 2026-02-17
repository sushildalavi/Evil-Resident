using UnityEngine;

namespace Sushil.Demo
{
    public class PlayerThrow : MonoBehaviour
    {
        public GameObject throwablePrefab;
        public Transform throwOrigin;
        public float throwForce = 10f;
        public float throwUpward = 2f;
        public float cooldown = 0.6f;

        float lastThrowTime;

        void Update()
        {
            if (Time.time - lastThrowTime < cooldown) return;

            if (Input.GetKeyDown(KeyCode.G))
            {
                Throw();
                lastThrowTime = Time.time;
            }
        }

        void Throw()
        {
            if (throwablePrefab == null || throwOrigin == null) return;

            GameObject obj = Instantiate(throwablePrefab, throwOrigin.position, Quaternion.identity);
            var rb = obj.GetComponent<Rigidbody>();
            if (rb == null) return;

            Vector3 dir = throwOrigin.forward * throwForce + Vector3.up * throwUpward;
            rb.AddForce(dir, ForceMode.VelocityChange);
        }
    }
}