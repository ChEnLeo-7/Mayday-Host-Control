using BepInEx.Configuration;
using HarmonyLib;
using Il2CppMayday;
using Il2CppMayday.Flight;
using Il2CppMirror;
using System;
using UnityEngine;

namespace MaydayHostControl
{
    public sealed partial class Plugin
    {
        internal static ConfigEntry<bool> ForceAutopilot;
        internal static ConfigEntry<string> ForceAutopilotKey;
        private static int ForceAutopilotVirtualKey = 0x4C; // L
        private static string ForceAutopilotDisplay = "L";
        private static bool ForceAutopilotKeyDown;
        private static float NextForceAutopilotAttempt;
        private static bool ForceApGroundToastShown;

        // Set only for the synchronous EngageAp call. The narrow prefixes below use it
        // to bypass AP-owned locks (seat, AP power, AP fault blocker) without touching
        // engine/fuel/control-surface physics.
        internal static bool ForceApEngageContext;
        internal static bool ForceApPowerBypass;

        private static bool ForceApActive =>
            ForceAutopilot != null && ForceAutopilot.Value && NetworkServer.activeHost;

        internal static void InitializeForceAutopilotShortcut()
        {
            ForceAutopilotVirtualKey = ParseBroadcastKey(ForceAutopilotKey != null ? ForceAutopilotKey.Value : null);
            ForceAutopilotDisplay = GetVirtualKeyDisplayName(ForceAutopilotVirtualKey);
        }

        internal static string ForceAutopilotShortcutName => ForceAutopilotDisplay;

        internal static void SetForceAutopilotShortcut(int virtualKey)
        {
            ForceAutopilotVirtualKey = virtualKey;
            ForceAutopilotDisplay = GetVirtualKeyDisplayName(virtualKey);
            if (ForceAutopilotKey != null)
            {
                ForceAutopilotKey.Value = virtualKey.ToString();
                Settings.Save();
            }
            GuiShortcutCapture = false;
            GuiShortcutCaptureEscapeDown = false;
            GuiShortcutCaptureTarget = 0;
            GuiStatus = Loc.T("Shortcut changed to {0}", ForceAutopilotDisplay);
            LogEvent("force-autopilot-shortcut", "key=" + ForceAutopilotDisplay + " vk=" + virtualKey);
            RebuildContent();
        }

        internal static void TickForceAutopilotKey()
        {
            if (!NativeInput.IsGameWindowForeground()) return;
            var down = (NativeInput.GetAsyncKeyState(ForceAutopilotVirtualKey) & 0x8000) != 0;
            var pressed = down && !ForceAutopilotKeyDown;
            ForceAutopilotKeyDown = down;
            if (!pressed || GuiShortcutCapture || GuiVisible || BroadcastChatOpen || GuiClosing) return;
            if (ForceAutopilot == null) return;
            // Force AP is server-authoritative. Do not let a room member toggle a
            // setting that can never do anything on their client.
            if (!NetworkServer.activeHost) return;
            ForceAutopilot.Value = !ForceAutopilot.Value;
            Settings.Save();
            ForceApEngageContext = false;
            if (!ForceAutopilot.Value)
            {
                ForceApPowerBypass = false;
                ForceApGroundToastShown = false;
            }
            try
            {
                Notify.Local(Loc.T(ForceAutopilot.Value ? "WC.ForceAutopilotOn" : "WC.ForceAutopilotOff"), NotifyKind.Info,
                    new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<Il2CppSystem.Object>(0));
            }
            catch { }
            LogEvent("force-autopilot-toggle", "on=" + ForceAutopilot.Value);
        }

