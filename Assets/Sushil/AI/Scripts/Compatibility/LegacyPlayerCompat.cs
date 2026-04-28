using UnityEngine;
using Sushil.AI;

namespace Sushil.Systems
{
    public class PlayerHide : MonoBehaviour
    {
        [SerializeField] bool isHidden;

        public bool IsHidden => isHidden;

        public void SetHidden(bool hidden)
        {
            isHidden = hidden;
        }
    }

    public class PlayerDeath : MonoBehaviour
    {
        public bool isDead;
        public string lastReason = string.Empty;

        public void Kill(string reason = "Resident one-shot")
        {
            if (isDead) return;

            isDead = true;
            lastReason = reason ?? string.Empty;

            var rohit = GetComponent<RohitFPSController>();
            if (rohit != null)
            {
                ResidentAI.KillRohitController(rohit, reason);
                return;
            }

            var controller = GetComponent<CharacterController>();
            if (controller != null) controller.enabled = false;

            var rigidbody = GetComponent<Rigidbody>();
            if (rigidbody != null)
            {
                rigidbody.linearVelocity = Vector3.zero;
                rigidbody.angularVelocity = Vector3.zero;
            }

            var behaviours = GetComponents<MonoBehaviour>();
            for (int i = 0; i < behaviours.Length; i++)
            {
                var behaviour = behaviours[i];
                if (behaviour == null || behaviour == this || behaviour is PlayerHide) continue;
                behaviour.enabled = false;
            }

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            GameOverOverlay.Show(reason);
        }
    }
}
