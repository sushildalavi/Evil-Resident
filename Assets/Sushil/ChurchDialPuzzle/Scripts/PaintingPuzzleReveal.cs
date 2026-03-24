using System.Collections;
using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public class PaintingPuzzleReveal : MonoBehaviour
{
    public enum RevealMode
    {
        None,
        SwingOpen,
        SlideAside,
        FadeOutAndDisable,
        DisableInstant
    }

    [Header("Reveal")]
    [SerializeField] private RevealMode revealMode = RevealMode.SwingOpen;
    [SerializeField] private Transform panelRoot;
    [SerializeField] private float revealDelay = 0.05f;
    [SerializeField] private float revealDuration = 0.8f;
    [SerializeField] private AnimationCurve revealCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Transforms")]
    [SerializeField] private Vector3 swingOpenEuler = new Vector3(0f, -100f, 0f);
    [SerializeField] private Vector3 slideOffset = new Vector3(1.25f, 0f, 0f);

    [Header("Visibility")]
    [SerializeField] private Renderer[] fadeRenderers;
    [SerializeField] private Collider[] collidersToDisable;
    [SerializeField] private bool disablePanelAtEnd;
    [SerializeField] private GameObject[] activateOnReveal;
    [SerializeField] private GameObject[] deactivateOnReveal;

    [Header("Animation Hooks")]
    [SerializeField] private Animator[] animators;
    [SerializeField] private string revealTriggerName = "Reveal";
    [SerializeField] private string revealedBoolName = "Revealed";
    [SerializeField] private bool setRevealedBool = true;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip revealStartedSound;
    [SerializeField] private AudioClip revealCompletedSound;

    [Header("Events")]
    public UnityEvent onRevealStarted;
    public UnityEvent onRevealCompleted;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    private readonly MaterialPropertyBlock propertyBlock = new MaterialPropertyBlock();
    private Color[] originalRendererColors;
    private Vector3 closedLocalPosition;
    private Quaternion closedLocalRotation;
    private Coroutine revealRoutine;

    public bool IsRevealed { get; private set; }
    public bool IsRevealing { get; private set; }

    void Reset()
    {
        AutoAssign();
        CacheClosedPose();
    }

    void Awake()
    {
        AutoAssign();
        CacheClosedPose();
        CacheRendererColors();
    }

    void OnValidate()
    {
        AutoAssign();
        revealDelay = Mathf.Max(0f, revealDelay);
        revealDuration = Mathf.Max(0.01f, revealDuration);
    }

    public bool BeginReveal()
    {
        return BeginReveal(null, null);
    }

    public bool BeginReveal(RohitFPSController player, ChurchDialPuzzleManager puzzleToEnterAfterReveal)
    {
        if (!enabled || IsRevealing)
            return false;

        if (IsRevealed)
        {
            if (puzzleToEnterAfterReveal != null)
                puzzleToEnterAfterReveal.EnterPuzzle(player);
            return false;
        }

        if (revealRoutine != null)
            StopCoroutine(revealRoutine);

        revealRoutine = StartCoroutine(RevealRoutine(player, puzzleToEnterAfterReveal));
        return true;
    }

    public void ForceRevealImmediate()
    {
        if (panelRoot == null)
            return;

        IsRevealing = false;
        IsRevealed = true;
        ApplyRevealPose(1f);
        ApplyStateToggles();
    }

    public void SetPanelRoot(Transform root)
    {
        panelRoot = root;
        AutoAssign();
        CacheClosedPose();
        CacheRendererColors();
    }

    public void SetCollidersToDisable(Collider[] colliders)
    {
        collidersToDisable = colliders;
    }

    private IEnumerator RevealRoutine(RohitFPSController player, ChurchDialPuzzleManager puzzleToEnterAfterReveal)
    {
        AutoAssign();
        CacheClosedPose();
        CacheRendererColors();

        IsRevealing = true;
        onRevealStarted.Invoke();
        PlayClip(revealStartedSound);
        TriggerAnimators();

        if (revealDelay > 0f)
            yield return new WaitForSeconds(revealDelay);

        float elapsed = 0f;
        float duration = Mathf.Max(0.01f, revealDuration);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = revealCurve != null ? revealCurve.Evaluate(t) : t;
            ApplyRevealPose(eased);
            yield return null;
        }

        ApplyRevealPose(1f);
        ApplyStateToggles();
        IsRevealing = false;
        IsRevealed = true;
        revealRoutine = null;
        PlayClip(revealCompletedSound);
        onRevealCompleted.Invoke();

        if (puzzleToEnterAfterReveal != null)
            puzzleToEnterAfterReveal.EnterPuzzle(player);
    }

    private void ApplyRevealPose(float normalized)
    {
        if (panelRoot == null)
            return;

        switch (revealMode)
        {
            case RevealMode.None:
                panelRoot.localPosition = closedLocalPosition;
                panelRoot.localRotation = closedLocalRotation;
                break;

            case RevealMode.SwingOpen:
                panelRoot.localPosition = closedLocalPosition;
                panelRoot.localRotation = closedLocalRotation * Quaternion.Euler(Vector3.Lerp(Vector3.zero, swingOpenEuler, normalized));
                break;

            case RevealMode.SlideAside:
                panelRoot.localRotation = closedLocalRotation;
                panelRoot.localPosition = closedLocalPosition + Vector3.Lerp(Vector3.zero, slideOffset, normalized);
                break;

            case RevealMode.FadeOutAndDisable:
                panelRoot.localPosition = closedLocalPosition;
                panelRoot.localRotation = closedLocalRotation;
                ApplyFade(1f - normalized);
                break;

            case RevealMode.DisableInstant:
                panelRoot.localPosition = closedLocalPosition;
                panelRoot.localRotation = closedLocalRotation;
                if (normalized >= 1f)
                    panelRoot.gameObject.SetActive(false);
                break;
        }
    }

    private void ApplyFade(float alpha)
    {
        if (fadeRenderers == null)
            return;

        for (int i = 0; i < fadeRenderers.Length; i++)
        {
            Renderer renderer = fadeRenderers[i];
            if (renderer == null)
                continue;

            propertyBlock.Clear();
            renderer.GetPropertyBlock(propertyBlock);

            Color targetColor = originalRendererColors != null && i < originalRendererColors.Length
                ? originalRendererColors[i]
                : Color.white;
            targetColor.a = alpha;

            Material sharedMaterial = renderer.sharedMaterial;
            if (sharedMaterial != null && sharedMaterial.HasProperty(BaseColorId))
                propertyBlock.SetColor(BaseColorId, targetColor);

            if (sharedMaterial != null && sharedMaterial.HasProperty(ColorId))
                propertyBlock.SetColor(ColorId, targetColor);

            renderer.SetPropertyBlock(propertyBlock);
        }
    }

    private void ApplyStateToggles()
    {
        SetCollidersEnabled(false);
        SetGameObjectState(activateOnReveal, true);
        SetGameObjectState(deactivateOnReveal, false);

        if ((revealMode == RevealMode.FadeOutAndDisable || revealMode == RevealMode.DisableInstant) && disablePanelAtEnd && panelRoot != null)
            panelRoot.gameObject.SetActive(false);
    }

    private void TriggerAnimators()
    {
        if (animators == null)
            return;

        for (int i = 0; i < animators.Length; i++)
        {
            Animator animator = animators[i];
            if (animator == null)
                continue;

            if (!string.IsNullOrWhiteSpace(revealTriggerName))
                animator.SetTrigger(revealTriggerName);

            if (setRevealedBool && !string.IsNullOrWhiteSpace(revealedBoolName))
                animator.SetBool(revealedBoolName, true);
        }
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

    private void SetGameObjectState(GameObject[] targets, bool state)
    {
        if (targets == null)
            return;

        for (int i = 0; i < targets.Length; i++)
        {
            if (targets[i] != null)
                targets[i].SetActive(state);
        }
    }

    private void CacheClosedPose()
    {
        if (panelRoot == null)
            return;

        closedLocalPosition = panelRoot.localPosition;
        closedLocalRotation = panelRoot.localRotation;
    }

    private void CacheRendererColors()
    {
        if (fadeRenderers == null)
        {
            originalRendererColors = System.Array.Empty<Color>();
            return;
        }

        originalRendererColors = new Color[fadeRenderers.Length];
        for (int i = 0; i < fadeRenderers.Length; i++)
        {
            Renderer renderer = fadeRenderers[i];
            if (renderer == null || renderer.sharedMaterial == null)
                continue;

            Material sharedMaterial = renderer.sharedMaterial;
            if (sharedMaterial.HasProperty(BaseColorId))
                originalRendererColors[i] = sharedMaterial.GetColor(BaseColorId);
            else if (sharedMaterial.HasProperty(ColorId))
                originalRendererColors[i] = sharedMaterial.GetColor(ColorId);
            else
                originalRendererColors[i] = Color.white;
        }
    }

    private void AutoAssign()
    {
        if (panelRoot == null)
            panelRoot = transform;

        if (fadeRenderers == null || fadeRenderers.Length == 0)
            fadeRenderers = panelRoot.GetComponentsInChildren<Renderer>(true);

        if (collidersToDisable == null || collidersToDisable.Length == 0)
            collidersToDisable = panelRoot.GetComponentsInChildren<Collider>(true);
    }

    private void PlayClip(AudioClip clip)
    {
        if (audioSource != null && clip != null)
            audioSource.PlayOneShot(clip);
    }
}
