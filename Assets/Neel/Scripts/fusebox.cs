using UnityEngine;

public class FuseBox : MonoBehaviour, IInteractable
{
    public Transform[] slots;
    public Renderer[] slotRenderers;
    public Light[] slotLights;

    public GameObject[] fusePrefabs;
    public GameObject door;
    public GameObject sparkEffect;

    private int currentFuses = 0;

    // 🔹 UI PROMPT
    public string GetPrompt(RohitFPSController player)
    {
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

        if (!PlayerInventory.instance.HasFuse())
        {
            Debug.Log("No fuse in inventory");
            return;
        }

        InsertAllFuses();
    }

    // 🔹 INSERT MULTIPLE
    void InsertAllFuses()
    {
        while (PlayerInventory.instance.HasFuse() && currentFuses < slots.Length)
        {
            InsertSingleFuse();
        }
    }

    // 🔹 INSERT ONE (SAFE VERSION)
    void InsertSingleFuse()
    {
        Debug.Log("Inserting fuse at slot: " + currentFuses);

        // 🚨 SAFETY CHECKS (PREVENT CRASH)
        if (slots == null || slotRenderers == null || fusePrefabs == null)
        {
            Debug.LogError("FuseBox arrays not assigned!");
            return;
        }

        if (currentFuses >= slots.Length ||
            currentFuses >= slotRenderers.Length ||
            currentFuses >= fusePrefabs.Length)
        {
            Debug.LogError("Array size mismatch!");
            return;
        }

        if (fusePrefabs[currentFuses] == null)
        {
            Debug.LogError("Fuse prefab missing at index " + currentFuses);
            return;
        }

        // 🎯 SPAWN FUSE
        GameObject fuse = Instantiate(fusePrefabs[currentFuses]);

        StartCoroutine(MoveFuse(fuse, slots[currentFuses]));

        // REMOVE FROM PLAYER
        PlayerInventory.instance.UseFuse();

        // 🎨 GET COLOR (FIXED)
        Renderer fuseRenderer = fuse.GetComponentInChildren<Renderer>();

        if (fuseRenderer == null)
        {
            Debug.LogError("No Renderer found on fuse!");
            return;
        }

        Material mat = fuseRenderer.material;
        Color fuseColor;

        if (mat.HasProperty("_Color"))
            fuseColor = mat.color;
        else if (mat.HasProperty("_BaseColor"))
            fuseColor = mat.GetColor("_BaseColor");
        else
            fuseColor = Color.white;

        Debug.Log("Fuse Color: " + fuseColor);

        // 🔥 APPLY COLOR TO SLOT
        Material slotMat = slotRenderers[currentFuses].material;

        slotMat.EnableKeyword("_EMISSION");
        slotMat.SetColor("_EmissionColor", fuseColor * 5f);
        slotMat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;

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