// Edit-mode tests for the 2x2x2 cube logic.
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace MazeNav.Tests
{
    public class Cube2ModelTests
    {
        [Test]
        public void NewCubeIsSolved() => Assert.IsTrue(new Cube2Model().IsSolved());

        [Test]
        public void Rot90MatchesUnityQuaternions()
        {
            var axes = new[] { Vector3Int.right, Vector3Int.left, Vector3Int.up, Vector3Int.down, new Vector3Int(0, 0, 1), new Vector3Int(0, 0, -1) };
            foreach (var n in axes)
            foreach (var v in axes)
            foreach (bool cw in new[] { true, false })
            {
                Vector3 expected = Quaternion.AngleAxis(cw ? 90 : -90, n) * (Vector3)v;
                Vector3Int got = Cube2Model.Rot90(v, n, cw);
                Assert.Less(Vector3.Distance(expected, got), 1e-4f, $"n={n} v={v} cw={cw}");
            }
        }

        [Test]
        public void EveryQuarterTurnHasOrderFour()
        {
            foreach (var f in Cube2Model.AllFaces)
            {
                var c = new Cube2Model();
                c.Apply(new CubeMove(f, false));
                Assert.IsFalse(c.IsSolved(), $"{f} once");
                c.Apply(new CubeMove(f, false));
                Assert.IsFalse(c.IsSolved(), $"{f} twice");
                c.Apply(new CubeMove(f, false));
                c.Apply(new CubeMove(f, false));
                Assert.IsTrue(c.IsSolved(), $"{f} four times");
            }
        }

        [Test]
        public void MoveThenInverseIsIdentity()
        {
            foreach (var f in Cube2Model.AllFaces)
            {
                var c = new Cube2Model();
                var m = new CubeMove(f, false);
                c.Apply(m);
                c.Apply(m.Inverse);
                Assert.IsTrue(c.IsSolved());
            }
        }

        [Test]
        public void UndoingAScrambleSolvesIt([Values(1, 2, 3, 4, 5)] int seed)
        {
            var scramble = Cube2Model.Scramble(12, new System.Random(seed));
            var c = new Cube2Model();
            c.Apply(scramble);
            Assert.IsFalse(c.IsSolved());
            for (int i = scramble.Count - 1; i >= 0; i--) c.Apply(scramble[i].Inverse);
            Assert.IsTrue(c.IsSolved());
        }

        [Test]
        public void TurningOppositeLayersTogetherLooksSolved()
        {
            // L and R' turn the two layers the same way = the whole cube rotated: still solved.
            var c = new Cube2Model();
            c.Apply(new CubeMove(Face.L, false));
            Assert.IsFalse(c.IsSolved());
            c.Apply(new CubeMove(Face.R, true));
            Assert.IsTrue(c.IsSolved());
        }

        [Test]
        public void ClockwiseMeansClockwiseFromOutside()
        {
            // U clockwise, seen from above: the front-top cubies move to the left side.
            var c = new Cube2Model();
            var frontTopRight = c.Cubies[System.Array.FindIndex(c.Cubies, q => q.Pos == new Vector3Int(1, 1, -1))];
            c.Apply(new CubeMove(Face.U, false));
            Assert.AreEqual(new Vector3Int(-1, 1, -1), frontTopRight.Pos);
        }

        [Test]
        public void UndoingOnlyPartOfAScrambleDoesNotSolveIt([Values(3, 5, 9)] int length)
        {
            // Every scramble needs all of its moves undone (none of them cancel out).
            var rng = new System.Random(length);
            for (int k = 0; k < 30; k++)
            {
                var s = Cube2Model.Scramble(length, rng);
                var c = new Cube2Model();
                c.Apply(s);
                for (int i = s.Count - 1; i >= 1; i--)
                {
                    c.Apply(s[i].Inverse);
                    Assert.IsFalse(c.IsSolved(), $"solved after undoing {s.Count - i} of {s.Count}");
                }
            }
        }

        [Test]
        public void ScrambleNeverRepeatsAFaceOrEndsSolved()
        {
            var rng = new System.Random(9);
            for (int k = 0; k < 50; k++)
            {
                List<CubeMove> s = Cube2Model.Scramble(3, rng);
                Assert.AreEqual(3, s.Count);
                for (int i = 1; i < s.Count; i++) Assert.AreNotEqual(s[i - 1].Face, s[i].Face);
                var c = new Cube2Model();
                foreach (var m in s)
                {
                    Assert.Contains(m.Face, Cube2Model.ScrambleFaces);
                    c.Apply(m);
                    Assert.IsFalse(c.IsSolved(), "no prefix of a scramble may be solved");
                }
            }
        }
    }
}
