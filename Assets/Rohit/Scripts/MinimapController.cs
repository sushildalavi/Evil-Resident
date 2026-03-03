using UnityEngine;
using UnityEngine.UI;
using Sushil.AI;
using Sushil.Systems;
using System.Collections.Generic;

/// <summary>
/// Valorant-style minimap: clear layout (walls/floor), player facing, enemy and keys.
/// Uses replacement shader for readable geometry when available; otherwise bright overhead light.
/// </summary>
public class MinimapController : MonoBehaviour
{
    [Header("Master Switch")]
    public bool enableMinimap = false;

    [Header("Bounds (world XZ)")]
    public float worldMinX = -60f;
    public float worldMaxX = 60f;
    public float worldMinZ = -60f;
    public float worldMaxZ = 60f;
    public bool autoFitWorldBoundsFromScene = true;
    public float mapPaddingWorld = 2f;

    [Header("Minimap camera")]
    public float cameraHeight = 50f;
    public float orthoSize = 28f;
    public int renderTextureSize = 384;
    public bool showWholeSceneLayout = true;
    public bool useReadableReplacementShader = true;

    [Header("Fog of war")]
    public float revealRadiusWorld = 8f;
    public bool useFogOfWar = false;

    [Header("UI")]
    public float minimapSizePixels = 200f;
    public float iconSizePixels = 10f;
    public float playerArrowLength = 14f;
    public Color playerColor = new Color(0.2f, 0.6f, 1f, 1f);
    public Color enemyColor = new Color(1f, 0.2f, 0.2f, 1f);
    public Color keyColor = new Color(1f, 0.85f, 0.2f, 1f);

    [Header("Layout Markers (Doors Only)")]
    public bool showDoorMarkers = true;
    public float doorMarkerLength = 12f;
    public float doorMarkerThickness = 3f;
    public Color doorMarkerColor = new Color(0.62f, 0.86f, 1f, 0.95f);
    public Color escapeDoorMarkerColor = new Color(0.48f, 1f, 0.62f, 1f);

    [Header("Timed Visibility")]
    public bool showOnlyWhileHidden = true;
    public bool showInShortIntervals = true;
    public float firstRevealDelay = 4f;
    public float visibleDuration = 2.8f;
    public float hiddenDuration = 10f;
    public float fadeSpeed = 5f;

    Transform player;
    Transform stalker;
    KeyItem[] keyItems;
    Camera minimapCamera;
    RenderTexture renderTexture;
    Texture2D fogTexture;
    RectTransform minimapRect;
    CanvasGroup panelGroup;
    RectTransform panelRect;
    RectTransform playerIconRect;
    RectTransform enemyIconRect;
    Image[] keyIcons = new Image[3];
    readonly List<Door> doorList = new();
    readonly List<RectTransform> doorMarkerRects = new();
    MainDoor mainDoor;
    RectTransform escapeDoorMarkerRect;
    Canvas canvas;
    bool uiBuilt;
    Light minimapLight;
    Shader replacementShader;
    bool useReplacementShader;
    float nextRevealAt;
    float hideAt;
    bool visibleNow;

    const int FogResolution = 256;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void EnsureMinimapOnPlayer()
    {
        // Minimap disabled intentionally.
    }

    void Start()
    {
        if (!enableMinimap)
        {
            enabled = false;
            return;
        }

        // Force stable readable style for this project.
        showWholeSceneLayout = true;
        useReadableReplacementShader = true;
        useFogOfWar = false;
        showDoorMarkers = true;
        showOnlyWhileHidden = true;

        ResolveReferences();
        if (autoFitWorldBoundsFromScene)
            AutoFitWorldBoundsFromScene();
        BuildMinimapCamera();
        BuildFogTexture();
        BuildUI();
        uiBuilt = true;
        nextRevealAt = Time.unscaledTime + Mathf.Max(0f, firstRevealDelay);
        hideAt = 0f;
        visibleNow = !showInShortIntervals;
        SetPanelAlpha(visibleNow ? 1f : 0f);
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
        mainDoor = FindFirstObjectByType<MainDoor>();
        RefreshDoors();
    }

    void RefreshDoors()
    {
        doorList.Clear();
        Door[] allDoors = FindObjectsByType<Door>(FindObjectsSortMode.None);
        if (allDoors == null) return;

        for (int i = 0; i < allDoors.Length; i++)
        {
            if (allDoors[i] != null && allDoors[i].gameObject.activeInHierarchy)
                doorList.Add(allDoors[i]);
        }
    }

