using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class BatchNavMeshBakeTool
{
    const string MenuPath = "Tools/NavMesh/Bake Enabled Build Scenes";

    [MenuItem(MenuPath)]
    static void BakeEnabledBuildScenesNavMesh()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        EditorBuildSettingsScene[] buildScenes = EditorBuildSettings.scenes;
        List<string> scenePaths = new List<string>();
        for (int i = 0; i < buildScenes.Length; i++)
        {
            EditorBuildSettingsScene scene = buildScenes[i];
            if (scene != null && scene.enabled && !string.IsNullOrEmpty(scene.path))
                scenePaths.Add(scene.path);
        }

        if (scenePaths.Count == 0)
        {
            Debug.LogWarning("[BatchNavMeshBake] No enabled scenes found in Build Settings.");
            return;
        }

        string originalScenePath = EditorSceneManager.GetActiveScene().path;
        if (!EditorUtility.DisplayDialog(
                "Bake NavMesh",
                $"Bake NavMesh for {scenePaths.Count} enabled build scene(s)?\n\nThis will open each scene, build all NavMeshSurface components, and save.",
                "Bake All",
                "Cancel"))
        {
            return;
        }

        try
        {
            for (int i = 0; i < scenePaths.Count; i++)
            {
                string scenePath = scenePaths[i];
                float progress = (float)i / scenePaths.Count;
                EditorUtility.DisplayProgressBar("Batch NavMesh Bake", $"Opening {scenePath}", progress);

                var opened = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                if (!opened.IsValid())
                {
                    Debug.LogError($"[BatchNavMeshBake] Failed to open scene: {scenePath}");
                    continue;
                }

                NavMeshSurface[] surfaces = Object.FindObjectsByType<NavMeshSurface>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                if (surfaces == null || surfaces.Length == 0)
                {
                    Debug.LogWarning($"[BatchNavMeshBake] No NavMeshSurface found in scene: {scenePath}");
                }
                else
                {
                    for (int s = 0; s < surfaces.Length; s++)
                    {
                        NavMeshSurface surface = surfaces[s];
                        if (surface == null) continue;

                        float sceneProgress = progress + (1f / scenePaths.Count) * ((float)s / Mathf.Max(1, surfaces.Length));
                        EditorUtility.DisplayProgressBar("Batch NavMesh Bake", $"Baking {opened.name} ({surface.name})", sceneProgress);
                        surface.BuildNavMesh();
                    }
                }

                EditorSceneManager.MarkSceneDirty(opened);
                EditorSceneManager.SaveScene(opened);
                Debug.Log($"[BatchNavMeshBake] Baked and saved: {scenePath}");
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();

            if (!string.IsNullOrEmpty(originalScenePath))
                EditorSceneManager.OpenScene(originalScenePath, OpenSceneMode.Single);
        }

        Debug.Log($"[BatchNavMeshBake] Completed baking {scenePaths.Count} scene(s).");
    }
}
