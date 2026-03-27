using UnityEngine;

public class FusePickup : MonoBehaviour, IInteractable
{
    private bool playerNearby = false;
    private bool collected = false;

    void Update()
    {
        // Legacy trigger fallback for scenes that still rely on trigger-only behavior.
        if (playerNearby && Input.GetKeyDown(KeyCode.E))
            TryCollect();
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

    public string GetPrompt(RohitFPSController player)
    {
        if (collected) return "";
        if (PlayerInventory.instance == null) return "Inventory missing";
        return "Press E to pick up fuse";
    }

    public KeyCode GetInteractKey()
    {
        return KeyCode.E;
    }

    public void Interact(RohitFPSController player)
    {
        TryCollect();
    }

    void TryCollect()
    {
        if (collected) return;
        if (PlayerInventory.instance == null) return;

        collected = true;
        PlayerInventory.instance.PickUpFuse();
        Debug.Log("Picked fuse. Total: " + PlayerInventory.instance.fuseCount);
        Destroy(gameObject);
    }
}