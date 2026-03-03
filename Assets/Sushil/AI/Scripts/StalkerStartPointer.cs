using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Sushil.Systems
{
    public class StalkerStartPointer : MonoBehaviour
    {
        static StalkerStartPointer instance;

        [Header("Display")]
        public float showSeconds = 8f;
        public float pointerRadius = 150f;
        public float smoothing = 10f;
        public float hideWhenCloserThan = 2.2f;
        public Color pointerColor = new Color(1f, 0.25f, 0.25f, 1f);

        Canvas canvas;
        RectTransform root;
        RectTransform arrowRect;
        Text arrowText;
        Text infoText;

        Transform playerCamera;
        Transform stalker;
        float hideAtTime;
        bool active;
        Vector2 smoothPos;
        float smoothAngle;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (instance != null) return;
            GameObject go = new GameObject("StalkerStartPointer");
            instance = go.AddComponent<StalkerStartPointer>();
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
            StartPointerWindow();
        }

        void Start()
        {
            ResolveReferences();
            StartPointerWindow();
        }

        void Update()
        {
            if (!active) return;
            if (StartScreenOverlay.IsShowing) return;
            if (Time.unscaledTime >= hideAtTime)
            {
                SetVisible(false);
                active = false;
                return;
            }

            if (playerCamera == null || stalker == null)
            {
                ResolveReferences();
                if (playerCamera == null || stalker == null) return;
            }

            Vector3 toStalker = stalker.position - playerCamera.position;
            Vector3 flat = new Vector3(toStalker.x, 0f, toStalker.z);
            float distance = flat.magnitude;
            if (distance < hideWhenCloserThan)
            {
                arrowRect.gameObject.SetActive(false);
                infoText.text = "WATCHER: VERY CLOSE";
                return;
            }
            arrowRect.gameObject.SetActive(true);

            Vector3 camForward = playerCamera.forward;
            camForward.y = 0f;
            camForward.Normalize();
            Vector3 camRight = playerCamera.right;
            camRight.y = 0f;
            camRight.Normalize();
            Vector3 dir = flat.normalized;

            float x = Vector3.Dot(camRight, dir);
            float y = Vector3.Dot(camForward, dir);
            Vector2 uiDir = new Vector2(x, y).normalized;

            float angle = Mathf.Atan2(uiDir.y, uiDir.x) * Mathf.Rad2Deg - 90f;
            Vector2 targetPos = uiDir * pointerRadius;
            float t = 1f - Mathf.Exp(-Mathf.Max(1f, smoothing) * Time.unscaledDeltaTime);
            smoothPos = Vector2.Lerp(smoothPos, targetPos, t);
            smoothAngle = Mathf.LerpAngle(smoothAngle, angle, t);
            arrowRect.anchoredPosition = smoothPos;
            arrowRect.localRotation = Quaternion.Euler(0f, 0f, smoothAngle);

            infoText.text = $"WATCHER: {distance:0}m";
        }

        void ResolveReferences()
        {
            if (playerCamera == null)
            {
                Camera cam = Camera.main;
                if (cam == null) cam = FindFirstObjectByType<Camera>();
                if (cam != null) playerCamera = cam.transform;
            }

            if (stalker == null)
            {
                var ai = FindFirstObjectByType<Sushil.AI.StalkerAI>();
                if (ai != null) stalker = ai.transform;
            }
        }

        void StartPointerWindow()
        {
            active = true;
            hideAtTime = Time.unscaledTime + Mathf.Max(0.5f, showSeconds);
            smoothPos = Vector2.zero;
            smoothAngle = 0f;
            SetVisible(true);
        }

        void SetVisible(bool visible)
        {
            if (root != null) root.gameObject.SetActive(visible);
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
            root.anchorMin = new Vector2(0.5f, 0.5f);
            root.anchorMax = new Vector2(0.5f, 0.5f);
            root.pivot = new Vector2(0.5f, 0.5f);
            root.anchoredPosition = Vector2.zero;
            root.sizeDelta = new Vector2(400f, 400f);

            arrowRect = new GameObject("Arrow", typeof(RectTransform)).GetComponent<RectTransform>();
            arrowRect.SetParent(root, false);
            arrowRect.anchorMin = new Vector2(0.5f, 0.5f);
            arrowRect.anchorMax = new Vector2(0.5f, 0.5f);
            arrowRect.pivot = new Vector2(0.5f, 0.5f);
            arrowRect.sizeDelta = new Vector2(60f, 60f);

            arrowText = arrowRect.gameObject.AddComponent<Text>();
            arrowText.font = OverlayTypography.GetFont(52);
            arrowText.fontSize = 52;
            arrowText.alignment = TextAnchor.MiddleCenter;
            arrowText.text = "▲";
            arrowText.color = pointerColor;
            arrowText.resizeTextForBestFit = true;
            arrowText.resizeTextMinSize = 24;
            arrowText.resizeTextMaxSize = 56;

            RectTransform infoRect = new GameObject("Info", typeof(RectTransform)).GetComponent<RectTransform>();
            infoRect.SetParent(root, false);
            infoRect.anchorMin = new Vector2(0.5f, 0.5f);
            infoRect.anchorMax = new Vector2(0.5f, 0.5f);
            infoRect.pivot = new Vector2(0.5f, 0.5f);
            infoRect.anchoredPosition = new Vector2(0f, -210f);
            infoRect.sizeDelta = new Vector2(420f, 70f);

            infoText = infoRect.gameObject.AddComponent<Text>();
            infoText.font = OverlayTypography.GetFont(30);
            infoText.fontSize = 30;
            infoText.alignment = TextAnchor.MiddleCenter;
            infoText.text = "WATCHER";
            infoText.color = new Color(1f, 0.85f, 0.5f, 1f);
            infoText.resizeTextForBestFit = true;
            infoText.resizeTextMinSize = 16;
            infoText.resizeTextMaxSize = 32;
        }
    }
}
