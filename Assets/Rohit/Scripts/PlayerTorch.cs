using UnityEngine;
using UnityEngine.UI;
using Sushil.Systems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class PlayerTorch : MonoBehaviour
{
    [Header("References")]
    public Transform cameraTransform;
    public Light torchLight;
    public Text torchHudText;

    [Header("Controls")]
    public KeyCode toggleKey = KeyCode.T;

    [Header("Torch Settings")]
    public bool startOn = true;
    public float intensity = 9f;
    public float range = 22f;
    [Range(10f, 120f)] public float spotAngle = 58f;
    public Color lightColor = new Color(1f, 0.96f, 0.86f);

    [Header("Battery (Optional)")]
    public bool useBattery = false;
    public float maxBatterySeconds = 90f;
    public float drainPerSecond = 1f;
    public float rechargePerSecond = 0.4f;

    float battery;
    bool isOn;

    void Start()
    {
        if (cameraTransform == null)
        {
            Camera cam = GetComponentInChildren<Camera>();
            if (cam != null) cameraTransform = cam.transform;
        }

        if (torchLight == null)
            CreateTorchLight();

        battery = Mathf.Max(1f, maxBatterySeconds);
        SetTorch(startOn);
        ApplyLightSettings();
        RefreshHud();
    }

    void Update()
    {
        if (PauseOverlay.IsPaused || StartScreenOverlay.IsShowing || GameOverOverlay.IsShowing || EscapeOverlay.IsShowing)
        {
            RefreshHud();
            return;
        }

        if (WasTogglePressed())
        {
            if (!useBattery || battery > 0.05f)
                SetTorch(!isOn);
        }

        if (useBattery)
            UpdateBattery();

        RefreshHud();
    }

    void UpdateBattery()
    {
        if (isOn)
        {
            battery -= drainPerSecond * Time.deltaTime;
            if (battery <= 0f)
            {
                battery = 0f;
                SetTorch(false);
            }
        }
        else
        {
            battery += rechargePerSecond * Time.deltaTime;
            if (battery > maxBatterySeconds) battery = maxBatterySeconds;
        }
    }

    bool WasTogglePressed()
    {
        bool pressed = false;
#if ENABLE_LEGACY_INPUT_MANAGER
        pressed |= Input.GetKeyDown(toggleKey);
#endif
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && toggleKey == KeyCode.T)
            pressed |= Keyboard.current.tKey.wasPressedThisFrame;
#endif
        return pressed;
    }

    void SetTorch(bool on)
    {
        isOn = on;
        if (torchLight != null) torchLight.enabled = on;
    }

    void ApplyLightSettings()
    {
        if (torchLight == null) return;
        torchLight.type = LightType.Spot;
        torchLight.intensity = intensity;
        torchLight.range = range;
        torchLight.spotAngle = spotAngle;
        torchLight.color = lightColor;
        torchLight.shadows = LightShadows.Soft;
    }

    void CreateTorchLight()
    {
        Transform parent = cameraTransform != null ? cameraTransform : transform;
        GameObject torch = new GameObject("PlayerTorchLight");
        torch.transform.SetParent(parent, false);
        torch.transform.localPosition = new Vector3(0.08f, -0.05f, 0.2f);
        torch.transform.localRotation = Quaternion.identity;
        torchLight = torch.AddComponent<Light>();
    }

    void RefreshHud()
    {
        if (torchHudText == null) return;
        if (!useBattery)
        {
            torchHudText.text = $"Torch: {(isOn ? "ON" : "OFF")} ({toggleKey})";
            return;
        }

        float pct = (maxBatterySeconds <= 0.01f) ? 0f : (battery / maxBatterySeconds) * 100f;
        torchHudText.text = $"Torch: {(isOn ? "ON" : "OFF")} {pct:0}% ({toggleKey})";
    }
}
