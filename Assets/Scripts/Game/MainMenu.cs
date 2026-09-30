// MainMenu — the welcome screen (Main scene).
// Desktop: mouse-driven buttons: "Play on Desktop" / "Play on Meta Quest".
//   * Windows: the Quest button starts OpenXR, which talks to a Quest over Link / Air Link.
//   * macOS Editor: the Quest button builds the APK and installs it on a USB-connected Quest.
//   * macOS player: explains that Link needs Windows.
// On the Quest itself (or once XR is running) the menu floats in front of you:
// press A or the trigger to start; flick the left stick to change maze size.
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MazeNav
{
    public class MainMenu : MonoBehaviour
    {
        static readonly int[] Sizes = { 8, 10, 14, 18 };

        CameraRig rig;
        Text status, sizeLabel, cubeLabel;
        bool vr, starting;
        readonly ButtonEdge aEdge = new ButtonEdge(), trigEdge = new ButtonEdge(), stickEdge = new ButtonEdge(), cubeStickEdge = new ButtonEdge();

        void Start()
        {
            GameSettings.ParseCommandLine();
            vr = XRBoot.IsActive;
            rig = CameraRig.Create(vr);
            if (!vr) rig.Cam.backgroundColor = new Color(0.09f, 0.11f, 0.16f);
            BuildUI();
            if (GameSettings.AutoStart && !vr) StartGame(RunMode.Desktop);
        }

        void BuildUI()
        {
            UIFactory.EnsureEventSystem();
            var canvas = UIFactory.CreateCanvas("Menu Canvas", vr, new Vector2(1200, 760));
            if (vr)
            {
                canvas.transform.position = new Vector3(0, 1.5f, 1.8f);
                canvas.worldCamera = rig.Cam;
            }
            Transform root = canvas.transform;

            var card = UIFactory.Panel("Card", root, new Color(0.13f, 0.16f, 0.23f, 0.97f),
                                       new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-560, -350), new Vector2(560, 350));
            Transform c = card.transform;

            Place(UIFactory.Label("Title", c, "MAZE NAVIGATOR", 76, new Color(1f, 0.82f, 0.3f), TextAnchor.MiddleCenter, FontStyle.Bold), 0.80f, 0.95f);
            Place(UIFactory.Label("Subtitle", c, "Escape a procedurally generated maze with A* + GPS voice guidance.\nThe exit is locked: solve a 2×2×2 cube in 2 minutes or start over!", 30, new Color(0.85f, 0.9f, 1f)), 0.64f, 0.80f);

            var sizeBtn = UIFactory.Button("Size", c, "", new Color(0.25f, 0.3f, 0.4f), new Vector2(0.06f, 0.50f), new Vector2(0.48f, 0.60f), CycleSize, 28);
            sizeLabel = sizeBtn.GetComponentInChildren<Text>();
            RefreshSize();
            var cubeBtn = UIFactory.Button("Cube", c, "", new Color(0.25f, 0.3f, 0.4f), new Vector2(0.52f, 0.50f), new Vector2(0.94f, 0.60f), CycleCube, 28);
            cubeLabel = cubeBtn.GetComponentInChildren<Text>();
            RefreshCube();

            if (vr)
            {
                Place(UIFactory.Label("VRPrompt", c, "Press  A  or the trigger to start\nLeft stick ← → maze size   ·   Right stick ← → cube difficulty", 36, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold), 0.25f, 0.46f);
                Place(UIFactory.Label("Help", c, "In the maze:  left stick walk · right stick turn · B / Y bird's-eye · A show path · X repeat", 24, new Color(0.7f, 0.75f, 0.85f)), 0.04f, 0.2f);
            }
            else
            {
                UIFactory.Button("Desktop", c, "Play on Desktop", new Color(0.18f, 0.5f, 0.9f), new Vector2(0.06f, 0.28f), new Vector2(0.48f, 0.43f), () => StartGame(RunMode.Desktop), 34);
                UIFactory.Button("Quest", c, QuestButtonLabel(), new Color(0.47f, 0.3f, 0.85f), new Vector2(0.52f, 0.28f), new Vector2(0.94f, 0.43f), OnQuestClicked, 30);
                Place(UIFactory.Label("Help", c, "↑ ↓ walk   ← → turn   V bird's-eye view   P show path   R repeat   G voice on/off   T autopilot   Esc menu", 22, new Color(0.7f, 0.75f, 0.85f)), 0.04f, 0.14f);
            }
            status = UIFactory.Label("Status", c, "", 24, new Color(1f, 0.75f, 0.5f));
            Place(status, 0.15f, 0.27f);
        }

        static void Place(Graphic g, float yMin, float yMax)
        {
            var rt = g.rectTransform;
            rt.anchorMin = new Vector2(0.04f, yMin);
            rt.anchorMax = new Vector2(0.96f, yMax);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        static string QuestButtonLabel()
        {
            switch (Application.platform)
            {
                case RuntimePlatform.OSXEditor: return "Play on Meta Quest\n<size=20>(build & install over USB)</size>";
                case RuntimePlatform.OSXPlayer: return "Play on Meta Quest\n<size=20>(see note)</size>";
                default: return "Play on Meta Quest\n<size=20>(Quest Link / Air Link)</size>";
            }
        }

        void CycleSize()
        {
            int i = System.Array.IndexOf(Sizes, GameSettings.Size);
            GameSettings.Size = Sizes[(i + 1) % Sizes.Length];
            RefreshSize();
        }

        void CycleCube()
        {
            GameSettings.CubeLevel = (GameSettings.CubeLevel + 1) % GameSettings.CubeLevels.Length;
            RefreshCube();
        }

        void RefreshCube()
        {
            var (name, moves) = GameSettings.CubeLevels[GameSettings.CubeLevel];
            cubeLabel.text = $"Exit cube:  {name} ({moves}-move scramble)";
        }

        void RefreshSize() => sizeLabel.text = $"Maze size:  {GameSettings.Size} × {GameSettings.Size}";

        void Update()
        {
            if (starting) return;
            if (vr)
            {
                if (aEdge.Pressed(XRPad.A) | trigEdge.Pressed(XRPad.RightTrigger)) StartGame(RunMode.VR);
                if (stickEdge.Pressed(Mathf.Abs(XRPad.LeftStick.x) > 0.7f)) CycleSize();
                if (cubeStickEdge.Pressed(Mathf.Abs(XRPad.RightStick.x) > 0.7f)) CycleCube();
            }
            else if (Keyboard.current != null && Keyboard.current.enterKey.wasPressedThisFrame)
            {
                StartGame(RunMode.Desktop);
            }
        }

        void OnQuestClicked()
        {
#if UNITY_EDITOR
            if (Application.platform == RuntimePlatform.OSXEditor)
            {
                status.text = "Leaving Play mode and building for Quest… watch the Console.";
                var tool = System.Type.GetType("MazeNav.EditorTools.MazeProjectSetup, Assembly-CSharp-Editor");
                tool?.GetMethod("BuildAndInstallAfterPlayMode")?.Invoke(null, null);
                return;
            }
#endif
            if (Application.platform == RuntimePlatform.OSXPlayer)
            {
                status.text = "Quest Link isn't available on macOS. Open the project in Unity and choose\nMaze > Build & Install to Quest (USB cable, developer mode on).";
                return;
            }
            status.text = "Looking for a headset…";
            StartCoroutine(XRBoot.Start(ok =>
            {
                if (ok) StartGame(RunMode.VR);
                else status.text = "No headset found. Connect your Quest with Link or Air Link, then try again.";
            }));
        }

        void StartGame(RunMode mode)
        {
            starting = true;
            GameSettings.Mode = mode;
            SceneManager.LoadScene("Maze");
        }
    }
}
