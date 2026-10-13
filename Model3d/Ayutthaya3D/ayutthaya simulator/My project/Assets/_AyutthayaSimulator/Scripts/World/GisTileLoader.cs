using System;
using System.IO;
using UnityEngine;

namespace AyutthayaSimulator
{
    [Serializable]
    public sealed class UnityGisBuilding
    {
        public string id;
        public string name;
        public string buildingType;
        public string historic;
        public float heightMeters;
        public string heightSource;
        public string confidence;
        public float[] footprint;
    }

    [Serializable]
    public sealed class UnityGisRoad
    {
        public string id;
        public string name;
        public string highway;
        public float widthMeters;
        public string widthSource;
        public int bridge;
        public float[] points;
    }

    [Serializable]
    public sealed class UnityGisWater
    {
        public string id;
        public string name;
        public string kind;
        public string shape;
        public float widthMeters;
        public string widthSource;
        public float[] points;
    }

    [Serializable]
    public sealed class UnityGisTile
    {
        public int schemaVersion;
        public string tileId;
        public string terrainStatus;
        public string originName;
        public float originLatitude;
        public float originLongitude;
        public float originEasting;
        public float originNorthing;
        public string attribution;
        public string licenseName;
        public UnityGisBuilding[] buildings;
        public UnityGisRoad[] roads;
        public UnityGisWater[] water;
    }

    public static class GisTileLoader
    {
        public const string FileName = "demo_tile_500m.unity.json";

        public static string StreamingPath => Path.Combine(Application.streamingAssetsPath, "gis", FileName);

        public static bool TryLoad(out UnityGisTile tile, out string error)
        {
            tile = null;
            error = "";
            var path = StreamingPath;
            if (!File.Exists(path))
            {
                error = "Missing GIS tile: " + path;
                return false;
            }
            try
            {
                tile = JsonUtility.FromJson<UnityGisTile>(File.ReadAllText(path));
                if (tile == null)
                {
                    error = "GIS JSON parsed as null.";
                    return false;
                }
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }
    }
}
