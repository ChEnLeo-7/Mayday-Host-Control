using BepInEx.Configuration;
using BepInEx;
using Il2CppMayday;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppMayday.Flight;
using Il2CppMayday.Airport;
using Il2CppMayday.Players;
using Il2CppMayday.UI;
using System;
using HarmonyLib;
using UnityEngine;

namespace MaydayHostControl
{
    public sealed partial class Plugin
    {
        private enum ThirdPersonViewMode
        {
            Rear,
            Front
        }

        private enum ThirdPersonTargetKind
        {
            Player,
            Plane,
            GroundVehicle
        }

        internal static ConfigEntry<string> ThirdPersonKey;
        internal static ConfigEntry<string> ThirdPersonFreeLookKey;
        internal static ConfigEntry<bool> ThirdPersonFreeLookInvertY;
        internal static ConfigEntry<string> CameraFlyKey;
        internal static ConfigEntry<float> CameraFlySpeed;
        internal static ConfigEntry<bool> CameraFlyPlayerControls;
        internal static ConfigEntry<string> CameraFlyControlsKey;
        internal static ConfigEntry<float> ThirdPersonFreeLookSensitivity;
        private static int ThirdPersonVirtualKey = 0x56; // V
        private static string ThirdPersonDisplay = "V";
        private static int ThirdPersonFreeLookVirtualKey = 0x43; // C
        private static string ThirdPersonFreeLookDisplay = "C";
        private static int CameraFlyVirtualKey = 0x48; // H
        private static string CameraFlyDisplay = "H";
        private static int CameraFlyControlsVirtualKey = 0x4A; // J
        private static string CameraFlyControlsDisplay = "J";
        private static bool CameraFlyControlsKeyDown;
        internal static bool ThirdPersonFreeLookActive;
        private static float ThirdPersonFreeLookYaw;
        private static float ThirdPersonFreeLookPitch;
        internal static bool CameraFlyActive;
        private static bool CameraFlyKeyDown;
        private static bool CameraFlyWasThirdPerson;
        private static PlayerRig CameraFlyRig;
        private static Il2CppMayday.UI.CrosshairWidget CachedCrosshairWidget;
        private static bool CrosshairDebugState;
        private static RectTransform CrosshairRootFallback;
        private static bool CrosshairDebugLogged;
        private static bool CameraFlyPreviousMotorEnabled = true;
        private static Vector3 CameraFlyFrozenPosition;
        private static Quaternion CameraFlyFrozenRotation;
        private static float CameraFlyYaw;
        private static float CameraFlyPitch;
        private static bool ThirdPersonKeyDown;
        private static bool ThirdPersonActive;
        private static ThirdPersonViewMode ThirdPersonMode = ThirdPersonViewMode.Rear;
        private static PlayerRig ThirdPersonRig;
        private static Camera ThirdPersonCamera;
        private static GameObject ThirdPersonCameraObject;
        private static bool ThirdPersonPreviousLocalThirdPerson;
        private static bool ThirdPersonPreviousFirstPerson = true;
        private static bool ThirdPersonPreviousHoldAnchor = true;

        internal static string ThirdPersonShortcutName => ThirdPersonDisplay;

        internal static void InitializeThirdPersonShortcut()
        {
            ThirdPersonVirtualKey = ParseBroadcastKey(ThirdPersonKey != null ? ThirdPersonKey.Value : null);
            ThirdPersonDisplay = GetVirtualKeyDisplayName(ThirdPersonVirtualKey);
            ThirdPersonFreeLookVirtualKey = ParseBroadcastKey(ThirdPersonFreeLookKey != null ? ThirdPersonFreeLookKey.Value : null);
            ThirdPersonFreeLookDisplay = GetVirtualKeyDisplayName(ThirdPersonFreeLookVirtualKey);
            CameraFlyVirtualKey = ParseBroadcastKey(CameraFlyKey != null ? CameraFlyKey.Value : null);
            CameraFlyDisplay = GetVirtualKeyDisplayName(CameraFlyVirtualKey);
            CameraFlyControlsVirtualKey = ParseBroadcastKey(CameraFlyControlsKey != null ? CameraFlyControlsKey.Value : null);
            CameraFlyControlsDisplay = GetVirtualKeyDisplayName(CameraFlyControlsVirtualKey);
        }

        internal static string ThirdPersonFreeLookShortcutName => ThirdPersonFreeLookDisplay;

        internal static void SetThirdPersonFreeLookShortcut(int virtualKey)
        {
            ThirdPersonFreeLookVirtualKey = virtualKey;
            ThirdPersonFreeLookDisplay = GetVirtualKeyDisplayName(virtualKey);
            if (ThirdPersonFreeLookKey != null)
            {
                ThirdPersonFreeLookKey.Value = virtualKey.ToString();
                Settings.Save();
            }
            GuiShortcutCapture = false;
            GuiShortcutCaptureEscapeDown = false;
            GuiShortcutCaptureTarget = 0;
            GuiStatus = Loc.T("Shortcut changed to {0}", ThirdPersonFreeLookDisplay);
            LogEvent("third-person-freelook-shortcut", "key=" + ThirdPersonFreeLookDisplay + " vk=" + virtualKey);
            RebuildContent();
        }

