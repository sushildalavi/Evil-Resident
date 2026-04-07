using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class TutorialDoorTransition : MonoBehaviour, IInteractable
{
    [Header("Lock Settings")]
    public KeyType requiredKey;
    [Tooltip("If true, any collected key can open the tutorial door.")]
    public bool allowAnyCollectedKey = true;

    [Header("Open Animation")]
    public float openAngle = 90f;
    public float openSpeed = 2f;
    public bool openClockwise = true;
    [Tooltip("Which edge of the DoorPanel the hinge is on.")]
    public bool hingeOnRightEdge = false;

    [Header("Scene Transition")]
    [Tooltip("Name of tutorial follow-up scene to load.")]
    public string mainGameSceneName = "New Tutorial 2";
    [Tooltip("Delay in seconds after the door opens before loading the scene.")]
    public float transitionDelay = 1.2f;
    public string transitionMessage = "Tutorial 2: Stalker";

    [Header("Legacy (Unused)")]
    [Tooltip("Kept only for backward scene compatibility.")]
    public HideableObject[] requiredHideSpots;

    [Header("Audio (Optional)")]
    public AudioClip unlockSound;
    public AudioClip lockedSound;

    bool isOpen;
    bool sceneLoadTriggered;
    float currentAngle;
    float timer;
    AudioSource audioSource;
    Collider[] doorColliders;
    Transform hingePivot;

    CanvasGroup transitionCanvasGroup;

    void Start()
    {
        audioSource = GetComponent<AudioSource>();

        Door existingDoor = GetComponent<Door>();
        if (existingDoor != null)
            Destroy(existingDoor);

        Transform panel = transform.Find("DoorPanel");
        doorColliders = panel != null
            ? panel.GetComponentsInChildren<Collider>(true)
            : GetComponentsInChildren<Collider>(true);

        SetCollidersEnabled(true);

        if (panel != null)
        {
            float halfWidth = panel.localScale.x / 2f;
            Vector3 hingeOffset = hingeOnRightEdge
                ? new Vector3(halfWidth, 0f, 0f)
                : new Vector3(-halfWidth, 0f, 0f);

            Vector3 hingeLocalPos = panel.localPosition + hingeOffset;
            hingeLocalPos.y = 0f;

            GameObject hingeObj = new GameObject("_HingePivot");
            hingeObj.transform.SetParent(transform, false);
            hingeObj.transform.localPosition = hingeLocalPos;
            hingeObj.transform.localRotation = Quaternion.identity;

            panel.SetParent(hingeObj.transform, true);
            hingePivot = hingeObj.transform;
        }
    }

    void Update()
    {
        if (!isOpen)
            return;

        float direction = openClockwise ? -1f : 1f;
        float target = openAngle * direction;
        currentAngle = Mathf.MoveTowards(currentAngle, target, openSpeed * 100f * Time.deltaTime);

        if (hingePivot != null)
            hingePivot.localRotation = Quaternion.Euler(0f, currentAngle, 0f);

        if (sceneLoadTriggered)
            return;

        timer -= Time.deltaTime;
        if (timer <= 0f)
        {
            sceneLoadTriggered = true;
            LoadTargetScene();
        }
    }

    public KeyCode GetInteractKey() => KeyCode.E;

    public string GetPrompt(RohitFPSController player)
    {
        if (isOpen) return string.Empty;

        if (HasRequiredKey(player))
            return "Press E to open the door.";

        return "The way forward is locked";
    }

    public void Interact(RohitFPSController player)
    {
        if (isOpen) return;

        if (HasRequiredKey(player))
        {
            PlaySound(unlockSound);

            TutorialStepUI tutorial = FindFirstObjectByType<TutorialStepUI>();
            if (tutorial != null)
                tutorial.MarkDoorAndKeyComplete();

            // Tutorial 1 should hand off immediately once the player opens with the key.
            LoadTargetScene();
        }
        else
        {
            PlaySound(lockedSound);
        }
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
        const string tutorial2Path = "Assets/Sahil/Tutorial/New Tutorial 2.unity";
        int buildIndex = SceneUtility.GetBuildIndexByScenePath(tutorial2Path);
        if (buildIndex >= 0)
        {
            SceneManager.LoadScene(buildIndex);
            return;
        }

        SceneManager.LoadScene(mainGameSceneName);
    }

    void ShowTransitionMessage()
    {
        if (transitionCanvasGroup != null)
        {
            transitionCanvasGroup.alpha = 1f;
            return;
        }

        GameObject canvasObj = new GameObject("TutorialDoorTransitionCanvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 320;

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        transitionCanvasGroup = canvasObj.AddComponent<CanvasGroup>();
        transitionCanvasGroup.alpha = 1f;

        GameObject bgObj = new GameObject("Background");
        bgObj.transform.SetParent(canvasObj.transform, false);
        Image bg = bgObj.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.55f);
        RectTransform bgRect = bg.rectTransform;
        bgRect.anchorMin = new Vector2(0.3f, 0.45f);
        bgRect.anchorMax = new Vector2(0.7f, 0.58f);
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;

        GameObject textObj = new GameObject("TransitionText");
        textObj.transform.SetParent(canvasObj.transform, false);
        Text text = textObj.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 42;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = new Color(1f, 0.93f, 0.75f, 1f);
        text.text = string.IsNullOrWhiteSpace(transitionMessage) ? "Tutorial 2: Stalker" : transitionMessage;
        text.resizeTextForBestFit = true;
        text.resizeTextMinSize = 22;
        text.resizeTextMaxSize = 42;

        RectTransform textRect = text.rectTransform;
        textRect.anchorMin = new Vector2(0.32f, 0.47f);
        textRect.anchorMax = new Vector2(0.68f, 0.56f);
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        Shadow shadow = textObj.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.8f);
        shadow.effectDistance = new Vector2(2f, -2f);
    }

    void SetCollidersEnabled(bool enabled)
    {
        if (doorColliders == null) return;
        for (int i = 0; i < doorColliders.Length; i++)
        {
            if (doorColliders[i] != null)
                doorColliders[i].enabled = enabled;
        }
    }

    void PlaySound(AudioClip clip)
    {
        if (clip != null && audioSource != null)
            audioSource.PlayOneShot(clip);
    }
}
