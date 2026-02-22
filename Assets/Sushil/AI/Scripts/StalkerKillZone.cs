using UnityEngine;
using Sushil.Systems;

namespace Sushil.AI
{
    public class StalkerKillZone : MonoBehaviour
    {
        [Header("Safety")]
        public float armDelay = 0.75f;
        public bool onlyKillDuringChase = true;

        bool killed;
        float armedAt;
        StalkerAI stalkerAI;

        void Awake()
        {
            stalkerAI = GetComponentInParent<StalkerAI>();
            armedAt = Time.time + armDelay;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (killed) return;
            if (Time.time < armedAt) return;

            var death = other.GetComponentInParent<PlayerDeath>();
            if (death == null) return;
            if (death.isDead) return;

            if (onlyKillDuringChase && stalkerAI != null && stalkerAI.state != StalkerAI.State.Chase)
                return;

            var hide = death.GetComponent<PlayerHide>();
            if (hide != null && hide.IsHidden) return;

            killed = true;
            death.Kill("Stalker one-shot (trigger)");
        }
    }
}
