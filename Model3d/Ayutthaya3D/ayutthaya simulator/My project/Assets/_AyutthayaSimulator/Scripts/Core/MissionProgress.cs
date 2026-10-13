using System;
using System.Collections.Generic;

namespace AyutthayaSimulator.Core
{
    public enum MissionKind
    {
        ReachPoint,
        TalkToNpc,
        EnterVehicle,
        InteractWithId
    }

    public sealed class MissionRecord
    {
        public string Id = "";
        public MissionKind Kind;
        public string TargetId = "";
        public float TargetX;
        public float TargetZ;
        public float Radius = 4f;
        public bool Completed;
    }

    public static class MissionProgress
    {
        public static bool TryComplete(MissionRecord mission, string eventId, float x, float z, bool enteredVehicle)
        {
            if (mission == null) throw new ArgumentNullException(nameof(mission));
            if (mission.Completed) return false;
            var done = false;
            switch (mission.Kind)
            {
                case MissionKind.ReachPoint:
                    var dx = x - mission.TargetX;
                    var dz = z - mission.TargetZ;
                    done = (dx * dx) + (dz * dz) <= mission.Radius * mission.Radius;
                    break;
                case MissionKind.TalkToNpc:
                case MissionKind.InteractWithId:
                    done = !string.IsNullOrEmpty(eventId) && eventId == mission.TargetId;
                    break;
                case MissionKind.EnterVehicle:
                    done = enteredVehicle && (string.IsNullOrEmpty(mission.TargetId) || mission.TargetId == eventId);
                    break;
            }
            if (done) mission.Completed = true;
            return done;
        }

        public static string[] CompletedIds(IReadOnlyList<MissionRecord> missions)
        {
            var ids = new List<string>();
            if (missions == null) return ids.ToArray();
            for (var i = 0; i < missions.Count; i++)
            {
                if (missions[i] != null && missions[i].Completed && !string.IsNullOrEmpty(missions[i].Id))
                    ids.Add(missions[i].Id);
            }
            return ids.ToArray();
        }

        public static void Restore(IReadOnlyList<MissionRecord> missions, string[] completedIds)
        {
            if (missions == null) return;
            var done = new HashSet<string>(completedIds ?? Array.Empty<string>());
            for (var i = 0; i < missions.Count; i++)
            {
                if (missions[i] == null) continue;
                missions[i].Completed = done.Contains(missions[i].Id);
            }
        }
    }
}
