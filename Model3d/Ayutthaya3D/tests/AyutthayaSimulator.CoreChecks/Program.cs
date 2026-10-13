using System;
using System.IO;
using AyutthayaSimulator.Core;

internal static class Program
{
    static int failures;

    static void Main()
    {
        CheckUtm();
        CheckTiles();
        CheckSave();
        CheckMissions();
        CheckBinding();
        CheckPolygon();
        CheckLocalization();
        if (failures > 0)
        {
            Console.Error.WriteLine("FAILED " + failures);
            Environment.Exit(1);
        }
        Console.WriteLine("Core checks passed.");
    }

    static void CheckUtm()
    {
        Near(14.3574, 100.5681, 669093.9953, 1587827.4463);
        Near(14.3558, 100.5586, 668070.5458, 1587643.4967);
        Near(14.35, 100.56, 668225.8788, 1587002.8358);
        Near(14.0, 99.0, 500000.0000, 1547726.3067);
        Near(20.0, 105.0, 1128517.4513, 2222765.8092);
        var origin = GisCoordinateConverter.Wgs84ToUtm(14.3559133, 100.5585079);
        var local = GisCoordinateConverter.ToLocal(14.3559133, 100.5585079, origin);
        Eq(Math.Abs(local.X) < 0.01 && Math.Abs(local.Z) < 0.01, "origin local is zero");
        Eq(GisCoordinateConverter.ZoneFromLongitude(100.56) == 47, "Ayutthaya longitude is UTM zone 47");
    }

    static void Near(double lat, double lon, double east, double north)
    {
        var value = GisCoordinateConverter.Wgs84ToUtm(lat, lon);
        var de = Math.Abs(value.Easting - east);
        var dn = Math.Abs(value.Northing - north);
        Eq(de < 0.02 && dn < 0.02, "UTM " + lat + "," + lon + " error e=" + de + " n=" + dn);
    }

    static void CheckTiles()
    {
        var tile = TileStreamingPolicy.TileFromLocal(10, -1, 500);
        Eq(tile.X == 0 && tile.Z == -1, "tile index");
        var around = TileStreamingPolicy.TilesInRadius(new TileId(0, 0), 1);
        Eq(around.Count == 9, "load radius count");
        Eq(TileStreamingPolicy.ShouldUnload(new TileId(3, 0), new TileId(0, 0), 2), "unload far tile");
        Eq(!TileStreamingPolicy.ShouldUnload(new TileId(1, 1), new TileId(0, 0), 2), "keep near tile");
        Eq(TileStreamingPolicy.ShouldShiftOrigin(2500, 0, 2000), "shift origin");
        Eq(!TileStreamingPolicy.ShouldShiftOrigin(10, 10, 2000), "do not shift demo");
    }

    static void CheckSave()
    {
        var data = new SaveGameData
        {
            Version = 1,
            SavedAtUtc = "2026-10-13T00:00:00Z",
            PlayerX = 12.5f,
            PlayerY = 1.25f,
            PlayerZ = -3.5f,
            PlayerYaw = 90f,
            InVehicle = false,
            VehicleId = "",
            GameHour = 15.5f,
            Weather = "Rain",
            Language = "en",
            LookSensitivity = 0.08f,
            CompletedMissionIds = new[] { "walk_to_marker", "talk_guide" }
        };
        var decoded = SaveFileCodec.Decode(SaveFileCodec.Encode(data));
        Eq(Math.Abs(decoded.PlayerX - 12.5f) < 0.001f, "save x");
        Eq(decoded.Weather == "Rain" && decoded.Language == "en", "save weather language");
        Eq(decoded.CompletedMissionIds.Length == 2, "save missions");
        var threw = false;
        try { SaveFileCodec.Decode("{\"version\": 9}"); }
        catch (InvalidOperationException) { threw = true; }
        Eq(threw, "reject unknown save version");
    }

    static void CheckMissions()
    {
        var reach = new MissionRecord { Id = "walk", Kind = MissionKind.ReachPoint, TargetX = 10, TargetZ = 0, Radius = 2 };
        Eq(!MissionProgress.TryComplete(reach, "", 0, 0, false), "too far");
        Eq(MissionProgress.TryComplete(reach, "", 11, 0, false), "reached");
        Eq(!MissionProgress.TryComplete(reach, "", 11, 0, false), "not double completed");
        var talk = new MissionRecord { Id = "talk", Kind = MissionKind.TalkToNpc, TargetId = "guide_npc" };
        Eq(MissionProgress.TryComplete(talk, "guide_npc", 0, 0, false), "talk");
        var drive = new MissionRecord { Id = "drive", Kind = MissionKind.EnterVehicle, TargetId = "demo_vehicle" };
        Eq(!MissionProgress.TryComplete(drive, "demo_vehicle", 0, 0, false), "enter flag required");
        Eq(MissionProgress.TryComplete(drive, "demo_vehicle", 0, 0, true), "entered");
    }

    static void CheckBinding()
    {
        var nameOnly = ModelBindingRules.Classify(new ModelNodeFacts { Name = "Door_01", HasMesh = false });
        Eq(nameOnly.Decision == BindingDecision.Reject, "name without mesh rejected");
        var candidate = ModelBindingRules.Classify(new ModelNodeFacts { Name = "Door_01", HasMesh = true, HasRendererBounds = true });
        Eq(candidate.Decision == BindingDecision.CandidateNeedsConfirmation, "mesh plus name is candidate");
        var forced = ModelBindingRules.Classify(new ModelNodeFacts { Name = "Cube", HasMesh = true, ManualOverride = true, OverrideKind = ModelKind.Building });
        Eq(forced.Decision == BindingDecision.Bind && forced.Kind == ModelKind.Building, "manual override binds");
    }

    static void CheckPolygon()
    {
        var square = new float[] { 0, 0, 4, 0, 4, 3, 0, 3, 0, 0 };
        Eq(Math.Abs(PolygonTriangulator.Area(square) - 12f) < 0.01f, "polygon area");
        Eq(PolygonTriangulator.Triangulate(square).Length >= 6, "polygon triangles");
    }

    static void CheckLocalization()
    {
        Eq(LocalizationCatalog.Get("th", "interact").Contains("E"), "thai prompt");
        Eq(LocalizationCatalog.Get("en", "interact").Contains("Press E"), "english prompt");
        Eq(LocalizationCatalog.Get("th", "missing_key") == "missing_key", "unknown key");
    }

    static void Eq(bool condition, string name)
    {
        if (condition) return;
        failures++;
        Console.Error.WriteLine("FAIL " + name);
    }
}
