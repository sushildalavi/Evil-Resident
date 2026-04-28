using UnityEngine;
using System.Collections.Generic;

public enum FuseId
{
    FuseA = 0,
    FuseB = 1,
    FuseC = 2
}

public class PlayerInventory : MonoBehaviour
{
    public static PlayerInventory instance;

    private HashSet<KeyType> collectedKeys = new HashSet<KeyType>();
    private FuseId? carriedFuseId;

    // Fuse carry state
    public int fuseCount = 0;
    public int TotalFusesCollected { get; private set; }

    void Awake()
    {
        instance = this;
    }

    void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    // 🔑 KEY SYSTEM (UNCHANGED)
    public bool HasKey(KeyType keyType) => collectedKeys.Contains(keyType);

    public bool HasAllKeys() =>
        collectedKeys.Contains(KeyType.Gold) &&
        collectedKeys.Contains(KeyType.Bronze) &&
        collectedKeys.Contains(KeyType.Silver);

    public void AddKey(KeyType keyType)
    {
        if (collectedKeys.Add(keyType))
            Debug.Log($"Picked up {keyType} Key! ({collectedKeys.Count}/3)");
    }

    public int KeyCount => collectedKeys.Count;

    public bool HasGold => collectedKeys.Contains(KeyType.Gold);
    public bool HasBronze => collectedKeys.Contains(KeyType.Bronze);
    public bool HasSilver => collectedKeys.Contains(KeyType.Silver);
    public int FuseCount => fuseCount;
    public bool HasCarriedFuse => carriedFuseId.HasValue;
    public FuseId? CarriedFuseId => carriedFuseId;
    public string CarriedFuseName => carriedFuseId.HasValue ? FuseIdToLabel(carriedFuseId.Value) : "";

    // Fuse functions
    public static string FuseIdToLabel(FuseId fuseId)
    {
        switch (fuseId)
        {
            case FuseId.FuseA: return "Fuse A";
            case FuseId.FuseB: return "Fuse B";
            case FuseId.FuseC: return "Fuse C";
            default: return fuseId.ToString();
        }
    }

    public bool TryPickUpFuse(FuseId fuseId)
    {
        if (carriedFuseId.HasValue)
        {
            Debug.Log($"Cannot pick up {FuseIdToLabel(fuseId)}: already carrying {FuseIdToLabel(carriedFuseId.Value)}");
            return false;
        }

        carriedFuseId = fuseId;
        fuseCount = 1;
        TotalFusesCollected++;
        Debug.Log($"Picked up {FuseIdToLabel(fuseId)}");
        return true;
    }

    public bool TryUseFuse(FuseId requiredFuseId)
    {
        if (!carriedFuseId.HasValue)
            return false;
        if (carriedFuseId.Value != requiredFuseId)
            return false;

        carriedFuseId = null;
        fuseCount = 0;
        return true;
    }

    public void PickUpFuse()
    {
        // Legacy fallback for old content that doesn't specify a fuse ID.
        TryPickUpFuse(FuseId.FuseA);
    }

    public bool HasFuse()
    {
        return carriedFuseId.HasValue;
    }

    public void UseFuse()
    {
        if (!carriedFuseId.HasValue)
            return;

        carriedFuseId = null;
        fuseCount = 0;
    }
}
