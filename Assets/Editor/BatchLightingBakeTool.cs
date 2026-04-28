using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class BatchLightingBakeTool
{
    const string MenuPath = "Tools/Lighting/Bake Enabled Build Scenes";

    [MenuItem(MenuPath)]
    static void BakeEnabledBuildScenes()
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
            Debug.LogWarning("[BatchLightingBake] No enabled scenes found in Build Settings.");
            return;
        }

        string originalScenePath = EditorSceneManager.GetActiveScene().path;
        if (!EditorUtility.DisplayDialog(
                "Bake Lighting",
                $"Bake lighting for {scenePaths.Count} enabled build scene(s)?\n\nThis will open each scene, bake, and save.",
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
                EditorUtility.DisplayProgressBar("Batch Lighting Bake", $"Opening {scenePath}", progress);

                var opened = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                if (!opened.IsValid())
                {
                    Debug.LogError($"[BatchLightingBake] Failed to open scene: {scenePath}");
                    continue;
                }

                EditorUtility.DisplayProgressBar("Batch Lighting Bake", $"Baking {opened.name}", progress);
                Lightmapping.Bake();

                EditorSceneManager.MarkSceneDirty(opened);
                EditorSceneManager.SaveScene(opened);
                Debug.Log($"[BatchLightingBake] Baked and saved: {scenePath}");
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();

            if (!string.IsNullOrEmpty(originalScenePath))
                EditorSceneManager.OpenScene(originalScenePath, OpenSceneMode.Single);
        }

        Debug.Log($"[BatchLightingBake] Completed baking {scenePaths.Count} scene(s).");
    }
}