        internal static string CameraFlyShortcutName => CameraFlyDisplay;
        internal static string CameraFlyControlsShortcutName => CameraFlyControlsDisplay;

        internal static void SetCameraFlyShortcut(int virtualKey)
        {
            CameraFlyVirtualKey = virtualKey;
            CameraFlyDisplay = GetVirtualKeyDisplayName(virtualKey);
            if (CameraFlyKey != null)
            {
                CameraFlyKey.Value = virtualKey.ToString();
                Settings.Save();
            }
            GuiShortcutCapture = false;
            GuiShortcutCaptureEscapeDown = false;
            GuiShortcutCaptureTarget = 0;
            GuiStatus = Loc.T("Shortcut changed to {0}", CameraFlyDisplay);
            LogEvent("camera-fly-shortcut", "key=" + CameraFlyDisplay + " vk=" + virtualKey);
            RebuildContent();
        }

        internal static void SetCameraFlyControlsShortcut(int virtualKey)
        {
            CameraFlyControlsVirtualKey = virtualKey;
            CameraFlyControlsDisplay = GetVirtualKeyDisplayName(virtualKey);
            if (CameraFlyControlsKey != null)
            {
                CameraFlyControlsKey.Value = virtualKey.ToString();
                Settings.Save();
            }
            GuiShortcutCapture = false;
            GuiShortcutCaptureEscapeDown = false;
            GuiShortcutCaptureTarget = 0;
            GuiStatus = Loc.T("Shortcut changed to {0}", CameraFlyControlsDisplay);
            LogEvent("camera-fly-controls-shortcut", "key=" + CameraFlyControlsDisplay + " vk=" + virtualKey);
            RebuildContent();
        }

        internal static void SetThirdPersonShortcut(int virtualKey)
        {
            ThirdPersonVirtualKey = virtualKey;
            ThirdPersonDisplay = GetVirtualKeyDisplayName(virtualKey);
            if (ThirdPersonKey != null)
            {
                ThirdPersonKey.Value = virtualKey.ToString();
                Settings.Save();
            }
            GuiShortcutCapture = false;
            GuiShortcutCaptureEscapeDown = false;
            GuiShortcutCaptureTarget = 0;
            GuiStatus = Loc.T("Shortcut changed to {0}", ThirdPersonDisplay);
            LogEvent("third-person-shortcut", "key=" + ThirdPersonDisplay + " vk=" + virtualKey);
            RebuildContent();
        }

        internal static void TickThirdPersonToggle()
        {
            if (!NativeInput.IsGameWindowForeground()) return;
            var down = (NativeInput.GetAsyncKeyState(ThirdPersonVirtualKey) & 0x8000) != 0;
            var pressed = down && !ThirdPersonKeyDown;
            ThirdPersonKeyDown = down;
            if (ThirdPersonActive && !CanUseThirdPerson())
            {
                DisableThirdPersonView();
                return;
            }
            UpdateThirdPersonFreeLookState();
            if (!pressed) return;
            if (GuiShortcutCapture || GuiVisible || BroadcastChatOpen || GuiClosing) return;
            if (!CanUseThirdPerson()) return;

            if (!ThirdPersonActive)
            {
                ThirdPersonMode = ThirdPersonViewMode.Rear;
                EnableThirdPersonView();
                return;
            }

            if (ThirdPersonMode == ThirdPersonViewMode.Rear)
            {
                ThirdPersonMode = ThirdPersonViewMode.Front;
                LogEvent("third-person", "mode=front");
            }
            else
            {
                DisableThirdPersonView();
            }
        }

        // 游戏原生暂停/控制台/遮罩/失焦时，Mod 相机不应继续吃鼠标。
        internal static bool NativeUiBlocksGameplay()
        {
            var root = UIRoot.Instance;
            if (root == null || root.CurrentMode != UIRoot.Mode.Game) return true;
            if (!Application.isFocused) return true;
            if (root.Covered) return true;
            if (root.TopScreen != null) return true;
            if (root.Pause != null && root.Pause.IsOpen) return true;
            if (root.Console != null && root.Console.IsOpen) return true;
            return false;
        }

        private static void UpdateThirdPersonFreeLookState()
        {
            if (!ThirdPersonActive || GuiVisible || BroadcastChatOpen || NativeUiBlocksGameplay())
            {
                ThirdPersonFreeLookActive = false;
                return;
            }
            var held = (NativeInput.GetAsyncKeyState(ThirdPersonFreeLookVirtualKey) & 0x8000) != 0;
            if (!held)
            {
                ThirdPersonFreeLookActive = false;
                return;
            }
            if (ThirdPersonFreeLookActive) return;
            ThirdPersonFreeLookActive = true;
            var camera = ThirdPersonCamera;
            if (camera == null) return;
            var target = GetThirdPersonFreeLookTarget();
            var offset = camera.transform.position - target;
            ThirdPersonFreeLookYaw = Mathf.Atan2(offset.x, offset.z) * Mathf.Rad2Deg;
            ThirdPersonFreeLookPitch = -Mathf.Asin(Mathf.Clamp(offset.y / Mathf.Max(0.01f, offset.magnitude), -1f, 1f)) * Mathf.Rad2Deg;
            LogEvent("third-person-freelook", "on");
        }

