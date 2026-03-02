using UnityEngine;
using UnityEngine.UI;
using Sushil.AI;

/// <summary>
/// Valorant-style minimap: clear layout (walls/floor), player facing, enemy and keys.
/// Uses replacement shader for readable geometry when available; otherwise bright overhead light.
/// </summary>
public class MinimapController : MonoBehaviour
{
    [Header("Bounds (world XZ)")]
    public float worldMinX = -60f;
    public float worldMaxX = 60f;
    public float worldMinZ = -60f;
    public float worldMaxZ = 60f;

    [Header("Minimap camera")]
    public float cameraHeight = 50f;
    public float orthoSize = 28f;
    public int renderTextureSize = 384;

    [Header("Fog of war")]
    public float revealRadiusWorld = 8f;

    [Header("UI")]
    public float minimapSizePixels = 200f;
    public float iconSizePixels = 10f;
    public float playerArrowLength = 14f;
    public Color playerColor = new Color(0.2f, 0.6f, 1f, 1f);
    public Color enemyColor = new Color(1f, 0.2f, 0.2f, 1f);
    public Color keyColor = new Color(1f, 0.85f, 0.2f, 1f);

    Transform player;
    Transform stalker;
    KeyItem[] keyItems;
    Camera minimapCamera;
    RenderTexture renderTexture;
    Texture2D fogTexture;
    RectTransform minimapRect;
    RectTransform playerIconRect;
    RectTransform enemyIconRect;
    Image[] keyIcons = new Image[3];
    Canvas canvas;
    bool uiBuilt;
    Light minimapLight;
    Shader replacementShader;
    bool useReplacementShader;

    const int FogResolution = 256;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void EnsureMinimapOnPlayer()
    {
        // Minimap disabled. Uncomment below to re-enable.
        // var rohit = FindFirstObjectByType<RohitFPSController>();
        // if (rohit == null) return;
        // if (rohit.GetComponent<MinimapController>() != null) return;
        // rohit.gameObject.AddComponent<MinimapController>();
    }

    void Start()
    {
        ResolveReferences();
        BuildMinimapCamera();
        BuildFogTexture();
        BuildUI();
        uiBuilt = true;
    }

    void ResolveReferences()
    {
        if (player == null)
        {
            var rohit = FindFirstObjectByType<RohitFPSController>();
            if (rohit != null) player = rohit.transform;
        }
        if (player == null)
        {
            var go = GameObject.FindGameObjectWithTag("Player");
            if (go != null) player = go.transform;
        }

        if (stalker == null)
        {
            var ai = FindFirstObjectByType<StalkerAI>();
            if (ai != null) stalker = ai.transform;
        }

        RefreshKeyItems();
    }

    void RefreshKeyItems()
    {
        keyItems = FindObjectsByType<KeyItem>(FindObjectsSortMode.None);
    }

    void BuildMinimapCamera()
    {
        if (player == null) return;

        replacementShader = Resources.Load<Shader>("Shaders/MinimapReplacement");
        useReplacementShader = replacementShader != null;

        GameObject camGo = new GameObject("MinimapCamera");
        camGo.transform.SetParent(transform);
        minimapCamera = camGo.AddComponent<Camera>();
        minimapCamera.orthographic = true;
        minimapCamera.orthographicSize = orthoSize;
        minimapCamera.nearClipPlane = 0.3f;
        minimapCamera.farClipPlane = cameraHeight + 20f;
        minimapCamera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        minimapCamera.clearFlags = CameraClearFlags.SolidColor;
        minimapCamera.backgroundColor = new Color(0.08f, 0.09f, 0.12f, 1f);
        minimapCamera.depth = 10;
        minimapCamera.useOcclusionCulling = false;
        minimapCamera.cullingMask = ~0;

        renderTexture = new RenderTexture(renderTextureSize, renderTextureSize, 16);
        renderTexture.name = "MinimapRT";
        minimapCamera.targetTexture = renderTexture;

        if (useReplacementShader)
            minimapCamera.enabled = false;

        if (!useReplacementShader)
        {
            GameObject lightGo = new GameObject("MinimapLight");
            lightGo.transform.SetParent(minimapCamera.transform);
            lightGo.transform.localPosition = Vector3.zero;
            lightGo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            minimapLight = lightGo.AddComponent<Light>();
            minimapLight.type = LightType.Directional;
            minimapLight.intensity = 2.5f;
            minimapLight.color = Color.white;
            minimapLight.enabled = false;
            Camera.onPreRender += OnCameraPreRender;
            Camera.onPostRender += OnCameraPostRender;
        }
    }

