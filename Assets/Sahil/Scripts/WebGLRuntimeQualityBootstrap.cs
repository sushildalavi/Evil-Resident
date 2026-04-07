using UnityEngine;

public static class WebGLRuntimeQualityBootstrap
{
    private static readonly string[] PreferredQualityOrder =
    {
        "WebGL",
        "Mobile",
        "Fast",
        "Simple",
        "Low"
    };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Apply()
    {
#if UNITY_WEBGL
        int qualityIndex = GetPreferredQualityIndex();
        if (qualityIndex >= 0)
            QualitySettings.SetQualityLevel(qualityIndex, true);

        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = -1;

        // WebGL-specific parity-safe GPU budget controls.
        QualitySettings.realtimeReflectionProbes = true;
        QualitySettings.anisotropicFiltering = AnisotropicFiltering.Enable;
        QualitySettings.shadowDistance = Mathf.Min(QualitySettings.shadowDistance, 50f);

        Debug.Log(
            $"[WebGLRuntimeQualityBootstrap] profile=webgl-optimized applied=true " +
            $"quality={QualitySettings.names[QualitySettings.GetQualityLevel()]} " +
            $"vSync={QualitySettings.vSyncCount} targetFps={Application.targetFrameRate} " +
            $"shadowDistance={QualitySettings.shadowDistance}");
#endif
    }

    private static int GetPreferredQualityIndex()
    {
        for (int i = 0; i < PreferredQualityOrder.Length; i++)
        {
            int index = GetQualityIndex(PreferredQualityOrder[i]);
            if (index >= 0)
                return index;
        }

        if (QualitySettings.names != null && QualitySettings.names.Length > 0)
            return 0;

        return -1;
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
