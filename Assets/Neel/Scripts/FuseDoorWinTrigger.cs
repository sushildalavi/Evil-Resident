using UnityEngine;
using Sushil.Systems;

public class FuseDoorWinTrigger : MonoBehaviour
{
    [Header("Optional")]
    public FuseDoor fuseDoor;
    public GameObject winUI;

    [Header("Open Detection")]
    [Tooltip("If not assigned, this object's transform is monitored.")]
    public Transform monitoredDoorTransform;
    [Tooltip("Trigger escape UI once local rotation changes beyond this angle.")]
    public float openedAngleThreshold = 8f;

    private Quaternion initialLocalRotation;
    private bool initialized;
    private bool triggered;

    void Start()
    {
        if (monitoredDoorTransform == null)
            monitoredDoorTransform = transform;

        initialLocalRotation = monitoredDoorTransform.localRotation;
        initialized = true;

        if (winUI != null)
            winUI.SetActive(false);
    }

    void Update()
    {
        if (!initialized || triggered) return;

        // Optional safety: only evaluate when a FuseDoor exists on this setup.
        if (fuseDoor == null)
            fuseDoor = GetComponent<FuseDoor>();
        if (fuseDoor == null) return;

        float delta = Quaternion.Angle(initialLocalRotation, monitoredDoorTransform.localRotation);
        if (delta < Mathf.Max(1f, openedAngleThreshold)) return;

        triggered = true;

        if (winUI != null)
            winUI.SetActive(true);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        EscapeOverlay.Show();
    }
}
