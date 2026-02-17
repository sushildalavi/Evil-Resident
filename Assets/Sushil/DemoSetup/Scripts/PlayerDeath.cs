using UnityEngine;

namespace Sushil.Systems
{
    public class PlayerDeath : MonoBehaviour
    {
        public bool isDead { get; private set; }

        public void Kill(string reason = "Killed")
        {
            if (isDead) return;
            isDead = true;

            Debug.Log($"[PlayerDeath] {reason}");
            gameObject.SetActive(false); // Alpha: disable player
        }
    }
}