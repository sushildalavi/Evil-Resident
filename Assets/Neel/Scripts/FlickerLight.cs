using UnityEngine;

public class FlickerLight : MonoBehaviour
{
    public Light fuseLight;

    void Update()
    {
        fuseLight.intensity = Random.Range(0.8f, 2f);
    }
}