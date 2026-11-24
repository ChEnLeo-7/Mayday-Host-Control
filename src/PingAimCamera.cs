using System;
using HarmonyLib;
using Il2CppMayday.Players;
using UnityEngine;

namespace MaydayHostControl
{
    public sealed partial class Plugin
    {
        // 只在 Mod 自己的相机（第三人称/自由相机）里生效；第一人称和幽灵不参与，保持原版行为。
        internal static bool UsingModCamera
        {
            get
            {
                var mod = ThirdPersonCameraClone;
                if (mod == null || !mod.isActiveAndEnabled) return false;
                if (!ThirdPersonActive && !CameraFlyActive) return false;
                var rig = PlayerRig.Local;
                var net = rig != null ? rig.Net : null;
                return net == null || (!net.IsDead && !net.IsSpectator);
            }
        }

        // 不碰 PingAim.Resolve 的参数，也不重写发送流程：
        // 在 SendPing 执行期间把本地玩家的 ViewTransform 临时指向 Mod 相机，
        // 原版就会用相机的位置/朝向做射线解算；其余冷却、击晕、音效、标签、贴地全部原样保留。
        [HarmonyPatch(typeof(Il2CppMayday.Players.PlayerEmotes), "SendPing")]
        internal static class PingCameraTransformPatch
        {
            [ThreadStatic] private static Transform SavedViewTransform;
            [ThreadStatic] private static bool Swapped;

            private static void Prefix(Il2CppMayday.PlayerNet net)
            {
                SavedViewTransform = null;
                Swapped = false;
                if (net == null || !UsingModCamera) return;
                var cam = ThirdPersonCameraClone;
                if (cam == null) return;
                try
                {
                    SavedViewTransform = net.ViewTransform;
                    net.ViewTransform = cam.transform;
                    Swapped = true;
                    LogEvent("ping-camera", "cam=" + cam.name + " swapped=true");
                }
                catch (Exception e)
                {
                    Log?.LogWarning("[MaydayHostControl] ping camera swap failed: " + e.Message);
                    Restore(net);
                }
            }

            private static void Postfix(Il2CppMayday.PlayerNet net, Il2CppMayday.Players.PingAim.Kind __result, bool send)
            {
                LogEvent("ping-send", "kind=" + __result + " send=" + send + " swapped=" + Swapped);
                Restore(net);
            }

            private static void Finalizer(Il2CppMayday.PlayerNet net)
            {
                Restore(net);
            }

            private static void Restore(Il2CppMayday.PlayerNet net)
            {
                if (!Swapped) return;
                Swapped = false;
                try
                {
                    if (net != null) net.ViewTransform = SavedViewTransform;
                }
                catch { }
                SavedViewTransform = null;
            }
        }

        // --- 临时诊断日志：用来确认 ping 走到哪一步，定位完可删 ---
        [HarmonyPatch(typeof(Il2CppMayday.Players.PlayerSync), "OwnerPing")]
        internal static class PingOwnerProbe
        {
            private static void Prefix(Vector3 scenePos, string label)
            {
                LogEvent("ping-owner", "label=" + (label ?? "") + " scene=" + scenePos.x.ToString("0.##") + "," + scenePos.y.ToString("0.##") + "," + scenePos.z.ToString("0.##"));
            }
        }

        [HarmonyPatch(typeof(Il2CppMayday.Players.PlayerSync), "RpcPing")]
        internal static class PingRpcProbe
        {
            private static void Prefix(string label)
            {
                LogEvent("ping-rpc", "label=" + (label ?? ""));
            }
        }

        [HarmonyPatch(typeof(Il2CppMayday.UI.PingWidget), "Add")]
        internal static class PingWidgetProbe
        {
            private static void Prefix(string label)
            {
                LogEvent("ping-widget", "label=" + (label ?? ""));
            }
        }

        private static bool CameraFlyPingDown;
        private static float NextCameraFlyPingAt;

        // 同一状态下把原版 HandlePing 关掉，避免“原版也发一次 + 手动再发一次”；
        // 只有在输入确实被屏蔽（Blocked=true）时才关，Blocked=false 时仍交给原版。
        [HarmonyPatch(typeof(Il2CppMayday.Players.PlayerEmotes), "HandlePing")]
        internal static class CameraFlyHandlePingGate
        {
            private static bool Prefix()
            {
                if (!CameraFlyActive) return true;
                if (CameraFlyPlayerControls != null && CameraFlyPlayerControls.Value) return true;
                return !Il2CppMayday.GameInput.Blocked;
            }
        }

        // 灵魂出窍 + 角色控制关闭时，游戏会屏蔽输入，原版 HandlePing 根本不会跑；
        // 这里直接读中键，用自由相机射线解算并发送，走和原版一样的 OwnerPing。
        internal static void TickCameraFlyPing()
        {
            try
            {
                if (!CameraFlyActive)
                {
                    CameraFlyPingDown = false;
                    return;
                }
                if (!NativeInput.IsGameWindowForeground())
                {
                    CameraFlyPingDown = false;
                    return;
                }
                if (CameraFlyPlayerControls != null && CameraFlyPlayerControls.Value)
                {
                    // 角色控制开着时交给原版，避免双发
                    CameraFlyPingDown = false;
                    return;
                }
                if (!Il2CppMayday.GameInput.Blocked)
                {
                    // 游戏输入没被屏蔽时也交给原版
                    CameraFlyPingDown = false;
                    return;
                }
                if (GuiVisible || BroadcastChatOpen || GuiShortcutCapture || GuiClosing || NativeUiBlocksGameplay())
                {
                    CameraFlyPingDown = false;
                    return;
                }

                var down = (NativeInput.GetAsyncKeyState(0x04) & 0x8000) != 0 || Il2CppMayday.GameInput.PingDown;
                var pressed = down && !CameraFlyPingDown;
                CameraFlyPingDown = down;
                if (!pressed || Time.time < NextCameraFlyPingAt) return;

                var rig = PlayerRig.Local;
                var net = rig != null ? rig.Net : null;
                var cam = ThirdPersonCameraClone;
                if (rig == null || net == null || cam == null) return;
                if (net.IsDead || net.IsSpectator || net.IsStunned) return;
                var sync = rig.Sync;
                if (sync == null) return;

                var held = net.Inventory != null ? net.Inventory.Held : null;
                var kind = Il2CppMayday.Players.PingAim.Resolve(cam.transform.position, cam.transform.forward, 60f, rig.transform, held, out var point, out var label);
                if (kind == Il2CppMayday.Players.PingAim.Kind.None) return;

                sync.OwnerPing(point, label);
                NextCameraFlyPingAt = Time.time + 0.35f;
                LogEvent("ping-camera", "cam=" + cam.name + " manual=true kind=" + kind);
            }
            catch (Exception e)
            {
                CameraFlyPingDown = false;
                Log?.LogWarning("[MaydayHostControl] camera fly ping failed: " + e.Message);
            }
        }
    }
}
