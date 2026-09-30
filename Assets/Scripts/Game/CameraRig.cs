// CameraRig — one camera setup for both platforms.
// Desktop: a plain Camera.
// VR: Rig root (on the floor) > Camera Offset > Camera with a TrackedPoseDriver
//     that copies the headset pose onto the camera every frame.
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;

namespace MazeNav
{
    public class CameraRig : MonoBehaviour
    {
        public Camera Cam { get; private set; }
        public Transform Offset { get; private set; }
        public bool IsVR { get; private set; }

        public static CameraRig Create(bool vr)
        {
            var root = new GameObject(vr ? "XR Rig" : "Camera Rig");
            var rig = root.AddComponent<CameraRig>();
            rig.IsVR = vr;

            rig.Offset = new GameObject("Camera Offset").transform;
            rig.Offset.SetParent(root.transform, false);

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            camGo.transform.SetParent(rig.Offset, false);
            rig.Cam = camGo.AddComponent<Camera>();
            rig.Cam.nearClipPlane = 0.05f;
            rig.Cam.farClipPlane = 500f;
            rig.Cam.clearFlags = CameraClearFlags.SolidColor;
            rig.Cam.backgroundColor = new Color(0.55f, 0.70f, 0.85f);
            camGo.AddComponent<AudioListener>();

            if (vr)
            {
                var tpd = camGo.AddComponent<TrackedPoseDriver>();
                var pos = new InputAction("HeadPosition", binding: "<XRHMD>/centerEyePosition", expectedControlType: "Vector3");
                var rot = new InputAction("HeadRotation", binding: "<XRHMD>/centerEyeRotation", expectedControlType: "Quaternion");
                pos.Enable(); rot.Enable();
                tpd.positionInput = new InputActionProperty(pos);
                tpd.rotationInput = new InputActionProperty(rot);
                tpd.trackingType = TrackedPoseDriver.TrackingType.RotationAndPosition;

                // Quest reports head height above the floor; if the runtime can't, fake a standing height.
                if (!XRBoot.TrySetFloorOrigin()) rig.Offset.localPosition = new Vector3(0, 1.6f, 0);
            }
            return rig;
        }

        /// Yaw-only forward direction of the viewer (ignores looking up/down).
        public Vector3 FlatForward
        {
            get
            {
                Vector3 f = Cam.transform.forward; f.y = 0;
                return f.sqrMagnitude < 1e-4f ? transform.forward : f.normalized;
            }
        }
    }
}
