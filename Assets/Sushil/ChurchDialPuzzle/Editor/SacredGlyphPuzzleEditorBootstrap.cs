using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class SacredGlyphPuzzleEditorBootstrap
{
    private const string RootName = "ChurchPuzzleSequenceRoot";
    private const string PaintingVisualName = "PaintingVisual";
    private const string PuzzleWallPanelName = "PuzzleWallPanel";
    private const string Dial1PivotName = "Dial1Pivot";
    private const string Dial1MeshName = "Dial1Mesh";
    private const string GeneratedChildName = "GeneratedDefault";
    private const string ScanQueuedKey = "SacredGlyphPuzzleEditorBootstrap.ScanQueued";

    // Name of the parent wall to attach the puzzle root to if no root exists yet.
    private const string WallParentName = "Wall2 (2)";

    // Local position of the puzzle root relative to the wall parent.
    // Centered horizontally, 1.2 m above the wall origin, 0.15 m proud of the surface.
    private static readonly Vector3 DefaultPuzzleLocalPosition = new Vector3(0f, 1.2f, 0.15f);

    private static bool isProcessing;

    static SacredGlyphPuzzleEditorBootstrap()
    {
        EditorApplication.delayCall += ScanAndRepairOpenScenes;
        EditorApplication.hierarchyChanged += QueueScan;
        EditorSceneManager.sceneOpened += OnSceneOpened;
    }

    private static void OnSceneOpened(Scene scene, OpenSceneMode mode)
    {
        QueueScan();
    }

    private static void QueueScan()
    {
        if (isProcessing || SessionState.GetBool(ScanQueuedKey, false))
            return;

        SessionState.SetBool(ScanQueuedKey, true);
        EditorApplication.delayCall += DelayedScan;
    }

    private static void DelayedScan()
    {
        SessionState.SetBool(ScanQueuedKey, false);
        ScanAndRepairOpenScenes();
    }

    private static void ScanAndRepairOpenScenes()
    {
        if (isProcessing)
            return;

        isProcessing = true;

        try
        {
            for (int sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
            {
                Scene scene = SceneManager.GetSceneAt(sceneIndex);
                if (!scene.IsValid() || !scene.isLoaded)
                    continue;

                ProcessScene(scene);
            }
        }
        finally
        {
            isProcessing = false;
        }
    }

    private static void ProcessScene(Scene scene)
    {
        Transform existingRoot = FindPuzzleRootInScene(scene);

        if (existingRoot != null)
        {
            // Root exists — repair if it is missing generated visuals.
            if (NeedsRepair(existingRoot))
            {
                SacredGlyphPuzzleAutoSetupEditor.Run(existingRoot.gameObject, false);
                SaveScene(scene);
            }
            return;
        }

        // No puzzle root found in this scene.
        // Try to create one under the designated wall parent.
        Transform wallParent = FindByName(scene, WallParentName);
        if (wallParent == null)
            return;

        // Check whether someone previously created the root but it has no puzzle children yet.
        Transform namedRoot = wallParent.Find(RootName);
        GameObject puzzleRootGO;

        if (namedRoot != null)
        {
            puzzleRootGO = namedRoot.gameObject;
        }
        else
        {
            puzzleRootGO = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(puzzleRootGO, "Create Sacred Glyph Puzzle Root");
            puzzleRootGO.transform.SetParent(wallParent, false);
            puzzleRootGO.transform.localPosition = DefaultPuzzleLocalPosition;
            puzzleRootGO.transform.localRotation = Quaternion.identity;
            puzzleRootGO.transform.localScale = Vector3.one;
        }

        SacredGlyphPuzzleAutoSetupEditor.Run(puzzleRootGO, false);
        SaveScene(scene);
    }

    // -----------------------------------------------------------------------
    //  Scene traversal helpers
    // -----------------------------------------------------------------------

    private static Transform FindPuzzleRootInScene(Scene scene)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            Transform found = FindPuzzleRoot(roots[i].transform);
            if (found != null)
                return found;
        }
        return null;
    }

    private static Transform FindPuzzleRoot(Transform current)
    {
        if (current == null)
            return null;

        if (LooksLikePuzzleRoot(current))
            return current;

        for (int i = 0; i < current.childCount; i++)
        {
            Transform found = FindPuzzleRoot(current.GetChild(i));
            if (found != null)
                return found;
        }

        return null;
    }

    private static Transform FindByName(Scene scene, string targetName)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            Transform found = FindByName(roots[i].transform, targetName);
            if (found != null)
                return found;
        }
        return null;
    }

    private static Transform FindByName(Transform current, string targetName)
    {
        if (current.name == targetName)
            return current;

        for (int i = 0; i < current.childCount; i++)
        {
            Transform found = FindByName(current.GetChild(i), targetName);
            if (found != null)
                return found;
        }

        return null;
    }

    // -----------------------------------------------------------------------
    //  Validation
    // -----------------------------------------------------------------------

    private static bool LooksLikePuzzleRoot(Transform transform)
    {
        if (transform == null)
            return false;

        if (transform.name == RootName)
            return true;

        return transform.Find(PaintingVisualName) != null &&
               transform.Find(PuzzleWallPanelName) != null;
    }

    private static bool NeedsRepair(Transform root)
    {
        Transform paintingVisual = root.Find(PaintingVisualName);
        Transform puzzleWallPanel = root.Find(PuzzleWallPanelName);
        Transform dial1Mesh = root.Find($"{Dial1PivotName}/{Dial1MeshName}");

        if (paintingVisual == null || puzzleWallPanel == null || dial1Mesh == null)
            return true;

        Transform paintingGenerated = paintingVisual.Find(GeneratedChildName);
        if (paintingGenerated == null)
            return true;

        if (puzzleWallPanel.Find(GeneratedChildName) == null)
            return true;

        Transform dialGenerated = dial1Mesh.Find(GeneratedChildName);
        if (dialGenerated == null)
            return true;

        if (paintingGenerated.Find("CanvasArt") == null)
            return true;

        if (dialGenerated.Find("SecondaryRim") == null)
            return true;

        // Check that generated materials exist and use the correct render-pipeline shader.
        // If any CDP_ material is using the wrong shader (e.g. Standard in a URP project),
        // the geometry will appear solid black, so we must rebuild to purge and recreate them.
        if (!HasValidMaterials())
            return true;

        return false;
    }

    private const string GeneratedMaterialFolder = "Assets/Sushil/ChurchDialPuzzle/Generated/Materials";

    private static bool HasValidMaterials()
    {
        if (!AssetDatabase.IsValidFolder(GeneratedMaterialFolder))
            return false;

        string[] guids = AssetDatabase.FindAssets("t:Material", new[] { GeneratedMaterialFolder });
        if (guids.Length == 0)
            return false;

        // Only perform shader validation when URP shaders are available.
        Shader urpLit   = Shader.Find("Universal Render Pipeline/Lit");
        Shader urpUnlit = Shader.Find("Universal Render Pipeline/Unlit");
        if (urpLit == null)
            return true; // URP not installed — accept whatever is there

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path) || !path.Contains("CDP_"))
                continue;

            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
                return false;

            bool usesUrp = mat.shader == urpLit || mat.shader == urpUnlit;
            if (!usesUrp)
                return false; // Wrong shader → stale material → will render black
        }

        return true;
    }

    // -----------------------------------------------------------------------
    //  Scene save
    // -----------------------------------------------------------------------

    private static void SaveScene(Scene scene)
    {
        EditorSceneManager.MarkSceneDirty(scene);

        if (!string.IsNullOrEmpty(scene.path))
            EditorSceneManager.SaveScene(scene);
    }
}
