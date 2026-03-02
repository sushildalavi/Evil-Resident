using UnityEngine;

public class KeyItem : MonoBehaviour, IInteractable
{
    [Header("Key Settings")]
    public KeyType keyType;

    [Header("Floating Animation")]
    public float rotateSpeed = 50f;
    public float bobSpeed = 2f;
    public float bobHeight = 0.25f;

    [Header("Highlight Aura")]
    public bool enableAura = true;
    public Color auraColor = new Color(0.25f, 0.9f, 1f, 1f);
    public int auraOrbCount = 6;
    public float auraRadius = 0.45f;
    public float auraOrbitSpeed = 1.8f;
    public float auraBobAmplitude = 0.12f;
    public float auraPulseSpeed = 2.2f;
    public float auraLightRange = 2.4f;
    public float auraLightIntensity = 1.35f;

    private Vector3 startPos;
    private Transform auraRoot;
    private Transform[] auraOrbs;
    private Light auraLight;

    void Start()
    {
        startPos = transform.position;
        if (enableAura)
            BuildAura();
    }

    void Update()
    {
        transform.Rotate(Vector3.up * rotateSpeed * Time.deltaTime);
        Vector3 pos = startPos;
        pos.y += Mathf.Sin(Time.time * bobSpeed) * bobHeight;
        transform.position = pos;

        if (enableAura)
            AnimateAura();
    }

    public KeyCode GetInteractKey() => KeyCode.F;

    public string GetPrompt(RohitFPSController player)
    {
        PlayerInventory inventory = player.GetComponent<PlayerInventory>();
        if (inventory != null && inventory.HasKey(keyType))
            return $"{keyType} Key (already collected)";
        return $"Press F to pick up {keyType} Key";
    }

    public void Interact(RohitFPSController player)
    {
        PlayerInventory inventory = player.GetComponent<PlayerInventory>();
        if (inventory == null) return;

        if (!inventory.HasKey(keyType))
        {
            inventory.AddKey(keyType);
            KeyPickupOverlay.ShowKeyCollected(keyType);
            Destroy(gameObject);
        }
    }

    void BuildAura()
    {
        if (auraRoot != null) return;

        auraRoot = new GameObject("KeyAura").transform;
        auraRoot.SetParent(transform, false);
        auraRoot.localPosition = Vector3.up * 0.15f;

        int count = Mathf.Clamp(auraOrbCount, 3, 12);
        auraOrbs = new Transform[count];

        Material orbMat = BuildAuraMaterial();
        for (int i = 0; i < count; i++)
        {
            GameObject orb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            orb.name = $"AuraOrb_{i}";
            orb.transform.SetParent(auraRoot, false);
            orb.transform.localScale = Vector3.one * 0.09f;

            Collider c = orb.GetComponent<Collider>();
            if (c != null) Destroy(c);

            var renderer = orb.GetComponent<MeshRenderer>();
            if (renderer != null) renderer.material = orbMat;

            auraOrbs[i] = orb.transform;
        }

        auraLight = gameObject.GetComponentInChildren<Light>();
        if (auraLight == null)
        {
            GameObject lightObj = new GameObject("KeyAuraLight");
            lightObj.transform.SetParent(auraRoot, false);
            auraLight = lightObj.AddComponent<Light>();
        }

        auraLight.type = LightType.Point;
        auraLight.range = auraLightRange;
        auraLight.intensity = auraLightIntensity;
        auraLight.color = auraColor;
        auraLight.shadows = LightShadows.None;
    }

    Material BuildAuraMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        Material mat = new Material(shader);
        mat.color = auraColor;

        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", auraColor);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", auraColor);
        if (mat.HasProperty("_EmissionColor"))
        {
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", auraColor * 2.4f);
        }

        return mat;
    }

    void AnimateAura()
    {
        if (auraOrbs == null || auraOrbs.Length == 0) return;

        float t = Time.time;
        float pulse = 0.85f + 0.25f * Mathf.Sin(t * auraPulseSpeed);
        float orbit = t * auraOrbitSpeed;

        for (int i = 0; i < auraOrbs.Length; i++)
        {
            Transform orb = auraOrbs[i];
            if (orb == null) continue;

            float phase = (Mathf.PI * 2f * i) / auraOrbs.Length;
            float x = Mathf.Cos(orbit + phase) * auraRadius;
            float z = Mathf.Sin(orbit + phase) * auraRadius;
            float y = Mathf.Sin((orbit * 2.1f) + phase) * auraBobAmplitude;

            orb.localPosition = new Vector3(x, y, z);
            orb.localScale = Vector3.one * (0.08f * pulse);
        }

        if (auraLight != null)
            auraLight.intensity = auraLightIntensity * pulse;
    }
}
