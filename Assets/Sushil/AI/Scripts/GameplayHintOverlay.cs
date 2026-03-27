using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Sushil.Systems
{
    public class GameplayHintOverlay : MonoBehaviour
    {
        static GameplayHintOverlay instance;

        [Header("Display")]
        public float messageDuration = 1.8f;
        public float startHintDelay = 0.5f;
        public float hideHintCooldown = 5f;

        RohitFPSController player;
        bool lastHidden;
        bool shownRunHintThisRound;
        float nextHideHintAtTime;
        float roundStartTime;
        float hideAtTime;
        string pendingMessage;
        bool ownsPromptText;
        string ownedMessage;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (instance != null) return;
            GameObject go = new GameObject("GameplayHintOverlay");
            instance = go.AddComponent<GameplayHintOverlay>();
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
            ResetRoundState();
        }

        void OnDestroy()
        {
            if (instance == this) instance = null;
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            ResetRoundState();
        }

        void ResetRoundState()
        {
            ReleaseOwnedPrompt();
            player = null;
            lastHidden = false;
            shownRunHintThisRound = false;
            nextHideHintAtTime = 0f;
            roundStartTime = Time.unscaledTime;
            hideAtTime = 0f;
            pendingMessage = null;
        }

        void LateUpdate()
        {
            if (StartScreenOverlay.IsShowing || GameOverOverlay.IsShowing || EscapeOverlay.IsShowing || PauseOverlay.IsPaused)
            {
                ReleaseOwnedPrompt();
                return;
            }

            if (player == null)
                player = FindFirstObjectByType<RohitFPSController>();
            if (player == null || player.promptText == null)
            {
                ReleaseOwnedPrompt();
                return;
            }

            if (!shownRunHintThisRound && Time.unscaledTime >= roundStartTime + Mathf.Max(0f, startHintDelay))
            {
                shownRunHintThisRound = true;
                QueueHint("Tip: Hold SHIFT to sprint away from the Resident.");
            }

            bool hidden = player.isHidden;
            if (hidden && !lastHidden && Time.unscaledTime >= nextHideHintAtTime)
            {
                nextHideHintAtTime = Time.unscaledTime + Mathf.Max(1f, hideHintCooldown);
                QueueHint("Stay hidden till he is fully out of sight. Beware: he can fake leaving.");
            }
            lastHidden = hidden;

            if (ownsPromptText)
            {
                if (Time.unscaledTime >= hideAtTime)
                {
                    ReleaseOwnedPrompt();
                }
                else
                {
                    if (!IsExternalPromptShowing())
                        ReapplyOwnedPrompt();
                    else
                        ReleaseOwnedPrompt();
                }
            }

            if (string.IsNullOrEmpty(pendingMessage))
                return;

            if (IsExternalPromptShowing())
                return;

            ShowHintOnPrompt(pendingMessage);
            pendingMessage = null;
        }

        void QueueHint(string msg)
        {
            pendingMessage = msg;
        }

        bool IsExternalPromptShowing()
        {
            Text prompt = player.promptText;
            if (prompt == null) return false;
            if (!prompt.gameObject.activeInHierarchy) return false;

            string t = prompt.text;
            if (string.IsNullOrWhiteSpace(t)) return false;
            if (ownsPromptText && t == ownedMessage) return false;
            return true;
        }

        void ShowHintOnPrompt(string msg)
        {
            Text prompt = player.promptText;
            if (prompt == null) return;

            // Force same clean built-in font to avoid platform font mismatches.
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font != null) prompt.font = font;

            prompt.text = msg;
            prompt.gameObject.SetActive(true);

            ownsPromptText = true;
            ownedMessage = msg;
            hideAtTime = Time.unscaledTime + Mathf.Max(1f, messageDuration);
        }

        void ReapplyOwnedPrompt()
        {
            if (player == null || player.promptText == null || string.IsNullOrEmpty(ownedMessage)) return;
            Text prompt = player.promptText;
            prompt.text = ownedMessage;
            if (!prompt.gameObject.activeSelf)
                prompt.gameObject.SetActive(true);
        }

        void ReleaseOwnedPrompt()
        {
            if (!ownsPromptText) return;

            if (player != null && player.promptText != null)
            {
                Text prompt = player.promptText;
                if (prompt.text == ownedMessage)
                    prompt.gameObject.SetActive(false);
            }

            ownsPromptText = false;
            ownedMessage = null;
            hideAtTime = 0f;
        }
    }
}
