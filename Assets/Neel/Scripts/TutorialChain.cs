using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Sushil.Systems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// Auto-bootstrapped controller that links the four tutorial scenes together.
//
// Some tutorial scenes (notably Tutorial 2 / fuse) don't have a door-transition
// component placed on a door, so finishing them used to leave the player stranded.
// This MonoBehaviour watches the active TutorialStepUI in any "New Tutorial N"
// scene; when the tutorial reports Complete it fades to black, shows a brief
// "Tutorial N+1" card, and loads the next scene. If there is no next tutorial
// (e.g. the chain has ended) it returns to the main menu / Level Select.
//
// WebGL-safe — pure built-in Unity UI, no Resources lookups beyond the legacy
// runtime font, no shaders, no external sprites.
public class TutorialChain : MonoBehaviour
{
    const string MainMenuSceneName = "Level Select";
    const string TutorialPrefix = "New Tutorial ";

    static TutorialChain instance;
    bool transitioning;
    float sceneEnteredAt; // unscaled time when current scene loaded
    const float InteractGraceSeconds = 1.5f; // ignore E-press triggers during warmup

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (instance != null) return;
        var go = new GameObject("TutorialChain");
        instance = go.AddComponent<TutorialChain>();
        DontDestroyOnLoad(go);
    }

    void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;
        // No overlay UI — transitions are instant (no fade, no card).
        SceneManager.sceneLoaded += OnSceneLoaded;
        RohitFPSController.OnPrimaryInteraction += OnPlayerInteract;
        // Run once for the scene that's already loaded when bootstrap fires.
        OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        RohitFPSController.OnPrimaryInteraction -= OnPlayerInteract;
    }

    // Whenever the player interacts with anything in a tutorial scene, check if it
    // looks like a door (by component type/name) OR if the current tutorial prompt
    // mentions "door" — either signal triggers the next-tutorial transition.
    void OnPlayerInteract(RohitFPSController source, IInteractable interactable)
    {
        if (transitioning) return;
        if (Time.unscaledTime - sceneEnteredAt < InteractGraceSeconds) return;
        Scene active = SceneManager.GetActiveScene();
        if (!IsTutorialScene(active.name)) return;

        bool doorLike = interactable != null && LooksLikeDoor(interactable);
        bool promptIsDoor = PromptMentionsDoor();
        if (!(doorLike && promptIsDoor)) return;

        StopAllCoroutines();
        StartCoroutine(TransitionToNextTutorial(active.name));
    }

    // Direct E-key listener: when the tutorial prompt explicitly says "interact with
    // the door", any E-press triggers the transition — even if the door has no
    // IInteractable script attached. This is the primary path for Tutorial 1, whose
    // exit door is just geometry with no behaviour.
    void Update()
    {
        if (transitioning) return;
        if (Time.unscaledTime - sceneEnteredAt < InteractGraceSeconds) return;

        Scene active = SceneManager.GetActiveScene();
        if (!IsTutorialScene(active.name)) return;
        if (!PromptMentionsDoor()) return;

        // Don't react while another overlay owns input.
        if (PauseOverlay.IsPaused || StartScreenOverlay.IsShowing ||
            GameOverOverlay.IsShowing || EscapeOverlay.IsShowing) return;

        if (IsInteractKeyPressed())
        {
            StopAllCoroutines();
            StartCoroutine(TransitionToNextTutorial(active.name));
        }
    }

    static bool IsInteractKeyPressed()
    {
        bool pressed = false;
#if ENABLE_LEGACY_INPUT_MANAGER
        pressed |= Input.GetKeyDown(KeyCode.E);
#endif
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
            pressed |= Keyboard.current.eKey.wasPressedThisFrame;
#endif
        return pressed;
    }

    static bool LooksLikeDoor(IInteractable interactable)
    {
        var asMono = interactable as MonoBehaviour;
        if (asMono == null) return false;

        // Quick component-type check covers the common door scripts in this project.
        if (asMono is Door) return true;
        if (asMono is FuseDoor) return true;
        if (asMono is MainDoor) return true;
        if (asMono is TutorialDoorTransition) return true;
        if (asMono is TutorialDoorSceneTransition) return true;

        // Fallback: name contains "door" — catches any custom door script we don't know about.
        string n = asMono.gameObject.name;
        return !string.IsNullOrEmpty(n) && n.ToLowerInvariant().Contains("door");
    }

    // True when the active TutorialStepUI prompt is the "go to the next level via door"
    // step (or the fuse-tutorial equivalent). At that point the next interaction is
    // intended to advance the chain regardless of what GameObject it landed on.
    static bool PromptMentionsDoor()
    {
        Text t = FindStepInstructionText();
        if (t == null || string.IsNullOrEmpty(t.text)) return false;
        string lower = t.text.ToLowerInvariant();
        return lower.Contains("door") ||
               lower.Contains("next level") ||
               lower.Contains("continue") ||
               lower.Contains("escape");
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        transitioning = false;
        sceneEnteredAt = Time.unscaledTime;
        if (!IsTutorialScene(scene.name)) return;
        StopAllCoroutines();
        StartCoroutine(WatchForTutorialCompletion(scene.name));
    }

    IEnumerator WatchForTutorialCompletion(string sceneName)
    {
        // Brief grace so initial scene-load physics can settle before safety nets fire.
        const float SceneWarmupSeconds = 1.5f;
        float warmupRemaining = SceneWarmupSeconds;
        while (warmupRemaining > 0f)
        {
            warmupRemaining -= Time.unscaledDeltaTime;
            if (transitioning) yield break;
            yield return null;
        }

        TutorialStepUI step = FindFirstObjectByType<TutorialStepUI>();
        Text instructionText = FindStepInstructionText();

        // Re-cache player spawn AFTER warmup — by now physics + spawners have settled.
        Transform player = FindPlayerTransform();
        Vector3 spawnPos = player != null ? player.position : Vector3.zero;
        bool hasSpawn = player != null;
        const float FallBelowSpawn   = 1.5f;   // small threshold = fast door-walk-through transition
        const float MaxHorizontalOOB = 80f;
        // Confirm the fall is sustained (player is genuinely falling, not just landing
        // from a jump) by requiring the dip to persist briefly before transitioning.
        const float SustainedFallSeconds = 0.25f;
        float fallSustainedFor = 0f;

        bool sawNonBlankPrompt = false;
        float blankFor = 0f;
        const float requiredBlank = 1.0f;

        while (!transitioning)
        {
            // Safety nets — only count after warmup, only meaningful if the player exists.
            if (hasSpawn && player != null)
            {
                bool falling = (spawnPos.y - player.position.y) >= FallBelowSpawn;
                fallSustainedFor = falling ? fallSustainedFor + Time.unscaledDeltaTime : 0f;
                if (fallSustainedFor >= SustainedFallSeconds)
                {
                    StartCoroutine(TransitionToNextTutorial(sceneName));
                    yield break;
                }

                Vector3 flatDelta = player.position - spawnPos;
                flatDelta.y = 0f;
                if (flatDelta.sqrMagnitude >= MaxHorizontalOOB * MaxHorizontalOOB)
                {
                    StartCoroutine(TransitionToNextTutorial(sceneName));
                    yield break;
                }
            }

            if (player == null)
            {
                player = FindPlayerTransform();
                if (player != null && !hasSpawn)
                {
                    spawnPos = player.position;
                    hasSpawn = true;
                }
            }

            // If a pause/menu/game-over overlay is up, don't advance the blank-prompt timer.
            if (PauseOverlay.IsPaused || StartScreenOverlay.IsShowing ||
                GameOverOverlay.IsShowing || EscapeOverlay.IsShowing)
            {
                blankFor = 0f;
                yield return null;
                continue;
            }

            if (step != null)
            {
                if (instructionText == null)
                    instructionText = FindStepInstructionText();

                bool blank = instructionText == null ||
                             string.IsNullOrEmpty(instructionText.text) ||
                             (instructionText.canvasRenderer != null && instructionText.canvasRenderer.GetAlpha() < 0.05f);

                if (!blank) sawNonBlankPrompt = true;

                // Only count blanks toward auto-transition AFTER we've seen a real prompt.
                blankFor = (blank && sawNonBlankPrompt) ? blankFor + Time.unscaledDeltaTime : 0f;

                if (blankFor >= requiredBlank)
                {
                    StartCoroutine(TransitionToNextTutorial(sceneName));
                    yield break;
                }
            }

            yield return null;
        }
    }

    static Transform FindPlayerTransform()
    {
        var rohit = FindFirstObjectByType<RohitFPSController>();
        if (rohit != null) return rohit.transform;
        var death = FindFirstObjectByType<PlayerDeath>();
        if (death != null) return death.transform;
        var hide = FindFirstObjectByType<PlayerHide>();
        if (hide != null) return hide.transform;
        var tagged = GameObject.FindGameObjectWithTag("Player");
        return tagged != null ? tagged.transform : null;
    }

    static Text FindStepInstructionText()
    {
        // TutorialStepUI builds its UI under a "TutorialStepCanvas" GameObject and
        // labels the text "InstructionText".
        GameObject canvasGO = GameObject.Find("TutorialStepCanvas");
        if (canvasGO == null) return null;
        Transform t = canvasGO.transform.Find("InstructionText");
        return t != null ? t.GetComponent<Text>() : null;
    }

    IEnumerator TransitionToNextTutorial(string fromScene)
    {
        transitioning = true;
        string nextScene = ComputeNextScene(fromScene);
        bool toMainMenu = string.IsNullOrEmpty(nextScene);

        // Instant load — no fade animation, no transition card.
        yield return null;

        if (toMainMenu)
            SceneManager.LoadScene(MainMenuSceneName);
        else
            SceneManager.LoadScene(nextScene);
    }

    static string ComputeNextScene(string current)
    {
        if (string.IsNullOrEmpty(current) || !current.StartsWith(TutorialPrefix))
            return null;
        string tail = current.Substring(TutorialPrefix.Length).Trim();
        if (!int.TryParse(tail, out int n)) return null;
        string next = TutorialPrefix + (n + 1);
        // Probe the build settings: if the next scene isn't included we're at the end.
        if (SceneUtility.GetBuildIndexByScenePath(next) >= 0) return next;
        if (SceneUtility.GetBuildIndexByScenePath($"Assets/Sahil/Tutorial/{next}.unity") >= 0) return next;
        return null; // chain ended, caller will go back to Main Menu
    }

    static bool IsTutorialScene(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName)) return false;
        if (!sceneName.StartsWith(TutorialPrefix)) return false;
        string tail = sceneName.Substring(TutorialPrefix.Length).Trim();
        return int.TryParse(tail, out _);
    }

}
