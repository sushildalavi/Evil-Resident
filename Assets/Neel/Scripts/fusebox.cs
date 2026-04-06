using UnityEngine;

public class FuseBox : MonoBehaviour, IInteractable
{
    [Header("Legacy Visual Slots (kept for prefab compatibility)")]
    public Transform[] slots;
    public Renderer[] slotRenderers;
    public Light[] slotLights;

    [Header("Color Settings")]
    public bool preferFusePrefabColor = true;
    public Color[] fallbackSlotColors = new Color[] { Color.yellow, Color.blue, new Color(0.6f, 0.2f, 1f, 1f) };

    [Header("Legacy Optional Visuals")]
    public GameObject[] fusePrefabs;
    public GameObject door;
    public GameObject sparkEffect;

    [Header("Fuse Identity")]
    public FuseId requiredFuseId = FuseId.FuseA;

    [Header("State")]
    [SerializeField] private bool isPowered;

    public bool IsPowered => isPowered;
    public bool IsFull => isPowered;

    public string GetPrompt(RohitFPSController player)
    {
        if (PlayerInventory.instance == null)
            return "Inventory missing";

        if (isPowered)
            return $"{name} already powered";

        if (!PlayerInventory.instance.HasCarriedFuse)
            return $"Need {PlayerInventory.FuseIdToLabel(requiredFuseId)}";

        if (PlayerInventory.instance.CarriedFuseId != requiredFuseId)
            return $"Wrong fuse: need {PlayerInventory.FuseIdToLabel(requiredFuseId)}";

        return $"Press E to insert {PlayerInventory.FuseIdToLabel(requiredFuseId)}";
    }

    public KeyCode GetInteractKey()
    {
        return KeyCode.E;
    }

    public void Interact(RohitFPSController player)
    {
        if (PlayerInventory.instance == null)
        {
            Debug.LogError("PlayerInventory.instance is null");
            return;
        }

        if (isPowered)
        {
            Debug.Log($"{name} is already powered");
            return;
        }

        if (!PlayerInventory.instance.HasCarriedFuse)
        {
            Debug.Log($"Cannot insert into {name}: no fuse carried");
            return;
        }

        FuseId carriedFuse = PlayerInventory.instance.CarriedFuseId.Value;
        if (carriedFuse != requiredFuseId)
        {
            Debug.Log($"Cannot insert {PlayerInventory.FuseIdToLabel(carriedFuse)} into {name}: requires {PlayerInventory.FuseIdToLabel(requiredFuseId)}");
            return;
        }

        if (!PlayerInventory.instance.TryUseFuse(requiredFuseId))
        {
            Debug.Log($"Failed to insert into {name}");
            return;
        }

        isPowered = true;
        ApplyPoweredVisuals();
        Debug.Log($"Inserted {PlayerInventory.FuseIdToLabel(requiredFuseId)} into {name}");
    }

    private void ApplyPoweredVisuals()
    {
        Color poweredColor = ResolveColorForRequiredFuse();

        if (slotRenderers != null)
        {
            for (int i = 0; i < slotRenderers.Length; i++)
            {
                Renderer r = slotRenderers[i];
                if (r == null) continue;
                SetRendererColor(r, poweredColor);
            }
        }

        if (slotLights != null)
        {
            for (int i = 0; i < slotLights.Length; i++)
            {
                Light l = slotLights[i];
                if (l == null) continue;
                l.color = poweredColor;
                l.intensity = Mathf.Max(l.intensity, 2f);
                l.enabled = true;
            }
        }

        if (sparkEffect != null)
        {
            Vector3 spawnPosition = transform.position;
            if (slots != null && slots.Length > 0 && slots[0] != null)
                spawnPosition = slots[0].position;
            Instantiate(sparkEffect, spawnPosition, Quaternion.identity);
        }
    }

    private Color ResolveColorForRequiredFuse()
    {
        if (fallbackSlotColors != null && fallbackSlotColors.Length > 0)
        {
            int idx = Mathf.Clamp((int)requiredFuseId, 0, fallbackSlotColors.Length - 1);
            return fallbackSlotColors[idx];
        }

        return Color.green;
    }

    private void SetRendererColor(Renderer renderer, Color color)
    {
        if (renderer == null) return;

        Material[] mats = renderer.materials;
        for (int i = 0; i < mats.Length; i++)
        {
            Material mat = mats[i];
            if (mat == null) continue;
            mat.EnableKeyword("_EMISSION");
            if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", color * 2f);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }
    }
}
