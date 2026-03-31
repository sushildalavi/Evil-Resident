using System.Collections;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace Sushil.AI
{
    public class NewLevelCopyRuntimeBootstrap : MonoBehaviour
    {
        const string NewLevelCopyScenePath = "Assets/Sushil/NewLevel Copy.unity";

        static NewLevelCopyRuntimeBootstrap instance;
        int lastRebuiltSceneHandle = int.MinValue;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (instance != null) return;

            var go = new GameObject("NewLevelCopyRuntimeBootstrap");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<NewLevelCopyRuntimeBootstrap>();
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
        }

        void Start()
        {
            TryScheduleForScene(SceneManager.GetActiveScene());
        }

        void OnDestroy()
        {
            if (instance == this)
            {
                SceneManager.sceneLoaded -= OnSceneLoaded;
                instance = null;
            }
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            TryScheduleForScene(scene);
        }

        void TryScheduleForScene(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded || !IsTargetScene(scene))
                return;

            StartCoroutine(RebuildSceneNavMesh(scene));
        }

        static bool IsTargetScene(Scene scene)
        {
            // Restrict runtime rebakes to the dedicated copy scene.
            // Rebuilding NewLevel at load bakes doors in the closed state and can
            // disconnect room interiors until dynamic links happen to bridge them.
            return scene.path == NewLevelCopyScenePath;
        }

        IEnumerator RebuildSceneNavMesh(Scene scene)
        {
            if (scene.handle == lastRebuiltSceneHandle)
                yield break;

            lastRebuiltSceneHandle = scene.handle;

            // Let scene objects finish Awake/Start before rebuilding navigation.
            yield return null;
            yield return null;

            var surfaces = FindObjectsByType<NavMeshSurface>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            bool rebuiltAny = false;

            foreach (var surface in surfaces)
            {
                if (surface == null || surface.gameObject.scene != scene)
                    continue;

                ConfigureSurface(surface);
                surface.BuildNavMesh();
                rebuiltAny = true;
            }

            if (!rebuiltAny)
                yield break;

            yield return null;

            var residents = FindObjectsByType<ResidentAI>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (var resident in residents)
            {
                if (resident == null || resident.gameObject.scene != scene)
                    continue;

                RefreshResidentOnNavMesh(resident);
            }
        }

        static void ConfigureSurface(NavMeshSurface surface)
        {
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.overrideVoxelSize = true;
            surface.voxelSize = 0.08f;
            surface.overrideTileSize = true;
            surface.tileSize = 128;
            surface.buildHeightMesh = true;
            surface.minRegionArea = 0.1f;
        }

        static void RefreshResidentOnNavMesh(ResidentAI resident)
        {
            var agent = resident.agent != null ? resident.agent : resident.GetComponent<NavMeshAgent>();
            if (agent == null)
                return;

            if (!agent.enabled)
                agent.enabled = true;

            if (!NavMesh.SamplePosition(resident.transform.position, out var hit, 6f, NavMesh.AllAreas))
                return;

            agent.Warp(hit.position);
            agent.ResetPath();
            agent.isStopped = false;
        }
    }
}
