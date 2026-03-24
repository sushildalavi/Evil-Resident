using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;

[CustomEditor(typeof(SacredGlyphPuzzleManager))]
public class SacredGlyphPuzzleAutoSetupEditor : Editor
{
    private const string RootName = "ChurchPuzzleSequenceRoot";
    private const string PaintingInteractableName = "PaintingInteractable";
    private const string PaintingVisualName = "PaintingVisual";
    private const string PuzzleWallPanelName = "PuzzleWallPanel";
    private const string Dial1PivotName = "Dial1Pivot";
    private const string Dial2PivotName = "Dial2Pivot";
    private const string Dial3PivotName = "Dial3Pivot";
    private const string Dial1MeshName = "Dial1Mesh";
    private const string Dial2MeshName = "Dial2Mesh";
    private const string Dial3MeshName = "Dial3Mesh";
    private const string CenterLockName = "CenterLock";
    private const string InteractionPointName = "InteractionPoint";
    private const string PuzzleManagerName = "PuzzleManager";
    private const string CameraFocusPointName = "CameraFocusPoint";
    private const string VaultDoorRootName = "VaultDoorRoot";
    private const string VaultDoorLeftName = "VaultDoorLeft";
    private const string VaultDoorRightName = "VaultDoorRight";
    private const string VaultSingleDoorName = "VaultSingleDoor";
    private const string VaultSlidingPanelName = "VaultSlidingPanel";
    private const string VaultInteriorName = "VaultInterior";

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        DrawDefaultInspector();
        serializedObject.ApplyModifiedProperties();

        EditorGUILayout.Space();

