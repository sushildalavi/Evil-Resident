using UnityEngine;
using UnityEngine.SceneManagement;

public class TutorialLightingEnforcer : MonoBehaviour
{
    static TutorialLightingEnforcer instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap()
    {
        if (instance != null)
            return;

        GameObject host = new GameObject("TutorialLightingEnforcer");
        instance = host.AddComponent<TutorialLightingEnforcer>();
        DontDestroyOnLoad(host);
    }

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        SceneManager.sceneLoaded += OnSceneLoaded;
        EnforceForScene(SceneManager.GetActiveScene());
    }

    void OnDestroy()
    {
        if (instance == this)
            instance = null;

        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnforceForScene(scene);
        Invoke(nameof(EnforceDelayed), 0.25f);
    }

    void EnforceDelayed()
    {
        EnforceForScene(SceneManager.GetActiveScene());
    }

    static bool IsTutorialScene(Scene scene)
    {
        return scene.name == "New Tutorial 1" ||
               scene.name == "New Tutorial 2" ||
               scene.name == "New Tutorial 3";
    }

    static void EnforceForScene(Scene scene)
    {
        if (!IsTutorialScene(scene))
            return;

        RenderSettings.ambientIntensity = 0f;
        RenderSettings.reflectionIntensity = 0f;
        RenderSettings.fog = false;

        Light[] lights = FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < lights.Length; i++)
        {
            Light light = lights[i];
            if (light == null)
                continue;

            if (ShouldKeepLight(light))
                continue;

            light.enabled = false;
        }
    }

    static bool ShouldKeepLight(Light light)
    {
        if (light == null)
            return false;

        string nameLower = light.gameObject.name.ToLowerInvariant();
        if (nameLower.Contains("horror"))
            return true;

        if (light.GetComponent<LightFlicker>() != null || light.GetComponentInParent<LightFlicker>() != null)
            return true;

        if (light.GetComponent<PlayerTorch>() != null || light.GetComponentInParent<PlayerTorch>() != null)
            return true;

        if (light.GetComponent<RohitFPSController>() != null || light.GetComponentInParent<RohitFPSController>() != null)
            return true;

        if (nameLower.Contains("playertorch") || nameLower.Contains("flashlight"))
            return true;

        return false;
    }
}
