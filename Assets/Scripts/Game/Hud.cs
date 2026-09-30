// Hud — the GPS panel (arrow + next instruction + distance), subtitles,
// status line, controls help and the "You escaped!" panel.
// Desktop: screen overlay. VR: a small panel fixed below your line of sight.
using UnityEngine;
using UnityEngine.UI;

namespace MazeNav
{
    public class Hud : MonoBehaviour
    {
        MazeGame game;
        Text subtitle, instruction, distance, timer, status, finishText;
        Image arrow, subtitleBg;
        GameObject finishPanel;
        float subtitleUntil;

        public void Init(MazeGame g, CameraRig rig, bool vr)
        {
            game = g;
            var canvas = UIFactory.CreateCanvas("HUD", vr, new Vector2(1000, 560));
            Transform root = canvas.transform;
            if (vr)
            {
                canvas.transform.SetParent(rig.Cam.transform, false);
                canvas.transform.localPosition = new Vector3(0, -0.28f, 1.1f);
                canvas.transform.localRotation = Quaternion.Euler(15, 0, 0);
                canvas.transform.localScale = Vector3.one * 0.0009f;
                canvas.worldCamera = rig.Cam;
            }

            // Subtitles (top on desktop, top of the panel in VR)
            subtitleBg = UIFactory.Panel("Subtitle BG", root, new Color(0, 0, 0, 0.6f),
                new Vector2(0.15f, vr ? 0.80f : 0.88f), new Vector2(0.85f, vr ? 1f : 0.97f));
            subtitle = UIFactory.Label("Subtitle", subtitleBg.transform, "", vr ? 40 : 34, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);

            // GPS card
            var card = vr
                ? UIFactory.Panel("GPS", root, new Color(0.08f, 0.1f, 0.15f, 0.85f), new Vector2(0.2f, 0.3f), new Vector2(0.8f, 0.76f))
                : UIFactory.Panel("GPS", root, new Color(0.08f, 0.1f, 0.15f, 0.85f), new Vector2(0, 1), new Vector2(0, 1), new Vector2(20, -230), new Vector2(430, -20));
            var arrowRt = UIFactory.Rect("Arrow", card.transform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(15, -70), new Vector2(155, 70));
            arrow = arrowRt.gameObject.AddComponent<Image>();
            arrow.sprite = UIFactory.ArrowSprite();
            arrow.color = new Color(0.3f, 0.85f, 1f);
            instruction = Sub(card.transform, "Instruction", 34, FontStyle.Bold, 0.62f, 0.95f);
            distance = Sub(card.transform, "Distance", 26, FontStyle.Normal, 0.36f, 0.62f);
            timer = Sub(card.transform, "Timer", 24, FontStyle.Normal, 0.08f, 0.36f);
            timer.color = new Color(0.7f, 0.8f, 0.9f);

            // Status + help (desktop only; VR help is on the start screen)
            if (!vr)
            {
                var statusBg = UIFactory.Panel("Status", root, new Color(0.08f, 0.1f, 0.15f, 0.85f), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-380, -140), new Vector2(-20, -20));
                status = UIFactory.Label("Status", statusBg.transform, "", 24, Color.white, TextAnchor.MiddleLeft);
                status.rectTransform.offsetMin = new Vector2(16, 0);
                var helpBg = UIFactory.Panel("Help", root, new Color(0, 0, 0, 0.5f), new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 0), new Vector2(0, 44));
                UIFactory.Label("Help", helpBg.transform,
                    "↑ ↓ walk    ← → turn    V bird's-eye    P path    R repeat    G voice    T autopilot    Esc menu", 22, new Color(0.85f, 0.9f, 1f));
            }

            // Finish panel
            var fin = UIFactory.Panel("Finish", root, new Color(0.05f, 0.35f, 0.15f, 0.93f), new Vector2(0.2f, 0.3f), new Vector2(0.8f, 0.7f));
            finishPanel = fin.gameObject;
            finishText = UIFactory.Label("Text", fin.transform, "", vr ? 40 : 38, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            finishPanel.SetActive(false);

            game.Voice.Subtitle += ShowSubtitle;
            subtitleBg.gameObject.SetActive(false);
        }

        static Text Sub(Transform card, string name, int size, FontStyle style, float yMin, float yMax)
        {
            var t = UIFactory.Label(name, card, "", size, Color.white, TextAnchor.MiddleLeft, style);
            t.rectTransform.anchorMin = new Vector2(0, yMin);
            t.rectTransform.anchorMax = new Vector2(1, yMax);
            t.rectTransform.offsetMin = new Vector2(170, 0);
            t.rectTransform.offsetMax = new Vector2(-10, 0);
            return t;
        }

        void ShowSubtitle(string text)
        {
            subtitle.text = text;
            subtitleBg.gameObject.SetActive(true);
            subtitleUntil = Time.time + 4f;
        }

        public void ShowFinish(float seconds, int recalcs)
        {
            finishPanel.SetActive(true);
            string again = GameSettings.Mode == RunMode.VR ? "A = new maze    Menu button = main menu" : "Enter = new maze    Esc = main menu";
            finishText.text = $"You escaped!\nTime {Format(seconds)}   ·   {recalcs} recalculation{(recalcs == 1 ? "" : "s")}\n\n<size=26>{again}</size>";
        }

        static string Format(float s) => $"{(int)s / 60}:{(int)s % 60:00}";

        void Update()
        {
            if (subtitleBg.gameObject.activeSelf && Time.time > subtitleUntil) subtitleBg.gameObject.SetActive(false);

            var gps = game.Gps;
            timer.text = $"Time {Format(game.Elapsed)}";
            if (gps.Arrived)
            {
                instruction.text = "Arrived";
                distance.text = "Exit reached";
                arrow.rectTransform.localRotation = Quaternion.identity;
            }
            else if (gps.Path != null)
            {
                float z;
                string what;
                switch (gps.Heading)
                {
                    case Maneuver.Left: z = 90; what = "Turn left"; break;
                    case Maneuver.Right: z = -90; what = "Turn right"; break;
                    case Maneuver.UTurn: z = 180; what = "Make a U-turn"; break;
                    default:
                        var s = gps.Step;
                        if (s.Action == Maneuver.Arrive) { z = 0; what = "Exit ahead"; }
                        else
                        {
                            z = s.Action == Maneuver.Left ? 90 : -90;
                            what = $"{(s.Action == Maneuver.Left ? "Left" : "Right")} in {Mathf.RoundToInt(s.Meters)} m";
                        }
                        break;
                }
                arrow.rectTransform.localRotation = Quaternion.Euler(0, 0, z);
                instruction.text = what;
                distance.text = $"Exit: {Mathf.RoundToInt(gps.RemainingMeters)} m";
            }

            if (status != null)
                status.text = $"View: {(game.View.FirstPerson ? "first person" : "bird's-eye")}\n" +
                              $"Voice: {(game.Voice.Muted ? "off" : "on")}   Path: {(game.ShowPath ? "on" : "off")}\n" +
                              $"Autopilot: {(game.Player.Autopilot ? "ON" : "off")}   Seed {game.Seed}";
        }
    }
}
