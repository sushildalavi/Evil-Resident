using System.Collections.Generic;
using UnityEngine;

public class HideableObject : MonoBehaviour, IInteractable
{
    [Header("Hide Points")]
    public Transform hidePoint;
    public Transform exitPoint;

    [Header("Camera Override (Optional)")]
    public Transform hiddenCameraPoint;

    [Header("Exterior-only visuals (optional)")]
    [Tooltip("Shown when outside this hide spot; hidden while hiding. If empty, auto-finds latch and/or cupboard doors/handles.")]
    [SerializeField] Renderer[] exteriorOnlyRenderers;

    Renderer[] cachedExteriorRenderers;

    static readonly string[] AutoExteriorRendererPaths =
    {
        "Visuals/Base/Latch",
        "LeftDoorPivot/LeftDoor",
        "RightDoorPivot/RightDoor",
        "RightDoorPivot/RightDoor (1)",
        "LeftHandle",
        "RightHandle",
    };

    void Awake()
    {
        if (exteriorOnlyRenderers != null && exteriorOnlyRenderers.Length > 0)
        {
            cachedExteriorRenderers = exteriorOnlyRenderers;
            return;
        }

        var list = new List<Renderer>();
        for (int i = 0; i < AutoExteriorRendererPaths.Length; i++)
        {
            Transform t = transform.Find(AutoExteriorRendererPaths[i]);
            if (t == null) continue;

            Renderer self = t.GetComponent<Renderer>();
            if (self != null)
                list.Add(self);
            else
                list.AddRange(t.GetComponentsInChildren<Renderer>(true));
        }

        cachedExteriorRenderers = list.Count > 0 ? list.ToArray() : System.Array.Empty<Renderer>();
    }

    /// <summary>Disable exterior-only renderers while the player is hidden inside (colliders stay on).</summary>
    public void SetExteriorOnlyRenderersVisible(bool visible)
    {
        if (cachedExteriorRenderers == null) return;
        for (int i = 0; i < cachedExteriorRenderers.Length; i++)
        {
            if (cachedExteriorRenderers[i] != null)
                cachedExteriorRenderers[i].enabled = visible;
        }
    }

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
