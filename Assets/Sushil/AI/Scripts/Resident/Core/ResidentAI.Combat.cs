using System.Collections;
using UnityEngine;
using Sushil.Systems;

namespace Sushil.AI
{
    public partial class ResidentAI
    {
        public bool TryKillTarget(GameObject target, string reason)
        {
            if (target == null || killTriggered) return false;

            var death = target.GetComponentInParent<PlayerDeath>();
            var rohit = target.GetComponentInParent<RohitFPSController>();
            Transform targetTransform = target.transform;
            bool isPlayerTarget = death != null ||
                                  rohit != null ||
                                  (player != null &&
                                   targetTransform != null &&
                                   (targetTransform == player || targetTransform.IsChildOf(player)));
            Vector3 targetWorldPos = targetTransform != null ? targetTransform.position : transform.position;
            if (isPlayerTarget &&
                IsSquareFuseKillBlocked(transform.position, targetWorldPos))
                return false;

            if (death != null && !death.isDead)
                return StartKillAttack(targetTransform, death, null, reason);

            if (rohit != null)
                return StartKillAttack(targetTransform, null, rohit, reason);

            return false;
        }

        // Cached scream clip — same WA_Scream sound the Weeping Angel uses, copied to
        // Resources/Audio/KillScream.wav so it can be loaded by either AI without
        // duplicating the asset reference per-prefab.
        static AudioClip cachedKillScreamClip;
        static bool killScreamLoadAttempted;

        bool StartKillAttack(Transform targetTransform, PlayerDeath death, RohitFPSController rohit, string reason)
        {
            killTriggered = true;
            PlayKillScream();
            if (killAttackRoutine != null) StopCoroutine(killAttackRoutine);
            killAttackRoutine = StartCoroutine(PerformKillAttack(targetTransform, death, rohit, reason));
            return true;
        }

        void PlayKillScream()
        {
            if (!killScreamLoadAttempted)
            {
                cachedKillScreamClip = Resources.Load<AudioClip>("Audio/KillScream");
                killScreamLoadAttempted = true;
            }
            if (cachedKillScreamClip == null) return;

            // 2D playback (no spatial blend) so it always reads at full volume,
            // ignores AudioListener pause, and doesn't get muted while Time.timeScale = 0.
            var oneShot = new GameObject("ResidentKillScream", typeof(AudioSource));
            DontDestroyOnLoad(oneShot);
            var src = oneShot.GetComponent<AudioSource>();
            src.clip = cachedKillScreamClip;
            src.spatialBlend = 0f;
            src.volume = 1f;
            src.pitch = 1f;
            src.ignoreListenerPause = true;
            src.Play();
            Destroy(oneShot, cachedKillScreamClip.length + 0.25f);
        }

        IEnumerator PerformKillAttack(Transform targetTransform, PlayerDeath death, RohitFPSController rohit, string reason)
        {
            killAttackActive = true;
            killAttackStartedAt = Time.time;
            float windup = Mathf.Max(0.02f, killAttackWindupSeconds);
            float swing = Mathf.Max(0.04f, killAttackSwingSeconds);
            float recover = Mathf.Max(0.05f, killAttackRecoverySeconds);
            killAttackImpactAt = killAttackStartedAt + windup;
            killAttackHitAt = killAttackImpactAt + (swing * 0.9f);
            killAttackRecoverAt = killAttackImpactAt + swing;
            killAttackEndAt = killAttackRecoverAt + recover;
            killAttackFocusPoint = targetTransform != null ? targetTransform.position : transform.position + transform.forward;

            if (IsAgentReady())
            {
                SafeSetStopped(true);
                SafeResetPath();
            }

            yield return AdvanceKillAttackUntil(targetTransform, killAttackImpactAt);
            yield return AdvanceKillAttackUntil(targetTransform, killAttackHitAt);

            if (death != null && !death.isDead)
                death.Kill(reason);
            else if (rohit != null)
                KillRohitController(rohit, reason);

            yield return AdvanceKillAttackUntil(targetTransform, killAttackEndAt);

            killAttackActive = false;
            killAttackRoutine = null;
        }

        IEnumerator AdvanceKillAttackUntil(Transform targetTransform, float endTime)
        {
            while (Time.time < endTime)
            {
                if (targetTransform != null)
                    killAttackFocusPoint = targetTransform.position;
                UpdateKillAttackFacing();
                yield return null;
            }
        }

        public static void KillRohitController(RohitFPSController rohit, string reason)
        {
            if (rohit == null) return;
            int rohitId = rohit.GetInstanceID();
            if (killedRohitInstanceIds.Contains(rohitId)) return;

            Debug.Log($"[ResidentAI] {reason} (Rohit fallback)");
            killedRohitInstanceIds.Add(rohitId);

            rohit.enabled = false;
            rohit.CancelInvoke();

            var interactionUi = rohit.GetComponent<InteractionUI>();
            if (interactionUi != null) interactionUi.enabled = false;

            var torch = rohit.GetComponent<PlayerTorch>();
            if (torch != null) torch.enabled = false;

            var cc = rohit.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;

            var rb = rohit.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            GameOverOverlay.Show(reason);
        }

