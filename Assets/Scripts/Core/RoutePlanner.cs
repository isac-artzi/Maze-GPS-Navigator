// RoutePlanner — turns a list of cells into car-GPS style instructions.
// A path is a sequence of unit moves. A "turn" happens at the first cell where
// the move direction changes. The 2D cross product of the two directions tells
// us left (+) from right (-); SignedAngle tells us how the player is facing.
using System.Collections.Generic;
using UnityEngine;

namespace MazeNav
{
    public enum Maneuver { Straight, Left, Right, UTurn, Arrive }

    public struct RouteStep
    {
        public Maneuver Action;     // what to do next
        public Vector2Int Cell;     // where to do it
        public float Meters;        // how far away that is
    }

    public static class RoutePlanner
    {
        /// Left/Right/Straight/UTurn when travelling `from` and then `to` (grid directions).
        public static Maneuver TurnDirection(Vector2Int from, Vector2Int to)
        {
            int cross = from.x * to.y - from.y * to.x;
            if (cross > 0) return Maneuver.Left;
            if (cross < 0) return Maneuver.Right;
            return from == to ? Maneuver.Straight : Maneuver.UTurn;
        }

        /// How must the player rotate to face `moveDir`, given their forward vector (grid space)?
        public static Maneuver HeadingCorrection(Vector2Int moveDir, Vector2 forward)
        {
            float angle = Vector2.SignedAngle(forward, moveDir);   // + = counter-clockwise = left
            float abs = Mathf.Abs(angle);
            if (abs <= 50f) return Maneuver.Straight;
            if (abs >= 130f) return Maneuver.UTurn;
            return angle > 0 ? Maneuver.Left : Maneuver.Right;
        }

        /// The next maneuver along `path` (path[0] = the player's cell).
        /// playerGrid is the player position in continuous grid units (cell centres are integers).
        public static RouteStep NextStep(IReadOnlyList<Vector2Int> path, Vector2 playerGrid, float cellSize)
        {
            var step = new RouteStep { Action = Maneuver.Arrive };
            if (path == null || path.Count == 0) return step;

            if (path.Count >= 2)
            {
                Vector2Int first = path[1] - path[0];
                for (int i = 1; i < path.Count - 1; i++)
                {
                    Vector2Int d = path[i + 1] - path[i];
                    if (d != first)
                    {
                        step.Action = TurnDirection(first, d);
                        step.Cell = path[i];
                        step.Meters = Vector2.Distance(playerGrid, path[i]) * cellSize;
                        return step;
                    }
                }
            }
            step.Cell = path[path.Count - 1];
            step.Meters = Vector2.Distance(playerGrid, step.Cell) * cellSize;
            return step;
        }

        /// Rounds a distance to the nearest 5 m bucket used by the voice clips (5..50).
        public static int DistanceBucket(float meters) => Mathf.Clamp(Mathf.RoundToInt(meters / 5f) * 5, 5, 50);
    }
}
