using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Optional helper component for quick visual validation of the puzzle in-scene.
/// </summary>
[DisallowMultipleComponent]
public class ChurchDialPuzzleDebugGizmos : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ChurchDialPuzzleManager puzzleManager;
    [SerializeField] private ChurchDialPuzzleInteractor puzzleInteractor;
    [SerializeField] private Transform interactionPoint;
    [SerializeField] private Transform cameraFocusPoint;

    [Header("Display")]
    [SerializeField] private bool drawWhenNotSelected = true;
    [SerializeField] private bool showStepLabels = true;
    [SerializeField] private float interactionRadius = 0.35f;
    [SerializeField] private Color idleColor = new Color(0.95f, 0.75f, 0.2f, 1f);
    [SerializeField] private Color activeColor = new Color(1f, 0.45f, 0.12f, 1f);
    [SerializeField] private Color solvedColor = new Color(0.35f, 1f, 0.55f, 1f);
    [SerializeField] private Color selectedDialColor = new Color(0.25f, 0.85f, 1f, 1f);

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

    public void SetReferences(ChurchDialPuzzleManager manager, ChurchDialPuzzleInteractor interactor, Transform point)
    {
        puzzleManager = manager;
        puzzleInteractor = interactor;
        interactionPoint = point;
        cameraFocusPoint = manager != null ? manager.CameraFocusPoint : null;
    }

    private void AutoAssign()
    {
        if (puzzleManager == null)
            puzzleManager = GetComponent<ChurchDialPuzzleManager>() ?? GetComponentInChildren<ChurchDialPuzzleManager>(true);

        if (puzzleInteractor == null)
            puzzleInteractor = GetComponent<ChurchDialPuzzleInteractor>() ?? GetComponentInChildren<ChurchDialPuzzleInteractor>(true);

        if (interactionPoint == null)
            interactionPoint = puzzleInteractor != null ? puzzleInteractor.InteractionPoint : transform;

        if (cameraFocusPoint == null && puzzleManager != null)
            cameraFocusPoint = puzzleManager.CameraFocusPoint;
    }

    private void DrawGizmosInternal()
    {
        AutoAssign();

        Color stateColor = idleColor;
        if (puzzleManager != null && puzzleManager.IsSolved)
            stateColor = solvedColor;
        else if (puzzleManager != null && puzzleManager.IsActive)
            stateColor = activeColor;

        Gizmos.color = stateColor;
        Gizmos.DrawWireCube(transform.position, new Vector3(0.35f, 0.35f, 0.35f));

        Transform point = interactionPoint != null ? interactionPoint : transform;
        float radius = puzzleInteractor != null ? puzzleInteractor.InteractionRadius : interactionRadius;

        BoxCollider boxCollider = point != null ? point.GetComponent<BoxCollider>() : null;
        if (boxCollider != null && boxCollider.enabled)
        {
            Matrix4x4 oldMatrix = Gizmos.matrix;
            Gizmos.color = stateColor;
            Gizmos.matrix = point.localToWorldMatrix;
            Gizmos.DrawWireCube(boxCollider.center, boxCollider.size);
            Gizmos.matrix = oldMatrix;
        }
        else if (point != null)
        {
            Gizmos.color = stateColor;
            Gizmos.DrawWireSphere(point.position, radius);
        }

        if (point != null)
            Gizmos.DrawLine(transform.position, point.position);

        if (cameraFocusPoint != null)
        {
            Gizmos.color = new Color(0.55f, 0.85f, 1f, 1f);
            Gizmos.DrawWireSphere(cameraFocusPoint.position, 0.16f);
            Gizmos.DrawLine(transform.position, cameraFocusPoint.position);
        }

        if (puzzleManager == null)
            return;

        DrawDialMarker(puzzleManager.Dial1, "1");
        DrawDialMarker(puzzleManager.Dial2, "2");
        DrawDialMarker(puzzleManager.Dial3, "3");

        if (puzzleManager.SelectedDial != null)
        {
            Gizmos.color = selectedDialColor;
            Gizmos.DrawWireSphere(puzzleManager.SelectedDial.transform.position, 0.24f);
        }
    }

    private void DrawDialMarker(ChurchDialPuzzleDial dial, string label)
    {
        if (dial == null)
            return;

        Gizmos.color = Color.white;
        Gizmos.DrawWireSphere(dial.transform.position, 0.12f);

#if UNITY_EDITOR
        if (showStepLabels)
            Handles.Label(dial.transform.position + (Vector3.up * 0.18f), $"{label}: {dial.CurrentStep}/{dial.TotalSteps}");
#endif
    }
}
