using UnityEngine;
using System.Collections.Generic;

public class PlayerInventory : MonoBehaviour
{
    private HashSet<KeyType> collectedKeys = new HashSet<KeyType>();

    public bool HasKey(KeyType keyType) => collectedKeys.Contains(keyType);

    public bool HasAllKeys() =>
        collectedKeys.Contains(KeyType.Circle) &&
        collectedKeys.Contains(KeyType.Rectangle) &&
        collectedKeys.Contains(KeyType.Square);

    public void AddKey(KeyType keyType)
    {
        if (collectedKeys.Add(keyType))
            Debug.Log($"Picked up {keyType} Key! ({collectedKeys.Count}/3)");
    }

    public int KeyCount => collectedKeys.Count;

    public bool HasCircle => collectedKeys.Contains(KeyType.Circle);
    public bool HasRectangle => collectedKeys.Contains(KeyType.Rectangle);
    public bool HasSquare => collectedKeys.Contains(KeyType.Square);
}
