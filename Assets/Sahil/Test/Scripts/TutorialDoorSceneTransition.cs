using UnityEngine;
using UnityEngine.SceneManagement;

public class TutorialDoorSceneTransition : MonoBehaviour, IInteractable
{
    [Header("Key Requirement")]
    public KeyType requiredKey = KeyType.Rectangle;
    [Tooltip("If true, any collected key can open this tutorial door.")]
    public bool allowAnyCollectedKey = true;

    [Header("Transition")]
    public string mainGameSceneName = "New Tutorial 2";
    public float interactDistance = 3f;

    [Header("Prompt")]
    public string openPrompt = "Press E to open the door.";
    public string lockedPrompt = "The way forward is locked";

    [Header("Optional")]
    public AudioClip unlockSound;
    public AudioClip lockedSound;
    public AudioSource audioSource;

    [Header("Legacy Serialized Compatibility")]
    public float openAngle = 90f;
    public float openSpeed = 2f;
    public bool openClockwise = true;
    public bool hingeOnRightEdge = false;
    public float transitionDelay = 0f;
    public string transitionMessage = "Tutorial 2: Stalker";
    public HideableObject[] requiredHideSpots;

    bool loading;

    void Start()
    {
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();
    }

    public KeyCode GetInteractKey() => KeyCode.E;

    public string GetPrompt(RohitFPSController player)
    {
        if (loading)
            return string.Empty;
        return HasRequiredKey(player) ? openPrompt : lockedPrompt;
    }

    public void Interact(RohitFPSController player)
    {
        if (loading)
            return;

        bool hasKey = HasRequiredKey(player);
        if (!hasKey)
        {
            if (lockedSound != null && audioSource != null)
                audioSource.PlayOneShot(lockedSound);
            return;
        }

        if (unlockSound != null && audioSource != null)
            audioSource.PlayOneShot(unlockSound);

        loading = true;

        TutorialStepUI tutorial = FindFirstObjectByType<TutorialStepUI>();
        if (tutorial != null)
            tutorial.MarkDoorAndKeyComplete();

        LoadTargetScene();
    }

    bool HasRequiredKey(RohitFPSController player)
    {
        PlayerInventory inventory = player != null ? player.GetComponent<PlayerInventory>() : null;
        if (inventory == null)
            return false;

        if (inventory.HasKey(requiredKey))
            return true;

        if (!allowAnyCollectedKey)
            return false;

        return inventory.KeyCount > 0;
    }

    void LoadTargetScene()
    {
        string target = string.IsNullOrWhiteSpace(mainGameSceneName) ? string.Empty : mainGameSceneName.Trim();
        if (string.IsNullOrEmpty(target))
        {
            Debug.LogError("[TutorialDoorSceneTransition] No target scene configured.");
            return;
        }

        // Path form in build settings (e.g. Assets/.../Scene.unity).
        int buildIndex = SceneUtility.GetBuildIndexByScenePath(target);
        if (buildIndex >= 0)
        {
            SceneManager.LoadScene(buildIndex);
            return;
        }

        // Name form for tutorial scenes.
        string tutorialPath = $"Assets/Sahil/Tutorial/{target}.unity";
        buildIndex = SceneUtility.GetBuildIndexByScenePath(tutorialPath);
        if (buildIndex >= 0)
        {
            SceneManager.LoadScene(buildIndex);
            return;
        }

        SceneManager.LoadScene(target);
    }
}
