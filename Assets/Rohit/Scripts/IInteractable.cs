using UnityEngine;

public interface IInteractable
{
    string GetPrompt(RohitFPSController player);
    void Interact(RohitFPSController player);
    KeyCode GetInteractKey();
}
