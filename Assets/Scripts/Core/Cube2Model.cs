// Cube2Model — the logic of a 2x2x2 Rubik's cube (no graphics).
//
// The cube is 8 "cubies" (small cubes). Each cubie remembers
//   * Pos  — which corner it sits in: every coordinate is -1 or +1
//   * X, Y, Z — where its own local x/y/z axes point now (integer vectors)
// A face turn picks the 4 cubies on that face and rotates both their position
// and their axes by 90° around the face's outward normal.
//
// A sticker's colour never changes: it is the colour of the face that sticker
// pointed at when the cube was solved. So to read the colour a cubie shows in
// world direction n, find which local axis now points along n.
//
// Faces (Unity axes, viewer looks along +Z):
//   U = +Y (up)  D = -Y (down)  R = +X (right)  L = -X (left)  F = -Z (front, facing you)  B = +Z (back)
using System.Collections.Generic;
using UnityEngine;

namespace MazeNav
{
    public enum Face { U, D, L, R, F, B }

    public struct CubeMove
    {
        public Face Face;
        public bool Prime;    // false = clockwise, true = counter-clockwise (seen from outside that face)
        public CubeMove(Face f, bool prime) { Face = f; Prime = prime; }
        public CubeMove Inverse => new CubeMove(Face, !Prime);
        public override string ToString() => Face + (Prime ? "'" : "");
    }

    public class Cube2Model
    {
        public class Cubie
        {
            public int Id;
            public Vector3Int Pos, X, Y, Z;
        }

        public readonly Cubie[] Cubies = new Cubie[8];

        public static readonly Face[] AllFaces = { Face.U, Face.D, Face.L, Face.R, Face.F, Face.B };

        public static Vector3Int Normal(Face f) => f switch
        {
            Face.U => new Vector3Int(0, 1, 0),
            Face.D => new Vector3Int(0, -1, 0),
            Face.R => new Vector3Int(1, 0, 0),
            Face.L => new Vector3Int(-1, 0, 0),
            Face.F => new Vector3Int(0, 0, -1),
            _      => new Vector3Int(0, 0, 1),
        };

        public static Face FaceOf(Vector3Int n)
        {
            foreach (var f in AllFaces) if (Normal(f) == n) return f;
            throw new System.ArgumentException($"{n} is not a unit axis");
        }

        public Cube2Model() { Reset(); }

        public void Reset()
        {
            int i = 0;
            for (int x = -1; x <= 1; x += 2)
            for (int y = -1; y <= 1; y += 2)
            for (int z = -1; z <= 1; z += 2)
                Cubies[i] = new Cubie
                {
                    Id = i++, Pos = new Vector3Int(x, y, z),
                    X = Vector3Int.right, Y = Vector3Int.up, Z = new Vector3Int(0, 0, 1)
                };
        }

        public static int Dot(Vector3Int a, Vector3Int b) => a.x * b.x + a.y * b.y + a.z * b.z;

        public static Vector3Int Cross(Vector3Int a, Vector3Int b) =>
            new Vector3Int(a.y * b.z - a.z * b.y, a.z * b.x - a.x * b.z, a.x * b.y - a.y * b.x);

        /// Rotate v by 90° about unit axis n, the same way Unity's Quaternion.AngleAxis(±90, n) does.
        /// Unity is left-handed, so +90° about an axis looks CLOCKWISE when you look from the tip of that axis.
        public static Vector3Int Rot90(Vector3Int v, Vector3Int n, bool clockwise)
        {
            Vector3Int along = n * Dot(n, v);
            Vector3Int c = Cross(n, v);
            return clockwise ? along + c : along - c;
        }

        /// True if the cubie currently belongs to the layer of face f.
        public static bool OnFace(Cubie c, Face f) => Dot(c.Pos, Normal(f)) > 0;

        public void Apply(CubeMove m)
        {
            Vector3Int n = Normal(m.Face);
            bool cw = !m.Prime;
            foreach (var c in Cubies)
            {
                if (!OnFace(c, m.Face)) continue;
                c.Pos = Rot90(c.Pos, n, cw);
                c.X = Rot90(c.X, n, cw);
                c.Y = Rot90(c.Y, n, cw);
                c.Z = Rot90(c.Z, n, cw);
            }
        }

        public void Apply(IEnumerable<CubeMove> moves) { foreach (var m in moves) Apply(m); }

        /// The colour (= the solved-state face) of the sticker cubie c shows toward world direction n.
        public static Face StickerFacing(Cubie c, Vector3Int n)
        {
            if (c.X == n) return Face.R;
            if (c.X == -n) return Face.L;
            if (c.Y == n) return Face.U;
            if (c.Y == -n) return Face.D;
            if (c.Z == n) return Face.B;
            return Face.F;   // c.Z == -n
        }

        /// Solved = every face shows one colour. (The whole cube may be turned around — still solved.)
        public bool IsSolved()
        {
            foreach (var f in AllFaces)
            {
                Vector3Int n = Normal(f);
                Face? first = null;
                foreach (var c in Cubies)
                {
                    if (!OnFace(c, f)) continue;
                    Face s = StickerFacing(c, n);
                    if (first == null) first = s;
                    else if (first != s) return false;
                }
            }
            return true;
        }

        /// On a 2x2x2, turning L is the same as turning R the other way and then rotating the
        /// whole cube. So R, U and F alone can reach every position — official 2x2 scrambles
        /// use only these three faces. It also avoids "fake" scrambles such as L R' (which is
        /// just the solved cube, turned).
        public static readonly Face[] ScrambleFaces = { Face.R, Face.U, Face.F };

        /// Random scramble of `length` quarter turns of R, U, F. Never turns the same face twice
        /// in a row, and the cube is not solved after any prefix of the scramble.
        public static List<CubeMove> Scramble(int length, System.Random rng)
        {
            var probe = new Cube2Model();
            while (true)
            {
                var list = new List<CubeMove>();
                Face? last = null;
                probe.Reset();
                bool ok = true;
                while (list.Count < length)
                {
                    Face f = ScrambleFaces[rng.Next(ScrambleFaces.Length)];
                    if (f == last) continue;
                    var m = new CubeMove(f, rng.Next(2) == 1);
                    list.Add(m);
                    probe.Apply(m);
                    if (probe.IsSolved()) { ok = false; break; }
                    last = f;
                }
                if (ok) return list;
            }
        }
    }
}
