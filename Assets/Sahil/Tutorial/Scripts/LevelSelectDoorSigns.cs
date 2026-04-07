using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Sushil.Systems;

public class LevelSelectDoorSigns : MonoBehaviour
{
    const string LevelSelectSceneName = "Level Select";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (!activeScene.isLoaded || activeScene.name != LevelSelectSceneName)
            return;

        var existing = Object.FindFirstObjectByType<LevelSelectDoorSigns>();
        if (existing != null)
        {
            existing.BuildSigns();
            return;
        }

        var go = new GameObject(nameof(LevelSelectDoorSigns));
        go.AddComponent<LevelSelectDoorSigns>();
    }

    void Awake()
    {
        BuildSigns();
    }

    void BuildSigns()
    {
        var transitions = Object.FindObjectsByType<LevelSelectDoorTransition>(FindObjectsSortMode.None);
        float headerWorldY = GetSharedHeaderWorldY(transitions);
        foreach (var transition in transitions)
        {
            if (transition == null || transform.Find(GetSignName(transition)) != null)
                continue;

            CreateSignForDoor(transition, headerWorldY);
        }
    }

    float GetSharedHeaderWorldY(LevelSelectDoorTransition[] transitions)
    {
        foreach (var transition in transitions)
        {
            if (transition != null && !IsTutorialDoor(transition))
                return transition.transform.TransformPoint(new Vector3(0.5f, 1.85f, -0.18f)).y;
        }

        foreach (var transition in transitions)
        {
            if (transition != null)
                return transition.transform.TransformPoint(new Vector3(0.5f, 1.85f, -0.18f)).y;
        }

        return 1.9f;
    }

    void CreateSignForDoor(LevelSelectDoorTransition transition, float headerWorldY)
    {
        Vector3 anchorWorld = transition.transform.TransformPoint(new Vector3(0.5f, 0f, -0.18f));

        GameObject signRoot = new GameObject(GetSignName(transition));
        signRoot.transform.SetParent(transform, false);
        signRoot.transform.position = new Vector3(anchorWorld.x, headerWorldY, anchorWorld.z);
        signRoot.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
        signRoot.transform.localScale = Vector3.one * 0.0042f;

        Canvas canvas = signRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 500;
        signRoot.AddComponent<GraphicRaycaster>();

        RectTransform canvasRect = canvas.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(520f, 170f);

        GameObject card = CreateImage(signRoot.transform, "Card",
            new Color(0.04f, 0.16f, 0.20f, 0.70f),
            new Vector2(0f, 0f), new Vector2(1f, 1f));
        AddOutline(card, new Color(0.22f, 0.90f, 0.95f, 0.95f), new Vector2(2f, -2f));
        AddShadow(card, new Color(0f, 0f, 0f, 0.42f), new Vector2(0f, -6f));

        GameObject glow = CreateImage(signRoot.transform, "Glow",
            new Color(0.10f, 0.85f, 0.95f, 0.08f),
            new Vector2(-0.02f, -0.04f), new Vector2(1.02f, 1.04f));
        AddOutline(glow, new Color(0.10f, 0.85f, 0.95f, 0.32f), new Vector2(3f, -3f));

        Text levelText = CreateText(signRoot.transform, "LevelName",
            BuildDoorLabel(transition), 64, FontStyle.Bold,
            new Color(0.68f, 0.98f, 1f, 1f),
            new Vector2(0.08f, 0.38f), new Vector2(0.92f, 0.82f));
        AddShadow(levelText.gameObject, new Color(0f, 0.12f, 0.14f, 0.95f), new Vector2(4f, -4f));
        AddOutline(levelText.gameObject, new Color(0.04f, 0.42f, 0.46f, 1f), new Vector2(2f, -2f));

        Text subtitleText = CreateText(signRoot.transform, "Subtitle",
            BuildSubtitle(transition), 26, FontStyle.Normal,
            new Color(0.84f, 0.96f, 0.98f, 0.95f),
            new Vector2(0.10f, 0.14f), new Vector2(0.90f, 0.36f));
        AddShadow(subtitleText.gameObject, new Color(0f, 0.08f, 0.10f, 0.95f), new Vector2(3f, -3f));

        Text arrowText = CreateText(signRoot.transform, "Arrow",
            "▼", 54, FontStyle.Bold,
            new Color(0.52f, 1f, 0.98f, 1f),
            new Vector2(0.42f, -0.14f), new Vector2(0.58f, 0.16f));
        AddShadow(arrowText.gameObject, new Color(0f, 0.12f, 0.14f, 0.95f), new Vector2(4f, -4f));
        AddOutline(arrowText.gameObject, new Color(0.04f, 0.42f, 0.46f, 1f), new Vector2(2f, -2f));
    }

    static string BuildDoorLabel(LevelSelectDoorTransition transition)
    {
        string label = string.IsNullOrWhiteSpace(transition.doorLabel)
            ? transition.gameObject.name
            : transition.doorLabel.Trim();
        if (label.EndsWith(" Door"))
            label = label[..^5];
        return label.ToUpperInvariant();
    }

    static string BuildSubtitle(LevelSelectDoorTransition transition)
    {
        string label = string.IsNullOrWhiteSpace(transition.doorLabel)
            ? transition.gameObject.name
            : transition.doorLabel.Trim();

        return label switch
        {
            "Easy Level" => "Child's Play",
            "Medium Level" => "Think You're Ready?",
            "Hard Level" => "Prove It",
            _ => "Start Here"
        };
    }

    static bool IsTutorialDoor(LevelSelectDoorTransition transition)
    {
        string label = string.IsNullOrWhiteSpace(transition.doorLabel)
            ? transition.gameObject.name
            : transition.doorLabel.Trim();
        return label.Contains("Tutorial");
    }

    static string GetSignName(LevelSelectDoorTransition transition)
    {
        return $"DoorSignRoot_{transition.GetInstanceID()}";
    }

    static GameObject CreateImage(Transform parent, string name, Color color, Vector2 anchorMin, Vector2 anchorMax)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        Image image = go.AddComponent<Image>();
        image.color = color;
        RectTransform rect = image.rectTransform;
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return go;
    }

    static Text CreateText(Transform parent, string name, string content, int fontSize, FontStyle style, Color color, Vector2 anchorMin, Vector2 anchorMax)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        Text text = go.AddComponent<Text>();
        text.font = OverlayTypography.GetFont(fontSize);
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = color;
        text.text = content;
        text.resizeTextForBestFit = true;
        text.resizeTextMinSize = Mathf.Max(14, Mathf.RoundToInt(fontSize * 0.55f));
        text.resizeTextMaxSize = fontSize;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;

        RectTransform rect = text.rectTransform;
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = new Vector3(-1f, 1f, 1f);
        return text;
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
}
