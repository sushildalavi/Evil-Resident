using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using Sushil.Systems;

namespace Sushil.AI
{
    public class ResidentKillZone : MonoBehaviour
    {
        const float MaxRuntimeWorldKillRadius = 0.9f;
        const string TriggerKillReason = "Resident one-shot (trigger)";

        [Header("Safety")]
        public float armDelay = 0.75f;
        public bool onlyKillDuringChase = true;

        bool killed;
        float armedAt;
        ResidentAI residentAI;
        SphereCollider killTrigger;
        readonly Collider[] overlapHits = new Collider[16];

        void Awake()
        {
            residentAI = GetComponentInParent<ResidentAI>();
            killTrigger = GetComponent<SphereCollider>();
            armedAt = Time.time + armDelay;
        }

        void Update()
        {
            if (killed) return;
            if (Time.time < armedAt) return;
            if (killTrigger == null || !killTrigger.enabled) return;
            
            Vector3 center = GetKillCenter();
            float radius = GetEffectiveWorldKillRadius();

            int count = Physics.OverlapSphereNonAlloc(
                center,
                radius,
                overlapHits,
                ~0,
                QueryTriggerInteraction.Collide);

            for (int i = 0; i < count; i++)
            {
                TryKill(overlapHits[i]);
                if (killed) return;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            TryKill(other);
        }

        private void OnTriggerStay(Collider other)
        {
            TryKill(other);
        }

        void TryKill(Collider other)
        {
            if (killed) return;
            if (Time.time < armedAt) return;
            if (other == null) return;
            if (other.transform == transform || other.transform.IsChildOf(transform)) return;
            if (killTrigger == null || !killTrigger.enabled) return;

            Vector3 center = GetKillCenter();
            float radius = GetEffectiveWorldKillRadius();
            Vector3 closestPoint = other.ClosestPoint(center);
            if ((closestPoint - center).sqrMagnitude > radius * radius)
                return;

            if (onlyKillDuringChase && residentAI != null && residentAI.state != ResidentAI.State.Chase)
                return;

            var death = other.GetComponentInParent<PlayerDeath>();
            if (death != null)
            {
                if (death.isDead) return;

                var hide = death.GetComponent<PlayerHide>();
                if (hide != null && hide.IsHidden) return;
                TryKillCharacter(other, death.transform, death.gameObject, closestPoint, center, () => death.Kill(TriggerKillReason));
                return;
            }

            var rohit = other.GetComponentInParent<RohitFPSController>();
            if (rohit == null || rohit.isHidden) return;

            TryKillCharacter(other, rohit.transform, rohit.gameObject, closestPoint, center, () => ResidentAI.KillRohitController(rohit, TriggerKillReason));
        }

        void TryKillCharacter(Collider hitCollider, Transform targetRoot, GameObject targetObject, Vector3 fallbackPoint, Vector3 killCenter, Action fallbackKill)
        {
            Vector3 targetContact = GetTargetContactPoint(hitCollider, targetRoot, fallbackPoint, killCenter);
            if (!CanKillTargetAtContact(killCenter, targetContact))
                return;

            if (residentAI != null)
                residentAI.NotifyImmediateResidentThreat(seen: true);

            if (!TryExecuteKill(targetObject, fallbackKill))
                return;

            killed = true;
        }

        bool CanKillTargetAtContact(Vector3 killCenter, Vector3 targetContact)
        {
            if (!IsWithinKillVerticalTolerance(killCenter, targetContact))
                return false;

            if (residentAI == null)
                return Vector3.Distance(killCenter, targetContact) <= MaxRuntimeWorldKillRadius;

            float contactDistance = Vector3.Distance(killCenter, targetContact);
            float allowedDistance = Mathf.Min(
                GetEffectiveWorldKillRadius(),
                Mathf.Max(0.35f, residentAI.killDistance + 0.02f));
            if (contactDistance > allowedDistance)
                return false;

            if (residentAI.IsSquareFuseKillBlocked(killCenter, targetContact))
                return false;
            if (!residentAI.IsKillContactClear(killCenter, targetContact))
                return false;
            if (!residentAI.IsKillContactPathStrictlyClear(targetContact))
                return false;

            return residentAI.IsCloseKillReachable(targetContact, contactDistance);
        }

        bool TryExecuteKill(GameObject targetObject, Action fallbackKill)
        {
            if (residentAI != null)
                return residentAI.TryKillTarget(targetObject, TriggerKillReason);

            fallbackKill?.Invoke();
            return true;
        }

        Vector3 GetKillCenter()
        {
            return transform.TransformPoint(killTrigger.center);
        }

        float GetEffectiveWorldKillRadius()
        {
            float maxScale = Mathf.Max(
                Mathf.Abs(transform.lossyScale.x),
                Mathf.Abs(transform.lossyScale.y),
                Mathf.Abs(transform.lossyScale.z));

            float colliderWorldRadius = killTrigger.radius * Mathf.Max(0.01f, maxScale);
            float maxKillRadius = IsSahilTestNewLevel() ? 0.84f : MaxRuntimeWorldKillRadius;
            return Mathf.Min(colliderWorldRadius, maxKillRadius);
        }

        Vector3 GetTargetContactPoint(Collider hitCollider, Transform targetRoot, Vector3 fallbackPoint, Vector3 killCenter)
        {
            if (hitCollider != null && hitCollider.enabled)
                return hitCollider.ClosestPoint(killCenter);

            if (targetRoot == null)
                return fallbackPoint;

            var controller = targetRoot.GetComponent<CharacterController>();
            if (controller != null)
                return controller.bounds.ClosestPoint(killCenter);

            var bodyCollider = targetRoot.GetComponent<Collider>();
            if (bodyCollider != null && bodyCollider.enabled)
                return bodyCollider.ClosestPoint(killCenter);

            return fallbackPoint;
        }

        bool IsSahilTestNewLevel()
        {
            string path = SceneManager.GetActiveScene().path;
            return path == "Assets/Sahil/Test/NewLevel.unity" ||
                   path == "Assets/Sahil/Test/NewNewLevel.unity";
        }

        bool IsWithinKillVerticalTolerance(Vector3 center, Vector3 closestPoint)
        {
            float allowedGap = 1.35f;
            if (residentAI != null)
                allowedGap = Mathf.Max(0.35f, residentAI.killVerticalTolerance);

            return Mathf.Abs(center.y - closestPoint.y) <= allowedGap;
        }
    }
}
