using System.Collections.Generic;

namespace AyutthayaSimulator.Core
{
    public static class LocalizationCatalog
    {
        static readonly Dictionary<string, string> Thai = new Dictionary<string, string>
        {
            { "interact", "กด E เพื่อโต้ตอบ" },
            { "enter_vehicle", "กด E เพื่อขึ้นรถ" },
            { "exit_vehicle", "กด F เพื่อลงรถ" },
            { "open_door", "กด E เพื่อเปิดหรือปิดประตู" },
            { "talk", "กด E เพื่อพูดคุย" },
            { "pause", "หยุดชั่วคราว" },
            { "resume", "เล่นต่อ" },
            { "save", "บันทึก" },
            { "load", "โหลด" },
            { "language", "ภาษา" },
            { "mission", "ภารกิจ" },
            { "no_mission", "ไม่มีภารกิจที่กำลังทำ" },
            { "walk", "เดิน / วิ่ง: WASD, Shift" },
            { "look", "มอง: เมาส์" },
            { "credits", "ข้อมูลแผนที่ © OpenStreetMap contributors (ODbL)" },
            { "terrain_placeholder", "พื้นดินเป็นระนาบ Placeholder ไม่ใช่ความสูงจาก DEM" },
            { "height_estimated", "ความสูงอาคารเป็นค่าประมาณ" },
            { "demo_title", "AYUTTHAYA SIMULATOR" },
            { "settings", "ตั้งค่า" },
            { "sensitivity", "ความไวเมาส์" },
            { "weather_clear", "อากาศแจ่มใส" },
            { "weather_rain", "ฝน" },
            { "weather_overcast", "เมฆครึ้ม" },
            { "weather_storm", "ฝนหนัก" },
            { "environment_controls", "T: เวลา +3 ชม.  Y: หยุดเวลา  R: อากาศ" },
            { "photo", "กด P เพื่อบันทึกภาพหน้าจอในโหมดเล่น" }
        };

        static readonly Dictionary<string, string> English = new Dictionary<string, string>
        {
            { "interact", "Press E to interact" },
            { "enter_vehicle", "Press E to enter vehicle" },
            { "exit_vehicle", "Press F to exit vehicle" },
            { "open_door", "Press E to open or close the door" },
            { "talk", "Press E to talk" },
            { "pause", "Paused" },
            { "resume", "Resume" },
            { "save", "Save" },
            { "load", "Load" },
            { "language", "Language" },
            { "mission", "Mission" },
            { "no_mission", "No active mission" },
            { "walk", "Move / run: WASD, Shift" },
            { "look", "Look: mouse" },
            { "credits", "Map data © OpenStreetMap contributors (ODbL)" },
            { "terrain_placeholder", "Ground is a flat placeholder, not DEM elevation" },
            { "height_estimated", "Building height is estimated" },
            { "demo_title", "AYUTTHAYA SIMULATOR" },
            { "settings", "Settings" },
            { "sensitivity", "Mouse sensitivity" },
            { "weather_clear", "Clear" },
            { "weather_rain", "Rain" },
            { "weather_overcast", "Overcast" },
            { "weather_storm", "Heavy rain" },
            { "environment_controls", "T: +3 hours  Y: freeze clock  R: weather" },
            { "photo", "Press P to capture a screenshot in Play Mode" }
        };

        public static string Get(string language, string key)
        {
            var table = language == "en" ? English : Thai;
            string value;
            if (table.TryGetValue(key, out value)) return value;
            if (English.TryGetValue(key, out value)) return value;
            return key;
        }
    }
}