        internal static void TickForceAutopilot()
        {
            ForceApEngageContext = false;
            ForceApPowerBypass = false;
            if (ForceAutopilot == null || !ForceAutopilot.Value)
            {
                ForceApGroundToastShown = false;
                return;
            }
            if (!NetworkServer.activeHost) return;

            var flight = PlaneFlight.Instance;
            if (flight == null) return;
            if (flight.crashed) return;

            if (flight.OnGroundNow)
            {
                // Ground is deliberately outside the bypass set. Do not force-engage,
                // and do not keep retrying silently: tell the host once per ground period.
                if (!ForceApGroundToastShown)
                {
                    ForceApGroundToastShown = true;
                    try
                    {
                        Notify.Local(Loc.T("WC.ForceAutopilotGround"), NotifyKind.Info,
                            new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<Il2CppSystem.Object>(0));
                    }
                    catch { }
                    LogEvent("force-autopilot", "ground=true");
                }
                return;
            }

            ForceApGroundToastShown = false;
            ForceApPowerBypass = true;
            if (flight.ApEngaged)
            {
                if (flight.NetworkapFault != 0) flight.NetworkapFault = 0;
                return;
            }
            if (Time.unscaledTime < NextForceAutopilotAttempt) return;
            NextForceAutopilotAttempt = Time.unscaledTime + 0.5f;

            try
            {
                // AP fault/trip is an AP-owned latch. Clearing the network fault lets the
                // normal EngageAp path run; it does not repair engine/fuel/control damage.
                flight.NetworkapFault = 0;
                var by = PlayerNet.Local;
                if (by == null) return;
                var engaged = flight.ServerEngageAp(by);
                LogEvent("force-autopilot", "engaged=" + engaged + " blocker=" + (flight.ApEngageBlocker ?? "-") + " ground=" + flight.OnGroundNow);
            }
            catch (Exception e)
            {
                Log?.LogWarning("[MaydayHostControl] force autopilot failed: " + e.Message);
            }
        }

        [HarmonyPatch(typeof(Il2CppMayday.Flight.PlaneFlight), "EngageAp")]
        internal static class ForceApEngageScopePatch
        {
            [HarmonyPrefix]
            private static void Prefix(Il2CppMayday.Flight.PlaneFlight __instance)
            {
                ForceApEngageContext = false;
                if (!ForceApActive || __instance == null || __instance.crashed || __instance.OnGroundNow) return;
                ForceApEngageContext = true;
            }

            [HarmonyPostfix]
            private static void Postfix()
            {
                ForceApEngageContext = false;
            }
        }

        [HarmonyPatch(typeof(Il2CppMayday.Flight.PlaneFlight), "FindPilot")]
        internal static class ForceApFindPilotPatch
        {
            [HarmonyPrefix]
            private static bool Prefix(ref Il2CppMayday.PlayerNet __result)
            {
                if (!ForceApEngageContext) return true;
                var local = Il2CppMayday.PlayerNet.Local;
                if (local == null) return true;
                __result = local;
                return false;
            }
        }

        [HarmonyPatch(typeof(Il2CppMayday.CockpitIntrusion), "get_Seated")]
        internal static class ForceApSeatedPatch
        {
            [HarmonyPrefix]
            private static bool Prefix(ref bool __result)
            {
                if (!ForceApEngageContext) return true;
                __result = true;
                return false;
            }
        }

        [HarmonyPatch(typeof(Il2CppMayday.PowerBus), "IsPowered")]
        internal static class ForceApPowerPatch
        {
            [HarmonyPrefix]
            private static bool Prefix(Il2CppMayday.PowerBusId bus, ref bool __result)
            {
                if (!ForceApPowerBypass) return true;
                if (bus != Il2CppMayday.PowerBusId.Autopilot && bus != Il2CppMayday.PowerBusId.Avionics) return true;
                __result = true;
                return false;
            }
        }

        [HarmonyPatch(typeof(Il2CppMayday.Flight.PlaneFlight), "get_ApEngageBlocker")]
        internal static class ForceApBlockerPatch
        {
            [HarmonyPrefix]
            private static bool Prefix(ref string __result)
            {
                if (!ForceApEngageContext) return true;
                __result = null;
                return false;
            }
        }

        [HarmonyPatch(typeof(Il2CppMayday.Flight.PlaneFlight), "ApPilotOverride")]
        internal static class ForceApPilotOverridePatch
        {
            [HarmonyPostfix]
            private static void Postfix(bool __result)
            {
                if (!__result || !ForceApActive) return;
                // The pilot always wins. Disarm force so the 0.5s retry cannot
                // immediately fight the pilot and re-engage the AP.
                ForceAutopilot.Value = false;
                Settings.Save();
                ForceApEngageContext = false;
                ForceApPowerBypass = false;
                ForceApGroundToastShown = false;
                try
                {
                    Notify.Local(Loc.T("WC.ForceAutopilotOff"), NotifyKind.Info,
                        new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<Il2CppSystem.Object>(0));
                }
                catch { }
                LogEvent("force-autopilot", "pilot_override=true");
            }
        }
    }
}
