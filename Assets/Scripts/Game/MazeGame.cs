// MazeGame — the conductor of the Maze scene. It builds everything at runtime:
// maze -> geometry -> player -> camera rig -> GPS -> voice -> HUD.
// Keys (desktop): V view · P path · R repeat · G voice · T autopilot · Esc menu · Enter new maze (after finishing)
// Buttons (Quest): B/Y view · A path (A = new maze after finishing) · X repeat · Menu = main menu
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace MazeNav
{
    public class MazeGame : MonoBehaviour
    {
        public const float CellSize = 4f, WallHeight = 3f, WallThickness = 0.25f;

        public MazeGrid Grid { get; private set; }
        public int Seed { get; private set; }
        public PlayerController Player { get; private set; }
        public CameraRig Rig { get; private set; }
        public ViewController View { get; private set; }
        public GpsNavigator Gps { get; private set; }
        public VoiceGuide Voice { get; private set; }
        public bool ShowPath { get; private set; }
        public float Elapsed => (finished ? finishTime : Time.time) - startTime;

        Hud hud;
        LineRenderer pathLine;
        bool pathToggle, finished;
        float startTime, finishTime;
        readonly ButtonEdge aEdge = new ButtonEdge(), bEdge = new ButtonEdge(), yEdge = new ButtonEdge(),
                            xEdge = new ButtonEdge(), menuEdge = new ButtonEdge();

        void Start()
        {
            GameSettings.ParseCommandLine();
            if (XRBoot.IsActive) GameSettings.Mode = RunMode.VR;          // e.g. launched on the Quest
            else if (GameSettings.Mode == RunMode.VR) GameSettings.Mode = RunMode.Desktop;
            bool vr = GameSettings.Mode == RunMode.VR;

            Seed = GameSettings.Seed != 0 ? GameSettings.Seed : Random.Range(1, 1_000_000);
            Grid = MazeGenerator.Generate(GameSettings.Size, GameSettings.Size, Seed);
            MazeBuilder.Build(Grid, CellSize, WallHeight, WallThickness, out _);
            SetupLighting();

            // Spawn in the start cell, facing along the first leg of the route.
            var first = AStarPathfinder.FindPath(Grid, Grid.Start, Grid.Exit);
            Vector2Int dir = first.Count > 1 ? first[1] - first[0] : Vector2Int.up;
            Player = PlayerController.Create(this, CellCenter(Grid.Start), Quaternion.LookRotation(new Vector3(dir.x, 0, dir.y)));
            Player.Autopilot = GameSettings.Autopilot;

            Rig = CameraRig.Create(vr);
            if (vr) Rig.transform.rotation = Player.transform.rotation;
            View = gameObject.AddComponent<ViewController>();
            View.Init(this, Rig, Player);
            View.Changed += _ => RefreshPath();

            Voice = gameObject.AddComponent<VoiceGuide>();
            Gps = gameObject.AddComponent<GpsNavigator>();
            Gps.Init(this, Voice, Player.transform);
            Gps.PathChanged += _ => RefreshPath();
            Gps.OnArrived += Finish;

            CreatePathLine();
            UIFactory.EnsureEventSystem();
            hud = gameObject.AddComponent<Hud>();
            hud.Init(this, Rig, vr);

            startTime = Time.time;
            Debug.Log($"[MazeNav] Maze {Grid.Width}x{Grid.Height} seed {Seed}, mode {GameSettings.Mode}, shortest route {first.Count - 1} cells.");
            if (!string.IsNullOrEmpty(GameSettings.ScreenshotDir)) StartCoroutine(CaptureTour());
        }

        void SetupLighting()
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.55f, 0.57f, 0.62f);
            var sun = FindAnyObjectByType<Light>();
            if (sun == null) sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.transform.rotation = Quaternion.Euler(55, -30, 0);
            sun.intensity = 1.1f;
            // Real-time shadows are costly on a standalone headset; keep them for desktop only.
            sun.shadows = GameSettings.Mode == RunMode.VR ? LightShadows.None : LightShadows.Soft;
        }

        // ---- grid <-> world helpers -------------------------------------------------------
        public Vector2Int WorldToCell(Vector3 p) => new Vector2Int(Mathf.FloorToInt(p.x / CellSize), Mathf.FloorToInt(p.z / CellSize));
        public Vector3 CellCenter(Vector2Int c, float y = 0) => new Vector3((c.x + 0.5f) * CellSize, y, (c.y + 0.5f) * CellSize);
        public Vector2 ToGrid(Vector3 p) => new Vector2(p.x / CellSize - 0.5f, p.z / CellSize - 0.5f);

        // ---- path line (a glowing breadcrumb trail of the A* route) -----------------------
        void CreatePathLine()
        {
            var go = new GameObject("Route Line");
            pathLine = go.AddComponent<LineRenderer>();
            pathLine.material = new Material(Shader.Find("Sprites/Default"));
            pathLine.startColor = pathLine.endColor = new Color(0.2f, 0.9f, 1f, 0.9f);
            pathLine.widthMultiplier = 0.45f;
            pathLine.numCornerVertices = 4;
            pathLine.alignment = LineAlignment.TransformZ;
            go.transform.rotation = Quaternion.Euler(90, 0, 0);   // lie flat on the floor
            pathLine.useWorldSpace = true;
        }

        void RefreshPath()
        {
            ShowPath = pathToggle || !View.FirstPerson;          // always shown from above
            List<Vector2Int> p = Gps.Path;
            pathLine.enabled = ShowPath && p != null && !Gps.Arrived;
            if (!pathLine.enabled) return;
            pathLine.positionCount = p.Count;
            for (int i = 0; i < p.Count; i++) pathLine.SetPosition(i, CellCenter(p[i], 0.06f));
        }

        // ---- input ---------------------------------------------------------------------------
        void Update()
        {
            var k = Keyboard.current;
            bool vr = GameSettings.Mode == RunMode.VR;

            bool toggleView = (k != null && (k.vKey.wasPressedThisFrame || k.tabKey.wasPressedThisFrame));
            bool togglePath = k != null && k.pKey.wasPressedThisFrame;
            bool repeat = k != null && k.rKey.wasPressedThisFrame;
            bool toMenu = k != null && k.escapeKey.wasPressedThisFrame;
            bool newMaze = k != null && finished && (k.enterKey.wasPressedThisFrame || k.numpadEnterKey.wasPressedThisFrame);
            if (vr)
            {
                toggleView |= bEdge.Pressed(XRPad.B) | yEdge.Pressed(XRPad.Y);
                bool a = aEdge.Pressed(XRPad.A);
                if (finished) newMaze |= a; else togglePath |= a;
                repeat |= xEdge.Pressed(XRPad.X);
                toMenu |= menuEdge.Pressed(XRPad.Menu);
            }

            if (toggleView) View.Toggle();
            if (togglePath) { pathToggle = !pathToggle; RefreshPath(); }
            if (repeat) Voice.Repeat();
            if (k != null && k.gKey.wasPressedThisFrame) Voice.SetMuted(!Voice.Muted);
            if (k != null && k.tKey.wasPressedThisFrame && !finished) Player.Autopilot = !Player.Autopilot;
            if (newMaze) SceneManager.LoadScene("Maze");
            if (toMenu) SceneManager.LoadScene("Main");
        }

        void Finish()
        {
            finished = true;
            finishTime = Time.time;
            Player.InputEnabled = false;
            Player.Autopilot = false;
            RefreshPath();
            hud.ShowFinish(Elapsed, Gps.Recalculations);
            Debug.Log($"[MazeNav] ARRIVED in {Elapsed:F1}s with {Gps.Recalculations} recalculations.");
            if (GameSettings.QuitOnArrive) StartCoroutine(QuitSoon());
        }

        IEnumerator QuitSoon()
        {
            yield return new WaitForSeconds(2.5f);
            if (!string.IsNullOrEmpty(GameSettings.ScreenshotDir)) Shot("4-arrived.png");
            yield return new WaitForSeconds(0.5f);
            Application.Quit();
        }

        // ---- automated screenshots (for the README and for smoke tests) -------------------
        IEnumerator CaptureTour()
        {
            System.IO.Directory.CreateDirectory(GameSettings.ScreenshotDir);
            yield return new WaitForSeconds(2.5f);
            Shot("1-first-person.png");
            pathToggle = true; RefreshPath();
            yield return new WaitForSeconds(1.5f);
            Shot("2-first-person-path.png");
            yield return new WaitForEndOfFrame();
            yield return null;
            View.Toggle();
            yield return new WaitForSeconds(2.2f);
            Shot("3-birds-eye.png");
            yield return new WaitForEndOfFrame();   // the capture happens at the end of this frame
            yield return null;
            View.Toggle();
            pathToggle = false; RefreshPath();
        }

        void Shot(string file)
        {
            string path = System.IO.Path.Combine(GameSettings.ScreenshotDir, file);
            ScreenCapture.CaptureScreenshot(path);
            Debug.Log($"[MazeNav] Screenshot {path}");
        }
    }
}
