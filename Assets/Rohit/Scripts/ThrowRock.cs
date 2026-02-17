using UnityEngine;

public class ThrowRock : MonoBehaviour
{
    public GameObject rockPrefab;
    public Transform throwPoint;
    public float throwForce = 15f;

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.G))
        {
            Throw();
        }
    }

    void Throw()
    {
        GameObject rock = Instantiate(rockPrefab, throwPoint.position, throwPoint.rotation);

        Rigidbody rb = rock.GetComponent<Rigidbody>();

        rb.AddForce(throwPoint.forward * throwForce, ForceMode.Impulse);
    }
}