        private static Vector3 GetThirdPersonFreeLookTarget()
        {
            var rig = ThirdPersonRig;
            var kind = GetThirdPersonTargetKind(rig, out var target);
            if (kind == ThirdPersonTargetKind.Plane && target != null)
                return target.position + target.up * 1.4f;
            if (kind == ThirdPersonTargetKind.GroundVehicle && target != null)
                return target.position + target.up * 1.0f;
            return rig != null && rig.head != null ? rig.head.position : Vector3.zero;
        }

        private static void ApplyThirdPersonFreeLook()
        {
            var camera = ThirdPersonCamera;
            if (camera == null) return;
            var kind = GetThirdPersonTargetKind(ThirdPersonRig, out _);
            var distance = kind == ThirdPersonTargetKind.Plane ? 32f :
                kind == ThirdPersonTargetKind.GroundVehicle ? 5f : 2.8f;
            var delta = ReadThirdPersonMouseDelta();
            var sensitivity = ThirdPersonFreeLookSensitivity != null ? ThirdPersonFreeLookSensitivity.Value : 1.0f;
            var invertY = ThirdPersonFreeLookInvertY != null && ThirdPersonFreeLookInvertY.Value;
            ThirdPersonFreeLookYaw += delta.x * 0.03f * sensitivity;
            ThirdPersonFreeLookPitch += (invertY ? delta.y : -delta.y) * 0.03f * sensitivity;
            ThirdPersonFreeLookPitch = Mathf.Clamp(ThirdPersonFreeLookPitch, -80f, 80f);
            var target = GetThirdPersonFreeLookTarget();
            var offset = Quaternion.Euler(ThirdPersonFreeLookPitch, ThirdPersonFreeLookYaw, 0f) * new Vector3(0f, 0f, distance);
            var position = target + offset;
            camera.transform.SetPositionAndRotation(position, Quaternion.LookRotation(target - position, Vector3.up));
        }

        private static bool CanUseThirdPerson()
        {
            var uiRoot = UIRoot.Instance;
            if (uiRoot == null || uiRoot.CurrentMode != UIRoot.Mode.Game) return false;
            var rig = PlayerRig.Local;
            if (rig == null || rig.viewCamera == null || rig.head == null) return false;
            var net = rig.Net;
            if (net != null && (net.IsDead || net.IsSpectator)) return false;
            return true;
        }

        private static void EnableThirdPersonView()
        {
            var rig = PlayerRig.Local;
            if (rig == null || rig.viewCamera == null || rig.head == null) return;
            ThirdPersonRig = rig;
            ThirdPersonPreviousLocalThirdPerson = Item.LocalThirdPerson;
            ThirdPersonPreviousFirstPerson = !ThirdPersonPreviousLocalThirdPerson;
            ThirdPersonPreviousHoldAnchor = rig.holdAnchorFP == null || rig.holdAnchorFP.gameObject.activeSelf;
            try
            {
                if (!EnsureThirdPersonCamera(rig)) throw new InvalidOperationException("camera clone failed");

                Item.LocalThirdPerson = true;
                if (rig.holdAnchorFP != null) rig.holdAnchorFP.gameObject.SetActive(false);
                if (rig.Avatar != null) rig.Avatar.SetFirstPerson(false);
                ThirdPersonActive = true;
                GuiStatus = Loc.T("WC.ThirdPersonOn");
                LogEvent("third-person", "enabled mode=" + ThirdPersonMode);
            }
            catch (Exception e)
            {
                ThirdPersonActive = false;
                CleanupThirdPersonCamera(rig);
                try
                {
                    Item.LocalThirdPerson = ThirdPersonPreviousLocalThirdPerson;
                    if (rig.holdAnchorFP != null) rig.holdAnchorFP.gameObject.SetActive(ThirdPersonPreviousHoldAnchor);
                    if (rig.Avatar != null) rig.Avatar.SetFirstPerson(true);
                }
                catch { }
                Log?.LogWarning("[MaydayHostControl] third person enable failed: " + e.Message);
            }
        }

        private static bool EnsureThirdPersonCamera(PlayerRig rig)
        {
            if (ThirdPersonCamera != null) return true;
            if (rig == null || rig.viewCamera == null) return false;
            var clone = UnityEngine.Object.Instantiate(rig.viewCamera.gameObject);
            ThirdPersonCameraObject = clone;
            if (ThirdPersonCameraObject == null) return false;
            ThirdPersonCameraObject.name = "HostThirdPersonCamera";
            ThirdPersonCameraObject.transform.SetParent(null, true);
            ThirdPersonCamera = ThirdPersonCameraObject.GetComponent<Camera>();
            if (ThirdPersonCamera == null)
            {
                UnityEngine.Object.Destroy(ThirdPersonCameraObject);
                ThirdPersonCameraObject = null;
                return false;
            }
            ThirdPersonCamera.enabled = true;
            var listener = ThirdPersonCameraObject.GetComponent<AudioListener>();
            if (listener != null) listener.enabled = false;
            rig.viewCamera.enabled = false;
            return true;
        }

        private static void CleanupThirdPersonCamera(PlayerRig rig)
        {
            if (ThirdPersonCameraObject != null)
            {
                try { UnityEngine.Object.Destroy(ThirdPersonCameraObject); } catch { }
                ThirdPersonCameraObject = null;
            }
            ThirdPersonCamera = null;
            try
            {
                if (rig != null && rig.viewCamera != null)
                    rig.viewCamera.enabled = true;
            }
            catch { }
        }

