using UnityEngine;

public class FuseItem : MonoBehaviour, IInteractable
{
    public FuseId fuseId = FuseId.FuseA;

    public string GetPrompt(RohitFPSController player)
    {
        if (PlayerInventory.instance == null) return "Inventory missing";
        if (PlayerInventory.instance.HasCarriedFuse)
            return $"Cannot pick up {PlayerInventory.FuseIdToLabel(fuseId)}: carrying {PlayerInventory.instance.CarriedFuseName}";
        return "Press E to pick up " + PlayerInventory.FuseIdToLabel(fuseId);
    }

    public KeyCode GetInteractKey()
    {
        return KeyCode.E;
    }

    public void Interact(RohitFPSController player)
    {
        if (PlayerInventory.instance == null) return;
        if (!PlayerInventory.instance.TryPickUpFuse(fuseId)) return;
        Destroy(gameObject);
    }
}
