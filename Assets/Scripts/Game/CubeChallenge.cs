// CubeChallenge — the locked exit. When the player reaches the exit cell they are
// taken to a "puzzle stage" (far away from the maze) with a scrambled 2x2x2 cube.
// Solve it before the clock runs out -> Solved event (the game ends).
// Run out of time -> Failed event (the game sends the player back to the start).
//
// Desktop: keys U D L R F B turn a face clockwise, Shift+key counter-clockwise,
//          or click the on-screen buttons. Arrow keys / mouse drag rotate the view.
// Quest:   right stick left/right picks a face (it lights up), A = clockwise, B = counter-clockwise,
//          left stick rotates the view.
// T (desktop) or autopilot: the cube solves itself by undoing every move — a demo, not a real solver.
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace MazeNav
{
    public class CubeChallenge : MonoBehaviour
    {
        public static readonly Vector3 StageOrigin = new Vector3(-2000f, 0f, -2000f);
        static readonly Face[] SelectOrder = { Face.F, Face.R, Face.U, Face.B, Face.L, Face.D };

        public bool Active { get; private set; }
        public int Attempts { get; private set; }
        public float SolveSeconds { get; private set; }
        public event System.Action Solved, Failed;

        MazeGame game;
        CameraRig rig;
        Transform stage, pedestal;
        CubeView cube;
        Canvas canvas;
        Text timerText, infoText, bannerText;
        GameObject bannerGo;

        float remaining, yaw = 30f, pitch = 25f;
        bool running, ended, autoSolve;
        int selected;
        Color savedBackground;
        float savedFov;
        bool announced60, announced30, announced10;
        readonly Queue<CubeMove> autoMoves = new Queue<CubeMove>();
        float autoNext;
        readonly ButtonEdge aEdge = new ButtonEdge(), bEdge = new ButtonEdge(), stickEdge = new ButtonEdge();

        public void Init(MazeGame g, CameraRig r) { game = g; rig = r; }

        // ------------------------------------------------------------------------------------
        public void Begin(bool autopilot)
        {
            Active = true;
            ended = false;
            running = false;
            Attempts++;
            remaining = GameSettings.CubeTimeLimit;
            announced60 = remaining <= 60; announced30 = remaining <= 30; announced10 = remaining <= 10;
            autoSolve = autopilot && !GameSettings.CubeGiveUp;
            autoMoves.Clear();
            yaw = 30f; pitch = 25f; selected = 0;

            if (stage == null) BuildStage();
            stage.gameObject.SetActive(true);
            if (cube != null) Destroy(cube.gameObject);

            // Put the viewer on the stage and the cube in front of them.
            savedBackground = rig.Cam.backgroundColor;
            savedFov = rig.Cam.fieldOfView;
            rig.Cam.backgroundColor = new Color(0.07f, 0.09f, 0.14f);
            float cubeY;
            if (rig.IsVR)
            {
                rig.transform.rotation = Quaternion.identity;
                float head = Mathf.Clamp(rig.Offset.localPosition.y + rig.Cam.transform.localPosition.y, 1.1f, 2.0f);
                Vector3 headOffset = rig.Cam.transform.position - rig.transform.position; headOffset.y = 0;
                rig.transform.position = StageOrigin + new Vector3(0, 0, -0.6f) - headOffset;
                cubeY = head - 0.25f;
            }
            else
            {
                cubeY = 1.2f;
                rig.transform.SetPositionAndRotation(StageOrigin + new Vector3(0, cubeY + 0.12f, -1.4f), Quaternion.identity);
                rig.Cam.fieldOfView = 45f;
            }
            pedestal.localScale = new Vector3(0.5f, (cubeY - 0.3f) / 2f, 0.5f);
            pedestal.localPosition = new Vector3(0, (cubeY - 0.3f) / 2f, 0);

            cube = CubeView.Create(stage, new Vector3(0, cubeY, 0));
            cube.Turned += OnTurned;
            ApplyOrbit();

            BuildUI();
            var scramble = Cube2Model.Scramble(GameSettings.ScrambleMoves, new System.Random(game.Seed * 31 + Attempts));
            foreach (var m in scramble) cube.Enqueue(m, player: false, seconds: 0.18f);
            Debug.Log($"[MazeNav] Cube challenge #{Attempts}: {scramble.Count}-move scramble, {GameSettings.CubeTimeLimit:F0}s limit.");
            game.Voice.Say("cube_intro", urgent: true, minRepeatSeconds: 0);
            StartCoroutine(StartClockWhenScrambled());
        }

        IEnumerator StartClockWhenScrambled()
        {
            yield return null;
            while (cube.Busy) yield return null;
            running = true;
            if (autoSolve) autoNext = Time.time + 2f;
        }

        void BuildStage()
        {
            stage = new GameObject("Puzzle Stage").transform;
            stage.position = StageOrigin;
            var floorMat = MazeBuilder.MakeMaterial(new Color(0.18f, 0.2f, 0.26f));
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            floor.name = "Floor";
            floor.transform.SetParent(stage, false);
            floor.transform.localScale = new Vector3(5f, 0.02f, 5f);
            floor.transform.localPosition = new Vector3(0, -0.02f, 0);
            floor.GetComponent<Renderer>().sharedMaterial = floorMat;

            var pedMat = MazeBuilder.MakeMaterial(new Color(0.55f, 0.58f, 0.65f));
            var ped = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ped.name = "Pedestal";
            ped.transform.SetParent(stage, false);
            ped.GetComponent<Renderer>().sharedMaterial = pedMat;
            pedestal = ped.transform;

            var lamp = new GameObject("Stage Light").AddComponent<Light>();
            lamp.transform.SetParent(stage, false);
            lamp.transform.localPosition = new Vector3(0.6f, 2.6f, -1.2f);
            lamp.type = LightType.Point;
            lamp.range = 6f;
            lamp.intensity = 1.4f;
        }

        // ------------------------------------------------------------------------------------
        void BuildUI()
        {
            if (canvas != null) Destroy(canvas.gameObject);
            bool vr = rig.IsVR;
            canvas = UIFactory.CreateCanvas("Cube UI", vr, new Vector2(900, 420));
            Transform root = canvas.transform;
            if (vr)
            {
                canvas.worldCamera = rig.Cam;
                canvas.transform.position = cube.transform.position + new Vector3(0, 0.55f, 0.35f);
                canvas.transform.localScale = Vector3.one * 0.0011f;
            }

            var top = vr
                ? UIFactory.Panel("Clock", root, new Color(0.05f, 0.07f, 0.12f, 0.85f), Vector2.zero, Vector2.one)
                : UIFactory.Panel("Clock", root, new Color(0.05f, 0.07f, 0.12f, 0.85f), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(-470, -165), new Vector2(470, -15));
            timerText = UIFactory.Label("Time", top.transform, "", vr ? 110 : 80, Color.white, TextAnchor.UpperCenter, FontStyle.Bold);
            timerText.rectTransform.offsetMax = new Vector2(0, -8);
            infoText = UIFactory.Label("Info", top.transform, "", vr ? 30 : 26, new Color(0.8f, 0.87f, 1f), TextAnchor.LowerCenter);
            infoText.rectTransform.offsetMin = new Vector2(10, 12);

            if (!vr)
            {
                // 6 rows x 2 buttons: U U' / D D' / ...
                var pad = UIFactory.Panel("Moves", root, new Color(0.05f, 0.07f, 0.12f, 0.85f), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-250, -300), new Vector2(-20, 300));
                UIFactory.Label("Title", UIFactory.Rect("TitleRow", pad.transform, new Vector2(0, 0.9f), new Vector2(1, 1)), "Turn a face", 26, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
                string[] names = { "Up", "Down", "Left", "Right", "Front", "Back" };
                for (int i = 0; i < 6; i++)
                {
                    Face f = Cube2Model.AllFaces[i];
                    float y1 = 0.88f - i * 0.145f, y0 = y1 - 0.125f;
                    var row = UIFactory.Rect(f.ToString(), pad.transform, new Vector2(0.05f, y0), new Vector2(0.95f, y1));
                    UIFactory.Label("Name", UIFactory.Rect("NameBox", row, new Vector2(0, 0), new Vector2(0.36f, 1)), names[i], 20, new Color(0.75f, 0.8f, 0.9f), TextAnchor.MiddleLeft);
                    UIFactory.Button("CW", row, f.ToString(), new Color(0.2f, 0.45f, 0.85f), new Vector2(0.38f, 0), new Vector2(0.68f, 1), () => Player(new CubeMove(f, false)), 26);
                    UIFactory.Button("CCW", row, f + "'", new Color(0.45f, 0.3f, 0.8f), new Vector2(0.70f, 0), new Vector2(1f, 1), () => Player(new CubeMove(f, true)), 26);
                }
                var help = UIFactory.Panel("Help", root, new Color(0, 0, 0, 0.55f), new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 0), new Vector2(0, 50));
                UIFactory.Label("Help", help.transform,
                    "Keys U D L R F B turn a face clockwise  ·  Shift + key = counter-clockwise (')  ·  arrow keys or drag = look around  ·  T = auto-solve demo  ·  Esc menu",
                    21, new Color(0.85f, 0.9f, 1f));
            }

            bannerGo = UIFactory.Panel("Banner", root, new Color(0.05f, 0.4f, 0.15f, 0.92f),
                vr ? new Vector2(0, 0) : new Vector2(0.25f, 0.4f), vr ? new Vector2(1, 1) : new Vector2(0.75f, 0.6f)).gameObject;
            bannerText = UIFactory.Label("Text", bannerGo.transform, "", vr ? 80 : 64, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            bannerGo.SetActive(false);
        }

        // ------------------------------------------------------------------------------------
        void Player(CubeMove m)
        {
            if (!running || ended) return;
            if (autoSolve) { autoSolve = false; autoMoves.Clear(); }     // the human took over
            cube.Enqueue(m);
        }

        void OnTurned(CubeMove m)
        {
            if (ended || !cube.Model.IsSolved()) return;
            StartCoroutine(Win());
        }

        void Update()
        {
            if (!Active || cube == null) return;
            HandleInput();
            ApplyOrbit();

            if (running && !ended)
            {
                remaining -= Time.deltaTime;
                if (!announced60 && remaining <= 60) { announced60 = true; game.Voice.Say("cube_one_minute", true, 0); }
                if (!announced30 && remaining <= 30) { announced30 = true; game.Voice.Say("cube_thirty", true, 0); }
                if (!announced10 && remaining <= 10) { announced10 = true; game.Voice.Say("cube_ten", true, 0); }
                if (remaining <= 0 && !cube.Busy) { remaining = 0; StartCoroutine(Lose()); }
                if (autoSolve) RunAutoSolve();
            }

            int secs = Mathf.CeilToInt(Mathf.Max(0, remaining));
            timerText.text = $"{secs / 60}:{secs % 60:00}";
            timerText.color = remaining > 30 ? Color.white : remaining > 10 ? new Color(1f, 0.8f, 0.3f) : new Color(1f, 0.35f, 0.3f);
            string sel = rig.IsVR ? $"Selected: {SelectOrder[selected]}   ·   " : "";
            string state = !running && !ended ? "Scrambling…" : autoSolve ? "Auto-solving (demo)" : $"Moves: {cube.MovesMade}";
            infoText.text = rig.IsVR
                ? $"{sel}{state}\nRight stick ← → choose a face · A turn clockwise · B counter-clockwise · Left stick look around"
                : $"Solve the cube to open the exit   ·   Attempt {Attempts}   ·   {state}";
        }

        void HandleInput()
        {
            float dt = Time.deltaTime;
            var k = Keyboard.current;
            if (k != null)
            {
                bool shift = k.leftShiftKey.isPressed || k.rightShiftKey.isPressed;
                if (k.uKey.wasPressedThisFrame) Player(new CubeMove(Face.U, shift));
                if (k.dKey.wasPressedThisFrame) Player(new CubeMove(Face.D, shift));
                if (k.lKey.wasPressedThisFrame) Player(new CubeMove(Face.L, shift));
                if (k.rKey.wasPressedThisFrame) Player(new CubeMove(Face.R, shift));
                if (k.fKey.wasPressedThisFrame) Player(new CubeMove(Face.F, shift));
                if (k.bKey.wasPressedThisFrame) Player(new CubeMove(Face.B, shift));
                if (k.tKey.wasPressedThisFrame && running && !ended) ToggleAutoSolve();
                if (k.leftArrowKey.isPressed) yaw -= 90 * dt;
                if (k.rightArrowKey.isPressed) yaw += 90 * dt;
                if (k.upArrowKey.isPressed) pitch += 70 * dt;
                if (k.downArrowKey.isPressed) pitch -= 70 * dt;
            }
            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.isPressed &&
                (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject()))
            {
                Vector2 d = mouse.delta.ReadValue();
                yaw -= d.x * 0.35f;
                pitch -= d.y * 0.35f;
            }

            if (rig.IsVR)
            {
                Vector2 look = XRPad.LeftStick;
                if (look.magnitude > 0.2f) { yaw -= look.x * 90 * dt; pitch += look.y * 70 * dt; }
                float sx = XRPad.RightStick.x;
                if (stickEdge.Pressed(Mathf.Abs(sx) > 0.7f))
                    selected = (selected + (sx > 0 ? 1 : SelectOrder.Length - 1)) % SelectOrder.Length;
                if (aEdge.Pressed(XRPad.A)) Player(new CubeMove(SelectOrder[selected], false));
                if (bEdge.Pressed(XRPad.B)) Player(new CubeMove(SelectOrder[selected], true));
                cube.SetHighlight(ended ? (Face?)null : SelectOrder[selected]);
            }
            pitch = Mathf.Clamp(pitch, -80f, 80f);
        }

        void ApplyOrbit()
        {
            // Yaw spins the cube around the vertical axis, pitch tips its top toward you.
            cube.transform.localRotation = Quaternion.AngleAxis(-pitch, Vector3.right) * Quaternion.AngleAxis(yaw, Vector3.up);
        }

        // ---- auto-solve: undo everything that was applied, newest first -------------------
        void ToggleAutoSolve()
        {
            autoSolve = !autoSolve;
            autoMoves.Clear();
            if (autoSolve) autoNext = Time.time + 0.3f;
        }

        void RunAutoSolve()
        {
            if (cube.Busy || Time.time < autoNext) return;
            if (autoMoves.Count == 0)
            {
                var all = cube.Applied;
                for (int i = all.Count - 1; i >= 0; i--) autoMoves.Enqueue(all[i].Inverse);
                if (autoMoves.Count == 0) return;
            }
            cube.Enqueue(autoMoves.Dequeue());
            autoNext = Time.time + 0.45f;
        }

        // ------------------------------------------------------------------------------------
        IEnumerator Win()
        {
            ended = true;
            running = false;
            SolveSeconds = GameSettings.CubeTimeLimit - remaining;
            Debug.Log($"[MazeNav] CUBE SOLVED in {SolveSeconds:F1}s, {cube.MovesMade} moves (attempt {Attempts}).");
            game.Voice.Say("cube_solved", urgent: true, minRepeatSeconds: 0);
            bannerGo.GetComponent<Image>().color = new Color(0.05f, 0.4f, 0.15f, 0.92f);
            bannerText.text = "SOLVED!";
            bannerGo.SetActive(true);
            for (float t = 0; t < 2.5f; t += Time.deltaTime) { yaw += 120 * Time.deltaTime; yield return null; }
            End();
            Solved?.Invoke();
        }

        IEnumerator Lose()
        {
            ended = true;
            running = false;
            Debug.Log($"[MazeNav] CUBE FAILED (attempt {Attempts}): time is up, back to the start.");
            game.Voice.Say("cube_failed", urgent: true, minRepeatSeconds: 0);
            bannerGo.GetComponent<Image>().color = new Color(0.55f, 0.08f, 0.08f, 0.92f);
            bannerText.text = "TIME'S UP!\n<size=36>Back to the start…</size>";
            bannerGo.SetActive(true);
            yield return new WaitForSeconds(3f);
            End();
            Failed?.Invoke();
        }

        void End()
        {
            Active = false;
            if (canvas != null) Destroy(canvas.gameObject);
            if (cube != null) Destroy(cube.gameObject);
            stage.gameObject.SetActive(false);
            rig.Cam.backgroundColor = savedBackground;
            rig.Cam.fieldOfView = savedFov;
        }
    }
}
