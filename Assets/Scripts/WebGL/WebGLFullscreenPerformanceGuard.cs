using UnityEngine;

public sealed class WebGLFullscreenPerformanceGuard : MonoBehaviour
{
#if UNITY_WEBGL && !UNITY_EDITOR
    private const float WindowedScale = 1f;
    private const float FullscreenScale1080p = 0.85f;
    private const float FullscreenScale1440p = 0.75f;
    private const float FullscreenScale4K = 0.66f;
    private const float PollInterval = 0.5f;

    private float nextCheckTime;
    private bool lastFullscreenState;
    private float lastAppliedScale = -1f;
#endif

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Install()
    {
        // Disabled: fixed 1080p WebGL render target now handles fullscreen performance.
    }

    private void Update()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        if (Time.unscaledTime < nextCheckTime)
            return;

        nextCheckTime = Time.unscaledTime + PollInterval;

        bool isFullscreen = Screen.fullScreen;
        float targetScale = isFullscreen ? GetFullscreenScale() : WindowedScale;

        if (Mathf.Abs(lastAppliedScale - targetScale) > 0.001f)
        {
            ScalableBufferManager.ResizeBuffers(targetScale, targetScale);
            lastAppliedScale = targetScale;
        }

        if (isFullscreen != lastFullscreenState)
        {
            Debug.Log($"[WebGLFullscreenPerformanceGuard] fullscreen={isFullscreen} renderScale={targetScale:0.00} screen={Screen.width}x{Screen.height}");
            lastFullscreenState = isFullscreen;
        }
#endif
    }

#if UNITY_WEBGL && !UNITY_EDITOR
    private static float GetFullscreenScale()
    {
        int maxDim = Mathf.Max(Screen.width, Screen.height);
        if (maxDim >= 2160)
            return FullscreenScale4K;
        if (maxDim >= 1440)
            return FullscreenScale1440p;

        return FullscreenScale1080p;
    }
#endif
}
