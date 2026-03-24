using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;

public static class ChurchDialPuzzleAutoSetupEditor
{
    private const string RootDefaultName = "ChurchPuzzleSequenceRoot";
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
    private const string VaultInteriorName = "VaultInterior";
    private const float LocalCenterTolerance = 0.01f;

    [MenuItem("Tools/Church Dial Puzzle/Auto-Setup Selected Root")]
    private static void AutoSetupSelectedRoot()
    {
        GameObject selected = Selection.activeGameObject;
        if (selected == null)
        {
            EditorUtility.DisplayDialog("Church Dial Puzzle", "Select the puzzle root object first.", "OK");
            return;
        }

        Run(selected, true);
    }

    [MenuItem("Tools/Church Dial Puzzle/Auto-Setup Selected Root", true)]
    private static bool ValidateAutoSetupSelectedRoot()
    {
        return Selection.activeGameObject != null;
    }

    [MenuItem("Tools/Church Dial Puzzle/Save Selected Root As Prefab")]
    private static void SaveSelectedRootAsPrefab()
    {
        if (Selection.activeGameObject == null)
            return;

        SaveRootAsPrefab(ResolveRoot(Selection.activeGameObject.transform).gameObject);
    }

    [MenuItem("Tools/Church Dial Puzzle/Save Selected Root As Prefab", true)]
    private static bool ValidateSaveSelectedRootAsPrefab()
    {
        return Selection.activeGameObject != null;
    }

    [MenuItem("Tools/Church Dial Puzzle/Rebuild Default Visuals")]
    private static void RebuildDefaultVisualsForSelectedRoot()
    {
        if (Selection.activeGameObject == null)
        {
            EditorUtility.DisplayDialog("Church Dial Puzzle", "Select the puzzle root object first.", "OK");
            return;
        }

        GameObject root = ResolveRoot(Selection.activeGameObject.transform).gameObject;
        ChurchDialPuzzleDefaultVisualBuilder.RebuildDefaultVisuals(root.transform);
        EditorSceneManager.MarkSceneDirty(root.scene);
        Selection.activeGameObject = root;
    }

    [MenuItem("Tools/Church Dial Puzzle/Rebuild Default Visuals", true)]
    private static bool ValidateRebuildDefaultVisualsForSelectedRoot()
    {
        return Selection.activeGameObject != null;
    }

    public static void Run(GameObject rootObject)
    {
        Run(rootObject, true);
    }

