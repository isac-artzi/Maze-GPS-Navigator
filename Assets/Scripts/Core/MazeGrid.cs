// MazeGrid — the data model of the maze.
// Each cell stores which of its four walls are still standing, as bit flags.
// Grid coordinates: x grows East, y grows North (y maps to world Z).
using System;
using System.Collections.Generic;
using UnityEngine;

namespace MazeNav
{
    [Flags]
    public enum Wall { None = 0, N = 1, E = 2, S = 4, W = 8, All = N | E | S | W }

    public class MazeGrid
    {
        public readonly int Width, Height;
        public Vector2Int Start, Exit;
        readonly Wall[,] walls;

        /// North, East, South, West — the four moves allowed in a maze.
        public static readonly Vector2Int[] Dirs =
        {
            new Vector2Int(0, 1), new Vector2Int(1, 0), new Vector2Int(0, -1), new Vector2Int(-1, 0)
        };

        public MazeGrid(int width, int height)
        {
            Width = width; Height = height;
            walls = new Wall[width, height];
            for (int x = 0; x < width; x++)
                for (int y = 0; y < height; y++)
                    walls[x, y] = Wall.All;
        }

        public bool InBounds(Vector2Int c) => c.x >= 0 && c.y >= 0 && c.x < Width && c.y < Height;
        public Wall WallsAt(Vector2Int c) => walls[c.x, c.y];
        public bool HasWall(Vector2Int c, Wall w) => (walls[c.x, c.y] & w) != 0;

        public static Wall DirToWall(Vector2Int d)
        {
            if (d.y > 0) return Wall.N;
            if (d.x > 0) return Wall.E;
            if (d.y < 0) return Wall.S;
            return Wall.W;
        }

        public static Wall Opposite(Wall w) => w switch
        {
            Wall.N => Wall.S, Wall.S => Wall.N, Wall.E => Wall.W, Wall.W => Wall.E, _ => Wall.None
        };

        /// Knock down the wall between cell c and its neighbour in direction d (both sides).
        public void RemoveWall(Vector2Int c, Vector2Int d)
        {
            Wall w = DirToWall(d);
            walls[c.x, c.y] &= ~w;
            Vector2Int n = c + d;
            if (InBounds(n)) walls[n.x, n.y] &= ~Opposite(w);
        }

        /// True if you can walk from c one step in direction d without hitting a wall.
        public bool CanMove(Vector2Int c, Vector2Int d) => InBounds(c + d) && !HasWall(c, DirToWall(d));

        public IEnumerable<Vector2Int> Neighbors(Vector2Int c)
        {
            foreach (var d in Dirs)
                if (CanMove(c, d)) yield return c + d;
        }
    }
}
