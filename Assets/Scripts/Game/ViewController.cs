// ViewController — switches between first-person and bird's-eye views.
// Desktop: the camera glides between the character's eyes and a point high
//          above the maze centre, looking straight down.
// VR:      you are teleported onto a glass platform high above the maze and
//          look down at your character. (Instant switch — gliding a VR camera
//          causes motion sickness.) Right stick = 45° snap turns.
using UnityEngine;

namespace MazeNav
{
    public class ViewController : MonoBehaviour
    {
        public bool FirstPerson { get; private set; } = true;
        public event System.Action<bool> Changed;

        MazeGame game;
        CameraRig rig;
        PlayerController player;
        Vector3 birdPos;
        Quaternion birdRot;
        float blend = 1f;          // desktop: 1 = first person, 0 = bird's-eye
        float snapCooldown;
        GameObject glass;

        public void Init(MazeGame g, CameraRig r, PlayerController p)
        {
            game = g; rig = r; player = p;
            float cs = MazeGame.CellSize;
            float spanX = g.Grid.Width * cs, spanZ = (g.Grid.Height + 1) * cs;
            Vector3 center = new Vector3(spanX / 2, 0, spanZ / 2);

            if (rig.IsVR)
            {
                float h = Mathf.Max(spanX, spanZ) * 0.55f;
                birdPos = center + new Vector3(0, h, -Mathf.Max(spanX, spanZ) * 0.35f);   // a bit south, so you look down and forward
                glass = GameObject.CreatePrimitive(PrimitiveType.Quad);
                glass.name = "Glass Platform";
                Destroy(glass.GetComponent<Collider>());
                glass.transform.position = birdPos + Vector3.up * 0.01f;
                glass.transform.rotation = Quaternion.Euler(90, 0, 0);
                glass.transform.localScale = new Vector3(3, 3, 1);
                var mat = new Material(Shader.Find("Sprites/Default")) { color = new Color(0.6f, 0.85f, 1f, 0.25f) };
                glass.GetComponent<Renderer>().sharedMaterial = mat;
                glass.SetActive(false);
            }
            else
            {
                float fov = 60f;
                float h = Mathf.Max(spanX, spanZ) * 0.58f / Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad);
                birdPos = center + Vector3.up * h;
                birdRot = Quaternion.Euler(90, 0, 0);
            }
            Apply();
        }

        public void Toggle()
        {
            FirstPerson = !FirstPerson;
            Apply();
            Changed?.Invoke(FirstPerson);
        }

        void Apply()
        {
            player.Body.SetFirstPerson(FirstPerson);
            player.Body.BirdsEyeMarker.SetActive(!FirstPerson);
            if (rig.IsVR)
            {
                glass.SetActive(!FirstPerson);
                if (!FirstPerson) PlaceHeadAt(birdPos);
            }
        }

        /// Move the VR rig so the user's head (not the rig origin) ends up above `worldXZ`.
        void PlaceHeadAt(Vector3 target)
        {
            Vector3 headOffset = rig.Cam.transform.position - rig.transform.position;
            headOffset.y = 0;
            rig.transform.position = new Vector3(target.x, target.y, target.z) - headOffset;
        }

        void LateUpdate()
        {
            if (rig.IsVR)
            {
                if (FirstPerson) PlaceHeadAt(player.transform.position);

                float x = XRPad.RightStick.x;
                snapCooldown -= Time.deltaTime;
                if (Mathf.Abs(x) < 0.3f) snapCooldown = 0;
                else if (Mathf.Abs(x) > 0.7f && snapCooldown <= 0)
                {
                    rig.transform.RotateAround(rig.Cam.transform.position, Vector3.up, 45f * Mathf.Sign(x));
                    snapCooldown = 0.4f;
                }
                return;
            }

            blend = Mathf.MoveTowards(blend, FirstPerson ? 1f : 0f, Time.deltaTime * 1.6f);
            float s = Mathf.SmoothStep(0, 1, blend);
            Vector3 eye = player.Body.EyeAnchor.position;
            Quaternion eyeRot = player.transform.rotation * Quaternion.Euler(6, 0, 0);   // look slightly down
            rig.transform.SetPositionAndRotation(Vector3.Lerp(birdPos, eye, s), Quaternion.Slerp(birdRot, eyeRot, s));
            rig.Cam.fieldOfView = Mathf.Lerp(60f, 72f, s);
        }
    }
}
