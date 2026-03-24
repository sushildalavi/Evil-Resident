using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Self-contained screen-space HUD for the Sacred Glyph puzzle.
///
/// • Shows a proximity hint ("Press E to Examine") at the bottom of the screen
///   whenever the player is within range of the painting, without requiring the
///   FPS controller's promptText field to be manually wired.
///
/// • Shows a controls overlay on the right side of the screen while the player
///   is in puzzle mode (1/2/3 select dial, A/D rotate, Esc exit).
///
/// The auto-setup editor adds this component to the puzzle root and populates
/// the two serialized references automatically.
/// </summary>
[DisallowMultipleComponent]
public class PuzzleHintOverlay : MonoBehaviour
{
    [SerializeField] private SacredGlyphPuzzleInteractor puzzleInteractor;
    [SerializeField] private Transform paintingInteractableTransform;
    [SerializeField, Min(1f)] private float proximityDistance = 7f;

    private RohitFPSController cachedPlayer;
    private bool wasInPuzzleMode;

    private GameObject hintPanel;
    private Text hintText;
    private GameObject controlsPanel;
    private Text controlsText;

    // -----------------------------------------------------------------------
    //  Unity lifecycle
    // -----------------------------------------------------------------------

    void Start()
    {
        cachedPlayer = FindFirstObjectByType<RohitFPSController>();
        BuildCanvas();
    }

    void Update()
    {
        // Re-find player if scene was loaded after Start
        if (cachedPlayer == null)
            cachedPlayer = FindFirstObjectByType<RohitFPSController>();

        bool inPuzzle = puzzleInteractor != null && puzzleInteractor.IsInPuzzleMode;

        // Toggle controls panel on state change
        if (inPuzzle != wasInPuzzleMode)
        {
            wasInPuzzleMode = inPuzzle;
            if (controlsPanel != null)
                controlsPanel.SetActive(inPuzzle);
            if (inPuzzle)
                SetHint(null); // hide proximity hint while in puzzle
        }

        if (inPuzzle)
            UpdatePuzzleReadout();

        if (!inPuzzle)
            UpdateProximityHint();
    }

    // -----------------------------------------------------------------------
    //  Public setters (called by the auto-setup editor)
    // -----------------------------------------------------------------------

    public void SetPuzzleInteractor(SacredGlyphPuzzleInteractor interactor)
    {
        puzzleInteractor = interactor;
    }

    public void SetPaintingInteractable(Transform painting)
    {
        paintingInteractableTransform = painting;
    }

    // -----------------------------------------------------------------------
    //  Proximity hint logic
    // -----------------------------------------------------------------------

    private void UpdateProximityHint()
    {
        if (cachedPlayer == null || paintingInteractableTransform == null)
        {
            SetHint(null);
            return;
        }

        float dist = Vector3.Distance(
            cachedPlayer.transform.position,
            paintingInteractableTransform.position);

        if (dist > proximityDistance)
        {
            SetHint(null);
            return;
        }

        PaintingFlipReveal reveal =
            paintingInteractableTransform.GetComponent<PaintingFlipReveal>();

        string prompt = reveal != null
            ? reveal.GetPrompt(cachedPlayer)
            : "[E]  Examine";

        SetHint(prompt);
    }

    private void SetHint(string text)
    {
        if (hintPanel == null || hintText == null)
            return;

        bool show = !string.IsNullOrEmpty(text);
        if (hintPanel.activeSelf != show)
            hintPanel.SetActive(show);

        if (show)
            hintText.text = text;
    }

    // -----------------------------------------------------------------------
    //  Canvas construction
    // -----------------------------------------------------------------------

    private void BuildCanvas()
    {
        GameObject canvasGO = new GameObject("PuzzleHintCanvas");
        canvasGO.transform.SetParent(transform, false);

        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;

        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        canvasGO.AddComponent<GraphicRaycaster>();

        Font font = GetBuiltinFont();

        // ---- Proximity hint panel — bottom-centre ----
        hintPanel = CreatePanel(canvasGO.transform, "HintPanel",
            anchorCenter: new Vector2(0.5f, 0.07f),
            size: new Vector2(600f, 58f));

        hintText = CreateLabel(hintPanel.transform, "HintText",
            content: "",
            fontSize: 26,
            color: new Color(1f, 0.92f, 0.55f),
            alignment: TextAnchor.MiddleCenter,
            font: font);

        hintPanel.SetActive(false);

        // ---- Puzzle controls panel — right side ----
        controlsPanel = CreatePanel(canvasGO.transform, "ControlsPanel",
            anchorCenter: new Vector2(0.83f, 0.5f),
            size: new Vector2(360f, 270f));

        controlsText = CreateLabel(controlsPanel.transform, "ControlsText",
            content: string.Empty,
            fontSize: 20,
            color: Color.white,
            alignment: TextAnchor.MiddleLeft,
            font: font);
        controlsText.supportRichText = true;

        controlsPanel.SetActive(false);
    }

    private void UpdatePuzzleReadout()
    {
        if (controlsText == null)
            return;

        SacredGlyphPuzzleManager manager = puzzleInteractor != null ? puzzleInteractor.PuzzleManager : null;
        if (manager == null || !manager.HasAllDialReferences)
        {
            controlsText.text =
                "<b>Sacred Glyph Puzzle</b>\n\n" +
                "<color=#FF8A8A>Missing dial references.</color>";
            return;
        }

        string selectedDial = $"Dial {puzzleInteractor.SelectedDialIndex + 1}";
        string goal = $"{manager.CorrectDial1Step}  {manager.CorrectDial2Step}  {manager.CorrectDial3Step}";
        string current = $"{manager.Dial1.CurrentStep}  {manager.Dial2.CurrentStep}  {manager.Dial3.CurrentStep}";

        string stateLine = manager.IsSolved
            ? "<color=#86FFB2>Solved. Vault open.</color>"
            : "<color=#FFEB99>Match the current values to the goal.</color>";

        controlsText.text =
            "<b>Sacred Glyph Puzzle</b>\n\n" +
            $"Goal:     <color=#FFEB99>{goal}</color>\n" +
            $"Current:  <color=#FFFFFF>{current}</color>\n" +
            $"Selected: <color=#9FD6FF>{selectedDial}</color>\n\n" +
            stateLine + "\n\n" +
            "<color=#FFEB99>[1] [2] [3]</color>  Select Dial\n" +
            "<color=#FFEB99>[ A ] / [ ← ]</color>  Rotate Left\n" +
            "<color=#FFEB99>[ D ] / [ → ]</color>  Rotate Right\n" +
            "<color=#FFEB99>[ Esc ]</color>  Exit Puzzle";
    }

    private static GameObject CreatePanel(Transform parent, string name,
        Vector2 anchorCenter, Vector2 size)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);

        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchorCenter;
        rt.anchorMax = anchorCenter;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = size;

        Image img = go.AddComponent<Image>();
        img.color = new Color(0f, 0f, 0f, 0.68f);

        return go;
    }

    private static Text CreateLabel(Transform parent, string name,
        string content, int fontSize, Color color, TextAnchor alignment, Font font)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);

        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(14f, 8f);
        rt.offsetMax = new Vector2(-14f, -8f);

        Text t = go.AddComponent<Text>();
        t.text = content;
        t.fontSize = fontSize;
        t.color = color;
        t.alignment = alignment;
        t.supportRichText = true;
        t.font = font;
        return t;
    }

    private static Font GetBuiltinFont()
    {
        Font f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (f != null) return f;
        return Resources.GetBuiltinResource<Font>("Arial.ttf");
    }
}
