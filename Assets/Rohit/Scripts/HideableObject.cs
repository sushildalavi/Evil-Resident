using UnityEngine;

public class HideableObject : MonoBehaviour, IInteractable
{
    [Header("Hide Points")]
    public Transform hidePoint;
    public Transform exitPoint;

    [Header("Camera Override (Optional)")]
    public Transform hiddenCameraPoint;

    Transform FindPoint(string relativePath)
    {
        return transform.Find(relativePath);
    }

    public Transform GetEffectiveHidePoint()
    {
        if (hidePoint != null)
            return hidePoint;

        Transform named = FindPoint("Points/HidePoint");
        if (named != null)
            return named;

        return transform;
    }

    public Transform GetEffectiveHiddenCameraPoint()
    {
        if (hiddenCameraPoint != null)
            return hiddenCameraPoint;

        Transform named = FindPoint("Points/HiddenCameraPoint");
        if (named != null)
            return named;

        // Fallback keeps camera inside container instead of player head height outside.
        return GetEffectiveHidePoint();
    }

    public Vector3 GetEffectiveExitPosition()
    {
        if (exitPoint != null)
            return exitPoint.position;

        Transform named = FindPoint("Points/ExitPoint");
        if (named != null)
            return named.position;

        return transform.position + transform.forward * 2f;
    }

    public KeyCode GetInteractKey() => KeyCode.F;

    public string GetPrompt(RohitFPSController player)
    {
        if (player.isHidden && player.currentHideObject == this)
            return "Press F to Exit Hiding Spot";
        if (!player.isHidden)
            return "Press F to Hide";
        return "";
    }

    public void Interact(RohitFPSController player)
    {
        if (!player.isHidden)
        {
            Transform targetHidePoint = GetEffectiveHidePoint();
            player.HideAt(targetHidePoint, this);
        }
        else if (player.currentHideObject == this)
        {
            Vector3 exitPos = GetEffectiveExitPosition();
            exitPos = player.ResolveSafeExitPosition(this, exitPos);
            player.ExitHide(exitPos);
        }
    }
}