        private static void DisableThirdPersonView()
        {
            var rig = ThirdPersonRig;
            ThirdPersonActive = false;
            ThirdPersonFreeLookActive = false;
            ThirdPersonMode = ThirdPersonViewMode.Rear;
            try
            {
                CleanupThirdPersonCamera(rig);
                ThirdPersonRig = null;
                if (rig != null)
                {
                    Item.LocalThirdPerson = ThirdPersonPreviousLocalThirdPerson;
                    if (rig.holdAnchorFP != null) rig.holdAnchorFP.gameObject.SetActive(ThirdPersonPreviousHoldAnchor);
                    if (rig.Avatar != null) rig.Avatar.SetFirstPerson(true);
                }
            }
            catch (Exception e)
            {
                Log?.LogWarning("[MaydayHostControl] third person disable failed: " + e.Message);
            }
            LogEvent("third-person", "disabled");
        }

        internal static void TickCameraFlyToggle()
        {
            if (!NativeInput.IsGameWindowForeground()) return;
            var down = (NativeInput.GetAsyncKeyState(CameraFlyVirtualKey) & 0x8000) != 0;
            var pressed = down && !CameraFlyKeyDown;
            CameraFlyKeyDown = down;
            if (CameraFlyActive && !CanUseThirdPerson()) { DisableCameraFly(); return; }
            if (!pressed) return;
            if (GuiShortcutCapture || GuiVisible || BroadcastChatOpen || GuiClosing) return;
            if (!CanUseThirdPerson()) return;
            if (CameraFlyActive) DisableCameraFly(); else EnableCameraFly();
        }

        internal static void TickCameraFlyControlsToggle()
        {
            if (!CameraFlyActive)
            {
                CameraFlyControlsKeyDown = false;
                return;
            }
            var down = (NativeInput.GetAsyncKeyState(CameraFlyControlsVirtualKey) & 0x8000) != 0;
            var pressed = down && !CameraFlyControlsKeyDown;
            CameraFlyControlsKeyDown = down;
            if (!pressed) return;
            var value = !(CameraFlyPlayerControls != null && CameraFlyPlayerControls.Value);
            if (CameraFlyPlayerControls != null)
            {
                CameraFlyPlayerControls.Value = value;
                Settings.Save();
            }
            if (CameraFlyRig != null && CameraFlyRig.Motor != null) CameraFlyRig.Motor.enabled = value && CameraFlyPreviousMotorEnabled;
            if (CameraFlyRig != null)
            {
                CameraFlyFrozenPosition = CameraFlyRig.transform.position;
                CameraFlyFrozenRotation = CameraFlyRig.transform.rotation;
            }
            ShowCameraFlyToast(value ? "WC.CameraFlyControlsOn" : "WC.CameraFlyControlsOff", value ? NotifyKind.Good : NotifyKind.Info);
        }

        private static void ShowCameraFlyToast(string key, NotifyKind kind)
        {
            try { Notify.Local(Loc.T(key), kind, new Il2CppReferenceArray<Il2CppSystem.Object>(0)); } catch { }
        }

        private static void ShowCameraFlyToastText(string text, NotifyKind kind)
        {
            try { Notify.Local(text, kind, new Il2CppReferenceArray<Il2CppSystem.Object>(0)); } catch { }
        }

        private static void EnableCameraFly()
        {
            var rig = PlayerRig.Local;
            if (rig == null || rig.viewCamera == null || rig.head == null) return;
            CameraFlyWasThirdPerson = ThirdPersonActive;
            CameraFlyRig = rig;
            var startCamera = CameraFlyWasThirdPerson && ThirdPersonCamera != null ? ThirdPersonCamera : rig.viewCamera;
            var startPosition = startCamera != null ? startCamera.transform.position : rig.head.position;
            var startRotation = startCamera != null ? startCamera.transform.rotation : rig.head.rotation;
            if (!CameraFlyWasThirdPerson)
            {
                ThirdPersonRig = rig;
                ThirdPersonPreviousLocalThirdPerson = Item.LocalThirdPerson;
                ThirdPersonPreviousHoldAnchor = rig.holdAnchorFP == null || rig.holdAnchorFP.gameObject.activeSelf;
                if (!EnsureThirdPersonCamera(rig))
                {
                    ThirdPersonRig = null;
                    CameraFlyRig = null;
                    return;
                }
                Item.LocalThirdPerson = true;
                if (rig.holdAnchorFP != null) rig.holdAnchorFP.gameObject.SetActive(false);
                if (rig.Avatar != null) rig.Avatar.SetFirstPerson(false);
            }
            CameraFlyFrozenPosition = rig.transform.position;
            CameraFlyFrozenRotation = rig.transform.rotation;
            CameraFlyPreviousMotorEnabled = rig.Motor == null || rig.Motor.enabled;
            if (rig.Motor != null) rig.Motor.enabled = false;
            if (ThirdPersonCamera != null) ThirdPersonCamera.transform.SetPositionAndRotation(startPosition, startRotation);
            var euler = startRotation.eulerAngles;
            CameraFlyYaw = euler.y;
            CameraFlyPitch = euler.x > 180f ? euler.x - 360f : euler.x;
            CameraFlyActive = true;
            GuiStatus = Loc.T("WC.CameraFlyOn");
            var controlsAtEnable = CameraFlyPlayerControls != null && CameraFlyPlayerControls.Value;
            ShowCameraFlyToastText(Loc.T("WC.CameraFlyOn") + " · " + Loc.T(controlsAtEnable ? "WC.CameraFlyControlsOn" : "WC.CameraFlyControlsOff"), NotifyKind.Info);
            if (controlsAtEnable) ShowCameraFlyToastText(Loc.T("WC.CameraFlyControlsLocked"), NotifyKind.Warning);
            LogEvent("camera-fly", "enabled");
        }

