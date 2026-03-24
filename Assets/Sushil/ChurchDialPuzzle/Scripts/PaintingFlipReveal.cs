using System.Collections;
using UnityEngine;
using UnityEngine.Events;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DisallowMultipleComponent]
public class PaintingFlipReveal : MonoBehaviour, IInteractable
{
    public enum RevealMode
    {
        FlipOpenOnHinge,
        RotateAside,
        SlideAside,
        DisableVisual,
        MarkRevealedOnly
    }

    [Header("Core References")]
    [SerializeField] private Transform revealTarget;
    [SerializeField] private SacredGlyphPuzzleInteractor puzzleInteractor;

    [Header("Interaction")]
    [SerializeField] private KeyCode interactKey = KeyCode.E;
    [SerializeField] private string examinePrompt = "Press E to Examine";
    [SerializeField] private string inspectPuzzlePrompt = "Press E to Inspect Mechanism";
    [SerializeField] private string revealingPrompt = "Inspecting...";
    [SerializeField] private string solvedPrompt = "Mechanism solved";
    [SerializeField] private string unavailablePrompt = "Mechanism unavailable";
    [SerializeField] private bool autoEnterPuzzleAfterReveal = false;
    [SerializeField] private bool onlyRevealOnce = true;

    [Header("Fallback Direct Interaction")]
    [SerializeField] private bool enableDirectFallbackInteraction = true;
    [SerializeField, Min(0.5f)] private float directInteractDistance = 6f;
    [SerializeField, Range(-1f, 1f)] private float directInteractViewDotThreshold = 0.72f;

