using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Sushil.Systems
{
    public class ResidentStartPointer : MonoBehaviour
    {
        static ResidentStartPointer instance;

        [Header("Display")]
        public float edgePadding = 92f;
        public float smoothing = 12f;
        public float hideWhenCloserThan = 0.2f;
        public Color pointerColor = new Color(1f, 0.82f, 0.42f, 1f);
        public Color dangerColor = new Color(1f, 0.28f, 0.28f, 1f);

        Canvas canvas;
        RectTransform root;
        RectTransform indicatorRect;
        RectTransform arrowRect;
        Text arrowText;
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
            ResolveReferences();
            ResetSmoothing();
        }

        void Start()
        {
            ResolveReferences();
            ResetSmoothing();
        }

        void Update()
        {
            if (StartScreenOverlay.IsShowing || PauseOverlay.IsPaused || GameOverOverlay.IsShowing || EscapeOverlay.IsShowing)
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
            arrowText.color = activeColor;
            infoText.color = activeColor;
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
            canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = short.MaxValue - 3;

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
            indicatorRect.sizeDelta = new Vector2(88f, 82f);

            arrowRect = new GameObject("Arrow", typeof(RectTransform)).GetComponent<RectTransform>();
            arrowRect.SetParent(indicatorRect, false);
            arrowRect.anchorMin = new Vector2(0.5f, 0.5f);
            arrowRect.anchorMax = new Vector2(0.5f, 0.5f);
            arrowRect.pivot = new Vector2(0.5f, 0.5f);
            arrowRect.anchoredPosition = new Vector2(0f, 10f);
            arrowRect.sizeDelta = new Vector2(58f, 58f);

            arrowText = arrowRect.gameObject.AddComponent<Text>();
            arrowText.font = OverlayTypography.GetFont(54);
            arrowText.fontSize = 54;
            arrowText.alignment = TextAnchor.MiddleCenter;
            arrowText.text = "▲";
            arrowText.color = pointerColor;
            arrowText.resizeTextForBestFit = true;
            arrowText.resizeTextMinSize = 24;
            arrowText.resizeTextMaxSize = 58;
            arrowText.raycastTarget = false;

            var arrowOutline = arrowRect.gameObject.AddComponent<Outline>();
            arrowOutline.effectColor = new Color(0f, 0f, 0f, 0.92f);
            arrowOutline.effectDistance = new Vector2(2f, -2f);

            RectTransform infoRect = new GameObject("Info", typeof(RectTransform)).GetComponent<RectTransform>();
            infoRect.SetParent(indicatorRect, false);
            infoRect.anchorMin = new Vector2(0.5f, 0.5f);
            infoRect.anchorMax = new Vector2(0.5f, 0.5f);
            infoRect.pivot = new Vector2(0.5f, 0.5f);
            infoRect.anchoredPosition = new Vector2(0f, -18f);
            infoRect.sizeDelta = new Vector2(80f, 24f);

            infoText = infoRect.gameObject.AddComponent<Text>();
            infoText.font = OverlayTypography.GetFont(18);
            infoText.fontSize = 18;
            infoText.alignment = TextAnchor.MiddleCenter;
            infoText.text = "0m";
            infoText.color = pointerColor;
            infoText.resizeTextForBestFit = true;
            infoText.resizeTextMinSize = 12;
            infoText.resizeTextMaxSize = 20;
            infoText.raycastTarget = false;

            var infoOutline = infoRect.gameObject.AddComponent<Outline>();
            infoOutline.effectColor = new Color(0f, 0f, 0f, 0.88f);
            infoOutline.effectDistance = new Vector2(2f, -2f);
        }
    }
}
