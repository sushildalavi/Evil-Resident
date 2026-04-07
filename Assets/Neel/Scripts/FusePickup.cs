using UnityEngine;

public class FusePickup : MonoBehaviour, IInteractable
{
    [Header("Fuse Identity")]
    public FuseId fuseId = FuseId.FuseA;

    private bool collected = false;

    public string GetPrompt(RohitFPSController player)
    {
        if (collected) return "";
        if (player == null) return "Inventory missing";

        PlayerInventory inventory = player.GetComponent<PlayerInventory>();
        if (inventory == null) return "Inventory missing";

        if (inventory.HasCarriedFuse)
            return $"Cannot pick up {PlayerInventory.FuseIdToLabel(fuseId)}: carrying {inventory.CarriedFuseName}";

        return $"Press E to pick up {PlayerInventory.FuseIdToLabel(fuseId)}";
    }

    public KeyCode GetInteractKey()
    {
        return KeyCode.E;
    }

    public void Interact(RohitFPSController player)
    {
        TryCollect(player);
    }

    void TryCollect(RohitFPSController player)
    {
        if (collected) return;
        if (player == null) return;

        PlayerInventory inventory = player.GetComponent<PlayerInventory>();
        if (inventory == null) return;

        if (!inventory.TryPickUpFuse(fuseId))
            return;

        collected = true;
        Destroy(gameObject);
    }
}
