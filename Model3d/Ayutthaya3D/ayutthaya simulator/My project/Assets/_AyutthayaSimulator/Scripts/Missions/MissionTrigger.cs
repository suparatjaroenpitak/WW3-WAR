using UnityEngine;

namespace AyutthayaSimulator
{
    public sealed class MissionTrigger : MonoBehaviour
    {
        public string MissionId;
        public MissionManager Manager;

        void OnTriggerEnter(Collider other)
        {
            if (!other.GetComponentInParent<PlayerController>()) return;
            if (Manager == null) Manager = FindFirstObjectByType<MissionManager>();
            if (Manager != null) Manager.NotifyReached(MissionId, other.transform.position);
        }
    }
}
