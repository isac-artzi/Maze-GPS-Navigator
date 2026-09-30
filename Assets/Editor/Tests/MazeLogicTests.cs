// Edit-mode tests for the pure logic (no scene needed).
// Run: Window > General > Test Runner > EditMode > Run All
// or:  Unity -batchmode -projectPath . -runTests -testPlatform EditMode -testResults results.xml
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace MazeNav.Tests
{
    public class MazeLogicTests
    {
        static readonly int[] Seeds = { 1, 7, 42, 1234, 99999 };

        [Test]
        public void EveryCellIsReachable([ValueSource(nameof(Seeds))] int seed)
        {
            var g = MazeGenerator.Generate(12, 12, seed);
            Assert.AreEqual(12 * 12, Bfs(g, g.Start).Count);
        }

        [Test]
        public void WallsAreSymmetric([ValueSource(nameof(Seeds))] int seed)
        {
            var g = MazeGenerator.Generate(10, 8, seed);
            for (int x = 0; x < g.Width; x++)
            for (int y = 0; y < g.Height; y++)
            {
                var c = new Vector2Int(x, y);
                foreach (var d in MazeGrid.Dirs)
                {
                    var n = c + d;
                    if (!g.InBounds(n)) continue;
                    Assert.AreEqual(g.HasWall(c, MazeGrid.DirToWall(d)), g.HasWall(n, MazeGrid.DirToWall(-d)), $"{c} -> {n}");
                }
            }
        }

        [Test]
        public void ExitGateIsOpen()
        {
            var g = MazeGenerator.Generate(8, 8, 3);
            Assert.IsFalse(g.HasWall(g.Exit, Wall.N));
        }

        [Test]
        public void SameSeedSameMaze()
        {
            var a = MazeGenerator.Generate(9, 9, 77);
            var b = MazeGenerator.Generate(9, 9, 77);
            for (int x = 0; x < 9; x++)
            for (int y = 0; y < 9; y++)
                Assert.AreEqual(a.WallsAt(new Vector2Int(x, y)), b.WallsAt(new Vector2Int(x, y)));
        }

        [Test]
        public void AStarFindsAShortestLegalPath([ValueSource(nameof(Seeds))] int seed)
        {
            var g = MazeGenerator.Generate(14, 14, seed);
            var path = AStarPathfinder.FindPath(g, g.Start, g.Exit);
            Assert.NotNull(path);
            Assert.AreEqual(g.Start, path[0]);
            Assert.AreEqual(g.Exit, path[path.Count - 1]);
            for (int i = 0; i < path.Count - 1; i++)
                Assert.IsTrue(g.CanMove(path[i], path[i + 1] - path[i]), $"illegal step {path[i]} -> {path[i + 1]}");
            Assert.AreEqual(Bfs(g, g.Start)[g.Exit], path.Count - 1, "A* path should be as short as BFS");
        }

        [Test]
        public void TurnDirections()
        {
            var N = Vector2Int.up; var E = Vector2Int.right; var S = Vector2Int.down; var W = Vector2Int.left;
            Assert.AreEqual(Maneuver.Left, RoutePlanner.TurnDirection(N, W));
            Assert.AreEqual(Maneuver.Right, RoutePlanner.TurnDirection(N, E));
            Assert.AreEqual(Maneuver.Left, RoutePlanner.TurnDirection(E, N));
            Assert.AreEqual(Maneuver.Right, RoutePlanner.TurnDirection(S, W));
            Assert.AreEqual(Maneuver.Straight, RoutePlanner.TurnDirection(E, E));
            Assert.AreEqual(Maneuver.UTurn, RoutePlanner.TurnDirection(E, W));
        }

        [Test]
        public void HeadingCorrection()
        {
            Assert.AreEqual(Maneuver.Straight, RoutePlanner.HeadingCorrection(Vector2Int.up, new Vector2(0.2f, 1)));
            Assert.AreEqual(Maneuver.Left, RoutePlanner.HeadingCorrection(Vector2Int.left, Vector2.up));
            Assert.AreEqual(Maneuver.Right, RoutePlanner.HeadingCorrection(Vector2Int.right, Vector2.up));
            Assert.AreEqual(Maneuver.UTurn, RoutePlanner.HeadingCorrection(Vector2Int.down, Vector2.up));
        }

        [Test]
        public void NextStepFindsFirstTurnAndDistance()
        {
            // North, North, then East: the turn is at (0,2), a right turn, 8 m away from (0,0) with 4 m cells.
            var path = new List<Vector2Int> { new(0, 0), new(0, 1), new(0, 2), new(1, 2) };
            var step = RoutePlanner.NextStep(path, Vector2.zero, 4f);
            Assert.AreEqual(Maneuver.Right, step.Action);
            Assert.AreEqual(new Vector2Int(0, 2), step.Cell);
            Assert.AreEqual(8f, step.Meters, 1e-3f);
            Assert.AreEqual(10, RoutePlanner.DistanceBucket(step.Meters));

            var straight = new List<Vector2Int> { new(0, 0), new(0, 1), new(0, 2) };
            Assert.AreEqual(Maneuver.Arrive, RoutePlanner.NextStep(straight, Vector2.zero, 4f).Action);
        }

        [Test]
        public void EveryPhraseKeyTheGpsCanSayExists()
        {
            foreach (var d in new[] { "left", "right" })
                for (int m = 5; m <= 50; m += 5)
                    Assert.IsTrue(VoicePhrases.All.ContainsKey($"in_{m}_{d}"));
            foreach (var k in new[] { "route_calculated", "recalculating", "uturn", "turn_left_now", "turn_right_now", "exit_ahead", "arrived" })
                Assert.IsTrue(VoicePhrases.All.ContainsKey(k), k);
        }

        static Dictionary<Vector2Int, int> Bfs(MazeGrid g, Vector2Int s)
        {
            var dist = new Dictionary<Vector2Int, int> { [s] = 0 };
            var q = new Queue<Vector2Int>();
            q.Enqueue(s);
            while (q.Count > 0)
            {
                var c = q.Dequeue();
                foreach (var n in g.Neighbors(c).Where(n => !dist.ContainsKey(n)))
                {
                    dist[n] = dist[c] + 1;
                    q.Enqueue(n);
                }
            }
            return dist;
        }
    }
}
