using UnityEngine;

public class FuseBox : MonoBehaviour, IInteractable
{
    public Transform[] slots;
    public Renderer[] slotRenderers;
    public Light[] slotLights;
    [Header("Color Settings")]
    public bool preferFusePrefabColor = true;
    public Color[] fallbackSlotColors = new Color[] { Color.yellow, Color.blue, new Color(0.6f, 0.2f, 1f, 1f) };

    public GameObject[] fusePrefabs;
    public GameObject door;
    public GameObject sparkEffect;

    private int currentFuses = 0;
    public bool IsFull => slots != null && slots.Length > 0 && currentFuses >= slots.Length;

    // 🔹 UI PROMPT
    public string GetPrompt(RohitFPSController player)
    {
        if (slots == null || slots.Length == 0)
            return "Fuse box not configured";

        if (PlayerInventory.instance == null)
            return "Inventory missing";

        if (currentFuses >= slots.Length)
            return "Fuse box is full";

        if (!PlayerInventory.instance.HasFuse())
            return "No fuse to insert";

        return "Press E to insert fuse";
    }

    // 🔹 INPUT KEY
    public KeyCode GetInteractKey()
    {
        return KeyCode.E;
    }

    // 🔹 INTERACTION
    public void Interact(RohitFPSController player)
    {
        Debug.Log("FuseBox interacted");

        if (PlayerInventory.instance == null)
        {
            Debug.LogError("PlayerInventory.instance is null");
            return;
        }

        if (slots == null || slots.Length == 0)
        {
            Debug.LogError("FuseBox has no slots configured");
            return;
        }

        if (!PlayerInventory.instance.HasFuse())
        {
            Debug.Log("No fuse in inventory");
            return;
        }

        InsertAllFuses();
    }

    // 🔹 INSERT ALL AVAILABLE (safe multi-insert with progress guard)
    void InsertAllFuses()
    {
        if (currentFuses >= slots.Length)
        {
            Debug.Log("FuseBox already full.");
            return;
        }

        int insertedCount = 0;
        int guard = 0;
        int maxAttempts = Mathf.Max(1, slots.Length + 1);

        while (PlayerInventory.instance.HasFuse() && currentFuses < slots.Length && guard < maxAttempts)
        {
            guard++;
            bool inserted = InsertSingleFuse();
            if (!inserted)
                break;
            insertedCount++;
        }

        if (insertedCount > 0)
        {
            Debug.Log("Inserted fuses: " + insertedCount + ", now filled: " + currentFuses + "/" + slots.Length);
        }
        else
        {
            Debug.LogWarning("Fuse insertion failed. Check FuseBox setup in inspector.");
        }
    }

