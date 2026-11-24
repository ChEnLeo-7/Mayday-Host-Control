using Il2CppMayday;
using Il2CppMayday.UI;
using Il2CppMirror;
using Il2CppTMPro;
using System;
using UnityEngine;

namespace MaydayHostControl
{
    public sealed partial class Plugin
    {
        private static RectTransform TpWindow;
        private static ulong TpWindowTarget;

        private static PlayerNet LocalHostPlayer()
        {
            try
            {
                var local = NetworkServer.localConnection;
                return local != null && local.identity != null ? local.identity.GetComponent<PlayerNet>() : null;
            }
            catch { return null; }
        }

        private static void OpenTpWindow(ulong steamId)
        {
            if (TpWindow != null) CloseTpWindow();
            if (GuiRoot == null) return;
            var target = GetLivePlayer(steamId);
            if (target == null || target.IsDead)
            {
                GuiStatus = Loc.T("Player is no longer online");
                return;
            }
            TpWindowTarget = steamId;
            TpWindow = UIF.Rect("TpWindow", GuiRoot);
            UIF.Stretch(TpWindow);
            UIF.Dimmer(TpWindow, 0.55f);
            var card = UIF.Panel(TpWindow, "Card", UITheme.Paper, UITheme.RadiusLarge, true, true).Root;
            UIF.At(card, UIF.Center, Vector2.zero, new Vector2(760f, 470f));
            NativeText(card, "Title", Loc.T("Teleport player"), UITheme.SizeHeader, UITheme.Navy, TextAlignmentOptions.Left, 28f, 20f, 704f, 48f, false);
            NativeText(card, "Target", target.DisplayName, UITheme.SizeBody, UITheme.InkSoft, TextAlignmentOptions.Left, 28f, 70f, 704f, 34f, false);

            NativeButton(card, "TpTargetToHost", Loc.T("Teleport player to host"), ButtonStyle.Secondary, 40f, 120f, 680f, 54f,
                () => { CloseTpWindow(); RunPanelAction(() => TpTargetToHost(steamId)); });
            NativeButton(card, "TpHostToTarget", Loc.T("Teleport host to player"), ButtonStyle.Secondary, 40f, 186f, 680f, 54f,
                () => { CloseTpWindow(); RunPanelAction(() => TpHostToTarget(steamId)); });
            NativeButton(card, "TpAllToTarget", Loc.T("Teleport all players to target"), ButtonStyle.Secondary, 40f, 252f, 680f, 54f,
                () => { CloseTpWindow(); RunPanelAction(() => TpAllToTarget(steamId)); });
            NativeButton(card, "CloseTp", Loc.T("Close panel"), ButtonStyle.Primary, 40f, 360f, 680f, 54f, CloseTpWindow);
            TpWindow.SetAsLastSibling();
        }

        private static void CloseTpWindow()
        {
            if (TpWindow == null) return;
            TpWindow.gameObject.SetActive(false);
            UnityEngine.Object.Destroy(TpWindow.gameObject);
            TpWindow = null;
            TpWindowTarget = 0;
        }

        private static string TpTargetToHost(ulong steamId)
        {
            var target = GetLivePlayer(steamId);
            var host = LocalHostPlayer();
            if (target == null || target.IsDead || host == null || host.IsDead) return Loc.T("Teleport target unavailable");
            target.ServerTeleport(host.transform.position, host.transform.eulerAngles.y);
            LogEvent("tp", "target-to-host target=" + steamId);
            return Loc.T("Teleported {0} to host", target.DisplayName);
        }

        private static string TpHostToTarget(ulong steamId)
        {
            var target = GetLivePlayer(steamId);
            var host = LocalHostPlayer();
            if (target == null || target.IsDead || host == null || host.IsDead) return Loc.T("Teleport target unavailable");
            host.ServerTeleport(target.transform.position, target.transform.eulerAngles.y);
            LogEvent("tp", "host-to-target target=" + steamId);
            return Loc.T("Teleported host to {0}", target.DisplayName);
        }

        private static string TpAllToTarget(ulong steamId)
        {
            var target = GetLivePlayer(steamId);
            var host = LocalHostPlayer();
            if (target == null || target.IsDead || host == null || host.IsDead) return Loc.T("Teleport target unavailable");
            var position = target.transform.position;
            var yaw = target.transform.eulerAngles.y;
            host.ServerTeleport(position, yaw);
            var count = 0;
            foreach (var pair in Online)
            {
                var player = GetLivePlayer(pair.Value.SteamId);
                if (player == null || player.IsDead) continue;
                if (pair.Value.SteamId == steamId) continue;
                try { player.ServerTeleport(position, yaw); count++; } catch { }
            }
            LogEvent("tp", "all-to-target target=" + steamId + " count=" + count);
            return Loc.T("Teleported {0} players to {1}", count, target.DisplayName);
        }

        private static string TpAllToHost()
        {
            var host = LocalHostPlayer();
            if (host == null || host.IsDead) return Loc.T("Teleport target unavailable");
            var position = host.transform.position;
            var yaw = host.transform.eulerAngles.y;
            var count = 0;
            foreach (var pair in Online)
            {
                var player = GetLivePlayer(pair.Value.SteamId);
                if (player == null || player.IsDead) continue;
                if (IsLocalHostSteamId(pair.Value.SteamId)) continue;
                try { player.ServerTeleport(position, yaw); count++; } catch { }
            }
            LogEvent("tp", "all-to-host count=" + count);
            return Loc.T("Teleported {0} players to host", count);
        }
    }
}
