using UnityEngine;

[DisallowMultipleComponent]
public class DialSelectionHighlighter : MonoBehaviour
{
    [Header("Targets")]
    [SerializeField] private Renderer[] targetRenderers;
    [SerializeField] private GameObject[] highlightObjects;
    [SerializeField] private Transform scaleTarget;

    [Header("Colors")]
    [SerializeField] private Color idleColor = new Color(0.55f, 0.47f, 0.34f, 1f);
    [SerializeField] private Color selectedColor = new Color(1f, 0.77f, 0.24f, 1f);
    [SerializeField] private Color solvedColor = new Color(0.40f, 1f, 0.65f, 1f);
    [SerializeField] private Color lockedColor = new Color(0.27f, 0.25f, 0.23f, 1f);

    [Header("Emission")]
    [SerializeField] private bool driveEmission = true;
    [SerializeField, Min(0f)] private float idleEmission = 0f;
    [SerializeField, Min(0f)] private float selectedEmission = 0.75f;
    [SerializeField, Min(0f)] private float solvedEmission = 1.3f;
    [SerializeField, Min(0f)] private float lockedEmission = 0.05f;

    [Header("Scale Pulse")]
    [SerializeField] private bool pulseWhileSelected = true;
    [SerializeField] private float pulseAmplitude = 0.025f;
    [SerializeField] private float pulseSpeed = 3.2f;

    private static readonly string[] ColorProperties = { "_BaseColor", "_Color", "_EmissionColor" };

    private MaterialPropertyBlock propertyBlock;
    private Vector3 baseScale = Vector3.one;
    private bool isSelected;
    private bool isSolved;
    private bool isInteractionLocked;

    void Reset()
    {
        AutoAssign();
        ApplyVisuals();
    }

    void Awake()
    {
        CacheState();
        ApplyVisuals();
    }

    void OnValidate()
    {
        AutoAssign();
        pulseAmplitude = Mathf.Max(0f, pulseAmplitude);
        pulseSpeed = Mathf.Max(0f, pulseSpeed);
        ApplyVisuals();
    }

    void Update()
    {
        if (scaleTarget == null)
            return;

        if (isSelected && !isSolved && !isInteractionLocked && pulseWhileSelected)
        {
            float pulse = 1f + (Mathf.Sin(Time.unscaledTime * pulseSpeed) * pulseAmplitude);
            scaleTarget.localScale = baseScale * pulse;
            return;
        }

        scaleTarget.localScale = baseScale;
    }

    void OnDisable()
    {
        if (scaleTarget != null)
            scaleTarget.localScale = baseScale;
    }

    public void SetSelected(bool selected)
    {
        isSelected = selected;
        ApplyVisuals();
    }

    public void SetSolved(bool solved)
    {
        isSolved = solved;
        ApplyVisuals();
    }

    public void SetInteractionLocked(bool locked)
    {
        isInteractionLocked = locked;
        ApplyVisuals();
    }

    public void SetHighlighted(bool highlighted)
    {
        SetSelected(highlighted);
    }

    public void ConfigureTargets(Renderer[] renderers, Transform targetScale)
    {
        targetRenderers = renderers;
        scaleTarget = targetScale;
        CacheState();
        ApplyVisuals();
    }

    private void AutoAssign()
    {
        if (scaleTarget == null)
            scaleTarget = transform;

        if (targetRenderers == null || targetRenderers.Length == 0)
            targetRenderers = GetComponentsInChildren<Renderer>(true);

        CacheState();
    }

    private void CacheState()
    {
        if (propertyBlock == null)
            propertyBlock = new MaterialPropertyBlock();

        if (scaleTarget != null)
            baseScale = scaleTarget.localScale;
    }

    private void ApplyVisuals()
    {
        if (propertyBlock == null)
            propertyBlock = new MaterialPropertyBlock();

        float emissionIntensity = isSolved
            ? solvedEmission
            : (isInteractionLocked ? lockedEmission : (isSelected ? selectedEmission : idleEmission));

        Color targetColor = isSolved
            ? solvedColor
            : (isInteractionLocked ? lockedColor : (isSelected ? selectedColor : idleColor));

        if (targetRenderers == null)
        {
            SetHighlightObjectsActive(isSolved || (isSelected && !isInteractionLocked));
            return;
        }

        for (int i = 0; i < targetRenderers.Length; i++)
        {
            Renderer renderer = targetRenderers[i];
            if (renderer == null)
                continue;

            Material sharedMaterial = renderer.sharedMaterial;
            if (sharedMaterial == null)
                continue;

            renderer.GetPropertyBlock(propertyBlock);
            for (int p = 0; p < ColorProperties.Length; p++)
            {
                string propertyName = ColorProperties[p];
                if (!sharedMaterial.HasProperty(propertyName))
                    continue;

                if (propertyName == "_EmissionColor")
                {
                    if (!driveEmission)
                        continue;

                    propertyBlock.SetColor(propertyName, targetColor * emissionIntensity);
                }
                else
                {
                    propertyBlock.SetColor(propertyName, targetColor);
                }
            }

            renderer.SetPropertyBlock(propertyBlock);
        }

        SetHighlightObjectsActive(isSolved || (isSelected && !isInteractionLocked));
    }

    private void SetHighlightObjectsActive(bool activeState)
    {
        if (highlightObjects == null)
            return;

        for (int i = 0; i < highlightObjects.Length; i++)
        {
            if (highlightObjects[i] != null)
                highlightObjects[i].SetActive(activeState);
        }
    }
}
