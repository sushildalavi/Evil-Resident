using UnityEngine;
using UnityEngine.Events;

public enum PuzzleRewardMode
{
    None = 0,
    SpawnPrefab = 1,
    RevealExistingObject = 2,
    ActivateLinkedObject = 3
}

[DisallowMultipleComponent]
public class PuzzleRewardHandler : MonoBehaviour
{
    [Header("Reward Mode")]
    [SerializeField] PuzzleRewardMode rewardMode = PuzzleRewardMode.SpawnPrefab;
    [SerializeField] bool grantOnlyOnce = true;
    [SerializeField] bool verboseLogging = true;
    [SerializeField] bool rewardGranted;

    [Header("Spawn Prefab")]
    [SerializeField] GameObject rewardPrefab;
    [SerializeField] Transform rewardSpawnPoint;
    [SerializeField] bool parentSpawnedRewardToSpawnPoint;

    [Header("Reveal Existing")]
    [SerializeField] GameObject existingRewardObject;

    [Header("Activate Linked Object")]
    [SerializeField] GameObject linkedObjectToActivate;

    [Header("Additional Linked Toggles (Optional)")]
    [SerializeField] GameObject[] objectsToEnable;
    [SerializeField] GameObject[] objectsToDisable;
    [SerializeField] Behaviour[] componentsToEnable;
    [SerializeField] Behaviour[] componentsToDisable;

    [Header("Optional One-Shot Hooks")]
    [SerializeField] AudioSource audioSource;
    [SerializeField] AudioClip rewardSfx;
    [SerializeField] ParticleSystem rewardVfx;
    [SerializeField] UnityEvent onRewardGranted;

    GameObject spawnedRewardInstance;

    public bool RewardGranted => rewardGranted;
    public GameObject SpawnedRewardInstance => spawnedRewardInstance;

    void Reset()
    {
        audioSource = GetComponent<AudioSource>();
    }

    public void HandlePuzzleSolved(ColorWheelPuzzleManager source)
    {
        if (grantOnlyOnce && rewardGranted)
        {
            if (verboseLogging)
                Debug.Log("[ColorWheelPuzzle] Reward already granted. Skipping duplicate trigger.", this);
            return;
        }

        TriggerPrimaryReward();
        TriggerLinkedToggles();

        if (audioSource != null && rewardSfx != null)
            audioSource.PlayOneShot(rewardSfx);

        if (rewardVfx != null)
            rewardVfx.Play();

        rewardGranted = true;
        onRewardGranted?.Invoke();
    }

    [ContextMenu("Grant Reward (Debug)")]
    public void GrantRewardDebug()
    {
        HandlePuzzleSolved(null);
    }

    void TriggerPrimaryReward()
    {
        switch (rewardMode)
        {
            case PuzzleRewardMode.None:
                if (verboseLogging)
                    Debug.Log("[ColorWheelPuzzle] Solved with no primary reward mode.", this);
                break;

            case PuzzleRewardMode.SpawnPrefab:
                SpawnRewardPrefab();
                break;

            case PuzzleRewardMode.RevealExistingObject:
                RevealExistingRewardObject();
                break;

            case PuzzleRewardMode.ActivateLinkedObject:
                ActivateLinkedRewardObject();
                break;
        }
    }

    void SpawnRewardPrefab()
    {
        if (rewardPrefab == null)
        {
            Debug.LogWarning("[ColorWheelPuzzle] Reward mode is SpawnPrefab but Reward Prefab is missing.", this);
            return;
        }

        Transform spawn = rewardSpawnPoint != null ? rewardSpawnPoint : transform;
        Quaternion rotation = spawn != null ? spawn.rotation : Quaternion.identity;
        Vector3 position = spawn != null ? spawn.position : transform.position;

        if (spawnedRewardInstance != null)
        {
            if (verboseLogging)
                Debug.Log("[ColorWheelPuzzle] Reward already exists; not spawning another instance.", this);
            return;
        }

        if (parentSpawnedRewardToSpawnPoint && spawn != null)
            spawnedRewardInstance = Instantiate(rewardPrefab, position, rotation, spawn);
        else
            spawnedRewardInstance = Instantiate(rewardPrefab, position, rotation);

        if (verboseLogging && spawnedRewardInstance != null)
            Debug.Log($"[ColorWheelPuzzle] Spawned reward '{spawnedRewardInstance.name}'.", spawnedRewardInstance);
    }

    void RevealExistingRewardObject()
    {
        if (existingRewardObject == null)
        {
            Debug.LogWarning("[ColorWheelPuzzle] Reward mode is RevealExistingObject but Existing Reward Object is missing.", this);
            return;
        }

        existingRewardObject.SetActive(true);
        if (verboseLogging)
            Debug.Log($"[ColorWheelPuzzle] Revealed existing reward object '{existingRewardObject.name}'.", existingRewardObject);
    }

    void ActivateLinkedRewardObject()
    {
        if (linkedObjectToActivate == null)
        {
            Debug.LogWarning("[ColorWheelPuzzle] Reward mode is ActivateLinkedObject but Linked Object is missing.", this);
            return;
        }

        linkedObjectToActivate.SetActive(true);
        if (verboseLogging)
            Debug.Log($"[ColorWheelPuzzle] Activated linked object '{linkedObjectToActivate.name}'.", linkedObjectToActivate);
    }

    void TriggerLinkedToggles()
    {
        ToggleObjects(objectsToEnable, true);
        ToggleObjects(objectsToDisable, false);
        ToggleBehaviours(componentsToEnable, true);
        ToggleBehaviours(componentsToDisable, false);
    }

    static void ToggleObjects(GameObject[] objects, bool active)
    {
        if (objects == null) return;
        for (int i = 0; i < objects.Length; i++)
        {
            if (objects[i] == null) continue;
            objects[i].SetActive(active);
        }
    }

    static void ToggleBehaviours(Behaviour[] behaviours, bool enabled)
    {
        if (behaviours == null) return;
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] == null) continue;
            behaviours[i].enabled = enabled;
        }
    }
}
