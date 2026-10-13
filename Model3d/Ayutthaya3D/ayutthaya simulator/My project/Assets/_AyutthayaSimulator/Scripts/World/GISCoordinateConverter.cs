using AyutthayaSimulator.Core;
using UnityEngine;

namespace AyutthayaSimulator
{
    public sealed class GISCoordinateConverter : MonoBehaviour
    {
        public double OriginLatitude = 14.3559133;
        public double OriginLongitude = 100.5585079;
        public int Zone = GisCoordinateConverter.AyutthayaZone;

        public Vector3 ToLocal(double latitude, double longitude)
        {
            var origin = GisCoordinateConverter.Wgs84ToUtm(OriginLatitude, OriginLongitude, Zone);
            var local = GisCoordinateConverter.ToLocal(latitude, longitude, origin, Zone);
            return new Vector3((float)local.X, 0f, (float)local.Z);
        }
    }
}
