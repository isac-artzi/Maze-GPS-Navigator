// CubeView — draws a Cube2Model as 8 small cubes with coloured stickers and
// animates face turns.
// A turn: parent the 4 cubies of that face to a temporary pivot, rotate the
// pivot 90° over a few frames, put the cubies back, then apply the same move to
// the model and snap every cubie to the model's exact position/rotation.
// Snapping from the model each time means rounding errors never pile up.
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MazeNav
{
    public class CubeView : MonoBehaviour
    {
        public const float Spacing = 0.25f;           // edge of one cubie (metres); whole cube = 0.5 m

        public Cube2Model Model { get; } = new Cube2Model();
        public bool Busy => turning || queue.Count > 0;
        public int MovesMade { get; private set; }
        public List<CubeMove> Applied { get; } = new List<CubeMove>();   // everything applied, scramble included
        public event System.Action<CubeMove> Turned;                    // raised for player moves only

        readonly Transform[] cubies = new Transform[8];
        readonly Queue<(CubeMove move, bool player, float seconds)> queue = new Queue<(CubeMove, bool, float)>();
        readonly Dictionary<Face, TextMesh> labels = new Dictionary<Face, TextMesh>();
        Transform highlight;
        bool turning;

        public static readonly Dictionary<Face, Color> StickerColors = new Dictionary<Face, Color>
        {
            [Face.U] = new Color(0.95f, 0.95f, 0.95f),   // white
            [Face.D] = new Color(1f, 0.85f, 0.05f),      // yellow
            [Face.F] = new Color(0.05f, 0.7f, 0.25f),    // green
            [Face.B] = new Color(0.1f, 0.35f, 0.95f),    // blue
            [Face.R] = new Color(0.85f, 0.1f, 0.12f),    // red
            [Face.L] = new Color(1f, 0.5f, 0.05f),       // orange
        };

        public static CubeView Create(Transform parent, Vector3 localPosition)
        {
            var go = new GameObject("Cube 2x2x2");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            var v = go.AddComponent<CubeView>();
            v.Build();
            return v;
        }

        void Build()
        {
            var body = MazeBuilder.MakeMaterial(new Color(0.06f, 0.06f, 0.07f));
            var mats = new Dictionary<Face, Material>();
            foreach (var kv in StickerColors) mats[kv.Key] = MazeBuilder.MakeMaterial(kv.Value);

            foreach (var c in Model.Cubies)
            {
                var t = new GameObject($"Cubie {c.Id}").transform;
                t.SetParent(transform, false);
                MazeBuilder.Cube("Body", t, Vector3.zero, Vector3.one * Spacing * 0.97f, body, false);
                // A sticker on every side of this cubie that faces outward in the solved cube.
                foreach (var f in Cube2Model.AllFaces)
                {
                    Vector3Int n = Cube2Model.Normal(f);
                    if (Cube2Model.Dot(c.Pos, n) <= 0) continue;
                    Vector3 size = Vector3.one * Spacing * 0.82f;
                    size[Axis(n)] = 0.01f;
                    MazeBuilder.Cube($"Sticker {f}", t, (Vector3)n * (Spacing * 0.485f + 0.004f), size, mats[f], false);
                }
                cubies[c.Id] = t;
            }

            // Letters U D L R F B float next to each face (they belong to the cube, not to the cubies).
            foreach (var f in Cube2Model.AllFaces)
            {
                Vector3Int n = Cube2Model.Normal(f);
                var tm = new GameObject($"Label {f}").AddComponent<TextMesh>();
                tm.transform.SetParent(transform, false);
                tm.transform.localPosition = (Vector3)n * Spacing * 1.45f;
                Vector3 upHint = n.y != 0 ? Vector3.forward : Vector3.up;
                tm.transform.localRotation = Quaternion.LookRotation(-(Vector3)n, upHint);
                tm.text = f.ToString();
                tm.fontSize = 64; tm.characterSize = 0.012f;
                tm.anchor = TextAnchor.MiddleCenter; tm.fontStyle = FontStyle.Bold;
                tm.color = new Color(1, 1, 1, 0.85f);
                tm.font = UIFactory.DefaultFont;
                tm.GetComponent<MeshRenderer>().sharedMaterial = UIFactory.DefaultFont.material;
                labels[f] = tm;
            }

            // Translucent square shown over the selected face (used in VR).
            var hq = GameObject.CreatePrimitive(PrimitiveType.Quad);
            DestroyImmediate(hq.GetComponent<Collider>());
            hq.name = "Selection";
            hq.transform.SetParent(transform, false);
            hq.transform.localScale = Vector3.one * Spacing * 2.1f;
            hq.GetComponent<Renderer>().sharedMaterial = new Material(Shader.Find("Sprites/Default")) { color = new Color(1f, 0.95f, 0.2f, 0.35f) };
            highlight = hq.transform;
            highlight.gameObject.SetActive(false);

            Sync();
        }

        static int Axis(Vector3Int n) => n.x != 0 ? 0 : n.y != 0 ? 1 : 2;

        public void SetHighlight(Face? f)
        {
            foreach (var kv in labels) kv.Value.color = f == kv.Key ? new Color(1f, 0.95f, 0.2f) : new Color(1, 1, 1, 0.85f);
            highlight.gameObject.SetActive(f.HasValue);
            if (!f.HasValue) return;
            Vector3 n = (Vector3)Cube2Model.Normal(f.Value);
            highlight.localPosition = n * (Spacing + 0.012f);
            highlight.localRotation = Quaternion.LookRotation(-n, Mathf.Abs(n.y) > 0.5f ? Vector3.forward : Vector3.up);
        }

        /// Queue a turn. player=false for scrambles (not counted, no event).
        public void Enqueue(CubeMove m, bool player = true, float seconds = 0.22f) => queue.Enqueue((m, player, seconds));

        void Update()
        {
            if (!turning && queue.Count > 0) StartCoroutine(Turn(queue.Dequeue()));
        }

        IEnumerator Turn((CubeMove move, bool player, float seconds) job)
        {
            turning = true;
            Vector3 axis = (Vector3)Cube2Model.Normal(job.move.Face);
            float angle = job.move.Prime ? -90f : 90f;

            var pivot = new GameObject("Pivot").transform;
            pivot.SetParent(transform, false);
            foreach (var c in Model.Cubies)
                if (Cube2Model.OnFace(c, job.move.Face)) cubies[c.Id].SetParent(pivot, true);

            for (float t = 0; t < job.seconds; t += Time.deltaTime)
            {
                pivot.localRotation = Quaternion.AngleAxis(angle * Mathf.SmoothStep(0, 1, t / job.seconds), axis);
                yield return null;
            }

            foreach (var t in cubies) t.SetParent(transform, true);
            Destroy(pivot.gameObject);
            Model.Apply(job.move);
            Applied.Add(job.move);
            Sync();
            turning = false;
            if (job.player)
            {
                MovesMade++;
                Turned?.Invoke(job.move);
            }
        }

        /// Only label the faces that point toward the camera. The text shader draws on top of
        /// everything, so a label behind the cube would otherwise show through it, mirrored.
        void LateUpdate()
        {
            var cam = Camera.main;
            if (cam == null) return;
            foreach (var kv in labels)
            {
                Vector3 n = transform.TransformDirection((Vector3)Cube2Model.Normal(kv.Key));
                Vector3 toCam = (cam.transform.position - (transform.position + n * Spacing)).normalized;
                kv.Value.gameObject.SetActive(Vector3.Dot(n, toCam) > 0.1f);
            }
        }

        /// Snap every cubie to the model's exact position and orientation.
        void Sync()
        {
            foreach (var c in Model.Cubies)
            {
                cubies[c.Id].localPosition = (Vector3)c.Pos * (Spacing * 0.5f);
                cubies[c.Id].localRotation = Quaternion.LookRotation((Vector3)c.Z, (Vector3)c.Y);
            }
        }
    }
}
