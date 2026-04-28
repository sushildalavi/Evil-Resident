using UnityEngine;
using UnityEngine.AI;
using Sushil.Systems;

namespace Sushil.AI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AudioSource))]
    public class HomunculiFootsteps : MonoBehaviour
    {
        [Header("References")]
        public Animator animator;
        public NavMeshAgent agent;
        public AudioSource footstepAudioSource;

        [Header("Wood Walk Clips (01-10)")]
        public AudioClip[] woodWalkClips;

        [Header("Step Sync")]
        [Range(0f, 1f)] public float firstStepPhase = 0.12f;
        [Range(0f, 1f)] public float secondStepPhase = 0.62f;
        [Min(0.01f)] public float minMoveSpeed = 0.08f;
        [Min(0.05f)] public float minStepInterval = 0.2f;

        [Header("Heavy Footstep Sound")]
        [Range(0f, 2f)] public float volumeScale = 1.25f;
        public Vector2 randomVolumeRange = new Vector2(0.85f, 1.0f);
        public Vector2 randomPitchRange = new Vector2(0.72f, 0.86f);

        [Header("3D Distance")]
        [Range(0f, 1f)] public float spatialBlend = 1f;
        [Min(0.1f)] public float minDistance = 2.5f;
        [Min(0.2f)] public float maxDistance = 24f;
        [Range(0f, 5f)] public float dopplerLevel = 0f;

        [Header("Listener Fade (Close vs Far Clarity)")]
        public Transform listenerTransform;
        public bool autoFindListener = true;
        [Min(0f)] public float closeDistance = 4.5f;
        [Min(0.1f)] public float farDistance = 10f;
        [Min(0.2f)] public float safeDistance = 16f;
        [Range(0f, 1f)] public float farLoudness = 0.55f;
        [Range(0f, 1f)] public float safeCutoffLoudness = 0.02f;

        [Header("AI Noise")]
        public bool emitNoiseSystemFootstep = false;
        [Min(0f)] public float noiseIntensity = 8f;
        public string noiseType = "footstep";

        float lastCyclePhase;
        float nextAllowedStepTime;
        float nextReferenceResolveTime;
        int lastClipIndex = -1;

        void Awake()
        {
            ResolveReferences();
            ConfigureAudioSource();
            lastCyclePhase = GetCyclePhase();
        }

        void OnEnable()
        {
            ResolveReferences();
            ConfigureAudioSource();
            lastCyclePhase = GetCyclePhase();
            nextAllowedStepTime = Time.time;
            nextReferenceResolveTime = Time.time;
        }

        void OnValidate()
        {
            if (randomVolumeRange.x > randomVolumeRange.y)
                randomVolumeRange.x = randomVolumeRange.y;

            if (randomPitchRange.x > randomPitchRange.y)
                randomPitchRange.x = randomPitchRange.y;

            minDistance = Mathf.Max(0.1f, minDistance);
            maxDistance = Mathf.Max(minDistance + 0.1f, maxDistance);
            minStepInterval = Mathf.Max(0.05f, minStepInterval);
            closeDistance = Mathf.Max(0f, closeDistance);
            farDistance = Mathf.Max(closeDistance + 0.1f, farDistance);
            safeDistance = Mathf.Max(farDistance + 0.1f, safeDistance);
            farLoudness = Mathf.Clamp01(farLoudness);
            safeCutoffLoudness = Mathf.Clamp01(safeCutoffLoudness);
        }

        void LateUpdate()
        {
            if (Time.time >= nextReferenceResolveTime)
            {
                ResolveReferences();
                nextReferenceResolveTime = Time.time + 1f;
            }

            if (animator == null || footstepAudioSource == null)
                return;

            ConfigureAudioSource();

            float cyclePhase = GetCyclePhase();
            bool isMoving = IsActuallyMoving();

            if (!isMoving)
            {
                lastCyclePhase = cyclePhase;
                return;
            }

            TryStepOnPhaseCrossing(lastCyclePhase, cyclePhase, firstStepPhase);
            TryStepOnPhaseCrossing(lastCyclePhase, cyclePhase, secondStepPhase);
            lastCyclePhase = cyclePhase;
        }

        void ResolveReferences()
        {
            if (animator == null)
                animator = GetComponent<Animator>() ?? GetComponentInChildren<Animator>();

            if (agent == null)
                agent = GetComponent<NavMeshAgent>();

            if (footstepAudioSource == null)
                footstepAudioSource = GetComponent<AudioSource>();

            if (listenerTransform == null && autoFindListener)
            {
                var anyListener = FindFirstObjectByType<AudioListener>();
                if (anyListener != null)
                    listenerTransform = anyListener.transform;
            }
        }

        void ConfigureAudioSource()
        {
            if (footstepAudioSource == null)
                return;

            footstepAudioSource.playOnAwake = false;
            footstepAudioSource.loop = false;
            footstepAudioSource.spatialBlend = spatialBlend;
            footstepAudioSource.minDistance = minDistance;
            footstepAudioSource.maxDistance = maxDistance;
            footstepAudioSource.dopplerLevel = dopplerLevel;
            footstepAudioSource.rolloffMode = AudioRolloffMode.Logarithmic;
        }

        bool IsActuallyMoving()
        {
            float speedSqrThreshold = minMoveSpeed * minMoveSpeed;

            if (agent != null)
            {
                if (!agent.enabled || !agent.isOnNavMesh || agent.isStopped)
                    return false;

                return agent.velocity.sqrMagnitude >= speedSqrThreshold;
            }

            return true;
        }

        float GetCyclePhase()
        {
            if (animator == null)
                return 0f;

            AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
            return Mathf.Repeat(state.normalizedTime, 1f);
        }

        void TryStepOnPhaseCrossing(float previousPhase, float currentPhase, float marker)
        {
            if (Time.time < nextAllowedStepTime)
                return;

            if (!CrossedMarker(previousPhase, currentPhase, marker))
                return;

            PlayFootstep();
            nextAllowedStepTime = Time.time + minStepInterval;
        }

        static bool CrossedMarker(float previousPhase, float currentPhase, float marker)
        {
            if (previousPhase <= currentPhase)
                return previousPhase < marker && marker <= currentPhase;

            // Wrapped from ~1 back to 0.
            return previousPhase < marker || marker <= currentPhase;
        }

        void PlayFootstep()
        {
            if (woodWalkClips == null || woodWalkClips.Length == 0 || footstepAudioSource == null)
                return;

            float distanceLoudness = GetDistanceLoudness();
            if (distanceLoudness <= safeCutoffLoudness)
                return;

            int clipIndex = GetNextClipIndex();
            AudioClip clip = woodWalkClips[clipIndex];
            if (clip == null)
                return;

            footstepAudioSource.pitch = Random.Range(randomPitchRange.x, randomPitchRange.y);
            float randomVolume = Random.Range(randomVolumeRange.x, randomVolumeRange.y) * volumeScale * distanceLoudness;
            footstepAudioSource.PlayOneShot(clip, randomVolume);

            if (emitNoiseSystemFootstep)
                NoiseSystem.Emit(transform.position, noiseIntensity * distanceLoudness, noiseType);
        }

        float GetDistanceLoudness()
        {
            if (listenerTransform == null)
                return 1f;

            float distance = Vector3.Distance(transform.position, listenerTransform.position);
            if (distance >= safeDistance)
                return 0f;

            if (distance <= closeDistance)
                return 1f;

            if (distance <= farDistance)
            {
                float tNearToFar = Mathf.InverseLerp(closeDistance, farDistance, distance);
                return Mathf.Lerp(1f, farLoudness, tNearToFar);
            }

            float tFarToSafe = Mathf.InverseLerp(farDistance, safeDistance, distance);
            return Mathf.Lerp(farLoudness, 0f, tFarToSafe);
        }

        int GetNextClipIndex()
        {
            int clipCount = woodWalkClips.Length;
            if (clipCount == 1)
            {
                lastClipIndex = 0;
                return 0;
            }

            int index = Random.Range(0, clipCount);
            if (index == lastClipIndex)
                index = (index + 1) % clipCount;

            lastClipIndex = index;
            return index;
        }
    }
}
