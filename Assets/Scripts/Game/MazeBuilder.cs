// MazeBuilder — turns a MazeGrid into 3D geometry.
// Cell (x, y) occupies world X [x*cs, (x+1)*cs] and Z [y*cs, (y+1)*cs].
// Every cell draws only its South and West walls; the last row adds North,
// the last column adds East — so no wall is ever built twice.
using UnityEngine;

namespace MazeNav
{
    public static class MazeBuilder
    {
        static Material baseMaterial, emissiveMaterial;

        /// A tinted copy of Resources/Materials/MazeLit (or MazeLitEmissive).
        /// Why material *assets*? A player build only contains shaders that some asset in the
        /// build references. Materials created purely in code would render magenta in a build.
        public static Material MakeMaterial(Color color, Texture2D tex = null, Vector2? tiling = null, bool emissive = false)
        {
            if (baseMaterial == null)
            {
                baseMaterial = Resources.Load<Material>("Materials/MazeLit");
                emissiveMaterial = Resources.Load<Material>("Materials/MazeLitEmissive");
                if (baseMaterial == null)   // project not set up yet: fall back to the primitive's material (Editor only)
                {
                    var tmp = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    baseMaterial = tmp.GetComponent<Renderer>().sharedMaterial;
                    Object.DestroyImmediate(tmp);
                }
                if (emissiveMaterial == null) emissiveMaterial = baseMaterial;
            }
            var m = new Material(emissive ? emissiveMaterial : baseMaterial) { color = color };
            if (tex != null) { m.mainTexture = tex; m.mainTextureScale = tiling ?? Vector2.one; }
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0.1f);
            return m;
        }

