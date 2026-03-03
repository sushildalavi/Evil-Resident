using UnityEngine;
using UnityEngine.SceneManagement;

namespace Sushil.Systems
{
    public static class WallColliderRuntimeFix
    {
        const float MinAxisScale = 0.001f;
        const float MinColliderThickness = 0.08f;
        static bool hasApplied;
        static int addedColliderCount;
        static int fixedScaleCount;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Apply()
        {
            if (hasApplied) return;
            hasApplied = true;

            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded) return;

            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
                FixHierarchyRecursive(roots[i].transform, underWallsParent: false);

            // Final fail-safe: if any wall/border object still has no collider, add one from mesh bounds.
            for (int i = 0; i < roots.Length; i++)
                AddFallbackBoundsColliders(roots[i].transform, underWallsParent: false);

            Debug.Log($"[WallColliderRuntimeFix] Applied. Added/updated colliders: {addedColliderCount}, fixed zero-scale walls: {fixedScaleCount}");
        }

        static void FixHierarchyRecursive(Transform t, bool underWallsParent)
        {
            if (t == null) return;

            string lower = t.name.ToLowerInvariant();
            bool inWalls = underWallsParent || lower == "walls";
            bool looksLikeWall = inWalls || lower.Contains("wall") || lower.Contains("border");

            if (looksLikeWall)
                EnsureSolidWallCollider(t);

            for (int i = 0; i < t.childCount; i++)
                FixHierarchyRecursive(t.GetChild(i), inWalls);
        }

        static void EnsureSolidWallCollider(Transform t)
        {
            if (t == null) return;
            if (!t.gameObject.activeInHierarchy) return;

            // Fix invalid zero scales that collapse collision volume.
            Vector3 s = t.localScale;
            bool scaleChanged = false;
            if (Mathf.Abs(s.x) < MinAxisScale) { s.x = 1f; scaleChanged = true; }
            if (Mathf.Abs(s.y) < MinAxisScale) { s.y = 1f; scaleChanged = true; }
            if (Mathf.Abs(s.z) < MinAxisScale) { s.z = 1f; scaleChanged = true; }
            if (scaleChanged)
            {
                t.localScale = s;
                fixedScaleCount++;
            }

            Collider existing = t.GetComponent<Collider>();
            MeshFilter mf = t.GetComponent<MeshFilter>();
            Mesh mesh = mf != null ? mf.sharedMesh : null;

            bool useSolidBox = existing == null || existing is MeshCollider;
            if (useSolidBox)
            {
                if (existing is MeshCollider oldMc)
                    Object.Destroy(oldMc);

                BoxCollider box = t.GetComponent<BoxCollider>();
                if (box == null) box = t.gameObject.AddComponent<BoxCollider>();
                ConfigureBoxFromMesh(box, mesh);
                box.isTrigger = false;
                box.enabled = true;
                addedColliderCount++;
                return;
            }

            if (existing is BoxCollider bc)
            {
                ClampBoxThickness(bc);
                bc.isTrigger = false;
                bc.enabled = true;
                return;
            }

            existing.isTrigger = false;
            existing.enabled = true;
        }

        static void AddFallbackBoundsColliders(Transform t, bool underWallsParent)
        {
            if (t == null) return;
            string lower = t.name.ToLowerInvariant();
            bool inWalls = underWallsParent || lower == "walls";
            bool looksLikeWall = inWalls || lower.Contains("wall") || lower.Contains("border");

            if (looksLikeWall && !lower.Contains("door") && !lower.Contains("key"))
            {
                Collider c = t.GetComponent<Collider>();
                MeshRenderer mr = t.GetComponent<MeshRenderer>();
                if (c == null && mr != null)
                {
                    BoxCollider box = t.gameObject.AddComponent<BoxCollider>();
                    ConfigureBoxFromRenderer(box, mr);
                    box.isTrigger = false;
                    box.enabled = true;
                    addedColliderCount++;
                }

                // Ensure existing box colliders are thick enough.
                BoxCollider bc = t.GetComponent<BoxCollider>();
                if (bc != null)
                {
                    ClampBoxThickness(bc);
                    bc.isTrigger = false;
                    bc.enabled = true;
                }
            }

            for (int i = 0; i < t.childCount; i++)
                AddFallbackBoundsColliders(t.GetChild(i), inWalls);
        }

        static void ConfigureBoxFromMesh(BoxCollider box, Mesh mesh)
        {
            if (box == null) return;
            if (mesh == null)
            {
                box.center = Vector3.zero;
                box.size = new Vector3(1f, MinColliderThickness, 1f);
                return;
            }

            Bounds b = mesh.bounds;
            Vector3 size = b.size;
            if (size.x < MinColliderThickness) size.x = MinColliderThickness;
            if (size.y < MinColliderThickness) size.y = MinColliderThickness;
            if (size.z < MinColliderThickness) size.z = MinColliderThickness;

            box.center = b.center;
            box.size = size;
        }

        static void ConfigureBoxFromRenderer(BoxCollider box, MeshRenderer mr)
        {
            if (box == null || mr == null) return;
            MeshFilter mf = mr.GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
            {
                ConfigureBoxFromMesh(box, mf.sharedMesh);
                return;
            }

            Bounds b = mr.bounds;
            Vector3 localCenter = box.transform.InverseTransformPoint(b.center);
            Vector3 lossy = box.transform.lossyScale;
            Vector3 localSize = new Vector3(
                SafeDiv(b.size.x, Mathf.Abs(lossy.x)),
                SafeDiv(b.size.y, Mathf.Abs(lossy.y)),
                SafeDiv(b.size.z, Mathf.Abs(lossy.z))
            );

            if (localSize.x < MinColliderThickness) localSize.x = MinColliderThickness;
            if (localSize.y < MinColliderThickness) localSize.y = MinColliderThickness;
            if (localSize.z < MinColliderThickness) localSize.z = MinColliderThickness;

            box.center = localCenter;
            box.size = localSize;
        }

        static float SafeDiv(float a, float b)
        {
            if (b < 0.0001f) return a;
            return a / b;
        }

        static void ClampBoxThickness(BoxCollider box)
        {
            Vector3 size = box.size;
            if (size.x < MinColliderThickness) size.x = MinColliderThickness;
            if (size.y < MinColliderThickness) size.y = MinColliderThickness;
            if (size.z < MinColliderThickness) size.z = MinColliderThickness;
            box.size = size;
        }
    }
}
