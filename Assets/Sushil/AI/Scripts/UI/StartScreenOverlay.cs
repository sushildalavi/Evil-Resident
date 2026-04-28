using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Sushil.Systems
{
    public class StartScreenOverlay : MonoBehaviour
    {
        static StartScreenOverlay instance;
        public static bool IsShowing => instance != null && instance.showing;

        Canvas canvas;
        GameObject root;
        bool showing;
        Text productionText;
        Text titleText;
        Text difficultyLabel;
        Text taglineText;
        Text riddleText;
        Text startHintText;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (instance == null)
            {
                var go = new GameObject("StartScreenOverlay");
                instance = go.AddComponent<StartScreenOverlay>();
            }
            else
            {
                instance.RefreshForCurrentScene();
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
            BuildUI();
            SceneManager.sceneLoaded += OnSceneLoaded;
            RefreshForCurrentScene();
        }

        void OnDestroy()
        {
            if (instance == this) instance = null;
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            ApplySceneText();
            RefreshForCurrentScene();
        }

        void Update()
        {
            if (!showing) return;
            if (WasStartPressed())
                HideAndStart();
        }

        void Show()
        {
            if (root == null) BuildUI();
            showing = true;
            if (root != null) root.SetActive(true);
            Time.timeScale = 0f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        void HideAndStart()
        {
            showing = false;
            if (root != null) root.SetActive(false);
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            GameAnalyticsTracker.BeginRun();
        }

        void RefreshForCurrentScene()
        {
            Scene activeScene = SceneManager.GetActiveScene();
            if (ShouldSuppressOverlayInScene(activeScene))
            {
                HideImmediate();
                return;
            }

            Show();
        }

        void HideImmediate()
        {
            showing = false;
            if (root != null) root.SetActive(false);

            Scene activeScene = SceneManager.GetActiveScene();
            if (IsMenuScene(activeScene.name))
            {
                Time.timeScale = 0f;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                return;
            }

            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        void BuildUI()
        {
            if (canvas != null && root != null) return;

            canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = short.MaxValue - 1;

            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>();

            root = new GameObject("StartRoot", typeof(RectTransform));
            root.transform.SetParent(transform, false);
            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;
            rootRect.localScale = Vector3.one;

            CreateBackground(root.transform);
            CreateVignette(root.transform);
            Transform content = CreateContentCard(root.transform);
            CreateAccentBar(content, top: true);
            CreateAccentBar(content, top: false);

            // "5 GUYS AT FREDDY'S presents" — small italic red header
            productionText = CreateText(content, "Production",
                "5 GUYS AT FREDDY'S  presents",
                22, FontStyle.Italic, TextAnchor.MiddleCenter,
                new Color(0.65f, 0.18f, 0.18f, 0.85f),
                new Vector2(0.08f, 0.86f), new Vector2(0.92f, 0.92f));

            // "EVIL RESIDENT" — huge bleeding-red title with glow + flicker
            titleText = CreateText(content, "Title",
                "EVIL  RESIDENT",
                88, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Color(0.92f, 0.18f, 0.18f, 1f),
                new Vector2(0.04f, 0.66f), new Vector2(0.96f, 0.84f));
            AddBloodGlow(titleText.gameObject);
            titleText.gameObject.AddComponent<HorrorTitleFlicker>();

            // "— EASY MODE —" / "— MEDIUM —" / "— HARD —" subtitle below title
            difficultyLabel = CreateText(content, "Difficulty",
                "— MEDIUM —",
                28, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Color(0.95f, 0.92f, 0.85f, 0.95f),
                new Vector2(0.10f, 0.58f), new Vector2(0.90f, 0.65f));

            taglineText = CreateText(content, "Tagline",
                "Escape from the main door.",
                26, FontStyle.Normal, TextAnchor.MiddleCenter,
                new Color(0.85f, 0.85f, 0.88f, 0.95f),
                new Vector2(0.10f, 0.46f), new Vector2(0.90f, 0.54f));

            riddleText = CreateText(content, "Riddle",
                string.Empty,
                22, FontStyle.Italic, TextAnchor.MiddleCenter,
                new Color(0.78f, 0.78f, 0.82f, 0.85f),
                new Vector2(0.10f, 0.30f), new Vector2(0.90f, 0.42f), 1.15f);

            // Bordered "PRESS TO BEGIN" call-to-action with pulsing yellow glow
            startHintText = CreateText(content, "StartHint",
                "[  PRESS  ENTER  OR  SPACE  TO  BEGIN  ]",
                30, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Color(1f, 0.86f, 0.36f, 1f),
                new Vector2(0.05f, 0.08f), new Vector2(0.95f, 0.17f));
            var startHintOutline = startHintText.gameObject.AddComponent<Outline>();
            startHintOutline.effectColor = new Color(0.45f, 0.30f, 0.05f, 0.9f);
            startHintOutline.effectDistance = new Vector2(2f, -2f);
            startHintText.gameObject.AddComponent<HorrorTitleFlicker>(); // gentle flicker on the call-to-action

            ApplySceneText();
        }

        static void AddBloodGlow(GameObject titleObject)
        {
            var a = titleObject.AddComponent<Outline>();
            a.effectColor = new Color(0.85f, 0.05f, 0.05f, 0.85f);
            a.effectDistance = new Vector2(2f, -2f);
            var b = titleObject.AddComponent<Outline>();
            b.effectColor = new Color(0.50f, 0.02f, 0.02f, 0.7f);
            b.effectDistance = new Vector2(4f, -4f);
            var drop = titleObject.AddComponent<Shadow>();
            drop.effectColor = new Color(0f, 0f, 0f, 0.95f);
            drop.effectDistance = new Vector2(5f, -5f);
        }

        static void CreateAccentBar(Transform parent, bool top)
        {
            var go = new GameObject(top ? "AccentTop" : "AccentBottom", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            if (top)
            {
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(1f, 1f);
                rect.pivot = new Vector2(0.5f, 1f);
            }
            else
            {
                rect.anchorMin = new Vector2(0f, 0f);
                rect.anchorMax = new Vector2(1f, 0f);
                rect.pivot = new Vector2(0.5f, 0f);
            }
            rect.sizeDelta = new Vector2(0f, 3f);
            rect.anchoredPosition = Vector2.zero;
            var img = go.AddComponent<Image>();
            img.color = new Color(0.85f, 0.10f, 0.10f, 0.9f);
            img.raycastTarget = false;
        }

        static void CreateVignette(Transform parent)
        {
            var go = new GameObject("Vignette", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var img = go.AddComponent<Image>();
            img.color = new Color(0.07f, 0f, 0f, 0.30f);
            img.raycastTarget = false;
        }

        void ApplySceneText()
        {
            if (titleText == null || taglineText == null || riddleText == null || startHintText == null)
                return;

            Scene activeScene = SceneManager.GetActiveScene();
            string scenePath = activeScene.path;
            string sceneName = activeScene.name;
            string sceneNameLower = string.IsNullOrWhiteSpace(sceneName) ? string.Empty : sceneName.ToLowerInvariant();

            // Reset visibility — every section is shown by default; specific paths hide what they don't need.
            titleText.gameObject.SetActive(true);
            if (difficultyLabel != null) difficultyLabel.gameObject.SetActive(true);
            taglineText.gameObject.SetActive(true);
            riddleText.gameObject.SetActive(true);
            startHintText.gameObject.SetActive(true);
            if (productionText != null) productionText.gameObject.SetActive(true);

            // Title is ALWAYS "EVIL RESIDENT" — what changes is the difficulty label / tagline / riddle.
            titleText.text = "EVIL  RESIDENT";

            // Menu scenes — usually suppressed by ShouldSuppressOverlayInScene, but defensive copy here too.
            if (sceneName == "Level Select" || sceneName == "Difficulty Select")
            {
                if (difficultyLabel != null) difficultyLabel.text = "— MAIN MENU —";
                taglineText.text = sceneName == "Difficulty Select"
                    ? "Choose your challenge."
                    : "Pick where to begin.";
                riddleText.gameObject.SetActive(false);
                startHintText.text = "[  PRESS  SPACE  TO  START  ]";
                return;
            }

            // Legacy tutorial scene
            if (scenePath == "Assets/Neel/Tutorial.unity")
            {
                if (difficultyLabel != null) difficultyLabel.text = "— TUTORIAL —";
                taglineText.text = "Learn the controls step by step.";
                riddleText.gameObject.SetActive(false);
                startHintText.text = "[  PRESS  ENTER  OR  SPACE  TO  BEGIN  ]";
                return;
            }

            // Standard gameplay scenes — Easy / Medium / Hard
            if (scenePath.StartsWith("Assets/Sahil/Test/") ||
                scenePath == "Assets/Sushil/Easy Level.unity")
            {
                string difficulty = "—";
                string tagline = "Escape from the main door.";
                if (sceneNameLower.Contains("easy"))
                {
                    difficulty = "EASY";
                    tagline = "Find the keys. Reach the main door. Don't get caught.";
                }
                else if (sceneNameLower.Contains("medium"))
                {
                    difficulty = "MEDIUM";
                    tagline = "He's faster now. Sees further. Listens harder.";
                }
                else if (sceneNameLower.Contains("difficult") || sceneNameLower.Contains("hard"))
                {
                    difficulty = "HARD";
                    tagline = "Mistakes are fatal. Hide well. Move quietly.";
                }

                if (difficultyLabel != null)
                    difficultyLabel.text = "— " + difficulty + " —";
                taglineText.text = tagline;

                if (IsEasyScene(scenePath, sceneNameLower))
                {
                    riddleText.gameObject.SetActive(false);
                }
                else
                {
                    riddleText.gameObject.SetActive(true);
                    riddleText.text = "\"Walls here do not have ears... but they might show something.\"";
                }
                startHintText.text = "[  PRESS  ENTER  OR  SPACE  TO  BEGIN  ]";
                return;
            }

            // Fallback for unrecognized scenes
            if (difficultyLabel != null) difficultyLabel.text = "— SURVIVAL —";
            taglineText.text = "Collect the keys. Escape. Don't be seen.";
            riddleText.gameObject.SetActive(false);
            startHintText.text = "[  PRESS  ENTER  OR  SPACE  TO  BEGIN  ]";
        }

        bool ShouldSuppressOverlayInScene(Scene scene)
        {
            string sceneName = string.IsNullOrWhiteSpace(scene.name) ? string.Empty : scene.name.ToLowerInvariant();
            return sceneName.Contains("tutorial") ||
                   sceneName == "level select" ||
                   sceneName == "difficulty select";
        }

        static bool IsMenuScene(string sceneName)
        {
            return sceneName == "Level Select" || sceneName == "Difficulty Select";
        }

        bool IsEasyScene(string scenePath, string sceneNameLower)
        {
            return scenePath == "Assets/Sahil/Test/Easy Level.unity" ||
                   scenePath == "Assets/Sushil/Easy Level.unity" ||
                   sceneNameLower.Contains("easy");
        }

        void CreateBackground(Transform parent)
        {
            GameObject bgObj = new GameObject("Background");
            bgObj.transform.SetParent(parent, false);
            var bg = bgObj.AddComponent<Image>();
            bg.color = Color.black; // fully opaque
            bg.raycastTarget = false;
            var bgRect = bg.rectTransform;
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;
        }

        Transform CreateContentCard(Transform parent)
        {
            var obj = new GameObject("ContentCard", typeof(RectTransform));
            obj.transform.SetParent(parent, false);
            var image = obj.AddComponent<Image>();
            image.color = new Color(0.03f, 0.03f, 0.04f, 1f);
            image.raycastTarget = false;

            // Stacked red glow outlines — same horror aesthetic as PauseOverlay/MainMenu
            var inner = obj.AddComponent<Outline>();
            inner.effectColor = new Color(0.85f, 0.10f, 0.10f, 0.65f);
            inner.effectDistance = new Vector2(2f, -2f);
            var outer = obj.AddComponent<Outline>();
            outer.effectColor = new Color(0.45f, 0.02f, 0.02f, 0.40f);
            outer.effectDistance = new Vector2(4f, -4f);

            var rect = image.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(900f, 760f);
            rect.anchoredPosition = Vector2.zero;
            return obj.transform;
        }

        Text CreateText(
            Transform parent,
            string name,
            string content,
            int fontSize,
            FontStyle style,
            TextAnchor anchor,
            Color color,
            Vector2 anchorMin,
            Vector2 anchorMax,
            float lineSpacing = 1f)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            Text text = obj.AddComponent<Text>();
            text.font = OverlayTypography.GetFont(fontSize);
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.alignment = anchor;
            text.color = color;
            text.text = content;
            text.lineSpacing = lineSpacing;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = Mathf.Max(12, Mathf.RoundToInt(fontSize * 0.55f));
            text.resizeTextMaxSize = fontSize;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;

            RectTransform rect = text.rectTransform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return text;
        }

        void AddTextEffects(GameObject textObj, Color outlineColor)
        {
            var shadow = textObj.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.85f);
            shadow.effectDistance = new Vector2(3f, -3f);

            var outline = textObj.AddComponent<Outline>();
            outline.effectColor = outlineColor;
            outline.effectDistance = new Vector2(2f, -2f);
        }

        bool WasStartPressed()
        {
            bool pressed = false;
#if ENABLE_LEGACY_INPUT_MANAGER
            pressed |= Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space);
#endif
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
                pressed |= Keyboard.current.enterKey.wasPressedThisFrame ||
                           Keyboard.current.numpadEnterKey.wasPressedThisFrame ||
                           Keyboard.current.spaceKey.wasPressedThisFrame;
#endif
            return pressed;
        }
    }
}