        public static GameObject Cube(string name, Transform parent, Vector3 center, Vector3 size, Material mat, bool collider = true)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = center;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        public static Transform Build(MazeGrid g, float cs, float wallH, float wallT, out Transform exitGate)
        {
            var root = new GameObject("Maze").transform;
            var wallsRoot = new GameObject("Walls").transform;
            wallsRoot.SetParent(root, false);

            // Textures give depth cues — important in first person and essential in VR.
            var wallMat = MakeMaterial(new Color(0.85f, 0.78f, 0.68f), BrickTexture(), new Vector2(2, 1));
            var floorMat = MakeMaterial(Color.white, TileTexture(), new Vector2(g.Width, g.Height + 1));

            float w = g.Width * cs, d = g.Height * cs;
            // Floor extends one extra row beyond the exit so you can walk out through the gate.
            Cube("Floor", root, new Vector3(w / 2, -0.1f, (d + cs) / 2), new Vector3(w, 0.2f, d + cs), floorMat);

            for (int x = 0; x < g.Width; x++)
            for (int y = 0; y < g.Height; y++)
            {
                var c = new Vector2Int(x, y);
                if (g.HasWall(c, Wall.S)) HWall(x, y);
                if (g.HasWall(c, Wall.W)) VWall(x, y);
                if (y == g.Height - 1 && g.HasWall(c, Wall.N)) HWall(x, y + 1);
                if (x == g.Width - 1 && g.HasWall(c, Wall.E)) VWall(x + 1, y);
            }

            void HWall(int x, int z) => Cube($"Wall H {x},{z}", wallsRoot,
                new Vector3((x + 0.5f) * cs, wallH / 2, z * cs), new Vector3(cs + wallT, wallH, wallT), wallMat);
            void VWall(int x, int z) => Cube($"Wall V {x},{z}", wallsRoot,
                new Vector3(x * cs, wallH / 2, (z + 0.5f) * cs), new Vector3(wallT, wallH, cs + wallT), wallMat);

            // Low walls around the exit courtyard so nobody walks off the edge of the world.
            var courtMat = MakeMaterial(new Color(0.35f, 0.6f, 0.4f));
            float ex = (g.Exit.x + 0.5f) * cs;
            Cube("Court N", root, new Vector3(ex, 0.5f, d + cs), new Vector3(cs, 1f, wallT), courtMat);
            Cube("Court W", root, new Vector3(ex - cs / 2, 0.5f, d + cs / 2), new Vector3(wallT, 1f, cs), courtMat);
            Cube("Court E", root, new Vector3(ex + cs / 2, 0.5f, d + cs / 2), new Vector3(wallT, 1f, cs), courtMat);

            // Start pad (blue) and exit gate (green arch + glowing pad).
            var startMat = MakeMaterial(new Color(0.2f, 0.45f, 1f));
            Cube("Start Pad", root, new Vector3((g.Start.x + 0.5f) * cs, 0.01f, (g.Start.y + 0.5f) * cs), new Vector3(cs * 0.6f, 0.02f, cs * 0.6f), startMat, false);

            var gateMat = MakeMaterial(new Color(0.1f, 1f, 0.35f), emissive: true);
            if (gateMat.HasProperty("_EmissionColor")) gateMat.SetColor("_EmissionColor", new Color(0.05f, 0.6f, 0.2f));
            exitGate = new GameObject("Exit Gate").transform;
            exitGate.SetParent(root, false);
            exitGate.localPosition = new Vector3(ex, 0, d);
            Cube("Pillar L", exitGate, new Vector3(-cs / 2 + 0.2f, wallH / 2 + 0.3f, 0), new Vector3(0.4f, wallH + 0.6f, 0.4f), gateMat);
            Cube("Pillar R", exitGate, new Vector3(cs / 2 - 0.2f, wallH / 2 + 0.3f, 0), new Vector3(0.4f, wallH + 0.6f, 0.4f), gateMat);
            Cube("Lintel", exitGate, new Vector3(0, wallH + 0.5f, 0), new Vector3(cs, 0.4f, 0.4f), gateMat);
            Cube("Exit Pad", exitGate, new Vector3(0, 0.01f, cs / 2), new Vector3(cs * 0.8f, 0.02f, cs * 0.8f), gateMat, false);
            var sign = new GameObject("EXIT sign").AddComponent<TextMesh>();
            sign.transform.SetParent(exitGate, false);
            sign.transform.localPosition = new Vector3(0, wallH + 1.3f, 0);
            sign.text = "EXIT"; sign.characterSize = 0.25f; sign.fontSize = 60;
            sign.anchor = TextAnchor.MiddleCenter; sign.color = new Color(0.2f, 1f, 0.4f);
            sign.font = UIFactory.DefaultFont;
            sign.GetComponent<MeshRenderer>().sharedMaterial = UIFactory.DefaultFont.material;
            // Two-sided: a back copy so the sign reads from inside the maze too.
            var back = Object.Instantiate(sign.gameObject, exitGate);
            back.transform.localRotation = Quaternion.Euler(0, 180, 0);
            back.transform.localPosition = sign.transform.localPosition;

            // One draw call per material instead of hundreds (matters on Quest).
            StaticBatchingUtility.Combine(wallsRoot.gameObject);
            return root;
        }

        static Texture2D BrickTexture()
        {
            const int n = 128;
            var t = new Texture2D(n, n, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear };
            var mortar = new Color(0.62f, 0.6f, 0.56f);
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                int row = y / 16;
                int xx = (x + (row % 2) * 16) % 32;
                bool isMortar = y % 16 < 2 || xx < 2;
                float noise = Mathf.PerlinNoise(x * 0.15f, y * 0.15f) * 0.15f;
                var brick = new Color(0.78f - noise, 0.45f - noise, 0.35f - noise);
                t.SetPixel(x, y, isMortar ? mortar : brick);
            }
            t.Apply();
            return t;
        }

        static Texture2D TileTexture()
        {
            const int n = 64;
            var t = new Texture2D(n, n, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat };
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                bool line = x < 2 || y < 2;
                float v = 0.55f + Mathf.PerlinNoise(x * 0.2f, y * 0.2f) * 0.08f;
                t.SetPixel(x, y, line ? new Color(0.35f, 0.35f, 0.38f) : new Color(v, v, v * 0.95f));
            }
            t.Apply();
            return t;
        }
    }
}
