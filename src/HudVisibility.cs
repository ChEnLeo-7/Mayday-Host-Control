using BepInEx.Configuration;
using Il2CppMayday;
using Il2CppMayday.UI;
using System;
using UnityEngine;

namespace MaydayHostControl
{
    public sealed partial class Plugin
    {
        internal static ConfigEntry<string> HideHudKey;
        private static int HideHudVirtualKey = 0x76; // F7
        private static string HideHudDisplay = "F7";
        private static bool HideHudKeyDown;

        internal static void InitializeHideHudShortcut()
        {
            HideHudVirtualKey = ParseBroadcastKey(HideHudKey != null ? HideHudKey.Value : null);
            HideHudDisplay = GetVirtualKeyDisplayName(HideHudVirtualKey);
        }

        internal static string HideHudShortcutName => HideHudDisplay;

        internal static void SetHideHudShortcut(int virtualKey)
        {
            HideHudVirtualKey = virtualKey;
            HideHudDisplay = GetVirtualKeyDisplayName(virtualKey);
            if (HideHudKey != null)
            {
                HideHudKey.Value = virtualKey.ToString();
                Settings.Save();
            }
            GuiShortcutCapture = false;
            GuiShortcutCaptureEscapeDown = false;
            GuiShortcutCaptureTarget = 0;
            GuiStatus = Loc.T("Shortcut changed to {0}", HideHudDisplay);
            LogEvent("hide-hud-shortcut", "key=" + HideHudDisplay + " vk=" + virtualKey);
            RebuildContent();
        }

        internal static void TickHideHud()
        {
            if (!NativeInput.IsGameWindowForeground()) return;
            var down = (NativeInput.GetAsyncKeyState(HideHudVirtualKey) & 0x8000) != 0;
            var pressed = down && !HideHudKeyDown;
            HideHudKeyDown = down;
            if (!pressed || GuiShortcutCapture || GuiVisible || BroadcastChatOpen || GuiClosing) return;
            try
            {
                var screen = HudScreen.Current;
                if (screen == null) return;
                screen.HiddenByUser = !screen.HiddenByUser;
                try
                {
                    Notify.Local(Loc.T(screen.HiddenByUser ? "WC.HudHidden" : "WC.HudShown"), NotifyKind.Info,
                        new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<Il2CppSystem.Object>(0));
                }
                catch { }
                LogEvent("hide-hud", "hidden=" + screen.HiddenByUser);
            }
            catch (Exception e)
            {
                Log?.LogWarning("[MaydayHostControl] hide hud failed: " + e.Message);
            }
        }
    }
}
