using UnityEngine;
using UnityEngine.SceneManagement;
using Sushil.AI;

public class TutorialPlayerParityEnforcer : MonoBehaviour
{
    static TutorialPlayerParityEnforcer instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap()
    {
        if (instance != null)
            return;

        GameObject host = new GameObject("TutorialPlayerParityEnforcer");
        instance = host.AddComponent<TutorialPlayerParityEnforcer>();
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
        Invoke(nameof(EnforceDelayedA), 0.15f);
        Invoke(nameof(EnforceDelayedB), 0.5f);
    }

    void EnforceDelayedA()
    {
        EnforceForScene(SceneManager.GetActiveScene());
    }

    void EnforceDelayedB()
    {
        EnforceForScene(SceneManager.GetActiveScene());
    }

    static bool NeedsParity(Scene scene)
    {
        return scene.name == "New Tutorial 1" ||
               scene.name == "New Tutorial 2" ||
               scene.name == "New Tutorial 3";
    }

    static void EnforceForScene(Scene scene)
    {
        if (!NeedsParity(scene))
            return;

        RohitFPSController controller = FindFirstObjectByType<RohitFPSController>();
        if (controller != null)
        {
            controller.walkSpeed = 3f;
            controller.sprintSpeed = 5f;
            controller.jumpHeight = 1f;
            controller.acceleration = 15f;
            controller.deceleration = 10f;
            controller.airControl = 0.2f;
            controller.mouseSensitivity = 150f;
            controller.useCapsulePreCast = false;

            CharacterController cc = controller.GetComponent<CharacterController>();
            if (cc != null)
            {
                cc.height = 1f;
                Vector3 center = cc.center;
                center.y = 0f;
                cc.center = center;
            }
        }

        PlayerTorch torch = FindFirstObjectByType<PlayerTorch>();
        if (torch != null)
        {
            torch.startOn = true;
            torch.useBattery = false;
            torch.intensity = 5f;
            torch.range = 45f;
            torch.spotAngle = 79.7f;
            torch.lightColor = new Color(1f, 0.96f, 0.86f, 1f);

            if (torch.cameraTransform == null)
            {
                Camera cam = torch.GetComponentInChildren<Camera>();
                if (cam != null)
                    torch.cameraTransform = cam.transform;
            }

            if (torch.torchLight == null)
            {
                Light existing = FindTorchLightInPlayer(torch.transform);
                if (existing != null)
                {
                    torch.torchLight = existing;
                }
                else
                {
                    Transform parent = torch.cameraTransform != null ? torch.cameraTransform : torch.transform;
                    GameObject torchObj = new GameObject("PlayerTorchLight");
                    torchObj.transform.SetParent(parent, false);
                    torchObj.transform.localPosition = new Vector3(0.08f, -0.05f, 0.2f);
                    torchObj.transform.localRotation = Quaternion.identity;
                    torch.torchLight = torchObj.AddComponent<Light>();
                }
            }

            if (torch.torchLight != null)
            {
                torch.torchLight.type = LightType.Spot;
                torch.torchLight.intensity = torch.intensity;
                torch.torchLight.range = torch.range;
                torch.torchLight.spotAngle = torch.spotAngle;
                torch.torchLight.color = torch.lightColor;
                torch.torchLight.shadows = LightShadows.Soft;
                torch.torchLight.enabled = true;
            }
        }

        EnforceEnemyScaleParity();
    }

    static void EnforceEnemyScaleParity()
    {
        ResidentAI[] residents = FindObjectsByType<ResidentAI>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < residents.Length; i++)
        {
            ResidentAI resident = residents[i];
            if (resident == null)
                continue;

            EnsureMinimumVisualHeight(resident.transform, 1.55f, 2.8f);
        }

        WeepingAngelAI[] angels = FindObjectsByType<WeepingAngelAI>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < angels.Length; i++)
        {
            WeepingAngelAI angel = angels[i];
            if (angel == null)
                continue;

            EnsureMinimumVisualHeight(angel.transform, 1.55f, 2.0f);
        }
    }

    static void EnsureMinimumVisualHeight(Transform root, float minHeight, float maxScaleMultiplier)
    {
        if (root == null || minHeight <= 0f)
            return;

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers == null || renderers.Length == 0)
            return;

        bool hasBounds = false;
        Bounds bounds = new Bounds(root.position, Vector3.zero);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer r = renderers[i];
            if (r == null)
                continue;

            if (!hasBounds)
            {
                bounds = r.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(r.bounds);
            }
        }

        if (!hasBounds)
            return;

        float currentHeight = bounds.size.y;
        if (currentHeight >= minHeight || currentHeight <= 0.001f)
            return;

        float multiplier = Mathf.Clamp(minHeight / currentHeight, 1f, Mathf.Max(1f, maxScaleMultiplier));
        root.localScale *= multiplier;
    }

    static Light FindTorchLightInPlayer(Transform root)
    {
        if (root == null)
            return null;

        Light[] lights = root.GetComponentsInChildren<Light>(true);
        for (int i = 0; i < lights.Length; i++)
        {
            Light light = lights[i];
            if (light == null)
                continue;

            string n = light.gameObject.name.ToLowerInvariant();
            if (n.Contains("torch") || n.Contains("flash"))
                return light;
        }

        for (int i = 0; i < lights.Length; i++)
        {
            Light light = lights[i];
            if (light != null && light.type == LightType.Spot)
                return light;
        }

        return null;
    }
}
