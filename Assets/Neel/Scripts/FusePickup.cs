using UnityEngine;

public class FusePickup : MonoBehaviour
{
    private bool playerNearby = false;

    void Update()
    {
        if (playerNearby && Input.GetKeyDown(KeyCode.E))
        {
            // ✅ allow multiple pickups
            PlayerInventory.instance.PickUpFuse();

            Debug.Log("Picked fuse. Total: " + PlayerInventory.instance.fuseCount);

            Destroy(gameObject);
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
            playerNearby = true;
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
            playerNearby = false;
    }
}