using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Sushil.Systems;
using Sushil.Demo;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Sushil.AI
{
    public class ResidentRohitNoiseBridge : MonoBehaviour
    {
        [Header("Player Source")]
        public RohitFPSController playerController;
        public ThrowRock throwRock;
        public CharacterController playerCharacterController;
        public bool autoFindPlayer = true;

        [Header("Movement Noise")]
        public bool emitMovementNoise = false;
        public float minMoveSpeed = 0.15f;
        public float walkStepInterval = 0.55f;
        public float sprintStepInterval = 0.35f;
        public float walkNoiseIntensity = 4f;
        public float sprintNoiseIntensity = 8f;
        public string footstepNoiseType = "footstep";

        [Header("Jump/Land Noise")]
        public bool emitJumpLandNoise = false;
        public float jumpNoiseIntensity = 10f;
        public float landingNoiseIntensity = 12f;
        public string jumpNoiseType = "jump";
        public string landingNoiseType = "land";

        [Header("Throw Noise")]
        public bool enableThrowIntegration = false;
        public KeyCode throwKey = KeyCode.G;
        public bool emitThrowReleaseNoise = false;
        public float throwReleaseNoiseIntensity = 6f;
        public string throwNoiseType = "throw";

        [Header("Thrown Rock Impact Noise")]
        public bool autoConfigureThrownRockImpactNoise = true;
        public float impactNoiseIntensity = 18f;
        public float impactMinSpeed = 0.75f;
        public float projectileLifeSeconds = 6f;
        public string impactNoiseType = "throwImpact";
        public float projectileTrackWindow = 1.25f;
        public bool continuouslyConfigureRocks = true;

        Vector3 lastPlayerPosition;
        float stepTimer;
        bool wasGrounded;
        Coroutine trackProjectileRoutine;
        readonly HashSet<int> configuredProjectileIds = new HashSet<int>();
        string cachedRockPrefabName;

        void Start()
        {
            if (!ResolveReferences()) return;

            lastPlayerPosition = playerController.transform.position;
            wasGrounded = IsGrounded();
        }

        void Update()
        {
            if (!ResolveReferences()) return;

            UpdateMovementNoise();
            if (enableThrowIntegration)
                UpdateThrowNoise();

            if (enableThrowIntegration && autoConfigureThrownRockImpactNoise && continuouslyConfigureRocks)
                EnsureThrowableNoiseOnRocks();
        }

        bool ResolveReferences()
        {
            if (playerController == null && autoFindPlayer)
                playerController = FindFirstObjectByType<RohitFPSController>();

            if (playerController == null) return false;

            if (throwRock == null)
                throwRock = playerController.GetComponent<ThrowRock>();

            if (playerCharacterController == null)
                playerCharacterController = playerController.GetComponent<CharacterController>();

            if (throwRock != null && throwRock.rockPrefab != null)
                cachedRockPrefabName = NormalizeObjectName(throwRock.rockPrefab.name);

            return true;
        }

        bool IsGrounded()
        {
            return playerCharacterController != null &&
                   playerCharacterController.enabled &&
                   playerCharacterController.isGrounded;
        }

        void UpdateMovementNoise()
        {
            Vector3 current = playerController.transform.position;
            float dt = Mathf.Max(Time.deltaTime, 0.0001f);

            Vector3 horizontalDelta = current - lastPlayerPosition;
            horizontalDelta.y = 0f;
            float speed = horizontalDelta.magnitude / dt;

            bool grounded = IsGrounded();

            if (emitJumpLandNoise && wasGrounded && !grounded)
                EmitFromPlayer(jumpNoiseIntensity, jumpNoiseType);

            if (emitJumpLandNoise && !wasGrounded && grounded)
                EmitFromPlayer(landingNoiseIntensity, landingNoiseType);

            if (emitMovementNoise && grounded && speed >= minMoveSpeed)
            {
                bool sprinting = IsSprintHeld();
                stepTimer -= Time.deltaTime;

                if (stepTimer <= 0f)
                {
                    EmitFromPlayer(sprinting ? sprintNoiseIntensity : walkNoiseIntensity, footstepNoiseType);
                    stepTimer = sprinting ? sprintStepInterval : walkStepInterval;
                }
            }
            else
            {
                stepTimer = 0f;
            }

            wasGrounded = grounded;
            lastPlayerPosition = current;
        }

        void UpdateThrowNoise()
        {
            if (throwRock == null) return;
            if (!WasKeyPressed(throwKey)) return;

            Vector3 throwPos = throwRock.throwPoint != null
                ? throwRock.throwPoint.position
                : playerController.transform.position;

            if (emitThrowReleaseNoise)
                NoiseSystem.Emit(throwPos, throwReleaseNoiseIntensity, throwNoiseType);

            if (!autoConfigureThrownRockImpactNoise) return;

            if (trackProjectileRoutine != null)
                StopCoroutine(trackProjectileRoutine);

            trackProjectileRoutine = StartCoroutine(TrackAndConfigureThrownProjectile(throwPos, projectileTrackWindow));
        }

        IEnumerator TrackAndConfigureThrownProjectile(Vector3 throwPos, float windowSeconds)
        {
            float elapsed = 0f;
            string rockPrefabName = throwRock != null && throwRock.rockPrefab != null
                ? NormalizeObjectName(throwRock.rockPrefab.name)
                : null;

            while (elapsed < windowSeconds)
            {
                Rigidbody[] allRigidbodies = FindObjectsByType<Rigidbody>(FindObjectsSortMode.None);
                foreach (Rigidbody rb in allRigidbodies)
                {
                    if (rb == null) continue;
                    GameObject obj = rb.gameObject;
                    int id = obj.GetInstanceID();
                    if (configuredProjectileIds.Contains(id)) continue;

                    if (!LooksLikeThrownRock(obj, rockPrefabName, throwPos)) continue;

                    ThrowableNoise.ConfigureOnObject(
                        obj,
                        impactNoiseIntensity,
                        impactMinSpeed,
                        projectileLifeSeconds,
                        impactNoiseType);

                    configuredProjectileIds.Add(id);
                    yield break;
                }

                elapsed += Time.deltaTime;
                yield return null;
            }
        }

        void EnsureThrowableNoiseOnRocks()
        {
            Rigidbody[] allRigidbodies = FindObjectsByType<Rigidbody>(FindObjectsSortMode.None);
            for (int i = 0; i < allRigidbodies.Length; i++)
            {
                Rigidbody rb = allRigidbodies[i];
                if (rb == null) continue;
                GameObject obj = rb.gameObject;
                if (obj == null) continue;
                if (!LooksLikeRockCandidate(obj)) continue;

                int id = obj.GetInstanceID();
                if (configuredProjectileIds.Contains(id)) continue;

                ThrowableNoise.ConfigureOnObject(
                    obj,
                    impactNoiseIntensity,
                    impactMinSpeed,
                    projectileLifeSeconds,
                    impactNoiseType);

                configuredProjectileIds.Add(id);
            }
        }

        bool LooksLikeRockCandidate(GameObject candidate)
        {
            string candidateName = NormalizeObjectName(candidate.name);
            if (string.IsNullOrEmpty(candidateName)) return false;

            if (!string.IsNullOrEmpty(cachedRockPrefabName))
                return candidateName == cachedRockPrefabName;

            return candidateName.Contains("rock");
        }

        bool LooksLikeThrownRock(GameObject candidate, string rockPrefabName, Vector3 throwPos)
        {
            string candidateName = NormalizeObjectName(candidate.name);
            bool closeToThrow = Vector3.Distance(candidate.transform.position, throwPos) <= 2.5f;

            if (string.IsNullOrEmpty(rockPrefabName))
                return closeToThrow;

            // Prefer exact prefab-name match, but allow close-to-throw fallback for prefabs with odd naming/spacing.
            return candidateName == rockPrefabName || closeToThrow;
        }

        string NormalizeObjectName(string rawName)
        {
            if (string.IsNullOrEmpty(rawName)) return string.Empty;
            return rawName.Replace("(Clone)", "").Trim().ToLowerInvariant();
        }

        void EmitFromPlayer(float intensity, string noiseType)
        {
            if (playerController == null) return;
            if (intensity <= 0f) return;

            NoiseSystem.Emit(playerController.transform.position, intensity, noiseType);
        }

        bool IsSprintHeld()
        {
            bool held = false;
#if ENABLE_LEGACY_INPUT_MANAGER
            held |= Input.GetKey(KeyCode.LeftShift);
#endif
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null) held |= Keyboard.current.leftShiftKey.isPressed;
#endif
            return held;
        }

        bool WasKeyPressed(KeyCode key)
        {
            bool pressed = false;
#if ENABLE_LEGACY_INPUT_MANAGER
            pressed |= Input.GetKeyDown(key);
#endif
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
                switch (key)
                {
                    case KeyCode.G: pressed |= Keyboard.current.gKey.wasPressedThisFrame; break;
                    case KeyCode.N: pressed |= Keyboard.current.nKey.wasPressedThisFrame; break;
                    case KeyCode.E: pressed |= Keyboard.current.eKey.wasPressedThisFrame; break;
                    case KeyCode.P: pressed |= Keyboard.current.pKey.wasPressedThisFrame; break;
                    case KeyCode.Space: pressed |= Keyboard.current.spaceKey.wasPressedThisFrame; break;
                    case KeyCode.Return: pressed |= Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.numpadEnterKey.wasPressedThisFrame; break;
                    case KeyCode.Escape: pressed |= Keyboard.current.escapeKey.wasPressedThisFrame; break;
                    case KeyCode.R: pressed |= Keyboard.current.rKey.wasPressedThisFrame; break;
                }
            }
#endif
            return pressed;
        }
    }
}
