using UnityEngine;

namespace AyutthayaSimulator
{
    public sealed class MissionReward : MonoBehaviour
    {
        public string MissionId;
        public string RewardNote = "No item reward in the demo. Completion is recorded in the save file.";
        public bool Granted;

        public void Grant()
        {
            Granted = true;
        }
    }
}
