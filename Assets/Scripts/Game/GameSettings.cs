// GameSettings — values carried from the Main scene into the Maze scene.
// Also reads optional command-line flags used for automated testing:
//   -autostart  -autopilot  -quitOnArrive  -seed N  -size N  -screenshots <dir>
//   -scramble N (cube scramble length)  -cubeTime S (seconds)  -cubeGiveUp (autopilot won't solve)  -quitAfter S
using UnityEngine;

namespace MazeNav
{
    public enum RunMode { Desktop, VR }

    public static class GameSettings
    {
        public static RunMode Mode = RunMode.Desktop;
        public static int Size = 10;          // maze is Size x Size cells
        public static int Seed = 0;           // 0 = random every run
        public static bool Autopilot, AutoStart, QuitOnArrive, CubeGiveUp;
        public static string ScreenshotDir;
        public static float QuitAfter;        // 0 = never

        // The exit challenge: a 2x2x2 cube scrambled with this many quarter turns.
        public static readonly (string name, int moves)[] CubeLevels = { ("Easy", 3), ("Medium", 5), ("Hard", 9) };
        public static int CubeLevel = 0;
        public static int ScrambleOverride;   // from -scramble, 0 = use CubeLevel
        public static int ScrambleMoves => ScrambleOverride > 0 ? ScrambleOverride : CubeLevels[CubeLevel].moves;
        public static float CubeTimeLimit = 120f;

        static bool parsed;

        public static void ParseCommandLine()
        {
            if (parsed) return;
            parsed = true;
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "-autostart": AutoStart = true; break;
                    case "-autopilot": Autopilot = true; break;
                    case "-quitOnArrive": QuitOnArrive = true; break;
                    case "-cubeGiveUp": CubeGiveUp = true; break;
                    case "-seed": if (i + 1 < args.Length) int.TryParse(args[++i], out Seed); break;
                    case "-size":
                        if (i + 1 < args.Length && int.TryParse(args[++i], out int s)) Size = Mathf.Clamp(s, 4, 30);
                        break;
                    case "-scramble": if (i + 1 < args.Length) int.TryParse(args[++i], out ScrambleOverride); break;
                    case "-cubeTime":
                        if (i + 1 < args.Length && float.TryParse(args[++i], System.Globalization.NumberStyles.Float,
                                System.Globalization.CultureInfo.InvariantCulture, out float t)) CubeTimeLimit = t;
                        break;
                    case "-quitAfter":
                        if (i + 1 < args.Length) float.TryParse(args[++i], System.Globalization.NumberStyles.Float,
                                System.Globalization.CultureInfo.InvariantCulture, out QuitAfter);
                        break;
                    case "-screenshots": if (i + 1 < args.Length) ScreenshotDir = args[++i]; break;
                }
            }
        }
    }
}
