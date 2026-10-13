using UnityEngine;

namespace AyutthayaSimulator
{
    public sealed class WorldMetadata : MonoBehaviour
    {
        public string TileId = "ayutthaya_demo_500m";
        public float TileSize = 500f;
        public string TerrainStatus = "PLACEHOLDER_FLAT_NO_DEM";
        public string Attribution = "© OpenStreetMap contributors";
        public string LicenseName = "ODbL-1.0";
        public string OriginName = "";
        public double OriginLatitude;
        public double OriginLongitude;
        public int BuildingCount;
        public int RoadCount;
        public int WaterCount;
        public int EstimatedHeightCount;
        public int MeasuredHeightCount;
    }
}