        void UpdateKillAttackAnimation()
        {
            float now = Time.time;
            float windupT = killAttackImpactAt > killAttackStartedAt
                ? Mathf.InverseLerp(killAttackStartedAt, killAttackImpactAt, now)
                : 1f;
            float swingT = killAttackRecoverAt > killAttackImpactAt
                ? Mathf.InverseLerp(killAttackImpactAt, killAttackRecoverAt, now)
                : 1f;
            float recoverT = killAttackEndAt > killAttackRecoverAt
                ? Mathf.InverseLerp(killAttackRecoverAt, killAttackEndAt, now)
                : 1f;

            bool inWindup = now < killAttackImpactAt;
            bool inSwing = now >= killAttackImpactAt && now < killAttackRecoverAt;
            float lunge01;
            float lean01;

            if (inWindup)
            {
                windupT = Mathf.SmoothStep(0f, 1f, windupT);
                lunge01 = windupT * 0.3f;
                lean01 = windupT * 0.35f;
                visualRoot.localPosition = visualBaseLocalPos + new Vector3(0f, 0f, killAttackLungeDistance * lunge01);
                visualRoot.localRotation = visualBaseLocalRot * Quaternion.Euler(-killAttackLeanDegrees * lean01, 0f, 0f);

                if (armR != null)
                {
                    armR.localPosition = armRBaseLocalPos + new Vector3(0.06f * windupT, 0.08f * windupT, -0.08f * windupT);
                    armR.localRotation = armRBaseRot * Quaternion.Euler(
                        killAttackRightArmBackDegrees * windupT,
                        -18f * windupT,
                        58f * windupT);
                }
                if (armL != null)
                {
                    armL.localPosition = armLBaseLocalPos + new Vector3(-0.04f * windupT, 0.03f * windupT, 0.02f * windupT);
                    armL.localRotation = armLBaseRot * Quaternion.Euler(
                        killAttackLeftArmBraceDegrees * 0.4f * windupT,
                        10f * windupT,
                        -14f * windupT);
                }
                if (legL != null)
                {
                    legL.localPosition = legLBaseLocalPos;
                    legL.localRotation = legLBaseRot * Quaternion.Euler(-10f * windupT, 0f, 0f);
                }
                if (legR != null)
                {
                    legR.localPosition = legRBaseLocalPos;
                    legR.localRotation = legRBaseRot * Quaternion.Euler(10f * windupT, 0f, 0f);
                }
                // WINDUP claws: spread wide — full horror display before the strike
                AnimateKillClaws_Windup(windupT);
                return;
            }

            if (inSwing)
            {
                swingT = Mathf.SmoothStep(0f, 1f, swingT);
                lunge01 = Mathf.Lerp(0.3f, 1f, swingT);
                lean01 = Mathf.Lerp(0.35f, 1f, swingT);
                float impactShake = Mathf.Sin(swingT * Mathf.PI) * 0.02f;
                visualRoot.localPosition = visualBaseLocalPos + new Vector3(0f, impactShake, killAttackLungeDistance * lunge01);
                visualRoot.localRotation = visualBaseLocalRot * Quaternion.Euler(
                    -killAttackLeanDegrees * lean01,
                    0f,
                    -6f * swingT);

                if (armR != null)
                {
                    armR.localPosition = armRBaseLocalPos + new Vector3(
                        Mathf.Lerp(0.06f, 0.14f, swingT),
                        Mathf.Lerp(0.08f, -0.02f, swingT),
                        Mathf.Lerp(-0.08f, 0.26f, swingT));
                    armR.localRotation = armRBaseRot * Quaternion.Euler(
                        Mathf.Lerp(killAttackRightArmBackDegrees, -killAttackRightArmStrikeDegrees, swingT),
                        Mathf.Lerp(-18f, 12f, swingT),
                        Mathf.Lerp(58f, -18f, swingT));
                }
                if (armL != null)
                {
                    armL.localPosition = armLBaseLocalPos + new Vector3(
                        Mathf.Lerp(-0.04f, -0.1f, swingT),
                        Mathf.Lerp(0.03f, 0.08f, swingT),
                        Mathf.Lerp(0.02f, 0.12f, swingT));
                    armL.localRotation = armLBaseRot * Quaternion.Euler(
                        Mathf.Lerp(killAttackLeftArmBraceDegrees * 0.4f, killAttackLeftArmBraceDegrees, swingT),
                        Mathf.Lerp(10f, 18f, swingT),
                        Mathf.Lerp(-14f, -24f, swingT));
                }
                if (legL != null)
                {
                    legL.localPosition = legLBaseLocalPos + new Vector3(0f, -0.02f * swingT, 0f);
                    legL.localRotation = legLBaseRot * Quaternion.Euler(Mathf.Lerp(-10f, -18f, swingT), 0f, 0f);
                }
                if (legR != null)
                {
                    legR.localPosition = legRBaseLocalPos + new Vector3(0f, 0.01f * swingT, 0f);
                    legR.localRotation = legRBaseRot * Quaternion.Euler(Mathf.Lerp(10f, 18f, swingT), 0f, 0f);
                }
                // SWING claws: slam forward and close — predatory grip
                AnimateKillClaws_Swing(swingT);
                return;
            }

            recoverT = Mathf.SmoothStep(0f, 1f, recoverT);
            lunge01 = 1f - recoverT;
            lean01 = 1f - recoverT;
            visualRoot.localPosition = visualBaseLocalPos + new Vector3(0f, 0f, killAttackLungeDistance * lunge01);
            visualRoot.localRotation = visualBaseLocalRot * Quaternion.Euler(
                -killAttackLeanDegrees * lean01,
                0f,
                -6f * lunge01);

            if (armR != null)
            {
                armR.localPosition = Vector3.Lerp(
                    armRBaseLocalPos + new Vector3(0.14f, -0.02f, 0.26f),
                    armRBaseLocalPos,
                    recoverT);
                armR.localRotation = armRBaseRot * Quaternion.Euler(
                    Mathf.Lerp(-killAttackRightArmStrikeDegrees, 0f, recoverT),
                    Mathf.Lerp(12f, 0f, recoverT),
                    Mathf.Lerp(-18f, 0f, recoverT));
            }
            if (armL != null)
            {
                armL.localPosition = Vector3.Lerp(
                    armLBaseLocalPos + new Vector3(-0.1f, 0.08f, 0.12f),
                    armLBaseLocalPos,
                    recoverT);
                armL.localRotation = armLBaseRot * Quaternion.Euler(
                    Mathf.Lerp(killAttackLeftArmBraceDegrees, 0f, recoverT),
                    Mathf.Lerp(18f, 0f, recoverT),
                    Mathf.Lerp(-24f, 0f, recoverT));
            }
            if (legL != null)
            {
                legL.localPosition = Vector3.Lerp(legLBaseLocalPos + new Vector3(0f, -0.02f, 0f), legLBaseLocalPos, recoverT);
                legL.localRotation = legLBaseRot * Quaternion.Euler(Mathf.Lerp(-18f, 0f, recoverT), 0f, 0f);
            }
            if (legR != null)
            {
                legR.localPosition = Vector3.Lerp(legRBaseLocalPos + new Vector3(0f, 0.01f, 0f), legRBaseLocalPos, recoverT);
                legR.localRotation = legRBaseRot * Quaternion.Euler(Mathf.Lerp(18f, 0f, recoverT), 0f, 0f);
            }
            // RECOVER claws: return to resting pose
            AnimateKillClaws_Recover(recoverT);
        }

