using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public class PuzzleSolvedReceiver : MonoBehaviour
{
    [Header("Vault Reveal")]
    [SerializeField] private VaultDoorRevealController vaultDoorRevealController;
    [SerializeField] private VaultRevealController legacyVaultRevealController;

    [Header("Doors")]
    [SerializeField] private Door[] doorsToUnlock;
    [SerializeField] private bool autoOpenUnlockedDoors;

    [Header("Activation")]
    [SerializeField] private GameObject[] activateObjects;
    [SerializeField] private GameObject[] deactivateObjects;
    [SerializeField] private Behaviour[] enableBehaviours;
    [SerializeField] private Behaviour[] disableBehaviours;

    [Header("Lights")]
    [SerializeField] private Light[] enableLights;
    [SerializeField] private Light[] disableLights;

    [Header("Animators")]
    [SerializeField] private Animator[] animators;
    [SerializeField] private string triggerName = "Solved";
    [SerializeField] private string solvedBoolName = "Solved";
    [SerializeField] private bool setSolvedBool = true;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip solvedSound;

    [Header("One Shot")]
    [SerializeField] private bool triggerOnlyOnce = true;

    [Header("Extra Events")]
    [SerializeField] private UnityEvent onSolved = new UnityEvent();

    private bool hasProcessed;

    public void OnPuzzleSolved()
    {
        TriggerSolvedActions();
    }

    public void TriggerSolvedActions()
    {
        if (triggerOnlyOnce && hasProcessed)
            return;

        hasProcessed = true;

        UnlockDoors();

        if (vaultDoorRevealController != null)
            vaultDoorRevealController.OpenVault();

        if (legacyVaultRevealController != null)
            legacyVaultRevealController.Reveal();

        SetActiveState(activateObjects, true);
        SetActiveState(deactivateObjects, false);
        SetBehavioursEnabled(enableBehaviours, true);
        SetBehavioursEnabled(disableBehaviours, false);
        SetLightsEnabled(enableLights, true);
        SetLightsEnabled(disableLights, false);
        TriggerAnimators();

        if (audioSource != null && solvedSound != null)
            audioSource.PlayOneShot(solvedSound);

        onSolved.Invoke();
    }

    public void SetVaultDoorReveal(VaultDoorRevealController revealController)
    {
        vaultDoorRevealController = revealController;
    }

    public void SetVaultReveal(VaultRevealController revealController)
    {
        legacyVaultRevealController = revealController;
    }

    public void ResetReceiverForTesting()
    {
        hasProcessed = false;
    }

    private void TriggerAnimators()
    {
        if (animators == null)
            return;

        for (int i = 0; i < animators.Length; i++)
        {
            Animator animator = animators[i];
            if (animator == null)
                continue;

            if (!string.IsNullOrWhiteSpace(triggerName))
                animator.SetTrigger(triggerName);

            if (setSolvedBool && !string.IsNullOrWhiteSpace(solvedBoolName))
                animator.SetBool(solvedBoolName, true);
        }
    }

    private void UnlockDoors()
    {
        if (doorsToUnlock == null)
            return;

        for (int i = 0; i < doorsToUnlock.Length; i++)
        {
            Door door = doorsToUnlock[i];
            if (door == null)
                continue;

            door.isLocked = false;
            if (autoOpenUnlockedDoors)
                door.Interact(null);
        }
    }

    private void SetActiveState(GameObject[] targets, bool state)
    {
        if (targets == null)
            return;

        for (int i = 0; i < targets.Length; i++)
        {
            if (targets[i] != null)
                targets[i].SetActive(state);
        }
    }

    private void SetBehavioursEnabled(Behaviour[] behaviours, bool enabledState)
    {
        if (behaviours == null)
            return;

        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] != null)
                behaviours[i].enabled = enabledState;
        }
    }

    private void SetLightsEnabled(Light[] lights, bool enabledState)
    {
        if (lights == null)
            return;

        for (int i = 0; i < lights.Length; i++)
        {
            if (lights[i] != null)
                lights[i].enabled = enabledState;
        }
    }
}
