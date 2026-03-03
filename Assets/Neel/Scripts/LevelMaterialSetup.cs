using UnityEngine;

/// <summary>
/// Applies distinct wall and floor materials at runtime so walls and floor read clearly
/// and edges are easier to see. Walls get a subtle procedural texture for variation.
/// </summary>
public class LevelMaterialSetup : MonoBehaviour
{
    const string WallMaterialPath = "Materials/WallMaterial";
    const string FloorMaterialPath = "Materials/FloorMaterial";

    [Header("Optional")]
    [Tooltip("Off by default. Turn on only if you want this script to override scene wall/floor materials at runtime.")]
    public bool autoApplyOnStart = false;

    void Start()
    {
        if (autoApplyOnStart)
            ApplyLevelMaterials();
    }

    [ContextMenu("Apply Level Materials Now")]
    public void ApplyLevelMaterials()
    {
        Material wallMatSource = Resources.Load<Material>(WallMaterialPath);
        Material floorMat = Resources.Load<Material>(FloorMaterialPath);
        if (wallMatSource == null || floorMat == null)
            return;

        // Wall: use an instance with a subtle texture so walls aren't flat
        Material wallMat = new Material(wallMatSource);
        Texture2D wallTex = CreateWallTexture();
        if (wallTex != null)
        {
            wallMat.SetTexture("_BaseMap", wallTex);
            wallMat.SetTextureScale("_BaseMap", new Vector2(6f, 6f));
        }

        GameObject walls = GameObject.Find("Walls");
        if (walls != null)
        {
            foreach (var r in walls.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (r != null && r.sharedMaterials != null && r.sharedMaterials.Length > 0)
                    r.sharedMaterial = wallMat;
            }
        }

        GameObject floor = GameObject.Find("Floor");
        if (floor != null)
        {
            foreach (var r in floor.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (r != null && r.sharedMaterials != null && r.sharedMaterials.Length > 0)
                    r.sharedMaterial = floorMat;
            }
        }
    }

    static Texture2D CreateWallTexture()
    {
        int size = 128;
        var tex = new Texture2D(size, size);
        tex.wrapMode = TextureWrapMode.Repeat;
        tex.filterMode = FilterMode.Bilinear;

        // Base wall color (warm gray)
        float r = 0.38f, g = 0.35f, b = 0.33f;
        float variation = 0.06f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float u = x / (float)size;
                float v = y / (float)size;
                float n = Mathf.PerlinNoise(u * 4f, v * 4f) * 2f - 1f;
                float n2 = Mathf.PerlinNoise(u * 12f + 3f, v * 12f) * 2f - 1f;
                float offset = (n * 0.5f + n2 * 0.25f) * variation;

                float rr = Mathf.Clamp01(r + offset);
                float gg = Mathf.Clamp01(g + offset);
                float bb = Mathf.Clamp01(b + offset * 0.8f);
                tex.SetPixel(x, y, new Color(rr, gg, bb, 1f));
            }
        }

        tex.Apply(true, true);
        return tex;
    }
}