        private static void DisableCameraFly()
        {
            CameraFlyActive = false;
            var rig = CameraFlyRig;
            if (rig != null)
            {
                // PlayerLook.Update was suppressed during free camera, so its internal yaw/pitch
                // may be stale. Seed it from the final camera pose before gameplay input resumes.
                try
                {
                    var look = rig.Look;
                    if (look != null) look.SetYawPitch(CameraFlyYaw, CameraFlyPitch);
                }
                catch (Exception e)
                {
                    Log?.LogWarning("[MaydayHostControl] camera fly look restore failed: " + e.Message);
                }

                if (rig.Motor != null) rig.Motor.enabled = CameraFlyPreviousMotorEnabled;
                if (!CameraFlyWasThirdPerson)
                {
                    CleanupThirdPersonCamera(rig);
                    ThirdPersonRig = null;
                    Item.LocalThirdPerson = ThirdPersonPreviousLocalThirdPerson;
                    if (rig.holdAnchorFP != null) rig.holdAnchorFP.gameObject.SetActive(ThirdPersonPreviousHoldAnchor);
                    if (rig.Avatar != null) rig.Avatar.SetFirstPerson(ThirdPersonPreviousFirstPerson);
                }
            }
            CameraFlyRig = null;
            CameraFlyWasThirdPerson = false;
            ShowCameraFlyToast("WC.CameraFlyOff", NotifyKind.Info);
            LogEvent("camera-fly", "disabled");
        }

        private static void ApplyCameraFlyLookOnly()
        {
            var held = (NativeInput.GetAsyncKeyState(ThirdPersonFreeLookVirtualKey) & 0x8000) != 0;
            if (!held) return;
            var camera = ThirdPersonCamera;
            if (camera == null) return;
            var delta = ReadThirdPersonMouseDelta();
            var sensitivity = ThirdPersonFreeLookSensitivity != null ? ThirdPersonFreeLookSensitivity.Value : 1.0f;
            var invertY = ThirdPersonFreeLookInvertY != null && ThirdPersonFreeLookInvertY.Value;
            CameraFlyYaw += delta.x * 0.03f * sensitivity;
            CameraFlyPitch += (invertY ? -delta.y : delta.y) * 0.03f * sensitivity;
            CameraFlyPitch = Mathf.Clamp(CameraFlyPitch, -89f, 89f);
            camera.transform.rotation = Quaternion.Euler(CameraFlyPitch, CameraFlyYaw, 0f);
        }


        private static void ApplyCameraFly()
        {
            if (GuiVisible || BroadcastChatOpen)
            {
                try
                {
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                    Il2CppMayday.GameInput.LookCaptured = false;
                }
                catch { }
                return;
            }
            // GuiClosing only pauses camera movement: it must not re-release the cursor that
            // SetGuiVisible(false) just restored, otherwise the free camera keeps a visible cursor
            // after the fade ends.
            if (GuiClosing || NativeUiBlocksGameplay()) return;

            var camera = ThirdPersonCamera;
            var rig = CameraFlyRig;
            if (camera == null || rig == null) { DisableCameraFly(); return; }
            try
            {
                if (rig.viewCamera != null) rig.viewCamera.enabled = false;
                camera.enabled = true;
                camera.depth = rig.viewCamera.depth + 10f;
            }
            catch { }

            var controls = CameraFlyPlayerControls != null && CameraFlyPlayerControls.Value;
            if (rig.Motor != null)
            {
                var shouldEnable = controls && CameraFlyPreviousMotorEnabled;
                if (rig.Motor.enabled != shouldEnable) rig.Motor.enabled = shouldEnable;
            }

            if (controls)
            {
                ApplyCameraFlyLookOnly();
                return;
            }

            try
            {
                rig.transform.position = CameraFlyFrozenPosition;
                rig.transform.rotation = CameraFlyFrozenRotation;
            }
            catch { }

            var delta = ReadThirdPersonMouseDelta();
            CameraFlyYaw += delta.x * 0.06f;
            CameraFlyPitch -= delta.y * 0.06f;
            CameraFlyPitch = Mathf.Clamp(CameraFlyPitch, -89f, 89f);

            var forward = (NativeInput.GetAsyncKeyState(0x57) & 0x8000) != 0 ? 1f : 0f;
            forward -= (NativeInput.GetAsyncKeyState(0x53) & 0x8000) != 0 ? 1f : 0f;
            var right = (NativeInput.GetAsyncKeyState(0x44) & 0x8000) != 0 ? 1f : 0f;
            right -= (NativeInput.GetAsyncKeyState(0x41) & 0x8000) != 0 ? 1f : 0f;
            var up = (NativeInput.GetAsyncKeyState(0x20) & 0x8000) != 0 ? 1f : 0f;
            up -= (NativeInput.GetAsyncKeyState(0x11) & 0x8000) != 0 ? 1f : 0f;
            var rotation = Quaternion.Euler(CameraFlyPitch, CameraFlyYaw, 0f);
            var move = rotation * new Vector3(right, 0f, forward) + Vector3.up * up;
            if (move.sqrMagnitude > 0.001f)
            {
                var speed = CameraFlySpeed != null ? Mathf.Clamp(CameraFlySpeed.Value, 1f, 50f) : 8f;
                if ((NativeInput.GetAsyncKeyState(0x10) & 0x8000) != 0) speed *= 3f;
                camera.transform.position += move.normalized * speed * Time.deltaTime;
            }
            camera.transform.rotation = rotation;
        }