        SacredGlyphPuzzleManager manager = (SacredGlyphPuzzleManager)target;
        EditorGUILayout.HelpBox(BuildValidationReport(manager), MessageType.Info);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Editor Actions", EditorStyles.boldLabel);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Print Current Steps"))
                PrintCurrentStepValues(manager);

            if (GUILayout.Button("Try Check Solved"))
                manager.TryCheckSolved();
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Randomize Dials"))
                RandomizeSteps(manager);

            if (GUILayout.Button("Set All To Zero"))
                SetAllToZero(manager);
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Auto-Setup Root"))
                Run(manager.transform.root.gameObject, false);

            if (GUILayout.Button("Rebuild Visuals"))
                RebuildVisuals(manager.transform.root.gameObject, false);
        }

        if (GUILayout.Button("Solve Puzzle Instantly"))
            SolveInstantly(manager);
    }

    [MenuItem("Tools/Sacred Glyph Puzzle/Create Basic Sequence Root")]
    private static void CreateBasicSequenceRoot()
    {
        GameObject root = new GameObject(RootName);
        Undo.RegisterCreatedObjectUndo(root, "Create Sacred Glyph Puzzle Root");
        Run(root, false);
        Selection.activeGameObject = root;
    }

    [MenuItem("Tools/Sacred Glyph Puzzle/Auto-Setup Selected Root")]
    private static void AutoSetupSelectedRoot()
    {
        if (Selection.activeGameObject == null)
        {
            EditorUtility.DisplayDialog("Sacred Glyph Puzzle", "Select the puzzle root or any child under it first.", "OK");
            return;
        }

        Run(Selection.activeGameObject, true);
    }

    [MenuItem("Tools/Sacred Glyph Puzzle/Auto-Setup Selected Root", true)]
    private static bool ValidateAutoSetupSelectedRoot()
    {
        return Selection.activeGameObject != null;
    }

    [MenuItem("Tools/Sacred Glyph Puzzle/Rebuild Visuals For Selected Root")]
    private static void RebuildVisualsForSelectedRoot()
    {
        if (Selection.activeGameObject == null)
        {
            EditorUtility.DisplayDialog("Sacred Glyph Puzzle", "Select the puzzle root or any child under it first.", "OK");
            return;
        }

        RebuildVisuals(Selection.activeGameObject, true);
    }

    [MenuItem("Tools/Sacred Glyph Puzzle/Rebuild Visuals For Selected Root", true)]
    private static bool ValidateRebuildVisualsForSelectedRoot()
    {
        return Selection.activeGameObject != null;
    }

    public static void Run(GameObject rootObject, bool showDialog)
    {
        if (rootObject == null)
            return;

        Transform root = ResolveRoot(rootObject.transform);
        if (root.parent == null)
            root.name = RootName;

        Undo.RegisterFullObjectHierarchyUndo(root.gameObject, "Auto-Setup Sacred Glyph Puzzle");

        Transform paintingInteractable = FindOrCreateChild(root, PaintingInteractableName);
        Transform paintingVisual = FindOrCreateChild(root, PaintingVisualName);
        Transform puzzleWallPanel = FindOrCreateChild(root, PuzzleWallPanelName);
        Transform dial1Pivot = FindOrCreateChild(root, Dial1PivotName);
        Transform dial2Pivot = FindOrCreateChild(root, Dial2PivotName);
        Transform dial3Pivot = FindOrCreateChild(root, Dial3PivotName);
        Transform centerLock = FindOrCreateChild(root, CenterLockName);
        Transform interactionPoint = FindOrCreateChild(root, InteractionPointName);
        Transform puzzleManagerObject = FindOrCreateChild(root, PuzzleManagerName);
        Transform cameraFocusPoint = FindOrCreateChild(root, CameraFocusPointName);
        Transform vaultDoorRoot = FindOrCreateChild(root, VaultDoorRootName);
        Transform vaultDoorLeft = FindOrCreateChild(vaultDoorRoot, VaultDoorLeftName);
        Transform vaultDoorRight = FindOrCreateChild(vaultDoorRoot, VaultDoorRightName);
        Transform vaultSingleDoor = FindOrCreateChild(vaultDoorRoot, VaultSingleDoorName);
        Transform vaultSlidingPanel = FindOrCreateChild(vaultDoorRoot, VaultSlidingPanelName);
        Transform vaultInterior = FindOrCreateChild(vaultDoorRoot, VaultInteriorName);

        Transform dial1Mesh = FindOrCreateChild(dial1Pivot, Dial1MeshName);
        Transform dial2Mesh = FindOrCreateChild(dial2Pivot, Dial2MeshName);
        Transform dial3Mesh = FindOrCreateChild(dial3Pivot, Dial3MeshName);

        PositionDefaultObjects(
            paintingInteractable,
            paintingVisual,
            puzzleWallPanel,
            dial1Pivot,
            dial2Pivot,
            dial3Pivot,
            centerLock,
            interactionPoint,
            cameraFocusPoint,
            vaultDoorRoot,
            vaultDoorLeft,
            vaultDoorRight,
            vaultSingleDoor,
            vaultSlidingPanel,
            vaultInterior);

        EnsurePaintingCollider(paintingInteractable.gameObject);

        SacredGlyphDial dial1 = GetOrAdd<SacredGlyphDial>(dial1Pivot.gameObject);
        SacredGlyphDial dial2 = GetOrAdd<SacredGlyphDial>(dial2Pivot.gameObject);
        SacredGlyphDial dial3 = GetOrAdd<SacredGlyphDial>(dial3Pivot.gameObject);
        DialSelectionHighlighter highlighter1 = GetOrAdd<DialSelectionHighlighter>(dial1Pivot.gameObject);
        DialSelectionHighlighter highlighter2 = GetOrAdd<DialSelectionHighlighter>(dial2Pivot.gameObject);
        DialSelectionHighlighter highlighter3 = GetOrAdd<DialSelectionHighlighter>(dial3Pivot.gameObject);
        SacredGlyphPuzzleManager manager = GetOrAdd<SacredGlyphPuzzleManager>(puzzleManagerObject.gameObject);
        SacredGlyphPuzzleInteractor interactor = GetOrAdd<SacredGlyphPuzzleInteractor>(root.gameObject);
        PaintingFlipReveal reveal = GetOrAdd<PaintingFlipReveal>(paintingInteractable.gameObject);
        VaultDoorRevealController vault = GetOrAdd<VaultDoorRevealController>(vaultDoorRoot.gameObject);
        PuzzleSolvedReceiver receiver = GetOrAdd<PuzzleSolvedReceiver>(root.gameObject);
        PuzzleHintOverlay hintOverlay = GetOrAdd<PuzzleHintOverlay>(root.gameObject);
        GetOrAdd<SacredGlyphPuzzleDebugGizmos>(root.gameObject);

        manager.SetDialReferences(dial1, dial2, dial3);
        interactor.SetPuzzleManager(manager);
        interactor.SetPaintingReveal(reveal);
        interactor.SetInteractionPoint(interactionPoint);
        interactor.SetCameraFocusPoint(cameraFocusPoint);

        reveal.SetRevealTarget(paintingVisual);
        reveal.SetPuzzleInteractor(interactor);

        vault.SetDoubleDoors(vaultDoorLeft, vaultDoorRight);
        vault.SetSingleDoor(vaultSingleDoor);
        vault.SetSlidingPanel(vaultSlidingPanel);
        vault.SetActivateOnOpen(new GameObject[] { vaultInterior.gameObject });

        receiver.SetVaultDoorReveal(vault);

        hintOverlay.SetPuzzleInteractor(interactor);
        hintOverlay.SetPaintingInteractable(paintingInteractable);

        RebuildVisuals(root.gameObject, false);

        highlighter1.ConfigureTargets(dial1Mesh.GetComponentsInChildren<Renderer>(true), dial1Pivot);
        highlighter2.ConfigureTargets(dial2Mesh.GetComponentsInChildren<Renderer>(true), dial2Pivot);
        highlighter3.ConfigureTargets(dial3Mesh.GetComponentsInChildren<Renderer>(true), dial3Pivot);

        EnsureSolvedCallbacks(manager, receiver);

        EditorUtility.SetDirty(root.gameObject);
        EditorUtility.SetDirty(manager);
        EditorUtility.SetDirty(interactor);
        EditorUtility.SetDirty(reveal);
        EditorUtility.SetDirty(vault);
        EditorUtility.SetDirty(receiver);
        EditorUtility.SetDirty(hintOverlay);
        EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
        Selection.activeGameObject = root.gameObject;

        if (showDialog)
        {
            EditorUtility.DisplayDialog(
                "Sacred Glyph Puzzle",
                "The selected root was auto-configured and default placeholder visuals were generated.",
                "OK");
        }
    }

    public static void RebuildVisuals(GameObject rootObject, bool showDialog)
    {
        if (rootObject == null)
            return;

        Transform root = ResolveRoot(rootObject.transform);
        Undo.RegisterFullObjectHierarchyUndo(root.gameObject, "Rebuild Sacred Glyph Puzzle Visuals");
        ChurchDialPuzzleDefaultVisualBuilder.RebuildDefaultVisuals(root);

        RefreshVisualReferences(root);

        EditorUtility.SetDirty(root.gameObject);
        EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
        Selection.activeGameObject = root.gameObject;

        if (showDialog)
        {
            EditorUtility.DisplayDialog(
                "Sacred Glyph Puzzle",
                "Default placeholder visuals were rebuilt for the selected puzzle root.",
                "OK");
        }
    }

    public static void PrintCurrentStepValues(SacredGlyphPuzzleManager manager)
    {
        if (manager == null)
            return;

        int dial1Step = manager.Dial1 != null ? manager.Dial1.CurrentStep : -1;
        int dial2Step = manager.Dial2 != null ? manager.Dial2.CurrentStep : -1;
        int dial3Step = manager.Dial3 != null ? manager.Dial3.CurrentStep : -1;

        StringBuilder builder = new StringBuilder();
        builder.Append("[SacredGlyphPuzzle] Current steps -> ");
        builder.Append($"Dial1:{dial1Step} ");
        builder.Append($"Dial2:{dial2Step} ");
        builder.Append($"Dial3:{dial3Step}");
        Debug.Log(builder.ToString(), manager);
    }

    public static string BuildValidationReport(SacredGlyphPuzzleManager manager)
    {
        if (manager == null)
            return "Puzzle manager reference is missing.";

        List<string> issues = new List<string>();
        Transform root = ResolveRoot(manager.transform);

        if (!manager.HasAllDialReferences)
            issues.Add("Dial1, Dial2, and Dial3 must all be assigned.");

        if (root.Find(PaintingInteractableName) == null)
            issues.Add("PaintingInteractable is missing.");

        Transform paintingVisual = root.Find(PaintingVisualName);
        if (paintingVisual == null)
            issues.Add("PaintingVisual is missing.");
        else if (paintingVisual.Find("GeneratedDefault") == null)
            issues.Add("PaintingVisual has no generated placeholder art yet.");

        Transform puzzleWallPanel = root.Find(PuzzleWallPanelName);
        if (puzzleWallPanel == null)
            issues.Add("PuzzleWallPanel is missing.");
        else if (puzzleWallPanel.Find("GeneratedDefault") == null)
            issues.Add("PuzzleWallPanel has no generated placeholder art yet.");

        if (root.GetComponentInChildren<PaintingFlipReveal>(true) == null)
            issues.Add("PaintingFlipReveal is missing.");

        if (root.GetComponentInChildren<SacredGlyphPuzzleInteractor>(true) == null)
            issues.Add("SacredGlyphPuzzleInteractor is missing.");

        if (root.GetComponentInChildren<VaultDoorRevealController>(true) == null)
            issues.Add("VaultDoorRevealController is missing.");

        PuzzleSolvedReceiver receiver = root.GetComponent<PuzzleSolvedReceiver>();
        if (receiver == null)
            issues.Add("PuzzleSolvedReceiver is missing on the root.");
        else if (!HasPersistentListener(manager.OnPuzzleSolvedEvent, receiver, nameof(PuzzleSolvedReceiver.OnPuzzleSolved)))
            issues.Add("OnPuzzleSolved is not wired to PuzzleSolvedReceiver.OnPuzzleSolved.");

        if (issues.Count == 0)
            return "Setup looks valid. If the visuals are still invisible, check that the root is in front of the camera and not inside the wall.";

        StringBuilder report = new StringBuilder();
        for (int i = 0; i < issues.Count; i++)
            report.AppendLine($"- {issues[i]}");

        return report.ToString().TrimEnd();
    }

    private static void RandomizeSteps(SacredGlyphPuzzleManager manager)
    {
        if (manager == null)
            return;

        Undo.RecordObjects(CollectUndoTargets(manager), "Randomize Sacred Glyph Dials");
        manager.RandomizeDialSteps();
        MarkTargetsDirty(manager);
    }

    private static void SetAllToZero(SacredGlyphPuzzleManager manager)
    {
        if (manager == null)
            return;

        Undo.RecordObjects(CollectUndoTargets(manager), "Set Sacred Glyph Dials To Zero");
        manager.SetAllDialsToZero();
        MarkTargetsDirty(manager);
    }

    private static void SolveInstantly(SacredGlyphPuzzleManager manager)
    {
        if (manager == null)
            return;

        Undo.RecordObjects(CollectUndoTargets(manager), "Solve Sacred Glyph Puzzle");
        manager.SolveInstantly();
        MarkTargetsDirty(manager);
    }

    private static Object[] CollectUndoTargets(SacredGlyphPuzzleManager manager)
    {
        List<Object> objects = new List<Object> { manager };
        if (manager.Dial1 != null) objects.Add(manager.Dial1);
        if (manager.Dial2 != null) objects.Add(manager.Dial2);
        if (manager.Dial3 != null) objects.Add(manager.Dial3);
        return objects.ToArray();
    }

    private static void MarkTargetsDirty(SacredGlyphPuzzleManager manager)
    {
        EditorUtility.SetDirty(manager);
        if (manager.Dial1 != null) EditorUtility.SetDirty(manager.Dial1);
        if (manager.Dial2 != null) EditorUtility.SetDirty(manager.Dial2);
        if (manager.Dial3 != null) EditorUtility.SetDirty(manager.Dial3);
        EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
    }

    public static void EnsureSolvedCallbacks(SacredGlyphPuzzleManager manager, PuzzleSolvedReceiver receiver)
    {
        if (manager == null || receiver == null || manager.OnPuzzleSolvedEvent == null)
            return;

        if (HasPersistentListener(manager.OnPuzzleSolvedEvent, receiver, nameof(PuzzleSolvedReceiver.OnPuzzleSolved)))
            return;

        UnityEventTools.AddPersistentListener(manager.OnPuzzleSolvedEvent, receiver.OnPuzzleSolved);
        EditorUtility.SetDirty(manager);
    }

    private static void EnsurePaintingCollider(GameObject paintingInteractable)
    {
        if (paintingInteractable == null)
            return;

        BoxCollider boxCollider = GetOrAdd<BoxCollider>(paintingInteractable);
        // Keep this as a solid collider so the player controller can raycast it directly when
        // the setup is correct. The runtime painting script also has a direct-input fallback now,
        // so this collider can stay simple and robust.
        boxCollider.isTrigger = false;
        boxCollider.center = new Vector3(0f, 0f, 0.08f);
        boxCollider.size = new Vector3(1.75f, 2.35f, 0.35f);
        EditorUtility.SetDirty(boxCollider);
    }

    private static void RefreshVisualReferences(Transform root)
    {
        if (root == null)
            return;

        Transform dial1Pivot = root.Find(Dial1PivotName);
        Transform dial2Pivot = root.Find(Dial2PivotName);
        Transform dial3Pivot = root.Find(Dial3PivotName);

        if (dial1Pivot != null)
        {
            DialSelectionHighlighter highlighter = dial1Pivot.GetComponent<DialSelectionHighlighter>();
            if (highlighter != null)
                highlighter.ConfigureTargets(dial1Pivot.GetComponentsInChildren<Renderer>(true), dial1Pivot);
        }

        if (dial2Pivot != null)
        {
            DialSelectionHighlighter highlighter = dial2Pivot.GetComponent<DialSelectionHighlighter>();
            if (highlighter != null)
                highlighter.ConfigureTargets(dial2Pivot.GetComponentsInChildren<Renderer>(true), dial2Pivot);
        }

        if (dial3Pivot != null)
        {
            DialSelectionHighlighter highlighter = dial3Pivot.GetComponent<DialSelectionHighlighter>();
            if (highlighter != null)
                highlighter.ConfigureTargets(dial3Pivot.GetComponentsInChildren<Renderer>(true), dial3Pivot);
        }
    }

    private static GameObject CreateChild(Transform parent, string name)
    {
        GameObject child = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(child, $"Create {name}");
        child.transform.SetParent(parent);
        child.transform.localPosition = Vector3.zero;
        child.transform.localRotation = Quaternion.identity;
        child.transform.localScale = Vector3.one;
        return child;
    }

    private static void PositionDefaultObjects(
        Transform paintingInteractable,
        Transform paintingVisual,
        Transform puzzleWallPanel,
        Transform dial1Pivot,
        Transform dial2Pivot,
        Transform dial3Pivot,
        Transform centerLock,
        Transform interactionPoint,
        Transform cameraFocusPoint,
        Transform vaultDoorRoot,
        Transform vaultDoorLeft,
        Transform vaultDoorRight,
        Transform vaultSingleDoor,
        Transform vaultSlidingPanel,
        Transform vaultInterior)
    {
        if (paintingInteractable.localPosition == Vector3.zero)
            paintingInteractable.localPosition = new Vector3(0f, 0f, 0.14f);
        else if (Mathf.Abs(paintingInteractable.localPosition.z) < 0.12f)
            paintingInteractable.localPosition = new Vector3(
                paintingInteractable.localPosition.x,
                paintingInteractable.localPosition.y,
                Mathf.Sign(paintingInteractable.localPosition.z == 0f ? 1f : paintingInteractable.localPosition.z) * 0.14f);

        if (paintingVisual.localPosition == Vector3.zero)
            paintingVisual.localPosition = Vector3.zero;

        if (puzzleWallPanel.localPosition == Vector3.zero)
            puzzleWallPanel.localPosition = Vector3.zero;

        if (dial1Pivot.localPosition == Vector3.zero)
            dial1Pivot.localPosition = new Vector3(-0.5f, 0.25f, 0.04f);

        if (dial2Pivot.localPosition == Vector3.zero)
            dial2Pivot.localPosition = new Vector3(0f, -0.1f, 0.04f);

        if (dial3Pivot.localPosition == Vector3.zero)
            dial3Pivot.localPosition = new Vector3(0.5f, 0.25f, 0.04f);

        if (centerLock.localPosition == Vector3.zero)
            centerLock.localPosition = new Vector3(0f, -0.78f, 0.07f);

        if (interactionPoint.localPosition == Vector3.zero)
            interactionPoint.localPosition = new Vector3(0f, 0f, 1f);

        if (cameraFocusPoint.localPosition == Vector3.zero)
        {
            cameraFocusPoint.localPosition = new Vector3(0f, -0.05f, 1.15f);
            cameraFocusPoint.localRotation = Quaternion.LookRotation(-Vector3.forward, Vector3.up);
        }

        if (vaultDoorRoot.localPosition == Vector3.zero)
            vaultDoorRoot.localPosition = new Vector3(0f, -0.12f, -0.18f);

        if (vaultDoorLeft.localPosition == Vector3.zero)
            vaultDoorLeft.localPosition = new Vector3(-0.28f, 0f, 0f);

        if (vaultDoorRight.localPosition == Vector3.zero)
            vaultDoorRight.localPosition = new Vector3(0.28f, 0f, 0f);

        if (vaultSingleDoor.localPosition == Vector3.zero)
            vaultSingleDoor.localPosition = Vector3.zero;

        if (vaultSlidingPanel.localPosition == Vector3.zero)
            vaultSlidingPanel.localPosition = Vector3.zero;

        if (vaultInterior.localPosition == Vector3.zero)
            vaultInterior.localPosition = new Vector3(0f, 0f, -1.05f);

        vaultInterior.gameObject.SetActive(false);
    }

    private static Transform ResolveRoot(Transform candidate)
    {
        if (candidate == null)
            return null;

        Transform current = candidate;
        while (current != null)
        {
            if (LooksLikePuzzleRoot(current))
                return current;

            current = current.parent;
        }

        return candidate;
    }

    private static bool LooksLikePuzzleRoot(Transform transform)
    {
        if (transform == null)
            return false;

        return transform.Find(PaintingInteractableName) != null ||
               transform.Find(PaintingVisualName) != null ||
               transform.Find(Dial1PivotName) != null ||
               transform.Find(Dial2PivotName) != null ||
               transform.Find(Dial3PivotName) != null ||
               transform.Find(PuzzleManagerName) != null ||
               transform.Find(VaultDoorRootName) != null;
    }

    private static Transform FindOrCreateChild(Transform parent, string name)
    {
        Transform child = parent.Find(name);
        if (child != null)
            return child;

        return CreateChild(parent, name).transform;
    }

    private static T GetOrAdd<T>(GameObject gameObject) where T : Component
    {
        T component = gameObject.GetComponent<T>();
        if (component != null)
            return component;

        return Undo.AddComponent<T>(gameObject);
    }

    private static bool HasPersistentListener(UnityEvent unityEvent, Object target, string methodName)
    {
        if (unityEvent == null || target == null || string.IsNullOrEmpty(methodName))
            return false;

        int listenerCount = unityEvent.GetPersistentEventCount();
        for (int i = 0; i < listenerCount; i++)
        {
            if (unityEvent.GetPersistentTarget(i) == target &&
                unityEvent.GetPersistentMethodName(i) == methodName)
                return true;
        }

        return false;
    }
}
