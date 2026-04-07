using System.Runtime.InteropServices;
using UnityEngine;

public sealed class WebGLFixedResolution1080p : MonoBehaviour
{
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void SetWebGLFixedResolution(int width, int height);
    [DllImport("__Internal")]
    private static extern void ConfigureWebGLPageForNoZoom();

    private const int TargetWidth = 1600;
    private const int TargetHeight = 900;
    private const float ReapplyInterval = 0.5f;

    private float nextApplyTime;
    private bool lastFullscreen;
#endif

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Install()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        var go = new GameObject(nameof(WebGLFixedResolution1080p));
        DontDestroyOnLoad(go);
        go.hideFlags = HideFlags.HideAndDontSave;
        go.AddComponent<WebGLFixedResolution1080p>();
#endif
    }

    private void Start()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        try
        {
            ConfigureWebGLPageForNoZoom();
        }
        catch
        {
            // Ignore if JS bridge is unavailable.
        }

        ApplyFixedResolution(true);
#endif
    }

    private void Update()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        bool fullscreen = Screen.fullScreen;
        if (fullscreen != lastFullscreen || Time.unscaledTime >= nextApplyTime)
            ApplyFixedResolution(false);

        lastFullscreen = fullscreen;
#endif
    }

#if UNITY_WEBGL && !UNITY_EDITOR
    private void ApplyFixedResolution(bool forceLog)
    {
        nextApplyTime = Time.unscaledTime + ReapplyInterval;

        try
        {
            SetWebGLFixedResolution(TargetWidth, TargetHeight);
        }
        catch
        {
            // Ignore if JS bridge is unavailable.
        }

        if (forceLog || Screen.fullScreen != lastFullscreen)
        {
            Debug.Log($"[WebGLFixedResolution1080p] lockedRenderResolution={TargetWidth}x{TargetHeight} fullscreen={Screen.fullScreen}");
        }
    }
#endif
}