    void BuildMinimapCamera()
    {
        if (player == null) return;

        replacementShader = Resources.Load<Shader>("Shaders/MinimapReplacement");
        useReplacementShader = useReadableReplacementShader && replacementShader != null;

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
        panelRect = panelGo.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(1f, 0f);
        panelRect.anchorMax = new Vector2(1f, 0f);
        panelRect.pivot = new Vector2(1f, 0f);
        panelRect.anchoredPosition = new Vector2(-26f, 26f);
        panelRect.sizeDelta = new Vector2(minimapSizePixels, minimapSizePixels);
        panelGroup = panelGo.AddComponent<CanvasGroup>();

        Image panelBg = panelGo.AddComponent<Image>();
        panelBg.color = new Color(0.03f, 0.04f, 0.06f, 0.92f);

        GameObject frameGo = new GameObject("Frame");
        frameGo.transform.SetParent(panelGo.transform, false);
        Image frame = frameGo.AddComponent<Image>();
        frame.color = new Color(0.72f, 0.78f, 0.9f, 0.2f);
        RectTransform frameRect = frame.rectTransform;
        frameRect.anchorMin = Vector2.zero;
        frameRect.anchorMax = Vector2.one;
        frameRect.offsetMin = new Vector2(-2f, -2f);
        frameRect.offsetMax = new Vector2(2f, 2f);
        frame.raycastTarget = false;

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
        fogGo.SetActive(useFogOfWar);

        if (showDoorMarkers)
            BuildDoorMarkers(panelGo.transform);
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

    void BuildDoorMarkers(Transform parent)
    {
        ClearDoorMarkers();
        RefreshDoors();

        for (int i = 0; i < doorList.Count; i++)
        {
            RectTransform r = CreateDoorMarker(parent, "DoorMarker_" + i, doorMarkerColor);
            doorMarkerRects.Add(r);
        }

        if (mainDoor != null)
            escapeDoorMarkerRect = CreateDoorMarker(parent, "EscapeDoorMarker", escapeDoorMarkerColor);
    }

    RectTransform CreateDoorMarker(Transform parent, string name, Color color)
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
        rect.sizeDelta = new Vector2(doorMarkerLength, doorMarkerThickness);
        return rect;
    }

    void ClearDoorMarkers()
    {
        for (int i = 0; i < doorMarkerRects.Count; i++)
        {
            if (doorMarkerRects[i] != null)
                Destroy(doorMarkerRects[i].gameObject);
        }
        doorMarkerRects.Clear();
        if (escapeDoorMarkerRect != null)
            Destroy(escapeDoorMarkerRect.gameObject);
        escapeDoorMarkerRect = null;
    }

    void LateUpdate()
    {
        if (!uiBuilt || minimapCamera == null) return;

        UpdateTimedVisibility();

        if (player == null)
        {
            ResolveReferences();
            if (player == null) return;
        }

        Vector3 playerPos = player.position;
        if (showWholeSceneLayout)
        {
            float cx = (worldMinX + worldMaxX) * 0.5f;
            float cz = (worldMinZ + worldMaxZ) * 0.5f;
            float w = Mathf.Max(1f, worldMaxX - worldMinX);
            float d = Mathf.Max(1f, worldMaxZ - worldMinZ);
            minimapCamera.orthographicSize = Mathf.Max(w, d) * 0.5f + Mathf.Max(0f, mapPaddingWorld);
            minimapCamera.transform.position = new Vector3(cx, cameraHeight, cz);
        }
        else
        {
            minimapCamera.orthographicSize = orthoSize;
            minimapCamera.transform.position = new Vector3(playerPos.x, playerPos.y + cameraHeight, playerPos.z);
        }

        if (useReplacementShader && replacementShader != null)
            minimapCamera.RenderWithShader(replacementShader, "RenderType");

        if (useFogOfWar)
            UpdateFog(playerPos.x, playerPos.z);
        UpdateIconPositions(playerPos);
        UpdateDoorMarkers();
    }

    void UpdateTimedVisibility()
    {
        if (panelGroup == null) return;

        bool overlayBlocking = StartScreenOverlay.IsShowing || PauseOverlay.IsPaused || GameOverOverlay.IsShowing || EscapeOverlay.IsShowing;
        if (overlayBlocking)
        {
            visibleNow = false;
            SetPanelAlpha(0f);
            return;
        }

        if (showOnlyWhileHidden)
        {
            bool isHidden = false;
            if (player != null)
            {
                RohitFPSController rohit = player.GetComponent<RohitFPSController>();
                if (rohit != null) isHidden = rohit.isHidden;
            }
            visibleNow = isHidden;
            float targetHidden = visibleNow ? 1f : 0f;
            panelGroup.alpha = Mathf.MoveTowards(panelGroup.alpha, targetHidden, Mathf.Max(0.5f, fadeSpeed) * Time.unscaledDeltaTime);
            return;
        }

        if (!showInShortIntervals)
        {
            visibleNow = true;
            SetPanelAlpha(1f);
            return;
        }

        float now = Time.unscaledTime;

        if (!visibleNow && now >= nextRevealAt)
        {
            visibleNow = true;
            hideAt = now + Mathf.Max(0.5f, visibleDuration);
        }
        else if (visibleNow && now >= hideAt)
        {
            visibleNow = false;
            nextRevealAt = now + Mathf.Max(0.5f, hiddenDuration);
        }

        float target = visibleNow ? 1f : 0f;
        panelGroup.alpha = Mathf.MoveTowards(panelGroup.alpha, target, Mathf.Max(0.5f, fadeSpeed) * Time.unscaledDeltaTime);
    }

