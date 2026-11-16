using System.Linq;
using Il2CppMayday;
using Il2CppMirror;
using UnityEngine;

namespace MaydayHostControl
{
    public sealed partial class Plugin
    {
        private static PlayerNet GetLivePlayer(ulong steamId)
        {
            if (!NetworkServer.activeHost || steamId == 0) return null;
            foreach (var pair in Online.ToArray())
            {
                if (pair.Value == null || pair.Value.SteamId != steamId || pair.Key == null || pair.Key.identity == null) continue;
                var player = pair.Key.identity.GetComponent<PlayerNet>();
                if (player != null && player.SteamId == steamId) return player;
            }
            var local = NetworkServer.localConnection;
            var host = local != null && local.identity != null ? local.identity.GetComponent<PlayerNet>() : null;
            return host != null && host.SteamId == steamId ? host : null;
        }

        private static bool IsPlayerEliminated(PlayerNet player) => player != null && player.IsDead;

        private static string EliminateManagedPlayer(ulong steamId)
        {
            if (!NetworkServer.activeHost) return Loc.T("Only available when hosting a room");
            if (IsLocalHostSteamId(steamId)) return Loc.T("Player is not initialized");
            var player = GetLivePlayer(steamId);
            if (player == null) return Loc.T("Player is no longer online");
            if (player.IsDead) return Loc.T("Player is already eliminated");
            player.ServerKill("Host control");
            LogEvent("player-eliminate", "steam=" + steamId + " dead=" + player.IsDead);
            return Loc.T("Eliminated {0}", player.DisplayName);
        }

        private static string EliminateAllRemotePlayers()
        {
            if (!NetworkServer.activeHost) return Loc.T("Only available when hosting a room");
            if (NetworkServer.localConnection == null) return Loc.T("Player is not initialized");
            var targets = Online.Where(x => x.Key != null && !IsLocalHostConnection(x.Key) && x.Value != null)
                .Select(x => x.Value.SteamId).Where(id => id != 0).Distinct().ToArray();
            var succeeded = 0;
            var failed = 0;
            foreach (var id in targets)
            {
                try
                {
                    var player = GetLivePlayer(id);
                    if (player == null) { failed++; continue; }
                    if (player.IsDead) continue;
                    EliminateManagedPlayer(id);
                    if (player.IsDead) succeeded++;
                    else failed++;
                }
                catch (System.Exception e)
                {
                    failed++;
                    LogEvent("eliminate-all-player-error", "steam=" + id + " error=" + e.Message);
                }
            }
            LogEvent("eliminate-all", "succeeded=" + succeeded + " failed=" + failed + " hostExcluded=true");
            return Loc.T("Bulk action completed: {0} succeeded, {1} failed", succeeded, failed);
        }

        private static string RespawnAllPlayers()
        {
            if (!NetworkServer.activeHost) return Loc.T("Only available when hosting a room");
            var local = NetworkServer.localConnection;
            var host = local != null && local.identity != null ? local.identity.GetComponent<PlayerNet>() : null;
            if (host == null) return Loc.T("Player is not initialized");
            if (host.IsDead) return Loc.T("Host must be alive to revive players");
            var targets = Online.Where(x => x.Key != null && !IsLocalHostConnection(x.Key) && x.Value != null)
                .Select(x => x.Value.SteamId).Where(id => id != 0).Distinct().ToArray();
            var succeeded = 0;
            var failed = 0;
            foreach (var id in targets)
            {
                try
                {
                    var player = GetLivePlayer(id);
                    if (player == null) { failed++; continue; }
                    if (!player.IsDead) continue;
                    // Match the per-player revive policy: native server revival at the host's current position.
                    player.ServerRevive(host.transform.position, host.transform.eulerAngles.y, host);
                    if (player.IsDead) failed++;
                    else succeeded++;
                }
                catch (System.Exception e)
                {
                    failed++;
                    LogEvent("respawn-all-player-error", "steam=" + id + " error=" + e.Message);
                }
            }
            LogEvent("respawn-all", "succeeded=" + succeeded + " failed=" + failed + " hostExcluded=true");
            return Loc.T("Bulk action completed: {0} succeeded, {1} failed", succeeded, failed);
        }

        private static string RespawnManagedPlayer(ulong steamId)
        {
            if (!NetworkServer.activeHost) return Loc.T("Only available when hosting a room");
            if (IsLocalHostSteamId(steamId)) return Loc.T("Player is not initialized");
            var player = GetLivePlayer(steamId);
            if (player == null) return Loc.T("Player is no longer online");
            if (!player.IsDead) return Loc.T("Player is already alive");
            var local = NetworkServer.localConnection;
            var host = local != null && local.identity != null ? local.identity.GetComponent<PlayerNet>() : null;
            if (host == null) return Loc.T("Player is not initialized");
            if (host.IsDead) return Loc.T("Host must be alive to revive players");
            // User-selected policy: use the native server revival API at the host's location.
            // Keep the position in native scene coordinates, not ViewTransform/world-origin offsets.
            player.ServerRevive(host.transform.position, host.transform.eulerAngles.y, host);
            LogEvent("player-respawn", "steam=" + steamId + " nearHost=true dead=" + player.IsDead);
            return Loc.T("Respawned {0}", player.DisplayName);
        }
    }
}