    public static void Run(GameObject rootObject, bool showDialog)
    {
        if (rootObject == null)
            return;

        Transform root = ResolveRoot(rootObject.transform);
        Undo.RegisterFullObjectHierarchyUndo(root.gameObject, "Auto-Setup Church Dial Puzzle");

        root.gameObject.name = string.IsNullOrWhiteSpace(root.gameObject.name) ? RootDefaultName : root.gameObject.name;

        Transform paintingInteractable = FindOrCreateChild(root, PaintingInteractableName);
        Transform paintingVisual = FindOrCreateChild(root, PaintingVisualName);
        Transform puzzleWallPanel = FindOrCreateChild(root, PuzzleWallPanelName);
        Transform dial1Pivot = FindOrCreateChild(root, Dial1PivotName);
        Transform dial2Pivot = FindOrCreateChild(root, Dial2PivotName);
        Transform dial3Pivot = FindOrCreateChild(root, Dial3PivotName);
        Transform centerLock = FindOrCreateChild(root, CenterLockName);
        Transform interactionPoint = FindOrCreateChild(root, InteractionPointName);
        Transform managerNode = FindOrCreateChild(root, PuzzleManagerName);
        Transform cameraFocusPoint = FindOrCreateChild(root, CameraFocusPointName);
        Transform vaultDoorRoot = FindOrCreateChild(root, VaultDoorRootName);
        Transform vaultDoorLeft = FindOrCreateChild(vaultDoorRoot, VaultDoorLeftName);
        Transform vaultDoorRight = FindOrCreateChild(vaultDoorRoot, VaultDoorRightName);
        Transform vaultInterior = FindOrCreateChild(vaultDoorRoot, VaultInteriorName);

        EnsurePlaceholderChild(dial1Pivot, Dial1MeshName);
        EnsurePlaceholderChild(dial2Pivot, Dial2MeshName);
        EnsurePlaceholderChild(dial3Pivot, Dial3MeshName);

        ApplyDefaultTransforms(
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
            vaultInterior);

        ChurchDialPuzzleDefaultVisualBuilder.RebuildDefaultVisuals(root);

        ChurchDialPuzzleManager manager = GetOrAdd<ChurchDialPuzzleManager>(managerNode.gameObject);
        ChurchDialPuzzleDial dial1 = GetOrAdd<ChurchDialPuzzleDial>(dial1Pivot.gameObject);
        ChurchDialPuzzleDial dial2 = GetOrAdd<ChurchDialPuzzleDial>(dial2Pivot.gameObject);
        ChurchDialPuzzleDial dial3 = GetOrAdd<ChurchDialPuzzleDial>(dial3Pivot.gameObject);

        DialSelectionHighlighter highlighter1 = GetOrAdd<DialSelectionHighlighter>(dial1Pivot.gameObject);
        DialSelectionHighlighter highlighter2 = GetOrAdd<DialSelectionHighlighter>(dial2Pivot.gameObject);
        DialSelectionHighlighter highlighter3 = GetOrAdd<DialSelectionHighlighter>(dial3Pivot.gameObject);

        PaintingPuzzleReveal reveal = GetOrAdd<PaintingPuzzleReveal>(paintingInteractable.gameObject);
        ChurchDialPuzzleInteractor interactor = GetOrAdd<ChurchDialPuzzleInteractor>(interactionPoint.gameObject);
        VaultRevealController vaultReveal = GetOrAdd<VaultRevealController>(vaultDoorRoot.gameObject);
        PuzzleSolvedReceiver solvedReceiver = GetOrAdd<PuzzleSolvedReceiver>(root.gameObject);
        ChurchDialPuzzleDebugGizmos gizmos = GetOrAdd<ChurchDialPuzzleDebugGizmos>(root.gameObject);

        highlighter1.ConfigureTargets(dial1Pivot.GetComponentsInChildren<Renderer>(true), dial1Pivot);
        highlighter2.ConfigureTargets(dial2Pivot.GetComponentsInChildren<Renderer>(true), dial2Pivot);
        highlighter3.ConfigureTargets(dial3Pivot.GetComponentsInChildren<Renderer>(true), dial3Pivot);

        reveal.SetPanelRoot(paintingVisual);
        reveal.SetCollidersToDisable(paintingVisual.GetComponentsInChildren<Collider>(true));

        manager.SetDialReferences(dial1, dial2, dial3);
        manager.SetCameraFocusPoint(cameraFocusPoint);
        manager.SetCenterLockRenderer(centerLock.GetComponentInChildren<Renderer>(true));

        interactor.SetPuzzleManager(manager);
        interactor.SetPaintingReveal(reveal);
        interactor.SetInteractionPoint(interactionPoint);

        vaultReveal.SetDoubleDoors(vaultDoorLeft, vaultDoorRight);
        vaultReveal.SetVaultInterior(vaultInterior.gameObject);

        solvedReceiver.SetVaultReveal(vaultReveal);

        gizmos.SetReferences(manager, interactor, interactionPoint);

        EnsureInteractionCollider(interactionPoint.gameObject);
        EnsureSolvedCallbacks(manager, solvedReceiver);

        EditorUtility.SetDirty(root.gameObject);
        EditorUtility.SetDirty(managerNode.gameObject);
        EditorUtility.SetDirty(manager);
        EditorUtility.SetDirty(dial1);
        EditorUtility.SetDirty(dial2);
        EditorUtility.SetDirty(dial3);
        EditorUtility.SetDirty(reveal);
        EditorUtility.SetDirty(interactor);
        EditorUtility.SetDirty(vaultReveal);
        EditorUtility.SetDirty(solvedReceiver);
        EditorUtility.SetDirty(gizmos);
        EditorSceneManager.MarkSceneDirty(root.gameObject.scene);

        Selection.activeGameObject = root.gameObject;

        if (!showDialog)
            return;

        StringBuilder message = new StringBuilder();
        message.AppendLine("Church dial puzzle sequence setup complete.");
        message.AppendLine();
        message.AppendLine("Next steps:");
        message.AppendLine("1. Review the generated default visuals and tweak positions for your wall.");
        message.AppendLine("2. Replace or refine the generated geometry with ProBuilder if needed.");
        message.AppendLine("3. Set the correct solution steps on ChurchDialPuzzleManager.");
        message.AppendLine("4. Save the root as a prefab when the sequence works.");
        EditorUtility.DisplayDialog("Church Dial Puzzle", message.ToString(), "OK");
    }

