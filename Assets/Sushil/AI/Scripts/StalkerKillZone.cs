using UnityEngine;
using Sushil.Systems;

namespace Sushil.AI
{
    public class StalkerKillZone : MonoBehaviour
    {
        bool killed;

        private void OnTriggerEnter(Collider other)
        {
            if (killed) return;
            if (!other.CompareTag("Player")) return;

            killed = true;

            var death = other.GetComponent<PlayerDeath>();
            if (death != null) death.Kill("Stalker one-shot (trigger)");
            else other.gameObject.SetActive(false);
        }
    }
}