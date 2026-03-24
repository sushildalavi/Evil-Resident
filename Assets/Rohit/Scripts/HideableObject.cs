using UnityEngine;

public class HideableObject : MonoBehaviour, IInteractable
{
    [Header("Hide Points")]
    public Transform hidePoint;
    public Transform exitPoint;

    [Header("Camera Override (Optional)")]
    public Transform hiddenCameraPoint;

    public KeyCode GetInteractKey() => KeyCode.E;

    public string GetPrompt(RohitFPSController player)
    {
        if (player.isHidden && player.currentHideObject == this)
            return "Press E to Exit Hiding Spot";
        if (!player.isHidden)
            return "Press E to Hide";
        return "";
    }

    public void Interact(RohitFPSController player)
    {
        if (!player.isHidden)
        {
            Transform targetHidePoint = hidePoint != null ? hidePoint : transform;
            player.HideAt(targetHidePoint, this);
        }
        else if (player.currentHideObject == this)
        {
            Vector3 exitPos;
            if (exitPoint != null)
                exitPos = exitPoint.position;
            else
                exitPos = transform.position + transform.forward * 2f;

            exitPos = player.ResolveSafeExitPosition(this, exitPos);
            player.ExitHide(exitPos);
        }
    }
}
