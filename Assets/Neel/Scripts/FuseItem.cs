using UnityEngine;

public class FuseItem : MonoBehaviour, IInteractable
{
    public FuseId fuseId = FuseId.FuseA;

    public string GetPrompt(RohitFPSController player)
    {
        if (player == null) return "Inventory missing";

        PlayerInventory inventory = player.GetComponent<PlayerInventory>();
        if (inventory == null) return "Inventory missing";

        if (inventory.HasCarriedFuse)
            return $"Cannot pick up {PlayerInventory.FuseIdToLabel(fuseId)}: carrying {inventory.CarriedFuseName}";

        return "Press E to pick up " + PlayerInventory.FuseIdToLabel(fuseId);
    }

    public KeyCode GetInteractKey()
    {
        return KeyCode.E;
    }

    public void Interact(RohitFPSController player)
    {
        if (player == null) return;

        PlayerInventory inventory = player.GetComponent<PlayerInventory>();
        if (inventory == null) return;
        if (!inventory.TryPickUpFuse(fuseId)) return;

        Destroy(gameObject);
    }
}
