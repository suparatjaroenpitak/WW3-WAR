using System.Collections.Generic;
using AyutthayaSimulator.Core;
using UnityEngine;

namespace AyutthayaSimulator
{
    public sealed class MissionManager : MonoBehaviour
    {
        public readonly List<MissionRecord> Records = new List<MissionRecord>();
        public string LastCompletedId = "";

        public void Add(MissionRecord record)
        {
            if (record != null) Records.Add(record);
        }

        public MissionRecord Active()
        {
            for (var i = 0; i < Records.Count; i++)
            {
                if (!Records[i].Completed) return Records[i];
            }
            return null;
        }

        public void NotifyPosition(Vector3 position, bool enteredVehicle, string eventId)
        {
            for (var i = 0; i < Records.Count; i++)
            {
                if (MissionProgress.TryComplete(Records[i], eventId, position.x, position.z, enteredVehicle))
                    LastCompletedId = Records[i].Id;
            }
        }

        public void NotifyReached(string missionId, Vector3 position)
        {
            NotifyPosition(position, false, missionId);
        }

        public string[] CompletedIds() => MissionProgress.CompletedIds(Records);

        public void Restore(string[] ids) => MissionProgress.Restore(Records, ids);
    }
}
