using UnityEngine;

namespace AyutthayaSimulator
{
    public sealed class BuildingMetadata : MonoBehaviour
    {
        public string OsmId = "";
        public string BuildingName = "";
        public string BuildingType = "";
        public string HistoricTag = "";
        public float HeightMeters;
        public string HeightSource = "";
        public string Confidence = "";
        public bool DoorPlacementIsProcedural = true;
        public string SourceLicense = "ODbL-1.0";
    }
}
