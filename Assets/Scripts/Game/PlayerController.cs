// PlayerController — moves the humanoid with a CharacterController.
// Desktop ("tank" controls, like the arrow keys in a car game):
//   Up/Down (or W/S) walk forward/back, Left/Right (or A/D) turn.
// VR, first person: left stick walks where you are looking; the body follows your head.
// VR, bird's-eye: left stick steers the little figure below you, relative to your view.
// Autopilot (T): the character follows the GPS route by itself — handy for demos and tests.
using UnityEngine;
using UnityEngine.InputSystem;

namespace MazeNav
{
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        public float WalkSpeed = 3.2f;     // m/s
        public float TurnSpeed = 110f;     // deg/s
        public bool InputEnabled = true;
        public bool Autopilot;

        public BlockyHumanoid Body { get; private set; }
        MazeGame game;
        CharacterController cc;
        float verticalVelocity;
        Vector2Int centeredCell = new Vector2Int(-1, -1);

        public static PlayerController Create(MazeGame game, Vector3 position, Quaternion rotation)
        {
            var go = new GameObject("Player");
            go.transform.SetPositionAndRotation(position, rotation);
            var cc = go.AddComponent<CharacterController>();
            cc.height = 1.8f; cc.radius = 0.35f; cc.center = new Vector3(0, 0.9f, 0);
            cc.stepOffset = 0.3f; cc.skinWidth = 0.04f;
            var p = go.AddComponent<PlayerController>();
            p.game = game;
            p.cc = cc;
            p.Body = BlockyHumanoid.Create(go.transform);
            return p;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            float fwd = 0, strafe = 0, turn = 0;

            if (Autopilot) AutoDrive(ref fwd, ref turn);
            else if (InputEnabled)
            {
                var k = Keyboard.current;
                if (k != null)
                {
                    if (k.upArrowKey.isPressed || k.wKey.isPressed) fwd += 1;
                    if (k.downArrowKey.isPressed || k.sKey.isPressed) fwd -= 1;
                    if (k.leftArrowKey.isPressed || k.aKey.isPressed) turn -= 1;
                    if (k.rightArrowKey.isPressed || k.dKey.isPressed) turn += 1;
                }
                if (GameSettings.Mode == RunMode.VR)
                {
                    Vector2 s = XRPad.LeftStick;
                    if (s.magnitude > 0.15f) { fwd += s.y; strafe += s.x; }
                }
            }

            Vector3 move;
            bool vrSteering = GameSettings.Mode == RunMode.VR && !Autopilot && game.Rig != null;
            if (vrSteering)
            {
                Vector3 f = game.Rig.FlatForward;
                Vector3 r = Vector3.Cross(Vector3.up, f);
                move = f * fwd + r * strafe;
                if (game.View.FirstPerson)
                {
                    transform.rotation = Quaternion.LookRotation(f);                  // body follows head
                    if (turn != 0) game.Rig.transform.Rotate(0, turn * TurnSpeed * dt, 0);
                }
                else if (move.sqrMagnitude > 0.01f)
                {
                    transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(move), 10f * dt);
                }
            }
            else
            {
                transform.Rotate(0, turn * TurnSpeed * dt, 0);
                move = transform.forward * fwd + transform.right * strafe;
            }
            if (move.magnitude > 1f) move.Normalize();

            verticalVelocity = cc.isGrounded ? -1f : verticalVelocity - 9.81f * dt;
            cc.Move((move * WalkSpeed + Vector3.up * verticalVelocity) * dt);

            float anim = move.magnitude * (fwd < 0 ? -1f : 1f);
            if (Mathf.Abs(turn) > 0.1f && anim == 0) anim = 0.4f;   // shuffle feet while turning on the spot
            Body.Animate(anim, dt);
        }

        /// Steer toward the centre of the current cell, then the next cell on the route.
        /// Going through cell centres keeps the character away from wall corners.
        void AutoDrive(ref float fwd, ref float turn)
        {
            var path = game.Gps.Path;
            if (path == null || path.Count == 0) return;

            Vector3 target;
            if (centeredCell != path[0] && Flat(game.CellCenter(path[0]) - transform.position).magnitude < 0.4f)
                centeredCell = path[0];
            if (centeredCell != path[0]) target = game.CellCenter(path[0]);
            else if (path.Count >= 2) target = game.CellCenter(path[1]);
            else target = game.CellCenter(path[0]) + Vector3.forward * MazeGame.CellSize;   // out through the gate

            Vector3 to = Flat(target - transform.position);
            float angle = Vector3.SignedAngle(transform.forward, to, Vector3.up);
            turn = Mathf.Clamp(angle / 25f, -1f, 1f);
            fwd = Mathf.Abs(angle) < 30f ? 1f : 0f;
        }

        static Vector3 Flat(Vector3 v) { v.y = 0; return v; }
    }
}
