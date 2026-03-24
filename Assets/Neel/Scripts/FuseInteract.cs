using UnityEngine;

public class FuseInteract : MonoBehaviour
{
    public string fuseName = "Fuse";

    public string GetPrompt()
    {
        return "Press E to pick up " + fuseName;
    }

    public void Interact()
    {
        PlayerInventory.instance.PickUpFuse();
        Destroy(gameObject);
    }
}