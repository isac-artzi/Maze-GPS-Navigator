// XRSupport — starting/stopping OpenXR at runtime and reading Quest controllers.
// Desktop builds ship with XR *off* at startup; choosing VR on the start screen
// initialises the OpenXR loader on demand (Quest Link / Air Link on Windows).
// Quest (Android) builds initialise XR on startup.
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Management;

namespace MazeNav
{
    public static class XRBoot
    {
        public static bool IsActive
        {
            get
            {
                var s = XRGeneralSettings.Instance;
                return s != null && s.Manager != null && s.Manager.activeLoader != null;
            }
        }

        /// True when this build is running on a standalone headset (Quest).
        public static bool IsHeadsetBuild => Application.platform == RuntimePlatform.Android;

        public static IEnumerator Start(Action<bool> done)
        {
            var manager = XRGeneralSettings.Instance != null ? XRGeneralSettings.Instance.Manager : null;
            if (manager == null) { done(false); yield break; }
            if (manager.activeLoader == null) yield return manager.InitializeLoader();
            if (manager.activeLoader == null) { done(false); yield break; }
            manager.StartSubsystems();
            done(true);
        }

        public static void Stop()
        {
            var manager = XRGeneralSettings.Instance != null ? XRGeneralSettings.Instance.Manager : null;
            if (manager == null || manager.activeLoader == null) return;
            manager.StopSubsystems();
            manager.DeinitializeLoader();
        }

        /// Ask the runtime to measure head height from the floor. Returns false if unsupported.
        public static bool TrySetFloorOrigin()
        {
            var subsystems = new List<XRInputSubsystem>();
            SubsystemManager.GetSubsystems(subsystems);
            bool ok = false;
            foreach (var s in subsystems)
                ok |= s.TrySetTrackingOriginMode(TrackingOriginModeFlags.Floor);
            return ok;
        }
    }

    /// Thin wrapper over UnityEngine.XR.InputDevices for the two Touch controllers.
    public static class XRPad
    {
        static InputDevice Dev(XRNode node) => InputDevices.GetDeviceAtXRNode(node);

        public static Vector2 LeftStick => Stick(XRNode.LeftHand);
        public static Vector2 RightStick => Stick(XRNode.RightHand);

        static Vector2 Stick(XRNode node)
        {
            Dev(node).TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 v);
            return v;
        }

        static bool Btn(XRNode node, InputFeatureUsage<bool> usage)
        {
            Dev(node).TryGetFeatureValue(usage, out bool b);
            return b;
        }

        public static bool A => Btn(XRNode.RightHand, CommonUsages.primaryButton);
        public static bool B => Btn(XRNode.RightHand, CommonUsages.secondaryButton);
        public static bool X => Btn(XRNode.LeftHand, CommonUsages.primaryButton);
        public static bool Y => Btn(XRNode.LeftHand, CommonUsages.secondaryButton);
        public static bool RightTrigger => Btn(XRNode.RightHand, CommonUsages.triggerButton);
        public static bool Menu => Btn(XRNode.LeftHand, CommonUsages.menuButton);
    }

    /// Turns a held button into a one-frame "pressed" event.
    public class ButtonEdge
    {
        bool prev;
        public bool Pressed(bool now) { bool r = now && !prev; prev = now; return r; }
    }
}