        private static Vector2 ReadThirdPersonMouseDelta()
        {
            var delta = Vector2.zero;
            try
            {
                var mouse = UnityEngine.InputSystem.Mouse.current;
                if (mouse != null) delta = mouse.delta.ReadValue();
            }
            catch { }
            if (delta == Vector2.zero)
            {
                try { delta = new Vector2(UnityEngine.Input.GetAxisRaw("Mouse X"), UnityEngine.Input.GetAxisRaw("Mouse Y")) * 3f; } catch { }
            }
            return Vector2.ClampMagnitude(delta, 80f);
        }

        internal static void CaptureCurrentCameraPose()
        {
            try
            {
                var camera = ThirdPersonCamera;
                var flight = PlaneFlight.Instance;
                if (camera == null || flight == null)
                {
                    GuiStatus = Loc.T("WC.CaptureCameraMissing");
                    return;
                }
                var plane = flight.transform;
                var localPosition = plane.InverseTransformPoint(camera.transform.position);
                var localRotation = Quaternion.Inverse(plane.rotation) * camera.transform.rotation;
                var localEuler = localRotation.eulerAngles;
                var localForward = plane.InverseTransformDirection(camera.transform.forward);
                var text = string.Format(System.Globalization.CultureInfo.InvariantCulture,
                    "worldPos={0:R},{1:R},{2:R}\\nworldEuler={3:R},{4:R},{5:R}\\nlocalPos={6:R},{7:R},{8:R}\\nlocalEuler={9:R},{10:R},{11:R}\\nlocalForward={12:R},{13:R},{14:R}\\n",
                    camera.transform.position.x, camera.transform.position.y, camera.transform.position.z,
                    camera.transform.eulerAngles.x, camera.transform.eulerAngles.y, camera.transform.eulerAngles.z,
                    localPosition.x, localPosition.y, localPosition.z,
                    localEuler.x, localEuler.y, localEuler.z,
                    localForward.x, localForward.y, localForward.z);
                var path = System.IO.Path.Combine(Paths.ConfigPath, "pilot-camera-pose.txt");
                System.IO.Directory.CreateDirectory(Paths.ConfigPath);
                System.IO.File.WriteAllText(path, text);
                LogEvent("pilot-camera-capture", text.Replace("\\n", " "));
                GuiStatus = Loc.T("WC.CaptureCameraSaved");
                RebuildContent();
            }
            catch (Exception e)
            {
                Log?.LogWarning("[MaydayHostControl] capture camera pose failed: " + e.Message);
            }
        }

        internal static bool HideGameCrosshair
        {
            get
            {
                if (CameraFlyActive)
                {
                    if (CameraFlyPlayerControls == null || !CameraFlyPlayerControls.Value) return false;
                    return !IsThirdPersonFreeLookKeyHeld();
                }
                return ThirdPersonActive || ThirdPersonFreeLookActive;
            }
        }

        private static bool IsThirdPersonFreeLookKeyHeld()
        {
            return ThirdPersonFreeLookVirtualKey > 0 && (NativeInput.GetAsyncKeyState(ThirdPersonFreeLookVirtualKey) & 0x8000) != 0;
        }

        internal static Camera ThirdPersonCameraClone => ThirdPersonCamera;

        internal static Camera ActiveViewCamera
        {
            get
            {
                if (ThirdPersonCamera != null) return ThirdPersonCamera;
                var rig = ThirdPersonRig != null ? ThirdPersonRig : PlayerRig.Local;
                return rig != null ? rig.viewCamera : null;
            }
        }

