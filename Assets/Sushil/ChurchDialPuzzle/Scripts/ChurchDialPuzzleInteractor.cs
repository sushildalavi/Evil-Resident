using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
[DisallowMultipleComponent]
public class ChurchDialPuzzleInteractor : MonoBehaviour, IInteractable
{
    [Header("References")]
    [SerializeField] private ChurchDialPuzzleManager puzzleManager;
    [SerializeField] private PaintingPuzzleReveal paintingReveal;
    [SerializeField] private Transform interactionPoint;

    [Header("Prompt")]
    [SerializeField] private string promptApproach = "Press E to Examine";
    [SerializeField] private string promptReveal = "Press E to Examine Painting";
    [SerializeField] private string promptActive = "Esc - Step Back";
    [SerializeField] private string promptSolved = "Mechanism solved";
    [SerializeField] private string promptIncomplete = "Puzzle wiring incomplete";
    [SerializeField] private bool allowInteractionAfterSolve;
    [SerializeField] private bool autoEnterPuzzleAfterReveal = true;

    [Header("Debug")]
    [Min(0.05f)]
    [SerializeField] private float interactionRadius = 0.35f;

    [Header("Interact Key")]
    [SerializeField] private KeyCode interactKey = KeyCode.E;

    public ChurchDialPuzzleManager PuzzleManager => puzzleManager;
    public PaintingPuzzleReveal PaintingReveal => paintingReveal;
    public Transform InteractionPoint => interactionPoint != null ? interactionPoint : transform;
    public float InteractionRadius => interactionRadius;

    void Reset()
    {
        AssignDefaults();
    }

    void OnValidate()
    {
        AssignDefaults();
        interactionRadius = Mathf.Max(0.05f, interactionRadius);
    }

    public KeyCode GetInteractKey() => interactKey;

    public string GetPrompt(RohitFPSController player)
    {
        if (puzzleManager == null)
            return promptIncomplete;

        if (!puzzleManager.AllDialsAssigned)
            return promptIncomplete;

        if (puzzleManager.IsActive)
            return promptActive;

        if (puzzleManager.IsSolved && !allowInteractionAfterSolve)
            return promptSolved;

        if (paintingReveal != null && !paintingReveal.IsRevealed)
        {
            if (paintingReveal.IsRevealing)
                return "Inspecting...";

            return promptReveal;
        }

        return promptApproach;
    }

    public void Interact(RohitFPSController player)
    {
        if (puzzleManager == null || !puzzleManager.AllDialsAssigned)
            return;

        if (puzzleManager.IsActive)
            return;

        if (puzzleManager.IsSolved && !allowInteractionAfterSolve)
            return;

        if (paintingReveal != null && !paintingReveal.IsRevealed)
        {
            paintingReveal.BeginReveal(player, autoEnterPuzzleAfterReveal ? puzzleManager : null);
            return;
        }

        puzzleManager.EnterPuzzle(player);
    }

    public void SetPuzzleManager(ChurchDialPuzzleManager manager)
    {
        puzzleManager = manager;
    }

    public void SetPaintingReveal(PaintingPuzzleReveal reveal)
    {
        paintingReveal = reveal;
    }

    public void SetInteractionPoint(Transform point)
    {
        interactionPoint = point;
    }

    private void AssignDefaults()
    {
        if (puzzleManager == null)
        {
            puzzleManager = GetComponentInParent<ChurchDialPuzzleManager>();
            if (puzzleManager == null && transform.parent != null)
                puzzleManager = transform.parent.GetComponentInChildren<ChurchDialPuzzleManager>(true);
        }

        if (paintingReveal == null)
        {
            paintingReveal = GetComponent<PaintingPuzzleReveal>() ?? GetComponentInParent<PaintingPuzzleReveal>(true);
            if (paintingReveal == null && transform.parent != null)
                paintingReveal = transform.parent.GetComponentInChildren<PaintingPuzzleReveal>(true);
        }

        if (interactionPoint == null)
            interactionPoint = transform;

        Collider collider = GetComponent<Collider>();
        if (collider != null)
            collider.isTrigger = true;
    }
}