    [Header("Reveal Motion")]
    [SerializeField] private RevealMode revealMode = RevealMode.FlipOpenOnHinge;
    [SerializeField, Min(0.01f)] private float revealDuration = 0.85f;
    [SerializeField] private AnimationCurve revealCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] private float openAngle = 105f;
    [SerializeField] private Vector3 rotateAsideEuler = new Vector3(0f, 105f, 0f);
    [SerializeField] private Vector3 slideOffset = new Vector3(1.1f, 0f, 0f);

    [Header("Reveal State Hooks")]
    [SerializeField] private Collider[] collidersToDisable;
    [SerializeField] private GameObject[] activateOnReveal;
    [SerializeField] private GameObject[] deactivateOnReveal;
    [SerializeField] private Light[] revealLights;

    [Header("Audio Hooks")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip revealStartedClip;
    [SerializeField] private AudioClip revealCompletedClip;

    [Header("Events")]
    [SerializeField] private UnityEvent onRevealStarted = new UnityEvent();
    [SerializeField] private UnityEvent onRevealCompleted = new UnityEvent();

    private Coroutine revealRoutine;
    private Vector3 closedLocalPosition;
    private Quaternion closedLocalRotation;
    private bool hasCachedClosedPose;
    private RohitFPSController fallbackPlayer;

    public bool IsRevealed { get; private set; }
    public bool IsRevealing { get; private set; }
    public SacredGlyphPuzzleInteractor PuzzleInteractor => puzzleInteractor;
    public UnityEvent OnRevealStarted => onRevealStarted;
    public UnityEvent OnRevealCompleted => onRevealCompleted;

    void Reset()
    {
        if (revealTarget == null)
            revealTarget = transform;

        CacheClosedPose();
    }

    void Awake()
    {
        if (revealTarget == null)
            revealTarget = transform;

        CacheClosedPose();
    }

    void OnValidate()
    {
        revealDuration = Mathf.Max(0.01f, revealDuration);
        directInteractDistance = Mathf.Max(0.5f, directInteractDistance);

        if (revealTarget == null)
            revealTarget = transform;

        if (!Application.isPlaying)
            CacheClosedPose();
    }

    void Update()
    {
        if (!Application.isPlaying || !enableDirectFallbackInteraction)
            return;

        if (IsRevealing)
            return;

        if (puzzleInteractor != null && puzzleInteractor.IsInPuzzleMode)
            return;

        if (fallbackPlayer == null)
            fallbackPlayer = FindFirstObjectByType<RohitFPSController>();

        if (fallbackPlayer == null || fallbackPlayer.isHidden)
            return;

        if (!CanUseDirectFallbackInteraction(fallbackPlayer))
            return;

        if (WasInteractPressed())
            Interact(fallbackPlayer);
    }

    public string GetPrompt(RohitFPSController player)
    {
        if (IsRevealing)
            return revealingPrompt;

        if (!IsRevealed)
            return examinePrompt;

        if (puzzleInteractor != null && !puzzleInteractor.CanEnterPuzzleMode())
        {
            if (puzzleInteractor.PuzzleManager != null && puzzleInteractor.PuzzleManager.IsInteractionLocked)
                return solvedPrompt;

            return unavailablePrompt;
        }

        return inspectPuzzlePrompt;
    }

    public KeyCode GetInteractKey()
    {
        return interactKey;
    }

    public void Interact(RohitFPSController player)
    {
        if (!IsRevealed)
        {
            BeginReveal(player);
            return;
        }

        if (puzzleInteractor != null)
            puzzleInteractor.TryEnterPuzzleMode(player);
    }

    public bool BeginReveal()
    {
        return BeginReveal(null);
    }

    public bool BeginReveal(RohitFPSController player)
    {
        if (IsRevealing)
            return false;

        if (IsRevealed)
        {
            if (onlyRevealOnce)
            {
                if (autoEnterPuzzleAfterReveal && puzzleInteractor != null)
                    puzzleInteractor.TryEnterPuzzleMode(player);

                return false;
            }

            ResetToClosedState();
        }

        if (revealRoutine != null)
            StopCoroutine(revealRoutine);

        revealRoutine = StartCoroutine(RevealRoutine(player));
        return true;
    }

    public void ForceRevealImmediate()
    {
        if (!hasCachedClosedPose)
            CacheClosedPose();

        ApplyRevealPose(1f);
        CompleteRevealState();
    }

    public void ResetToClosedState()
    {
        if (revealRoutine != null)
        {
            StopCoroutine(revealRoutine);
            revealRoutine = null;
        }

        IsRevealed = false;
        IsRevealing = false;

        if (revealTarget != null)
        {
            revealTarget.gameObject.SetActive(true);
            revealTarget.localPosition = closedLocalPosition;
            revealTarget.localRotation = closedLocalRotation;
        }

        SetCollidersEnabled(true);
        SetObjectsActive(activateOnReveal, false);
        SetObjectsActive(deactivateOnReveal, true);
        SetLightsEnabled(false);
    }

    public void SetRevealTarget(Transform target)
    {
        revealTarget = target;
        CacheClosedPose();
    }

    public void SetPuzzleInteractor(SacredGlyphPuzzleInteractor interactor)
    {
        puzzleInteractor = interactor;
    }

    public void SetAutoEnterPuzzleAfterReveal(bool shouldAutoEnter)
    {
        autoEnterPuzzleAfterReveal = shouldAutoEnter;
    }

    public void SetRevealMode(RevealMode mode)
    {
        revealMode = mode;
    }

    public void SetRevealDuration(float duration)
    {
        revealDuration = Mathf.Max(0.01f, duration);
    }

    public void SetOpenAngle(float angle)
    {
        openAngle = angle;
    }

    public void SetOnlyRevealOnce(bool shouldOnlyRevealOnce)
    {
        onlyRevealOnce = shouldOnlyRevealOnce;
    }

    public void SetCollidersToDisable(Collider[] colliders)
    {
        collidersToDisable = colliders;
    }

    private IEnumerator RevealRoutine(RohitFPSController player)
    {
        IsRevealing = true;
        onRevealStarted.Invoke();
        PlayClip(revealStartedClip);

        float elapsed = 0f;
        while (elapsed < revealDuration)
        {
            elapsed += Time.deltaTime;
            float normalized = Mathf.Clamp01(elapsed / revealDuration);
            float eased = revealCurve != null ? revealCurve.Evaluate(normalized) : normalized;
            ApplyRevealPose(eased);
            yield return null;
        }

        ApplyRevealPose(1f);
        CompleteRevealState();
        revealRoutine = null;

        if (autoEnterPuzzleAfterReveal && puzzleInteractor != null)
            puzzleInteractor.TryEnterPuzzleMode(player);
    }

    private void CompleteRevealState()
    {
        IsRevealing = false;
        IsRevealed = true;
        SetCollidersEnabled(false);
        SetObjectsActive(activateOnReveal, true);
        SetObjectsActive(deactivateOnReveal, false);
        SetLightsEnabled(true);
        PlayClip(revealCompletedClip);
        onRevealCompleted.Invoke();
    }

    private bool CanUseDirectFallbackInteraction(RohitFPSController player)
    {
        if (player == null)
            return false;

        Transform playerCamera = player.cameraTransform != null ? player.cameraTransform : player.transform;
        Vector3 targetPoint = GetInteractionWorldPoint();
        Vector3 toTarget = targetPoint - playerCamera.position;
        float distance = toTarget.magnitude;
        if (distance > directInteractDistance || distance <= 0.001f)
            return false;

        Vector3 direction = toTarget / distance;
        float facingDot = Vector3.Dot(playerCamera.forward, direction);
        return facingDot >= directInteractViewDotThreshold;
    }

    private Vector3 GetInteractionWorldPoint()
    {
        Collider collider = GetComponent<Collider>();
        if (collider != null)
            return collider.bounds.center;

        if (revealTarget != null)
            return revealTarget.position;

        return transform.position;
    }

    private bool WasInteractPressed()
    {
        bool pressed = false;

#if ENABLE_LEGACY_INPUT_MANAGER
        pressed |= Input.GetKeyDown(interactKey);
#endif
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            switch (interactKey)
            {
                case KeyCode.E:
                    pressed |= Keyboard.current.eKey.wasPressedThisFrame;
                    break;
                case KeyCode.F:
                    pressed |= Keyboard.current.fKey.wasPressedThisFrame;
                    break;
                default:
                    break;
            }
        }
#endif

        return pressed;
    }

    private void ApplyRevealPose(float normalized)
    {
        if (revealTarget == null)
            return;

        switch (revealMode)
        {
            case RevealMode.FlipOpenOnHinge:
                revealTarget.localPosition = closedLocalPosition;
                revealTarget.localRotation = closedLocalRotation * Quaternion.Euler(0f, openAngle * normalized, 0f);
                break;

            case RevealMode.RotateAside:
                revealTarget.localPosition = closedLocalPosition;
                revealTarget.localRotation = closedLocalRotation * Quaternion.Euler(rotateAsideEuler * normalized);
                break;

            case RevealMode.SlideAside:
                revealTarget.localRotation = closedLocalRotation;
                revealTarget.localPosition = closedLocalPosition + (slideOffset * normalized);
                break;

            case RevealMode.DisableVisual:
                revealTarget.localPosition = closedLocalPosition;
                revealTarget.localRotation = closedLocalRotation;
                if (normalized >= 1f)
                    revealTarget.gameObject.SetActive(false);
                break;

            case RevealMode.MarkRevealedOnly:
                revealTarget.localPosition = closedLocalPosition;
                revealTarget.localRotation = closedLocalRotation;
                break;
        }
    }

    private void CacheClosedPose()
    {
        if (revealTarget == null)
            return;

        closedLocalPosition = revealTarget.localPosition;
        closedLocalRotation = revealTarget.localRotation;
        hasCachedClosedPose = true;
    }

    private void SetCollidersEnabled(bool enabledState)
    {
        if (collidersToDisable == null)
            return;

        for (int i = 0; i < collidersToDisable.Length; i++)
        {
            if (collidersToDisable[i] != null)
                collidersToDisable[i].enabled = enabledState;
        }
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

    private void SetLightsEnabled(bool enabledState)
    {
        if (revealLights == null)
            return;

        for (int i = 0; i < revealLights.Length; i++)
        {
            if (revealLights[i] != null)
                revealLights[i].enabled = enabledState;
        }
    }

    private void PlayClip(AudioClip clip)
    {
        if (audioSource != null && clip != null)
            audioSource.PlayOneShot(clip);
    }
}