    public static void EnsureSolvedCallbacks(ChurchDialPuzzleManager manager, PuzzleSolvedReceiver solvedReceiver)
    {
        if (manager == null || solvedReceiver == null)
            return;

        if (HasPersistentListener(manager.onPuzzleSolved, solvedReceiver, nameof(PuzzleSolvedReceiver.OnPuzzleSolved)))
            return;

        UnityEventTools.AddPersistentListener(manager.onPuzzleSolved, solvedReceiver.OnPuzzleSolved);
        EditorUtility.SetDirty(manager);
    }

    public static void PrintCurrentStepValues(ChurchDialPuzzleManager manager)
    {
        if (manager == null)
            return;

        Debug.Log($"[ChurchDialPuzzle] Steps -> Dial1:{manager.Dial1?.CurrentStep} Dial2:{manager.Dial2?.CurrentStep} Dial3:{manager.Dial3?.CurrentStep}", manager);
    }

    public static void RandomizeStartSteps(ChurchDialPuzzleManager manager)
    {
        if (manager == null)
            return;

        List<Object> undoTargets = new List<Object> { manager };
        for (int i = 0; i < manager.Dials.Length; i++)
        {
            if (manager.Dials[i] != null)
                undoTargets.Add(manager.Dials[i]);
        }

        Undo.RecordObjects(undoTargets.ToArray(), "Randomize Church Dial Puzzle Steps");
        manager.RandomizeDialSteps(true);
        EditorUtility.SetDirty(manager);
    }

    public static string BuildValidationReport(ChurchDialPuzzleManager manager)
    {
        if (manager == null)
            return "Manager reference is missing.";

        Transform root = ResolveRoot(manager.transform);
        List<string> issues = new List<string>();

        if (!manager.AllDialsAssigned)
            issues.Add("Dial1, Dial2, and Dial3 are not all assigned.");

        ChurchDialPuzzleDial[] dials = manager.Dials;
        for (int i = 0; i < dials.Length; i++)
        {
            ChurchDialPuzzleDial dial = dials[i];
            if (dial == null)
                continue;

            if (dial.transform.childCount == 0)
            {
                issues.Add($"{dial.transform.name} has no mesh child.");
                continue;
            }

            if (!HasCenteredMeshChild(dial.transform))
                issues.Add($"{dial.transform.name} mesh child is offset from its pivot.");
        }

        if (root.Find(PaintingInteractableName) == null)
            issues.Add("PaintingInteractable is missing.");

        if (root.Find(PaintingVisualName) == null)
            issues.Add("PaintingVisual is missing.");

        if (root.Find(PuzzleWallPanelName) == null)
            issues.Add("PuzzleWallPanel is missing.");

        if (root.Find(InteractionPointName) == null)
            issues.Add("InteractionPoint is missing.");

        if (root.Find(CameraFocusPointName) == null)
            issues.Add("CameraFocusPoint is missing.");

        if (root.Find(VaultDoorRootName) == null)
            issues.Add("VaultDoorRoot is missing.");

        if (root.Find(PuzzleManagerName) == null)
            issues.Add("PuzzleManager child is missing.");

        if (root.GetComponentInChildren<PaintingPuzzleReveal>(true) == null)
            issues.Add("PaintingPuzzleReveal is missing.");

        if (root.GetComponentInChildren<ChurchDialPuzzleInteractor>(true) == null)
            issues.Add("ChurchDialPuzzleInteractor is missing.");

        if (root.GetComponentInChildren<VaultRevealController>(true) == null)
            issues.Add("VaultRevealController is missing.");

        PuzzleSolvedReceiver solvedReceiver = root.GetComponent<PuzzleSolvedReceiver>();
        if (solvedReceiver == null)
            issues.Add("PuzzleSolvedReceiver is missing on the root.");
        else if (!HasPersistentListener(manager.onPuzzleSolved, solvedReceiver, nameof(PuzzleSolvedReceiver.OnPuzzleSolved)))
            issues.Add("ChurchDialPuzzleManager.onPuzzleSolved is not wired to PuzzleSolvedReceiver.OnPuzzleSolved.");

        if (issues.Count == 0)
            return string.Empty;

        StringBuilder builder = new StringBuilder();
        for (int i = 0; i < issues.Count; i++)
            builder.AppendLine("- " + issues[i]);
        return builder.ToString().TrimEnd();
    }

