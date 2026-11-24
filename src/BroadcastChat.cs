using BepInEx.Configuration;
using HarmonyLib;
using Il2CppInterop.Runtime;
using System.Reflection;
using Il2CppMirror;
using System;
using System.Collections.Generic;
using System.Text;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppMayday;
using Il2CppMayday.UI;
using Il2CppTMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MaydayHostControl
{
    public sealed partial class Plugin
    {
        internal static ConfigEntry<string> BroadcastKey;
        internal static ConfigEntry<string> BroadcastPrefix;
        internal static ConfigEntry<string> BroadcastNewlineKey;

        private static int BroadcastShortcutVirtualKey = 0x78; // F9
        private static string BroadcastShortcutDisplay = "F9";
        private static bool BroadcastShortcutDown;
        private static bool BroadcastChatOpen;
        private static RectTransform BroadcastChatRoot;
        private static TMP_InputField BroadcastChatInput;
        private static TMP_Text BroadcastChatHint;
        private static UIButton BroadcastChatKindButton;
        private static int BroadcastChatKind;
        private static readonly int[] BroadcastChatKindCycle = { 0, 1, 4 };
        private static float BroadcastChatLastSent;
        private static float NextRelayBroadcastAt;
        private static int PingChatMarkerIndex;
        private static readonly Queue<string> BroadcastPendingLines = new Queue<string>();
        private static NotifyKind BroadcastPendingKind;
        private static float BroadcastNextLineAt;
        private static string BroadcastPendingPrefix = string.Empty;
        private static string BroadcastRawPrefix; // null = not detected yet
        internal static string LastBroadcastEchoText;
        internal static float LastBroadcastEchoAt;
        private static bool BroadcastEnterDown;
        private static bool BroadcastEscapeDown;
        private static bool BroadcastTabDown;
        private static bool BroadcastNewlineGuardPending;
        private static bool BroadcastNewlineGuardAllowed;
        private static int BroadcastRefocusFrames;
        private static bool BroadcastPreviousSendNavigation = true;
        private static bool BroadcastCursorReleased;
        private static string BroadcastChatStatus = string.Empty;
        private const string MemberRelayLobbyKey = "mayday_host_control_relay";
        private static ulong MemberRelayAdvertisedLobby;
        private static float NextMemberRelayAdvertiseAt;

        internal static string BroadcastShortcutName => BroadcastShortcutDisplay;

        internal static void InitializeBroadcastShortcut()
        {
            BroadcastShortcutVirtualKey = ParseBroadcastKey(BroadcastKey?.Value);
            BroadcastShortcutDisplay = GetVirtualKeyDisplayName(BroadcastShortcutVirtualKey);
        }

        internal static void SetBroadcastShortcut(int virtualKey)
        {
            BroadcastShortcutVirtualKey = virtualKey;
            BroadcastShortcutDisplay = GetVirtualKeyDisplayName(virtualKey);
            if (BroadcastKey != null)
            {
                BroadcastKey.Value = virtualKey.ToString();
                Settings.Save();
            }
            GuiShortcutCapture = false;
            GuiShortcutCaptureEscapeDown = false;
            GuiShortcutCaptureTarget = 0;
            GuiStatus = Loc.T("Shortcut changed to {0}", BroadcastShortcutDisplay);
            LogEvent("broadcast-shortcut", "key=" + BroadcastShortcutDisplay + " vk=" + virtualKey);
            RebuildContent();
        }

        internal static int ParseBroadcastKey(string configured)
        {
            var name = (configured ?? string.Empty).Trim();
            if (int.TryParse(name, out var stored) && stored >= 1 && stored <= 255) return stored;
            if (name.Length == 1 && char.IsLetter(name[0])) return char.ToUpperInvariant(name[0]);
            if (name.Length >= 2 && (name[0] == 'F' || name[0] == 'f') &&
                int.TryParse(name.Substring(1), out var function) && function >= 1 && function <= 12)
                return 0x70 + function - 1;
            return 0x78;
        }

        internal static void TickBroadcastChat()
        {
            DrainBroadcastLines();
            if (GuiShortcutCapture) return;
            if (!NativeInput.IsGameWindowForeground())
            {
                BroadcastShortcutDown = true;
                BroadcastTabDown = true;
                BroadcastEnterDown = true;
                BroadcastEscapeDown = true;
                return;
            }
            var down = (NativeInput.GetAsyncKeyState(BroadcastShortcutVirtualKey) & 0x8000) != 0;
            var pressed = down && !BroadcastShortcutDown;
            BroadcastShortcutDown = down;
            var enterShortcut = BroadcastShortcutVirtualKey == 0x0D;
            var shortcutShift = IsShiftDown();
            var shortcutCtrl = IsCtrlDown();
            var shortcutAlt = IsAltDown();
            var justOpened = false;
            if (pressed && !(enterShortcut && (shortcutShift || shortcutCtrl || shortcutAlt)) && CanOpenBroadcastChat())
            {
                if (!BroadcastChatOpen)
                {
                    OpenBroadcastChat();
                    justOpened = BroadcastChatOpen;
                }
                else if (!(enterShortcut && HasChatDraft()))
                {
                    CloseBroadcastChat();
                }
            }
            if (!BroadcastChatOpen) return;
            if (!CanOpenBroadcastChat())
            {
                CloseBroadcastChat();
                return;
            }
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            Il2CppMayday.GameInput.LookCaptured = false;
            if (!UnityEngine.Application.isFocused)
            {
                BroadcastTabDown = true;
                BroadcastEnterDown = true;
                BroadcastEscapeDown = true;
                return;
            }
            var tab = (NativeInput.GetAsyncKeyState(0x09) & 0x8000) != 0;
            if (tab && !BroadcastTabDown && !shortcutShift && !shortcutCtrl && !shortcutAlt) CycleBroadcastKind();
            BroadcastTabDown = tab;
            var escape = (NativeInput.GetAsyncKeyState(0x1B) & 0x8000) != 0;
            if (escape && !BroadcastEscapeDown) CloseBroadcastChat();
            BroadcastEscapeDown = escape;
            var enter = (NativeInput.GetAsyncKeyState(0x0D) & 0x8000) != 0;
            if (enter && !BroadcastEnterDown)
            {
                if (!shortcutShift && !shortcutCtrl && !shortcutAlt)
                {
                    if (enterShortcut && justOpened)
                    {
                        BroadcastEnterDown = true;
                        BroadcastNewlineGuardAllowed = false;
                        BroadcastNewlineGuardPending = true;
                    }
                    else
                    {
                        SendBroadcastMessage();
                    }
                }
                else
                {
                    var configuredCtrl = BroadcastNewlineKey != null && string.Equals(BroadcastNewlineKey.Value, "Ctrl", StringComparison.OrdinalIgnoreCase);
                    BroadcastNewlineGuardAllowed = configuredCtrl ? shortcutCtrl : shortcutShift;
                    BroadcastNewlineGuardPending = true;
                }
            }
            BroadcastEnterDown = enter;
            if (BroadcastRefocusFrames > 0 && BroadcastChatInput != null)
            {
                if (BroadcastChatInput.isFocused)
                {
                    BroadcastRefocusFrames = 0;
                }
                else
                {
                    var eventSystem = EventSystem.current;
                    if (eventSystem != null) eventSystem.SetSelectedGameObject(BroadcastChatInput.gameObject);
                    BroadcastChatInput.ActivateInputField();
                    BroadcastRefocusFrames--;
                }
            }
        }

        internal static void TickBroadcastChatLate()
        {
            ProcessBroadcastNewlineGuard();
        }

        private static bool IsCtrlDown()
        {
            return (NativeInput.GetAsyncKeyState(0x11) & 0x8000) != 0 ||
                   (NativeInput.GetAsyncKeyState(0xA2) & 0x8000) != 0 ||
                   (NativeInput.GetAsyncKeyState(0xA3) & 0x8000) != 0;
        }

        private static bool IsShiftDown()
        {
            return (NativeInput.GetAsyncKeyState(0x10) & 0x8000) != 0 ||
                   (NativeInput.GetAsyncKeyState(0xA0) & 0x8000) != 0 ||
                   (NativeInput.GetAsyncKeyState(0xA1) & 0x8000) != 0;
        }

        private static bool IsAltDown()
        {
            return (NativeInput.GetAsyncKeyState(0x12) & 0x8000) != 0 ||
                   (NativeInput.GetAsyncKeyState(0xA4) & 0x8000) != 0 ||
                   (NativeInput.GetAsyncKeyState(0xA5) & 0x8000) != 0;
        }

        private static void ProcessBroadcastNewlineGuard()
        {
            if (!BroadcastNewlineGuardPending) return;
            BroadcastNewlineGuardPending = false;
            if (BroadcastChatInput == null) return;
            try
            {
                var text = BroadcastChatInput.text ?? string.Empty;
                if (BroadcastNewlineGuardAllowed)
                {
                    if (!text.EndsWith("\n") && !text.EndsWith("\r")) BroadcastChatInput.text = text + "\n";
                }
                else
                {
                    var trimmed = text.TrimEnd('\n', '\r');
                    if (trimmed != text) BroadcastChatInput.text = trimmed;
                }
                BroadcastChatInput.caretPosition = (BroadcastChatInput.text ?? string.Empty).Length;
            }
            catch { }
        }

        private static bool CanOpenBroadcastChat()
        {
            if (GuiVisible || GuiClosing) return false;
            var uiRoot = UIRoot.Instance;
            if (uiRoot == null || uiRoot.CurrentMode != UIRoot.Mode.Game) return false;
            var pause = uiRoot.Pause;
            if (pause != null && pause.IsOpen) return false;
            if (NetworkServer.activeHost) return true;
            // Members always have the ping-label text fallback, even when the host
            // runs the vanilla game and has no mod-side relay command.
            return NetworkClient.active;
        }

        private static bool HostSupportsMemberRelay
        {
            get
            {
                if (NetworkServer.activeHost) return true;
                if (!NetworkClient.active || LobbyService == null || !LobbyService.InLobby) return false;
                try
                {
                    var lobby = LobbyService.Lobby;
                    return lobby.IsValid() &&
                           Il2CppSteamworks.SteamMatchmaking.GetLobbyData(lobby, MemberRelayLobbyKey) == "1";
                }
                catch { return false; }
            }
        }

        internal static void TickMemberRelayAdvertisement()
        {
            if (!NetworkServer.activeHost) return;
            var service = LobbyService;
            if (service == null || !service.InLobby || !service.Lobby.IsValid()) return;
            var lobbyId = service.Lobby.m_SteamID;
            if (MemberRelayAdvertisedLobby == lobbyId) return;
            if (Time.unscaledTime < NextMemberRelayAdvertiseAt) return;
            NextMemberRelayAdvertiseAt = Time.unscaledTime + 2f;
            try
            {
                var enabled = Il2CppMayday.UI.DebugConsole.AllowedForClients;
                var value = enabled ? "1" : "0";
                if (Il2CppSteamworks.SteamMatchmaking.SetLobbyData(service.Lobby, MemberRelayLobbyKey, value))
                    MemberRelayAdvertisedLobby = lobbyId;
            }
            catch { }
        }

        private static bool HasChatDraft()
        {
            return BroadcastChatInput != null && !string.IsNullOrWhiteSpace(BroadcastChatInput.text);
        }

        private static void OpenBroadcastChat()
        {
            if (BroadcastChatOpen) return;
            var uiRoot = UIRoot.Instance;
            if (uiRoot == null || uiRoot.ScreenLayer == null) return;
            var step = "panel";
            try
            {
                var panel = UIF.Panel(uiRoot.ScreenLayer, "BroadcastChat", UITheme.Paper, UITheme.RadiusLarge, true, true);
                if (panel == null || panel.Root == null) return;
                BroadcastChatRoot = panel.Root;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                Il2CppMayday.GameInput.LookCaptured = false;
                BroadcastCursorReleased = true;
                BroadcastChatRoot.anchorMin = new Vector2(0.5f, 0f);
                BroadcastChatRoot.anchorMax = new Vector2(0.5f, 0f);
                BroadcastChatRoot.pivot = new Vector2(0.5f, 0f);
                BroadcastChatRoot.sizeDelta = new Vector2(700f, 140f);
                BroadcastChatRoot.anchoredPosition = new Vector2(0f, 44f);
                BroadcastChatRoot.SetAsLastSibling();

                step = "input";
                BroadcastChatInput = UIF.Input(BroadcastChatRoot, "BroadcastInput", string.Empty,
                    Loc.T("WC.ChatPlaceholder"), 120, NativeStringAction(_ => { }), 360f, 44f);
                if (BroadcastChatInput != null)
                {
                    UIF.At(BroadcastChatInput.GetComponent<RectTransform>(), UIF.TopLeft,
                        new Vector2(12f, -10f), new Vector2(676f, 60f));
                    try { BroadcastChatInput.lineType = TMP_InputField.LineType.MultiLineNewline; } catch (Exception lineTypeError) { Log?.LogWarning("[MaydayHostControl] broadcast input multiline failed: " + lineTypeError.Message); }
                }

                step = "kind";
                BroadcastChatKindButton = NativeButton(BroadcastChatRoot, "BroadcastKind", ChatKindName(),
                    ButtonStyle.Secondary, 88f, 44f, CycleBroadcastKind);
                if (BroadcastChatKindButton != null)
                {
                    BroadcastChatKindButton.hoverScale = 1f;
                    var kindLabel = BroadcastChatKindButton.GetComponentInChildren<TMP_Text>();
                    if (kindLabel != null) { kindLabel.enableAutoSizing = true; kindLabel.fontSizeMax = kindLabel.fontSize; kindLabel.fontSizeMin = 12f; kindLabel.overflowMode = TextOverflowModes.Truncate; }
                    UIF.At(BroadcastChatKindButton.GetComponent<RectTransform>(), UIF.TopLeft,
                        new Vector2(428f, -80f), new Vector2(115f, 40f));
                }

                step = "send";
                var send = NativeButton(BroadcastChatRoot, "BroadcastSend", Loc.T("WC.ChatSend"),
                    ButtonStyle.Primary, 88f, 44f, SendBroadcastMessage);
                if (send != null)
                {
                    send.hoverScale = 1f;
                    UIF.At(send.GetComponent<RectTransform>(), UIF.TopLeft,
                        new Vector2(556f, -80f), new Vector2(120f, 40f));
                }

                step = "hint";
                var hintText = NativeText(BroadcastChatRoot, "BroadcastHint", Loc.T("WC.ChatHint"),
                    UITheme.SizeTiny, UITheme.InkSoft, TextAlignmentOptions.Left, 12f, 100f, 405f, 26f, true);
                BroadcastChatHint = hintText.GetComponent<TMP_Text>();
                if (BroadcastChatHint != null) BroadcastChatHint.raycastTarget = false;

                BroadcastChatOpen = true;
                BroadcastTabDown = false;
                BroadcastRefocusFrames = 2;
                var navSystem = EventSystem.current;
                if (navSystem != null) { BroadcastPreviousSendNavigation = navSystem.sendNavigationEvents; navSystem.sendNavigationEvents = false; }
                BroadcastChatStatus = string.Empty;
                BroadcastEnterDown = false;
                BroadcastEscapeDown = false;
                RefreshBroadcastChatUi();
                var eventSystem = EventSystem.current;
                if (eventSystem != null) eventSystem.SetSelectedGameObject(BroadcastChatInput.gameObject);
                if (BroadcastChatInput != null) BroadcastChatInput.ActivateInputField();
            }
            catch (Exception e)
            {
                Log?.LogError("[MaydayHostControl] broadcast chat open failed at " + step + ": " + e);
                CloseBroadcastChat();
            }
        }

        private static void CloseBroadcastChat()
        {
            var wasOpen = BroadcastChatOpen;
            var cursorReleased = BroadcastCursorReleased;
            BroadcastChatOpen = false;
            BroadcastCursorReleased = false;
            if (BroadcastChatRoot != null)
            {
                UnityEngine.Object.Destroy(BroadcastChatRoot.gameObject);
                BroadcastChatRoot = null;
            }
            BroadcastChatInput = null;
            BroadcastChatHint = null;
            BroadcastChatKindButton = null;
            var navSystem = EventSystem.current;
            if (navSystem != null) navSystem.sendNavigationEvents = BroadcastPreviousSendNavigation;
            var eventSystem = EventSystem.current;
            if (eventSystem != null) eventSystem.SetSelectedGameObject(null);
            if (wasOpen || cursorReleased) ReconcileCursorState();
        }

        private static void CycleBroadcastKind()
        {
            var index = Array.IndexOf(BroadcastChatKindCycle, BroadcastChatKind);
            BroadcastChatKind = BroadcastChatKindCycle[(index + 1) % BroadcastChatKindCycle.Length];
            BroadcastChatStatus = string.Empty;
            BroadcastRefocusFrames = 2;
            RefreshBroadcastChatUi();
        }

        private static string ChatKindName()
        {
            switch (BroadcastChatKind)
            {
                case 1: return Loc.T("WC.ChatKindGood");
                case 4: return Loc.T("WC.ChatKindMoney");
                default: return Loc.T("WC.ChatKindInfo");
            }
        }

        private static void AddChunkedText(List<string> chunks, string line, int maxChunk = 14, int maxChunks = 8)
        {
            var remaining = line;
            while (remaining.Length > 0 && chunks.Count < maxChunks)
            {
                var take = Math.Min(maxChunk, remaining.Length);
                if (take < remaining.Length)
                {
                    for (var i = take; i > Math.Max(1, take - 4); i--)
                    {
                        var c = remaining[i - 1];
                        if (c == ' ' || "，。！？；、,.!?;:".IndexOf(c) >= 0)
                        {
                            take = i;
                            break;
                        }
                    }
                }
                var chunk = remaining.Substring(0, take).Trim();
                if (chunk.Length > 0) chunks.Add(chunk);
                remaining = remaining.Substring(take);
            }
        }

        private static string FormatForClient(string text)
        {
            try
            {
                var method = typeof(GameState).GetMethod("Format", BindingFlags.Static | BindingFlags.NonPublic);
                if (method == null) method = typeof(GameState).GetMethod("Format", BindingFlags.Static | BindingFlags.Public);
                if (method == null) return null;
                return method.Invoke(null, new object[] { text, new Il2CppStringArray(0) }) as string;
            }
            catch (Exception e)
            {
                Log?.LogWarning("[MaydayHostControl] format probe failed: " + e.Message);
                return null;
            }
        }

        private static string ResolveBroadcastPrefix(string text)
        {
            var configured = BroadcastPrefix != null ? (BroadcastPrefix.Value ?? string.Empty).Trim() : string.Empty;
            if (configured.Length > 0) return configured;
            if (BroadcastRawPrefix != null) return BroadcastRawPrefix;
            var candidates = new[] { string.Empty, "@", "#", "!", "raw:", "@raw:", "$" };
            foreach (var candidate in candidates)
            {
                var formatted = FormatForClient(candidate + text);
                LogEvent("broadcast-format", "prefix=" + (candidate.Length == 0 ? "-" : candidate) + " result=" + (formatted ?? "<null>"));
                if (formatted == text)
                {
                    BroadcastRawPrefix = candidate;
                    return candidate;
                }
            }
            BroadcastRawPrefix = string.Empty;
            return string.Empty;
        }

        private static void SendBroadcastMessage()
        {
            var raw = (BroadcastChatInput != null ? BroadcastChatInput.text : string.Empty) ?? string.Empty;
            var normalized = raw.Replace("\r\n", "\n").Replace('\r', '\n');
            var queued = new List<string>();
            foreach (var part in normalized.Split('\n'))
            {
                var line = part.Trim();
                if (line.Length == 0) continue;
                if (line.Length > 120) line = line.Substring(0, 120);
                line = line.Replace("{", "\uFF5B").Replace("}", "\uFF5D");
                AddChunkedText(queued, line);
                if (queued.Count >= 8) break;
            }
            if (queued.Count == 0)
            {
                BroadcastChatStatus = Loc.T("WC.ChatEmpty");
                RefreshBroadcastChatUi();
                return;
            }
            if (Time.unscaledTime - BroadcastChatLastSent < 1.5f)
            {
                BroadcastChatStatus = Loc.T("WC.ChatTooFast");
                RefreshBroadcastChatUi();
                return;
            }
            if (!NetworkServer.activeHost)
            {
                if (!NetworkClient.active)
                {
                    BroadcastChatStatus = Loc.T("WC.ChatNotHost");
                    RefreshBroadcastChatUi();
                    return;
                }
                if (!HostSupportsMemberRelay || !ConsoleRelay.CanSend)
                {
                    SendPingChatFallback(raw);
                    return;
                }
                var relayedKind = (NotifyKind)BroadcastChatKind;
                if (!SendRelayedBroadcastLine(queued[0], relayedKind))
                {
                    SendPingChatFallback(raw);
                    return;
                }
                BroadcastPendingLines.Clear();
                BroadcastPendingKind = relayedKind;
                for (var i = 1; i < queued.Count; i++) BroadcastPendingLines.Enqueue(queued[i]);
                BroadcastNextLineAt = Time.unscaledTime + 0.35f;
                BroadcastChatLastSent = Time.unscaledTime;
                if (BroadcastChatInput != null) BroadcastChatInput.text = string.Empty;
                BroadcastRefocusFrames = 2;
                BroadcastChatStatus = Loc.T("WC.ChatSent");
                BroadcastNewlineGuardAllowed = false;
                BroadcastNewlineGuardPending = true;
                LogEvent("broadcast-relayed", "kind=" + BroadcastChatKind + " lines=" + queued.Count + " length=" + queued[0].Length);
                RefreshBroadcastChatUi();
                return;
            }
            var prefix = ResolveBroadcastPrefix(queued[0]);
            try
            {
                var state = GameState.Instance;
                if (state == null)
                {
                    BroadcastChatStatus = Loc.T("WC.ChatNotReady");
                    RefreshBroadcastChatUi();
                    return;
                }
                var kind = (NotifyKind)BroadcastChatKind;
                BroadcastPendingLines.Clear();
                BroadcastPendingKind = kind;
                BroadcastPendingPrefix = prefix;
                Notify.Local(queued[0], kind, new Il2CppReferenceArray<Il2CppSystem.Object>(0));
                LastBroadcastEchoText = queued[0];
                LastBroadcastEchoAt = Time.unscaledTime;
                Notify.All(prefix + queued[0], kind, new Il2CppStringArray(0));
                for (var i = 1; i < queued.Count; i++) BroadcastPendingLines.Enqueue(queued[i]);
                BroadcastNextLineAt = Time.unscaledTime + 0.35f;
                BroadcastChatLastSent = Time.unscaledTime;
                if (BroadcastChatInput != null) BroadcastChatInput.text = string.Empty;
                BroadcastRefocusFrames = 2;
                BroadcastChatStatus = string.Empty;
                BroadcastNewlineGuardAllowed = false;
                BroadcastNewlineGuardPending = true;
                LogEvent("broadcast-sent", "kind=" + BroadcastChatKind + " lines=" + queued.Count + " length=" + queued[0].Length);
            }
            catch (Exception e)
            {
                BroadcastChatStatus = Loc.T("WC.ChatFailed") + ": " + e.Message;
                Log?.LogWarning("[MaydayHostControl] broadcast failed: " + e);
            }
            RefreshBroadcastChatUi();
        }

        private static bool SendRelayedBroadcastLine(string line, NotifyKind kind)
        {
            if (!NetworkClient.active) return false;
            try
            {
                var text = line ?? string.Empty;
                if (text.Length > 120) text = text.Substring(0, 120);
                var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(text));
                var eye = Vector3.zero;
                var aim = Vector3.forward;
                var local = PlayerNet.Local;
                if (local != null)
                {
                    eye = local.EyePosition;
                    aim = local.AimDirection;
                }
                return ConsoleRelay.Send("ma_say " + (int)kind + " " + encoded, eye, aim);
            }
            catch (Exception e)
            {
                Log?.LogWarning("[MaydayHostControl] relayed broadcast failed: " + e.Message);
                return false;
            }
        }

        private static bool SendPingTextLine(string line)
        {
            var rig = Il2CppMayday.Players.PlayerRig.Local;
            var net = rig != null ? rig.Net : null;
            var sync = rig != null ? rig.Sync : null;
            if (net == null || sync == null || net.IsDead || net.IsSpectator || net.IsStunned) return false;
            var text = (line ?? string.Empty).Trim();
            if (text.Length == 0) return false;
            var name = net.DisplayName ?? string.Empty;
            if (name.Length > 8) name = name.Substring(0, 8);
            var label = name.Length > 0 ? name + ": " + text : text;
            if (label.Length > 40) label = label.Substring(0, 40);
            var eye = net.EyePosition;
            var aim = net.AimDirection;
            var verticalOffset = 0.35f * (PingChatMarkerIndex % 4);
            PingChatMarkerIndex++;
            var point = eye + aim * 2f;
            try
            {
                var held = net.Inventory != null ? net.Inventory.Held : null;
                var kind = Il2CppMayday.Players.PingAim.Resolve(eye, aim, 8f, rig.transform, held, out var hit, out _);
                if (kind != Il2CppMayday.Players.PingAim.Kind.None) point = hit;
            }
            catch { }
            point += Vector3.up * verticalOffset;
            try
            {
                sync.OwnerPing(point, label);
                return true;
            }
            catch (Exception e)
            {
                Log?.LogWarning("[MaydayHostControl] ping-label chat failed: " + e.Message);
                return false;
            }
        }

        private static void SendPingChatFallback(string raw)
        {
            var normalized = (raw ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n');
            var chunks = new List<string>();
            foreach (var part in normalized.Split('\n'))
            {
                var line = part.Trim();
                if (line.Length == 0) continue;
                if (line.Length > 120) line = line.Substring(0, 120);
                AddChunkedText(chunks, line, 28);
                if (chunks.Count >= 8) break;
            }
            if (chunks.Count == 0)
            {
                BroadcastChatStatus = Loc.T("WC.ChatEmpty");
                RefreshBroadcastChatUi();
                return;
            }
            PingChatMarkerIndex = 0;
            if (!SendPingTextLine(chunks[0]))
            {
                BroadcastChatStatus = Loc.T("WC.ChatFailed");
                RefreshBroadcastChatUi();
                return;
            }
            BroadcastPendingLines.Clear();
            BroadcastPendingKind = (NotifyKind)BroadcastChatKind;
            for (var i = 1; i < chunks.Count; i++) BroadcastPendingLines.Enqueue(chunks[i]);
            BroadcastNextLineAt = Time.unscaledTime + 0.35f;
            BroadcastChatLastSent = Time.unscaledTime;
            if (BroadcastChatInput != null) BroadcastChatInput.text = string.Empty;
            BroadcastRefocusFrames = 2;
            BroadcastChatStatus = Loc.T("WC.ChatSent");
            BroadcastNewlineGuardAllowed = false;
            BroadcastNewlineGuardPending = true;
            LogEvent("broadcast-ping", "lines=" + chunks.Count + " length=" + chunks[0].Length);
            RefreshBroadcastChatUi();
        }

        private static void DrainBroadcastLines()
        {
            if (BroadcastPendingLines.Count == 0) return;
            if (Time.unscaledTime < BroadcastNextLineAt) return;
            if (!NetworkServer.activeHost)
            {
                try
                {
                    var line = BroadcastPendingLines.Dequeue();
                    var sent = false;
                    if (HostSupportsMemberRelay && ConsoleRelay.CanSend)
                        sent = SendRelayedBroadcastLine(line, BroadcastPendingKind);
                    if (!sent) sent = SendPingTextLine(line);
                    if (!sent)
                    {
                        BroadcastPendingLines.Clear();
                        BroadcastChatStatus = Loc.T("WC.ChatFailed");
                        RefreshBroadcastChatUi();
                        return;
                    }
                }
                catch (Exception e)
                {
                    BroadcastPendingLines.Clear();
                    Log?.LogWarning("[MaydayHostControl] relayed/ping broadcast line failed: " + e.Message);
                    return;
                }
                BroadcastNextLineAt = Time.unscaledTime + 0.35f;
                return;
            }
            var prefix = BroadcastPendingPrefix;
            try
            {
                var line = BroadcastPendingLines.Dequeue();
                Notify.Local(line, BroadcastPendingKind, new Il2CppReferenceArray<Il2CppSystem.Object>(0));
                LastBroadcastEchoText = line;
                LastBroadcastEchoAt = Time.unscaledTime;
                Notify.All(prefix + line, BroadcastPendingKind, new Il2CppStringArray(0));
            }
            catch (Exception e)
            {
                BroadcastPendingLines.Clear();
                Log?.LogWarning("[MaydayHostControl] broadcast line failed: " + e.Message);
                return;
            }
            BroadcastNextLineAt = Time.unscaledTime + 0.35f;
        }

        private static void RefreshBroadcastChatUi()
        {
            if (BroadcastChatKindButton != null)
            {
                var label = BroadcastChatKindButton.GetComponentInChildren<TMP_Text>();
                if (label != null) label.text = ChatKindName();
            }
            if (BroadcastChatHint != null)
            {
                var hint = Loc.T("WC.ChatHint") + "    " + Loc.T("WC.ChatTabHint");
                if (!string.IsNullOrEmpty(BroadcastChatStatus)) hint += "    " + BroadcastChatStatus;
                BroadcastChatHint.text = hint;
            }
        }

    [HarmonyPatch(typeof(Il2CppMayday.UI.UIRoot), "OnNotify")]
    internal static class BroadcastNotifyDedupPatch
    {
        private static bool Prefix(string text, Il2CppMayday.NotifyKind kind)
        {
            if (!Il2CppMirror.NetworkServer.activeHost) return true;
            var expected = Plugin.LastBroadcastEchoText;
            if (!string.IsNullOrEmpty(expected) && expected == text && UnityEngine.Time.unscaledTime - Plugin.LastBroadcastEchoAt < 0.4f)
            {
                Plugin.LastBroadcastEchoText = null;
                return false;
            }
            return true;
        }
    }
    }
}