    void OnCameraPreRender(Camera cam)
    {
        if (cam == minimapCamera && minimapLight != null)
            minimapLight.enabled = true;
    }

    void OnCameraPostRender(Camera cam)
    {
        if (cam == minimapCamera && minimapLight != null)
            minimapLight.enabled = false;
    }

    void BuildFogTexture()
    {
        fogTexture = new Texture2D(FogResolution, FogResolution, TextureFormat.RGBA32, false);
        fogTexture.wrapMode = TextureWrapMode.Clamp;
        fogTexture.filterMode = FilterMode.Bilinear;
        Color[] fog = new Color[FogResolution * FogResolution];
        Color hidden = new Color(0f, 0f, 0f, 1f);
        for (int i = 0; i < fog.Length; i++) fog[i] = hidden;
        fogTexture.SetPixels(fog);
        fogTexture.Apply(true, false);
    }

    void BuildUI()
    {
        canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            GameObject canvasGo = new GameObject("MinimapCanvas");
            canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasGo.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1920, 1080);
            canvasGo.AddComponent<GraphicRaycaster>();
        }

        GameObject panelGo = new GameObject("MinimapPanel");
        panelGo.transform.SetParent(canvas.transform, false);
        RectTransform panelRect = panelGo.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(1f, 0f);
        panelRect.anchorMax = new Vector2(1f, 0f);
        panelRect.pivot = new Vector2(1f, 0f);
        panelRect.anchoredPosition = new Vector2(-20f, 20f);
        panelRect.sizeDelta = new Vector2(minimapSizePixels, minimapSizePixels);

        Image panelBg = panelGo.AddComponent<Image>();
        panelBg.color = new Color(0.08f, 0.08f, 0.12f, 0.95f);

        GameObject mapGo = new GameObject("MinimapImage");
        mapGo.transform.SetParent(panelGo.transform, false);
        RawImage mapImage = mapGo.AddComponent<RawImage>();
        mapImage.texture = renderTexture;
        mapImage.color = Color.white;
        RectTransform mapRect = mapGo.GetComponent<RectTransform>();
        mapRect.anchorMin = Vector2.zero;
        mapRect.anchorMax = Vector2.one;
        mapRect.offsetMin = Vector2.zero;
        mapRect.offsetMax = Vector2.zero;
        minimapRect = mapRect;

        GameObject fogGo = new GameObject("MinimapFog");
        fogGo.transform.SetParent(panelGo.transform, false);
        RawImage fogImage = fogGo.AddComponent<RawImage>();
        fogImage.texture = fogTexture;
        fogImage.color = Color.white;
        RectTransform fogRect = fogGo.GetComponent<RectTransform>();
        fogRect.anchorMin = Vector2.zero;
        fogRect.anchorMax = Vector2.one;
        fogRect.offsetMin = Vector2.zero;
        fogRect.offsetMax = Vector2.zero;

        playerIconRect = CreateArrowIcon(panelGo.transform, "PlayerIcon", playerColor);
        enemyIconRect = CreateIcon(panelGo.transform, "EnemyIcon", enemyColor, Vector2.zero);

        for (int i = 0; i < 3; i++)
        {
            keyIcons[i] = CreateIcon(panelGo.transform, "KeyIcon" + i, keyColor, Vector2.zero).GetComponent<Image>();
        }
    }

    RectTransform CreateArrowIcon(Transform parent, string name, Color color)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        Image img = go.AddComponent<Image>();
        img.color = color;
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        float w = iconSizePixels * 0.7f;
        rect.sizeDelta = new Vector2(w, playerArrowLength);
        return rect;
    }

    RectTransform CreateIcon(Transform parent, string name, Color color, Vector2 pos)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        Image img = go.AddComponent<Image>();
        img.color = color;
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = pos;
        rect.sizeDelta = new Vector2(iconSizePixels, iconSizePixels);
        return rect;
    }

    void LateUpdate()
    {
        if (!uiBuilt || minimapCamera == null) return;

        if (player == null)
        {
            ResolveReferences();
            if (player == null) return;
        }

        Vector3 playerPos = player.position;
        minimapCamera.transform.position = new Vector3(playerPos.x, playerPos.y + cameraHeight, playerPos.z);

        if (useReplacementShader && replacementShader != null)
            minimapCamera.RenderWithShader(replacementShader, "RenderType");

        UpdateFog(playerPos.x, playerPos.z);
        UpdateIconPositions(playerPos);
    }

    void UpdateFog(float playerX, float playerZ)
    {
        if (fogTexture == null) return;

        float worldW = worldMaxX - worldMinX;
        float worldD = worldMaxZ - worldMinZ;
        if (worldW <= 0 || worldD <= 0) return;

        float u = (playerX - worldMinX) / worldW;
        float v = (playerZ - worldMinZ) / worldD;
        int cx = Mathf.RoundToInt(u * (FogResolution - 1));
        int cy = Mathf.RoundToInt(v * (FogResolution - 1));

        float radiusWorld = revealRadiusWorld;
        float radiusU = radiusWorld / worldW * FogResolution;
        float radiusV = radiusWorld / worldD * FogResolution;
        int rPixels = Mathf.CeilToInt(Mathf.Max(radiusU, radiusV));

        Color revealed = new Color(0f, 0f, 0f, 0f);
        int x0 = Mathf.Clamp(cx - rPixels, 0, FogResolution - 1);
        int x1 = Mathf.Clamp(cx + rPixels, 0, FogResolution - 1);
        int y0 = Mathf.Clamp(cy - rPixels, 0, FogResolution - 1);
        int y1 = Mathf.Clamp(cy + rPixels, 0, FogResolution - 1);

        for (int y = y0; y <= y1; y++)
        {
            for (int x = x0; x <= x1; x++)
            {
                float px = (x - cx) / radiusU;
                float py = (y - cy) / radiusV;
                if (px * px + py * py <= 1f)
                    fogTexture.SetPixel(x, y, revealed);
            }
        }
        fogTexture.Apply(false, false);
    }

    void UpdateIconPositions(Vector3 playerPos)
    {
        float halfW = minimapSizePixels * 0.5f;
        float scale = halfW / orthoSize;

        if (playerIconRect != null)
        {
            playerIconRect.anchoredPosition = Vector2.zero;
            Vector3 forward = player.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude > 0.001f)
            {
                float angleDeg = -Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
                playerIconRect.localEulerAngles = new Vector3(0f, 0f, angleDeg);
            }
        }

        if (stalker != null && enemyIconRect != null)
        {
            Vector2 local = WorldToMinimapLocal(stalker.position, playerPos, scale);
            enemyIconRect.anchoredPosition = ClampToMinimap(local, halfW);
            enemyIconRect.gameObject.SetActive(true);
        }
        else if (enemyIconRect != null)
        {
            enemyIconRect.gameObject.SetActive(false);
        }

        RefreshKeyItems();
        for (int i = 0; i < 3; i++)
        {
            if (keyIcons[i] == null) continue;
            if (keyItems != null && i < keyItems.Length && keyItems[i] != null && keyItems[i].gameObject.activeInHierarchy)
            {
                Vector2 local = WorldToMinimapLocal(keyItems[i].transform.position, playerPos, scale);
                keyIcons[i].rectTransform.anchoredPosition = ClampToMinimap(local, halfW);
                keyIcons[i].gameObject.SetActive(true);
            }
            else
            {
                keyIcons[i].gameObject.SetActive(false);
            }
        }
    }

    static Vector2 WorldToMinimapLocal(Vector3 world, Vector3 playerPos, float scale)
    {
        float dx = world.x - playerPos.x;
        float dz = world.z - playerPos.z;
        return new Vector2(dx * scale, dz * scale);
    }

    static Vector2 ClampToMinimap(Vector2 local, float halfSize)
    {
        float max = halfSize - 4f;
        if (local.sqrMagnitude > max * max)
            return local.normalized * max;
        return local;
    }

    void OnDestroy()
    {
        Camera.onPreRender -= OnCameraPreRender;
        Camera.onPostRender -= OnCameraPostRender;
        if (renderTexture != null && renderTexture.IsCreated())
            renderTexture.Release();
        if (fogTexture != null)
            Destroy(fogTexture);
    }
}
