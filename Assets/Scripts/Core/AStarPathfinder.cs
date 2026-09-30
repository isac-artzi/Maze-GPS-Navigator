// AStarPathfinder — finds the shortest walkable route between two cells.
//   g(n) = steps taken from the start to n
//   h(n) = Manhattan distance from n to the goal (never over-estimates on a grid)
//   f(n) = g(n) + h(n)  -> always expand the open cell with the smallest f
// The open list is a plain List scanned linearly: easy to read, and fast
// enough for mazes of a few hundred cells. (A binary heap is the upgrade.)
using System.Collections.Generic;
using UnityEngine;

namespace MazeNav
{
    public static class AStarPathfinder
    {
        public static int Heuristic(Vector2Int a, Vector2Int b) => Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);

        /// Returns the list of cells from start to goal (inclusive), or null if unreachable.
        public static List<Vector2Int> FindPath(MazeGrid grid, Vector2Int start, Vector2Int goal)
        {
            if (!grid.InBounds(start) || !grid.InBounds(goal)) return null;

            var open = new List<Vector2Int> { start };
            var closed = new HashSet<Vector2Int>();
            var cameFrom = new Dictionary<Vector2Int, Vector2Int>();
            var gScore = new Dictionary<Vector2Int, int> { [start] = 0 };

            while (open.Count > 0)
            {
                // Pick the open cell with the lowest f (ties: lowest h — closer to the goal).
                int best = 0, bestF = int.MaxValue, bestH = int.MaxValue;
                for (int i = 0; i < open.Count; i++)
                {
                    int h = Heuristic(open[i], goal);
                    int f = gScore[open[i]] + h;
                    if (f < bestF || (f == bestF && h < bestH)) { best = i; bestF = f; bestH = h; }
                }

                Vector2Int current = open[best];
                if (current == goal) return Reconstruct(cameFrom, current);
                open.RemoveAt(best);
                closed.Add(current);

                foreach (var n in grid.Neighbors(current))
                {
                    if (closed.Contains(n)) continue;
                    int tentative = gScore[current] + 1;
                    if (!gScore.TryGetValue(n, out int old) || tentative < old)
                    {
                        gScore[n] = tentative;
                        cameFrom[n] = current;
                        if (!open.Contains(n)) open.Add(n);
                    }
                }
            }
            return null;
        }

        static List<Vector2Int> Reconstruct(Dictionary<Vector2Int, Vector2Int> cameFrom, Vector2Int current)
        {
            var path = new List<Vector2Int> { current };
            while (cameFrom.TryGetValue(current, out var prev)) { current = prev; path.Add(current); }
            path.Reverse();
            return path;
        }
    }
}
