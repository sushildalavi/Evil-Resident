using UnityEngine;

public class FusePickup : MonoBehaviour, IInteractable
{
    [Header("Fuse Identity")]
    public FuseId fuseId = FuseId.FuseA;

    private bool collected = false;

    public string GetPrompt(RohitFPSController player)
    {
        if (collected) return "";
        if (PlayerInventory.instance == null) return "Inventory missing";
        if (PlayerInventory.instance.HasCarriedFuse)
            return $"Cannot pick up {PlayerInventory.FuseIdToLabel(fuseId)}: carrying {PlayerInventory.instance.CarriedFuseName}";
        return $"Press E to pick up {PlayerInventory.FuseIdToLabel(fuseId)}";
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

        if (!PlayerInventory.instance.TryPickUpFuse(fuseId))
            return;

        collected = true;
        Destroy(gameObject);
    }
}
