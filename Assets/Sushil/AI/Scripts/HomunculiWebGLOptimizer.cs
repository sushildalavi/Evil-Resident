using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;

namespace Sushil.AI
{
    [DisallowMultipleComponent]
    public class HomunculiWebGLOptimizer : MonoBehaviour
    {
        [Header("Animator")]
        public Animator animator;
        public AnimatorCullingMode webglAnimatorCulling = AnimatorCullingMode.CullUpdateTransforms;

        [Header("Navigation")]
        public NavMeshAgent navAgent;

        [Header("Renderers")]
        public SkinnedMeshRenderer[] skinnedRenderers;
        public bool disableSkinnedMotionVectors = true;
        public bool disableReceiveShadows = true;
        public ShadowCastingMode shadowCastingMode = ShadowCastingMode.On;

        [Header("Audio")]
        public AudioSource[] audioSources;
        [Range(0f, 1f)] public float spatializerPriorityScale = 1f;

        void Awake()
        {
            ResolveReferences();
            ApplyForCurrentPlatform();
        }

        void OnValidate()
        {
            spatializerPriorityScale = Mathf.Clamp01(spatializerPriorityScale);
        }

        void ResolveReferences()
        {
            if (animator == null)
                animator = GetComponent<Animator>() ?? GetComponentInChildren<Animator>(true);

            if (navAgent == null)
                navAgent = GetComponent<NavMeshAgent>();

            if (skinnedRenderers == null || skinnedRenderers.Length == 0)
                skinnedRenderers = GetComponentsInChildren<SkinnedMeshRenderer>(true);

            if (audioSources == null || audioSources.Length == 0)
                audioSources = GetComponentsInChildren<AudioSource>(true);
        }

        void ApplyForCurrentPlatform()
        {
#if UNITY_WEBGL
            ApplyWebGLSettings();
#endif
        }

        void ApplyWebGLSettings()
        {
            if (animator != null)
            {
                animator.cullingMode = webglAnimatorCulling;
            }

            if (navAgent != null)
            {
                navAgent.autoBraking = false;
                navAgent.autoRepath = true;
            }

            if (skinnedRenderers != null)
            {
                for (int i = 0; i < skinnedRenderers.Length; i++)
                {
                    SkinnedMeshRenderer smr = skinnedRenderers[i];
                    if (smr == null)
                        continue;

                    smr.quality = SkinQuality.Auto;
                    smr.updateWhenOffscreen = false;
                    smr.shadowCastingMode = shadowCastingMode;

                    if (disableReceiveShadows)
                        smr.receiveShadows = false;

                    if (disableSkinnedMotionVectors)
                        smr.skinnedMotionVectors = false;
                }
            }

            if (audioSources != null)
            {
                for (int i = 0; i < audioSources.Length; i++)
                {
                    AudioSource source = audioSources[i];
                    if (source == null)
                        continue;

                    source.dopplerLevel = 0f;
                    source.priority = Mathf.RoundToInt(Mathf.Lerp(source.priority, 180f, 1f - spatializerPriorityScale));
                }
            }
        }
    }
}
