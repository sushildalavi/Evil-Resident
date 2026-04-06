using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Sushil.Systems
{
    public class ResidentStartPointer : MonoBehaviour
    {
        static ResidentStartPointer instance;
        static Sprite triangleSprite;

        [Header("Display")]
        public float edgePadding = 92f;
        public float smoothing = 12f;
        public float hideWhenCloserThan = 0.2f;
        public Color pointerColor = new Color(1f, 0.82f, 0.42f, 1f);
        public Color dangerColor = new Color(1f, 0.28f, 0.28f, 1f);

        RectTransform root;
        RectTransform indicatorRect;
        RectTransform arrowRect;
        Image arrowShaftImage;
        Image arrowTipImage;
        Image arrowWingLeftImage;
        Image arrowWingRightImage;
        Image infoPlateImage;
        Text infoText;

        Transform playerCamera;
        Camera playerCameraComponent;
        Transform resident;
        Vector2 smoothPos;
        float smoothAngle;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (instance != null) return;
            GameObject go = new GameObject("ResidentStartPointer");
            instance = go.AddComponent<ResidentStartPointer>();
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
            BuildUI();
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        void OnDestroy()
        {
            if (instance == this) instance = null;
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            RefreshState();
        }

        void Start()
        {
            RefreshState();
        }

        void Update()
        {
            if (ShouldHidePointer())
            {
                SetVisible(false);
                return;
            }

            SetVisible(true);

            if (playerCamera == null || resident == null || playerCameraComponent == null || !playerCameraComponent.isActiveAndEnabled)
            {
                ResolveReferences();
                if (playerCamera == null || resident == null || playerCameraComponent == null)
                    return;
            }

            Vector3 worldTarget = resident.position + Vector3.up * 1.3f;
            Vector3 toResident = worldTarget - playerCamera.position;
            Vector3 flatToResident = new Vector3(toResident.x, 0f, toResident.z);
            float distance = flatToResident.magnitude;

            if (distance < hideWhenCloserThan)
            {
                indicatorRect.gameObject.SetActive(false);
                return;
            }

            Vector3 viewport = playerCameraComponent.WorldToViewportPoint(worldTarget);
            bool behindCamera = viewport.z <= 0.01f;

            Vector2 canvasSize = GetCanvasSize();
            Vector2 margin = new Vector2(
                Mathf.Clamp01(edgePadding / Mathf.Max(1f, canvasSize.x)),
                Mathf.Clamp01(edgePadding / Mathf.Max(1f, canvasSize.y)));

            indicatorRect.gameObject.SetActive(true);

            bool onScreen = !behindCamera &&
                            viewport.x >= 0f &&
                            viewport.x <= 1f &&
                            viewport.y >= 0f &&
                            viewport.y <= 1f;

            Vector2 targetPos;
            float targetAngle;
            if (onScreen)
            {
                Vector2 clampedViewport = new Vector2(
                    Mathf.Clamp(viewport.x, margin.x, 1f - margin.x),
                    Mathf.Clamp(viewport.y, margin.y, 1f - margin.y));
                targetPos = ViewportToCanvas(clampedViewport, canvasSize);

                Vector2 dir = targetPos.sqrMagnitude > 1f ? targetPos.normalized : Vector2.up;
                targetAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
            }
            else
            {
                Vector3 camForward = playerCamera.forward;
                camForward.y = 0f;
                if (camForward.sqrMagnitude < 0.0001f)
                    camForward = Vector3.forward;
                camForward.Normalize();

                Vector3 camRight = playerCamera.right;
                camRight.y = 0f;
                if (camRight.sqrMagnitude < 0.0001f)
                    camRight = Vector3.right;
                camRight.Normalize();

                Vector3 flatDir3 = flatToResident.sqrMagnitude > 0.0001f ? flatToResident.normalized : camForward;
                Vector2 dir = new Vector2(Vector3.Dot(camRight, flatDir3), Vector3.Dot(camForward, flatDir3));
                if (behindCamera)
                    dir = -dir;
                if (dir.sqrMagnitude < 0.001f)
                    dir = Vector2.up;
                dir.Normalize();

                Vector2 extents = (canvasSize * 0.5f) - new Vector2(edgePadding, edgePadding);
                extents.x = Mathf.Max(24f, extents.x);
                extents.y = Mathf.Max(24f, extents.y);
                float scaleToEdge = Mathf.Min(
                    Mathf.Abs(extents.x / Mathf.Max(0.001f, Mathf.Abs(dir.x))),
                    Mathf.Abs(extents.y / Mathf.Max(0.001f, Mathf.Abs(dir.y))));
                targetPos = dir * scaleToEdge;
                targetAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
            }

            float smoothT = 1f - Mathf.Exp(-Mathf.Max(1f, smoothing) * Time.unscaledDeltaTime);
            smoothPos = Vector2.Lerp(smoothPos, targetPos, smoothT);
            smoothAngle = Mathf.LerpAngle(smoothAngle, targetAngle, smoothT);

            indicatorRect.anchoredPosition = smoothPos;
            arrowRect.localRotation = Quaternion.Euler(0f, 0f, smoothAngle);

            bool veryClose = distance <= 3f;
            Color activeColor = veryClose ? dangerColor : pointerColor;
            ApplyMarkerColor(activeColor);
            infoText.text = $"{distance:0}m";
        }

        void ResolveReferences()
        {
            if (playerCameraComponent == null || !playerCameraComponent.isActiveAndEnabled)
            {
                Camera cam = Camera.main;
                if (cam == null) cam = FindFirstObjectByType<Camera>();
                if (cam != null)
                {
                    playerCameraComponent = cam;
                    playerCamera = cam.transform;
                }
            }

            if (resident == null)
            {
                var ai = FindFirstObjectByType<Sushil.AI.ResidentAI>();
                if (ai != null) resident = ai.transform;
            }
        }

        void RefreshState()
        {
            ResolveReferences();
            ResetSmoothing();
        }

        bool ShouldHidePointer()
        {
            return StartScreenOverlay.IsShowing ||
                   PauseOverlay.IsPaused ||
                   GameOverOverlay.IsShowing ||
                   EscapeOverlay.IsShowing;
        }

        void ResetSmoothing()
        {
            smoothPos = Vector2.zero;
            smoothAngle = 0f;
            if (indicatorRect != null)
                indicatorRect.gameObject.SetActive(false);
        }

        void SetVisible(bool visible)
        {
            if (root != null) root.gameObject.SetActive(visible);
        }

        Vector2 GetCanvasSize()
        {
            if (root != null && root.rect.size.sqrMagnitude > 1f)
                return root.rect.size;
            return new Vector2(Screen.width, Screen.height);
        }

        Vector2 ViewportToCanvas(Vector2 viewport, Vector2 canvasSize)
        {
            return new Vector2(
                (viewport.x - 0.5f) * canvasSize.x,
                (viewport.y - 0.5f) * canvasSize.y);
        }

        void BuildUI()
        {
            var overlayCanvas = gameObject.AddComponent<Canvas>();
            overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            overlayCanvas.sortingOrder = short.MaxValue - 3;

            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>();

            root = new GameObject("PointerRoot", typeof(RectTransform)).GetComponent<RectTransform>();
            root.SetParent(transform, false);
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = Vector2.zero;
            root.offsetMax = Vector2.zero;

            indicatorRect = new GameObject("Indicator", typeof(RectTransform)).GetComponent<RectTransform>();
            indicatorRect.SetParent(root, false);
            indicatorRect.anchorMin = new Vector2(0.5f, 0.5f);
            indicatorRect.anchorMax = new Vector2(0.5f, 0.5f);
            indicatorRect.pivot = new Vector2(0.5f, 0.5f);
            indicatorRect.sizeDelta = new Vector2(64f, 68f);

            arrowRect = new GameObject("Arrow", typeof(RectTransform)).GetComponent<RectTransform>();
            arrowRect.SetParent(indicatorRect, false);
            arrowRect.anchorMin = new Vector2(0.5f, 0.5f);
            arrowRect.anchorMax = new Vector2(0.5f, 0.5f);
            arrowRect.pivot = new Vector2(0.5f, 0.5f);
            arrowRect.anchoredPosition = new Vector2(0f, 8f);
            arrowRect.sizeDelta = new Vector2(42f, 42f);

            arrowShaftImage = CreateImageElement(
                "ArrowShaft",
                arrowRect,
                new Vector2(4f, 18f),
                new Vector2(0f, -5f),
                pointerColor);
            AddOutline(arrowShaftImage.gameObject, new Color(0f, 0f, 0f, 0.95f), new Vector2(1f, -1f));

            arrowTipImage = CreateImageElement(
                "ArrowTip",
                arrowRect,
                new Vector2(18f, 16f),
                new Vector2(0f, 10f),
                pointerColor,
                0f);
            arrowTipImage.sprite = GetTriangleSprite();
            arrowTipImage.preserveAspect = true;
            AddOutline(arrowTipImage.gameObject, new Color(0f, 0f, 0f, 0.95f), new Vector2(1f, -1f));

            arrowWingLeftImage = CreateImageElement(
                "ArrowShoulderLeft",
                arrowRect,
                new Vector2(3f, 10f),
                new Vector2(-5f, 2f),
                pointerColor,
                58f);
            AddOutline(arrowWingLeftImage.gameObject, new Color(0f, 0f, 0f, 0.95f), new Vector2(1f, -1f));

            arrowWingRightImage = CreateImageElement(
                "ArrowShoulderRight",
                arrowRect,
                new Vector2(3f, 10f),
                new Vector2(5f, 2f),
                pointerColor,
                -58f);
            AddOutline(arrowWingRightImage.gameObject, new Color(0f, 0f, 0f, 0.95f), new Vector2(1f, -1f));

            RectTransform infoRect = new GameObject("Info", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            infoRect.SetParent(indicatorRect, false);
            infoRect.anchorMin = new Vector2(0.5f, 0.5f);
            infoRect.anchorMax = new Vector2(0.5f, 0.5f);
            infoRect.pivot = new Vector2(0.5f, 0.5f);
            infoRect.anchoredPosition = new Vector2(0f, -18f);
            infoRect.sizeDelta = new Vector2(52f, 18f);
            infoPlateImage = infoRect.GetComponent<Image>();
            infoPlateImage.color = new Color(0.04f, 0.04f, 0.05f, 0.7f);
            infoPlateImage.raycastTarget = false;
            AddOutline(infoRect.gameObject, new Color(0f, 0f, 0f, 0.9f), new Vector2(1f, -1f));

            RectTransform infoLabelRect = new GameObject("InfoLabel", typeof(RectTransform)).GetComponent<RectTransform>();
            infoLabelRect.SetParent(infoRect, false);
            infoLabelRect.anchorMin = Vector2.zero;
            infoLabelRect.anchorMax = Vector2.one;
            infoLabelRect.offsetMin = Vector2.zero;
            infoLabelRect.offsetMax = Vector2.zero;

            infoText = CreatePointerText(infoLabelRect.gameObject, 14, "0m", pointerColor, 10, 16);
            AddOutline(infoLabelRect.gameObject, new Color(0f, 0f, 0f, 0.88f), new Vector2(1f, -1f));
            ApplyMarkerColor(pointerColor);
        }

        Image CreateImageElement(string name, Transform parent, Vector2 size, Vector2 anchoredPosition, Color color, float rotationZ = 0f)
        {
            RectTransform rect = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
            rect.localRotation = Quaternion.Euler(0f, 0f, rotationZ);

            Image image = rect.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        Text CreatePointerText(GameObject owner, int fontSize, string content, Color color, int minSize, int maxSize)
        {
            Text text = owner.AddComponent<Text>();
            text.font = OverlayTypography.GetFont(fontSize);
            text.fontSize = fontSize;
            text.alignment = TextAnchor.MiddleCenter;
            text.text = content;
            text.color = color;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = minSize;
            text.resizeTextMaxSize = maxSize;
            text.raycastTarget = false;
            return text;
        }

        Sprite GetTriangleSprite()
        {
            if (triangleSprite != null)
                return triangleSprite;

            Texture2D texture = new Texture2D(32, 32, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;

            float centerX = 15.5f;
            for (int y = 0; y < 32; y++)
            {
                float t = y / 31f;
                float halfWidth = Mathf.Lerp(13.5f, 0.5f, t);
                for (int x = 0; x < 32; x++)
                {
                    float edge = halfWidth - Mathf.Abs(x - centerX);
                    float alpha = Mathf.Clamp01(edge + 1f);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            texture.Apply();
            triangleSprite = Sprite.Create(texture, new Rect(0f, 0f, 32f, 32f), new Vector2(0.5f, 0.18f), 100f);
            return triangleSprite;
        }

        void ApplyMarkerColor(Color activeColor)
        {
            if (arrowShaftImage != null)
                arrowShaftImage.color = activeColor;

            if (arrowTipImage != null)
                arrowTipImage.color = activeColor;

            if (arrowWingLeftImage != null)
                arrowWingLeftImage.color = activeColor;

            if (arrowWingRightImage != null)
                arrowWingRightImage.color = activeColor;

            if (infoPlateImage != null)
                infoPlateImage.color = new Color(0.04f, 0.04f, 0.05f, 0.7f);

            if (infoText != null)
                infoText.color = Color.Lerp(activeColor, Color.white, 0.1f);
        }

        void AddOutline(GameObject owner, Color color, Vector2 distance)
        {
            var outline = owner.AddComponent<Outline>();
            outline.effectColor = color;
            outline.effectDistance = distance;
        }
    }
}
