using System.Collections;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace Sushil.AI
{
    public class NewLevelCopyRuntimeBootstrap : MonoBehaviour
    {
        const string TargetSceneName = "NewLevel Copy";

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
            if (!scene.IsValid() || !scene.isLoaded || scene.name != TargetSceneName)
                return;

            StartCoroutine(RebuildSceneNavMesh(scene));
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

            var stalkers = FindObjectsByType<StalkerAI>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (var stalker in stalkers)
            {
                if (stalker == null || stalker.gameObject.scene != scene)
                    continue;

                RefreshStalkerOnNavMesh(stalker);
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

        static void RefreshStalkerOnNavMesh(StalkerAI stalker)
        {
            var agent = stalker.agent != null ? stalker.agent : stalker.GetComponent<NavMeshAgent>();
            if (agent == null)
                return;

            if (!agent.enabled)
                agent.enabled = true;

            if (!NavMesh.SamplePosition(stalker.transform.position, out var hit, 6f, NavMesh.AllAreas))
                return;

            agent.Warp(hit.position);
            agent.ResetPath();
            agent.isStopped = false;
        }
    }
}
