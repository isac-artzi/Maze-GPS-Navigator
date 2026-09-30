// GpsNavigator — the "car GPS". Every time the player enters a new cell:
//   1. Run A* from that cell to the exit (the route is always fresh).
//   2. If the new cell isn't the one the old route expected -> "Recalculating."
// Every frame it compares the route with where the player is facing:
//   * facing the wrong way           -> "Turn left." / "Turn right." / "Make a U-turn."
//   * a turn is coming up            -> "In 10 meters, turn left."   (once per turn)
//   * the turn is right here          -> "Turn left."                 (once per turn)
//   * no more turns                   -> "The exit is straight ahead."
using System.Collections.Generic;
using UnityEngine;

namespace MazeNav
{
    public class GpsNavigator : MonoBehaviour
    {
        static readonly Vector2Int None = new Vector2Int(int.MinValue, int.MinValue);

        public List<Vector2Int> Path { get; private set; }
        public RouteStep Step { get; private set; }
        public Maneuver Heading { get; private set; }
        public float RemainingMeters { get; private set; }
        public bool Arrived { get; private set; }
        public int Recalculations { get; private set; }

        public event System.Action<List<Vector2Int>> PathChanged;
        public event System.Action OnArrived;

        MazeGame game;
        VoiceGuide voice;
        Transform player;
        Vector2Int lastCell = None, farAnnounced = None, nowAnnounced = None;
        bool exitAnnounced;
        Maneuver pendingHeading = Maneuver.Straight;
        float pendingSince;

        public void Init(MazeGame g, VoiceGuide v, Transform p)
        {
            game = g; voice = v; player = p;
        }

        /// Forget the current route (after a teleport); the next frame plans a fresh one.
        public void ResetRoute()
        {
            Path = null;
            Arrived = false;
            lastCell = farAnnounced = nowAnnounced = None;
            exitAnnounced = false;
            pendingHeading = Maneuver.Straight;
        }

        void Update()
        {
            if (Arrived || player == null) return;
            Vector2Int cell = game.WorldToCell(player.position);
            if (cell != lastCell) OnCellChanged(cell);
            if (!Arrived) Guide();
        }

        void OnCellChanged(Vector2Int cell)
        {
            bool first = Path == null;
            bool offRoute = !first && (Path.Count < 2 || Path[1] != cell);
            lastCell = cell;
            if (!game.Grid.InBounds(cell)) return;

            Path = AStarPathfinder.FindPath(game.Grid, cell, game.Grid.Exit);
            PathChanged?.Invoke(Path);

            if (cell == game.Grid.Exit)
            {
                Arrived = true;
                RemainingMeters = 0;
                voice.Say("arrived", urgent: true, minRepeatSeconds: 0);
                OnArrived?.Invoke();
                return;
            }
            if (first)
            {
                Debug.Log($"[MazeNav] Route calculated: {Path.Count - 1} cells to the exit.");
                voice.Say("route_calculated");
            }
            else if (offRoute)
            {
                Recalculations++;
                Debug.Log($"[MazeNav] Off route at {cell}; new route {Path.Count - 1} cells.");
                voice.Say("recalculating", urgent: true, minRepeatSeconds: 2f);
                farAnnounced = nowAnnounced = None;
                exitAnnounced = false;
            }
        }

        void Guide()
        {
            if (Path == null || Path.Count < 2) return;
            float cs = MazeGame.CellSize;

            Step = RoutePlanner.NextStep(Path, game.ToGrid(player.position), cs);
            RemainingMeters = (Path.Count - 1) * cs;

            // 1) Is the player even facing along the route? (debounced so turning on the spot isn't nagged)
            Vector2 forward = new Vector2(player.forward.x, player.forward.z);
            Maneuver heading = RoutePlanner.HeadingCorrection(Path[1] - Path[0], forward);
            Heading = heading;
            if (heading != Maneuver.Straight)
            {
                if (pendingHeading != heading) { pendingHeading = heading; pendingSince = Time.time; }
                else if (Time.time - pendingSince > 0.6f)
                {
                    string key = heading == Maneuver.UTurn ? "uturn" : heading == Maneuver.Left ? "turn_left_now" : "turn_right_now";
                    voice.Say(key, urgent: false, minRepeatSeconds: 5f);
                }
                return;
            }
            pendingHeading = Maneuver.Straight;

            // 2) No turns left: the exit is ahead.
            if (Step.Action == Maneuver.Arrive)
            {
                if (!exitAnnounced) { exitAnnounced = true; voice.Say("exit_ahead"); }
                return;
            }

            // 3) Upcoming turn: announce once from afar, once at the corner.
            string dir = Step.Action == Maneuver.Left ? "left" : "right";
            if (Step.Cell != farAnnounced && Step.Meters > cs * 0.9f)
            {
                farAnnounced = Step.Cell;
                voice.Say($"in_{RoutePlanner.DistanceBucket(Step.Meters)}_{dir}", urgent: false, minRepeatSeconds: 0.5f);
            }
            else if (Step.Cell != nowAnnounced && Step.Meters <= cs * 0.6f)
            {
                nowAnnounced = farAnnounced = Step.Cell;
                voice.Say($"turn_{dir}_now", urgent: true, minRepeatSeconds: 1.5f);
            }
        }
    }
}