        private static void UpdateCrosshairRoot()
        {
            if (CachedCrosshairWidget == null)
            {
                try
                {
                    var screen = Il2CppMayday.UI.HudScreen.Current;
                    if (screen != null)
                    {
                        var field = typeof(Il2CppMayday.UI.HudScreen).GetField("crosshair", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
                        CachedCrosshairWidget = field != null ? field.GetValue(screen) as Il2CppMayday.UI.CrosshairWidget : null;
                    }
                }
                catch { }
            }
            if (CrosshairRootFallback == null && CachedCrosshairWidget != null) CrosshairRootFallback = CachedCrosshairWidget.Root;
            if (CrosshairRootFallback == null)
            {
                try
                {
                    var named = UnityEngine.GameObject.Find("Crosshair");
                    if (named != null) CrosshairRootFallback = named.GetComponent<RectTransform>();
                    if (CrosshairRootFallback == null)
                    {
                        var namedWidget = UnityEngine.GameObject.Find("CrosshairWidget");
                        if (namedWidget != null) CrosshairRootFallback = namedWidget.GetComponent<RectTransform>();
                    }
                }
                catch { }
            }
            if (CrosshairRootFallback != null)
            {
                var hide = HideGameCrosshair;
                CrosshairRootFallback.gameObject.SetActive(!hide);
                if (CrosshairDebugState != hide)
                {
                    CrosshairDebugState = hide;
                    LogEvent("crosshair-debug", "hide=" + hide + " root=" + CrosshairRootFallback.name);
                }
            }
            else if (!CrosshairDebugLogged)
            {
                CrosshairDebugLogged = true;
                LogEvent("crosshair-debug", "rootMissing");
            }
        }

        internal static void ReleaseFreeCameraInput()
        {
            if (!CameraFlyActive) return;
            try
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                Il2CppMayday.GameInput.LookCaptured = false;
            }
            catch { }
        }

        internal static void TickThirdPersonCameraLate()
        {
            UpdateCrosshairRoot();
            if (CameraFlyActive) { ApplyCameraFly(); return; }
            if (!ThirdPersonActive) return;
            if (NativeUiBlocksGameplay()) return;
            var rig = ThirdPersonRig;
            if (rig == null || ThirdPersonCamera == null || rig.head == null)
            {
                DisableThirdPersonView();
                return;
            }

            try
            {
                if (rig.viewCamera != null) rig.viewCamera.enabled = false;
                if (ThirdPersonCamera != null)
                {
                    ThirdPersonCamera.enabled = true;
                    ThirdPersonCamera.depth = rig.viewCamera.depth + 10f;
                }
            }
            catch { }

            if (ThirdPersonFreeLookActive)
            {
                ApplyThirdPersonFreeLook();
                return;
            }

            var targetKind = GetThirdPersonTargetKind(rig, out var target);
            if (targetKind == ThirdPersonTargetKind.Plane)
            {
                if (ThirdPersonMode == ThirdPersonViewMode.Front)
                {
                    if (ApplyPilotView(true)) return;
                }
                else
                {
                    ApplyPilotRearChase();
                    return;
                }
            }
            else if (targetKind == ThirdPersonTargetKind.GroundVehicle && target != null)
            {
                if (ThirdPersonMode == ThirdPersonViewMode.Front)
                    ApplyVehicleView(target, true);
                else
                    ApplyVehicleView(target, false);
                return;
            }

            if (ThirdPersonMode == ThirdPersonViewMode.Front)
                ApplyFrontView(rig);
            else
                ApplyRearView(rig);
        }

        private static ThirdPersonTargetKind GetThirdPersonTargetKind(PlayerRig rig, out Transform target)
        {
            target = null;
            try
            {
                var seating = rig != null ? rig.Seating : null;
                if (seating == null || !seating.IsSeated) return ThirdPersonTargetKind.Player;
                if (seating.IsPilotSeat)
                {
                    var flight = PlaneFlight.Instance;
                    target = flight != null ? flight.transform : null;
                    return target != null ? ThirdPersonTargetKind.Plane : ThirdPersonTargetKind.Player;
                }

                var seat = seating.Seat;
                var current = seat != null ? seat.SitAnchor : null;
                for (var depth = 0; current != null && depth < 8; depth++, current = current.parent)
                {
                    var vehicle = current.GetComponent<DrivableVehicle>();
                    if (vehicle != null)
                    {
                        target = vehicle.transform;
                        return ThirdPersonTargetKind.GroundVehicle;
                    }
                }
            }
            catch { }
            return ThirdPersonTargetKind.Player;
        }

        private static bool IsSeated(PlayerRig rig)
        {
            try
            {
                var seating = rig.Seating;
                return seating != null && seating.IsSeated;
            }
            catch { return false; }
        }

        private static bool IsPilotSeated(PlayerRig rig)
        {
            try
            {
                var seating = rig.Seating;
                return seating != null && seating.IsSeated && seating.IsPilotSeat;
            }
            catch
            {
                return false;
            }
        }

