using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[DisallowMultipleComponent]
public class SacredGlyphPuzzleDebugGizmos : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private SacredGlyphPuzzleManager puzzleManager;
    [SerializeField] private SacredGlyphPuzzleInteractor puzzleInteractor;
    [SerializeField] private Transform interactionPoint;
    [SerializeField] private Transform cameraFocusPoint;

    [Header("Display")]
    [SerializeField] private bool drawWhenNotSelected;
    [SerializeField] private bool showInteractionRadius;
    [SerializeField] private bool showStepLabels = true;
    [SerializeField, Min(0.05f)] private float interactionRadius = 0.35f;
    [SerializeField] private Color idleColor = new Color(0.84f, 0.64f, 0.25f, 1f);
    [SerializeField] private Color activeColor = new Color(1f, 0.34f, 0.12f, 1f);
    [SerializeField] private Color solvedColor = new Color(0.42f, 1f, 0.64f, 1f);
    [SerializeField] private Color selectedDialColor = new Color(0.25f, 0.86f, 1f, 1f);

    void Reset()
    {
        AutoAssign();
    }

    void OnValidate()
    {
        AutoAssign();
        interactionRadius = Mathf.Max(0.05f, interactionRadius);
    }

    void OnDrawGizmos()
    {
        if (!drawWhenNotSelected)
            return;

        DrawGizmosInternal();
    }

    void OnDrawGizmosSelected()
    {
        DrawGizmosInternal();
    }

    private void AutoAssign()
    {
        if (puzzleManager == null)
            puzzleManager = GetComponent<SacredGlyphPuzzleManager>() ?? GetComponentInChildren<SacredGlyphPuzzleManager>(true);

        if (puzzleInteractor == null)
            puzzleInteractor = GetComponent<SacredGlyphPuzzleInteractor>() ?? GetComponentInChildren<SacredGlyphPuzzleInteractor>(true);

        if (interactionPoint == null && puzzleInteractor != null)
            interactionPoint = puzzleInteractor.InteractionPoint;

        if (cameraFocusPoint == null && puzzleInteractor != null)
            cameraFocusPoint = puzzleInteractor.CameraFocusPoint;
    }

    private void DrawGizmosInternal()
    {
        AutoAssign();

        Color stateColor = idleColor;
        if (puzzleManager != null && puzzleManager.IsSolved)
            stateColor = solvedColor;
        else if (puzzleInteractor != null && puzzleInteractor.IsInPuzzleMode)
            stateColor = activeColor;

        Gizmos.color = stateColor;
        Gizmos.DrawWireCube(transform.position, Vector3.one * 0.3f);

        Transform point = interactionPoint != null ? interactionPoint : transform;
        if (showInteractionRadius && point != null)
        {
            Gizmos.DrawWireSphere(point.position, interactionRadius);
            Gizmos.DrawLine(transform.position, point.position);
        }

        if (cameraFocusPoint != null)
        {
            Gizmos.color = new Color(0.6f, 0.85f, 1f, 1f);
            Gizmos.DrawWireSphere(cameraFocusPoint.position, 0.16f);
            Gizmos.DrawLine(transform.position, cameraFocusPoint.position);
        }

        if (puzzleManager == null)
            return;

        for (int i = 0; i < 3; i++)
        {
            SacredGlyphDial dial = puzzleManager.GetDial(i);
            if (dial == null)
                continue;

            Gizmos.color = i == puzzleInteractor?.SelectedDialIndex && puzzleInteractor.IsInPuzzleMode
                ? selectedDialColor
                : Color.white;
            Gizmos.DrawWireSphere(dial.transform.position, 0.14f);

#if UNITY_EDITOR
            if (showStepLabels)
                Handles.Label(dial.transform.position + (Vector3.up * 0.18f), $"Dial {i + 1}: {dial.CurrentStep}/{dial.TotalSteps}");
#endif
        }
    }
}