        // ── kill-attack claw helpers ──────────────────────────────────────────

        void AnimateKillClaws_Windup(float t)
        {
            if (clawShaftsL == null) return;
            for (int i = 0; i < 4; i++)
            {
                float outer = (i == 0 || i == 3) ? 1f : 0.65f;
                float spreadX =  22f * t * outer;   // tilt forward
                float spreadZ =  12f * t * outer;   // fan outward
                if (clawShaftsL[i] != null)
                    clawShaftsL[i].localRotation = clawBasesL[i] * Quaternion.Euler(spreadX, 0f,  spreadZ);
                if (clawShaftsR[i] != null)
                    clawShaftsR[i].localRotation = clawBasesR[i] * Quaternion.Euler(spreadX, 0f, -spreadZ);
            }
        }

        void AnimateKillClaws_Swing(float t)
        {
            if (clawShaftsL == null) return;
            for (int i = 0; i < 4; i++)
            {
                float strikeX = Mathf.Lerp(22f, 40f, t);   // slam further forward
                float strikeZ = Mathf.Lerp(12f, -6f, t);   // claws close inward
                if (clawShaftsL[i] != null)
                    clawShaftsL[i].localRotation = clawBasesL[i] * Quaternion.Euler(strikeX, 0f,  strikeZ);
                if (clawShaftsR[i] != null)
                    clawShaftsR[i].localRotation = clawBasesR[i] * Quaternion.Euler(strikeX, 0f, -strikeZ);
            }
        }

        void AnimateKillClaws_Recover(float t)
        {
            if (clawShaftsL == null) return;
            for (int i = 0; i < 4; i++)
            {
                float strikeX = 40f;
                float strikeZ = -6f;
                if (clawShaftsL[i] != null)
                    clawShaftsL[i].localRotation = Quaternion.Slerp(
                        clawBasesL[i] * Quaternion.Euler(strikeX, 0f,  strikeZ), clawBasesL[i], t);
                if (clawShaftsR[i] != null)
                    clawShaftsR[i].localRotation = Quaternion.Slerp(
                        clawBasesR[i] * Quaternion.Euler(strikeX, 0f, -strikeZ), clawBasesR[i], t);
            }
        }

        void UpdateKillAttackFacing()
        {
            Vector3 toFocus = killAttackFocusPoint - transform.position;
            toFocus.y = 0f;
            if (toFocus.sqrMagnitude < 0.0001f)
                return;

            Quaternion targetRotation = Quaternion.LookRotation(toFocus.normalized, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 16f);
        }
    }
}
