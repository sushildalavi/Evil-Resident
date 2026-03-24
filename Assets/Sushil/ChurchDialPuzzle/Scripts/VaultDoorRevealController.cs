using System.Collections;
using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public class VaultDoorRevealController : MonoBehaviour
{
    public enum RevealMode
    {
        SingleRotateDoor,
        DoubleRotateDoor,
        SlidingPanel
    }

    [Header("Mode")]
    [SerializeField] private RevealMode revealMode = RevealMode.DoubleRotateDoor;
    [SerializeField] private bool onlyOpenOnce = true;

    [Header("References")]
    [SerializeField] private Transform singleDoorTransform;
    [SerializeField] private Transform leftDoorTransform;
    [SerializeField] private Transform rightDoorTransform;
    [SerializeField] private Transform slidingPanelTransform;
    [SerializeField] private Light vaultInteriorLight;
    [SerializeField] private AudioSource audioSource;

    [Header("Motion")]
    [SerializeField, Min(0f)] private float openDelay = 0.3f;
    [SerializeField, Min(0.01f)] private float openDuration = 1.8f;
    [SerializeField] private float openAngle = 110f;
    [SerializeField] private Vector3 slideOffset = new Vector3(0f, 0f, -1.25f);
    [SerializeField] private AnimationCurve openCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Optional State Hooks")]
    [SerializeField] private GameObject[] activateOnOpen;
    [SerializeField] private GameObject[] deactivateOnOpen;
    [SerializeField] private AudioClip openStartedClip;
    [SerializeField] private AudioClip openCompletedClip;

    [Header("Events")]
    [SerializeField] private UnityEvent onVaultOpenStarted = new UnityEvent();
    [SerializeField] private UnityEvent onVaultOpenCompleted = new UnityEvent();

    private Quaternion singleDoorClosedRotation;
    private Quaternion leftDoorClosedRotation;
    private Quaternion rightDoorClosedRotation;
    private Vector3 slidingPanelClosedPosition;
    private Coroutine openRoutine;
    private bool hasOpened;
    private bool hasCachedClosedState;

    public bool HasOpened => hasOpened;
    public UnityEvent OnVaultOpenStarted => onVaultOpenStarted;
    public UnityEvent OnVaultOpenCompleted => onVaultOpenCompleted;

    void Awake()
    {
        CacheClosedState();
        SetOpenState(false);
    }

    void OnValidate()
    {
        openDelay = Mathf.Max(0f, openDelay);
        openDuration = Mathf.Max(0.01f, openDuration);

        if (!Application.isPlaying)
            CacheClosedState();
    }

    public void OpenVault()
    {
        if (onlyOpenOnce && hasOpened)
            return;

        if (openRoutine != null)
            StopCoroutine(openRoutine);

        openRoutine = StartCoroutine(OpenRoutine());
    }

    public void HandlePuzzleSolved()
    {
        OpenVault();
    }

    public void ResetToClosedState()
    {
        if (openRoutine != null)
        {
            StopCoroutine(openRoutine);
            openRoutine = null;
        }

        hasOpened = false;
        ApplyMotion(0f);
        SetOpenState(false);
    }

    public void SetSingleDoor(Transform door)
    {
        singleDoorTransform = door;
        CacheClosedState();
    }

    public void SetRevealMode(RevealMode mode)
    {
        revealMode = mode;
        CacheClosedState();
    }

    public void SetOpenAngle(float angle)
    {
        openAngle = angle;
    }

    public void SetOpenDuration(float duration)
    {
        openDuration = Mathf.Max(0.01f, duration);
    }

    public void SetOpenDelay(float delay)
    {
        openDelay = Mathf.Max(0f, delay);
    }

    public void SetDoubleDoors(Transform leftDoor, Transform rightDoor)
    {
        leftDoorTransform = leftDoor;
        rightDoorTransform = rightDoor;
        CacheClosedState();
    }

    public void SetSlidingPanel(Transform panel)
    {
        slidingPanelTransform = panel;
        CacheClosedState();
    }

    public void SetActivateOnOpen(GameObject[] objects)
    {
        activateOnOpen = objects;
    }

    private IEnumerator OpenRoutine()
    {
        if (!hasCachedClosedState)
            CacheClosedState();

        onVaultOpenStarted.Invoke();
        PlayClip(openStartedClip);

        if (openDelay > 0f)
            yield return new WaitForSeconds(openDelay);

        SetOpenState(true);

        float elapsed = 0f;
        while (elapsed < openDuration)
        {
            elapsed += Time.deltaTime;
            float normalized = Mathf.Clamp01(elapsed / openDuration);
            float eased = openCurve != null ? openCurve.Evaluate(normalized) : normalized;
            ApplyMotion(eased);
            yield return null;
        }

        ApplyMotion(1f);
        hasOpened = true;
        openRoutine = null;
        PlayClip(openCompletedClip);
        onVaultOpenCompleted.Invoke();
    }

    private void ApplyMotion(float normalized)
    {
        switch (revealMode)
        {
            case RevealMode.SingleRotateDoor:
                if (singleDoorTransform != null)
                {
                    Quaternion targetRotation = singleDoorClosedRotation * Quaternion.Euler(0f, openAngle, 0f);
                    singleDoorTransform.localRotation = Quaternion.Slerp(singleDoorClosedRotation, targetRotation, normalized);
                }
                break;

            case RevealMode.DoubleRotateDoor:
                if (leftDoorTransform != null)
                {
                    Quaternion leftTarget = leftDoorClosedRotation * Quaternion.Euler(0f, -openAngle, 0f);
                    leftDoorTransform.localRotation = Quaternion.Slerp(leftDoorClosedRotation, leftTarget, normalized);
                }

                if (rightDoorTransform != null)
                {
                    Quaternion rightTarget = rightDoorClosedRotation * Quaternion.Euler(0f, openAngle, 0f);
                    rightDoorTransform.localRotation = Quaternion.Slerp(rightDoorClosedRotation, rightTarget, normalized);
                }
                break;

            case RevealMode.SlidingPanel:
                if (slidingPanelTransform != null)
                    slidingPanelTransform.localPosition = Vector3.Lerp(slidingPanelClosedPosition, slidingPanelClosedPosition + slideOffset, normalized);
                break;
        }
    }

    private void SetOpenState(bool openState)
    {
        if (vaultInteriorLight != null)
            vaultInteriorLight.enabled = openState;

        SetObjectsActive(activateOnOpen, openState);
        SetObjectsActive(deactivateOnOpen, !openState);
    }

    private void CacheClosedState()
    {
        if (singleDoorTransform != null)
            singleDoorClosedRotation = singleDoorTransform.localRotation;

        if (leftDoorTransform != null)
            leftDoorClosedRotation = leftDoorTransform.localRotation;

        if (rightDoorTransform != null)
            rightDoorClosedRotation = rightDoorTransform.localRotation;

        if (slidingPanelTransform != null)
            slidingPanelClosedPosition = slidingPanelTransform.localPosition;

        hasCachedClosedState = true;
    }

    private void SetObjectsActive(GameObject[] targets, bool activeState)
    {
        if (targets == null)
            return;

        for (int i = 0; i < targets.Length; i++)
        {
            if (targets[i] != null)
                targets[i].SetActive(activeState);
        }
    }

    private void PlayClip(AudioClip clip)
    {
        if (audioSource != null && clip != null)
            audioSource.PlayOneShot(clip);
    }
}