    public static void SaveRootAsPrefab(GameObject rootObject)
    {
        if (rootObject == null)
            return;

        rootObject = ResolveRoot(rootObject.transform).gameObject;

        string path = EditorUtility.SaveFilePanelInProject(
            "Save Church Dial Puzzle Prefab",
            rootObject.name,
            "prefab",
            "Choose where to save the church dial puzzle prefab.");

        if (string.IsNullOrEmpty(path))
            return;

        PrefabUtility.SaveAsPrefabAssetAndConnect(rootObject, path, InteractionMode.UserAction);
    }

    private static void ApplyDefaultTransforms(
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
        Transform vaultInterior)
    {
        if (paintingInteractable.localPosition == Vector3.zero)
            paintingInteractable.localPosition = new Vector3(0f, 0f, 0.05f);

        if (paintingVisual.localPosition == Vector3.zero)
            paintingVisual.localPosition = Vector3.zero;

        if (puzzleWallPanel.localPosition == Vector3.zero)
            puzzleWallPanel.localPosition = Vector3.zero;

        if (dial1Pivot.localPosition == Vector3.zero)
            dial1Pivot.localPosition = new Vector3(0f, 0.8f, 0.04f);

        if (dial2Pivot.localPosition == Vector3.zero)
            dial2Pivot.localPosition = new Vector3(0f, 0f, 0.04f);

        if (dial3Pivot.localPosition == Vector3.zero)
            dial3Pivot.localPosition = new Vector3(0f, -0.8f, 0.04f);

        if (centerLock.localPosition == Vector3.zero)
            centerLock.localPosition = new Vector3(0f, 1.36f, 0.07f);

        if (interactionPoint.localPosition == Vector3.zero)
            interactionPoint.localPosition = new Vector3(0f, 0f, 0.55f);

        if (cameraFocusPoint.localPosition == Vector3.zero)
        {
            cameraFocusPoint.localPosition = new Vector3(0f, 0f, 1.7f);
            cameraFocusPoint.localRotation = Quaternion.LookRotation(-Vector3.forward, Vector3.up);
        }

        if (vaultDoorRoot.localPosition == Vector3.zero)
            vaultDoorRoot.localPosition = Vector3.zero;

        if (vaultDoorLeft.localPosition == Vector3.zero)
            vaultDoorLeft.localPosition = new Vector3(-0.45f, 0f, -0.15f);

        if (vaultDoorRight.localPosition == Vector3.zero)
            vaultDoorRight.localPosition = new Vector3(0.45f, 0f, -0.15f);

        if (vaultInterior.localPosition == Vector3.zero)
            vaultInterior.localPosition = new Vector3(0f, 0f, -1.1f);

        vaultInterior.gameObject.SetActive(false);
    }

