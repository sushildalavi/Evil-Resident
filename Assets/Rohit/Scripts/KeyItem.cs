using UnityEngine;

public class KeyItem : MonoBehaviour, IInteractable
{
    [Header("Key Settings")]
    public KeyType keyType;

    [Header("Floating Animation")]
    public float rotateSpeed = 50f;
    public float bobSpeed = 2f;
    public float bobHeight = 0.25f;

    private Vector3 startPos;

    void Start()
    {
        startPos = transform.position;
    }

    void Update()
    {
        transform.Rotate(Vector3.up * rotateSpeed * Time.deltaTime);
        Vector3 pos = startPos;
        pos.y += Mathf.Sin(Time.time * bobSpeed) * bobHeight;
        transform.position = pos;
    }

    public KeyCode GetInteractKey() => KeyCode.P;

    public string GetPrompt(RohitFPSController player)
    {
        PlayerInventory inventory = player.GetComponent<PlayerInventory>();
        if (inventory != null && inventory.HasKey(keyType))
            return $"{keyType} Key (already collected)";
        return $"Press P to pick up {keyType} Key";
    }

    public void Interact(RohitFPSController player)
    {
        PlayerInventory inventory = player.GetComponent<PlayerInventory>();
        if (inventory == null) return;

        if (!inventory.HasKey(keyType))
        {
            inventory.AddKey(keyType);
            Destroy(gameObject);
        }
    }
}
