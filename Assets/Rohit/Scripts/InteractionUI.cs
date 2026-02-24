using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RohitFPSController))]
public class InteractionUI : MonoBehaviour
{
    Canvas canvas;
    Text promptText;
    Text keyHudText;
    Font builtInFont;

    void Awake()
    {
        LoadFont();
        CreateCanvas();
        CreatePromptText();
        CreateKeyHud();
        AssignToController();
    }

    void LoadFont()
    {
        builtInFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (builtInFont == null)
            builtInFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
    }

    void CreateCanvas()
    {
        GameObject canvasObj = new GameObject("InteractionCanvas");
        canvasObj.transform.SetParent(transform);

        canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        canvasObj.AddComponent<GraphicRaycaster>();
    }

    void CreatePromptText()
    {
        GameObject promptObj = new GameObject("PromptText");
        promptObj.transform.SetParent(canvas.transform, false);

        promptText = promptObj.AddComponent<Text>();
        promptText.text = "";
        promptText.fontSize = 28;
        promptText.color = Color.white;
        promptText.alignment = TextAnchor.MiddleCenter;
        promptText.horizontalOverflow = HorizontalWrapMode.Overflow;
        if (builtInFont != null) promptText.font = builtInFont;

        GameObject bgObj = new GameObject("PromptBackground");
        bgObj.transform.SetParent(promptObj.transform, false);
        bgObj.transform.SetAsFirstSibling();

        Image bg = bgObj.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.6f);

        RectTransform bgRect = bg.rectTransform;
        bgRect.anchorMin = new Vector2(-0.1f, -0.3f);
        bgRect.anchorMax = new Vector2(1.1f, 1.3f);
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;

        RectTransform promptRect = promptText.rectTransform;
        promptRect.anchorMin = new Vector2(0.5f, 0f);
        promptRect.anchorMax = new Vector2(0.5f, 0f);
        promptRect.pivot = new Vector2(0.5f, 0f);
        promptRect.anchoredPosition = new Vector2(0f, 120f);
        promptRect.sizeDelta = new Vector2(500f, 50f);

        promptObj.SetActive(false);
    }

    void CreateKeyHud()
    {
        GameObject hudObj = new GameObject("KeyHudText");
        hudObj.transform.SetParent(canvas.transform, false);

        keyHudText = hudObj.AddComponent<Text>();
        keyHudText.text = "Keys:  O  R  S";
        keyHudText.fontSize = 24;
        keyHudText.color = Color.white;
        keyHudText.alignment = TextAnchor.UpperLeft;
        keyHudText.horizontalOverflow = HorizontalWrapMode.Overflow;
        keyHudText.supportRichText = true;
        if (builtInFont != null) keyHudText.font = builtInFont;

        RectTransform hudRect = keyHudText.rectTransform;
        hudRect.anchorMin = new Vector2(0f, 1f);
        hudRect.anchorMax = new Vector2(0f, 1f);
        hudRect.pivot = new Vector2(0f, 1f);
        hudRect.anchoredPosition = new Vector2(20f, -20f);
        hudRect.sizeDelta = new Vector2(300f, 50f);
    }

    void AssignToController()
    {
        RohitFPSController controller = GetComponent<RohitFPSController>();
        if (controller.promptText == null)
            controller.promptText = promptText;
        if (controller.keyHudText == null)
            controller.keyHudText = keyHudText;
    }
}
