using System.Collections.Generic;
using Sushil.Systems;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class CollectibleStatusHUD : MonoBehaviour
{
    const float RefreshInterval = 0.35f;
    const float HudScale = 0.86f;
    const float RootPadding = 18f;
    const float KeyTokenWidth = 96f;
    const float KeyTokenHeight = 62f;
    const float KeyTokenGap = 12f;
    const float FuseTokenWidth = 54f;
    const float FuseTokenHeight = 72f;
    const float FuseTokenGap = 12f;
    const float SectionHeaderHeight = 36f;
    const float SectionDetailTop = 42f;
    const float SectionTokenTop = 62f;
    const float SectionMinWidth = 492f;

    static readonly KeyType[] KeyOrder =
    {
        KeyType.Circle,
        KeyType.Rectangle,
        KeyType.Square
    };

    static readonly Color CircleKeyAccent = new Color(0.32f, 0.72f, 1f, 1f);
    static readonly Color RectangleKeyAccent = new Color(0.36f, 1f, 0.46f, 1f);
    static readonly Color SquareKeyAccent = new Color(1f, 0.34f, 0.34f, 1f);
    static readonly Color RootShellColor = new Color(0.02f, 0.025f, 0.035f, 0.95f);
    static readonly Color RootCoreColor = new Color(0.055f, 0.06f, 0.075f, 0.96f);
    static readonly Color RootStrokeColor = new Color(0.80f, 0.84f, 0.92f, 0.07f);
    static readonly Color HeaderBandColor = new Color(0.09f, 0.10f, 0.13f, 0.94f);
    static readonly Color SectionBaseColor = new Color(0.055f, 0.06f, 0.075f, 0.94f);
    static readonly Color SectionInnerColor = new Color(0.085f, 0.09f, 0.11f, 0.76f);
    static readonly Color TokenShelfColor = new Color(0.03f, 0.035f, 0.045f, 0.92f);
    static readonly Color TextPrimaryColor = new Color(0.96f, 0.97f, 0.99f, 0.98f);
    static readonly Color TextSecondaryColor = new Color(0.70f, 0.75f, 0.82f, 0.88f);
    static readonly Color TextMutedColor = new Color(0.58f, 0.63f, 0.71f, 0.82f);

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
        if (!EnsureUIBuilt())
            return;

        if (Time.unscaledTime >= nextRefreshAt)
            RefreshTargets();

        UpdateVisibility();
        UpdateVisuals();
    }

    void MarkDirty()
    {
        nextRefreshAt = 0f;
    }

    bool EnsureUIBuilt()
    {
        if (HasValidUI())
            return true;

        if (TryRestoreUIReferences())
            return true;

        if (transform.Find("CollectibleRoot") != null)
            return false;

        BuildUI();
        return HasValidUI();
    }

    bool HasValidUI()
    {
        return canvas != null &&
               rootRect != null &&
               keySection != null &&
               keySection.root != null &&
               keySection.tokenHost != null &&
               fuseSection != null &&
               fuseSection.root != null &&
               fuseSection.tokenHost != null;
    }

    bool TryRestoreUIReferences()
    {
        canvas = GetComponent<Canvas>();
        rootRect = FindDirectChild<RectTransform>(transform, "CollectibleRoot");
        if (canvas == null || rootRect == null)
            return false;

        titleText = FindDirectChild<Text>(rootRect, "Title");
        subtitleText = FindDirectChild<Text>(rootRect, "Subtitle");
        rootGlow = FindDirectChild<Image>(rootRect, "RootGlow");
        keySection = RestoreSectionView("Keys");
        fuseSection = RestoreSectionView("Fuses");
        if (keySection == null || fuseSection == null)
            return false;

        RestoreKeyTokens();
        RestoreFuseTokens();
        return HasValidUI();
    }

    void BuildUI()
    {
        canvas = GetComponent<Canvas>();
        if (canvas == null)
            canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue - 36;

        var scaler = GetComponent<CanvasScaler>();
        if (scaler == null)
            scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        if (GetComponent<GraphicRaycaster>() == null)
            gameObject.AddComponent<GraphicRaycaster>();

        rootRect = CreatePanel("CollectibleRoot", transform, RootShellColor);
        rootRect.anchorMin = new Vector2(0f, 1f);
        rootRect.anchorMax = new Vector2(0f, 1f);
        rootRect.pivot = new Vector2(0f, 1f);
        rootRect.anchoredPosition = new Vector2(24f, -24f);
        rootRect.sizeDelta = new Vector2(496f, 248f);
        rootRect.localScale = Vector3.one * HudScale;

        AddOutline(rootRect.gameObject, new Color(0f, 0f, 0f, 0.72f), new Vector2(1f, -1f));
        AddShadow(rootRect.gameObject, new Color(0f, 0f, 0f, 0.72f), new Vector2(6f, -6f));

        Image rootCore = CreateImage("RootCore", rootRect, RootCoreColor);
        Stretch(rootCore.rectTransform, 4f, 4f, 4f, 4f);

        Image topBand = CreateImage("TopBand", rootRect, HeaderBandColor);
        SetTopStretch(topBand.rectTransform, 5f, 5f, 5f, 54f);

        Image topRim = CreateImage("TopRim", rootRect, RootStrokeColor);
        SetTopStretch(topRim.rectTransform, 10f, 11f, 10f, 1f);

        Image lowerBand = CreateImage("LowerBand", rootRect, new Color(0f, 0f, 0f, 0.16f));
        SetBottomStretch(lowerBand.rectTransform, 5f, 5f, 5f, 28f);

        rootGlow = CreateImage("RootGlow", rootRect, new Color(0.42f, 0.70f, 1f, 0.10f));
        Stretch(rootGlow.rectTransform, 3f, 3f, 3f, 3f);

        Image accentSweep = CreateImage("AccentSweep", rootRect, new Color(0.20f, 0.24f, 0.32f, 0.38f));
        SetTopStretch(accentSweep.rectTransform, 140f, 54f, 20f, 1f);

        Image headerLine = CreateImage("HeaderLine", rootRect, new Color(0.92f, 0.95f, 1f, 0.12f));
        SetTopStretch(headerLine.rectTransform, RootPadding, 60f, 146f, 1f);

        Image scanLine = CreateImage("ScanLine", rootRect, new Color(1f, 1f, 1f, 0.03f));
        SetTopStretch(scanLine.rectTransform, 8f, 116f, 8f, 1f);

        titleText = CreateText("Title", rootRect, "COLLECTIBLE STATUS", 22, FontStyle.Bold, TextAnchor.MiddleLeft,
            TextPrimaryColor);
        SetTopLeft(titleText.rectTransform, RootPadding, 10f, 220f, 24f);
        AddShadow(titleText.gameObject, new Color(0f, 0f, 0f, 0.82f), new Vector2(2f, -2f));

        subtitleText = CreateText("Subtitle", rootRect, "ACCESS KEYS  /  POWER CELLS", 11, FontStyle.Bold, TextAnchor.MiddleLeft,
            TextSecondaryColor);
        SetTopLeft(subtitleText.rectTransform, RootPadding, 34f, 250f, 14f);

        CreateHeaderChip("ChipCircle", "O", 134f, CircleKeyAccent);
        CreateHeaderChip("ChipRectangle", "R", 96f, RectangleKeyAccent);
        CreateHeaderChip("ChipSquare", "S", 58f, SquareKeyAccent);

        keySection = CreateSection("Keys", "ACCESS KEYS");
        fuseSection = CreateSection("Fuses", "POWER CELLS");
        ApplySectionAccent(keySection, new Color(0.38f, 0.70f, 1f, 0.95f));
        ApplySectionAccent(fuseSection, new Color(1f, 0.78f, 0.34f, 0.92f));

        BuildKeyTokens();
        RebuildFuseTokens(3);
    }

    void RefreshTargets()
    {
        if (!EnsureUIBuilt())
            return;

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
        if (!EnsureUIBuilt())
            return;

        bool hasCollectibles = targetKeyCount > 0 || targetFuseCount > 0;
        if (rootRect != null)
            rootRect.gameObject.SetActive(hasCollectibles);

        if (canvas == null) return;
        canvas.enabled = hasCollectibles && !ShouldHide();
    }

    void UpdateVisuals()
    {
        if (!EnsureUIBuilt() || rootRect == null || !rootRect.gameObject.activeSelf) return;

        PlayerInventory inventory = GetInventory();
        int collectedKeys = inventory != null ? inventory.KeyCount : 0;
        int collectedFuses = inventory != null ? inventory.TotalFusesCollected : 0;
        int carryingFuses = inventory != null ? inventory.FuseCount : 0;
        int installedFuses = Mathf.Max(0, collectedFuses - carryingFuses);

        float rootPulse = 0.92f + 0.08f * Mathf.Sin(Time.unscaledTime * 1.7f);
        if (rootGlow != null)
            rootGlow.color = new Color(0.38f, 0.68f, 1f, 0.06f + (0.03f * rootPulse));
        if (subtitleText != null)
            subtitleText.color = new Color(TextSecondaryColor.r, TextSecondaryColor.g, TextSecondaryColor.b, 0.72f + (0.10f * rootPulse));

        if (keySection.root != null)
            keySection.root.gameObject.SetActive(targetKeyCount > 0);
        if (fuseSection.root != null)
            fuseSection.root.gameObject.SetActive(targetFuseCount > 0);

        if (keySection.summaryText != null)
            keySection.summaryText.text = $"{collectedKeys}/{targetKeyCount} SECURED";
        if (keySection.detailText != null)
        {
            int remainingKeys = Mathf.Max(0, targetKeyCount - collectedKeys);
            keySection.detailText.text = remainingKeys > 0
                ? $"{remainingKeys} KEY{(remainingKeys == 1 ? string.Empty : "S")} STILL MISSING"
                : "ALL KEY SHAPES COLLECTED";
        }

        if (fuseSection.summaryText != null)
            fuseSection.summaryText.text = $"{collectedFuses}/{targetFuseCount} SECURED";
        if (fuseSection.detailText != null)
        {
            int missingFuses = Mathf.Max(0, targetFuseCount - installedFuses);
            if (carryingFuses > 0)
                fuseSection.detailText.text = $"{installedFuses} INSTALLED  |  {carryingFuses} CARRYING";
            else if (missingFuses > 0)
                fuseSection.detailText.text = $"{missingFuses} SLOT{(missingFuses == 1 ? string.Empty : "S")} STILL EMPTY";
            else
                fuseSection.detailText.text = "ALL POWER CELLS INSTALLED";
        }

        for (int i = 0; i < keyTokens.Count; i++)
        {
            KeyTokenView token = keyTokens[i];
            bool collected = inventory != null && inventory.HasKey(token.keyType);
            float pulse = 0.88f + 0.12f * Mathf.Sin((Time.unscaledTime * 3.0f) + (i * 0.7f));
            ApplyKeyStyle(token, collected, GetKeyAccentColor(token.keyType), pulse);
        }

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
        float rootWidth = Mathf.Max(SectionMinWidth, contentWidth + (RootPadding * 2f) + 32f);

        SetTopLeft(titleText.rectTransform, RootPadding, 10f, rootWidth - (RootPadding * 2f), 24f);
        SetTopLeft(subtitleText.rectTransform, RootPadding, 34f, rootWidth - 180f, 14f);

        float top = 68f;
        float keyHeight = SectionTokenTop + KeyTokenHeight + 16f;
        float fuseHeight = SectionTokenTop + FuseTokenHeight + 16f;

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
        SetTopLeft(section.headerTint.rectTransform, 8f, 8f, width - 16f, SectionHeaderHeight);
        SetTopLeft(section.titleText.rectTransform, 16f, 17f, 190f, 18f);

        float summaryWidth = Mathf.Clamp(width * 0.34f, 156f, 212f);
        SetTopRight(section.summaryPlate.rectTransform, 12f, 12f, summaryWidth, 24f);
        Stretch(section.summaryText.rectTransform, 8f, 2f, 8f, 2f);
        SetTopLeft(section.detailText.rectTransform, 16f, SectionDetailTop, width - 32f, 15f);

        float tokenX = Mathf.Max(12f, (width - tokenAreaWidth) * 0.5f);
        SetTopLeft(section.tokenHost, tokenX, SectionTokenTop, tokenAreaWidth, tokenAreaHeight);

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
        RectTransform rect = CreatePanel(name, rootRect, SectionBaseColor);
        AddOutline(rect.gameObject, new Color(0f, 0f, 0f, 0.56f), new Vector2(1f, -1f));
        AddShadow(rect.gameObject, new Color(0f, 0f, 0f, 0.28f), new Vector2(0f, -2f));

        Image sectionInner = CreateImage("SectionInner", rect, SectionInnerColor);
        Stretch(sectionInner.rectTransform, 2f, 2f, 2f, 2f);

        Image accentBar = CreateImage("AccentBar", rect, Color.white);
        accentBar.rectTransform.anchorMin = new Vector2(0f, 0f);
        accentBar.rectTransform.anchorMax = new Vector2(0f, 1f);
        accentBar.rectTransform.pivot = new Vector2(0f, 0.5f);
        accentBar.rectTransform.anchoredPosition = Vector2.zero;
        accentBar.rectTransform.sizeDelta = new Vector2(3f, 0f);

        Image headerTint = CreateImage("HeaderTint", rect, new Color(1f, 1f, 1f, 0.05f));
        Image edgeLight = CreateImage("EdgeLight", rect, new Color(1f, 1f, 1f, 0.06f));
        SetTopStretch(edgeLight.rectTransform, 12f, 10f, 12f, 1f);

        Image summaryPlate = CreateImage("SummaryPlate", rect, new Color(0.08f, 0.10f, 0.13f, 0.98f));
        AddOutline(summaryPlate.gameObject, new Color(1f, 1f, 1f, 0.03f), new Vector2(1f, -1f));

        RectTransform tokenHost = CreatePanel("TokenHost", rect, TokenShelfColor);
        AddOutline(tokenHost.gameObject, new Color(1f, 1f, 1f, 0.03f), new Vector2(1f, -1f));
        Image tokenHostGlow = CreateImage("TokenHostGlow", tokenHost, new Color(1f, 1f, 1f, 0.03f));
        Stretch(tokenHostGlow.rectTransform, 1f, 1f, 1f, 1f);

        Text sectionTitle = CreateText("Title", rect, title, 14, FontStyle.Bold, TextAnchor.MiddleLeft,
            TextPrimaryColor);
        AddShadow(sectionTitle.gameObject, new Color(0f, 0f, 0f, 0.7f), new Vector2(2f, -2f));

        Text summary = CreateText("Summary", summaryPlate.rectTransform, string.Empty, 12, FontStyle.Bold, TextAnchor.MiddleCenter,
            TextPrimaryColor);
        summary.resizeTextForBestFit = true;
        summary.resizeTextMinSize = 8;
        summary.resizeTextMaxSize = 12;
        summary.horizontalOverflow = HorizontalWrapMode.Wrap;
        summary.verticalOverflow = VerticalWrapMode.Truncate;

        Text detail = CreateText("Detail", rect, string.Empty, 11, FontStyle.Bold, TextAnchor.MiddleLeft,
            TextMutedColor);
        detail.resizeTextForBestFit = true;
        detail.resizeTextMinSize = 8;
        detail.resizeTextMaxSize = 11;
        detail.horizontalOverflow = HorizontalWrapMode.Wrap;
        detail.verticalOverflow = VerticalWrapMode.Truncate;

        return new SectionView
        {
            root = rect,
            accentBar = accentBar,
            headerTint = headerTint,
            summaryPlate = summaryPlate,
            titleText = sectionTitle,
            summaryText = summary,
            detailText = detail,
            tokenHost = tokenHost
        };
    }

    void ApplySectionAccent(SectionView section, Color accent)
    {
        section.accentBar.color = accent;
        if (section.headerTint != null)
            section.headerTint.color = new Color(accent.r, accent.g, accent.b, 0.07f);
        if (section.summaryPlate != null)
            section.summaryPlate.color = Color.Lerp(accent, new Color(0.07f, 0.08f, 0.11f, 0.98f), 0.78f);
    }

    void BuildKeyTokens()
    {
        if (keySection == null || keySection.tokenHost == null)
            return;

        keyTokens.Clear();

        for (int i = 0; i < KeyOrder.Length; i++)
        {
            KeyType keyType = KeyOrder[i];
            Color accent = GetKeyAccentColor(keyType);
            RectTransform token = CreatePanel($"{keyType}Token", keySection.tokenHost, new Color(0.08f, 0.09f, 0.12f, 0.98f));
            AddOutline(token.gameObject, new Color(1f, 1f, 1f, 0.03f), new Vector2(1f, -1f));
            Image halo = CreateImage("Halo", token, new Color(1f, 1f, 1f, 0.04f));
            Stretch(halo.rectTransform, -2f, -2f, -2f, -2f);
            Image face = CreateImage("Face", token, new Color(0.11f, 0.12f, 0.16f, 0.96f));
            Stretch(face.rectTransform, 3f, 3f, 3f, 3f);

            Image spine = CreateImage("Spine", token, accent);
            SetTopStretch(spine.rectTransform, 4f, 4f, 4f, 4f);

            Image shadowStrip = CreateImage("ShadowStrip", token, new Color(0f, 0f, 0f, 0.30f));
            SetBottomStretch(shadowStrip.rectTransform, 3f, 3f, 3f, 11f);

            Image gloss = CreateImage("Gloss", token, new Color(1f, 1f, 1f, 0.06f));
            SetTopStretch(gloss.rectTransform, 5f, 10f, 40f, 1f);

            Text head = CreateText("Head", token, GetKeyHeadGlyph(keyType), 24, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Color(0.95f, 0.95f, 0.95f, 0.92f));
            SetTopLeft(head.rectTransform, 12f, 14f, 22f, 22f);

            Image shaft = CreateImage("Shaft", token, new Color(0.86f, 0.87f, 0.90f, 0.92f));
            SetTopLeft(shaft.rectTransform, 34f, 22f, 26f, 6f);

            Image toothA = CreateImage("ToothA", token, new Color(0.86f, 0.87f, 0.90f, 0.92f));
            SetTopLeft(toothA.rectTransform, 58f, 22f, 6f, 14f);

            Image toothB = CreateImage("ToothB", token, new Color(0.86f, 0.87f, 0.90f, 0.92f));
            SetTopLeft(toothB.rectTransform, 66f, 26f, 6f, 10f);

            Image labelPlate = CreateImage("LabelPlate", token, accent);
            SetBottomStretch(labelPlate.rectTransform, 4f, 4f, 4f, 16f);

            Text label = CreateText("Label", token, GetKeyLabel(keyType), 10, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Color(0.86f, 0.89f, 0.94f, 0.9f));
            SetBottomStretch(label.rectTransform, 5f, 5f, 5f, 16f);

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
        if (fuseSection == null || fuseSection.tokenHost == null)
            return;

        for (int i = 0; i < fuseTokens.Count; i++)
        {
            if (fuseTokens[i].root != null)
                Destroy(fuseTokens[i].root.gameObject);
        }

        fuseTokens.Clear();

        for (int i = 0; i < count; i++)
        {
            RectTransform token = CreatePanel($"Fuse_{i}", fuseSection.tokenHost, new Color(0.08f, 0.09f, 0.12f, 0.98f));
            AddOutline(token.gameObject, new Color(1f, 1f, 1f, 0.03f), new Vector2(1f, -1f));
            Image glow = CreateImage("Glow", token, new Color(1f, 1f, 1f, 0.05f));
            Stretch(glow.rectTransform, -2f, -2f, -2f, -2f);
            Image face = CreateImage("Face", token, new Color(0.11f, 0.12f, 0.16f, 0.96f));
            Stretch(face.rectTransform, 3f, 3f, 3f, 3f);

            Image body = CreateImage("Body", token, new Color(0.72f, 0.74f, 0.80f, 0.85f));
            SetTopLeft(body.rectTransform, 16f, 16f, 20f, 34f);

            Image core = CreateImage("Core", token, new Color(0.98f, 0.98f, 1f, 0.88f));
            SetTopLeft(core.rectTransform, 22f, 20f, 8f, 28f);

            Image capTop = CreateImage("CapTop", token, new Color(0.84f, 0.86f, 0.92f, 0.86f));
            SetTopLeft(capTop.rectTransform, 12f, 10f, 28f, 7f);

            Image capBottom = CreateImage("CapBottom", token, new Color(0.58f, 0.60f, 0.68f, 0.86f));
            SetTopLeft(capBottom.rectTransform, 12f, 52f, 28f, 8f);

            Image spark = CreateImage("Spark", token, new Color(1f, 1f, 1f, 0.12f));
            SetTopLeft(spark.rectTransform, 22f, 13f, 8f, 14f);

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
            ? Color.Lerp(new Color(0.07f, 0.08f, 0.11f, 0.98f), accent, 0.16f)
            : new Color(0.07f, 0.08f, 0.10f, 0.96f);
        Color faceColor = collected
            ? Color.Lerp(new Color(0.11f, 0.13f, 0.16f, 0.98f), accent, 0.28f)
            : new Color(0.10f, 0.11f, 0.15f, 0.95f);
        Color mutedAccent = Color.Lerp(accent, new Color(0.26f, 0.29f, 0.35f, 1f), 0.74f);
        Color glyphColor = collected
            ? Color.Lerp(accent, Color.white, 0.18f * pulse)
            : new Color(0.46f, 0.51f, 0.58f, 0.88f);

        token.plate.color = plateColor;
        token.halo.color = new Color(accent.r, accent.g, accent.b, collected ? 0.10f + (0.07f * pulse) : 0.03f);
        token.face.color = faceColor;
        token.spine.color = collected ? accent : mutedAccent;
        token.gloss.color = new Color(1f, 1f, 1f, collected ? 0.12f + (0.04f * pulse) : 0.03f);
        token.head.color = glyphColor;
        token.shaft.color = glyphColor;
        token.toothA.color = glyphColor;
        token.toothB.color = glyphColor;
        token.labelPlate.color = collected
            ? Color.Lerp(accent, Color.black, 0.42f)
            : Color.Lerp(mutedAccent, Color.black, 0.48f);
        token.label.color = collected
            ? new Color(0.98f, 0.99f, 1f, 0.96f)
            : new Color(0.68f, 0.72f, 0.78f, 0.84f);

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
                plateColor = Color.Lerp(new Color(0.08f, 0.09f, 0.11f, 0.98f), accent, 0.14f);
                faceColor = Color.Lerp(new Color(0.11f, 0.12f, 0.16f, 0.96f), accent, 0.28f);
                bodyColor = Color.Lerp(accent, Color.white, 0.14f * pulse);
                coreColor = Color.Lerp(Color.white, accent, 0.22f);
                capTopColor = Color.Lerp(accent, Color.white, 0.24f);
                capBottomColor = Color.Lerp(accent, Color.black, 0.25f);
                sparkAlpha = 0.18f + (0.08f * pulse);
                break;

            case FuseState.Carried:
                Color carryAccent = Color.Lerp(accent, new Color(1f, 0.93f, 0.62f, 1f), 0.35f);
                plateColor = Color.Lerp(new Color(0.08f, 0.09f, 0.11f, 0.98f), carryAccent, 0.12f);
                faceColor = Color.Lerp(new Color(0.11f, 0.12f, 0.16f, 0.96f), carryAccent, 0.20f);
                bodyColor = Color.Lerp(carryAccent, Color.white, 0.10f * pulse);
                coreColor = Color.Lerp(Color.white, carryAccent, 0.30f);
                capTopColor = Color.Lerp(carryAccent, Color.white, 0.18f);
                capBottomColor = Color.Lerp(carryAccent, Color.black, 0.30f);
                sparkAlpha = 0.10f + (0.05f * pulse);
                break;

            default:
                plateColor = new Color(0.07f, 0.08f, 0.10f, 0.96f);
                faceColor = new Color(0.10f, 0.11f, 0.15f, 0.95f);
                bodyColor = new Color(0.38f, 0.42f, 0.50f, 0.88f);
                coreColor = new Color(0.22f, 0.24f, 0.30f, 0.84f);
                capTopColor = new Color(0.54f, 0.58f, 0.66f, 0.86f);
                capBottomColor = new Color(0.30f, 0.34f, 0.42f, 0.90f);
                sparkAlpha = 0.03f;
                break;
        }

        token.plate.color = plateColor;
        token.glow.color = new Color(accent.r, accent.g, accent.b, state == FuseState.Missing ? 0.03f : 0.08f + (0.06f * pulse));
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

    SectionView RestoreSectionView(string name)
    {
        RectTransform rect = FindDirectChild<RectTransform>(rootRect, name);
        if (rect == null)
            return null;

        Image summaryPlate = FindDirectChild<Image>(rect, "SummaryPlate");
        return new SectionView
        {
            root = rect,
            accentBar = FindDirectChild<Image>(rect, "AccentBar"),
            headerTint = FindDirectChild<Image>(rect, "HeaderTint"),
            summaryPlate = summaryPlate,
            titleText = FindDirectChild<Text>(rect, "Title"),
            summaryText = summaryPlate != null ? FindDirectChild<Text>(summaryPlate.rectTransform, "Summary") : null,
            detailText = FindDirectChild<Text>(rect, "Detail"),
            tokenHost = FindDirectChild<RectTransform>(rect, "TokenHost")
        };
    }

    void RestoreKeyTokens()
    {
        keyTokens.Clear();
        if (keySection == null || keySection.tokenHost == null)
            return;

        for (int i = 0; i < KeyOrder.Length; i++)
        {
            KeyType keyType = KeyOrder[i];
            RectTransform token = FindDirectChild<RectTransform>(keySection.tokenHost, $"{keyType}Token");
            if (token == null)
                continue;

            keyTokens.Add(new KeyTokenView
            {
                keyType = keyType,
                root = token,
                plate = token.GetComponent<Image>(),
                halo = FindDirectChild<Image>(token, "Halo"),
                face = FindDirectChild<Image>(token, "Face"),
                spine = FindDirectChild<Image>(token, "Spine"),
                gloss = FindDirectChild<Image>(token, "Gloss"),
                head = FindDirectChild<Text>(token, "Head"),
                shaft = FindDirectChild<Image>(token, "Shaft"),
                toothA = FindDirectChild<Image>(token, "ToothA"),
                toothB = FindDirectChild<Image>(token, "ToothB"),
                labelPlate = FindDirectChild<Image>(token, "LabelPlate"),
                label = FindDirectChild<Text>(token, "Label")
            });
        }
    }

    void RestoreFuseTokens()
    {
        fuseTokens.Clear();
        if (fuseSection == null || fuseSection.tokenHost == null)
            return;

        for (int i = 0; i < fuseSection.tokenHost.childCount; i++)
        {
            RectTransform token = fuseSection.tokenHost.GetChild(i) as RectTransform;
            if (token == null || !token.name.StartsWith("Fuse_"))
                continue;

            fuseTokens.Add(new FuseTokenView
            {
                root = token,
                plate = token.GetComponent<Image>(),
                glow = FindDirectChild<Image>(token, "Glow"),
                face = FindDirectChild<Image>(token, "Face"),
                body = FindDirectChild<Image>(token, "Body"),
                core = FindDirectChild<Image>(token, "Core"),
                capTop = FindDirectChild<Image>(token, "CapTop"),
                capBottom = FindDirectChild<Image>(token, "CapBottom"),
                spark = FindDirectChild<Image>(token, "Spark")
            });
        }
    }

    static T FindDirectChild<T>(Transform parent, string name) where T : Component
    {
        if (parent == null)
            return null;

        Transform child = parent.Find(name);
        return child != null ? child.GetComponent<T>() : null;
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

    static Color GetKeyAccentColor(KeyType keyType)
    {
        switch (keyType)
        {
            case KeyType.Circle: return CircleKeyAccent;
            case KeyType.Rectangle: return RectangleKeyAccent;
            case KeyType.Square: return SquareKeyAccent;
            default: return Color.white;
        }
    }

    void CreateHeaderChip(string name, string label, float right, Color color)
    {
        RectTransform chip = CreatePanel(name, rootRect, new Color(0.06f, 0.07f, 0.09f, 0.96f));
        SetTopRight(chip, right, 12f, 30f, 18f);
        AddOutline(chip.gameObject, new Color(1f, 1f, 1f, 0.03f), new Vector2(1f, -1f));

        Image fill = CreateImage($"{name}Fill", chip, new Color(color.r, color.g, color.b, 0.18f));
        Stretch(fill.rectTransform, 2f, 2f, 2f, 2f);

        Image dot = CreateImage($"{name}Dot", chip, color);
        SetTopLeft(dot.rectTransform, 5f, 5f, 8f, 8f);

        Text chipLabel = CreateText($"{name}Label", chip, label, 9, FontStyle.Bold, TextAnchor.MiddleCenter,
            TextPrimaryColor);
        Stretch(chipLabel.rectTransform, 12f, 2f, 2f, 2f);
    }

    class SectionView
    {
        public RectTransform root;
        public Image accentBar;
        public Image headerTint;
        public Image summaryPlate;
        public Text titleText;
        public Text summaryText;
        public Text detailText;
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