    private static void EnsureInteractionCollider(GameObject interactionObject)
    {
        if (interactionObject == null)
            return;

        BoxCollider boxCollider = interactionObject.GetComponent<BoxCollider>();
        if (boxCollider == null)
            boxCollider = Undo.AddComponent<BoxCollider>(interactionObject);

        Collider[] colliders = interactionObject.GetComponents<Collider>();
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] == null || colliders[i] == boxCollider)
                continue;

            Undo.RecordObject(colliders[i], "Disable Extra Church Dial Puzzle Collider");
            colliders[i].enabled = false;
        }

        Undo.RecordObject(boxCollider, "Configure Church Dial Puzzle Collider");
        boxCollider.isTrigger = true;
        boxCollider.center = Vector3.zero;
        boxCollider.size = new Vector3(2.4f, 2.8f, 0.25f);
        boxCollider.enabled = true;
    }

    private static bool HasCenteredMeshChild(Transform pivot)
    {
        if (pivot == null || pivot.childCount == 0)
            return false;

        Transform child = pivot.GetChild(0);
        return child.localPosition.sqrMagnitude <= LocalCenterTolerance * LocalCenterTolerance;
    }

    private static bool HasPersistentListener(UnityEvent unityEvent, Object target, string methodName)
    {
        if (unityEvent == null || target == null || string.IsNullOrEmpty(methodName))
            return false;

        int persistentCount = unityEvent.GetPersistentEventCount();
        for (int i = 0; i < persistentCount; i++)
        {
            if (unityEvent.GetPersistentTarget(i) == target &&
                unityEvent.GetPersistentMethodName(i) == methodName)
                return true;
        }

        return false;
    }

    private static Transform ResolveRoot(Transform candidate)
    {
        if (candidate == null)
            return null;

        Transform current = candidate;
        while (current.parent != null)
        {
            if (LooksLikePuzzleRoot(current))
                return current;

            current = current.parent;
        }

        return LooksLikePuzzleRoot(current) ? current : candidate;
    }

    private static bool LooksLikePuzzleRoot(Transform transform)
    {
        if (transform == null)
            return false;

        return transform.Find(Dial1PivotName) != null ||
               transform.Find(Dial2PivotName) != null ||
               transform.Find(Dial3PivotName) != null ||
               transform.Find(PaintingInteractableName) != null ||
               transform.Find(VaultDoorRootName) != null ||
               transform.Find(PuzzleManagerName) != null;
    }

    private static Transform FindOrCreateChild(Transform parent, string childName)
    {
        Transform child = parent.Find(childName);
        if (child != null)
            return child;

        GameObject childObject = new GameObject(childName);
        Undo.RegisterCreatedObjectUndo(childObject, "Create Church Dial Puzzle Child");
        childObject.transform.SetParent(parent, false);
        childObject.transform.localPosition = Vector3.zero;
        childObject.transform.localRotation = Quaternion.identity;
        childObject.transform.localScale = Vector3.one;
        return childObject.transform;
    }

    private static void EnsurePlaceholderChild(Transform parent, string childName)
    {
        if (parent.Find(childName) != null)
            return;

        FindOrCreateChild(parent, childName);
    }

    private static T GetOrAdd<T>(GameObject target) where T : Component
    {
        T existing = target.GetComponent<T>();
        if (existing != null)
            return existing;

        return Undo.AddComponent<T>(target);
    }
}

[CustomEditor(typeof(ChurchDialPuzzleManager))]
public class ChurchDialPuzzleManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        ChurchDialPuzzleManager manager = (ChurchDialPuzzleManager)target;
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Setup Tools", EditorStyles.boldLabel);

        string validation = ChurchDialPuzzleAutoSetupEditor.BuildValidationReport(manager);
        if (string.IsNullOrEmpty(validation))
            EditorGUILayout.HelpBox("Setup looks valid.", MessageType.Info);
        else
            EditorGUILayout.HelpBox(validation, MessageType.Warning);

        if (GUILayout.Button("Auto-Setup Sequence Root"))
            ChurchDialPuzzleAutoSetupEditor.Run(manager.gameObject, false);

        if (GUILayout.Button("Rebuild Default Visuals"))
            ChurchDialPuzzleDefaultVisualBuilder.RebuildDefaultVisuals(manager.transform);

        if (GUILayout.Button("Validate Setup"))
        {
            if (string.IsNullOrEmpty(validation))
                Debug.Log("[ChurchDialPuzzle] Validation passed.", manager);
            else
                Debug.LogWarning("[ChurchDialPuzzle] Validation issues:\n" + validation, manager);
        }

        if (GUILayout.Button("Auto-Wire Solved Event"))
        {
            PuzzleSolvedReceiver receiver = manager.GetComponentInParent<PuzzleSolvedReceiver>() ??
                manager.GetComponent<PuzzleSolvedReceiver>() ??
                manager.GetComponentInChildren<PuzzleSolvedReceiver>(true);
            ChurchDialPuzzleAutoSetupEditor.EnsureSolvedCallbacks(manager, receiver);
        }

        if (GUILayout.Button("Print Current Step Values"))
            ChurchDialPuzzleAutoSetupEditor.PrintCurrentStepValues(manager);

        if (GUILayout.Button("Randomize Start Steps"))
            ChurchDialPuzzleAutoSetupEditor.RandomizeStartSteps(manager);

        if (GUILayout.Button("Set Current Steps As Solution"))
        {
            Undo.RecordObject(manager, "Use Current Church Dial Puzzle Steps As Solution");
            manager.SetCurrentStepsAsSolution();
            EditorUtility.SetDirty(manager);
        }

        if (GUILayout.Button("Save Puzzle Root As Prefab"))
            ChurchDialPuzzleAutoSetupEditor.SaveRootAsPrefab(manager.gameObject);

        if (GUI.changed)
            EditorUtility.SetDirty(manager);
    }
}
