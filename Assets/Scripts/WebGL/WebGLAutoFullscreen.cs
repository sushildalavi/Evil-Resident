using UnityEngine;

public sealed class WebGLAutoFullscreen : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Install()
    {
        // Intentionally disabled: do not force fullscreen on startup.
    }
}
