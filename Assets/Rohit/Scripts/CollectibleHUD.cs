using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class CollectibleHUD : MonoBehaviour
{
    public enum CollectibleType
    {
        CircleKey,
        RectangleKey,
        SquareKey,
        Fuse1,
        Fuse2,
        Fuse3,
        Fuse4,
        Fuse5,
        Fuse6,
        Fuse7,
        Fuse8
    }

    enum RowKind
    {
        Keys,
        Fuses
    }

    sealed class RowView
    {
        public RowKind kind;
        public RectTransform root;
        public Image background;
        public Outline outline;
        public Shadow shadow;
        public Image accentBar;
        public Image sheen;
        public Image pipBand;
        public Image counterPlate;
        public Text label;
        public Image dividerLeft;
        public Image dividerRight;
        public RectTransform pipHost;
        public Text counter;
        public readonly List<PipView> pips = new List<PipView>();
    }

    sealed class PipView
    {
        public CollectibleType type;
        public RectTransform root;
        public Image fill;
        public Outline outline;
        public Coroutine animation;
        public bool collected;
        public Color themeColor;
        public Color fillColor;
        public Color borderColor;
    }

    const float RefreshInterval = 0.35f;
    const int DefaultKeyCount = 3;
    const int DefaultFuseCount = 3;
    const int MaxSupportedFuses = 8;
    const float HudPadding = 24f;
    const float RootGap = 8f;
    const float RowHeight = 42f;
    const float RowMinWidth = 258f;
    const float LabelWidth = 56f;
    const float CounterWidth = 42f;
    const float DividerWidth = 1f;
    const float DividerHeight = 18f;
    const float HorizontalPadding = 14f;
    const float ItemGap = 6f;
    const float GroupGap = 10f;
    const float AccentBarWidth = 3f;

    static readonly KeyType[] KeyOrder =
    {
        KeyType.Circle,
        KeyType.Rectangle,
        KeyType.Square
    };

    static readonly Color KeysBase = Rgba(100, 180, 255, 1f);
    static readonly Color FusesBase = Rgba(255, 180, 50, 1f);
    static readonly Color CompleteBase = Rgba(80, 200, 120, 1f);
    static readonly Color CircleKeyColor = Color.blue;
    static readonly Color RectangleKeyColor = Color.green;
    static readonly Color SquareKeyColor = Color.red;
    static readonly Color[] DefaultFuseColors =
    {
        Color.yellow,
        Color.blue,
        new Color(0.6f, 0.2f, 1f, 1f)
    };
    static readonly Color RowBackgroundColor = new Color(0.02f, 0.03f, 0.055f, 0.82f);
    static readonly Color RowSheenColor = new Color(1f, 1f, 1f, 0.05f);
    static readonly Color RowShadowColor = new Color(0f, 0f, 0f, 0.42f);
    static readonly Color PipBandBaseColor = new Color(1f, 1f, 1f, 0.045f);
    static readonly Color CounterPlateBaseColor = new Color(1f, 1f, 1f, 0.05f);
    static readonly Color UncollectedFill = new Color(1f, 1f, 1f, 0f);
    static readonly Color ToastBackgroundColor = new Color(80f / 255f, 200f / 255f, 120f / 255f, 0.15f);
    static readonly Color ToastBorderColor = new Color(80f / 255f, 200f / 255f, 120f / 255f, 0.3f);
    static readonly Color ToastTextColor = new Color(80f / 255f, 200f / 255f, 120f / 255f, 0.8f);

    static CollectibleHUD instance;
    static Font cachedFont;
    static Sprite circleSprite;
    static Sprite roundedRectSprite;

    Canvas canvas;
    RectTransform hudRoot;
    RectTransform toastRoot;
    RowView keyRow;
    RowView fuseRow;

    readonly List<KeyType> trackedKeys = new List<KeyType>();
    readonly HashSet<KeyType> shownKeys = new HashSet<KeyType>();
    readonly bool[] shownFuses = new bool[MaxSupportedFuses];

    int targetKeyCount;
    int targetFuseCount;
    float nextRefreshAt;
    bool overlaysSuppressed;

    public static bool Exists => instance != null;

    public static void EnsureExists()
    {
        if (instance != null)
            return;

        var go = new GameObject("CollectibleHUD");
        instance = go.AddComponent<CollectibleHUD>();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        EnsureExists();
        if (instance != null)
            instance.MarkDirty();
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
        BuildDefaultState();
        UpdateOverlayVisibility(SceneManager.GetActiveScene());
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

    void Update()
    {
        if (overlaysSuppressed)
            return;

        if (Time.unscaledTime >= nextRefreshAt)
            RefreshTargets();

        SyncInventoryState();
    }

    public void CollectItem(CollectibleType type)
    {
        bool changed = false;

        if (TryMapKey(type, out KeyType keyType))
        {
            changed = shownKeys.Add(keyType);
        }
        else
        {
            int fuseIndex = GetFuseIndex(type);
            if (fuseIndex >= 0 && fuseIndex < targetFuseCount && fuseIndex < shownFuses.Length && !shownFuses[fuseIndex])
            {
                shownFuses[fuseIndex] = true;
                changed = true;
            }
        }

        if (!changed)
            return;

        ApplyVisualState(true);
        ShowToast(GetToastLabel(type), GetCollectibleDisplayColor(type));
    }

    public void ResetAll()
    {
        shownKeys.Clear();
        for (int i = 0; i < shownFuses.Length; i++)
            shownFuses[i] = false;

        ApplyVisualState(false);
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        UpdateOverlayVisibility(scene);
        MarkDirty();
        StartCoroutine(RefreshNextFrame());
    }

    IEnumerator RefreshNextFrame()
    {
        yield return null;
        if (overlaysSuppressed)
            yield break;

        MarkDirty();
        RefreshTargets();
        SyncInventoryState(force: true);
    }

    void MarkDirty()
    {
        nextRefreshAt = 0f;
    }

    void BuildUI()
    {
        canvas = gameObject.GetComponent<Canvas>();
        if (canvas == null)
            canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue - 24;

        CanvasScaler scaler = gameObject.GetComponent<CanvasScaler>();
        if (scaler == null)
            scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        if (gameObject.GetComponent<GraphicRaycaster>() == null)
            gameObject.AddComponent<GraphicRaycaster>();

        hudRoot = CreateRect("HUDRoot", transform);
        hudRoot.anchorMin = new Vector2(0f, 1f);
        hudRoot.anchorMax = new Vector2(0f, 1f);
        hudRoot.pivot = new Vector2(0f, 1f);
        hudRoot.anchoredPosition = new Vector2(HudPadding, -HudPadding);

        toastRoot = CreateRect("ToastRoot", transform);
        toastRoot.anchorMin = new Vector2(0f, 1f);
        toastRoot.anchorMax = new Vector2(0f, 1f);
        toastRoot.pivot = new Vector2(0f, 1f);
        toastRoot.anchoredPosition = new Vector2(HudPadding, -(HudPadding + 98f));

        keyRow = CreateRow("KeysRow", hudRoot, RowKind.Keys, "KEYS");
        fuseRow = CreateRow("FuseRow", hudRoot, RowKind.Fuses, "FUSES");
    }

    void BuildDefaultState()
    {
        trackedKeys.Clear();
        trackedKeys.AddRange(KeyOrder);
        targetKeyCount = DefaultKeyCount;
        targetFuseCount = DefaultFuseCount;

        BuildRowPips(keyRow, targetKeyCount);
        BuildRowPips(fuseRow, targetFuseCount);
        LayoutRows();
        ApplyVisualState(false);
    }

    void RefreshTargets()
    {
        nextRefreshAt = Time.unscaledTime + RefreshInterval;

        List<KeyType> detectedKeys = ResolveTrackedKeys();
        int detectedFuses = ResolveFuseTargetCount();

        if (ShouldUseProjectDefaults())
        {
            if (detectedKeys.Count < DefaultKeyCount)
            {
                detectedKeys.Clear();
                detectedKeys.AddRange(KeyOrder);
            }

            detectedFuses = Mathf.Max(detectedFuses, DefaultFuseCount);
        }

        if (detectedKeys.Count == 0)
            detectedKeys.AddRange(KeyOrder);

        detectedFuses = Mathf.Clamp(Mathf.Max(detectedFuses, DefaultFuseCount), 0, MaxSupportedFuses);

        bool keysChanged = trackedKeys.Count != detectedKeys.Count;
        if (!keysChanged)
        {
            for (int i = 0; i < detectedKeys.Count; i++)
            {
                if (trackedKeys[i] != detectedKeys[i])
                {
                    keysChanged = true;
                    break;
                }
            }
        }

        bool fusesChanged = targetFuseCount != detectedFuses;
        if (!keysChanged && !fusesChanged)
            return;

        trackedKeys.Clear();
        trackedKeys.AddRange(detectedKeys);
        targetKeyCount = trackedKeys.Count;
        targetFuseCount = detectedFuses;

        BuildRowPips(keyRow, targetKeyCount);
        BuildRowPips(fuseRow, targetFuseCount);
        LayoutRows();
        SyncInventoryState(force: true);
    }

    void SyncInventoryState(bool force = false)
    {
        PlayerInventory inventory = GetInventory();
        if (inventory == null)
        {
            ApplyVisualState(false);
            return;
        }

        bool changed = force;

        for (int i = 0; i < trackedKeys.Count; i++)
        {
            KeyType keyType = trackedKeys[i];
            bool hasKey = inventory.HasKey(keyType);
            bool isShown = shownKeys.Contains(keyType);

            if (hasKey && !isShown)
            {
                shownKeys.Add(keyType);
                changed = true;
            }
            else if (!hasKey && isShown)
            {
                shownKeys.Remove(keyType);
                changed = true;
            }
        }

        FuseBox[] trackedFuseBoxes = GetTrackedFuseBoxes();
        for (int i = 0; i < shownFuses.Length; i++)
        {
            bool shouldBeShown = i < targetFuseCount &&
                                i < trackedFuseBoxes.Length &&
                                trackedFuseBoxes[i] != null &&
                                trackedFuseBoxes[i].IsPowered;
            if (shownFuses[i] != shouldBeShown)
            {
                shownFuses[i] = shouldBeShown;
                changed = true;
            }
        }

        if (changed)
            ApplyVisualState(true);
    }

    RowView CreateRow(string name, Transform parent, RowKind kind, string labelText)
    {
        RectTransform rowRect = CreateRect(name, parent);
        Image background = rowRect.gameObject.AddComponent<Image>();
        background.sprite = GetRoundedRectSprite();
        background.color = RowBackgroundColor;

        Outline outline = rowRect.gameObject.AddComponent<Outline>();
        outline.effectDistance = new Vector2(1f, -1f);
        outline.useGraphicAlpha = true;

        Shadow shadow = rowRect.gameObject.AddComponent<Shadow>();
        shadow.effectDistance = new Vector2(0f, -4f);
        shadow.effectColor = RowShadowColor;
        shadow.useGraphicAlpha = true;

        Image accentBar = CreateSolidImage("AccentBar", rowRect, Color.white);
        Image sheen = CreateSolidImage("Sheen", rowRect, RowSheenColor);
        Text label = CreateText("Label", rowRect, labelText, 12, TextAnchor.MiddleLeft);
        AddTextShadow(label, new Color(0f, 0f, 0f, 0.55f), new Vector2(0f, -1f));
        Image dividerLeft = CreateSolidImage("DividerLeft", rowRect, Color.white);
        Image dividerRight = CreateSolidImage("DividerRight", rowRect, Color.white);
        Image pipBand = CreateSolidImage("PipBand", rowRect, PipBandBaseColor);
        RectTransform pipHost = CreateRect("PipHost", rowRect);
        Image counterPlate = CreateSolidImage("CounterPlate", rowRect, CounterPlateBaseColor);
        Text counter = CreateText("Counter", rowRect, "0/0", 12, TextAnchor.MiddleCenter);
        AddTextShadow(counter, new Color(0f, 0f, 0f, 0.55f), new Vector2(0f, -1f));

        return new RowView
        {
            kind = kind,
            root = rowRect,
            background = background,
            outline = outline,
            shadow = shadow,
            accentBar = accentBar,
            sheen = sheen,
            pipBand = pipBand,
            counterPlate = counterPlate,
            label = label,
            dividerLeft = dividerLeft,
            dividerRight = dividerRight,
            pipHost = pipHost,
            counter = counter
        };
    }

    void BuildRowPips(RowView row, int count)
    {
        ClearChildren(row.pipHost);
        row.pips.Clear();

        for (int i = 0; i < count; i++)
        {
            PipView pip = row.kind == RowKind.Keys
                ? CreateKeyPip(i, row.pipHost)
                : CreateFusePip(i, row.pipHost);
            row.pips.Add(pip);
        }
    }

    PipView CreateKeyPip(int index, Transform parent)
    {
        RectTransform rect = CreateRect($"KeyPip_{index}", parent);
        rect.sizeDelta = index == 1 ? new Vector2(14f, 10f) : new Vector2(14f, 14f);
        CollectibleType type = MapKeyIndex(index);
        Color pipColor = GetCollectibleDisplayColor(type);

        Image fill = rect.gameObject.AddComponent<Image>();
        fill.color = UncollectedFill;
        fill.raycastTarget = false;

        if (index == 0)
            fill.sprite = GetCircleSprite();
        else
            fill.sprite = GetRoundedRectSprite();

        Outline outline = rect.gameObject.AddComponent<Outline>();
        outline.effectDistance = new Vector2(1f, -1f);
        outline.useGraphicAlpha = false;

        return new PipView
        {
            type = type,
            root = rect,
            fill = fill,
            outline = outline,
            themeColor = pipColor,
            fillColor = UncollectedFill,
            borderColor = WithAlpha(pipColor, 0.3f)
        };
    }

    PipView CreateFusePip(int index, Transform parent)
    {
        RectTransform rect = CreateRect($"FusePip_{index}", parent);
        rect.sizeDelta = new Vector2(8f, 18f);
        CollectibleType type = MapFuseIndex(index);
        Color pipColor = GetCollectibleDisplayColor(type);

        Image fill = rect.gameObject.AddComponent<Image>();
        fill.color = UncollectedFill;
        fill.sprite = GetRoundedRectSprite();
        fill.raycastTarget = false;

        Outline outline = rect.gameObject.AddComponent<Outline>();
        outline.effectDistance = new Vector2(1f, -1f);
        outline.useGraphicAlpha = false;

        return new PipView
        {
            type = type,
            root = rect,
            fill = fill,
            outline = outline,
            themeColor = pipColor,
            fillColor = UncollectedFill,
            borderColor = WithAlpha(pipColor, 0.3f)
        };
    }

    void LayoutRows()
    {
        float keyWidth = LayoutRow(keyRow, trackedKeys.Count);
        float fuseWidth = LayoutRow(fuseRow, targetFuseCount);
        float rootWidth = Mathf.Max(keyWidth, fuseWidth);

        hudRoot.sizeDelta = new Vector2(rootWidth, (RowHeight * 2f) + RootGap);
        toastRoot.anchoredPosition = new Vector2(HudPadding, -(HudPadding + hudRoot.sizeDelta.y + 12f));

        PositionRow(keyRow.root, 0f, rootWidth);
        PositionRow(fuseRow.root, RowHeight + RootGap, rootWidth);
    }

    float LayoutRow(RowView row, int count)
    {
        float pipAreaWidth = GetPipAreaWidth(row.kind, count);
        float width = Mathf.Max(RowMinWidth, (HorizontalPadding * 2f) + LabelWidth + GroupGap + DividerWidth + GroupGap + pipAreaWidth + GroupGap + DividerWidth + GroupGap + CounterWidth);

        row.root.sizeDelta = new Vector2(width, RowHeight);
        row.background.color = RowBackgroundColor;

        StretchLeft(row.accentBar.rectTransform, 0f, 0f, AccentBarWidth);
        SetTopStretch(row.sheen.rectTransform, 14f, 7f, 14f, 1f);

        SetLeftCenter(row.label.rectTransform, HorizontalPadding + 8f, LabelWidth, 16f);
        SetLeftCenter(row.dividerLeft.rectTransform, HorizontalPadding + 8f + LabelWidth + GroupGap, DividerWidth, DividerHeight);

        float pipX = HorizontalPadding + 8f + LabelWidth + GroupGap + DividerWidth + GroupGap;
        SetLeftCenter(row.pipBand.rectTransform, pipX - 8f, pipAreaWidth + 16f, 24f);
        SetLeftCenter(row.pipHost, pipX, pipAreaWidth, 18f);

        SetLeftCenter(row.dividerRight.rectTransform, pipX + pipAreaWidth + GroupGap, DividerWidth, DividerHeight);
        SetLeftCenter(row.counterPlate.rectTransform, width - HorizontalPadding - CounterWidth - 8f, CounterWidth + 16f, 24f);
        SetLeftCenter(row.counter.rectTransform, width - HorizontalPadding - CounterWidth, CounterWidth, 16f);

        for (int i = 0; i < row.pips.Count; i++)
        {
            PipView pip = row.pips[i];
            float step = row.kind == RowKind.Keys ? 19f : 13f;
            float widthOffset = row.kind == RowKind.Keys && i == 1 ? 14f : (row.kind == RowKind.Fuses ? 8f : 14f);
            float height = row.kind == RowKind.Fuses ? 18f : (i == 1 ? 10f : 14f);
            SetLeftCenter(pip.root, i * step, widthOffset, height);
        }

        return width;
    }

    void PositionRow(RectTransform row, float top, float width)
    {
        row.anchorMin = new Vector2(0f, 1f);
        row.anchorMax = new Vector2(0f, 1f);
        row.pivot = new Vector2(0f, 1f);
        row.anchoredPosition = new Vector2(0f, -top);
        row.sizeDelta = new Vector2(width, RowHeight);
    }

    void ApplyVisualState(bool animate)
    {
        ApplyRowState(keyRow, trackedKeys.Count, shownKeys.Count, KeysBase, CompleteBase, animate);
        ApplyRowState(fuseRow, targetFuseCount, CountShownFuses(), FusesBase, CompleteBase, animate);
    }

    void ApplyRowState(RowView row, int total, int collected, Color baseColor, Color completeColor, bool animate)
    {
        bool isComplete = total > 0 && collected >= total;
        bool isPartial = collected > 0 && !isComplete;
        Color theme = isComplete ? completeColor : ResolveRowThemeColor(row, baseColor);

        float labelAlpha = isComplete ? 0.8f : isPartial ? 0.7f : 0.55f;
        float counterAlpha = isComplete ? 0.9f : isPartial ? 0.85f : 0.45f;
        float dividerAlpha = isComplete ? 0.2f : isPartial ? 0.18f : 0.15f;

        row.label.text = row.kind == RowKind.Keys ? "KEYS" : "FUSES";
        row.counter.text = $"{collected}/{total}";
        row.background.color = Color.Lerp(RowBackgroundColor, new Color(theme.r * 0.20f, theme.g * 0.20f, theme.b * 0.20f, RowBackgroundColor.a), isComplete ? 0.75f : isPartial ? 0.35f : 0.08f);
        row.label.color = WithAlpha(theme, labelAlpha);
        row.counter.color = WithAlpha(theme, counterAlpha);
        row.dividerLeft.color = WithAlpha(theme, dividerAlpha);
        row.dividerRight.color = WithAlpha(theme, dividerAlpha);
        row.outline.effectColor = WithAlpha(theme, dividerAlpha);
        row.shadow.effectColor = new Color(0f, 0f, 0f, isComplete ? 0.52f : 0.42f);
        row.accentBar.color = WithAlpha(theme, isComplete ? 0.85f : isPartial ? 0.70f : 0.38f);
        row.sheen.color = new Color(1f, 1f, 1f, isComplete ? 0.08f : 0.05f);
        row.pipBand.color = Color.Lerp(PipBandBaseColor, WithAlpha(theme, 0.12f), isComplete ? 1f : isPartial ? 0.75f : 0.2f);
        row.counterPlate.color = Color.Lerp(CounterPlateBaseColor, WithAlpha(theme, isComplete ? 0.24f : 0.16f), isComplete ? 1f : isPartial ? 0.7f : 0.25f);

        for (int i = 0; i < row.pips.Count; i++)
        {
            bool isCollected = row.kind == RowKind.Keys
                ? (i < trackedKeys.Count && shownKeys.Contains(trackedKeys[i]))
                : shownFuses[i];

            Color pipColor = isComplete ? completeColor : row.pips[i].themeColor;
            Color targetBorder = isCollected ? pipColor : WithAlpha(pipColor, 0.34f);
            Color targetFill = isCollected ? pipColor : UncollectedFill;
            AnimatePip(row.pips[i], targetFill, targetBorder, animate ? 0.2f : 0f);
        }
    }

    void AnimatePip(PipView pip, Color fillColor, Color borderColor, float duration)
    {
        if (pip.animation != null)
            StopCoroutine(pip.animation);

        if (duration <= 0f)
        {
            pip.fill.color = fillColor;
            pip.outline.effectColor = borderColor;
            pip.fillColor = fillColor;
            pip.borderColor = borderColor;
            return;
        }

        pip.animation = StartCoroutine(AnimatePipRoutine(pip, fillColor, borderColor, duration));
    }

    IEnumerator AnimatePipRoutine(PipView pip, Color targetFill, Color targetBorder, float duration)
    {
        Color startFill = pip.fillColor;
        Color startBorder = pip.borderColor;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = 1f - Mathf.Pow(1f - t, 3f);
            pip.fill.color = Color.Lerp(startFill, targetFill, eased);
            pip.outline.effectColor = Color.Lerp(startBorder, targetBorder, eased);
            yield return null;
        }

        pip.fill.color = targetFill;
        pip.outline.effectColor = targetBorder;
        pip.fillColor = targetFill;
        pip.borderColor = targetBorder;
        pip.animation = null;
    }

    void ShowToast(string message, Color themeColor)
    {
        if (overlaysSuppressed)
            return;

        RectTransform toast = CreateRect("Toast", toastRoot);
        toast.anchorMin = new Vector2(0f, 1f);
        toast.anchorMax = new Vector2(0f, 1f);
        toast.pivot = new Vector2(0f, 1f);
        toast.anchoredPosition = new Vector2(0f, -toastRoot.childCount * 28f);
        toast.sizeDelta = new Vector2(Mathf.Max(120f, message.Length * 7f), 22f);

        Image background = toast.gameObject.AddComponent<Image>();
        background.color = new Color(themeColor.r, themeColor.g, themeColor.b, ToastBackgroundColor.a);

        Outline outline = toast.gameObject.AddComponent<Outline>();
        outline.effectDistance = new Vector2(1f, -1f);
        outline.effectColor = new Color(themeColor.r, themeColor.g, themeColor.b, ToastBorderColor.a);
        outline.useGraphicAlpha = true;

        Text text = CreateText("ToastText", toast, message, 11, TextAnchor.MiddleLeft);
        text.color = Color.Lerp(themeColor, Color.white, 0.15f);
        Stretch(text.rectTransform, 8f, 3f, 8f, 3f);

        CanvasGroup group = toast.gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        StartCoroutine(AnimateToast(toast, group, text));
    }

    IEnumerator AnimateToast(RectTransform toast, CanvasGroup group, Text text)
    {
        const float fadeIn = 0.1f;
        const float hold = 1.2f;
        const float fadeOut = 0.3f;

        float elapsed = 0f;
        while (elapsed < fadeIn)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / fadeIn);
            group.alpha = t;
            yield return null;
        }

        group.alpha = 1f;
        yield return new WaitForSecondsRealtime(hold);

        elapsed = 0f;
        while (elapsed < fadeOut)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / fadeOut);
            group.alpha = 1f - t;
            yield return null;
        }

        Destroy(toast.gameObject);
    }

    List<KeyType> ResolveTrackedKeys()
    {
        var found = new HashSet<KeyType>();

        KeyItem[] keyItems = FindObjectsByType<KeyItem>(FindObjectsSortMode.None);
        for (int i = 0; i < keyItems.Length; i++)
        {
            KeyItem key = keyItems[i];
            if (key != null && key.gameObject.activeInHierarchy)
                found.Add(key.keyType);
        }

        Door[] doors = FindObjectsByType<Door>(FindObjectsSortMode.None);
        for (int i = 0; i < doors.Length; i++)
        {
            Door door = doors[i];
            if (door != null && door.gameObject.activeInHierarchy)
                found.Add(door.requiredKey);
        }

        if (FindFirstObjectByType<MainDoor>() != null)
            found.UnionWith(KeyOrder);

        PlayerInventory inventory = GetInventory();
        if (inventory != null)
        {
            for (int i = 0; i < KeyOrder.Length; i++)
            {
                if (inventory.HasKey(KeyOrder[i]))
                    found.Add(KeyOrder[i]);
            }
        }

        var ordered = new List<KeyType>();
        for (int i = 0; i < KeyOrder.Length; i++)
        {
            if (found.Contains(KeyOrder[i]))
                ordered.Add(KeyOrder[i]);
        }

        return ordered;
    }

    int ResolveFuseTargetCount()
    {
        return GetTrackedFuseBoxes().Length;
    }

    bool ShouldUseProjectDefaults()
    {
        if (FindFirstObjectByType<RohitFPSController>() != null) return true;
        if (GetInventory() != null) return true;
        if (FindFirstObjectByType<MainDoor>() != null) return true;
        if (FindFirstObjectByType<FuseDoor>() != null) return true;
        if (FindObjectsByType<Door>(FindObjectsSortMode.None).Length > 0) return true;
        if (FindObjectsByType<FuseBox>(FindObjectsSortMode.None).Length > 0) return true;
        return false;
    }

    static PlayerInventory GetInventory()
    {
        return PlayerInventory.instance != null
            ? PlayerInventory.instance
            : FindFirstObjectByType<PlayerInventory>();
    }

    void UpdateOverlayVisibility(Scene scene)
    {
        overlaysSuppressed = IsOverlaySuppressedScene(scene.name);

        if (canvas != null)
            canvas.enabled = !overlaysSuppressed;

        if (hudRoot != null)
            hudRoot.gameObject.SetActive(!overlaysSuppressed);

        if (toastRoot != null)
            toastRoot.gameObject.SetActive(!overlaysSuppressed);
    }

    static bool IsOverlaySuppressedScene(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
            return false;

        return sceneName == "Level Select" ||
               sceneName == "New Tutorial 1" ||
               sceneName == "New Tutorial 2" ||
               sceneName == "New Tutorial 3";
    }

    int CountShownFuses()
    {
        int count = 0;
        for (int i = 0; i < targetFuseCount && i < shownFuses.Length; i++)
        {
            if (shownFuses[i])
                count++;
        }

        return count;
    }

    Color ResolveRowThemeColor(RowView row, Color fallback)
    {
        if (row == null || row.pips.Count == 0)
            return fallback;

        for (int i = 0; i < row.pips.Count; i++)
        {
            if (!IsPipCollected(row, i))
                return row.pips[i].themeColor;
        }

        return row.pips[row.pips.Count - 1].themeColor;
    }

    bool IsPipCollected(RowView row, int index)
    {
        if (row == null || index < 0 || index >= row.pips.Count)
            return false;

        if (row.kind == RowKind.Keys)
            return index < trackedKeys.Count && shownKeys.Contains(trackedKeys[index]);

        return index < shownFuses.Length && shownFuses[index];
    }

    static bool TryMapKey(CollectibleType type, out KeyType keyType)
    {
        switch (type)
        {
            case CollectibleType.CircleKey:
                keyType = KeyType.Circle;
                return true;
            case CollectibleType.RectangleKey:
                keyType = KeyType.Rectangle;
                return true;
            case CollectibleType.SquareKey:
                keyType = KeyType.Square;
                return true;
            default:
                keyType = default;
                return false;
        }
    }

    static CollectibleType MapKeyIndex(int index)
    {
        switch (index)
        {
            case 0: return CollectibleType.CircleKey;
            case 1: return CollectibleType.RectangleKey;
            default: return CollectibleType.SquareKey;
        }
    }

    static int GetFuseIndex(CollectibleType type)
    {
        int index = (int)type - (int)CollectibleType.Fuse1;
        return index >= 0 ? index : -1;
    }

    static CollectibleType MapFuseIndex(int index)
    {
        index = Mathf.Clamp(index, 0, MaxSupportedFuses - 1);
        return (CollectibleType)((int)CollectibleType.Fuse1 + index);
    }

    static string GetToastLabel(CollectibleType type)
    {
        switch (type)
        {
            case CollectibleType.CircleKey: return "+ CIRCLE KEY";
            case CollectibleType.RectangleKey: return "+ RECTANGLE KEY";
            case CollectibleType.SquareKey: return "+ SQUARE KEY";
            default: return "+ FUSE";
        }
    }

    Color GetCollectibleDisplayColor(CollectibleType type)
    {
        if (TryMapKey(type, out KeyType keyType))
            return ResolveKeyDisplayColor(keyType);

        int fuseIndex = GetFuseIndex(type);
        return ResolveFuseDisplayColor(fuseIndex);
    }

    Color ResolveKeyDisplayColor(KeyType keyType)
    {
        switch (keyType)
        {
            case KeyType.Circle:
                return CircleKeyColor;
            case KeyType.Rectangle:
                return RectangleKeyColor;
            case KeyType.Square:
                return SquareKeyColor;
            default:
                return KeysBase;
        }
    }

    Color ResolveFuseDisplayColor(int fuseIndex)
    {
        FuseBox[] boxes = GetTrackedFuseBoxes();
        if (fuseIndex >= 0 && fuseIndex < boxes.Length)
        {
            FuseBox box = boxes[fuseIndex];
            int boxColorIndex = Mathf.Clamp((int)box.requiredFuseId, 0, MaxSupportedFuses - 1);
            if (box.preferFusePrefabColor &&
                box.fusePrefabs != null &&
                boxColorIndex < box.fusePrefabs.Length &&
                box.fusePrefabs[boxColorIndex] != null)
            {
                Renderer fuseRenderer = box.fusePrefabs[boxColorIndex].GetComponentInChildren<Renderer>(true);
                if (fuseRenderer != null)
                {
                    Material shared = fuseRenderer.sharedMaterial;
                    if (shared != null)
                    {
                        if (shared.HasProperty("_BaseColor"))
                            return shared.GetColor("_BaseColor");
                        if (shared.HasProperty("_Color"))
                            return shared.GetColor("_Color");
                    }
                }
            }

            if (box.fallbackSlotColors != null && box.fallbackSlotColors.Length > 0)
                return box.fallbackSlotColors[Mathf.Clamp(boxColorIndex, 0, box.fallbackSlotColors.Length - 1)];
        }

        return DefaultFuseColors[Mathf.Clamp(fuseIndex, 0, DefaultFuseColors.Length - 1)];
    }

    FuseBox[] GetTrackedFuseBoxes()
    {
        FuseBox[] boxes = FindObjectsByType<FuseBox>(FindObjectsSortMode.None);
        if (boxes == null || boxes.Length == 0)
            return System.Array.Empty<FuseBox>();

        List<FuseBox> trackedBoxes = new List<FuseBox>(boxes.Length);
        for (int i = 0; i < boxes.Length; i++)
        {
            FuseBox box = boxes[i];
            if (box == null || !box.isActiveAndEnabled || !box.gameObject.activeInHierarchy)
                continue;

            trackedBoxes.Add(box);
        }

        trackedBoxes.Sort((a, b) =>
        {
            int idCompare = a.requiredFuseId.CompareTo(b.requiredFuseId);
            if (idCompare != 0)
                return idCompare;

            int nameCompare = string.CompareOrdinal(a.name, b.name);
            if (nameCompare != 0)
                return nameCompare;

            return a.GetInstanceID().CompareTo(b.GetInstanceID());
        });

        return trackedBoxes.ToArray();
    }

    static float GetPipAreaWidth(RowKind kind, int count)
    {
        if (count <= 0)
            return 0f;

        float itemWidth = kind == RowKind.Keys ? 14f : 8f;
        float step = kind == RowKind.Keys ? 19f : 13f;
        return itemWidth + ((count - 1) * step);
    }

    static RectTransform CreateRect(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.localScale = Vector3.one;
        return rect;
    }

    static Text CreateText(string name, Transform parent, string content, int fontSize, TextAnchor anchor)
    {
        RectTransform rect = CreateRect(name, parent);
        Text text = rect.gameObject.AddComponent<Text>();
        text.font = GetFont();
        text.text = content;
        text.fontSize = fontSize;
        text.fontStyle = FontStyle.Bold;
        text.alignment = anchor;
        text.color = Color.white;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.supportRichText = false;
        text.raycastTarget = false;
        return text;
    }

    static void AddTextShadow(Text text, Color color, Vector2 distance)
    {
        Shadow shadow = text.gameObject.AddComponent<Shadow>();
        shadow.effectColor = color;
        shadow.effectDistance = distance;
        shadow.useGraphicAlpha = true;
    }

    static Image CreateSolidImage(string name, Transform parent, Color color)
    {
        RectTransform rect = CreateRect(name, parent);
        Image image = rect.gameObject.AddComponent<Image>();
        image.sprite = GetRoundedRectSprite();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    static void ClearChildren(RectTransform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            Transform child = parent.GetChild(i);
            if (Application.isPlaying)
                Destroy(child.gameObject);
            else
                DestroyImmediate(child.gameObject);
        }
    }

    static Font GetFont()
    {
        if (cachedFont == null)
            cachedFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (cachedFont == null)
            cachedFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
        return cachedFont;
    }

    static Sprite GetCircleSprite()
    {
        if (circleSprite != null)
            return circleSprite;

        Texture2D texture = new Texture2D(32, 32, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;

        Vector2 center = new Vector2(15.5f, 15.5f);
        float radius = 13.5f;
        for (int y = 0; y < 32; y++)
        {
            for (int x = 0; x < 32; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), center);
                float alpha = Mathf.Clamp01(radius - dist);
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        texture.Apply();
        circleSprite = Sprite.Create(texture, new Rect(0f, 0f, 32f, 32f), new Vector2(0.5f, 0.5f), 100f);
        return circleSprite;
    }

    static Sprite GetRoundedRectSprite()
    {
        if (roundedRectSprite != null)
            return roundedRectSprite;

        Texture2D texture = new Texture2D(32, 32, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;

        for (int y = 0; y < 32; y++)
        {
            for (int x = 0; x < 32; x++)
                texture.SetPixel(x, y, Color.white);
        }

        texture.Apply();
        roundedRectSprite = Sprite.Create(texture, new Rect(0f, 0f, 32f, 32f), new Vector2(0.5f, 0.5f), 100f);
        return roundedRectSprite;
    }

    static void SetLeftCenter(RectTransform rect, float left, float width, float height)
    {
        rect.anchorMin = new Vector2(0f, 0.5f);
        rect.anchorMax = new Vector2(0f, 0.5f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.anchoredPosition = new Vector2(left, 0f);
        rect.sizeDelta = new Vector2(width, height);
    }

    static void Stretch(RectTransform rect, float left, float top, float right, float bottom)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
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

    static void StretchLeft(RectTransform rect, float top, float bottom, float width)
    {
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.offsetMin = new Vector2(0f, bottom);
        rect.offsetMax = new Vector2(width, -top);
    }

    static Color Rgba(float r, float g, float b, float a)
    {
        return new Color(r / 255f, g / 255f, b / 255f, a);
    }

    static Color WithAlpha(Color color, float alpha)
    {
        color.a = alpha;
        return color;
    }
}
