using UnityEngine;

public class FuseItem : MonoBehaviour, IInteractable
{
    public string fuseName = "Fuse";

    public string GetPrompt(RohitFPSController player)
    {
        return "Press E to pick up " + fuseName;
    }

    public KeyCode GetInteractKey()
    {
        return KeyCode.E;
    }

    public void Interact(RohitFPSController player)
    {
        PlayerInventory.instance.PickUpFuse();
        Destroy(gameObject);
    }
}