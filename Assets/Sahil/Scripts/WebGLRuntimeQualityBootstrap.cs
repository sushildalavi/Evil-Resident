using UnityEngine;

public static class WebGLRuntimeQualityBootstrap
{
    private const string DesiredQualityName = "PC";
    private const int TargetFps = 60;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Apply()
    {
#if UNITY_WEBGL
        int qualityIndex = GetQualityIndex(DesiredQualityName);
        if (qualityIndex >= 0)
            QualitySettings.SetQualityLevel(qualityIndex, true);

        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = TargetFps;

        Debug.Log($"[WebGLRuntimeQualityBootstrap] quality={QualitySettings.names[QualitySettings.GetQualityLevel()]} vSync={QualitySettings.vSyncCount} targetFps={Application.targetFrameRate}");
#endif
    }

    private static int GetQualityIndex(string qualityName)
    {
        string[] qualityNames = QualitySettings.names;
        for (int i = 0; i < qualityNames.Length; i++)
        {
            if (qualityNames[i] == qualityName)
                return i;
        }

        return -1;
    }
}
