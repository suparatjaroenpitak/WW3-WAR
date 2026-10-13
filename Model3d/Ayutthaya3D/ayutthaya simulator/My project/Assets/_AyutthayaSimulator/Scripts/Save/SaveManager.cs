using System;
using System.IO;
using AyutthayaSimulator.Core;
using UnityEngine;

namespace AyutthayaSimulator
{
    public sealed class SaveManager : MonoBehaviour
    {
        public string FileName = "ayutthaya-simulator-save.json";
        public string LastStatus = "no_save_yet";

        public string PathOnDisk => Path.Combine(Application.persistentDataPath, FileName);

        public bool Save(PlayerController player, DayNightCycle cycle, WeatherManager weather, MissionManager missions)
        {
            try
            {
                var data = new SaveGameData();
                data.SavedAtUtc = DateTime.UtcNow.ToString("o");
                if (player != null)
                {
                    data.PlayerX = player.transform.position.x;
                    data.PlayerY = player.transform.position.y;
                    data.PlayerZ = player.transform.position.z;
                    data.PlayerYaw = player.transform.eulerAngles.y;
                    data.Language = player.State.Language;
                    data.LookSensitivity = player.Input.LookSensitivity;
                    data.InVehicle = player.State.Mode == PlayerMode.Driving;
                    data.VehicleId = player.CurrentVehicle != null ? player.CurrentVehicle.VehicleId : "";
                }
                if (cycle != null) data.GameHour = cycle.Hour;
                if (weather != null) data.Weather = weather.Current.ToString();
                if (missions != null) data.CompletedMissionIds = missions.CompletedIds();
                var json = SaveFileCodec.Encode(data);
                File.WriteAllText(PathOnDisk, json);
                LastStatus = "saved";
                return true;
            }
            catch (Exception ex)
            {
                LastStatus = "save_failed: " + ex.Message;
                Debug.LogWarning(LastStatus);
                return false;
            }
        }

        public bool Load(PlayerController player, DayNightCycle cycle, WeatherManager weather, MissionManager missions)
        {
            try
            {
                if (!File.Exists(PathOnDisk))
                {
                    LastStatus = "missing_save";
                    return false;
                }
                var data = SaveFileCodec.Decode(File.ReadAllText(PathOnDisk));
                if (player != null)
                {
                    player.State.Language = string.IsNullOrEmpty(data.Language) ? "th" : data.Language;
                    player.Input.LookSensitivity = data.LookSensitivity <= 0f ? 0.08f : data.LookSensitivity;
                    if (data.InVehicle)
                    {
                        LastStatus = "loaded_position_vehicle_flag_not_reentered";
                    }
                    player.Movement.Teleport(new Vector3(data.PlayerX, data.PlayerY, data.PlayerZ), data.PlayerYaw);
                }
                if (cycle != null) cycle.SetHour(data.GameHour);
                if (weather != null)
                    weather.Set(Enum.TryParse(data.Weather, out WeatherKind restoredWeather) ? restoredWeather : WeatherKind.Clear, true);
                if (cycle != null) cycle.ApplyLighting();
                if (missions != null) missions.Restore(data.CompletedMissionIds);
                if (LastStatus != "loaded_position_vehicle_flag_not_reentered") LastStatus = "loaded";
                return true;
            }
            catch (Exception ex)
            {
                LastStatus = "load_failed: " + ex.Message;
                Debug.LogWarning(LastStatus);
                return false;
            }
        }
    }
}
