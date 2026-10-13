using System;
using System.Globalization;
using System.Text;

namespace AyutthayaSimulator.Core
{
    public sealed class SaveGameData
    {
        public int Version = 1;
        public string SavedAtUtc = "";
        public float PlayerX;
        public float PlayerY;
        public float PlayerZ;
        public float PlayerYaw;
        public bool InVehicle;
        public string VehicleId = "";
        public float GameHour;
        public string Weather = "Clear";
        public string Language = "th";
        public string[] CompletedMissionIds = Array.Empty<string>();
        public float LookSensitivity = 0.12f;
    }

    public static class SaveFileCodec
    {
        public const int CurrentVersion = 1;

        public static string Encode(SaveGameData data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (data.Version != CurrentVersion)
                throw new InvalidOperationException("Unsupported save version " + data.Version);
            var builder = new StringBuilder(512);
            builder.Append("{\n");
            Field(builder, "version", data.Version.ToString(CultureInfo.InvariantCulture), true);
            Field(builder, "savedAtUtc", Quote(data.SavedAtUtc ?? ""), true);
            Field(builder, "playerX", F(data.PlayerX), true);
            Field(builder, "playerY", F(data.PlayerY), true);
            Field(builder, "playerZ", F(data.PlayerZ), true);
            Field(builder, "playerYaw", F(data.PlayerYaw), true);
            Field(builder, "inVehicle", data.InVehicle ? "true" : "false", true);
            Field(builder, "vehicleId", Quote(data.VehicleId ?? ""), true);
            Field(builder, "gameHour", F(data.GameHour), true);
            Field(builder, "weather", Quote(data.Weather ?? "Clear"), true);
            Field(builder, "language", Quote(data.Language ?? "th"), true);
            Field(builder, "lookSensitivity", F(data.LookSensitivity), true);
            builder.Append("  \"completedMissionIds\": [");
            var ids = data.CompletedMissionIds ?? Array.Empty<string>();
            for (var i = 0; i < ids.Length; i++)
            {
                if (i > 0) builder.Append(", ");
                builder.Append(Quote(ids[i] ?? ""));
            }
            builder.Append("]\n}\n");
            return builder.ToString();
        }

        public static SaveGameData Decode(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) throw new InvalidOperationException("Save file is empty.");
            var data = new SaveGameData();
            data.Version = ReadInt(json, "version");
            if (data.Version != CurrentVersion)
                throw new InvalidOperationException("Save version " + data.Version + " is not supported. Expected " + CurrentVersion + ".");
            data.SavedAtUtc = ReadString(json, "savedAtUtc");
            data.PlayerX = ReadFloat(json, "playerX");
            data.PlayerY = ReadFloat(json, "playerY");
            data.PlayerZ = ReadFloat(json, "playerZ");
            data.PlayerYaw = ReadFloat(json, "playerYaw");
            data.InVehicle = ReadBool(json, "inVehicle");
            data.VehicleId = ReadString(json, "vehicleId");
            data.GameHour = ReadFloat(json, "gameHour");
            data.Weather = ReadString(json, "weather");
            data.Language = ReadString(json, "language");
            data.LookSensitivity = ReadFloat(json, "lookSensitivity");
            data.CompletedMissionIds = ReadStringArray(json, "completedMissionIds");
            return data;
        }

        static void Field(StringBuilder builder, string name, string raw, bool comma)
        {
            builder.Append("  \"").Append(name).Append("\": ").Append(raw);
            if (comma) builder.Append(',');
            builder.Append('\n');
        }

        static string F(float value) => value.ToString("R", CultureInfo.InvariantCulture);
        static string Quote(string value) => "\"" + (value ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

        static int ReadInt(string json, string name) => int.Parse(ReadRaw(json, name), CultureInfo.InvariantCulture);
        static float ReadFloat(string json, string name) => float.Parse(ReadRaw(json, name), CultureInfo.InvariantCulture);
        static bool ReadBool(string json, string name) => ReadRaw(json, name) == "true";

        static string ReadString(string json, string name)
        {
            var token = ReadRaw(json, name);
            if (token.Length < 2 || token[0] != '"') throw new InvalidOperationException("Field " + name + " is not a string.");
            return token.Substring(1, token.Length - 2).Replace("\\\"", "\"").Replace("\\\\", "\\");
        }

        static string[] ReadStringArray(string json, string name)
        {
            var key = "\"" + name + "\"";
            var index = json.IndexOf(key, StringComparison.Ordinal);
            if (index < 0) return Array.Empty<string>();
            var start = json.IndexOf('[', index);
            var end = json.IndexOf(']', start);
            if (start < 0 || end < 0) throw new InvalidOperationException("Field " + name + " is not an array.");
            var body = json.Substring(start + 1, end - start - 1).Trim();
            if (body.Length == 0) return Array.Empty<string>();
            var parts = body.Split(',');
            var values = new string[parts.Length];
            for (var i = 0; i < parts.Length; i++)
            {
                var token = parts[i].Trim();
                values[i] = token.Length >= 2 ? token.Substring(1, token.Length - 2) : "";
            }
            return values;
        }

        static string ReadRaw(string json, string name)
        {
            var key = "\"" + name + "\"";
            var index = json.IndexOf(key, StringComparison.Ordinal);
            if (index < 0) throw new InvalidOperationException("Save is missing field " + name + ".");
            var colon = json.IndexOf(':', index + key.Length);
            if (colon < 0) throw new InvalidOperationException("Save field " + name + " is malformed.");
            var i = colon + 1;
            while (i < json.Length && char.IsWhiteSpace(json[i])) i++;
            if (i >= json.Length) throw new InvalidOperationException("Save field " + name + " has no value.");
            if (json[i] == '"')
            {
                var end = i + 1;
                while (end < json.Length && json[end] != '"') end++;
                return json.Substring(i, end - i + 1);
            }
            var stop = i;
            while (stop < json.Length && json[stop] != ',' && json[stop] != '\n' && json[stop] != '}') stop++;
            return json.Substring(i, stop - i).Trim();
        }
    }
}