        private static bool ApplyPilotView(bool front)
        {
            try
            {
                var flight = PlaneFlight.Instance;
                if (flight == null) return false;
                var plane = flight.transform;
                var center = plane.position + plane.up * 1.4f;
                var position = front
                    ? center + plane.forward * 28f + plane.up * 9f
                    : center - plane.forward * 42f + plane.up * 22f;
                var lookPoint = front ? center : center + plane.up * 2f;
                var rotation = Quaternion.LookRotation(lookPoint - position, plane.up);
                ThirdPersonCamera.transform.SetPositionAndRotation(position, rotation);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static void ApplyPilotRearChase()
        {
            var flight = PlaneFlight.Instance;
            if (flight == null) return;
            var plane = flight.transform;
            // Pose captured in-game with the free camera: behind 27.58m, above 17.98m,
            // pitched down 16.36 degrees, expressed in the aircraft local frame.
            var localPosition = new Vector3(-0.000134f, 17.980566f, -27.576778f);
            var localRotation = Quaternion.Euler(16.364405f, 0f, 0f);
            ThirdPersonCamera.transform.SetPositionAndRotation(
                plane.TransformPoint(localPosition),
                plane.rotation * localRotation);
        }

        private static void ApplyVehicleView(Transform vehicle, bool front)
        {
            var target = vehicle.position + vehicle.up * 1.0f;
            var forward = vehicle.forward;
            var distance = front ? 4.5f : 5.5f;
            var height = front ? 1.8f : 2.2f;
            var direction = front ? forward : -forward;
            var allowed = distance;
            try
            {
                var hits = Physics.RaycastAll(target, direction, distance, ~0, QueryTriggerInteraction.Ignore);
                for (var i = 0; i < hits.Length; i++)
                {
                    var collider = hits[i].collider;
                    if (collider == null || collider.transform.IsChildOf(vehicle)) continue;
                    if (hits[i].distance < allowed) allowed = hits[i].distance;
                }
            }
            catch { }

            var clipped = allowed < distance ? Math.Max(0.8f, allowed - 0.25f) : distance;
            var position = target + direction * clipped + vehicle.up * height;
            var lookPoint = target + vehicle.up * 0.35f;
            ThirdPersonCamera.transform.SetPositionAndRotation(
                position,
                Quaternion.LookRotation(lookPoint - position, vehicle.up));
        }

        private static void ApplyCockpitRearView(PlayerRig rig)
        {
            var head = rig.head;
            var rotation = rig.viewCamera.transform.rotation;
            var forward = rotation * Vector3.forward;
            var up = rotation * Vector3.up;
            var position = head.position - forward * 2.0f + up * 0.7f;
            var lookPoint = head.position + forward * 4f + up * 0.1f;
            ThirdPersonCamera.transform.SetPositionAndRotation(position, Quaternion.LookRotation(lookPoint - position, Vector3.up));
        }
        private static void ApplyRearView(PlayerRig rig)
        {
            var head = rig.head;
            var rotation = rig.viewCamera.transform.rotation;
            var forward = rotation * Vector3.forward;
            var up = rotation * Vector3.up;
            const float distance = 2.8f;
            const float height = 0.25f;

            var allowed = distance;
            try
            {
                var hits = Physics.RaycastAll(head.position, -forward, distance, ~0, QueryTriggerInteraction.Ignore);
                for (var i = 0; i < hits.Length; i++)
                {
                    var collider = hits[i].collider;
                    if (collider == null) continue;
                    if (collider.transform.IsChildOf(rig.transform)) continue;
                    if (hits[i].distance < allowed) allowed = hits[i].distance;
                }
            }
            catch { }

            var clipped = allowed < distance ? Math.Max(0.45f, allowed - 0.2f) : distance;
            ThirdPersonCamera.transform.SetPositionAndRotation(head.position - forward * clipped + up * height, rotation);
        }

        private static void ApplyFrontView(PlayerRig rig)
        {
            var head = rig.head;
            var rotation = rig.viewCamera.transform.rotation;
            var forward = rotation * Vector3.forward;
            var up = rotation * Vector3.up;
            const float distance = 2.6f;

            var allowed = distance;
            try
            {
                var hits = Physics.RaycastAll(head.position, forward, distance, ~0, QueryTriggerInteraction.Ignore);
                for (var i = 0; i < hits.Length; i++)
                {
                    var collider = hits[i].collider;
                    if (collider == null) continue;
                    if (collider.transform.IsChildOf(rig.transform)) continue;
                    if (hits[i].distance < allowed) allowed = hits[i].distance;
                }
            }
            catch { }

            var clipped = allowed < distance ? Math.Max(0.45f, allowed - 0.2f) : distance;
            var position = head.position + forward * clipped + up * 0.15f;
            var rotationToHead = Quaternion.LookRotation(head.position - position, Vector3.up);
            ThirdPersonCamera.transform.SetPositionAndRotation(position, rotationToHead);
        }
    [HarmonyPatch(typeof(Il2CppMayday.Players.PlayerLook), "Update")]
    internal static class ThirdPersonFreeLookPatch
    {
        private static bool Prefix()
        {
            return !Plugin.ThirdPersonFreeLookActive && !Plugin.CameraFlyActive;
        }
    }

    [HarmonyPatch(typeof(Il2CppMayday.GameInput), "get_Blocked")]
    internal static class CameraFlyInputBlockPatch
    {
        private static bool Prefix(ref bool __result)
        {
            if (!Plugin.CameraFlyActive) return true;
            if (Plugin.CameraFlyPlayerControls != null && Plugin.CameraFlyPlayerControls.Value) return true;
            __result = true;
            return false;
        }
    }
    [HarmonyPatch(typeof(Il2CppMayday.GameInput), "get_Look")]
    internal static class CameraFlyLookBlockPatch
    {
        private static bool Prefix(ref Vector2 __result)
        {
            if (!Plugin.CameraFlyActive || !Plugin.GuiVisible) return true;
            __result = Vector2.zero;
            return false;
        }
    }

    [HarmonyPatch(typeof(Il2CppMayday.GameInput), "get_Move")]
    internal static class CameraFlyMoveBlockPatch
    {
        private static bool Prefix(ref Vector2 __result)
        {
            if (!Plugin.CameraFlyActive || !Plugin.GuiVisible) return true;
            __result = Vector2.zero;
            return false;
        }
    }

}
}