    void SetPanelAlpha(float a)
    {
        if (panelGroup != null)
            panelGroup.alpha = Mathf.Clamp01(a);
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
        float scale = halfW / Mathf.Max(1f, minimapCamera.orthographicSize);

        if (playerIconRect != null)
            playerIconRect.gameObject.SetActive(false);

        if (stalker != null && enemyIconRect != null)
            enemyIconRect.gameObject.SetActive(false);
        else if (enemyIconRect != null)
            enemyIconRect.gameObject.SetActive(false);

        for (int i = 0; i < keyIcons.Length; i++)
        {
            if (keyIcons[i] != null) keyIcons[i].gameObject.SetActive(false);
        }
    }

    static Vector2 WorldToMinimapLocal(Vector3 world, Vector3 playerPos, float scale)
    {
        float dx = world.x - playerPos.x;
        float dz = world.z - playerPos.z;
        return new Vector2(dx * scale, dz * scale);
    }

    Vector2 WorldToMinimapAbsolute(Vector3 world)
    {
        float u = Mathf.InverseLerp(worldMinX, worldMaxX, world.x);
        float v = Mathf.InverseLerp(worldMinZ, worldMaxZ, world.z);
        float x = (u - 0.5f) * minimapSizePixels;
        float y = (v - 0.5f) * minimapSizePixels;
        return new Vector2(x, y);
    }

    static Vector2 ClampToMinimap(Vector2 local, float halfSize)
    {
        float max = halfSize - 4f;
        if (local.sqrMagnitude > max * max)
            return local.normalized * max;
        return local;
    }

    void UpdateDoorMarkers()
    {
        if (!showDoorMarkers) return;
        if (doorMarkerRects.Count != doorList.Count)
            BuildDoorMarkers(panelRect != null ? panelRect : canvas.transform);

        float halfW = minimapSizePixels * 0.5f;
        for (int i = 0; i < doorList.Count; i++)
        {
            Door door = doorList[i];
            RectTransform marker = i < doorMarkerRects.Count ? doorMarkerRects[i] : null;
            if (door == null || marker == null)
                continue;

            Vector2 p = WorldToMinimapAbsolute(door.transform.position);
            marker.anchoredPosition = ClampToMinimap(p, halfW);

            Vector3 f = door.transform.forward;
            f.y = 0f;
            if (f.sqrMagnitude > 0.0001f)
            {
                float angle = -Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg;
                marker.localEulerAngles = new Vector3(0f, 0f, angle);
            }
        }

        if (escapeDoorMarkerRect != null && mainDoor != null)
        {
            Vector2 p = WorldToMinimapAbsolute(mainDoor.transform.position);
            escapeDoorMarkerRect.anchoredPosition = ClampToMinimap(p, halfW);
            Vector3 f = mainDoor.transform.forward;
            f.y = 0f;
            if (f.sqrMagnitude > 0.0001f)
            {
                float angle = -Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg;
                escapeDoorMarkerRect.localEulerAngles = new Vector3(0f, 0f, angle);
            }
            escapeDoorMarkerRect.sizeDelta = new Vector2(doorMarkerLength * 1.35f, doorMarkerThickness * 1.35f);
        }
    }

    void AutoFitWorldBoundsFromScene()
    {
        Renderer[] all = FindObjectsByType<Renderer>(FindObjectsSortMode.None);
        bool init = false;
        Bounds b = default;

        for (int i = 0; i < all.Length; i++)
        {
            Renderer r = all[i];
            if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) continue;
            if (r.GetComponentInParent<Canvas>() != null) continue;
            if (r is ParticleSystemRenderer) continue;
            if (!init)
            {
                b = r.bounds;
                init = true;
            }
            else
            {
                b.Encapsulate(r.bounds);
            }
        }

        if (!init) return;

        worldMinX = b.min.x;
        worldMaxX = b.max.x;
        worldMinZ = b.min.z;
        worldMaxZ = b.max.z;
    }

    void OnDestroy()
    {
        Camera.onPreRender -= OnCameraPreRender;
        Camera.onPostRender -= OnCameraPostRender;
        if (renderTexture != null && renderTexture.IsCreated())
            renderTexture.Release();
        if (fogTexture != null)
            Destroy(fogTexture);
        ClearDoorMarkers();
    }
}
