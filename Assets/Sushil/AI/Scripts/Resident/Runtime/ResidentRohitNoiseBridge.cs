using UnityEngine;

namespace Sushil.AI
{
    public class ResidentRohitNoiseBridge : MonoBehaviour
    {
        public RohitFPSController playerController;
        public bool autoFindPlayer = true;

        void Awake()
        {
            if (playerController == null && autoFindPlayer)
                playerController = FindFirstObjectByType<RohitFPSController>();
        }
    }
}
