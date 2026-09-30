// BlockyHumanoid — a humanoid built from cubes, with a procedural walk cycle.
// Limbs hang from pivot objects at the hips and shoulders; swinging the
// pivots with a sine wave is all a walk cycle needs at this level of detail.
// Swap in a rigged Mixamo character later by replacing this component.
using UnityEngine;
using UnityEngine.Rendering;

namespace MazeNav
{
    public class BlockyHumanoid : MonoBehaviour
    {
        public const float EyeHeight = 1.62f;

        public Transform Head { get; private set; }
        public Transform EyeAnchor { get; private set; }
        public GameObject BirdsEyeMarker { get; private set; }

        Transform legL, legR, armL, armR;
        Renderer[] headRenderers;
        float phase;

        public static BlockyHumanoid Create(Transform parent)
        {
            var go = new GameObject("Humanoid");
            go.transform.SetParent(parent, false);
            var h = go.AddComponent<BlockyHumanoid>();
            h.Build();
            return h;
        }

        void Build()
        {
            var skin = MazeBuilder.MakeMaterial(new Color(0.93f, 0.76f, 0.6f));
            var shirt = MazeBuilder.MakeMaterial(new Color(0.95f, 0.45f, 0.15f));
            var pants = MazeBuilder.MakeMaterial(new Color(0.15f, 0.2f, 0.4f));
            var dark = MazeBuilder.MakeMaterial(new Color(0.1f, 0.1f, 0.12f));

            legL = Pivot("Hip L", new Vector3(-0.12f, 0.85f, 0));
            legR = Pivot("Hip R", new Vector3(0.12f, 0.85f, 0));
            MazeBuilder.Cube("Leg", legL, new Vector3(0, -0.42f, 0), new Vector3(0.18f, 0.84f, 0.2f), pants, false);
            MazeBuilder.Cube("Leg", legR, new Vector3(0, -0.42f, 0), new Vector3(0.18f, 0.84f, 0.2f), pants, false);

            MazeBuilder.Cube("Torso", transform, new Vector3(0, 1.17f, 0), new Vector3(0.5f, 0.64f, 0.28f), shirt, false);

            armL = Pivot("Shoulder L", new Vector3(-0.33f, 1.45f, 0));
            armR = Pivot("Shoulder R", new Vector3(0.33f, 1.45f, 0));
            MazeBuilder.Cube("Arm", armL, new Vector3(0, -0.3f, 0), new Vector3(0.14f, 0.62f, 0.14f), shirt, false);
            MazeBuilder.Cube("Arm", armR, new Vector3(0, -0.3f, 0), new Vector3(0.14f, 0.62f, 0.14f), shirt, false);

            Head = Pivot("Head", new Vector3(0, 1.62f, 0));
            MazeBuilder.Cube("Skull", Head, Vector3.zero, new Vector3(0.3f, 0.32f, 0.3f), skin, false);
            MazeBuilder.Cube("Eye L", Head, new Vector3(-0.07f, 0.03f, 0.151f), new Vector3(0.05f, 0.05f, 0.01f), dark, false);
            MazeBuilder.Cube("Eye R", Head, new Vector3(0.07f, 0.03f, 0.151f), new Vector3(0.05f, 0.05f, 0.01f), dark, false);
            headRenderers = Head.GetComponentsInChildren<Renderer>();

            EyeAnchor = new GameObject("Eye Anchor").transform;
            EyeAnchor.SetParent(transform, false);
            EyeAnchor.localPosition = new Vector3(0, EyeHeight, 0.12f);

            // A big yellow disc + pointer, shown only from above so you can find yourself.
            var marker = MazeBuilder.MakeMaterial(new Color(1f, 0.9f, 0.1f));
            BirdsEyeMarker = new GameObject("Birds-eye Marker");
            BirdsEyeMarker.transform.SetParent(transform, false);
            var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            DestroyImmediate(disc.GetComponent<Collider>());
            disc.transform.SetParent(BirdsEyeMarker.transform, false);
            disc.transform.localPosition = new Vector3(0, 3.4f, 0);
            disc.transform.localScale = new Vector3(1.6f, 0.05f, 1.6f);
            disc.GetComponent<Renderer>().sharedMaterial = marker;
            MazeBuilder.Cube("Pointer", BirdsEyeMarker.transform, new Vector3(0, 3.42f, 1.1f), new Vector3(0.35f, 0.08f, 1.2f), marker, false);
            BirdsEyeMarker.SetActive(false);
        }

        Transform Pivot(string name, Vector3 localPos)
        {
            var t = new GameObject(name).transform;
            t.SetParent(transform, false);
            t.localPosition = localPos;
            return t;
        }

        /// First person: hide the head from the camera (but keep its shadow).
        public void SetFirstPerson(bool fp)
        {
            foreach (var r in headRenderers)
                r.shadowCastingMode = fp ? ShadowCastingMode.ShadowsOnly : ShadowCastingMode.On;
        }

        /// speed01: 0 = idle, 1 = full walking speed. Negative = walking backwards.
        public void Animate(float speed01, float dt)
        {
            phase += dt * 8f * Mathf.Abs(speed01);
            float swing = Mathf.Sin(phase) * 35f * Mathf.Clamp01(Mathf.Abs(speed01));
            legL.localRotation = Quaternion.Euler(swing, 0, 0);
            legR.localRotation = Quaternion.Euler(-swing, 0, 0);
            armL.localRotation = Quaternion.Euler(-swing * 0.8f, 0, 0);
            armR.localRotation = Quaternion.Euler(swing * 0.8f, 0, 0);
        }
    }
}
