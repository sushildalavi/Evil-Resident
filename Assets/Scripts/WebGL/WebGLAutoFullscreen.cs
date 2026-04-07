using System.Collections;
using System.Runtime.InteropServices;
using UnityEngine;

public sealed class WebGLAutoFullscreen : MonoBehaviour
{
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void RequestWebGLFullscreen();
#endif

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Install()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        var go = new GameObject(nameof(WebGLAutoFullscreen));
        DontDestroyOnLoad(go);
        go.hideFlags = HideFlags.HideAndDontSave;
        go.AddComponent<WebGLAutoFullscreen>();
#endif
    }

    private IEnumerator Start()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        // First request as early as possible.
        TryRequestFullscreen();

        // Retry for the first few seconds while the page is still initializing.
        const float timeoutSeconds = 6f;
        const float retryInterval = 0.5f;
        var elapsed = 0f;

        while (!Screen.fullScreen && elapsed < timeoutSeconds)
        {
            yield return new WaitForSeconds(retryInterval);
            elapsed += retryInterval;
            TryRequestFullscreen();
        }
#else
        yield break;
#endif
    }

    private void Update()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        if (Screen.fullScreen)
        {
            return;
        }

        // Browser fullscreen usually requires a user gesture.
        if (Input.anyKeyDown || Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1))
        {
            TryRequestFullscreen();
        }
#endif
    }

    private static void TryRequestFullscreen()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        Screen.fullScreen = true;

        try
        {
            RequestWebGLFullscreen();
        }
        catch
        {
            // Ignore if JS bridge is unavailable.
        }
#endif
    }
}
