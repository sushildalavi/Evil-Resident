using UnityEngine;
using UnityEngine.SceneManagement;

public class LevelSelectDoorTransition : MonoBehaviour, IInteractable
{
    [Header("Display")]
    public string doorLabel = "Tutorial Door";
    public string promptText = "Press E to enter Tutorial";

    [Header("Target Scene")]
    [Tooltip("Preferred full path (for example: Assets/Sahil/Test/Easy Level.unity).")]
    public string targetScenePath = "";
    [Tooltip("Fallback scene name.")]
    public string targetSceneName = "";

    public KeyCode GetInteractKey() => KeyCode.E;

    public string GetPrompt(RohitFPSController player)
    {
        return string.IsNullOrWhiteSpace(promptText)
            ? $"Press E - {doorLabel}"
            : promptText;
    }

    public void Interact(RohitFPSController player)
    {
        if (TryLoadByPath(targetScenePath))
            return;

        string fallback = string.IsNullOrWhiteSpace(targetSceneName) ? string.Empty : targetSceneName.Trim();
        if (string.IsNullOrEmpty(fallback))
        {
            Debug.LogError($"[LevelSelectDoorTransition] No target configured on '{name}'.");
            return;
        }

        SceneManager.LoadScene(fallback);
    }

    bool TryLoadByPath(string scenePath)
    {
        if (string.IsNullOrWhiteSpace(scenePath))
            return false;

        int buildIndex = SceneUtility.GetBuildIndexByScenePath(scenePath.Trim());
        if (buildIndex < 0)
            return false;

        SceneManager.LoadScene(buildIndex);
        return true;
    }
}
