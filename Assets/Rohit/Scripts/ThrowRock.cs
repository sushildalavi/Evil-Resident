using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class ThrowRock : MonoBehaviour
{
    [Header("Feature Toggle")]
    public bool enableThrowFeature = false;

    public GameObject rockPrefab;
    public Transform throwPoint;
    public float throwForce = 15f;

    void Update()
    {
        if (!enableThrowFeature) return;

        if (WasThrowPressed())
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

        rb.AddForce(throwPoint.forward * throwForce, ForceMode.Impulse);
    }

    bool WasThrowPressed()
    {
        bool pressed = false;
#if ENABLE_LEGACY_INPUT_MANAGER
        pressed |= Input.GetKeyDown(KeyCode.G);
#endif
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null) pressed |= Keyboard.current.gKey.wasPressedThisFrame;
#endif
        return pressed;
    }
}
