using System.Collections;
using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public class VaultRevealController : MonoBehaviour
{
    public enum RevealMode
    {
        SingleRotatingDoor,
        DoubleRotatingDoors,
        SlidingPanel
    }

    [Header("Mode")]
    [SerializeField] private RevealMode revealMode = RevealMode.DoubleRotatingDoors;
    [SerializeField] private bool triggerOnlyOnce = true;

    [Header("Timing")]
    [SerializeField] private float openDelay = 0.35f;
    [SerializeField] private float openDuration = 1.8f;
    [SerializeField] private AnimationCurve openCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Door References")]
    [SerializeField] private Transform singleDoor;
    [SerializeField] private Vector3 singleDoorOpenEuler = new Vector3(0f, -105f, 0f);
    [SerializeField] private Transform vaultDoorLeft;
    [SerializeField] private Vector3 leftDoorOpenEuler = new Vector3(0f, -95f, 0f);
    [SerializeField] private Transform vaultDoorRight;
    [SerializeField] private Vector3 rightDoorOpenEuler = new Vector3(0f, 95f, 0f);
    [SerializeField] private Transform slidingPanel;
    [SerializeField] private Vector3 slideOffset = new Vector3(0f, 0f, -1.5f);

    [Header("Vault Interior")]
    [SerializeField] private GameObject vaultInterior;
    [SerializeField] private Light[] interiorLights;
    [SerializeField] private GameObject[] activateOnOpen;
    [SerializeField] private GameObject[] deactivateOnOpen;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip unlockStartSound;
    [SerializeField] private AudioClip vaultOpenSound;

    [Header("Events")]
    public UnityEvent onRevealStarted;
    public UnityEvent onRevealCompleted;

    private Vector3 singleDoorClosedLocalEuler;
    private Vector3 leftDoorClosedLocalEuler;
    private Vector3 rightDoorClosedLocalEuler;
    private Vector3 slidingPanelClosedLocalPosition;
    private Coroutine revealRoutine;
    private bool hasOpened;

    public bool HasOpened => hasOpened;

    void Reset()
    {
        AutoAssign();
        CacheClosedState();
    }

    void Awake()
    {
        AutoAssign();
        CacheClosedState();
        if (!hasOpened)
            SetVaultOpenState(false);
    }

    void OnValidate()
    {
        AutoAssign();
        openDelay = Mathf.Max(0f, openDelay);
        openDuration = Mathf.Max(0.01f, openDuration);
    }

    public void Reveal()
    {
        if (!enabled)
            return;

        if (triggerOnlyOnce && hasOpened)
            return;

        if (revealRoutine != null)
            StopCoroutine(revealRoutine);

        revealRoutine = StartCoroutine(RevealRoutine());
    }

    public void TriggerReveal()
    {
        Reveal();
    }

    public void ResetForTesting()
    {
        if (revealRoutine != null)
        {
            StopCoroutine(revealRoutine);
            revealRoutine = null;
        }

        hasOpened = false;
        ApplyOpenState(0f);
        SetVaultOpenState(false);
    }

    public void SetVaultInterior(GameObject interior)
    {
        vaultInterior = interior;
    }

    public void SetSingleDoor(Transform door)
    {
        singleDoor = door;
    }

    public void SetDoubleDoors(Transform left, Transform right)
    {
        vaultDoorLeft = left;
        vaultDoorRight = right;
    }

    public void SetSlidingPanel(Transform panel)
    {
        slidingPanel = panel;
    }

    private IEnumerator RevealRoutine()
    {
        CacheClosedState();
        onRevealStarted.Invoke();
        PlayClip(unlockStartSound);

        if (openDelay > 0f)
            yield return new WaitForSeconds(openDelay);

        PlayClip(vaultOpenSound);

        float elapsed = 0f;
        float duration = Mathf.Max(0.01f, openDuration);
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = openCurve != null ? openCurve.Evaluate(t) : t;
            ApplyOpenState(eased);
            yield return null;
        }

        ApplyOpenState(1f);
        SetVaultOpenState(true);
        hasOpened = true;
        revealRoutine = null;
        onRevealCompleted.Invoke();
    }

    private void ApplyOpenState(float normalized)
    {
        switch (revealMode)
        {
            case RevealMode.SingleRotatingDoor:
                if (singleDoor != null)
                {
                    Vector3 targetEuler = singleDoorClosedLocalEuler + singleDoorOpenEuler;
                    singleDoor.localRotation = Quaternion.Euler(Vector3.Lerp(singleDoorClosedLocalEuler, targetEuler, normalized));
                }
                break;

            case RevealMode.DoubleRotatingDoors:
                if (vaultDoorLeft != null)
                {
                    Vector3 leftTarget = vaultDoorLeftClosedTarget();
                    vaultDoorLeft.localRotation = Quaternion.Euler(Vector3.Lerp(leftDoorClosedLocalEuler, leftTarget, normalized));
                }

                if (vaultDoorRight != null)
                {
                    Vector3 rightTarget = vaultDoorRightClosedTarget();
                    vaultDoorRight.localRotation = Quaternion.Euler(Vector3.Lerp(rightDoorClosedLocalEuler, rightTarget, normalized));
                }
                break;

            case RevealMode.SlidingPanel:
                if (slidingPanel != null)
                    slidingPanel.localPosition = slidingPanelClosedLocalPosition + Vector3.Lerp(Vector3.zero, slideOffset, normalized);
                break;
        }
    }

    private Vector3 vaultDoorLeftClosedTarget()
    {
        return leftDoorClosedLocalEuler + leftDoorOpenEuler;
    }

    private Vector3 vaultDoorRightClosedTarget()
    {
        return rightDoorClosedLocalEuler + rightDoorOpenEuler;
    }

    private void SetVaultOpenState(bool open)
    {
        if (vaultInterior != null)
            vaultInterior.SetActive(open);

        if (interiorLights != null)
        {
            for (int i = 0; i < interiorLights.Length; i++)
            {
                if (interiorLights[i] != null)
                    interiorLights[i].enabled = open;
            }
        }

        SetActiveState(activateOnOpen, open);
        SetActiveState(deactivateOnOpen, !open);
    }

    private void SetActiveState(GameObject[] targets, bool state)
    {
        if (targets == null)
            return;

        for (int i = 0; i < targets.Length; i++)
        {
            if (targets[i] != null)
                targets[i].SetActive(state);
        }
    }

    private void CacheClosedState()
    {
        if (singleDoor != null)
            singleDoorClosedLocalEuler = singleDoor.localEulerAngles;

        if (vaultDoorLeft != null)
            leftDoorClosedLocalEuler = vaultDoorLeft.localEulerAngles;

        if (vaultDoorRight != null)
            rightDoorClosedLocalEuler = vaultDoorRight.localEulerAngles;

        if (slidingPanel != null)
            slidingPanelClosedLocalPosition = slidingPanel.localPosition;
    }

    private void AutoAssign()
    {
        if (vaultInterior == null)
        {
            Transform interior = transform.Find("VaultInterior");
            if (interior != null)
                vaultInterior = interior.gameObject;
        }

        if (interiorLights == null || interiorLights.Length == 0)
            interiorLights = GetComponentsInChildren<Light>(true);
    }

    private void PlayClip(AudioClip clip)
    {
        if (audioSource != null && clip != null)
            audioSource.PlayOneShot(clip);
    }
}
