// MazeGenerator — "recursive backtracker" (depth-first search) maze generation.
// 1. Start with every wall standing.
// 2. Walk randomly to unvisited neighbours, knocking down walls as you go.
// 3. When stuck, back up (pop the stack) until a cell has unvisited neighbours.
// The result is a "perfect" maze (exactly one path between any two cells).
// We then knock down a few extra walls ("braiding") so there are loops —
// that gives the GPS something to recalculate when you take a wrong turn.
using System.Collections.Generic;
using UnityEngine;

namespace MazeNav
{
    public static class MazeGenerator
    {
        public static MazeGrid Generate(int width, int height, int seed, float loopFraction = 0.08f)
        {
            var rng = new System.Random(seed);
            var grid = new MazeGrid(width, height);
            var visited = new bool[width, height];
            var stack = new Stack<Vector2Int>();
            var options = new List<Vector2Int>(4);

            var start = new Vector2Int(0, 0);
            visited[0, 0] = true;
            stack.Push(start);

            while (stack.Count > 0)
            {
                Vector2Int c = stack.Peek();
                options.Clear();
                foreach (var d in MazeGrid.Dirs)
                {
                    Vector2Int n = c + d;
                    if (grid.InBounds(n) && !visited[n.x, n.y]) options.Add(d);
                }
                if (options.Count == 0) { stack.Pop(); continue; }   // dead end: backtrack

                Vector2Int dir = options[rng.Next(options.Count)];
                grid.RemoveWall(c, dir);
                Vector2Int next = c + dir;
                visited[next.x, next.y] = true;
                stack.Push(next);
            }

            // Braiding: remove a few interior walls to create loops.
            int extra = Mathf.RoundToInt(width * height * loopFraction);
            for (int attempt = 0; extra > 0 && attempt < width * height * 10; attempt++)
            {
                var c = new Vector2Int(rng.Next(width), rng.Next(height));
                var d = MazeGrid.Dirs[rng.Next(4)];
                if (grid.InBounds(c + d) && grid.HasWall(c, MazeGrid.DirToWall(d)))
                {
                    grid.RemoveWall(c, d);
                    extra--;
                }
            }

            grid.Start = new Vector2Int(0, 0);
            grid.Exit = new Vector2Int(width - 1, height - 1);
            grid.RemoveWall(grid.Exit, new Vector2Int(0, 1));   // open the outer North wall: the exit gate
            return grid;
        }
    }
}
