// GameSettings — values carried from the Main scene into the Maze scene.
// Also reads optional command-line flags used for automated testing:
//   -autostart  -autopilot  -quitOnArrive  -seed N  -size N  -screenshots <dir>
using UnityEngine;

namespace MazeNav
{
    public enum RunMode { Desktop, VR }

    public static class GameSettings
    {
        public static RunMode Mode = RunMode.Desktop;
        public static int Size = 10;          // maze is Size x Size cells
        public static int Seed = 0;           // 0 = random every run
        public static bool Autopilot, AutoStart, QuitOnArrive;
        public static string ScreenshotDir;

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
                    case "-seed": if (i + 1 < args.Length) int.TryParse(args[++i], out Seed); break;
                    case "-size":
                        if (i + 1 < args.Length && int.TryParse(args[++i], out int s)) Size = Mathf.Clamp(s, 4, 30);
                        break;
                    case "-screenshots": if (i + 1 < args.Length) ScreenshotDir = args[++i]; break;
                }
            }
        }
    }
}
