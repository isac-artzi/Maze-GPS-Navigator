// VoicePhrases — the single source of truth for everything the GPS can say.
// The Editor menu "Maze > Generate Voice Clips" turns each entry into
// Assets/Resources/Voice/<key>.wav using the operating system's speech engine.
using System.Collections.Generic;

namespace MazeNav
{
    public static class VoicePhrases
    {
        static Dictionary<string, string> table;

        public static IReadOnlyDictionary<string, string> All
        {
            get
            {
                if (table != null) return table;
                table = new Dictionary<string, string>
                {
                    ["route_calculated"] = "Route calculated. Follow my directions to the exit.",
                    ["recalculating"]    = "Recalculating.",
                    ["uturn"]            = "Make a U-turn.",
                    ["turn_left_now"]    = "Turn left.",
                    ["turn_right_now"]   = "Turn right.",
                    ["exit_ahead"]       = "The exit is straight ahead.",
                    ["arrived"]          = "You have arrived at the exit. Well done!",
                    ["guidance_on"]      = "Voice guidance on.",
                    ["guidance_off"]     = "Voice guidance off.",
                };
                for (int d = 5; d <= 50; d += 5)
                {
                    table[$"in_{d}_left"]  = $"In {d} meters, turn left.";
                    table[$"in_{d}_right"] = $"In {d} meters, turn right.";
                }
                return table;
            }
        }

        public static string Text(string key) => All.TryGetValue(key, out var t) ? t : key;
    }
}
