using UnityEngine;
using Sushil.Systems;

namespace Sushil.AI
{
    public class StalkerKillZone : MonoBehaviour
    {
        const float MaxRuntimeWorldKillRadius = 1.15f;

        [Header("Safety")]
        public float armDelay = 0.75f;
        public bool onlyKillDuringChase = true;

        bool killed;
        float armedAt;
        StalkerAI stalkerAI;
        SphereCollider killTrigger;
        readonly Collider[] overlapHits = new Collider[16];

        void Awake()
        {
            stalkerAI = GetComponentInParent<StalkerAI>();
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

            if (onlyKillDuringChase && stalkerAI != null && stalkerAI.state != StalkerAI.State.Chase)
                return;

            var death = other.GetComponentInParent<PlayerDeath>();
            if (death != null)
            {
                if (death.isDead) return;

                var hide = death.GetComponent<PlayerHide>();
                if (hide != null && hide.IsHidden) return;

                killed = true;
                death.Kill("Stalker one-shot (trigger)");
                return;
            }

            var rohit = other.GetComponentInParent<RohitFPSController>();
            if (rohit == null) return;
            if (rohit.isHidden) return;

            killed = true;
            StalkerAI.KillRohitController(rohit, "Stalker one-shot (trigger)");
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
            return Mathf.Min(colliderWorldRadius, MaxRuntimeWorldKillRadius);
        }
    }
}
