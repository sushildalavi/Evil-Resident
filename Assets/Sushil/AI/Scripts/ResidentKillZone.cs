using UnityEngine;
using UnityEngine.SceneManagement;
using Sushil.Systems;

namespace Sushil.AI
{
    public class ResidentKillZone : MonoBehaviour
    {
        const float MaxRuntimeWorldKillRadius = 0.96f;

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
                Vector3 targetContact = GetTargetContactPoint(other, death.transform, closestPoint);
                if (!IsWithinKillVerticalTolerance(center, targetContact))
                    return;
                if (residentAI != null && residentAI.IsSquareFuseKillBlocked(center, targetContact))
                    return;
                if (residentAI != null && !residentAI.IsKillContactClear(center, targetContact))
                    return;
                if (residentAI != null && !residentAI.IsCloseKillReachable(targetContact, Vector3.Distance(center, targetContact)))
                    return;

                if (residentAI != null)
                {
                    if (!residentAI.TryKillTarget(death.gameObject, "Resident one-shot (trigger)"))
                        return;
                }
                else
                {
                    death.Kill("Resident one-shot (trigger)");
                }

                killed = true;
                return;
            }

            var rohit = other.GetComponentInParent<RohitFPSController>();
            if (rohit == null) return;
            if (rohit.isHidden) return;
            Vector3 rohitContact = GetTargetContactPoint(other, rohit.transform, closestPoint);
            if (!IsWithinKillVerticalTolerance(center, rohitContact))
                return;
            if (residentAI != null && residentAI.IsSquareFuseKillBlocked(center, rohitContact))
                return;
            if (residentAI != null && !residentAI.IsKillContactClear(center, rohitContact))
                return;
            if (residentAI != null && !residentAI.IsCloseKillReachable(rohitContact, Vector3.Distance(center, rohitContact)))
                return;

            if (residentAI != null)
            {
                if (!residentAI.TryKillTarget(rohit.gameObject, "Resident one-shot (trigger)"))
                    return;
            }
            else
            {
                ResidentAI.KillRohitController(rohit, "Resident one-shot (trigger)");
            }

            killed = true;
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
            float maxKillRadius = IsSahilTestNewLevel() ? 0.88f : MaxRuntimeWorldKillRadius;
            return Mathf.Min(colliderWorldRadius, maxKillRadius);
        }

        Vector3 GetTargetContactPoint(Collider hitCollider, Transform targetRoot, Vector3 fallbackPoint)
        {
            if (hitCollider != null && hitCollider.enabled)
                return hitCollider.ClosestPoint(GetKillCenter());

            if (targetRoot == null)
                return fallbackPoint;

            var controller = targetRoot.GetComponent<CharacterController>();
            if (controller != null)
                return controller.bounds.ClosestPoint(GetKillCenter());

            var bodyCollider = targetRoot.GetComponent<Collider>();
            if (bodyCollider != null && bodyCollider.enabled)
                return bodyCollider.ClosestPoint(GetKillCenter());

            return fallbackPoint;
        }

        bool IsSahilTestNewLevel()
        {
            return SceneManager.GetActiveScene().path == "Assets/Sahil/Test/NewLevel.unity";
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
