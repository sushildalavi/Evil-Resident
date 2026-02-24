using UnityEngine;
using Sushil.Demo;

public class ThrowRock : MonoBehaviour
{
    public GameObject rockPrefab;
    public Transform throwPoint;
    public float throwForce = 15f;

    [Header("Noise Integration")]
    public bool configureImpactNoise = true;
    public float impactNoiseIntensity = 18f;
    public float impactMinSpeed = 0.5f;
    public float projectileLifeSeconds = 8f;
    public string impactNoiseType = "throwImpact";

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.G))
        {
            Throw();
        }
    }

    void Throw()
    {
        if (rockPrefab == null || throwPoint == null) return;

        GameObject rock = Instantiate(rockPrefab, throwPoint.position, throwPoint.rotation);

        Rigidbody rb = rock.GetComponent<Rigidbody>();
        if (rb == null) return;

        // Always configure thrown rock impact noise so stalker distraction stays reliable,
        // even if prefab instances have stale inspector values.
        ThrowableNoise.ConfigureOnObject(
            rock,
            impactNoiseIntensity,
            impactMinSpeed,
            projectileLifeSeconds,
            impactNoiseType);

        rb.AddForce(throwPoint.forward * throwForce, ForceMode.Impulse);
    }
}
