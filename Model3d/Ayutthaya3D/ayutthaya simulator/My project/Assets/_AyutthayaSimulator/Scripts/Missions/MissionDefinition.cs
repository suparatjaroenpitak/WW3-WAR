using AyutthayaSimulator.Core;
using UnityEngine;

namespace AyutthayaSimulator
{
    [CreateAssetMenu(fileName = "Mission", menuName = "Ayutthaya Simulator/Mission")]
    public sealed class MissionDefinition : ScriptableObject
    {
        public string MissionId = "mission";
        public string TitleThai = "";
        public string TitleEnglish = "";
        public MissionKind Kind;
        public string TargetId = "";
        public float TargetX;
        public float TargetZ;
        public float Radius = 6f;
    }
}
