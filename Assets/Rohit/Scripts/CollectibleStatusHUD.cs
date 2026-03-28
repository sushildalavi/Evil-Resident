using System.Collections.Generic;
using Sushil.Systems;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class CollectibleStatusHUD : MonoBehaviour
{
    const float RefreshInterval = 0.35f;
    const float RootPadding = 12f;
    const float KeyTokenWidth = 92f;
    const float KeyTokenHeight = 58f;
    const float KeyTokenGap = 10f;
    const float FuseTokenWidth = 50f;
    const float FuseTokenHeight = 66f;
    const float FuseTokenGap = 10f;

    static readonly KeyType[] KeyOrder =
    {
        KeyType.Circle,
        KeyType.Rectangle,
        KeyType.Square
    };

    static readonly Color[] KeyAccentColors =
    {
        new Color(1f, 0.34f, 0.34f, 1f),
        new Color(0.36f, 1f, 0.46f, 1f),
        new Color(0.32f, 0.72f, 1f, 1f)
    };

    static readonly Color[] FuseAccentColors =
    {
        new Color(1f, 0.86f, 0.30f, 1f),
        new Color(0.36f, 0.84f, 1f, 1f),
        new Color(0.70f, 0.38f, 1f, 1f)
    };

    static CollectibleStatusHUD instance;
    static Font cachedFont;

    Canvas canvas;
    RectTransform rootRect;
    Text titleText;
    Text subtitleText;
    Image rootGlow;

    SectionView keySection;
    SectionView fuseSection;

    readonly List<KeyTokenView> keyTokens = new List<KeyTokenView>();
    readonly List<FuseTokenView> fuseTokens = new List<FuseTokenView>();

    float nextRefreshAt;
    int targetKeyCount;
    int targetFuseCount;

    public static bool Exists => instance != null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (instance == null)
        {
            var go = new GameObject("CollectibleStatusHUD");
            instance = go.AddComponent<CollectibleStatusHUD>();
        }
        else
        {
            instance.MarkDirty();
        }
    }

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;
        BuildUI();
        MarkDirty();
    }

    void OnDestroy()
    {
        if (instance == this)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            instance = null;
        }
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        MarkDirty();
    }

    void Update()
    {
        if (Time.unscaledTime >= nextRefreshAt)
            RefreshTargets();

        UpdateVisibility();
        UpdateVisuals();
    }

    void MarkDirty()
    {
        nextRefreshAt = 0f;
    }

    void BuildUI()
    {
        canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue - 36;

        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        gameObject.AddComponent<GraphicRaycaster>();

        rootRect = CreatePanel("CollectibleRoot", transform, new Color(0.04f, 0.05f, 0.09f, 0.92f));
        rootRect.anchorMin = new Vector2(0f, 1f);
        rootRect.anchorMax = new Vector2(0f, 1f);
        rootRect.pivot = new Vector2(0f, 1f);
        rootRect.anchoredPosition = new Vector2(22f, -22f);
        rootRect.sizeDelta = new Vector2(420f, 188f);

        AddOutline(rootRect.gameObject, new Color(0f, 0f, 0f, 0.55f), new Vector2(1f, -1f));
        AddShadow(rootRect.gameObject, new Color(0f, 0f, 0f, 0.60f), new Vector2(4f, -4f));

        rootGlow = CreateImage("RootGlow", rootRect, new Color(0.55f, 0.80f, 1f, 0.12f));
        Stretch(rootGlow.rectTransform, 3f, 3f, 3f, 3f);

        Image headerGlow = CreateImage("HeaderGlow", rootRect, new Color(0.35f, 0.64f, 1f, 0.10f));
        SetTopStretch(headerGlow.rectTransform, 4f, 4f, 4f, 34f);

        Image headerLine = CreateImage("HeaderLine", rootRect, new Color(0.80f, 0.88f, 1f, 0.18f));
        SetTopLeft(headerLine.rectTransform, RootPadding, 50f, 268f, 2f);

        titleText = CreateText("Title", rootRect, "OBJECTIVE TRACKER", 24, FontStyle.Bold, TextAnchor.MiddleLeft,
            new Color(0.96f, 0.98f, 1f, 0.99f));
        SetTopLeft(titleText.rectTransform, RootPadding, 10f, 220f, 24f);
        AddShadow(titleText.gameObject, new Color(0f, 0f, 0f, 0.75f), new Vector2(2f, -2f));

        subtitleText = CreateText("Subtitle", rootRect, "RGB KEYS  |  POWER CELLS", 11, FontStyle.Bold, TextAnchor.MiddleLeft,
            new Color(0.70f, 0.82f, 0.96f, 0.88f));
        SetTopLeft(subtitleText.rectTransform, RootPadding, 34f, 220f, 14f);

        CreateHeaderChip("ChipRed", 116f, KeyAccentColors[0]);
        CreateHeaderChip("ChipGreen", 86f, KeyAccentColors[1]);
        CreateHeaderChip("ChipBlue", 56f, KeyAccentColors[2]);

        keySection = CreateSection("Keys", "KEYS");
        fuseSection = CreateSection("Fuses", "FUSES");
        keySection.accentBar.color = new Color(0.95f, 0.52f, 0.52f, 0.92f);
        fuseSection.accentBar.color = new Color(0.56f, 0.78f, 1f, 0.90f);

        BuildKeyTokens();
        RebuildFuseTokens(3);
    }

    void RefreshTargets()
    {
        nextRefreshAt = Time.unscaledTime + RefreshInterval;

        PlayerInventory inventory = GetInventory();
        int liveKeys = CountActiveComponents<KeyItem>();
        int liveFuses = CountUniqueFuseObjects();

        int collectedKeys = inventory != null ? inventory.KeyCount : 0;
        int collectedFuses = inventory != null ? inventory.TotalFusesCollected : 0;

        targetKeyCount = Mathf.Max(collectedKeys + liveKeys, 0);
        targetFuseCount = Mathf.Max(collectedFuses + liveFuses, 0);

        int desiredFuseTokens = Mathf.Max(targetFuseCount, 3);
        if (fuseTokens.Count != desiredFuseTokens)
            RebuildFuseTokens(desiredFuseTokens);

        LayoutSections();
    }

    void UpdateVisibility()
    {
        bool hasCollectibles = targetKeyCount > 0 || targetFuseCount > 0;
        if (rootRect != null)
            rootRect.gameObject.SetActive(hasCollectibles);

        if (canvas == null) return;
        canvas.enabled = hasCollectibles && !ShouldHide();
    }

    void UpdateVisuals()
    {
        if (rootRect == null || !rootRect.gameObject.activeSelf) return;

        PlayerInventory inventory = GetInventory();
        int collectedKeys = inventory != null ? inventory.KeyCount : 0;
        int collectedFuses = inventory != null ? inventory.TotalFusesCollected : 0;
        int carryingFuses = inventory != null ? inventory.FuseCount : 0;

        float rootPulse = 0.92f + 0.08f * Mathf.Sin(Time.unscaledTime * 1.7f);
        if (rootGlow != null)
            rootGlow.color = new Color(0.55f, 0.80f, 1f, 0.08f + (0.03f * rootPulse));
        if (subtitleText != null)
            subtitleText.color = new Color(0.72f, 0.84f, 0.98f, 0.76f + (0.12f * rootPulse));

        if (keySection.root != null)
            keySection.root.gameObject.SetActive(targetKeyCount > 0);
        if (fuseSection.root != null)
            fuseSection.root.gameObject.SetActive(targetFuseCount > 0);

        if (keySection.summaryText != null)
            keySection.summaryText.text = $"{collectedKeys}/{targetKeyCount} COLLECTED";

        if (fuseSection.summaryText != null)
        {
            string carrySuffix = carryingFuses > 0 ? $"  |  CARRY {carryingFuses}" : string.Empty;
            fuseSection.summaryText.text = $"{collectedFuses}/{targetFuseCount} COLLECTED{carrySuffix}";
        }

        for (int i = 0; i < keyTokens.Count; i++)
        {
            KeyTokenView token = keyTokens[i];
            bool collected = inventory != null && inventory.HasKey(token.keyType);
            float pulse = 0.88f + 0.12f * Mathf.Sin((Time.unscaledTime * 3.0f) + (i * 0.7f));
            ApplyKeyStyle(token, collected, KeyAccentColors[i], pulse);
        }

        int installedFuses = Mathf.Max(0, collectedFuses - carryingFuses);
        for (int i = 0; i < fuseTokens.Count; i++)
        {
            FuseState state = FuseState.Missing;
            if (i < installedFuses) state = FuseState.Installed;
            else if (i < collectedFuses) state = FuseState.Carried;

            float pulse = 0.84f + 0.16f * Mathf.Sin((Time.unscaledTime * 3.6f) + (i * 0.6f));
            Color accent = FuseAccentColors[i % FuseAccentColors.Length];
            ApplyFuseStyle(fuseTokens[i], state, accent, pulse);
        }
    }

    void LayoutSections()
    {
        float keyAreaWidth = (KeyTokenWidth * keyTokens.Count) + (KeyTokenGap * Mathf.Max(0, keyTokens.Count - 1));
        float fuseAreaWidth = (FuseTokenWidth * fuseTokens.Count) + (FuseTokenGap * Mathf.Max(0, fuseTokens.Count - 1));
        float contentWidth = Mathf.Max(keyAreaWidth, fuseAreaWidth);
        float rootWidth = Mathf.Max(430f, 148f + contentWidth + RootPadding);

        SetTopLeft(titleText.rectTransform, RootPadding, 10f, rootWidth - (RootPadding * 2f), 24f);
        SetTopLeft(subtitleText.rectTransform, RootPadding, 34f, rootWidth - 180f, 14f);

        float top = 60f;
        float keyHeight = 68f;
        float fuseHeight = 86f;

        if (targetKeyCount > 0)
        {
            LayoutSection(keySection, top, rootWidth - (RootPadding * 2f), keyHeight, keyAreaWidth, KeyTokenHeight);
            top += keyHeight + 8f;
        }

        if (targetFuseCount > 0)
        {
            LayoutSection(fuseSection, top, rootWidth - (RootPadding * 2f), fuseHeight, fuseAreaWidth, FuseTokenHeight);
            top += fuseHeight + 8f;
        }

        rootRect.sizeDelta = new Vector2(rootWidth, Mathf.Max(112f, top + 4f));
    }

    void LayoutSection(SectionView section, float top, float width, float height, float tokenAreaWidth, float tokenAreaHeight)
    {
        SetTopLeft(section.root, RootPadding, top, width, height);
        SetTopLeft(section.titleText.rectTransform, 12f, 10f, 118f, 18f);
        SetTopLeft(section.summaryText.rectTransform, 12f, 32f, 124f, 18f);
        SetTopLeft(section.tokenHost, 138f, Mathf.Max(5f, (height - tokenAreaHeight) * 0.5f), tokenAreaWidth, tokenAreaHeight);

        float x = 0f;
        if (section == keySection)
        {
            for (int i = 0; i < keyTokens.Count; i++)
            {
                SetTopLeft(keyTokens[i].root, x, 0f, KeyTokenWidth, KeyTokenHeight);
                x += KeyTokenWidth + KeyTokenGap;
            }
        }
        else
        {
            for (int i = 0; i < fuseTokens.Count; i++)
            {
                SetTopLeft(fuseTokens[i].root, x, 0f, FuseTokenWidth, FuseTokenHeight);
                x += FuseTokenWidth + FuseTokenGap;
            }
        }
    }

    SectionView CreateSection(string name, string title)
    {
        RectTransform rect = CreatePanel(name, rootRect, new Color(0.10f, 0.13f, 0.19f, 0.88f));
        AddOutline(rect.gameObject, new Color(0f, 0f, 0f, 0.45f), new Vector2(1f, -1f));

        Image accentBar = CreateImage("AccentBar", rect, Color.white);
        accentBar.rectTransform.anchorMin = new Vector2(0f, 0f);
        accentBar.rectTransform.anchorMax = new Vector2(0f, 1f);
        accentBar.rectTransform.pivot = new Vector2(0f, 0.5f);
        accentBar.rectTransform.anchoredPosition = Vector2.zero;
        accentBar.rectTransform.sizeDelta = new Vector2(4f, 0f);

        Image edgeLight = CreateImage("EdgeLight", rect, new Color(1f, 1f, 1f, 0.06f));
        SetTopLeft(edgeLight.rectTransform, 8f, 4f, 180f, 1f);

        Text sectionTitle = CreateText("Title", rect, title, 16, FontStyle.Bold, TextAnchor.MiddleLeft,
            new Color(0.91f, 0.95f, 1f, 0.96f));
        AddShadow(sectionTitle.gameObject, new Color(0f, 0f, 0f, 0.7f), new Vector2(2f, -2f));

        Text summary = CreateText("Summary", rect, string.Empty, 12, FontStyle.Bold, TextAnchor.MiddleLeft,
            new Color(0.70f, 0.78f, 0.88f, 0.92f));

        RectTransform tokenHost = new GameObject("TokenHost", typeof(RectTransform)).GetComponent<RectTransform>();
        tokenHost.SetParent(rect, false);
        tokenHost.anchorMin = new Vector2(0f, 1f);
        tokenHost.anchorMax = new Vector2(0f, 1f);
        tokenHost.pivot = new Vector2(0f, 1f);

        return new SectionView
        {
            root = rect,
            accentBar = accentBar,
            titleText = sectionTitle,
            summaryText = summary,
            tokenHost = tokenHost
        };
    }

    void BuildKeyTokens()
    {
        keyTokens.Clear();

        for (int i = 0; i < KeyOrder.Length; i++)
        {
            KeyType keyType = KeyOrder[i];
            RectTransform token = CreatePanel($"{keyType}Token", keySection.tokenHost, new Color(0.16f, 0.18f, 0.24f, 0.98f));
            Image halo = CreateImage("Halo", token, new Color(1f, 1f, 1f, 0.06f));
            Stretch(halo.rectTransform, -2f, -2f, -2f, -2f);
            Image face = CreateImage("Face", token, new Color(0.18f, 0.21f, 0.28f, 0.96f));
            Stretch(face.rectTransform, 3f, 3f, 3f, 3f);

            Image spine = CreateImage("Spine", token, KeyAccentColors[i]);
            SetTopLeft(spine.rectTransform, 6f, 7f, 5f, 44f);

            Image shadowStrip = CreateImage("ShadowStrip", token, new Color(0f, 0f, 0f, 0.22f));
            SetBottomStretch(shadowStrip.rectTransform, 3f, 3f, 3f, 7f);

            Image gloss = CreateImage("Gloss", token, new Color(1f, 1f, 1f, 0.10f));
            SetTopStretch(gloss.rectTransform, 4f, 4f, 4f, 10f);

            Text head = CreateText("Head", token, GetKeyHeadGlyph(keyType), 26, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Color(0.95f, 0.95f, 0.95f, 0.92f));
            SetTopLeft(head.rectTransform, 9f, 8f, 24f, 22f);

            Image shaft = CreateImage("Shaft", token, new Color(0.86f, 0.87f, 0.90f, 0.92f));
            SetTopLeft(shaft.rectTransform, 30f, 18f, 28f, 8f);

            Image toothA = CreateImage("ToothA", token, new Color(0.86f, 0.87f, 0.90f, 0.92f));
            SetTopLeft(toothA.rectTransform, 55f, 18f, 7f, 16f);

            Image toothB = CreateImage("ToothB", token, new Color(0.86f, 0.87f, 0.90f, 0.92f));
            SetTopLeft(toothB.rectTransform, 63f, 23f, 7f, 11f);

            Image labelPlate = CreateImage("LabelPlate", token, KeyAccentColors[i]);
            SetBottomStretch(labelPlate.rectTransform, 4f, 4f, 4f, 14f);

            Text label = CreateText("Label", token, GetKeyLabel(keyType), 10, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Color(0.86f, 0.89f, 0.94f, 0.9f));
            SetBottomStretch(label.rectTransform, 5f, 5f, 5f, 14f);

            keyTokens.Add(new KeyTokenView
            {
                keyType = keyType,
                root = token,
                plate = token.GetComponent<Image>(),
                halo = halo,
                face = face,
                spine = spine,
                gloss = gloss,
                head = head,
                shaft = shaft,
                toothA = toothA,
                toothB = toothB,
                labelPlate = labelPlate,
                label = label
            });
        }
    }

    void RebuildFuseTokens(int count)
    {
        for (int i = 0; i < fuseTokens.Count; i++)
        {
            if (fuseTokens[i].root != null)
                Destroy(fuseTokens[i].root.gameObject);
        }

        fuseTokens.Clear();

        for (int i = 0; i < count; i++)
        {
            RectTransform token = CreatePanel($"Fuse_{i}", fuseSection.tokenHost, new Color(0.15f, 0.17f, 0.23f, 0.98f));
            Image glow = CreateImage("Glow", token, new Color(1f, 1f, 1f, 0.06f));
            Stretch(glow.rectTransform, -2f, -2f, -2f, -2f);
            Image face = CreateImage("Face", token, new Color(0.19f, 0.21f, 0.29f, 0.96f));
            Stretch(face.rectTransform, 3f, 3f, 3f, 3f);

            Image body = CreateImage("Body", token, new Color(0.72f, 0.74f, 0.80f, 0.85f));
            SetTopLeft(body.rectTransform, 14f, 15f, 18f, 30f);

            Image core = CreateImage("Core", token, new Color(0.98f, 0.98f, 1f, 0.88f));
            SetTopLeft(core.rectTransform, 19f, 18f, 8f, 24f);

            Image capTop = CreateImage("CapTop", token, new Color(0.84f, 0.86f, 0.92f, 0.86f));
            SetTopLeft(capTop.rectTransform, 10f, 9f, 26f, 8f);

            Image capBottom = CreateImage("CapBottom", token, new Color(0.58f, 0.60f, 0.68f, 0.86f));
            SetTopLeft(capBottom.rectTransform, 10f, 45f, 26f, 7f);

            Image spark = CreateImage("Spark", token, new Color(1f, 1f, 1f, 0.12f));
            SetTopLeft(spark.rectTransform, 20f, 13f, 6f, 12f);

            fuseTokens.Add(new FuseTokenView
            {
                root = token,
                plate = token.GetComponent<Image>(),
                glow = glow,
                face = face,
                body = body,
                core = core,
                capTop = capTop,
                capBottom = capBottom,
                spark = spark
            });
        }
    }

    void ApplyKeyStyle(KeyTokenView token, bool collected, Color accent, float pulse)
    {
        Color plateColor = collected
            ? Color.Lerp(new Color(0.14f, 0.16f, 0.22f, 0.98f), accent, 0.18f)
            : new Color(0.15f, 0.16f, 0.21f, 0.95f);
        Color faceColor = collected
            ? Color.Lerp(new Color(0.22f, 0.24f, 0.30f, 0.98f), accent, 0.34f)
            : new Color(0.20f, 0.22f, 0.28f, 0.94f);
        Color mutedAccent = Color.Lerp(accent, new Color(0.34f, 0.37f, 0.44f, 1f), 0.72f);
        Color glyphColor = collected
            ? Color.Lerp(accent, Color.white, 0.18f * pulse)
            : new Color(0.50f, 0.55f, 0.62f, 0.88f);

        token.plate.color = plateColor;
        token.halo.color = new Color(accent.r, accent.g, accent.b, collected ? 0.12f + (0.08f * pulse) : 0.05f);
        token.face.color = faceColor;
        token.spine.color = collected ? accent : mutedAccent;
        token.gloss.color = new Color(1f, 1f, 1f, collected ? 0.14f + (0.05f * pulse) : 0.05f);
        token.head.color = glyphColor;
        token.shaft.color = glyphColor;
        token.toothA.color = glyphColor;
        token.toothB.color = glyphColor;
        token.labelPlate.color = collected
            ? Color.Lerp(accent, Color.black, 0.28f)
            : Color.Lerp(mutedAccent, Color.black, 0.35f);
        token.label.color = collected
            ? new Color(0.98f, 0.99f, 1f, 0.96f)
            : new Color(0.72f, 0.76f, 0.82f, 0.86f);

        token.face.rectTransform.localScale = collected
            ? Vector3.one * (1f + ((pulse - 0.84f) * 0.03f))
            : Vector3.one;
    }

    void ApplyFuseStyle(FuseTokenView token, FuseState state, Color accent, float pulse)
    {
        Color plateColor;
        Color faceColor;
        Color bodyColor;
        Color coreColor;
        Color capTopColor;
        Color capBottomColor;
        float sparkAlpha;

        switch (state)
        {
            case FuseState.Installed:
                plateColor = Color.Lerp(new Color(0.14f, 0.18f, 0.22f, 0.98f), accent, 0.16f);
                faceColor = Color.Lerp(new Color(0.18f, 0.22f, 0.28f, 0.96f), accent, 0.32f);
                bodyColor = Color.Lerp(accent, Color.white, 0.14f * pulse);
                coreColor = Color.Lerp(Color.white, accent, 0.22f);
                capTopColor = Color.Lerp(accent, Color.white, 0.24f);
                capBottomColor = Color.Lerp(accent, Color.black, 0.25f);
                sparkAlpha = 0.18f + (0.08f * pulse);
                break;

            case FuseState.Carried:
                Color carryAccent = Color.Lerp(accent, new Color(1f, 0.93f, 0.62f, 1f), 0.35f);
                plateColor = Color.Lerp(new Color(0.16f, 0.18f, 0.23f, 0.98f), carryAccent, 0.14f);
                faceColor = Color.Lerp(new Color(0.20f, 0.22f, 0.29f, 0.96f), carryAccent, 0.22f);
                bodyColor = Color.Lerp(carryAccent, Color.white, 0.10f * pulse);
                coreColor = Color.Lerp(Color.white, carryAccent, 0.30f);
                capTopColor = Color.Lerp(carryAccent, Color.white, 0.18f);
                capBottomColor = Color.Lerp(carryAccent, Color.black, 0.30f);
                sparkAlpha = 0.10f + (0.05f * pulse);
                break;

            default:
                plateColor = new Color(0.15f, 0.16f, 0.21f, 0.95f);
                faceColor = new Color(0.19f, 0.20f, 0.27f, 0.94f);
                bodyColor = new Color(0.38f, 0.42f, 0.50f, 0.88f);
                coreColor = new Color(0.24f, 0.26f, 0.32f, 0.82f);
                capTopColor = new Color(0.54f, 0.58f, 0.66f, 0.86f);
                capBottomColor = new Color(0.30f, 0.34f, 0.42f, 0.90f);
                sparkAlpha = 0.03f;
                break;
        }

        token.plate.color = plateColor;
        token.glow.color = new Color(accent.r, accent.g, accent.b, state == FuseState.Missing ? 0.04f : 0.10f + (0.07f * pulse));
        token.face.color = faceColor;
        token.body.color = bodyColor;
        token.core.color = coreColor;
        token.capTop.color = capTopColor;
        token.capBottom.color = capBottomColor;
        token.spark.color = new Color(1f, 1f, 1f, sparkAlpha);

        token.core.rectTransform.localScale = state == FuseState.Missing
            ? Vector3.one
            : new Vector3(1f, 1f + ((pulse - 0.84f) * 0.06f), 1f);
    }

    PlayerInventory GetInventory()
    {
        if (PlayerInventory.instance != null)
            return PlayerInventory.instance;

        return FindFirstObjectByType<PlayerInventory>();
    }

    int CountActiveComponents<T>() where T : Component
    {
        T[] objects = FindObjectsByType<T>(FindObjectsSortMode.None);
        if (objects == null || objects.Length == 0) return 0;

        int count = 0;
        for (int i = 0; i < objects.Length; i++)
        {
            T obj = objects[i];
            if (obj != null && obj.gameObject.activeInHierarchy)
                count++;
        }

        return count;
    }

    int CountUniqueFuseObjects()
    {
        var ids = new HashSet<int>();
        RegisterFuseObjects(ids, FindObjectsByType<FusePickup>(FindObjectsSortMode.None));
        RegisterFuseObjects(ids, FindObjectsByType<FuseItem>(FindObjectsSortMode.None));
        RegisterFuseObjects(ids, FindObjectsByType<FuseInteract>(FindObjectsSortMode.None));
        return ids.Count;
    }

    void RegisterFuseObjects<T>(HashSet<int> ids, T[] fuseObjects) where T : Component
    {
        if (fuseObjects == null) return;

        for (int i = 0; i < fuseObjects.Length; i++)
        {
            T obj = fuseObjects[i];
            if (obj == null || !obj.gameObject.activeInHierarchy) continue;
            ids.Add(obj.gameObject.GetInstanceID());
        }
    }

    bool ShouldHide()
    {
        return StartScreenOverlay.IsShowing
            || PauseOverlay.IsPaused
            || GameOverOverlay.IsShowing
            || EscapeOverlay.IsShowing;
    }

    static RectTransform CreatePanel(string name, Transform parent, Color color)
    {
        RectTransform rect = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.localScale = Vector3.one;

        Image image = rect.GetComponent<Image>();
        image.color = color;
        return rect;
    }

    static Image CreateImage(string name, Transform parent, Color color)
    {
        RectTransform rect = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.localScale = Vector3.one;

        Image image = rect.GetComponent<Image>();
        image.color = color;
        return image;
    }

    static Text CreateText(string name, Transform parent, string text, int size, FontStyle style, TextAnchor anchor, Color color)
    {
        RectTransform rect = new GameObject(name, typeof(RectTransform), typeof(Text)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.localScale = Vector3.one;

        Text label = rect.GetComponent<Text>();
        label.font = GetFont();
        label.text = text;
        label.fontSize = size;
        label.fontStyle = style;
        label.alignment = anchor;
        label.color = color;
        label.resizeTextForBestFit = false;
        label.horizontalOverflow = HorizontalWrapMode.Overflow;
        label.verticalOverflow = VerticalWrapMode.Overflow;
        return label;
    }

    static Font GetFont()
    {
        if (cachedFont == null)
            cachedFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        return cachedFont;
    }

    static void Stretch(RectTransform rect, float left, float top, float right, float bottom)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
    }

    static void SetTopLeft(RectTransform rect, float x, float y, float width, float height)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(width, height);
    }

    static void SetTopRight(RectTransform rect, float right, float y, float width, float height)
    {
        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.anchoredPosition = new Vector2(-right, -y);
        rect.sizeDelta = new Vector2(width, height);
    }

    static void SetTopStretch(RectTransform rect, float left, float top, float right, float height)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -top);
        rect.offsetMin = new Vector2(left, -top - height);
        rect.offsetMax = new Vector2(-right, -top);
    }

    static void SetBottomStretch(RectTransform rect, float left, float bottom, float right, float height)
    {
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = new Vector2(0f, bottom);
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, bottom + height);
    }

    static void AddShadow(GameObject target, Color color, Vector2 distance)
    {
        Shadow shadow = target.AddComponent<Shadow>();
        shadow.effectColor = color;
        shadow.effectDistance = distance;
    }

    static void AddOutline(GameObject target, Color color, Vector2 distance)
    {
        Outline outline = target.AddComponent<Outline>();
        outline.effectColor = color;
        outline.effectDistance = distance;
    }

    static string GetKeyHeadGlyph(KeyType keyType)
    {
        switch (keyType)
        {
            case KeyType.Circle: return "O";
            case KeyType.Rectangle: return "▭";
            case KeyType.Square: return "□";
            default: return "?";
        }
    }

    static string GetKeyLabel(KeyType keyType)
    {
        switch (keyType)
        {
            case KeyType.Circle: return "CIRCLE";
            case KeyType.Rectangle: return "RECT";
            case KeyType.Square: return "SQUARE";
            default: return keyType.ToString().ToUpperInvariant();
        }
    }

    void CreateHeaderChip(string name, float right, Color color)
    {
        Image chip = CreateImage(name, rootRect, color);
        SetTopRight(chip.rectTransform, right, 18f, 22f, 7f);

        Image gloss = CreateImage($"{name}Gloss", chip.rectTransform, new Color(1f, 1f, 1f, 0.18f));
        SetTopStretch(gloss.rectTransform, 1f, 1f, 1f, 2f);
    }

    class SectionView
    {
        public RectTransform root;
        public Image accentBar;
        public Text titleText;
        public Text summaryText;
        public RectTransform tokenHost;
    }

    class KeyTokenView
    {
        public KeyType keyType;
        public RectTransform root;
        public Image plate;
        public Image halo;
        public Image face;
        public Image spine;
        public Image gloss;
        public Text head;
        public Image shaft;
        public Image toothA;
        public Image toothB;
        public Image labelPlate;
        public Text label;
    }

    class FuseTokenView
    {
        public RectTransform root;
        public Image plate;
        public Image glow;
        public Image face;
        public Image body;
        public Image core;
        public Image capTop;
        public Image capBottom;
        public Image spark;
    }

    enum FuseState
    {
        Missing,
        Carried,
        Installed
    }
}
