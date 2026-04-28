using UnityEngine;

public class ActivatedByFuse : MonoBehaviour
{
    [Header("Fuse Mapping")]
    public FuseId requiredFuseId = FuseId.FuseA;
    [Tooltip("Optional: require power from this exact fuse box instead of any box with matching fuse ID.")]
    public FuseBox requiredFuseBox;

    [Header("Activation")]
    public bool deactivateUntilActivated = true;
    public bool activateOnlyOnce = true;
    public bool debugLogs = true;

    [Header("Controlled Targets")]
    [Tooltip("If empty, this script auto-controls all Light components on this GameObject and its children.")]
    public Light[] controlledLights;
    [Tooltip("When enabled, child AudioSources are stopped while inactive and started on activation.")]
    public bool controlChildAudioSources = true;
    [Tooltip("Optional additional objects enabled when activated.")]
    public GameObject[] objectsToEnable;

    private bool isActivated;

    private void OnEnable()
    {
        FuseBox.FuseBoxPowered += HandleFuseBoxPowered;
    }

    private void OnDisable()
    {
        FuseBox.FuseBoxPowered -= HandleFuseBoxPowered;
    }

    private void Start()
    {
        if (deactivateUntilActivated)
            ApplyActivationState(false);

        if (IsRequirementAlreadySatisfied())
            Activate();
    }

    private bool IsRequirementAlreadySatisfied()
    {
        FuseBox[] boxes = FindObjectsByType<FuseBox>(FindObjectsSortMode.None);
        if (boxes == null || boxes.Length == 0)
            return false;

        for (int i = 0; i < boxes.Length; i++)
        {
            FuseBox box = boxes[i];
            if (box == null || !box.IsPowered)
                continue;

            if (requiredFuseBox != null)
            {
                if (box == requiredFuseBox)
                    return true;
                continue;
            }

            if (box.RequiredFuseId == requiredFuseId)
                return true;
        }

        return false;
    }

    private void HandleFuseBoxPowered(FuseId fuseId, FuseBox fuseBox)
    {
        if (isActivated && activateOnlyOnce)
            return;

        if (requiredFuseBox != null)
        {
            if (fuseBox == requiredFuseBox)
                Activate();
            return;
        }

        if (fuseId == requiredFuseId)
            Activate();
    }

    private void Activate()
    {
        if (isActivated && activateOnlyOnce)
            return;

        isActivated = true;
        ApplyActivationState(true);

        if (debugLogs)
            Debug.Log($"{name} activated by fuse condition");
    }

    private void ApplyActivationState(bool active)
    {
        Light[] lights = controlledLights;
        if (lights == null || lights.Length == 0)
            lights = GetComponentsInChildren<Light>(true);

        for (int i = 0; i < lights.Length; i++)
        {
            Light l = lights[i];
            if (l == null) continue;
            l.enabled = active;
        }

        if (controlChildAudioSources)
        {
            AudioSource[] audioSources = GetComponentsInChildren<AudioSource>(true);
            for (int i = 0; i < audioSources.Length; i++)
            {
                AudioSource source = audioSources[i];
                if (source == null) continue;

                if (active)
                {
                    source.enabled = true;
                    if (!source.isPlaying)
                        source.Play();
                }
                else
                {
                    if (source.isPlaying)
                        source.Stop();
                    source.enabled = false;
                }
            }
        }

        if (objectsToEnable != null)
        {
            for (int i = 0; i < objectsToEnable.Length; i++)
            {
                GameObject go = objectsToEnable[i];
                if (go == null) continue;
                go.SetActive(active);
            }
        }
    }
}
