using UnityEngine;

public class HideableObject : MonoBehaviour, IInteractable
{
    [Header("Hide Points")]
    public Transform hidePoint;
    public Transform exitPoint;

    [Header("Camera Override (Optional)")]
    public Transform hiddenCameraPoint;

    [Header("Exterior-only visuals (optional)")]
    [Tooltip("Shown when the player is outside this hide spot; hidden while hiding (e.g. front latch). If empty, uses Visuals/Base/Latch when present.")]
    [SerializeField] Renderer[] exteriorOnlyRenderers;

    Renderer[] cachedExteriorRenderers;

    void Awake()
    {
        if (exteriorOnlyRenderers != null && exteriorOnlyRenderers.Length > 0)
        {
            cachedExteriorRenderers = exteriorOnlyRenderers;
            return;
        }

        Transform latch = transform.Find("Visuals/Base/Latch");
        if (latch == null)
        {
            cachedExteriorRenderers = System.Array.Empty<Renderer>();
            return;
        }

        Renderer self = latch.GetComponent<Renderer>();
        if (self != null)
            cachedExteriorRenderers = new[] { self };
        else
            cachedExteriorRenderers = latch.GetComponentsInChildren<Renderer>(true);
    }

    /// <summary>Exterior-only pieces stay visible from outside; disable rendering while the player is hidden inside.</summary>
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