    // 🔹 INSERT ONE (SAFE VERSION)
    bool InsertSingleFuse()
    {
        Debug.Log("Inserting fuse at slot: " + currentFuses);

        // 🚨 SAFETY CHECKS (PREVENT CRASH)
        if (slots == null)
        {
            Debug.LogError("FuseBox slots array not assigned!");
            return false;
        }

        if (currentFuses >= slots.Length)
        {
            Debug.LogError("Slot index out of range");
            return false;
        }

        if (slots[currentFuses] == null)
        {
            Debug.LogError("Slot transform missing at index " + currentFuses);
            return false;
        }

        // 🎯 SPAWN FUSE (optional setup: if prefab is missing, still insert and color slot)
        Material sourceFuseMaterial = null;
        if (fusePrefabs != null && currentFuses < fusePrefabs.Length && fusePrefabs[currentFuses] != null)
        {
            GameObject fuse = Instantiate(fusePrefabs[currentFuses]);
            StartCoroutine(MoveFuse(fuse, slots[currentFuses]));
            Renderer fuseRenderer = fuse.GetComponentInChildren<Renderer>();
            if (fuseRenderer != null)
                sourceFuseMaterial = fuseRenderer.material;
            else
                Debug.LogWarning("Fuse prefab has no renderer at index " + currentFuses + ". Using fallback slot color.");
        }
        else
        {
            Debug.LogWarning("Fuse prefab missing at index " + currentFuses + ". Inserting without spawned visual.");
        }

        Color fuseColor = ResolveFuseColor(sourceFuseMaterial, currentFuses);

        Debug.Log("Fuse Color: " + fuseColor);

        // 🔥 APPLY COLOR TO SLOT
        Renderer[] targetSlotRenderers = GetSlotRenderers(currentFuses);
        if (targetSlotRenderers == null || targetSlotRenderers.Length == 0)
        {
            Debug.LogError("Slot renderer(s) missing at index " + currentFuses);
            return false;
        }

        for (int i = 0; i < targetSlotRenderers.Length; i++)
        {
            Renderer r = targetSlotRenderers[i];
            if (r == null) continue;
            ApplyColorToRendererMaterials(r, fuseColor);
            ApplyColorToRenderer(r, fuseColor);
        }

        // REMOVE FROM PLAYER only after successful slot update.
        PlayerInventory.instance.UseFuse();

        // 💡 LIGHT
        if (slotLights != null && currentFuses < slotLights.Length && slotLights[currentFuses] != null)
        {
            slotLights[currentFuses].color = fuseColor;
            slotLights[currentFuses].intensity = 5f;
        }

        // ⚡ SPARK
        if (sparkEffect != null)
        {
            Instantiate(sparkEffect, slots[currentFuses].position, Quaternion.identity);
        }

        currentFuses++;

        // 🚪 OPEN DOOR
        if (currentFuses == slots.Length)
        {
            ActivatePower();
        }

        return true;
    }

    Renderer[] GetSlotRenderers(int index)
    {
        if (slotRenderers != null && index < slotRenderers.Length && slotRenderers[index] != null)
            return new Renderer[] { slotRenderers[index] };

        if (slots != null && index < slots.Length && slots[index] != null)
            return slots[index].GetComponentsInChildren<Renderer>(true);

        return System.Array.Empty<Renderer>();
    }

    Color ResolveFuseColor(Material sourceMaterial, int slotIndex)
    {
        if (preferFusePrefabColor && sourceMaterial != null)
        {
            if (sourceMaterial.HasProperty("_BaseColor"))
                return sourceMaterial.GetColor("_BaseColor");
            if (sourceMaterial.HasProperty("_Color"))
                return sourceMaterial.color;
        }

        if (fallbackSlotColors != null && fallbackSlotColors.Length > 0)
            return fallbackSlotColors[Mathf.Clamp(slotIndex, 0, fallbackSlotColors.Length - 1)];

        return Color.green;
    }

    void ApplyColorToRendererMaterials(Renderer renderer, Color color)
    {
        if (renderer == null) return;

        Material[] mats = renderer.materials;
        for (int i = 0; i < mats.Length; i++)
        {
            Material mat = mats[i];
            if (mat == null) continue;
            mat.EnableKeyword("_EMISSION");
            if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", color * 5f);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }
    }

    void ApplyColorToRenderer(Renderer renderer, Color color)
    {
        if (renderer == null) return;

        var block = new MaterialPropertyBlock();
        renderer.GetPropertyBlock(block);
        block.SetColor("_EmissionColor", color * 5f);
        block.SetColor("_BaseColor", color);
        block.SetColor("_Color", color);
        renderer.SetPropertyBlock(block);
    }

    // 🔹 FINAL POWER ON
    void ActivatePower()
    {
        Debug.Log("All fuses inserted!");

        if (door != null)
        {
            Door d = door.GetComponent<Door>();
            if (d != null)
                d.OpenDoor();
        }
    }

    // 🔹 FUSE ANIMATION
    System.Collections.IEnumerator MoveFuse(GameObject fuse, Transform target)
    {
        float t = 0;
        Vector3 start = target.position + Vector3.up * 2f;

        fuse.transform.position = start;

        while (t < 1)
        {
            t += Time.deltaTime * 2f;
            fuse.transform.position = Vector3.Lerp(start, target.position, t);
            yield return null;
        }
    }
}