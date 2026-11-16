using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Threading;
using MelonLoader;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Configuration;
using HarmonyLib;
using Il2CppMirror;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;

[assembly: MelonInfo(typeof(MaydayHostControl.Plugin), "更好的房主管理", "0.5.27", "Mayday Host Control")]

namespace MaydayHostControl
{
    public sealed partial class Plugin : MelonMod
    {
        public const string Guid = "local.mayday.hostcontrol";
        public const string Name = "更好的房主管理";
        public const string Version = "0.5.27";

        internal static ManualLogSource Log;
        internal static bool DebugLoggingEnabled = false;
        internal sealed class FlightCheckpoint
        {
            public int SchemaVersion { get; set; } = 5;
            public string CheckpointId { get; set; }
            public string DisplayName { get; set; }
            public string GameVersion { get; set; }
            public DateTime SavedUtc { get; set; }
            public string CaptureMode { get; set; } = "Ground";
            public string RecoveryMode { get; set; } = "ExactGround";
            public string PassengerRecoveryPolicy { get; set; } = "PendingOnly";
            public int SafeRecoveryAirport { get; set; } = -1;
            public string SafeRecoveryAirportCode { get; set; }
            public int SavedOnBoardPassengers { get; set; }
            public int SavedWaitingPassengers { get; set; }
            public int SavedDisembarkingPassengers { get; set; }
            public int RunSeed { get; set; }
            public int Leg { get; set; }
            public int Money { get; set; }
            public int Debt { get; set; }
            public float Rating { get; set; }
            public float Difficulty { get; set; }
            public int TotalPassengers { get; set; }
            public int TotalEarned { get; set; }
            public float RunStartTime { get; set; }
            public int CurrentAirport { get; set; }
            public int DestinationAirport { get; set; }
            public string CurrentAirportName { get; set; }
            public string CurrentAirportCode { get; set; }
            public string DestinationName { get; set; }
            public float GroundTimeLeft { get; set; }
            public float LegTime { get; set; }
            public float TimeOfDay { get; set; }
            public float Weather { get; set; }
            public string[] Upgrades { get; set; }
            public ItemCheckpoint[] Items { get; set; }
            public PlaneCheckpoint Plane { get; set; }
            public PlayerCheckpoint[] Players { get; set; }
            public PendingPassengerCheckpoint[] PendingPassengers { get; set; }
            public int PassengerRunSeed { get; set; }
            public int PassengerLegSeed { get; set; }
            public int PassengerQueueSeed { get; set; }
            public SeatRowCheckpoint[] SeatRows { get; set; }
            public TrolleyCheckpoint[] Trolleys { get; set; }
            public byte SeatRowMask { get; set; }
            public bool PremiumSeats { get; set; }
            public byte SeatKitMask { get; set; }
            public BaggageCartCheckpoint[] BaggageCarts { get; set; }
        }

        internal sealed class PlaneCheckpoint
        {
            public byte WheelDamage { get; set; }
            public bool GearDown { get; set; }
            public byte Flap { get; set; }
            public bool ParkingBrake { get; set; }
            public bool GearJammed { get; set; }
            public byte ApModes { get; set; }
            public byte ApFault { get; set; }
            public bool SeatbeltSign { get; set; }
            public bool Crashed { get; set; }
            public string CrashReason { get; set; }
            public bool ReverseGear { get; set; }
            public float FuelLeft { get; set; }
            public float FuelRight { get; set; }
            public float FuelCapacity { get; set; }
            public float EngineLeftOil { get; set; }
            public float EngineRightOil { get; set; }
            public float EngineLeftHealth { get; set; }
            public float EngineRightHealth { get; set; }
            public float EngineLeftFire { get; set; }
            public float EngineRightFire { get; set; }
            public byte EngineLeftState { get; set; }
            public byte EngineRightState { get; set; }
            public ushort EngineLeftFaults { get; set; }
            public ushort EngineRightFaults { get; set; }
            public float EngineLeftRpm { get; set; }
            public float EngineRightRpm { get; set; }
            public float EngineLeftTemp { get; set; }
            public float EngineRightTemp { get; set; }
            public float EngineLeftThrust { get; set; }
            public float EngineRightThrust { get; set; }
            public float EngineLeftRepair { get; set; }
            public float EngineRightRepair { get; set; }
            public float EngineLeftBattery { get; set; }
            public float EngineRightBattery { get; set; }
            public byte EngineLeftBatteryState { get; set; }
            public byte EngineRightBatteryState { get; set; }
            public byte EngineLeftCowlState { get; set; }
            public byte EngineRightCowlState { get; set; }
            public float FuelReserve { get; set; }
            public byte FuelCaps { get; set; }
            public byte FuelLeaks { get; set; }
            public bool FuelPumpLeft { get; set; }
            public bool FuelPumpRight { get; set; }
            public bool FuelCrossfeed { get; set; }
            public float ApAltitudeTarget { get; set; }
            public float ApHeadingTarget { get; set; }
            public float ApSpeedTarget { get; set; }
            public ulong IceState { get; set; }
            public float CrosswindX { get; set; }
            public float CrosswindY { get; set; }
            public FlightPhysicsCheckpoint Physics { get; set; }
            public float[] HullDamage { get; set; }
            public float[] HullDirt { get; set; }
            public float[] HullHoles { get; set; }
        }

        internal sealed class FlightPhysicsCheckpoint
        {
            public float X { get; set; }
            public float Y { get; set; }
            public float Z { get; set; }
            public float VelocityX { get; set; }
            public float VelocityY { get; set; }
            public float VelocityZ { get; set; }
            public float Heading { get; set; }
            public float Pitch { get; set; }
            public float Roll { get; set; }
            public float PitchRate { get; set; }
            public float RollRate { get; set; }
            public float YawRate { get; set; }
            public float Speed { get; set; }
            public float Track { get; set; }
            public float Gamma { get; set; }
            public bool OnGround { get; set; }
        }

        internal sealed class ItemCheckpoint
        {
            public string ItemId { get; set; }
            public float X { get; set; }
            public float Y { get; set; }
            public float Z { get; set; }
            public float Qx { get; set; }
            public float Qy { get; set; }
            public float Qz { get; set; }
            public float Qw { get; set; }
            public byte State { get; set; }
            public float Amount { get; set; }
            public int Count { get; set; }
            public uint Tag { get; set; }
            public byte TintR { get; set; }
            public byte TintG { get; set; }
            public byte TintB { get; set; }
            public byte TintA { get; set; }
            public string RuntimeType { get; set; }
            public bool SceneOwned { get; set; }
            public double HotUntil { get; set; }
            public byte EatPercent { get; set; }
            public byte CigarettePuffs { get; set; }
            public byte JerryCanPlug { get; set; }
            public bool AccentMode { get; set; }
        }

        internal sealed class PlayerCheckpoint
        {
            public ulong SteamId { get; set; }
            public string DisplayName { get; set; }
            public float X { get; set; }
            public float Y { get; set; }
            public float Z { get; set; }
            public float Heading { get; set; }
            public string PositionAnchor { get; set; }
            public int ActiveSlot { get; set; }
            public InventoryItemCheckpoint[] Inventory { get; set; }
            public bool WasSeated { get; set; }
            public byte SeatKind { get; set; }
            public int SeatIndex { get; set; }
            public string HeldItemId { get; set; }
            public bool ActiveItemWasHeld { get; set; }
        }

        internal sealed class InventoryItemCheckpoint
        {
            public int SlotOrder { get; set; }
            public ItemCheckpoint Item { get; set; }
        }

        internal sealed class SeatRowCheckpoint
        {
            public int Slot { get; set; }
            public bool Premium { get; set; }
            public double InstalledAt { get; set; }
        }

        internal sealed class BaggageCartCheckpoint
        {
            public int SiteIndex { get; set; }
            public string Terminal { get; set; }
            public string Stand { get; set; }
            public bool Arrival { get; set; }
            public bool AtAnchor { get; set; }
            public float X { get; set; }
            public float Y { get; set; }
            public float Z { get; set; }
            public float Yaw { get; set; }
        }

        internal sealed class TrolleyCheckpoint
        {
            public float X { get; set; }
            public float Y { get; set; }
            public float Z { get; set; }
            public float Qx { get; set; }
            public float Qy { get; set; }
            public float Qz { get; set; }
            public float Qw { get; set; }
            public float Side { get; set; }
            public bool Braked { get; set; }
            public TrolleySlotCheckpoint[] Slots { get; set; }
        }

        internal sealed class TrolleySlotCheckpoint
        {
            public int Index { get; set; }
            public string PlaceKey { get; set; }
            public ItemCheckpoint Item { get; set; }
        }

        internal sealed class PendingPassengerCheckpoint
        {
            public int Seed { get; set; }
            public byte Type { get; set; }
            public int Seat { get; set; }
            public int Fare { get; set; }
            public bool Paid { get; set; }
            public int LegNumber { get; set; }
            public byte TagR { get; set; }
            public byte TagG { get; set; }
            public byte TagB { get; set; }
            public byte TagA { get; set; }
            public float Mood { get; set; }
            public string BagId { get; set; }
            public byte BagColor { get; set; }
            public byte HijackerVisual { get; set; }
            public byte HijackerServer { get; set; }
        }

        private static bool ItemCatalogReady;
        private static readonly Dictionary<ulong, PlayerCheckpoint> PendingPlayerRecovery = new();
        private static readonly HashSet<ulong> PlayerRestoreStarted = new();
        private static readonly Queue<ulong> PlayerRestoreQueue = new();
        private static readonly Dictionary<ulong, int> PlayerItemsRemaining = new();
        private static readonly Dictionary<ulong, int> PlayerRestoreIndex = new();
        private static readonly Dictionary<ulong, object> PlayerRestoreDelegates = new();
        private static readonly Dictionary<ulong, DateTime> PlayerRestoreDeadlines = new();
        private static readonly Dictionary<ulong, long> PlayerRestoreAttempts = new();
        private static long PlayerRestoreAttemptCounter;
        private static int ServerGeneration;
        private static int PlayerRecoveryGeneration = -1;
        private static bool RecoveryPoisoned;
        private static string RecoveryPoisonReason = string.Empty;
        private sealed class RecoveryOperation
        {
            public string Id;
            public string CheckpointId;
            public FlightCheckpoint Snapshot;
            public string BackupPath;
            public int Generation;
            public int WaitFrames;
            public bool AwaitingAirportMaterialization;
            public bool AirportParked;
        }
        private static RecoveryOperation ActiveRecovery;
        private static string RecoveryStage = "IDLE";
        private static string CheckpointPath;
        private static string CheckpointDirectory;
        private static string HostAdminLegacyCheckpointPath;
        private static string HostAdminCheckpointDirectory;
        private static string MigrationMarkerPath;
        private static string DiagnosticPath;
        private static string SessionId;
        private static readonly Queue<string> RuntimeEvents = new();
        private static readonly object EventLock = new();
        private static DateTime SessionStartedUtc;
        private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
        private static string LastRecoveryMessage = "尚未恢复";
        private static string LastDiagnosticZip;
        internal static Il2CppMayday.Net.SteamLobbyService LobbyService;
        internal static bool LastJoinableSet;
        internal static bool LastJoinableSetKnown;
        internal static bool ManualJoinAllowed = true;

        internal static void LogEvent(string category, string message)
        {
            var line = DateTime.UtcNow.ToString("O") + " session=" + SessionId + " category=" + category + " " + message;
            lock (EventLock)
            {
                RuntimeEvents.Enqueue(line);
                while (RuntimeEvents.Count > 2000) RuntimeEvents.Dequeue();
            }
            Log?.LogInfo("[MaydayHostControl] " + line);
        }

        internal static void SyncMirrorCapacity(string source)
        {
            if (!Il2CppMirror.NetworkServer.activeHost) return;
            var target = AuthoritativeCapacity;
            var manager = Il2CppMirror.NetworkManager.singleton;
            var beforeManager = manager != null ? manager.maxConnections : -1;
            var beforeServer = Il2CppMirror.NetworkServer.maxConnections;
            if (manager != null && manager.maxConnections != target) manager.maxConnections = target;
            if (Il2CppMirror.NetworkServer.maxConnections != target) Il2CppMirror.NetworkServer.maxConnections = target;
            SyncSteamTransportCapacity(source, target);
            if (beforeManager != target || beforeServer != target)
                LogEvent("capacity", "source=" + source + " target=" + target + " manager=" + beforeManager + "->" + (manager != null ? manager.maxConnections : -1) + " networkServer=" + beforeServer + "->" + Il2CppMirror.NetworkServer.maxConnections + " connections=" + (Il2CppMirror.NetworkServer.connections?.Count ?? 0));
        }

        internal static void SyncSteamTransportCapacity(string source, int target)
        {
            // Steam transports cache their own admission limit; changing Mirror alone leaves it stale.
            var next = Il2CppMirror.FizzySteam.NextServer.server;
            if (next != null && next.maxConnections != target)
            {
                var before = next.maxConnections;
                next.maxConnections = target;
                LogEvent("transport-capacity", "source=" + source + " transport=NextServer limit=" + before + "->" + target);
            }
            var legacy = Il2CppMirror.FizzySteam.LegacyServer.server;
            if (legacy != null && legacy.maxConnections != target)
            {
                var before = legacy.maxConnections;
                legacy.maxConnections = target;
                LogEvent("transport-capacity", "source=" + source + " transport=LegacyServer limit=" + before + "->" + target);
            }
        }

        internal static bool IsConfiguredConnectionLimitReached(string source, int connectionId, string address)
        {
            var configured = AuthoritativeCapacity;
            var current = Il2CppMirror.NetworkServer.connections?.Count ?? 0;
            var full = current >= configured;
            var manager = Il2CppMirror.NetworkManager.singleton;
            LogEvent("connection-capacity-decision", "source=" + source + " conn=" + connectionId + " address=" + (address ?? "") + " allowed=" + !full + " full=" + full + " configured=" + configured + " current=" + current + " managerLimit=" + (manager != null ? manager.maxConnections : -1) + " networkLimit=" + Il2CppMirror.NetworkServer.maxConnections);
            return full;
        }

        internal static bool IsConfiguredAuthenticatorFull()
        {
            SyncMirrorCapacity("MaydayAuthenticator.IsFull");
            var configured = AuthoritativeCapacity;
            var current = Il2CppMirror.NetworkServer.connections?.Count ?? 0;
            // Authentication runs after Mirror has inserted the arriving connection.
            var full = Il2CppMirror.NetworkServer.activeHost && current > configured;
            LogEvent("auth-capacity-decision", "full=" + full + " configured=" + configured + " current=" + current + " networkLimit=" + Il2CppMirror.NetworkServer.maxConnections + " activeHost=" + Il2CppMirror.NetworkServer.activeHost);
            return full;
        }

        internal static void LogConnectionDecision(string source, int connectionId, string uri)
        {
            var manager = Il2CppMirror.NetworkManager.singleton;
            LogEvent("connection-check", "source=" + source + " conn=" + connectionId + " uri=" + (uri ?? "") + " managerLimit=" + (manager != null ? manager.maxConnections : -1) + " networkLimit=" + Il2CppMirror.NetworkServer.maxConnections + " connections=" + (Il2CppMirror.NetworkServer.connections?.Count ?? 0) + " configured=" + PlayerLimit.Value);
        }

        private static object GetLobbySnapshot(Il2CppMayday.Net.SteamLobbyService service)
        {
            if (service == null || !service.InLobby || !service.Lobby.IsValid()) return new { active = false };
            var lobby = service.Lobby;
            return new { active = true, lobbyId = lobby.m_SteamID.ToString(), members = Il2CppSteamworks.SteamMatchmaking.GetNumLobbyMembers(lobby), memberLimit = Il2CppSteamworks.SteamMatchmaking.GetLobbyMemberLimit(lobby), visibility = service.Visibility.ToString(), advertise = service.Advertise, joinableSetKnown = LastJoinableSetKnown, joinableSetSucceeded = LastJoinableSet };
        }
        internal sealed class OnlinePlayer
        {
            public ulong SteamId;
            public string Name;
        }

        internal static readonly Dictionary<NetworkConnectionToClient, OnlinePlayer> Online = new();
        internal sealed class BanRecord
        {
            public string PlayerName { get; set; }
            public DateTimeOffset? BannedAtUtc { get; set; }
        }

        internal static readonly Dictionary<ulong, BanRecord> BanRecords = new();
        private static string BanRecordsPath => Path.ChangeExtension(BlacklistPath, ".json");
        internal static HashSet<ulong> Blacklist = new();
        internal static Dictionary<ulong, string> PlayerTags = new();
        internal static string BlacklistPath;
        internal static string PlayerTagsPath;
        internal static string CommandPath;
        internal static ConfigEntry<int> WebPort;
        internal static ConfigEntry<string> OpenPanelKey;
        internal static ConfigEntry<string> PanelLanguage;
        internal static ConfigEntry<string> RoomName;
        internal static ConfigEntry<bool> DefaultRoomJoinable;
        internal static ConfigEntry<string> DefaultRoomVisibility;
        internal static ConfigEntry<bool> RoomDefaultsConfigured;
        internal static ConfigEntry<int> PlayerLimit;
        internal static ConfigEntry<bool> AllowAirborneSafetySnapshots;
        internal static ConfigFile Settings;

        // Exposed without a compile-time reference so the optional capacity mod can read the live panel value.
        public static int AuthoritativeCapacity => Math.Max(1, Math.Min(250, PlayerLimit != null ? PlayerLimit.Value : 6));
        private static HttpListener Web;
        private static Thread WebThread;
        private sealed class AdminCommand
        {
            public string Id;
            public string Text;
            public bool Web;
        }

        private static readonly Queue<AdminCommand> Commands = new();
        private static readonly Dictionary<string, string> WebResults = new();
        private static readonly Queue<string> WebResultOrder = new();
        private static readonly Queue<string> Audit = new();
        private static readonly object StateLock = new();
        private static string CachedStatusJson = "{}";
        private static DateTime NextStatusRefreshUtc;
        private static bool VisibilitySetKnown;
        private static bool VisibilitySetSucceeded;
        private static string LastVisibilityRequested = string.Empty;
        private static DateTime LastVisibilityChangedUtc;
        private static string CurrentGameVersion = string.Empty;
        private static volatile bool Running;

        private static string DetectGameLanguage()
        {
            try { return Loc.FromSystemLanguage(Application.systemLanguage.ToString()); }
            catch { return "en"; }
        }

        public override void OnInitializeMelon()
        {
            Log = new ManualLogSource(Name);
            Settings = new ConfigFile("MaydayHostControl");
            try { CurrentGameVersion = Application.version.ToString(); } catch { CurrentGameVersion = string.Empty; }
            WebPort = Settings.Bind("LegacyWeb", "Port", 27842, "保留字段；Manager 版本不启动 HTTP 服务。");
            var detectedGameLanguage = DetectGameLanguage();
            PanelLanguage = Settings.Bind("General", "Language", detectedGameLanguage, "Host Control interface language; change in Settings.");
            OpenPanelKey = Settings.Bind("General", "OpenPanelKey", "F8", "Host Control shortcut; change in Settings.");
            InitializePanelShortcut();
            RoomName = Settings.Bind("Room", "Name", "", "自定义 Steam 房间名称；游戏内面板可实时修改当前大厅，并作为新大厅默认名称。");
            DefaultRoomJoinable = Settings.Bind("Room", "DefaultRoomJoinable", true, "新房间是否允许玩家加入；在创建房间前的配置页面保存。");
            DefaultRoomVisibility = Settings.Bind("Room", "DefaultRoomVisibility", "FriendsOnly", "新房间可见性：Private、FriendsOnly 或 Public；保存创建前配置后生效。");
            RoomDefaultsConfigured = Settings.Bind("Room", "RoomDefaultsConfigured", false, "是否已保存创建前配置；未保存时保留游戏原本的可见性选择。");
            PlayerLimit = Settings.Bind("General", "PlayerLimit", 6, "Host-only Mirror/Steam 大厅人数上限，Mod 接受范围 1-250；Steam 或游戏运行时可能有更低上限。");
            PlayerLimit.Value = Math.Max(1, Math.Min(250, PlayerLimit.Value));
            AllowAirborneSafetySnapshots = Settings.Bind("Save", "AllowAirborneSafetySnapshots", true, "允许在 Flight 阶段保存安全快照。读取时返回起飞机场 PreShift，不会原位恢复空中物理、乘客实体或客户端本地动作。");
            BlacklistPath = Path.Combine(Paths.ConfigPath, "mayday-hostcontrol-blacklist.txt");
            PlayerTagsPath = Path.Combine(Paths.ConfigPath, "mayday-hostcontrol-player-tags.json");
            CommandPath = Path.Combine(Paths.ConfigPath, "mayday-hostcontrol-command.txt");
            CheckpointPath = Path.Combine(Paths.ConfigPath, "mayday-hostcontrol-legacy-checkpoint.json");
            CheckpointDirectory = Path.Combine(Paths.ConfigPath, "MaydayHostControl-Checkpoints");
            HostAdminLegacyCheckpointPath = Path.Combine(Paths.LegacyBepInExConfigPath, "mayday-hostadmin-checkpoint.json");
            HostAdminCheckpointDirectory = Path.Combine(Paths.LegacyBepInExConfigPath, "MaydayHostAdmin-Checkpoints");
            MigrationMarkerPath = Path.Combine(Paths.ConfigPath, ".legacy-migration-v2");
            Directory.CreateDirectory(CheckpointDirectory);
            DiagnosticPath = Path.Combine(Paths.ConfigPath, "MaydayHostControl-Diagnostics");
            SessionId = System.Guid.NewGuid().ToString("N");
            SessionStartedUtc = DateTime.UtcNow;
            LogEvent("plugin-load", "version=" + Version + " game=" + Application.version + " loader=MelonLoader gui=GameUI");
            MigrateLegacyData();
            LoadBlacklist();
            LoadPlayerTags();
            if (!File.Exists(CommandPath)) File.WriteAllText(CommandPath, "# 每行一条命令，例如 players\r\n");
            Running = true;
            RegisterGameCommands();
            Log.LogInfo("[MaydayHostControl] 已加载。快捷键 " + PanelShortcutName + " 仅在房间内打开房主管理页面；HTTP 服务未启动。");
        }

        private static void RegisterGameCommands()
        {
            try
            {
                var players = DelegateSupport.ConvertDelegate<Il2CppMayday.UI.ConsoleCommands.Handler>(GamePlayers);
                var kick = DelegateSupport.ConvertDelegate<Il2CppMayday.UI.ConsoleCommands.Handler>(GameKick);
                var ban = DelegateSupport.ConvertDelegate<Il2CppMayday.UI.ConsoleCommands.Handler>(GameBan);
                var unban = DelegateSupport.ConvertDelegate<Il2CppMayday.UI.ConsoleCommands.Handler>(GameUnban);
                var bans = DelegateSupport.ConvertDelegate<Il2CppMayday.UI.ConsoleCommands.Handler>(GameBans);
                Il2CppMayday.UI.ConsoleCommands.Add("ma_players", "ma_players", "", players, null);
                Il2CppMayday.UI.ConsoleCommands.Add("ma_kick", "ma_kick <SteamID64|序号>", "", kick, null);
                Il2CppMayday.UI.ConsoleCommands.Add("ma_ban", "ma_ban <SteamID64>", "", ban, null);
                Il2CppMayday.UI.ConsoleCommands.Add("ma_unban", "ma_unban <SteamID64>", "", unban, null);
                Il2CppMayday.UI.ConsoleCommands.Add("ma_bans", "ma_bans", "", bans, null);
                Log.LogInfo("[MaydayHostControl] 已注册游戏内实时命令");
            }
            catch (Exception e) { Log.LogError("[MaydayHostControl] 注册游戏内命令失败: " + e); }
        }

        private static void GamePlayers(Il2CppMayday.UI.ConsoleCtx ctx, Il2CppStringArray args) => PrintPlayers(ctx);
        private static void GameKick(Il2CppMayday.UI.ConsoleCtx ctx, Il2CppStringArray args) => ExecuteGame(ctx, "kick", args);
        private static void GameBan(Il2CppMayday.UI.ConsoleCtx ctx, Il2CppStringArray args) => ExecuteGame(ctx, "ban", args);
        private static void GameUnban(Il2CppMayday.UI.ConsoleCtx ctx, Il2CppStringArray args) => ExecuteGame(ctx, "unban", args);
        private static void GameBans(Il2CppMayday.UI.ConsoleCtx ctx, Il2CppStringArray args) => PrintBans(ctx);

        private static void PrintPlayers(Il2CppMayday.UI.ConsoleCtx ctx)
        {
            if (Online.Count == 0) { ctx.Print("[HostAdmin] 当前没有已认证玩家"); return; }
            var index = 1;
            foreach (var pair in Online) ctx.Print("[" + index++ + "] " + pair.Value.Name + " SteamID=" + pair.Value.SteamId + " Connection=" + pair.Key.connectionId);
        }

        private static void PrintBans(Il2CppMayday.UI.ConsoleCtx ctx)
        {
            if (Blacklist.Count == 0) { ctx.Print("[HostAdmin] 黑名单为空"); return; }
            foreach (var id in Blacklist.OrderBy(x => x)) ctx.Print("[HostAdmin] 黑名单 SteamID=" + id);
        }

        private static void ExecuteGame(Il2CppMayday.UI.ConsoleCtx ctx, string command, Il2CppStringArray args)
        {
            if (args == null || args.Length == 0) { ctx.Print("[HostAdmin] 缺少参数"); return; }
            var value = args[0].ToString();
            Execute(command + " " + value);
            ctx.Print("[HostAdmin] 已执行 " + command + " " + value);
        }

        private static void StartWebAdmin()
        {
            try
            {
                Web = new HttpListener();
                Web.Prefixes.Add("http://127.0.0.1:" + WebPort.Value + "/");
                Web.Start();
                WebThread = new Thread(WebLoop) { IsBackground = true, Name = "MaydayHostControlWeb" };
                WebThread.Start();
                Log.LogInfo("[MaydayHostControl] 网页管理端已启动: http://127.0.0.1:" + WebPort.Value + "/");
            }
            catch (Exception e) { Log.LogError("[MaydayHostControl] 网页管理端启动失败: " + e.Message); }
        }

        private static void WebLoop()
        {
            while (Web != null && Web.IsListening)
            {
                try { HandleWeb(Web.GetContext()); }
                catch (HttpListenerException) { break; }
                catch (Exception e) { Log?.LogDebug("[MaydayHostControl] 网页请求失败: " + e.Message); }
            }
        }

        private sealed class WebAction
        {
            public string action { get; set; }
            public string steamId { get; set; }
        }

        private sealed class RoomControlRequest
        {
            public int capacity { get; set; }
            public bool joinable { get; set; }
            public string visibility { get; set; }
        }

        private sealed class PlayerTagRequest
        {
            public string steamId { get; set; }
            public string tag { get; set; }
        }

        private sealed class CheckpointRequest
        {
            public string id { get; set; }
            public string name { get; set; }
        }

        private sealed class CheckpointSummary
        {
            public string id { get; set; }
            public string name { get; set; }
            public DateTime savedUtc { get; set; }
            public int schemaVersion { get; set; }
            public string gameVersion { get; set; }
            public int leg { get; set; }
            public int airport { get; set; }
            public string airportName { get; set; }
            public int players { get; set; }
            public int items { get; set; }
            public string captureMode { get; set; }
            public string recoveryMode { get; set; }
            public string warning { get; set; }
            public bool compatible { get; set; }
            public string issue { get; set; }
            public bool legacy { get; set; }
        }

        private static string SanitizePlayerTag(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            var cleaned = new string(value.Where(c => !char.IsControl(c)).ToArray()).Trim();
            return cleaned.Length <= 32 ? cleaned : cleaned.Substring(0, 32);
        }

        private static void MigrateLegacyData()
        {
            if (File.Exists(MigrationMarkerPath)) return;
            try
            {
                Directory.CreateDirectory(Paths.ConfigPath);
                var legacyRoot = Paths.LegacyBepInExConfigPath;
                var legacyConfigs = new[]
                {
                    Path.Combine(legacyRoot, "local.mayday.deepsave.cfg"),
                    Path.Combine(legacyRoot, "local.mayday.hostadmin.cfg")
                };
                var legacyMelon = Path.Combine(Paths.GameRootPath, "UserData", "MelonPreferences.cfg");
                if (!PlayerLimit.ExistedAtBind)
                {
                    var capacityText = ReadLegacyMelonValue(legacyMelon, "General", "PlayerLimit") ?? legacyConfigs.Reverse().Where(File.Exists).Select(path => ReadLegacyConfigValue(path, "General", "PlayerLimit")).FirstOrDefault(value => value != null);
                    if (int.TryParse(capacityText, out var capacity) && capacity >= 1 && capacity <= 250) PlayerLimit.Value = capacity;
                }
                if (!RoomName.ExistedAtBind)
                {
                    var roomName = ReadLegacyMelonValue(legacyMelon, "Room", "Name") ?? legacyConfigs.Reverse().Where(File.Exists).Select(path => ReadLegacyConfigValue(path, "Room", "Name")).FirstOrDefault(value => value != null);
                    if (roomName != null) RoomName.Value = roomName;
                }
                if (!AllowAirborneSafetySnapshots.ExistedAtBind)
                {
                    var airborneText = ReadLegacyMelonValue(legacyMelon, "Save", "AllowAirborneSafetySnapshots") ?? legacyConfigs.Reverse().Where(File.Exists).Select(path => ReadLegacyConfigValue(path, "Save", "AllowAirborneSafetySnapshots")).FirstOrDefault(value => value != null);
                    if (bool.TryParse(airborneText, out var airborne)) AllowAirborneSafetySnapshots.Value = airborne;
                }
                Settings.Save();

                var banned = new HashSet<ulong>();
                if (File.Exists(BlacklistPath))
                    foreach (var line in File.ReadAllLines(BlacklistPath)) if (ulong.TryParse(line.Trim(), out var id)) banned.Add(id);
                foreach (var file in new[] { Path.Combine(legacyRoot, "mayday-deepsave-blacklist.txt"), Path.Combine(legacyRoot, "mayday-hostadmin-blacklist.txt") })
                    if (File.Exists(file)) foreach (var line in File.ReadAllLines(file)) if (ulong.TryParse(line.Trim(), out var id)) banned.Add(id);
                File.WriteAllLines(BlacklistPath, banned.OrderBy(x => x).Select(x => x.ToString()), Encoding.UTF8);

                var tags = new Dictionary<string, string>(StringComparer.Ordinal);
                if (File.Exists(PlayerTagsPath))
                {
                    var current = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(PlayerTagsPath));
                    if (current != null) foreach (var entry in current) tags[entry.Key] = entry.Value;
                }
                foreach (var file in new[] { Path.Combine(legacyRoot, "mayday-deepsave-player-tags.json"), Path.Combine(legacyRoot, "mayday-hostadmin-player-tags.json") })
                    if (File.Exists(file))
                    {
                        var legacy = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(file));
                        if (legacy != null) foreach (var entry in legacy) if (!tags.ContainsKey(entry.Key)) tags[entry.Key] = entry.Value;
                    }
                if (tags.Count > 0) File.WriteAllText(PlayerTagsPath, JsonSerializer.Serialize(tags, JsonOptions), Encoding.UTF8);
                File.WriteAllText(MigrationMarkerPath, DateTime.UtcNow.ToString("O"), Encoding.UTF8);
                LogEvent("legacy-migration", "configs=" + legacyConfigs.Count(File.Exists) + " bans=" + banned.Count + " tags=" + tags.Count);
            }
            catch (Exception e)
            {
                Log.LogWarning("[MaydayHostControl] 旧数据迁移失败，将在下次启动重试: " + e.Message);
            }
        }

        private static string ReadLegacyMelonValue(string path, string section, string key)
        {
            if (!File.Exists(path)) return null;
            var currentSection = string.Empty;
            foreach (var raw in File.ReadAllLines(path))
            {
                var line = raw.Trim();
                if (line.StartsWith("[") && line.EndsWith("]"))
                {
                    currentSection = line.Substring(1, line.Length - 2).Trim('"');
                    continue;
                }
                if (string.Equals(currentSection, "MaydayHostControl." + section, StringComparison.OrdinalIgnoreCase))
                {
                    var equals = line.IndexOf('=');
                    if (equals > 0 && string.Equals(line.Substring(0, equals).Trim(), key, StringComparison.OrdinalIgnoreCase))
                        return line.Substring(equals + 1).Trim().Trim('"');
                }
                if (!string.Equals(currentSection, "MaydayHostControl", StringComparison.OrdinalIgnoreCase) || !line.StartsWith(section + " =", StringComparison.OrdinalIgnoreCase)) continue;
                var marker = key + " =";
                var markerIndex = line.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
                if (markerIndex < 0) continue;
                var value = line.Substring(markerIndex + marker.Length).Trim();
                if (value.StartsWith("\""))
                {
                    var endQuote = value.IndexOf('"', 1);
                    return endQuote > 0 ? value.Substring(1, endQuote - 1) : value.Trim('"', ' ', '}');
                }
                var end = value.IndexOfAny(new[] { ',', '}' });
                return (end >= 0 ? value.Substring(0, end) : value).Trim();
            }
            return null;
        }

        private static string ReadLegacyConfigValue(string path, string section, string key)
        {
            var currentSection = string.Empty;
            foreach (var raw in File.ReadAllLines(path))
            {
                var line = raw.Trim();
                if (line.StartsWith("[") && line.EndsWith("]")) { currentSection = line.Substring(1, line.Length - 2); continue; }
                if (!string.Equals(currentSection, section, StringComparison.OrdinalIgnoreCase)) continue;
                var equals = line.IndexOf('=');
                if (equals <= 0 || !string.Equals(line.Substring(0, equals).Trim(), key, StringComparison.OrdinalIgnoreCase)) continue;
                return line.Substring(equals + 1).Trim();
            }
            return null;
        }

        private static void LoadPlayerTags()
        {
            PlayerTags = new Dictionary<ulong, string>();
            try
            {
                if (!File.Exists(PlayerTagsPath)) return;
                var raw = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(PlayerTagsPath));
                if (raw == null) return;
                foreach (var entry in raw)
                    if (ulong.TryParse(entry.Key, out var id))
                    {
                        var tag = SanitizePlayerTag(entry.Value);
                        if (id != 0 && tag.Length > 0) PlayerTags[id] = tag;
                    }
                Log.LogInfo("[MaydayHostControl] 已加载玩家标签: " + PlayerTags.Count);
            }
            catch (Exception e) { Log.LogWarning("[MaydayHostControl] 玩家标签加载失败: " + e.Message); }
        }

        private static void SavePlayerTags()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(PlayerTagsPath));
                var data = new Dictionary<string, string>();
                foreach (var entry in PlayerTags.OrderBy(x => x.Key)) data[entry.Key.ToString()] = entry.Value;
                File.WriteAllText(PlayerTagsPath, JsonSerializer.Serialize(data, JsonOptions), Encoding.UTF8);
            }
            catch (Exception e) { Log.LogError("[MaydayHostControl] 玩家标签保存失败: " + e.Message); }
        }

        private static string ApplyPlayerTag(string steamIdText, string value)
        {
            if (!ulong.TryParse(steamIdText, out var steamId) || steamId == 0) return "SteamID64 无效";
            var tag = SanitizePlayerTag(value);
            if (tag.Length == 0) PlayerTags.Remove(steamId);
            else PlayerTags[steamId] = tag;
            SavePlayerTags();
            LogEvent("player-tag-updated", "steam=" + steamId + " tag=" + (tag.Length == 0 ? "<empty>" : tag));
            return tag.Length == 0 ? "已清除玩家标签" : "已保存玩家标签：" + tag;
        }

        private static string SanitizeCheckpointName(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            var cleaned = new string(value.Where(c => !char.IsControl(c)).ToArray()).Trim();
            return cleaned.Length <= 60 ? cleaned : cleaned.Substring(0, 60);
        }

        private static string ResolveCheckpointPath(string id)
        {
            if (string.Equals(id, "legacy", StringComparison.Ordinal)) return File.Exists(CheckpointPath) ? CheckpointPath : null;
            if (string.Equals(id, "hostadmin-legacy", StringComparison.Ordinal)) return File.Exists(HostAdminLegacyCheckpointPath) ? HostAdminLegacyCheckpointPath : null;
            if (!System.Guid.TryParseExact(id, "N", out _)) return null;
            var path = Path.Combine(CheckpointDirectory, id + ".json");
            if (File.Exists(path)) return path;
            var hostAdminPath = Path.Combine(HostAdminCheckpointDirectory, id + ".json");
            return File.Exists(hostAdminPath) ? hostAdminPath : null;
        }

        private static bool IsReadOnlyLegacyCheckpoint(string id)
        {
            if (string.Equals(id, "legacy", StringComparison.Ordinal) || string.Equals(id, "hostadmin-legacy", StringComparison.Ordinal)) return true;
            var path = ResolveCheckpointPath(id);
            if (path == null || string.IsNullOrWhiteSpace(HostAdminCheckpointDirectory)) return false;
            var legacyRoot = Path.GetFullPath(HostAdminCheckpointDirectory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            return Path.GetFullPath(path).StartsWith(legacyRoot, StringComparison.OrdinalIgnoreCase);
        }

        private static CheckpointSummary ReadCheckpointSummary(string id, string path, bool legacy)
        {
            try
            {
                var snapshot = JsonSerializer.Deserialize<FlightCheckpoint>(File.ReadAllText(path), JsonOptions);
                if (snapshot == null) throw new InvalidDataException("JSON 内容为空");
                var schemaSupported = snapshot.SchemaVersion == 4 || snapshot.SchemaVersion == 5;
                var issue = !schemaSupported ? "需要 schema 4 或 5，当前为 " + snapshot.SchemaVersion :
                    !string.Equals(snapshot.GameVersion, CurrentGameVersion, StringComparison.Ordinal) ? "游戏版本不匹配：" + (snapshot.GameVersion ?? "未知") : string.Empty;
                var captureMode = snapshot.SchemaVersion >= 5 ? snapshot.CaptureMode ?? "Ground" : "Ground";
                var recoveryMode = snapshot.SchemaVersion >= 5 ? snapshot.RecoveryMode ?? "ExactGround" : "ExactGround";
                var warning = recoveryMode == "SafeOriginPreShift"
                    ? "飞行中安全快照：恢复到起飞机场 PreShift；不会恢复已生成乘客、空中位置或客户端本地动作"
                    : string.Empty;
                return new CheckpointSummary
                {
                    id = id,
                    name = string.IsNullOrWhiteSpace(snapshot.DisplayName) ? (legacy ? "旧版单文件存档" : "未命名存档") : snapshot.DisplayName,
                    savedUtc = snapshot.SavedUtc,
                    schemaVersion = snapshot.SchemaVersion,
                    gameVersion = snapshot.GameVersion ?? string.Empty,
                    leg = snapshot.Leg,
                    airport = snapshot.CurrentAirport,
                    airportName = snapshot.CurrentAirportName ?? string.Empty,
                    players = snapshot.Players?.Length ?? 0,
                    items = snapshot.Items?.Length ?? 0,
                    captureMode = captureMode,
                    recoveryMode = recoveryMode,
                    warning = warning,
                    compatible = issue.Length == 0,
                    issue = issue,
                    legacy = legacy
                };
            }
            catch (Exception e)
            {
                return new CheckpointSummary { id = id, name = legacy ? "旧版单文件存档" : "损坏的存档", compatible = false, issue = "无法读取：" + e.Message, legacy = legacy };
            }
        }

        private static Il2CppMayday.World.AirportDef ResolveCheckpointAirportDefinition(FlightCheckpoint snapshot, out string reason)
        {
            reason = string.Empty;
            if (snapshot == null) { reason = "检查点内容为空"; return null; }
            try
            {
                var route = new Il2CppMayday.World.RouteGenerator(snapshot.RunSeed);
                var definition = route.Airport(snapshot.CurrentAirport);
                if (definition == null) { reason = "存档路由 seed=" + snapshot.RunSeed + " 中不存在机场 index=" + snapshot.CurrentAirport; return null; }
                if (!string.IsNullOrWhiteSpace(snapshot.CurrentAirportName) && !string.Equals(definition.name, snapshot.CurrentAirportName, StringComparison.OrdinalIgnoreCase))
                {
                    reason = "存档路由机场名称不匹配：seed=" + snapshot.RunSeed + " index=" + snapshot.CurrentAirport + " route=" + definition.name + " checkpoint=" + snapshot.CurrentAirportName;
                    return null;
                }
                if (!string.IsNullOrWhiteSpace(snapshot.CurrentAirportCode) && !string.Equals(definition.code, snapshot.CurrentAirportCode, StringComparison.OrdinalIgnoreCase))
                {
                    reason = "存档路由机场代码不匹配：route=" + definition.code + " checkpoint=" + snapshot.CurrentAirportCode;
                    return null;
                }
                if (snapshot.DestinationAirport >= 0)
                {
                    var destination = route.Airport(snapshot.DestinationAirport);
                    if (destination == null) { reason = "存档路由中不存在目的地机场 index=" + snapshot.DestinationAirport; return null; }
                    if (!string.IsNullOrWhiteSpace(snapshot.DestinationName) && !string.Equals(destination.name, snapshot.DestinationName, StringComparison.OrdinalIgnoreCase))
                    {
                        reason = "存档路由目的地名称不匹配：route=" + destination.name + " checkpoint=" + snapshot.DestinationName;
                        return null;
                    }
                }
                return definition;
            }
            catch (Exception e) { reason = "无法按存档 seed 生成机场定义：" + e.Message; return null; }
        }

        private static CheckpointSummary[] ListCheckpoints()
        {
            var result = new List<CheckpointSummary>();
            try
            {
                Directory.CreateDirectory(CheckpointDirectory);
                var seen = new HashSet<string>(StringComparer.Ordinal);
                foreach (var path in Directory.GetFiles(CheckpointDirectory, "*.json", SearchOption.TopDirectoryOnly))
                {
                    var id = Path.GetFileNameWithoutExtension(path);
                    if (System.Guid.TryParseExact(id, "N", out _) && seen.Add(id)) result.Add(ReadCheckpointSummary(id, path, false));
                }
                if (Directory.Exists(HostAdminCheckpointDirectory))
                    foreach (var path in Directory.GetFiles(HostAdminCheckpointDirectory, "*.json", SearchOption.TopDirectoryOnly))
                    {
                        var id = Path.GetFileNameWithoutExtension(path);
                        if (System.Guid.TryParseExact(id, "N", out _) && seen.Add(id)) result.Add(ReadCheckpointSummary(id, path, true));
                    }
                if (File.Exists(CheckpointPath)) result.Add(ReadCheckpointSummary("legacy", CheckpointPath, true));
                if (File.Exists(HostAdminLegacyCheckpointPath)) result.Add(ReadCheckpointSummary("hostadmin-legacy", HostAdminLegacyCheckpointPath, true));
            }
            catch (Exception e) { LogEvent("checkpoint-list-error", e.Message); }
            return result.OrderByDescending(x => x.savedUtc).ToArray();
        }

        private static CheckpointRequest ReadCheckpointRequest(HttpListenerRequest request)
        {
            using var reader = new StreamReader(request.InputStream, request.ContentEncoding);
            var text = reader.ReadToEnd();
            return string.IsNullOrWhiteSpace(text) ? new CheckpointRequest() : JsonSerializer.Deserialize<CheckpointRequest>(text);
        }

        private static string QueueWebCommand(string command)
        {
            var id = System.Guid.NewGuid().ToString("N");
            lock (Commands) Commands.Enqueue(new AdminCommand { Id = id, Text = command, Web = true });
            return JsonSerializer.Serialize(new { id, queued = true });
        }

        private static string BuildCommand(WebAction action)
        {
            if (action == null || !ulong.TryParse(action.steamId, out var id)) return string.Empty;
            switch ((action.action ?? string.Empty).ToLowerInvariant())
            {
                case "kick": return "kick " + id;
                case "ban": return "ban " + id;
                case "unban": return "unban " + id;
                default: return string.Empty;
            }
        }

        private static void RefreshWebSnapshot()
        {
            if (DateTime.UtcNow < NextStatusRefreshUtc) return;
            NextStatusRefreshUtc = DateTime.UtcNow.AddMilliseconds(500);
            try
            {
                var localConnection = Il2CppMirror.NetworkServer.localConnection;
                var players = Online.Select(x => new { name = x.Value.Name, steamId = x.Value.SteamId.ToString(), tag = PlayerTags.TryGetValue(x.Value.SteamId, out var tag) ? tag : string.Empty, connection = x.Key.connectionId, isHost = localConnection != null && x.Key.connectionId == localConnection.connectionId, profileUrl = "https://steamcommunity.com/profiles/" + x.Value.SteamId, addFriendUrl = "steam://friends/add/" + x.Value.SteamId }).ToArray();
                string[] audit;
                lock (StateLock) audit = Audit.Reverse().Take(30).ToArray();
                var gameState = Il2CppMayday.GameState.Instance;
                var lobbyService = LobbyService;
                var currentRoomName = lobbyService != null && lobbyService.InLobby && !string.IsNullOrWhiteSpace(lobbyService.LobbyName) ? lobbyService.LobbyName : RoomName.Value;
                var lobbyVisibility = lobbyService != null ? lobbyService.Visibility.ToString() : "Unknown";
                var lobbyAdvertise = lobbyService != null && lobbyService.Advertise;
                var lobbyId = lobbyService != null && lobbyService.InLobby ? lobbyService.Lobby.m_SteamID : 0UL;
                int lobbyMembers = -1, lobbyLimit = -1;
                var lobbyJoinable = false;
                if (lobbyId != 0)
                {
                    var lobby = new Il2CppSteamworks.CSteamID(lobbyId);
                    lobbyMembers = Il2CppSteamworks.SteamMatchmaking.GetNumLobbyMembers(lobby);
                    lobbyLimit = Il2CppSteamworks.SteamMatchmaking.GetLobbyMemberLimit(lobby);
                    lobbyJoinable = LastJoinableSetKnown && LastJoinableSet;
                }
                var managerLimit = Il2CppMirror.NetworkManager.singleton != null ? Il2CppMirror.NetworkManager.singleton.maxConnections : -1;
                var mirrorLimit = Il2CppMirror.NetworkServer.maxConnections;
                var mirrorConnections = Il2CppMirror.NetworkServer.connections != null ? Il2CppMirror.NetworkServer.connections.Count : 0;
                var checkpointCount = ListCheckpoints().Length;
                var json = JsonSerializer.Serialize(new { players, bans = Blacklist.OrderBy(x => x).Select(x => x.ToString()).ToArray(), roomName = currentRoomName, configuredRoomName = RoomName.Value, audit, onlineCount = players.Length, capacity = PlayerLimit.Value, serverActive = Il2CppMirror.NetworkServer.activeHost, mirrorLimit, managerLimit, mirrorConnections, lobbyId = lobbyId.ToString(), lobbyMembers, lobbyLimit, lobbyJoinable, lobbyVisibility, lobbyAdvertise, visibilitySetKnown = VisibilitySetKnown, visibilitySetSucceeded = VisibilitySetSucceeded, visibilityRequested = LastVisibilityRequested, visibilityChangedUtc = LastVisibilityChangedUtc == default ? string.Empty : LastVisibilityChangedUtc.ToString("O"), manualJoinAllowed = ManualJoinAllowed, phase = gameState != null ? gameState.Phase.ToString() : "Unknown", leg = gameState != null ? gameState.Leg : -1, money = gameState != null ? gameState.Money : 0, checkpointExists = checkpointCount > 0, checkpointCount, recoveryMessage = LastRecoveryMessage, recoveryStage = RecoveryStage, recoveryOperationId = ActiveRecovery?.Id ?? string.Empty, recoveryPoisoned = RecoveryPoisoned, recoveryPoisonReason = RecoveryPoisonReason, pendingPlayerRecovery = PendingPlayerRecovery.Count, currentFlight = gameState != null && gameState.Leg > 0 ? gameState.Leg : 0 });
                lock (StateLock) CachedStatusJson = json;
            }
            catch (Exception e) { LogEvent("status-snapshot-error", e.Message); }
        }

        private static void HandleWeb(HttpListenerContext context)
        {
            var request = context.Request;
            var expectedHost = "127.0.0.1:" + WebPort.Value;
            var origin = request.Headers["Origin"];
            if (!string.Equals(request.Headers["Host"], expectedHost, StringComparison.OrdinalIgnoreCase) ||
                ((request.HttpMethod == "POST" || request.HttpMethod == "DELETE") && origin != null && !string.Equals(origin, "http://" + expectedHost, StringComparison.OrdinalIgnoreCase)))
            {
                context.Response.StatusCode = 403;
                context.Response.Close();
                return;
            }
            var path = request.Url.AbsolutePath;
            string body;
            if (path == "/api/status")
            {
                lock (StateLock) body = CachedStatusJson;
                context.Response.ContentType = "application/json; charset=utf-8";
            }
            else if (path == "/api/diagnostic/latest" && request.HttpMethod == "GET")
            {
                if (string.IsNullOrEmpty(LastDiagnosticZip) || !File.Exists(LastDiagnosticZip)) { context.Response.StatusCode = 404; body = "诊断包不存在"; context.Response.ContentType = "text/plain; charset=utf-8"; }
                else
                {
                    context.Response.ContentType = "application/zip";
                    context.Response.AddHeader("Content-Disposition", "attachment; filename=\"" + Path.GetFileName(LastDiagnosticZip) + "\"");
                    context.Response.ContentLength64 = new FileInfo(LastDiagnosticZip).Length;
                    using var fs = File.OpenRead(LastDiagnosticZip);
                    fs.CopyTo(context.Response.OutputStream);
                    context.Response.Close();
                    return;
                }
            }
            else if (path == "/api/checkpoints" && request.HttpMethod == "GET")
            {
                body = JsonSerializer.Serialize(new { checkpoints = ListCheckpoints() });
                context.Response.ContentType = "application/json; charset=utf-8";
            }
            else if (path == "/api/checkpoints" && request.HttpMethod == "POST")
            {
                var data = ReadCheckpointRequest(request) ?? new CheckpointRequest();
                var name = SanitizeCheckpointName(data.name);
                var encodedName = name.Length == 0 ? "-" : Convert.ToBase64String(Encoding.UTF8.GetBytes(name));
                body = QueueWebCommand("checkpoint " + encodedName);
                context.Response.ContentType = "application/json; charset=utf-8";
            }
            else if (path == "/api/checkpoints/recover" && request.HttpMethod == "POST")
            {
                var data = ReadCheckpointRequest(request);
                body = data == null || ResolveCheckpointPath(data.id) == null ? "{\"error\":\"invalid_checkpoint\"}" : QueueWebCommand("recover " + data.id);
                if (data == null || ResolveCheckpointPath(data.id) == null) context.Response.StatusCode = 400;
                context.Response.ContentType = "application/json; charset=utf-8";
            }
            else if (path == "/api/checkpoints/delete" && request.HttpMethod == "POST")
            {
                var data = ReadCheckpointRequest(request);
                body = data == null || IsReadOnlyLegacyCheckpoint(data.id) || ResolveCheckpointPath(data.id) == null ? "{\"error\":\"invalid_checkpoint\"}" : QueueWebCommand("deletecheckpoint " + data.id);
                if (data == null || IsReadOnlyLegacyCheckpoint(data.id) || ResolveCheckpointPath(data.id) == null) context.Response.StatusCode = 400;
                context.Response.ContentType = "application/json; charset=utf-8";
            }
            else if (path == "/api/checkpoint" && request.HttpMethod == "POST")
            {
                body = QueueWebCommand("checkpoint -");
                context.Response.ContentType = "application/json; charset=utf-8";
            }
            else if (path == "/api/recover" && request.HttpMethod == "POST")
            {
                var latest = ListCheckpoints().FirstOrDefault(x => x.compatible);
                body = latest == null ? "{\"error\":\"no_compatible_checkpoint\"}" : QueueWebCommand("recover " + latest.id);
                if (latest == null) context.Response.StatusCode = 400;
                context.Response.ContentType = "application/json; charset=utf-8";
            }
            else if (path == "/api/export-diagnostics" && request.HttpMethod == "POST")
            {
                body = QueueWebCommand("exportlogs");
                context.Response.ContentType = "application/json; charset=utf-8";
            }
            else if (path == "/api/room-name" && request.HttpMethod == "POST")
            {
                using var reader = new StreamReader(request.InputStream, request.ContentEncoding);
                var requestedName = SanitizeRoomName(reader.ReadToEnd());
                var id = System.Guid.NewGuid().ToString("N");
                var encodedName = requestedName.Length == 0 ? "-" : Convert.ToBase64String(Encoding.UTF8.GetBytes(requestedName));
                lock (Commands) Commands.Enqueue(new AdminCommand { Id = id, Text = "setroom " + encodedName, Web = true });
                body = JsonSerializer.Serialize(new { id, queued = true, roomName = requestedName, applies = "next_lobby" });
                context.Response.ContentType = "application/json; charset=utf-8";
            }
            else if (path == "/api/room-capacity" && request.HttpMethod == "POST")
            {
                using var reader = new StreamReader(request.InputStream, request.ContentEncoding);
                var control = JsonSerializer.Deserialize<RoomControlRequest>(reader.ReadToEnd());
                if (control == null || control.capacity < 1 || control.capacity > 250) { context.Response.StatusCode = 400; body = "{\"error\":\"capacity_must_be_1_to_250\"}"; }
                else body = QueueWebCommand("setcapacity " + control.capacity);
                context.Response.ContentType = "application/json; charset=utf-8";
            }
            else if (path == "/api/room-joinable" && request.HttpMethod == "POST")
            {
                using var reader = new StreamReader(request.InputStream, request.ContentEncoding);
                var control = JsonSerializer.Deserialize<RoomControlRequest>(reader.ReadToEnd());
                if (control == null) { context.Response.StatusCode = 400; body = "{\"error\":\"invalid_joinable_request\"}"; }
                else body = QueueWebCommand("setjoinable " + (control.joinable ? "true" : "false"));
                context.Response.ContentType = "application/json; charset=utf-8";
            }
            else if (path == "/api/room-visibility" && request.HttpMethod == "POST")
            {
                using var reader = new StreamReader(request.InputStream, request.ContentEncoding);
                var control = JsonSerializer.Deserialize<RoomControlRequest>(reader.ReadToEnd());
                var visibility = control?.visibility ?? string.Empty;
                if (visibility != "Private" && visibility != "FriendsOnly" && visibility != "Public") { context.Response.StatusCode = 400; body = "{\"error\":\"invalid_visibility\"}"; }
                else body = QueueWebCommand("setvisibility " + visibility);
                context.Response.ContentType = "application/json; charset=utf-8";
            }
            else if (path == "/api/kick-all" && request.HttpMethod == "POST")
            {
                body = QueueWebCommand("kickall");
                context.Response.ContentType = "application/json; charset=utf-8";
            }
            else if (path == "/api/player-tag" && request.HttpMethod == "POST")
            {
                using var reader = new StreamReader(request.InputStream, request.ContentEncoding);
                var data = JsonSerializer.Deserialize<PlayerTagRequest>(reader.ReadToEnd());
                if (data == null || string.IsNullOrWhiteSpace(data.steamId) || !ulong.TryParse(data.steamId, out var tagId) || tagId == 0)
                {
                    context.Response.StatusCode = 400;
                    body = "{\"error\":\"invalid_player_tag_request\"}";
                }
                else
                {
                    var rawTag = data.tag ?? string.Empty;
                    var encodedTag = Convert.ToBase64String(Encoding.UTF8.GetBytes(rawTag));
                    body = QueueWebCommand("settag " + tagId + " " + encodedTag);
                }
                context.Response.ContentType = "application/json; charset=utf-8";
            }
            else if (path == "/api/action" && request.HttpMethod == "POST")
            {
                using var reader = new StreamReader(request.InputStream, request.ContentEncoding);
                var action = JsonSerializer.Deserialize<WebAction>(reader.ReadToEnd());
                var id = System.Guid.NewGuid().ToString("N");
                var command = action == null ? string.Empty : BuildCommand(action);
                if (command.Length == 0) { context.Response.StatusCode = 400; body = "{\"error\":\"invalid_action\"}"; }
                else
                {
                    lock (Commands) Commands.Enqueue(new AdminCommand { Id = id, Text = command, Web = true });
                    body = JsonSerializer.Serialize(new { id, queued = true });
                }
                context.Response.ContentType = "application/json; charset=utf-8";
            }
            else if (path.StartsWith("/api/result/") && request.HttpMethod == "GET")
            {
                var id = path.Substring("/api/result/".Length);
                lock (StateLock) body = JsonSerializer.Serialize(new { done = WebResults.TryGetValue(id, out var result), result });
                context.Response.ContentType = "application/json; charset=utf-8";
            }
            else if (path == "/api/command" && request.HttpMethod == "POST")
            {
                using var reader = new StreamReader(request.InputStream, request.ContentEncoding);
                var command = reader.ReadToEnd();
                lock (Commands) Commands.Enqueue(new AdminCommand { Id = System.Guid.NewGuid().ToString("N"), Text = command, Web = false });
                body = "{\"queued\":true}";
                context.Response.ContentType = "application/json; charset=utf-8";
            }
            else
            {
                body = WebPage;
                context.Response.ContentType = "text/html; charset=utf-8";
            }
            var bytes = Encoding.UTF8.GetBytes(body);
            context.Response.ContentLength64 = bytes.Length;
            using var output = context.Response.OutputStream;
            output.Write(bytes, 0, bytes.Length);
        }

        private static readonly string WebPage = AdminWebPage.Html;

        private static void ReadConsole()
        {
            try
            {
                while (Running)
                {
                    var line = Console.ReadLine();
                    if (line == null) break;
                    lock (Commands) Commands.Enqueue(new AdminCommand { Id = System.Guid.NewGuid().ToString("N"), Text = line, Web = false });
                }
            }
            catch (Exception e) { Log?.LogDebug("[MaydayHostControl] 控制台输入不可用: " + e.Message); }
        }

        internal static void PumpCommands()
        {
            RefreshWebSnapshot();
            PumpRecoveryOperation();
            PumpPlayerRecovery();
            PumpFileCommands();
            while (true)
            {
                AdminCommand command;
                lock (Commands) { if (Commands.Count == 0) return; command = Commands.Dequeue(); }
                var result = Execute(command.Text);
                NextStatusRefreshUtc = DateTime.MinValue;
                RefreshWebSnapshot();
                if (command.Web) CompleteWebCommand(command, result);
            }
        }

        private static void ClearInventoryForRestore(Il2CppMayday.PlayerInventory inventory)
        {
            if (inventory == null || inventory.slots == null) return;
            var ids = new List<uint>();
            for (var i = 0; i < inventory.slots.Count; i++) if (inventory.slots[i] != 0) ids.Add(inventory.slots[i]);
            foreach (var netId in ids.Distinct())
            {
                Il2CppMayday.Item item = null;
                if (Il2CppMirror.NetworkServer.spawned != null && Il2CppMirror.NetworkServer.spawned.TryGetValue(netId, out var identity) && identity != null)
                    item = FindNetworkItem(identity);
                inventory.ServerForget(netId);
                item?.ServerDestroy();
            }
            inventory.Networkactive = 0;
        }

        private static void ClearPlayerRecovery(string reason)
        {
            PendingPlayerRecovery.Clear();
            PlayerRestoreStarted.Clear();
            PlayerRestoreQueue.Clear();
            PlayerItemsRemaining.Clear();
            PlayerRestoreIndex.Clear();
            PlayerRestoreDelegates.Clear();
            PlayerRestoreDeadlines.Clear();
            PlayerRestoreAttempts.Clear();
            PlayerRecoveryGeneration = -1;
            LogEvent("player-recovery-cleared", "reason=" + reason);
        }

        internal static void OnServerStarted()
        {
            ServerGeneration++;
            RecoveryPoisoned = false;
            RecoveryPoisonReason = string.Empty;
            ActiveRecovery = null;
            RecoveryStage = "IDLE";
            ManualJoinAllowed = DefaultRoomJoinable.Value;
            VisibilitySetKnown = false;
            VisibilitySetSucceeded = false;
            LastVisibilityRequested = string.Empty;
            LastVisibilityChangedUtc = default;
            ClearPlayerRecovery("server-start generation=" + ServerGeneration);
            SyncMirrorCapacity("OnStartServer");
        }

        internal static void OnServerStopped()
        {
            ServerGeneration++;
            ClearPlayerRecovery("server-stop generation=" + ServerGeneration);
            RecoveryPoisoned = false;
            RecoveryPoisonReason = string.Empty;
            ActiveRecovery = null;
            RecoveryStage = "IDLE";
        }

        private static void MarkRecoveryPoisoned(string reason)
        {
            RecoveryPoisoned = true;
            ManualJoinAllowed = false;
            RecoveryPoisonReason = reason ?? "恢复未完整完成";
            try
            {
                var service = LobbyService;
                if (service != null && service.InLobby && service.Lobby.IsValid())
                {
                    var closed = Il2CppSteamworks.SteamMatchmaking.SetLobbyJoinable(service.Lobby, false);
                    if (closed) LastJoinableSet = false;
                    LastJoinableSetKnown = closed;
                }
            }
            catch (Exception e) { LogEvent("recovery-lockdown-error", e.Message); }
            LogEvent("recovery-lockdown", "generation=" + ServerGeneration + " reason=" + RecoveryPoisonReason);
        }

        internal static void PumpPlayerRecovery()
        {
            if (PlayerRecoveryGeneration != ServerGeneration)
            {
                if (PendingPlayerRecovery.Count != 0) ClearPlayerRecovery("generation-mismatch");
                return;
            }
            if (!Il2CppMirror.NetworkServer.activeHost || PendingPlayerRecovery.Count == 0) return;
            var now = DateTime.UtcNow;
            foreach (var steamId in PlayerRestoreDeadlines.Where(x => x.Value <= now).Select(x => x.Key).ToArray())
            {
                PlayerRestoreDeadlines.Remove(steamId);
                PlayerRestoreDelegates.Remove(steamId);
                PlayerRestoreStarted.Remove(steamId);
                PlayerRestoreIndex.Remove(steamId);
                PlayerRestoreAttempts.Remove(steamId);
                PendingPlayerRecovery.Remove(steamId);
                RecoveryStage = "PLAYER_PARTIAL";
                LastRecoveryMessage = "场景已恢复，但至少一名玩家的背包恢复超时；请查看诊断日志";
                LogEvent("player-item-restore-timeout", "steam=" + steamId + " generation=" + ServerGeneration);
            }
            var plane = Il2CppMayday.Flight.PlaneFlight.Instance;
            var all = Il2CppMayday.PlayerNet.All;
            if (plane == null || all == null) return;
            for (var i = 0; i < all.Count; i++)
            {
                var player = all[i];
                if (player == null || player.SteamId == 0 || player.Inventory == null || !PendingPlayerRecovery.TryGetValue(player.SteamId, out var saved) || PlayerRestoreStarted.Contains(player.SteamId)) continue;
                PlayerRestoreStarted.Add(player.SteamId);
                try
                {
                    player.ServerReleaseSeat();
                    ClearInventoryForRestore(player.Inventory);
                    var inventory = (saved.Inventory ?? Array.Empty<InventoryItemCheckpoint>()).Where(x => x?.Item != null).OrderByDescending(x => x.SlotOrder).Take(Il2CppMayday.PlayerInventory.SlotCount).ToArray();
                    LogEvent("player-inventory-restore-plan", "steam=" + player.SteamId + " giveOrder=" + string.Join(",", inventory.Select(x => x.Item.ItemId)) + " finalOrder=" + string.Join(",", inventory.OrderBy(x => x.SlotOrder).Select(x => x.Item.ItemId)));
                    PlayerRestoreIndex[player.SteamId] = 0;
                    var attempt = ++PlayerRestoreAttemptCounter;
                    PlayerRestoreAttempts[player.SteamId] = attempt;
                    GiveNextPlayerItem(player, saved, plane, inventory, attempt);
                }
                catch (Exception e)
                {
                    LogEvent("player-restore-error", "steam=" + player.SteamId + " error=" + e);
                    PlayerRestoreStarted.Remove(player.SteamId);
                }
            }
        }

        private static void GiveNextPlayerItem(Il2CppMayday.PlayerNet player, PlayerCheckpoint saved, Il2CppMayday.Flight.PlaneFlight plane, InventoryItemCheckpoint[] inventory, long attempt)
        {
            if (player == null || saved == null || plane == null) return;
            var index = PlayerRestoreIndex.TryGetValue(saved.SteamId, out var current) ? current : 0;
            if (index >= inventory.Length) { FinishPlayerRecovery(player, saved, plane); return; }
            var state = inventory[index].Item;
            var callbackCalled = false;
            var generation = ServerGeneration;
            System.Action<Il2CppMayday.Item> managed = item =>
            {
                callbackCalled = true;
                if (generation != ServerGeneration || PlayerRecoveryGeneration != generation || !PendingPlayerRecovery.ContainsKey(saved.SteamId) || !PlayerRestoreAttempts.TryGetValue(saved.SteamId, out var currentAttempt) || currentAttempt != attempt || !PlayerRestoreIndex.TryGetValue(saved.SteamId, out var expectedIndex) || expectedIndex != index)
                {
                    LogEvent("player-item-restore-late-callback", "steam=" + saved.SteamId + " item=" + state.ItemId + " generation=" + generation + " current=" + ServerGeneration);
                    return;
                }
                PlayerRestoreDeadlines.Remove(saved.SteamId);
                PlayerRestoreDelegates.Remove(saved.SteamId);
                try { if (item != null) ApplyItemSnapshot(item, state); }
                catch (Exception e) { LogEvent("player-item-restore-error", "steam=" + saved.SteamId + " item=" + state.ItemId + " error=" + e.Message); }
                PlayerRestoreIndex[saved.SteamId] = index + 1;
                GiveNextPlayerItem(player, saved, plane, inventory, attempt);
            };
            try
            {
                var init = DelegateSupport.ConvertDelegate<Il2CppSystem.Action<Il2CppMayday.Item>>(managed);
                PlayerRestoreDelegates[saved.SteamId] = init;
                PlayerRestoreDeadlines[saved.SteamId] = DateTime.UtcNow.AddSeconds(8);
                var given = player.Inventory.ServerGive(state.ItemId, init);
                if (given == null && !callbackCalled)
                {
                    PlayerRestoreDeadlines.Remove(saved.SteamId);
                    PlayerRestoreDelegates.Remove(saved.SteamId);
                    LogEvent("player-item-restore-skip", "steam=" + saved.SteamId + " item=" + state.ItemId + " reason=ServerGive returned null");
                    PlayerRestoreIndex[saved.SteamId] = index + 1;
                    GiveNextPlayerItem(player, saved, plane, inventory, attempt);
                }
            }
            catch (Exception e)
            {
                PlayerRestoreDeadlines.Remove(saved.SteamId);
                PlayerRestoreDelegates.Remove(saved.SteamId);
                LogEvent("player-item-restore-error", "steam=" + saved.SteamId + " item=" + state.ItemId + " error=" + e.Message);
                PlayerRestoreIndex[saved.SteamId] = index + 1;
                GiveNextPlayerItem(player, saved, plane, inventory, attempt);
            }
        }

        private static Il2CppMayday.ISeat FindSeatBySavedKey(PlayerCheckpoint saved)
        {
            if (saved == null) return null;
            var interactables = UnityEngine.Object.FindObjectsOfType<Il2CppMayday.Interactable>(true);
            foreach (var interactable in interactables)
            {
                if (interactable == null) continue;
                var seat = interactable.GetComponent<Il2CppMayday.Passengers.PassengerSeat>();
                if (seat != null && saved.SeatIndex >= 0 && seat.GlobalIndex == saved.SeatIndex)
                {
                    var asSeat = interactable.GetComponent<Il2CppMayday.ISeat>();
                    if (asSeat != null) return asSeat;
                }
                var pilot = interactable.GetComponent<Il2CppMayday.Flight.PilotSeat>();
                if (pilot != null && (byte)pilot.Kind == saved.SeatKind)
                {
                    var asSeat = interactable.GetComponent<Il2CppMayday.ISeat>();
                    if (asSeat != null) return asSeat;
                }
            }
            return null;
        }

        private static void FinishPlayerRecovery(Il2CppMayday.PlayerNet player, PlayerCheckpoint saved, Il2CppMayday.Flight.PlaneFlight plane)
        {
            if (player == null || saved == null || plane == null) return;
            var local = new Vector3(saved.X, saved.Y, saved.Z);
            Transform anchor = plane.transform;
            if (saved.PositionAnchor == "airport")
            {
                var state = Il2CppMayday.GameState.Instance;
                var site = state != null ? Il2CppMayday.Airport.AirportSites.Find(state.CurrentAirport) : null;
                if (site != null && site.spawnPoint != null) anchor = site.spawnPoint;
                if (local.sqrMagnitude > 10000f) local = Vector3.zero;
            }
            else if (Math.Abs(local.x) > 7.5f || local.y < -1f || local.y > 5.5f || Math.Abs(local.z) > 13f) local = new Vector3(0f, 1.2f, -4f);
            var orderedInventory = (saved.Inventory ?? Array.Empty<InventoryItemCheckpoint>()).Where(x => x != null && x.Item != null).OrderBy(x => x.SlotOrder).Take(Il2CppMayday.PlayerInventory.SlotCount).ToArray();
            var activeIndex = Math.Max(0, Math.Min(saved.ActiveSlot, Math.Max(0, orderedInventory.Length - 1)));
            player.Inventory.Networkactive = Math.Max(0, Math.Min(activeIndex, Math.Max(0, Il2CppMayday.PlayerInventory.SlotCount - 1)));
            player.ServerTeleport(anchor.TransformPoint(local), anchor.eulerAngles.y + saved.Heading);
            if (saved.ActiveItemWasHeld && !string.IsNullOrWhiteSpace(saved.HeldItemId) && player.Inventory.Held != null)
            {
                try { player.Inventory.Held.ServerSetHeld(player, activeIndex); }
                catch (Exception e) { LogEvent("player-held-restore-error", "steam=" + saved.SteamId + " item=" + saved.HeldItemId + " error=" + e.Message); }
            }
            if (saved.WasSeated)
            {
                var seat = FindSeatBySavedKey(saved);
                if (seat != null && player.ServerSit(seat)) LogEvent("player-seat-restored", "steam=" + saved.SteamId + " seat=" + saved.SeatIndex);
                else LogEvent("player-seat-restore-skip", "steam=" + saved.SteamId + " kind=" + saved.SeatKind + " index=" + saved.SeatIndex);
            }
            PendingPlayerRecovery.Remove(saved.SteamId);
            PlayerItemsRemaining.Remove(saved.SteamId);
            PlayerRestoreIndex.Remove(saved.SteamId);
            PlayerRestoreDelegates.Remove(saved.SteamId);
            PlayerRestoreDeadlines.Remove(saved.SteamId);
            PlayerRestoreAttempts.Remove(saved.SteamId);
            if (PendingPlayerRecovery.Count == 0 && RecoveryStage == "PLAYER_PENDING")
            {
                RecoveryStage = "SUCCEEDED";
                LastRecoveryMessage = "场景和所有已保存玩家均已恢复";
            }
            LogEvent("player-restored", "steam=" + saved.SteamId + " name=" + saved.DisplayName + " inventory=" + (saved.Inventory?.Length ?? 0) + " pending=" + PendingPlayerRecovery.Count);
        }

        private static void PumpFileCommands()
        {
            string[] lines;
            try { lines = File.ReadAllLines(CommandPath); }
            catch (Exception e) { Log.LogWarning("[MaydayHostControl] 读取命令文件失败: " + e.Message); return; }

            var pending = lines.Where(x => !string.IsNullOrWhiteSpace(x) && !x.TrimStart().StartsWith("#")).ToArray();
            if (pending.Length == 0) return;
            try { File.WriteAllText(CommandPath, "# 每行一条命令，例如 players\r\n"); }
            catch (Exception e) { Log.LogWarning("[MaydayHostControl] 清空命令文件失败: " + e.Message); }
            foreach (var line in pending)
            {
                var command = new AdminCommand { Id = System.Guid.NewGuid().ToString("N"), Text = line, Web = false };
                Execute(command.Text);
            }
        }

        private static void CompleteWebCommand(AdminCommand command, string result)
        {
            lock (StateLock)
            {
                WebResults[command.Id] = result;
                WebResultOrder.Enqueue(command.Id);
                while (WebResultOrder.Count > 64)
                {
                    var expired = WebResultOrder.Dequeue();
                    WebResults.Remove(expired);
                }
                Audit.Enqueue(DateTime.Now.ToString("HH:mm:ss") + " " + result);
                while (Audit.Count > 100) Audit.Dequeue();
            }
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private static bool IsAirborneSafetySnapshot(FlightCheckpoint snapshot)
        {
            return snapshot != null && snapshot.SchemaVersion >= 5 &&
                string.Equals(snapshot.CaptureMode, "Airborne", StringComparison.Ordinal) &&
                string.Equals(snapshot.RecoveryMode, "SafeOriginPreShift", StringComparison.Ordinal);
        }

        private static FlightPhysicsCheckpoint CaptureFlightPhysics(Il2CppMayday.Flight.PlaneFlight plane)
        {
            if (plane == null || plane.sim == null) return null;
            var sim = plane.sim;
            return new FlightPhysicsCheckpoint
            {
                X = sim.Pos.x,
                Y = sim.Pos.y,
                Z = sim.Pos.z,
                VelocityX = sim.Velocity.x,
                VelocityY = sim.Velocity.y,
                VelocityZ = sim.Velocity.z,
                Heading = sim.Heading,
                Pitch = sim.Pitch,
                Roll = sim.Roll,
                PitchRate = sim.PitchRate,
                RollRate = sim.RollRate,
                YawRate = sim.YawRate,
                Speed = sim.Speed,
                Track = sim.Track,
                Gamma = sim.Gamma,
                OnGround = sim.OnGround
            };
        }

        private static bool ValidItemCheckpoint(ItemCheckpoint item, bool cabinPose)
        {
            if (item == null || !IsSupportedInventoryItemId(item.ItemId) || string.IsNullOrWhiteSpace(item.RuntimeType) || item.RuntimeType.Length > 256) return false;
            if (!Finite(item.X) || !Finite(item.Y) || !Finite(item.Z) || !Finite(item.Qx) || !Finite(item.Qy) || !Finite(item.Qz) || !Finite(item.Qw) || !Finite(item.Amount) ||
                double.IsNaN(item.HotUntil) || double.IsInfinity(item.HotUntil) || item.HotUntil < 0d || item.Count < 0 || item.Count > 10000) return false;
            var quaternionLength = item.Qx * item.Qx + item.Qy * item.Qy + item.Qz * item.Qz + item.Qw * item.Qw;
            if (quaternionLength < 0.25f || quaternionLength > 4f) return false;
            return !cabinPose || Math.Abs(item.X) <= 7.5f && item.Y >= -1f && item.Y <= 5.5f && Math.Abs(item.Z) <= 13f;
        }

        private static bool ValidFlightPhysics(FlightPhysicsCheckpoint physics)
        {
            if (physics == null) return false;
            var values = new[]
            {
                physics.X, physics.Y, physics.Z, physics.VelocityX, physics.VelocityY, physics.VelocityZ,
                physics.Heading, physics.Pitch, physics.Roll, physics.PitchRate, physics.RollRate,
                physics.YawRate, physics.Speed, physics.Track, physics.Gamma
            };
            return values.All(Finite) && Math.Abs(physics.X) <= 1000000f && Math.Abs(physics.Y) <= 100000f &&
                Math.Abs(physics.Z) <= 1000000f && physics.Speed >= 0f && physics.Speed <= 1000f &&
                Math.Abs(physics.PitchRate) <= 720f && Math.Abs(physics.RollRate) <= 720f && Math.Abs(physics.YawRate) <= 720f;
        }

        private static float[] CopySyncList(Il2CppMirror.SyncList<float> list)
        {
            if (list == null) return Array.Empty<float>();
            var values = new float[list.Count];
            for (var i = 0; i < values.Length; i++) values[i] = list[i];
            return values;
        }

        private static void RestoreSyncList(Il2CppMirror.SyncList<float> list, float[] values)
        {
            if (list == null) throw new InvalidOperationException("SyncList 未初始化");
            list.Clear();
            if (values == null) return;
            foreach (var value in values.Take(512)) list.Add(value);
        }

        private static float[] CopyVector4List(Il2CppMirror.SyncList<UnityEngine.Vector4> list)
        {
            if (list == null) return Array.Empty<float>();
            var values = new float[list.Count * 4];
            for (var i = 0; i < list.Count; i++)
            {
                var v = list[i];
                values[i * 4] = v.x; values[i * 4 + 1] = v.y; values[i * 4 + 2] = v.z; values[i * 4 + 3] = v.w;
            }
            return values;
        }

        private static Il2CppMayday.Item FindNetworkItem(Il2CppMirror.NetworkIdentity identity)
        {
            if (identity == null) return null;
            return identity.GetComponent<Il2CppMayday.Item>() ?? identity.GetComponentInChildren<Il2CppMayday.Item>(true);
        }

        private static bool IsUnsafePersistentItemId(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return true;
            var key = id.ToLowerInvariant();
            return new[] { "bomb", "hijack", "pax", "player", "luggage", "vest", "valuable", "cigarette", "cookable", "battery", "defib", "camera", "seatkit", "sticky", "carry" }.Any(x => key.Contains(x));
        }

        private static bool IsSupportedInventoryItemId(string id)
        {
            if (string.IsNullOrWhiteSpace(id) || id.Length > 128) return false;
            if (string.Equals(id, "cigarette_pack", StringComparison.OrdinalIgnoreCase)) return true;
            if (IsUnsafePersistentItemId(id)) return false;
            return id.All(c => !char.IsControl(c) && (char.IsLetterOrDigit(c) || c == '_' || c == '-' || c == '.' || c == '/'));
        }

        private static bool IsSupportedItemId(string id)
        {
            if (!IsSupportedInventoryItemId(id)) return false;
            if (!ItemCatalogReady) { Il2CppMayday.ItemCatalog.Build(); ItemCatalogReady = true; }
            if (string.Equals(id, "hammer", StringComparison.OrdinalIgnoreCase)) return true;
            if (!Il2CppMayday.ItemCatalog.Implemented(id)) return false;
            var info = Il2CppMayday.ItemCatalog.Get(id);
            if (info == null) return false;
            var category = info.category.ToString();
            return category == "Cabin" || category == "Galley" || category == "Cleaning" || category == "Maintenance" || category == "Misc";
        }

        private static bool IsUnsafePersistentItem(Il2CppMayday.Item item)
        {
            if (item == null) return true;
            if (string.Equals(item.ItemId, "cigarette_pack", StringComparison.OrdinalIgnoreCase) && item.GetType() == typeof(Il2CppMayday.Item)) return false;
            return item is Il2CppMayday.Passengers.LuggageItem || item is Il2CppMayday.Passengers.PaxCarryItem || item is Il2CppMayday.Players.PlayerCarryItem || item is Il2CppMayday.Players.LifeVestItem || item is Il2CppMayday.Players.DefibItem || item is Il2CppMayday.Systems.HijackBombItem || item is Il2CppMayday.Service.CookableItem || item is Il2CppMayday.Service.CigaretteItem || item is Il2CppMayday.Players.CameraItem || item is Il2CppMayday.Systems.BatteryItem || item is Il2CppMayday.Flight.FlightManualItem;
        }

        private static bool IsSupportedInventoryItem(Il2CppMayday.Item item)
        {
            return !IsUnsafePersistentItem(item) && IsSupportedInventoryItemId(item.ItemId);
        }

        private static bool IsSupportedCabinItem(Il2CppMayday.Item item)
        {
            return IsSupportedInventoryItem(item);
        }

        private static ItemCheckpoint[] CaptureCabinItems(Il2CppMayday.Flight.PlaneFlight plane)
        {
            var result = new List<ItemCheckpoint>();
            var capturedInstances = new HashSet<int>();
            var skipped = 0;
            var spawned = Il2CppMirror.NetworkServer.spawned;
            var planeTransform = plane.transform;
            if (spawned != null)
            {
                foreach (var entry in spawned)
                {
                    try
                    {
                        var identity = entry.Value;
                        if (identity == null) continue;
                        var item = FindNetworkItem(identity);
                        if (item == null) continue;
                        if (!item.InWorld || !item.IsFree || item.IsHeld || item.InSocket || item.IsUsing) continue;
                        if (!IsSupportedCabinItem(item)) { skipped++; continue; }
                        var worldPosition = item.NetworksyncPos;
                        var localPosition = planeTransform.InverseTransformPoint(worldPosition);
                        if (Math.Abs(localPosition.x) > 7.5f || localPosition.y < -1f || localPosition.y > 5.5f || Math.Abs(localPosition.z) > 13f) continue;
                        var localRotation = Quaternion.Inverse(planeTransform.rotation) * item.NetworksyncRot;
                        var saved = SnapshotItem(item, localPosition, localRotation);
                        if (saved != null) { result.Add(saved); capturedInstances.Add(item.GetInstanceID()); }
                    }
                    catch (Exception e) { LogEvent("item-snapshot-skip", e.Message); }
                }
            }
            foreach (var sceneItem in UnityEngine.Object.FindObjectsOfType<Il2CppMayday.Item>(true))
            {
                try
                {
                    if (sceneItem == null || capturedInstances.Contains(sceneItem.GetInstanceID())) continue;
                    LogEvent("scene-item-candidate", "id=" + sceneItem.ItemId + " type=" + sceneItem.GetType().FullName + " inWorld=" + sceneItem.InWorld + " free=" + sceneItem.IsFree + " held=" + sceneItem.IsHeld + " socket=" + sceneItem.InSocket + " using=" + sceneItem.IsUsing);
                    if ((!sceneItem.InWorld && !sceneItem.IsFree) || sceneItem.IsHeld || sceneItem.InSocket || sceneItem.IsUsing || !IsSupportedCabinItem(sceneItem)) continue;
                    var worldPosition = sceneItem.NetworksyncPos;
                    var localPosition = planeTransform.InverseTransformPoint(worldPosition);
                    if (Math.Abs(localPosition.x) > 7.5f || localPosition.y < -1f || localPosition.y > 5.5f || Math.Abs(localPosition.z) > 13f) continue;
                    var localRotation = Quaternion.Inverse(planeTransform.rotation) * sceneItem.NetworksyncRot;
                    var saved = SnapshotItem(sceneItem, localPosition, localRotation);
                    if (saved != null) { saved.SceneOwned = true; result.Add(saved); capturedInstances.Add(sceneItem.GetInstanceID()); }
                }
                catch (Exception e) { LogEvent("scene-item-snapshot-skip", e.Message); }
            }
            LogEvent("item-snapshot", "captured=" + result.Count + " skippedSpecial=" + skipped);
            return result.ToArray();
        }

        private static void ApplyItemSnapshot(Il2CppMayday.Item item, ItemCheckpoint saved)
        {
            if (item == null || saved == null) return;
            item.NetworkState = saved.State;
            item.NetworkAmount = saved.Amount;
            item.NetworkCount = saved.Count;
            item.NetworkTag = saved.Tag;
            item.NetworkTint = new Color32(saved.TintR, saved.TintG, saved.TintB, saved.TintA);
            if (item is Il2CppMayday.Service.CupItem cup) cup.NetworkhotUntil = Time.time + Math.Max(0d, saved.HotUntil);
            if (item is Il2CppMayday.Service.EdibleItem edible) edible.NetworkeatPct = saved.EatPercent;
            if (item is Il2CppMayday.Systems.JerryCanItem can) can.Networkplug = saved.JerryCanPlug;
            if (item is Il2CppMayday.Systems.SprayCanTool spray) spray.NetworkaccentMode = saved.AccentMode;
        }

        private static int RestoreBaggageCarts(BaggageCartCheckpoint[] savedCarts, bool allowNativeUnavailable, out int unavailable)
        {
            var restored = 0;
            unavailable = 0;
            foreach (var saved in (savedCarts ?? Array.Empty<BaggageCartCheckpoint>()).Take(64))
            {
                var cart = saved.Arrival ? Il2CppMayday.Airport.BaggageCart.Arrival : Il2CppMayday.Airport.BaggageCart.Departure;
                if (cart == null || Il2CppMayday.Airport.GroundService.SiteIndex != saved.SiteIndex || !string.Equals(Il2CppMayday.Airport.BaggageCart.NameTerminal, saved.Terminal, StringComparison.Ordinal) || !string.Equals(Il2CppMayday.Airport.BaggageCart.NameStand, saved.Stand, StringComparison.Ordinal))
                {
                    if (allowNativeUnavailable)
                    {
                        unavailable++;
                        LogEvent("baggage-cart-restore-unavailable", "site=" + saved.SiteIndex + " arrival=" + saved.Arrival + " reason=native_unavailable_in_preshift");
                    }
                    else LogEvent("baggage-cart-restore-skip", "site=" + saved.SiteIndex + " arrival=" + saved.Arrival + " reason=not_found_or_site_mismatch");
                    continue;
                }
                try
                {
                    cart.ServerDropLinks();
                    cart.ServerClearRiders();
                    cart.Networkpusher = 0;
                    cart.NetworkleaderCart = 0;
                    if (saved.AtAnchor) cart.ServerResetToAnchor();
                    else
                    {
                        cart.ServerLeaveAnchor();
                        var pos = new Vector3(saved.X, saved.Y, saved.Z);
                        cart.ServerTowPose(pos, saved.Yaw);
                        cart.NetworksitePos = pos;
                        cart.NetworksiteYaw = saved.Yaw;
                        cart.NetworkatAnchor = false;
                    }
                    restored++;
                }
                catch (Exception e) { LogEvent("baggage-cart-restore-error", "site=" + saved.SiteIndex + " error=" + e.Message); }
            }
            return restored;
        }

        private static int RestoreTrolleys(Il2CppMayday.Flight.PlaneFlight plane, TrolleyCheckpoint[] savedTrolleys)
        {
            var trolley = Il2CppMayday.Service.DrinkTrolley.Instance;
            if (trolley == null || savedTrolleys == null || savedTrolleys.Length == 0) return 0;
            var saved = savedTrolleys[0];
            if (trolley.slots != null)
                for (var i = 0; i < trolley.slots.Length; i++)
                {
                    var socket = trolley.slots[i];
                    var current = socket != null ? socket.Current : null;
                    if (socket != null && current != null)
                    {
                        socket.ServerEject(Vector3.zero);
                        current.ServerDestroy();
                    }
                }
            trolley.ServerRelease();
            trolley.ServerResetPark();
            var localPos = new Vector3(saved.X, saved.Y, saved.Z);
            var worldPos = plane.transform.TransformPoint(localPos);
            var worldRot = plane.transform.rotation * new Quaternion(saved.Qx, saved.Qy, saved.Qz, saved.Qw);
            trolley.Networkside = saved.Side;
            trolley.Networkbraked = saved.Braked;
            trolley.NetworkpusherId = 0;
            trolley.SetPose(worldPos, true);
            trolley.transform.rotation = worldRot;
            trolley.Networkpose = worldPos;
            var restored = 0;
            foreach (var slotEntry in (saved.Slots ?? Array.Empty<TrolleySlotCheckpoint>()).Take(32))
            {
                if (trolley.slots == null || slotEntry.Index < 0 || slotEntry.Index >= trolley.slots.Length || slotEntry.Item == null) continue;
                var socket = trolley.slots[slotEntry.Index];
                if (socket == null || !string.Equals(socket.placeKey ?? string.Empty, slotEntry.PlaceKey ?? string.Empty, StringComparison.Ordinal)) continue;
                try
                {
                    var anchor = socket.Anchor != null ? socket.Anchor : trolley.transform;
                    var managed = new System.Action<Il2CppMayday.Item>(item =>
                    {
                        if (item == null || !IsSupportedInventoryItem(item)) { item?.ServerDestroy(); return; }
                        ApplyItemSnapshot(item, slotEntry.Item);
                    });
                    var init = DelegateSupport.ConvertDelegate<Il2CppSystem.Action<Il2CppMayday.Item>>(managed);
                    var pos = anchor.position;
                    var item = Il2CppMayday.Item.ServerSpawn(slotEntry.Item.ItemId, pos, anchor.rotation, init, true);
                    if (item != null && socket.ServerInsert(item)) restored++;
                    else item?.ServerDestroy();
                }
                catch (Exception e) { LogEvent("trolley-slot-restore-error", "slot=" + slotEntry.Index + " item=" + slotEntry.Item.ItemId + " error=" + e.Message); }
            }
            LogEvent("trolley-restored", "slots=" + restored + " side=" + saved.Side + " braked=" + saved.Braked);
            return restored;
        }

        private static int RestoreCabinItems(Il2CppMayday.Flight.PlaneFlight plane, ItemCheckpoint[] items)
        {
            var restored = 0;
            if (items == null || items.Length == 0) return restored;
            Il2CppMayday.ItemCatalog.Build();
            var toRemove = new List<Il2CppMayday.Item>();
            var spawned = Il2CppMirror.NetworkServer.spawned;
            if (spawned != null)
            {
                foreach (var entry in spawned)
                {
                    try
                    {
                        var identity = entry.Value;
                        if (identity == null) continue;
                        var item = FindNetworkItem(identity);
                        if (item == null || !item.InWorld || !item.IsFree || item.IsHeld || item.InSocket || item.IsUsing || !IsSupportedCabinItem(item)) continue;
                        var local = plane.transform.InverseTransformPoint(item.NetworksyncPos);
                        if (Math.Abs(local.x) <= 7.5f && local.y >= -1f && local.y <= 5.5f && Math.Abs(local.z) <= 13f) toRemove.Add(item);
                    }
                    catch { }
                }
            }
            var removedInstances = toRemove.Select(x => x.GetInstanceID()).ToHashSet();
            foreach (var item in toRemove) item.ServerDestroy();
            var sceneItems = UnityEngine.Object.FindObjectsOfType<Il2CppMayday.Item>(true)
                .Where(x => x != null && !removedInstances.Contains(x.GetInstanceID()) && !x.IsHeld && !x.IsUsing && IsSupportedCabinItem(x))
                .ToList();
            var usedSceneItems = new HashSet<int>();
            foreach (var entry in items.Take(256))
            {
                try
                {
                    if (entry == null || string.IsNullOrWhiteSpace(entry.RuntimeType) || !IsSupportedInventoryItemId(entry.ItemId)) continue;
                    if (Math.Abs(entry.X) > 7.5f || entry.Y < -1f || entry.Y > 5.5f || Math.Abs(entry.Z) > 13f || entry.Count < 0 || entry.Count > 10000 || float.IsNaN(entry.Amount) || float.IsInfinity(entry.Amount)) continue;
                    var localPosition = new Vector3(entry.X, entry.Y, entry.Z);
                    var worldPosition = plane.transform.TransformPoint(localPosition);
                    var localRotation = new Quaternion(entry.Qx, entry.Qy, entry.Qz, entry.Qw);
                    var worldRotation = plane.transform.rotation * localRotation;
                    if (entry.SceneOwned)
                    {
                        var existing = sceneItems.FirstOrDefault(x => !usedSceneItems.Contains(x.GetInstanceID()) && string.Equals(x.ItemId, entry.ItemId, StringComparison.Ordinal) && string.Equals(x.GetType().FullName, entry.RuntimeType, StringComparison.Ordinal));
                        if (existing != null)
                        {
                            var wasSocketed = existing.InSocket;
                            existing.ServerRelease(worldPosition, worldRotation, Vector3.zero);
                            ApplyItemSnapshot(existing, entry);
                            existing.NetworkinWorld = true;
                            existing.NetworkisUsing = false;
                            existing.Networkattach = default(Il2CppMayday.AttachState);
                            existing.ServerTeleport(worldPosition, worldRotation);
                            existing.NetworksyncPos = worldPosition;
                            existing.NetworksyncRot = worldRotation;
                            usedSceneItems.Add(existing.GetInstanceID());
                            restored++;
                            LogEvent("scene-item-restored", "id=" + entry.ItemId + " type=" + entry.RuntimeType + " fromSocket=" + wasSocketed + " x=" + entry.X + " y=" + entry.Y + " z=" + entry.Z);
                            continue;
                        }
                        LogEvent("scene-item-spawn-fallback", "id=" + entry.ItemId + " type=" + entry.RuntimeType + " catalogImplemented=" + Il2CppMayday.ItemCatalog.Implemented(entry.ItemId));
                    }
                    System.Action<Il2CppMayday.Item> initManaged = item =>
                    {
                        if (item == null) return;
                        if (!IsSupportedCabinItem(item)) { item.ServerDestroy(); return; }
                        ApplyItemSnapshot(item, entry);
                        item.NetworkinWorld = true;
                        item.NetworkisUsing = false;
                        item.Networkattach = default(Il2CppMayday.AttachState);
                        item.NetworksyncPos = worldPosition;
                        item.NetworksyncRot = worldRotation;
                    };
                    var init = DelegateSupport.ConvertDelegate<Il2CppSystem.Action<Il2CppMayday.Item>>(initManaged);
                    var spawnedItem = Il2CppMayday.Item.ServerSpawn(entry.ItemId, worldPosition, worldRotation, init, true);
                    if (spawnedItem != null) restored++;
                    else LogEvent("item-restore-skip", "id=" + entry.ItemId + " error=ServerSpawn returned null");
                }
                catch (Exception e) { LogEvent("item-restore-skip", "id=" + entry?.ItemId + " error=" + e.Message); }
            }
            return restored;
        }

        private static ItemCheckpoint SnapshotItem(Il2CppMayday.Item item, Vector3 localPosition, Quaternion localRotation)
        {
            return IsSupportedCabinItem(item) ? SnapshotItemCore(item, localPosition, localRotation) : null;
        }

        private static ItemCheckpoint SnapshotInventoryItem(Il2CppMayday.Item item)
        {
            return IsSupportedInventoryItem(item) ? SnapshotItemCore(item, Vector3.zero, Quaternion.identity) : null;
        }

        private static ItemCheckpoint SnapshotSocketItem(Il2CppMayday.Item item)
        {
            return IsSupportedInventoryItem(item) ? SnapshotItemCore(item, Vector3.zero, Quaternion.identity) : null;
        }

        private static ItemCheckpoint SnapshotItemCore(Il2CppMayday.Item item, Vector3 localPosition, Quaternion localRotation)
        {
            var tint = item.NetworkTint;
            return new ItemCheckpoint
            {
                ItemId = item.ItemId, X = localPosition.x, Y = localPosition.y, Z = localPosition.z,
                Qx = localRotation.x, Qy = localRotation.y, Qz = localRotation.z, Qw = localRotation.w,
                State = item.NetworkState, Amount = item.NetworkAmount, Count = item.NetworkCount, Tag = item.NetworkTag,
                TintR = tint.r, TintG = tint.g, TintB = tint.b, TintA = tint.a, RuntimeType = item.GetType().FullName,
                HotUntil = item is Il2CppMayday.Service.CupItem cup ? Math.Max(0d, cup.NetworkhotUntil - Time.time) : 0,
                EatPercent = item is Il2CppMayday.Service.EdibleItem edible ? edible.NetworkeatPct : (byte)0,
                JerryCanPlug = item is Il2CppMayday.Systems.JerryCanItem can ? can.Networkplug : (byte)0,
                AccentMode = item is Il2CppMayday.Systems.SprayCanTool spray && spray.NetworkaccentMode
            };
        }

        private static PlayerCheckpoint[] CapturePlayers(Il2CppMayday.Flight.PlaneFlight plane, Il2CppMayday.AirportSite airport, bool forcePlaneAnchor)
        {
            var players = new List<PlayerCheckpoint>();
            var all = Il2CppMayday.PlayerNet.All;
            var spawned = Il2CppMirror.NetworkServer.spawned;
            for (var i = 0; all != null && i < all.Count; i++)
            {
                var player = all[i];
                if (player == null || player.SteamId == 0 || player.Inventory == null || player.ViewTransform == null) continue;
                var planeLocal = plane.transform.InverseTransformPoint(player.ViewTransform.position);
                var inPlaneBounds = Math.Abs(planeLocal.x) <= 7.5f && planeLocal.y >= -1f && planeLocal.y <= 5.5f && Math.Abs(planeLocal.z) <= 13f;
                var anchor = forcePlaneAnchor || inPlaneBounds || airport == null || airport.spawnPoint == null ? plane.transform : airport.spawnPoint;
                var local = anchor.InverseTransformPoint(player.ViewTransform.position);
                var inventory = new List<InventoryItemCheckpoint>();
                var slots = player.Inventory.slots;
                for (var slot = 0; slots != null && slot < slots.Count; slot++)
                {
                    var netId = slots[slot];
                    if (netId == 0 || spawned == null || !spawned.TryGetValue(netId, out var identity) || identity == null) continue;
                    var item = FindNetworkItem(identity);
                    var itemState = item == null ? null : SnapshotInventoryItem(item);
                    if (itemState == null) throw new InvalidOperationException("玩家 " + player.DisplayName + " 背包含不支持的特殊物品，无法完整保存：" + (item != null ? item.ItemId + " type=" + item.GetType().FullName : "netId=" + netId));
                    inventory.Add(new InventoryItemCheckpoint { SlotOrder = slot, Item = itemState });
                }
                var heading = Mathf.DeltaAngle(anchor.eulerAngles.y, player.ViewTransform.eulerAngles.y);
                var currentSeat = player.CurrentSeat;
                var seatKind = currentSeat != null ? (byte)currentSeat.Kind : (byte)0;
                var seatIndex = -1;
                if (player.IsSeated)
                {
                    foreach (var interactable in UnityEngine.Object.FindObjectsOfType<Il2CppMayday.Interactable>(true))
                    {
                        if (interactable == null) continue;
                        var seatRef = interactable.GetComponent<Il2CppMayday.ISeat>();
                        if (seatRef == null || Il2CppMayday.PlayerNet.OccupantOf(seatRef) != player) continue;
                        var paxSeat = interactable.GetComponent<Il2CppMayday.Passengers.PassengerSeat>();
                        if (paxSeat != null) { seatIndex = paxSeat.GlobalIndex; seatKind = (byte)paxSeat.Kind; }
                        break;
                    }
                }
                var held = player.Inventory.Held;
                players.Add(new PlayerCheckpoint
                {
                    SteamId = player.SteamId, DisplayName = player.DisplayName,
                    X = local.x, Y = local.y, Z = local.z, Heading = heading,
                    PositionAnchor = forcePlaneAnchor || inPlaneBounds ? "plane" : "airport",
                    ActiveSlot = Math.Max(0, inventory.FindIndex(x => x.SlotOrder == player.Inventory.Networkactive)), Inventory = inventory.ToArray(),
                    WasSeated = player.IsSeated, SeatKind = seatKind, SeatIndex = seatIndex,
                    HeldItemId = held != null ? held.ItemId : string.Empty, ActiveItemWasHeld = held != null
                });
            }
            return players.ToArray();
        }

        private static SeatRowCheckpoint[] CaptureSeatRows()
        {
            var rows = new List<SeatRowCheckpoint>();
            var spawned = Il2CppMirror.NetworkServer.spawned;
            if (spawned == null) return rows.ToArray();
            foreach (var entry in spawned)
            {
                try
                {
                    var identity = entry.Value;
                    if (identity == null) continue;
                    var row = identity.GetComponent<Il2CppMayday.Passengers.SeatRow>();
                    if (row == null || row.Networkslot < 0) continue;
                    rows.Add(new SeatRowCheckpoint { Slot = row.Networkslot, Premium = row.Networkpremium, InstalledAt = Math.Max(0d, Time.time - row.NetworkinstalledAt) });
                }
                catch (Exception e) { LogEvent("seat-row-snapshot-skip", e.Message); }
            }
            return rows.GroupBy(x => x.Slot).Select(x => x.First()).OrderBy(x => x.Slot).ToArray();
        }

        private static BaggageCartCheckpoint[] CaptureBaggageCarts()
        {
            var result = new List<BaggageCartCheckpoint>();
            foreach (var cart in new[] { Il2CppMayday.Airport.BaggageCart.Departure, Il2CppMayday.Airport.BaggageCart.Arrival }.Where(x => x != null).Distinct())
            {
                if (cart.Towed || cart.Linked || cart.RiderCount > 0 || cart.riders != null && cart.riders.Count > 0 || cart.carried != null && cart.carried.Count > 0)
                {
                    LogEvent("baggage-cart-skip", "site=" + Il2CppMayday.Airport.GroundService.SiteIndex + " arrival=" + cart.arrival + " reason=linked_or_occupied_or_loaded");
                    continue;
                }
                cart.GetServerPose(out var pos, out var yaw);
                result.Add(new BaggageCartCheckpoint
                {
                    SiteIndex = Il2CppMayday.Airport.GroundService.SiteIndex, Terminal = Il2CppMayday.Airport.BaggageCart.NameTerminal, Stand = Il2CppMayday.Airport.BaggageCart.NameStand, Arrival = cart.arrival,
                    AtAnchor = cart.AtAnchor, X = pos.x, Y = pos.y, Z = pos.z, Yaw = yaw
                });
            }
            return result.ToArray();
        }

        private static TrolleyCheckpoint[] CaptureTrolleys(Il2CppMayday.Flight.PlaneFlight plane)
        {
            var trolley = Il2CppMayday.Service.DrinkTrolley.Instance;
            if (trolley == null) return Array.Empty<TrolleyCheckpoint>();
            var pos = plane.transform.InverseTransformPoint(trolley.Networkpose);
            var rot = Quaternion.Inverse(plane.transform.rotation) * trolley.transform.rotation;
            var slots = new List<TrolleySlotCheckpoint>();
            for (var i = 0; trolley.slots != null && i < trolley.slots.Length; i++)
            {
                var socket = trolley.slots[i];
                if (socket == null || socket.Current == null) continue;
                var item = SnapshotSocketItem(socket.Current);
                if (item == null) throw new InvalidOperationException("餐车插槽 " + i + " 中有当前不支持的物品：" + socket.Current.ItemId);
                slots.Add(new TrolleySlotCheckpoint { Index = i, PlaceKey = socket.placeKey ?? string.Empty, Item = item });
            }
            return new[] { new TrolleyCheckpoint
            {
                X = pos.x, Y = pos.y, Z = pos.z, Qx = rot.x, Qy = rot.y, Qz = rot.z, Qw = rot.w,
                Side = trolley.Networkside, Braked = trolley.Networkbraked, Slots = slots.ToArray()
            } };
        }

        private static PendingPassengerCheckpoint[] CapturePendingPassengers(Il2CppMayday.Passengers.PassengerDirector director, bool discardGenerated)
        {
            if (director == null) return Array.Empty<PendingPassengerCheckpoint>();
            if (discardGenerated) return Array.Empty<PendingPassengerCheckpoint>();
            if (director.OnBoard + director.WaitingAtGate + director.Disembarking > 0)
                throw new InvalidOperationException("当前有乘客已生成/登机；此恢复版本只支持重建 Ground 待登机队列，暂不支持已登机乘客的完整续跑");
            var result = new List<PendingPassengerCheckpoint>();
            var pending = director.pendingSpawns;
            for (var i = 0; pending != null && i < pending.Count; i++)
            {
                var pax = pending[i];
                if (pax == null) continue;
                var color = pax.tag;
                result.Add(new PendingPassengerCheckpoint
                {
                    Seed = pax.seed, Type = (byte)pax.type, Seat = pax.seat, Fare = pax.fare, Paid = pax.paid,
                    LegNumber = pax.legN, TagR = color.r, TagG = color.g, TagB = color.b, TagA = color.a,
                    Mood = pax.mood, BagId = pax.bagId, BagColor = pax.bagColor, HijackerVisual = pax.hjVis, HijackerServer = pax.hjSrv
                });
            }
            return result.ToArray();
        }

        private static void RestoreSeatRows(FlightCheckpoint snapshot)
        {
            var director = Il2CppMayday.Passengers.PassengerDirector.Instance;
            if (director == null) throw new InvalidOperationException("PassengerDirector 尚未初始化，无法恢复座椅");
            foreach (var saved in (snapshot.SeatRows ?? Array.Empty<SeatRowCheckpoint>()).Take(64))
            {
                if (saved.Slot < 0 || saved.Slot >= director.RowCount) continue;
                if (!director.IsRowInstalled(saved.Slot)) director.ServerInstallRow(saved.Slot, true);
            }
            for (var slot = 0; slot < Math.Min(8, director.RowCount); slot++)
                if ((snapshot.SeatRowMask & (1 << slot)) != 0 && !director.IsRowInstalled(slot)) director.ServerInstallRow(slot, true);
            if ((snapshot.PremiumSeats || snapshot.SeatRows.Any(x => x != null && x.Premium)) && !director.PremiumSeats) director.ServerSetPremium(true, false);
            var spawned = Il2CppMirror.NetworkServer.spawned;
            if (spawned != null)
                foreach (var entry in spawned)
                {
                    var identity = entry.Value;
                    if (identity == null) continue;
                    var row = identity.GetComponent<Il2CppMayday.Passengers.SeatRow>();
                    if (row == null) continue;
                    var saved = snapshot.SeatRows.FirstOrDefault(x => x.Slot == row.Networkslot);
                    if (saved != null) row.NetworkinstalledAt = Time.time - saved.InstalledAt;
                }
            director.NetworkkitMask = snapshot.SeatKitMask;
            director.kitMask = snapshot.SeatKitMask;
            director.ServerInitSeats();
        }

        private static int RestorePendingPassengers(FlightCheckpoint snapshot)
        {
            var director = Il2CppMayday.Passengers.PassengerDirector.Instance;
            if (director == null) throw new InvalidOperationException("PassengerDirector 尚未初始化");
            director.ServerClearAll();
            director.ServerInitSeats();
            var list = new Il2CppSystem.Collections.Generic.List<Il2CppMayday.Passengers.PassengerDirector.PendingPax>();
            foreach (var saved in (snapshot.PendingPassengers ?? Array.Empty<PendingPassengerCheckpoint>()).Take(128))
            {
                if (saved == null || saved.Type > (byte)Il2CppMayday.Passengers.PaxType.Police || float.IsNaN(saved.Mood) || float.IsInfinity(saved.Mood)) continue;
                var pax = new Il2CppMayday.Passengers.PassengerDirector.PendingPax
                {
                    seed = saved.Seed,
                    type = (Il2CppMayday.Passengers.PaxType)saved.Type,
                    seat = saved.Seat,
                    fare = saved.Fare,
                    paid = saved.Paid,
                    legN = saved.LegNumber,
                    tag = new Color32(saved.TagR, saved.TagG, saved.TagB, saved.TagA),
                    mood = saved.Mood,
                    bagId = saved.BagId ?? string.Empty,
                    bagColor = saved.BagColor,
                    hjVis = saved.HijackerVisual,
                    hjSrv = saved.HijackerServer
                };
                list.Add(pax);
            }
            director.pendingSpawns = list;
            director.ckRunSeed = snapshot.PassengerRunSeed;
            director.lastRunSeed = snapshot.PassengerLegSeed;
            director.achLullabySeed = snapshot.PassengerQueueSeed;
            director.NetworksyncOnBoard = 0;
            director.NetworksyncDisembarking = 0;
            director.NetworksyncWaiting = 0;
            var boardingDone = !IsAirborneSafetySnapshot(snapshot) && snapshot.DestinationAirport >= 0 && list.Count == 0;
            director.NetworksyncBoardingDone = boardingDone;
            LogEvent("passengers-restored", "pending=" + list.Count + " runSeed=" + snapshot.PassengerRunSeed + " destination=" + snapshot.DestinationAirport + " boardingDone=" + boardingDone);
            return list.Count;
        }

        private static void RegisterPlayerRecovery(PlayerCheckpoint[] players)
        {
            ClearPlayerRecovery("register-new-checkpoint");
            PlayerRecoveryGeneration = ServerGeneration;
            foreach (var player in (players ?? Array.Empty<PlayerCheckpoint>()).Where(x => x != null && x.SteamId != 0).GroupBy(x => x.SteamId).Select(x => x.First()).Take(999))
            {
                PendingPlayerRecovery[player.SteamId] = player;
                PlayerRestoreQueue.Enqueue(player.SteamId);
            }
            LogEvent("player-recovery-registered", "players=" + PendingPlayerRecovery.Count);
        }

        private static string SaveCheckpoint(string requestedName)
        {
            try
            {
                if (!Il2CppMirror.NetworkServer.activeHost) return "仅房主可以保存航班检查点";
                if (RecoveryPoisoned) return "当前房间曾发生不完整恢复，已禁止保存以保护原存档；请退出并新建房间。原因：" + RecoveryPoisonReason;
                var state = Il2CppMayday.GameState.Instance;
                var plane = Il2CppMayday.Flight.PlaneFlight.Instance;
                if (state == null || plane == null) return "航班对象尚未初始化";
                var isAirborne = state.Phase == Il2CppMayday.GamePhase.Flight;
                var isGroundCapture = state.Phase == Il2CppMayday.GamePhase.Ground || state.Phase == Il2CppMayday.GamePhase.PreShift;
                if (!isGroundCapture && !isAirborne) return "当前阶段不能保存；阶段=" + state.Phase;
                if (isAirborne)
                {
                    if (!AllowAirborneSafetySnapshots.Value) return "飞行中安全快照已在配置中关闭";
                    if (plane.Networkcrashed || state.DestinationAirport < 0) return "坠毁状态或没有有效目的地时不能创建飞行中安全快照";
                    if (plane.sim == null || plane.sim.OnGround) return "飞行权威物理尚未进入稳定空中状态";
                }
                else if (!plane.OnGroundNow || plane.AlongNoseSpeed > 0.5f || !plane.ParkingBrakeSet)
                {
                    return "飞机尚未停稳并拉起停机刹车，请停泊后再保存";
                }
                var engines = Il2CppMayday.Systems.SysRefs.Engines;
                var fuel = Il2CppMayday.Systems.SysRefs.Fuel;
                var hull = Il2CppMayday.Systems.SysRefs.Hull;
                if (engines == null || fuel == null || hull == null || engines.Length < 2 || engines[0] == null || engines[1] == null)
                    return "飞机系统尚未完成初始化，无法安全保存机况";
                var leftEngine = engines[0];
                var rightEngine = engines[1];
                var currentAirportSite = Il2CppMayday.Airport.AirportSites.Find(state.CurrentAirport);
                Il2CppMayday.World.AirportDef currentAirportDefinition = null;
                try { currentAirportDefinition = new Il2CppMayday.World.RouteGenerator(state.RunSeed).Airport(state.CurrentAirport); } catch { }
                if (currentAirportSite == null && currentAirportDefinition == null) return "无法解析当前航线的安全恢复机场";
                var currentAirportName = currentAirportDefinition != null ? currentAirportDefinition.name : state.CurrentAirportName;
                var currentAirportCode = currentAirportDefinition != null ? currentAirportDefinition.code : Il2CppMayday.Airport.AirportSites.CodeOf(currentAirportSite, state.CurrentAirport);
                var passengerDirector = Il2CppMayday.Passengers.PassengerDirector.Instance;
                if (passengerDirector == null) return "乘客系统尚未初始化";
                var checkpointId = System.Guid.NewGuid().ToString("N");
                var displayName = SanitizeCheckpointName(requestedName);
                if (displayName.Length == 0) displayName = "航段 " + state.Leg + " · " + (state.CurrentAirportName ?? "机场") + " · " + DateTime.Now.ToString("MM-dd HH:mm");
                var snapshot = new FlightCheckpoint
                {
                    CheckpointId = checkpointId,
                    DisplayName = displayName,
                    GameVersion = Application.version,
                    SavedUtc = DateTime.UtcNow,
                    CaptureMode = isAirborne ? "Airborne" : "Ground",
                    RecoveryMode = isAirborne ? "SafeOriginPreShift" : "ExactGround",
                    PassengerRecoveryPolicy = isAirborne ? "DiscardGenerated" : "PendingOnly",
                    SafeRecoveryAirport = state.CurrentAirport,
                    SafeRecoveryAirportCode = currentAirportCode,
                    SavedOnBoardPassengers = passengerDirector.OnBoard,
                    SavedWaitingPassengers = passengerDirector.WaitingAtGate,
                    SavedDisembarkingPassengers = passengerDirector.Disembarking,
                    RunSeed = state.RunSeed,
                    Leg = state.Leg,
                    Money = state.Money,
                    Debt = state.Debt,
                    Rating = state.Rating,
                    Difficulty = state.Difficulty,
                    TotalPassengers = state.TotalPassengers,
                    TotalEarned = state.TotalEarned,
                    RunStartTime = state.RunStartTime,
                    CurrentAirport = state.CurrentAirport,
                    DestinationAirport = state.DestinationAirport,
                    CurrentAirportName = currentAirportName,
                    CurrentAirportCode = currentAirportCode,
                    DestinationName = state.DestinationName,
                    GroundTimeLeft = state.GroundTimeLeft,
                    LegTime = state.LegTime,
                    TimeOfDay = state.TimeOfDay,
                    Weather = state.Weather,
                    Upgrades = state.Upgrades == null ? Array.Empty<string>() : Enumerable.Range(0, state.Upgrades.Count).Select(i => state.Upgrades[i]).ToArray(),
                    Plane = new PlaneCheckpoint
                    {
                        WheelDamage = plane.NetworkwheelDamage,
                        GearDown = plane.NetworkgearDownCmd,
                        Flap = plane.NetworkflapSetting,
                        ParkingBrake = plane.NetworkparkingBrake,
                        GearJammed = plane.NetworkgearJammed,
                        ApModes = plane.NetworkapModes,
                        ApFault = plane.NetworkapFault,
                        SeatbeltSign = plane.NetworkseatbeltSign,
                        Crashed = plane.Networkcrashed,
                        CrashReason = plane.NetworkcrashReason,
                        ReverseGear = plane.NetworkreverseGear,
                        FuelLeft = fuel.NetworkfuelL,
                        FuelRight = fuel.NetworkfuelR,
                        FuelCapacity = fuel.Networkcapacity,
                        EngineLeftOil = leftEngine.Networkoil,
                        EngineRightOil = rightEngine.Networkoil,
                        EngineLeftHealth = leftEngine.Networkhealth,
                        EngineRightHealth = rightEngine.Networkhealth,
                        EngineLeftFire = leftEngine.Networkfire,
                        EngineRightFire = rightEngine.Networkfire,
                        EngineLeftState = (byte)leftEngine.Networkstate,
                        EngineRightState = (byte)rightEngine.Networkstate,
                        EngineLeftFaults = leftEngine.Networkfaults,
                        EngineRightFaults = rightEngine.Networkfaults,
                        EngineLeftRpm = leftEngine.Networkrpm,
                        EngineRightRpm = rightEngine.Networkrpm,
                        EngineLeftTemp = leftEngine.Networktemp,
                        EngineRightTemp = rightEngine.Networktemp,
                        EngineLeftThrust = leftEngine.Networkthrust,
                        EngineRightThrust = rightEngine.Networkthrust,
                        EngineLeftRepair = leftEngine.Networkrepair,
                        EngineRightRepair = rightEngine.Networkrepair,
                        EngineLeftBattery = leftEngine.Networkbatt,
                        EngineRightBattery = rightEngine.Networkbatt,
                        EngineLeftBatteryState = (byte)leftEngine.Networkbat,
                        EngineRightBatteryState = (byte)rightEngine.Networkbat,
                        EngineLeftCowlState = (byte)leftEngine.Networkcowl,
                        EngineRightCowlState = (byte)rightEngine.Networkcowl,
                        FuelReserve = fuel.NetworkreserveSync,
                        FuelCaps = fuel.Networkcaps,
                        FuelLeaks = fuel.Networkleaks,
                        FuelPumpLeft = fuel.NetworkpumpL,
                        FuelPumpRight = fuel.NetworkpumpR,
                        FuelCrossfeed = fuel.Networkcrossfeed,
                        ApAltitudeTarget = plane.NetworkapAltTarget,
                        ApHeadingTarget = plane.NetworkapHdgTarget,
                        ApSpeedTarget = plane.NetworkapSpdTarget,
                        IceState = plane.NetworkiceSync,
                        CrosswindX = plane.Networkcrosswind.x,
                        CrosswindY = plane.Networkcrosswind.y,
                        Physics = CaptureFlightPhysics(plane),
                        HullDamage = CopySyncList(hull.damage),
                        HullDirt = CopySyncList(hull.dirt),
                        HullHoles = CopyVector4List(hull.holes)
                    },
                    Items = CaptureCabinItems(plane),
                    Players = CapturePlayers(plane, currentAirportSite, isAirborne),
                    PendingPassengers = CapturePendingPassengers(passengerDirector, isAirborne),
                    PassengerRunSeed = passengerDirector.ckRunSeed,
                    PassengerLegSeed = passengerDirector.lastRunSeed,
                    PassengerQueueSeed = passengerDirector.achLullabySeed,
                    SeatRows = CaptureSeatRows(),
                    Trolleys = CaptureTrolleys(plane),
                    SeatRowMask = Il2CppMayday.Passengers.PassengerDirector.Instance != null ? Il2CppMayday.Passengers.PassengerDirector.Instance.NetworkrowMask : (byte)0,
                    PremiumSeats = Il2CppMayday.Passengers.PassengerDirector.Instance != null && Il2CppMayday.Passengers.PassengerDirector.Instance.NetworkpremiumSeats,
                    SeatKitMask = Il2CppMayday.Passengers.PassengerDirector.Instance != null ? Il2CppMayday.Passengers.PassengerDirector.Instance.NetworkkitMask : (byte)0,
                    BaggageCarts = !isAirborne && state.DestinationAirport >= 0 ? CaptureBaggageCarts() : Array.Empty<BaggageCartCheckpoint>()
                };
                if (isAirborne && !ValidFlightPhysics(snapshot.Plane.Physics)) return "飞行物理快照超出安全范围，未写入存档";
                var capturedIds = snapshot.Players.Select(x => x.SteamId).ToHashSet();
                var missingRemote = Online.Values.Select(x => x.SteamId).Where(x => x != 0).Distinct().Any(id => !capturedIds.Contains(id));
                var localPlayer = Il2CppMirror.NetworkServer.localConnection != null && Il2CppMirror.NetworkServer.localConnection.identity != null ? Il2CppMirror.NetworkServer.localConnection.identity.GetComponent<Il2CppMayday.PlayerNet>() : null;
                if (missingRemote || (localPlayer != null && localPlayer.SteamId != 0 && !capturedIds.Contains(localPlayer.SteamId))) return "玩家对象尚未全部初始化，无法完整保存玩家位置和背包；请稍候重试";
                Directory.CreateDirectory(CheckpointDirectory);
                var target = Path.Combine(CheckpointDirectory, checkpointId + ".json");
                var temp = target + ".tmp";
                File.WriteAllText(temp, JsonSerializer.Serialize(snapshot, JsonOptions), Encoding.UTF8);
                File.Move(temp, target);
                LogEvent("checkpoint-saved", "id=" + checkpointId + " mode=" + snapshot.CaptureMode + "/" + snapshot.RecoveryMode + " name=" + displayName + " path=" + target + " leg=" + snapshot.Leg + " airport=" + snapshot.CurrentAirport + " money=" + snapshot.Money + " items=" + snapshot.Items.Length + " players=" + snapshot.Players.Length + " pendingPassengers=" + snapshot.PendingPassengers.Length + " generatedPassengers=" + (snapshot.SavedOnBoardPassengers + snapshot.SavedWaitingPassengers + snapshot.SavedDisembarkingPassengers) + " seatRows=" + snapshot.SeatRows.Length + " trolleys=" + snapshot.Trolleys.Length + " baggageCarts=" + snapshot.BaggageCarts.Length + " hullPanels=" + snapshot.Plane.HullDamage.Length + " hullHoles=" + (snapshot.Plane.HullHoles.Length / 4) + " engineFaults=" + snapshot.Plane.EngineLeftFaults + "/" + snapshot.Plane.EngineRightFaults);
                if (isAirborne)
                    return "飞行中安全快照已保存：" + displayName + "；读取时返回起飞机场 PreShift，取消当前目的地；" + (snapshot.SavedOnBoardPassengers + snapshot.SavedWaitingPassengers + snapshot.SavedDisembarkingPassengers) + " 个已生成乘客及客户端本地动作不会恢复";
                return "地面存档已保存：" + displayName + "；飞机机况、" + snapshot.Items.Length + " 件散落物、" + snapshot.Players.Length + " 名玩家位置/基础背包、" + snapshot.PendingPassengers.Length + " 名待登机乘客、" + snapshot.SeatRows.Length + " 排座椅配置、" + snapshot.Trolleys.Length + " 辆餐车、" + snapshot.BaggageCarts.Length + " 辆空行李车";
            }
            catch (Exception e) { LogEvent("checkpoint-save-error", e.ToString()); return "保存检查点失败: " + e.Message; }
        }

        private static Il2CppMayday.AirportSite FindMaterializedCheckpointAirport(FlightCheckpoint snapshot)
        {
            if (snapshot == null) return null;
            var manager = Il2CppMayday.World.WorldManager.Instance;
            var site = manager != null ? manager.AirportByIndex(snapshot.CurrentAirport) : null;
            if (site == null) site = Il2CppMayday.Airport.AirportSites.Find(snapshot.CurrentAirport);
            if (site == null) return null;
            if (!string.IsNullOrWhiteSpace(snapshot.CurrentAirportCode) && !string.Equals(site.code, snapshot.CurrentAirportCode, StringComparison.OrdinalIgnoreCase)) return null;
            if (!string.IsNullOrWhiteSpace(snapshot.CurrentAirportName) && !string.Equals(site.displayName, snapshot.CurrentAirportName, StringComparison.OrdinalIgnoreCase)) return null;
            return site;
        }

        private static bool RecoverySceneObjectsReady(FlightCheckpoint snapshot)
        {
            if (snapshot == null) return false;
            if (snapshot.Trolleys != null && snapshot.Trolleys.Length > 0 && Il2CppMayday.Service.DrinkTrolley.Instance == null) return false;
            var airport = FindMaterializedCheckpointAirport(snapshot);
            if (airport == null || airport.spawnPoint == null || airport.stand == null) return false;
            if (snapshot.DestinationAirport < 0) return true;
            foreach (var saved in snapshot.BaggageCarts ?? Array.Empty<BaggageCartCheckpoint>())
            {
                var cart = saved.Arrival ? Il2CppMayday.Airport.BaggageCart.Arrival : Il2CppMayday.Airport.BaggageCart.Departure;
                if (cart == null || Il2CppMayday.Airport.GroundService.SiteIndex != saved.SiteIndex || !string.Equals(Il2CppMayday.Airport.BaggageCart.NameTerminal, saved.Terminal, StringComparison.Ordinal) || !string.Equals(Il2CppMayday.Airport.BaggageCart.NameStand, saved.Stand, StringComparison.Ordinal)) return false;
            }
            return true;
        }

        private static void PumpRecoveryOperation()
        {
            var operation = ActiveRecovery;
            if (operation == null) return;
            if (operation.Generation != ServerGeneration || !Il2CppMirror.NetworkServer.activeHost)
            {
                ActiveRecovery = null;
                RecoveryStage = "CANCELLED";
                LastRecoveryMessage = "恢复已取消：服务器会话已变化";
                return;
            }
            if (++operation.WaitFrames < 5) return;
            try
            {
                var state = Il2CppMayday.GameState.Instance;
                var plane = Il2CppMayday.Flight.PlaneFlight.Instance;
                var engines = Il2CppMayday.Systems.SysRefs.Engines;
                var fuel = Il2CppMayday.Systems.SysRefs.Fuel;
                var hull = Il2CppMayday.Systems.SysRefs.Hull;
                var director = Il2CppMayday.Passengers.PassengerDirector.Instance;
                var coreReady = state != null && plane != null && engines != null && engines.Length >= 2 && engines[0] != null && engines[1] != null && fuel != null && hull != null && director != null;
                if (operation.AwaitingAirportMaterialization)
                {
                    if (!coreReady)
                    {
                        if (operation.WaitFrames < 600) return;
                        throw new InvalidOperationException("按检查点路由重建后，核心运行对象在 600 帧内未就绪");
                    }
                    var airport = FindMaterializedCheckpointAirport(operation.Snapshot);
                    if (airport == null || airport.spawnPoint == null || airport.stand == null)
                    {
                        if (operation.WaitFrames == 5 || operation.WaitFrames == 60 || operation.WaitFrames == 120 || operation.WaitFrames == 300)
                            LogEvent("recovery-airport-materialization-wait", "operation=" + operation.Id + " frame=" + operation.WaitFrames + " seed=" + operation.Snapshot.RunSeed + " airport=" + operation.Snapshot.CurrentAirport + " name=" + operation.Snapshot.CurrentAirportName + " code=" + operation.Snapshot.CurrentAirportCode + " runtimeSites=" + Il2CppMayday.Airport.AirportSites.Snapshot()?.Count);
                        if (operation.WaitFrames < 600) return;
                        throw new InvalidOperationException("原生路由重建后，检查点机场仍未实例化：index=" + operation.Snapshot.CurrentAirport + " name=" + operation.Snapshot.CurrentAirportName + " code=" + operation.Snapshot.CurrentAirportCode);
                    }
                    plane.ServerParkAt(airport);
                    operation.AwaitingAirportMaterialization = false;
                    operation.AirportParked = true;
                    operation.WaitFrames = 0;
                    RecoveryStage = "WAITING_RUNTIME";
                    LogEvent("recovery-airport-materialized", "operation=" + operation.Id + " seed=" + operation.Snapshot.RunSeed + " index=" + airport.index + " name=" + airport.displayName + " code=" + airport.code);
                    return;
                }
                if (!coreReady || !operation.AirportParked || !RecoverySceneObjectsReady(operation.Snapshot))
                {
                    if (operation.WaitFrames == 5 || operation.WaitFrames == 60 || operation.WaitFrames == 120 || operation.WaitFrames == 300)
                        LogEvent("recovery-runtime-wait", "operation=" + operation.Id + " frame=" + operation.WaitFrames + " core=" + coreReady + " scene=" + RecoverySceneObjectsReady(operation.Snapshot));
                    if (operation.WaitFrames < 600) return;
                    throw new InvalidOperationException("机场停靠后核心/场景对象在 600 帧内未就绪");
                }
                RecoveryStage = "APPLYING";
                ApplyRecoverySnapshot(operation, state, plane);
                RecoveryStage = PendingPlayerRecovery.Count == 0 ? "SUCCEEDED" : "PLAYER_PENDING";
                ActiveRecovery = null;
                try
                {
                    var service = LobbyService;
                    if (service != null && service.InLobby && service.Lobby.IsValid())
                    {
                        LastJoinableSet = Il2CppSteamworks.SteamMatchmaking.SetLobbyJoinable(service.Lobby, true);
                        LastJoinableSetKnown = true;
                        if (LastJoinableSet) ManualJoinAllowed = true;
                    }
                }
                catch (Exception e) { LogEvent("recovery-joinable-error", e.Message); }
            }
            catch (Exception e)
            {
                ActiveRecovery = null;
                RecoveryStage = "FATAL";
                MarkRecoveryPoisoned(e.Message);
                LastRecoveryMessage = "恢复失败，当前房间不可继续，请退出并新建房间后重试: " + e.Message;
                LogEvent("checkpoint-recover-error", "operation=" + operation.Id + " backup=" + operation.BackupPath + " exception=" + e);
            }
        }

        private static void ApplyRecoverySnapshot(RecoveryOperation operation, Il2CppMayday.GameState state, Il2CppMayday.Flight.PlaneFlight plane)
        {
            var snapshot = operation.Snapshot;
            var systemsEngines = Il2CppMayday.Systems.SysRefs.Engines;
            var systemsFuel = Il2CppMayday.Systems.SysRefs.Fuel;
            var systemsHull = Il2CppMayday.Systems.SysRefs.Hull;
            state.NetworkLeg = snapshot.Leg;
            state.NetworkDebt = snapshot.Debt;
            state.NetworkRating = snapshot.Rating;
            state.NetworkDifficulty = snapshot.Difficulty;
            state.NetworkTotalPassengers = snapshot.TotalPassengers;
            state.NetworkTotalEarned = snapshot.TotalEarned;
            state.NetworkRunStartTime = snapshot.RunStartTime;
            var safeAirborne = IsAirborneSafetySnapshot(snapshot);
            var effectiveAirport = safeAirborne ? snapshot.SafeRecoveryAirport : snapshot.CurrentAirport;
            var effectiveDestination = safeAirborne ? -1 : snapshot.DestinationAirport;
            state.NetworkCurrentAirport = effectiveAirport;
            state.NetworkDestinationAirport = effectiveDestination;
            state.NetworkCurrentAirportName = snapshot.CurrentAirportName ?? string.Empty;
            state.NetworkDestinationName = safeAirborne ? string.Empty : snapshot.DestinationName ?? string.Empty;
            state.NetworkGroundTimeLeft = safeAirborne ? -1f : snapshot.GroundTimeLeft;
            state.NetworkLegTime = safeAirborne ? 0f : snapshot.LegTime;
            state.NetworkTimeOfDay = snapshot.TimeOfDay;
            state.NetworkWeather = snapshot.Weather;
            var restoredPhase = safeAirborne || effectiveDestination < 0 ? Il2CppMayday.GamePhase.PreShift : Il2CppMayday.GamePhase.Ground;
            state.NetworkPhase = restoredPhase;
            state.ServerSetPhase(restoredPhase);
            var savedPlane = snapshot.Plane;
            plane.NetworkwheelDamage = savedPlane.WheelDamage;
            plane.NetworkgearDownCmd = safeAirborne || savedPlane.GearDown;
            plane.NetworkflapSetting = safeAirborne ? (byte)0 : savedPlane.Flap;
            plane.NetworkparkingBrake = safeAirborne || savedPlane.ParkingBrake;
            plane.NetworkgearJammed = safeAirborne ? false : savedPlane.GearJammed;
            plane.NetworkapModes = safeAirborne ? (byte)0 : savedPlane.ApModes;
            plane.NetworkapFault = safeAirborne ? (byte)0 : savedPlane.ApFault;
            plane.NetworkseatbeltSign = savedPlane.SeatbeltSign;
            plane.Networkcrashed = safeAirborne ? false : savedPlane.Crashed;
            plane.NetworkcrashReason = safeAirborne ? string.Empty : savedPlane.CrashReason ?? string.Empty;
            plane.NetworkreverseGear = safeAirborne ? false : savedPlane.ReverseGear;
            systemsFuel.NetworkfuelL = savedPlane.FuelLeft;
            systemsFuel.NetworkfuelR = savedPlane.FuelRight;
            systemsFuel.Networkcapacity = savedPlane.FuelCapacity;
            if (snapshot.SchemaVersion >= 5)
            {
                systemsFuel.NetworkreserveSync = savedPlane.FuelReserve;
                systemsFuel.Networkcaps = savedPlane.FuelCaps;
                systemsFuel.Networkleaks = savedPlane.FuelLeaks;
                systemsFuel.NetworkpumpL = safeAirborne ? false : savedPlane.FuelPumpLeft;
                systemsFuel.NetworkpumpR = safeAirborne ? false : savedPlane.FuelPumpRight;
                systemsFuel.Networkcrossfeed = safeAirborne ? false : savedPlane.FuelCrossfeed;
                plane.NetworkapAltTarget = safeAirborne ? 0f : savedPlane.ApAltitudeTarget;
                plane.NetworkapHdgTarget = safeAirborne ? 0f : savedPlane.ApHeadingTarget;
                plane.NetworkapSpdTarget = safeAirborne ? 0f : savedPlane.ApSpeedTarget;
                plane.NetworkiceSync = savedPlane.IceState;
                plane.Networkcrosswind = safeAirborne ? Vector2.zero : new Vector2(savedPlane.CrosswindX, savedPlane.CrosswindY);
            }
            var leftEngine = systemsEngines[0];
            var rightEngine = systemsEngines[1];
            leftEngine.Networkoil = savedPlane.EngineLeftOil;
            rightEngine.Networkoil = savedPlane.EngineRightOil;
            leftEngine.Networkhealth = savedPlane.EngineLeftHealth;
            rightEngine.Networkhealth = savedPlane.EngineRightHealth;
            leftEngine.Networkfire = savedPlane.EngineLeftFire;
            rightEngine.Networkfire = savedPlane.EngineRightFire;
            leftEngine.Networkstate = safeAirborne ? Il2CppMayday.Systems.EngineState.Off : (Il2CppMayday.Systems.EngineState)savedPlane.EngineLeftState;
            rightEngine.Networkstate = safeAirborne ? Il2CppMayday.Systems.EngineState.Off : (Il2CppMayday.Systems.EngineState)savedPlane.EngineRightState;
            leftEngine.Networkfaults = savedPlane.EngineLeftFaults;
            rightEngine.Networkfaults = savedPlane.EngineRightFaults;
            if (snapshot.SchemaVersion >= 5)
            {
                leftEngine.Networkrpm = safeAirborne ? 0f : savedPlane.EngineLeftRpm;
                rightEngine.Networkrpm = safeAirborne ? 0f : savedPlane.EngineRightRpm;
                leftEngine.Networktemp = safeAirborne ? 0f : savedPlane.EngineLeftTemp;
                rightEngine.Networktemp = safeAirborne ? 0f : savedPlane.EngineRightTemp;
                leftEngine.Networkthrust = safeAirborne ? 0f : savedPlane.EngineLeftThrust;
                rightEngine.Networkthrust = safeAirborne ? 0f : savedPlane.EngineRightThrust;
                leftEngine.Networkfire = safeAirborne ? 0f : savedPlane.EngineLeftFire;
                rightEngine.Networkfire = safeAirborne ? 0f : savedPlane.EngineRightFire;
                leftEngine.Networkrepair = savedPlane.EngineLeftRepair;
                rightEngine.Networkrepair = savedPlane.EngineRightRepair;
                leftEngine.Networkbatt = savedPlane.EngineLeftBattery;
                rightEngine.Networkbatt = savedPlane.EngineRightBattery;
                leftEngine.Networkbat = (Il2CppMayday.Systems.BatteryState)savedPlane.EngineLeftBatteryState;
                rightEngine.Networkbat = (Il2CppMayday.Systems.BatteryState)savedPlane.EngineRightBatteryState;
                leftEngine.Networkcowl = (Il2CppMayday.Systems.CowlState)savedPlane.EngineLeftCowlState;
                rightEngine.Networkcowl = (Il2CppMayday.Systems.CowlState)savedPlane.EngineRightCowlState;
            }
            RestoreSyncList(systemsHull.damage, savedPlane.HullDamage);
            RestoreSyncList(systemsHull.dirt, savedPlane.HullDirt);
            systemsHull.ServerResetHoles();
            for (var i = 0; i + 3 < savedPlane.HullHoles.Length; i += 4)
            {
                var hole = new Vector4(savedPlane.HullHoles[i], savedPlane.HullHoles[i + 1], savedPlane.HullHoles[i + 2], savedPlane.HullHoles[i + 3]);
                if (hole.w > 0) systemsHull.SetHole(i / 4, hole);
            }
            systemsHull.RepaintHoles(-1);
            var restoredPendingPassengers = RestorePendingPassengers(snapshot);
            RestoreSeatRows(snapshot);
            var restoredCarts = RestoreBaggageCarts(snapshot.BaggageCarts, snapshot.DestinationAirport < 0, out var unavailableCarts);
            var restoredTrolleyItems = RestoreTrolleys(plane, snapshot.Trolleys);
            var restoredItems = RestoreCabinItems(plane, snapshot.Items);
            var expectedTrolleyItems = snapshot.Trolleys.Sum(x => x?.Slots?.Length ?? 0);
            var incomplete = new List<string>();
            if (restoredCarts + unavailableCarts != snapshot.BaggageCarts.Length) incomplete.Add("行李车 " + restoredCarts + "/" + snapshot.BaggageCarts.Length + "（原生不可用 " + unavailableCarts + "）");
            if (restoredTrolleyItems != expectedTrolleyItems) incomplete.Add("餐车物品 " + restoredTrolleyItems + "/" + expectedTrolleyItems);
            if (restoredItems != snapshot.Items.Length) incomplete.Add("散落物 " + restoredItems + "/" + snapshot.Items.Length);
            if (restoredPendingPassengers != snapshot.PendingPassengers.Length) incomplete.Add("待登机乘客 " + restoredPendingPassengers + "/" + snapshot.PendingPassengers.Length);
            if (incomplete.Count != 0) throw new InvalidOperationException("恢复不完整（" + string.Join("，", incomplete) + "）");
            RegisterPlayerRecovery(snapshot.Players);
            PumpPlayerRecovery();
            var upgrades = state.Upgrades;
            if (upgrades == null) throw new InvalidOperationException("升级列表未初始化");
            upgrades.Clear();
            foreach (var upgrade in snapshot.Upgrades.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct()) state.ServerAddUpgrade(upgrade);
            if (state.Leg != snapshot.Leg || state.Money != snapshot.Money || state.CurrentAirport != effectiveAirport || systemsFuel.NetworkfuelL != savedPlane.FuelLeft || systemsFuel.NetworkfuelR != savedPlane.FuelRight ||
                safeAirborne && (state.Phase != Il2CppMayday.GamePhase.PreShift || state.DestinationAirport != -1 || !plane.NetworkparkingBrake || !plane.NetworkgearDownCmd || plane.NetworkapModes != 0 || leftEngine.Networkstate != Il2CppMayday.Systems.EngineState.Off || rightEngine.Networkstate != Il2CppMayday.Systems.EngineState.Off))
                throw new InvalidOperationException("恢复后关键航班状态读回校验失败");
            LastRecoveryMessage = safeAirborne
                ? "飞行安全快照已恢复到起飞机场 PreShift；已生成乘客、空中位置和客户端本地动作未恢复" + (PendingPlayerRecovery.Count == 0 ? string.Empty : "；等待 " + PendingPlayerRecovery.Count + " 名玩家重连恢复")
                : "场景恢复完成 " + snapshot.SavedUtc.ToString("u") + (PendingPlayerRecovery.Count == 0 ? string.Empty : "；等待 " + PendingPlayerRecovery.Count + " 名玩家重连恢复");
            LogEvent("checkpoint-recovered", "operation=" + operation.Id + " backup=" + operation.BackupPath + " leg=" + state.Leg + " airport=" + state.CurrentAirport + " money=" + state.Money + " items=" + restoredItems + " baggageCarts=" + restoredCarts + " baggageCartsUnavailable=" + unavailableCarts + " trolleyItems=" + restoredTrolleyItems + " seatRows=" + snapshot.SeatRows.Length + " pendingPassengers=" + restoredPendingPassengers + " savedPlayers=" + snapshot.Players.Length + " playerPending=" + PendingPlayerRecovery.Count);
        }

        private static string RecoverCheckpoint(string checkpointId)
        {
            string backup = null;
            try
            {
                if (!Il2CppMirror.NetworkServer.activeHost) return "仅房主可以恢复航班检查点";
                if (ActiveRecovery != null) return "已有恢复任务正在进行，请等待完成";
                if (RecoveryPoisoned) return "当前房间已被标记为恢复不完整，不能再次恢复；请退出并新建房间。原因：" + RecoveryPoisonReason;
                var sourcePath = ResolveCheckpointPath(checkpointId);
                var localConnection = Il2CppMirror.NetworkServer.localConnection;
                var remoteConnections = 0;
                if (Il2CppMirror.NetworkServer.connections != null)
                    foreach (var connection in Il2CppMirror.NetworkServer.connections.Values)
                        if (connection != null && (localConnection == null || connection.connectionId != localConnection.connectionId)) remoteConnections++;
                if (remoteConnections != 0) return "请先让客人暂时离开房间；房主本地连接无需退出";
                if (sourcePath == null) return "未找到所选航班存档";
                var snapshot = JsonSerializer.Deserialize<FlightCheckpoint>(File.ReadAllText(sourcePath), JsonOptions);
                if (snapshot == null || snapshot.SchemaVersion != 4 && snapshot.SchemaVersion != 5 || snapshot.GameVersion != Application.version) return "检查点版本与当前游戏不兼容";
                if (snapshot.SchemaVersion == 4)
                {
                    snapshot.CaptureMode = "Ground";
                    snapshot.RecoveryMode = "ExactGround";
                    snapshot.PassengerRecoveryPolicy = "PendingOnly";
                    snapshot.SafeRecoveryAirport = snapshot.CurrentAirport;
                    snapshot.SafeRecoveryAirportCode = snapshot.CurrentAirportCode;
                }
                var safeAirborne = IsAirborneSafetySnapshot(snapshot);
                if (snapshot.SchemaVersion == 5)
                {
                    var validGroundMode = snapshot.CaptureMode == "Ground" && snapshot.RecoveryMode == "ExactGround" && snapshot.PassengerRecoveryPolicy == "PendingOnly";
                    var validAirMode = safeAirborne && snapshot.PassengerRecoveryPolicy == "DiscardGenerated" && snapshot.SafeRecoveryAirport == snapshot.CurrentAirport &&
                        string.Equals(snapshot.SafeRecoveryAirportCode ?? string.Empty, snapshot.CurrentAirportCode ?? string.Empty, StringComparison.OrdinalIgnoreCase) &&
                        snapshot.PendingPassengers != null && snapshot.PendingPassengers.Length == 0 && snapshot.BaggageCarts != null && snapshot.BaggageCarts.Length == 0 &&
                        snapshot.SavedOnBoardPassengers >= 0 && snapshot.SavedWaitingPassengers >= 0 && snapshot.SavedDisembarkingPassengers >= 0 &&
                        snapshot.Players != null && snapshot.Players.All(x => x != null && x.PositionAnchor == "plane") && ValidFlightPhysics(snapshot.Plane?.Physics);
                    if (!validGroundMode && !validAirMode) return "schema 5 的采集/恢复策略组合无效，未执行恢复";
                }
                if (snapshot.Leg < 0 || snapshot.Money < 0 || snapshot.Upgrades == null || snapshot.Plane == null || snapshot.Items == null || snapshot.Players == null || snapshot.PendingPassengers == null || snapshot.SeatRows == null || snapshot.Trolleys == null || snapshot.BaggageCarts == null) return "检查点数据校验失败";
                if (snapshot.Players.Length > 999 || snapshot.Players.Any(x => x == null || x.SteamId == 0 || x.Inventory == null || x.Inventory.Length > Il2CppMayday.PlayerInventory.SlotCount) || snapshot.Players.Select(x => x.SteamId).Distinct().Count() != snapshot.Players.Length || snapshot.PendingPassengers.Length > 128 || snapshot.Items.Length > 256 || snapshot.SeatRows.Length > 32 || snapshot.Trolleys.Length > 4 || snapshot.BaggageCarts.Length > 4) return "玩家/乘客/场景对象检查点数据校验失败";
                if (snapshot.SeatRows.Any(x => x == null || x.Slot < 0 || x.Slot >= 64 || double.IsNaN(x.InstalledAt) || double.IsInfinity(x.InstalledAt) || x.InstalledAt < 0d) || snapshot.SeatRows.Select(x => x.Slot).Distinct().Count() != snapshot.SeatRows.Length) return "座椅检查点数据校验失败";
                if (snapshot.Items.Any(x => !ValidItemCheckpoint(x, true))) return "散落物检查点数据校验失败";
                if (snapshot.Trolleys.Any(x => x == null || x.Slots == null || x.Slots.Length > 32 || !Finite(x.X) || !Finite(x.Y) || !Finite(x.Z) || !Finite(x.Qx) || !Finite(x.Qy) || !Finite(x.Qz) || !Finite(x.Qw) || !Finite(x.Side) || x.Slots.Any(s => s == null || s.Index < 0 || s.Index >= 32 || (s.PlaceKey?.Length ?? 0) > 128 || !ValidItemCheckpoint(s.Item, false)) || x.Slots.Select(s => s.Index).Distinct().Count() != x.Slots.Length)) return "餐车检查点数据校验失败";
                if (snapshot.BaggageCarts.Any(x => x == null || !Finite(x.X) || !Finite(x.Y) || !Finite(x.Z) || !Finite(x.Yaw) || string.IsNullOrWhiteSpace(x.Terminal) || string.IsNullOrWhiteSpace(x.Stand) || x.Terminal.Length > 128 || x.Stand.Length > 128)) return "行李车检查点数据校验失败";
                foreach (var savedPlayer in snapshot.Players)
                {
                    if (savedPlayer.Inventory.Any(x => x == null || x.SlotOrder < 0 || x.SlotOrder >= Il2CppMayday.PlayerInventory.SlotCount || !ValidItemCheckpoint(x.Item, false)) || savedPlayer.Inventory.Select(x => x.SlotOrder).Distinct().Count() != savedPlayer.Inventory.Length) return "玩家背包检查点数据校验失败";
                    if (!Finite(savedPlayer.X) || !Finite(savedPlayer.Y) || !Finite(savedPlayer.Z) || !Finite(savedPlayer.Heading) || savedPlayer.PositionAnchor != "plane" && savedPlayer.PositionAnchor != "airport") return "玩家位置检查点数据校验失败";
                }
                Il2CppMayday.ItemCatalog.Build(); ItemCatalogReady = true;
                foreach (var item in snapshot.Items)
                    if (item == null || !IsSupportedInventoryItemId(item.ItemId)) return "当前游戏不支持检查点散落物：" + item?.ItemId;
                foreach (var trolley in snapshot.Trolleys)
                    foreach (var slot in trolley?.Slots ?? Array.Empty<TrolleySlotCheckpoint>())
                        if (slot?.Item == null || !IsSupportedInventoryItemId(slot.Item.ItemId)) return "当前游戏不支持餐车物品：" + slot?.Item?.ItemId;
                foreach (var savedPlayer in snapshot.Players)
                    foreach (var inventoryEntry in savedPlayer.Inventory)
                        if (inventoryEntry?.Item == null || !IsSupportedInventoryItemId(inventoryEntry.Item.ItemId)) return "当前游戏不支持玩家背包物品：" + inventoryEntry?.Item?.ItemId;
                if (snapshot.PendingPassengers.Any(x => x == null || x.Type > (byte)Il2CppMayday.Passengers.PaxType.Police || !Finite(x.Mood))) return "待登机乘客检查点数据不兼容";
                var p = snapshot.Plane;
                if (p.HullDamage == null || p.HullDirt == null || p.HullHoles == null || p.HullDamage.Length > 128 || p.HullDirt.Length > 128 || p.HullHoles.Length > 512 * 4 || p.HullHoles.Length % 4 != 0 || p.EngineLeftState > 3 || p.EngineRightState > 3 || p.FuelLeft < 0 || p.FuelRight < 0 || p.FuelCapacity <= 0 || p.EngineLeftHealth < 0 || p.EngineLeftHealth > 1 || p.EngineRightHealth < 0 || p.EngineRightHealth > 1 || new[] { p.FuelLeft, p.FuelRight, p.FuelCapacity, p.EngineLeftOil, p.EngineRightOil, p.EngineLeftHealth, p.EngineRightHealth, p.EngineLeftFire, p.EngineRightFire }.Any(x => float.IsNaN(x) || float.IsInfinity(x)) || p.HullDamage.Concat(p.HullDirt).Concat(p.HullHoles).Any(x => float.IsNaN(x) || float.IsInfinity(x))) return "飞机机况检查点数据超出安全范围";
                if (snapshot.SchemaVersion >= 5 && (p.EngineLeftBatteryState > 3 || p.EngineRightBatteryState > 3 || p.EngineLeftCowlState > 3 || p.EngineRightCowlState > 3 || p.FuelReserve < 0 || p.Physics != null && !ValidFlightPhysics(p.Physics) ||
                    new[] { p.EngineLeftRpm, p.EngineRightRpm, p.EngineLeftTemp, p.EngineRightTemp, p.EngineLeftThrust, p.EngineRightThrust, p.EngineLeftRepair, p.EngineRightRepair, p.EngineLeftBattery, p.EngineRightBattery, p.FuelReserve, p.ApAltitudeTarget, p.ApHeadingTarget, p.ApSpeedTarget, p.CrosswindX, p.CrosswindY }.Any(x => !Finite(x))))
                    return "schema 5 飞机扩展状态超出安全范围";
                var state = Il2CppMayday.GameState.Instance;
                var plane = Il2CppMayday.Flight.PlaneFlight.Instance;
                var preflightEngines = Il2CppMayday.Systems.SysRefs.Engines;
                var preflightFuel = Il2CppMayday.Systems.SysRefs.Fuel;
                var preflightHull = Il2CppMayday.Systems.SysRefs.Hull;
                var preflightDirector = Il2CppMayday.Passengers.PassengerDirector.Instance;
                var preflightWorld = Il2CppMayday.World.WorldManager.Instance;
                var recoverySourcePhaseReady = state != null && (state.Phase == Il2CppMayday.GamePhase.PreShift || state.Phase == Il2CppMayday.GamePhase.Ground);
                if (!recoverySourcePhaseReady || plane == null || preflightWorld == null || preflightEngines == null || preflightEngines.Length < 2 || preflightEngines[0] == null || preflightEngines[1] == null || preflightFuel == null || preflightHull == null || preflightHull.damage == null || preflightHull.dirt == null || preflightHull.holes == null || preflightDirector == null)
                    return "恢复预检失败：初始/地面阶段核心运行对象尚未全部就绪";
                var airportDefinition = ResolveCheckpointAirportDefinition(snapshot, out var definitionError);
                if (airportDefinition == null) return "无法从存档路由安全解析机场，未执行恢复：" + definitionError;
                if (!string.IsNullOrWhiteSpace(snapshot.CurrentAirportName) && !string.Equals(airportDefinition.name, snapshot.CurrentAirportName, StringComparison.OrdinalIgnoreCase)) return "存档机场定义与名称不一致，未执行恢复";
                snapshot.CurrentAirportName = airportDefinition.name;
                snapshot.CurrentAirportCode = airportDefinition.code;
                if (!safeAirborne && snapshot.DestinationAirport >= 0)
                {
                    var routeForDestination = new Il2CppMayday.World.RouteGenerator(snapshot.RunSeed);
                    var destinationDefinition = routeForDestination.Airport(snapshot.DestinationAirport);
                    snapshot.DestinationName = destinationDefinition.name;
                }
                backup = sourcePath + ".pre-recover-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + System.Guid.NewGuid().ToString("N").Substring(0, 8) + ".json";
                File.Copy(sourcePath, backup, false);
                try
                {
                    var service = LobbyService;
                    ManualJoinAllowed = false;
                    if (service != null && service.InLobby && service.Lobby.IsValid())
                    {
                        var closed = Il2CppSteamworks.SteamMatchmaking.SetLobbyJoinable(service.Lobby, false);
                        if (closed) LastJoinableSet = false;
                        LastJoinableSetKnown = closed;
                        if (!closed) return "恢复预检失败：无法暂时关闭 Steam Lobby 加入，请重建房间后重试";
                    }
                }
                catch (Exception e) { LogEvent("recovery-lock-lobby-error", e.Message); return "恢复预检失败：关闭 Steam Lobby 加入时出错：" + e.Message; }
                state.ServerResetRun(snapshot.RunSeed, snapshot.Money);
                plane.ServerResetForRun();
                var world = preflightWorld;
                var recoveryDestination = safeAirborne ? -1 : snapshot.DestinationAirport;
                world.Reconcile(snapshot.RunSeed, snapshot.CurrentAirport, recoveryDestination, true);
                ActiveRecovery = new RecoveryOperation
                {
                    Id = System.Guid.NewGuid().ToString("N"),
                    CheckpointId = checkpointId,
                    Snapshot = snapshot,
                    BackupPath = backup,
                    Generation = ServerGeneration,
                    WaitFrames = 0,
                    AwaitingAirportMaterialization = true,
                    AirportParked = false
                };
                RecoveryStage = "WAITING_AIRPORT";
                LastRecoveryMessage = safeAirborne ? "飞行安全快照恢复已启动：正在返回起飞机场并取消原航班" : "恢复任务已启动：正在按存档路由生成目标机场场景";
                LogEvent("checkpoint-recovery-started", "operation=" + ActiveRecovery.Id + " mode=" + snapshot.CaptureMode + "/" + snapshot.RecoveryMode + " checkpoint=" + checkpointId + " generation=" + ServerGeneration + " seed=" + snapshot.RunSeed + " airport=" + snapshot.CurrentAirport + " destination=" + recoveryDestination + " name=" + airportDefinition.name + " code=" + airportDefinition.code);
                return safeAirborne ? "飞行安全快照恢复已启动；将返回起飞机场 PreShift，已生成乘客和客户端本地动作不会恢复" : "恢复任务已启动；面板会持续显示进度，请勿让客人加入";
            }
            catch (Exception e)
            {
                if (backup != null) MarkRecoveryPoisoned(e.Message);
                LastRecoveryMessage = backup == null ? "恢复预检失败，未修改当前房间: " + e.Message : "恢复失败，当前房间可能处于不完整状态，请退出并新建房间后重试: " + e.Message;
                LogEvent("checkpoint-recover-error", "backup=" + backup + " exception=" + e);
                return LastRecoveryMessage;
            }
        }

        private static string ExportDiagnostics()
        {
            var stage = Path.Combine(DiagnosticPath, "work-" + System.Guid.NewGuid().ToString("N"));
            var zip = Path.Combine(Paths.ConfigPath, "MaydayHostControl-Diagnostics-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + ".zip");
            try
            {
                Directory.CreateDirectory(stage);
                var gameRoot = Paths.GameRootPath;
                var melonRoot = Path.Combine(gameRoot, "MelonLoader");
                var logCandidates = new List<string>();
                var latestLog = Path.Combine(melonRoot, "Latest.log");
                if (File.Exists(latestLog)) logCandidates.Add(latestLog);
                var logsDirectory = Path.Combine(melonRoot, "Logs");
                if (Directory.Exists(logsDirectory)) logCandidates.AddRange(Directory.GetFiles(logsDirectory, "*.log", SearchOption.TopDirectoryOnly).OrderByDescending(File.GetLastWriteTimeUtc).Take(5));
                foreach (var file in logCandidates.Distinct())
                {
                    try { File.Copy(file, Path.Combine(stage, Path.GetFileName(file)), true); } catch { }
                }
                var userData = Path.Combine(gameRoot, "UserData");
                foreach (var name in new[] { "Loader.cfg", "MelonPreferences.cfg" })
                {
                    var file = Path.Combine(userData, name);
                    if (File.Exists(file)) File.Copy(file, Path.Combine(stage, name), true);
                }
                LogEvent("diagnostics-export-start", "destination=" + zip);
                string[] events;
                lock (EventLock) events = RuntimeEvents.ToArray();
                File.WriteAllLines(Path.Combine(stage, "HostControl-RuntimeEvents.txt"), events, Encoding.UTF8);
                File.WriteAllText(Path.Combine(stage, "HostControl-State.json"), JsonSerializer.Serialize(new { sessionId = SessionId, startedUtc = SessionStartedUtc, exportedUtc = DateTime.UtcNow, players = Online.Select(x => new { x.Value.Name, steamId = x.Value.SteamId, connection = x.Key.connectionId }).ToArray(), mirrorLimit = Il2CppMirror.NetworkServer.maxConnections, managerLimit = Il2CppMirror.NetworkManager.singleton != null ? Il2CppMirror.NetworkManager.singleton.maxConnections : -1, connectionCount = Il2CppMirror.NetworkServer.connections?.Count ?? 0, lobby = GetLobbySnapshot(LobbyService), phase = Il2CppMayday.GameState.Instance != null ? Il2CppMayday.GameState.Instance.Phase.ToString() : "Unknown" }, JsonOptions), Encoding.UTF8);
                using (var archive = ZipFile.Open(zip, ZipArchiveMode.Create))
                    foreach (var file in Directory.GetFiles(stage)) archive.CreateEntryFromFile(file, Path.GetFileName(file), System.IO.Compression.CompressionLevel.Optimal);
                LastDiagnosticZip = zip;
                LogEvent("diagnostics-exported", "path=" + zip + " files=" + Directory.GetFiles(stage).Length);
                Directory.Delete(stage, true);
                return "诊断包已导出：" + zip;
            }
            catch (Exception e) { LogEvent("diagnostics-export-error", e.ToString()); try { if (Directory.Exists(stage)) Directory.Delete(stage, true); } catch { } return "导出失败: " + e.Message; }
        }

        private static string SanitizeRoomName(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            var cleaned = new string(value.Where(c => !char.IsControl(c)).ToArray()).Trim();
            return cleaned.Length <= 48 ? cleaned : cleaned.Substring(0, 48);
        }

        private static string ApplyRoomName(string name)
        {
            var service = LobbyService;
            if (service != null && service.InLobby && service.Lobby.IsValid())
            {
                var success = Il2CppSteamworks.SteamMatchmaking.SetLobbyData(service.Lobby, Il2CppMayday.Net.SteamLobbyService.KeyName, name);
                if (!success)
                {
                    LogEvent("room-name-live", "active=true success=false name=" + name);
                    return "Steam Lobby 拒絕更新房間名稱";
                }
                service.LobbyName = name;
                LogEvent("room-name-live", "active=true success=true lobby=" + service.Lobby.m_SteamID + " name=" + name);
            }
            else LogEvent("room-name-live", "active=false name=" + (name.Length == 0 ? "<default>" : name));

            RoomName.Value = name;
            Settings.Save();
            return service != null && service.InLobby
                ? "已实时更新当前房间名称，并保存为新房间默认值"
                : "已保存大厅名称；将在下次创建大厅时应用";
        }

        private static string ApplyRoomCapacity(int target)
        {
            if (target < 1 || target > 250) return "Mod 人数范围为 1 到 250；实际 Lobby 上限由 Steam 决定";
            var service = LobbyService;
            if (service != null && service.InLobby && service.Lobby.IsValid())
            {
                var before = Il2CppSteamworks.SteamMatchmaking.GetLobbyMemberLimit(service.Lobby);
                var success = Il2CppSteamworks.SteamMatchmaking.SetLobbyMemberLimit(service.Lobby, target);
                var after = Il2CppSteamworks.SteamMatchmaking.GetLobbyMemberLimit(service.Lobby);
                if (!success || after != target)
                {
                    LogEvent("room-capacity-live", "success=false requested=" + target + " lobby=" + before + "->" + after + " mirror=" + Il2CppMirror.NetworkServer.maxConnections + " connections=" + (Il2CppMirror.NetworkServer.connections?.Count ?? 0));
                    return "Steam Lobby 未接受人数上限 " + target + "（当前 " + after + "）；Mirror 与配置未变更";
                }
                PlayerLimit.Value = target;
                Settings.Save();
                SyncMirrorCapacity("admin-live");
                LogEvent("room-capacity-live", "success=true target=" + target + " lobby=" + before + "->" + after + " mirror=" + Il2CppMirror.NetworkServer.maxConnections + " connections=" + (Il2CppMirror.NetworkServer.connections?.Count ?? 0));
                return "已实时将房间人数上限改为 " + target + "；现有玩家不会被踢出";
            }

            PlayerLimit.Value = target;
            Settings.Save();
            SyncMirrorCapacity("admin-live");
            LogEvent("room-capacity-live", "active=false target=" + target);
            return "已将人数上限保存为 " + target + "；将在当前服务器启动或下次开房时应用";
        }

        private static string ApplyRoomJoinable(bool joinable)
        {
            if (joinable && (RecoveryPoisoned || ActiveRecovery != null || RecoveryStage == "WAITING_RUNTIME" || RecoveryStage == "APPLYING" || RecoveryStage == "FATAL"))
                return "当前恢复状态不允许重新开放房间（设置未保存）";
            var service = LobbyService;
            if (service == null || !service.InLobby || !service.Lobby.IsValid()) return "当前没有可修改的 Steam Lobby";
            if (!joinable) ManualJoinAllowed = false;
            var success = Il2CppSteamworks.SteamMatchmaking.SetLobbyJoinable(service.Lobby, joinable);
            LastJoinableSetKnown = success;
            if (success)
            {
                LastJoinableSet = joinable;
                ManualJoinAllowed = joinable;
            }
            LogEvent("room-joinable-live", "success=" + success + " requested=" + joinable + " effective=" + ManualJoinAllowed + " lobby=" + service.Lobby.m_SteamID);
            if (!success) return joinable ? "Steam Lobby 开放失败，服务端仍保持关闭" : "服务端已禁止新玩家，但 Steam Lobby 状态更新失败";
            return joinable ? "已实时开放房间，允许新玩家加入" : "已实时关闭房间，不影响现有玩家";
        }

        private static string ApplyRoomVisibility(string value)
        {
            Il2CppMayday.LobbyVisibility gameVisibility;
            Il2CppSteamworks.ELobbyType steamVisibility;
            string label;
            switch (value)
            {
                case "Private": gameVisibility = Il2CppMayday.LobbyVisibility.Private; steamVisibility = Il2CppSteamworks.ELobbyType.k_ELobbyTypePrivate; label = "仅邀请"; break;
                case "FriendsOnly": gameVisibility = Il2CppMayday.LobbyVisibility.FriendsOnly; steamVisibility = Il2CppSteamworks.ELobbyType.k_ELobbyTypeFriendsOnly; label = "仅好友"; break;
                case "Public": gameVisibility = Il2CppMayday.LobbyVisibility.Public; steamVisibility = Il2CppSteamworks.ELobbyType.k_ELobbyTypePublic; label = "公开"; break;
                default: return "可见性必须为 Private、FriendsOnly 或 Public";
            }
            var service = LobbyService;
            if (service == null || !service.InLobby || !service.Lobby.IsValid()) return "当前没有可修改的 Steam Lobby";
            var before = service.Visibility;
            var success = Il2CppSteamworks.SteamMatchmaking.SetLobbyType(service.Lobby, steamVisibility);
            VisibilitySetKnown = true;
            VisibilitySetSucceeded = success;
            LastVisibilityRequested = value;
            LastVisibilityChangedUtc = DateTime.UtcNow;
            if (success) service.Visibility = gameVisibility;
            LogEvent("room-visibility-live", "success=" + success + " requested=" + value + " game=" + before + "->" + service.Visibility + " advertise=" + service.Advertise + " lobby=" + service.Lobby.m_SteamID);
            return success ? "Steam API 已接受可见性变更：" + label + "；外部列表传播仍需 Steam 完成" : "Steam Lobby 可见性更新失败";
        }

        private static bool IsLocalHostConnection(NetworkConnectionToClient connection)
        {
            var local = Il2CppMirror.NetworkServer.localConnection;
            return connection != null && local != null && connection.connectionId == local.connectionId;
        }

        private static bool IsLocalHostSteamId(ulong steamId)
        {
            return Online.Any(x => x.Value.SteamId == steamId && IsLocalHostConnection(x.Key));
        }

        private static string KickAllRemotePlayers()
        {
            if (!Il2CppMirror.NetworkServer.activeHost) return "当前没有运行中的房主服务器";
            if (Il2CppMirror.NetworkServer.localConnection == null) return "无法确认房主本地连接，未执行批量踢出";
            var targets = Online.Where(x => x.Key != null && !IsLocalHostConnection(x.Key)).ToArray();
            var kicked = 0;
            var failed = 0;
            foreach (var target in targets)
            {
                try
                {
                    target.Key.Disconnect();
                    Online.Remove(target.Key);
                    kicked++;
                    LogEvent("kick-all-player", "name=" + target.Value.Name + " steam=" + target.Value.SteamId + " conn=" + target.Key.connectionId);
                }
                catch (Exception e)
                {
                    failed++;
                    LogEvent("kick-all-player-error", "steam=" + target.Value.SteamId + " conn=" + target.Key.connectionId + " error=" + e.Message);
                }
            }
            LogEvent("kick-all", "kicked=" + kicked + " failed=" + failed + " hostExcluded=true onlineAfter=" + Online.Count);
            if (targets.Length == 0) return "没有可踢出的远程玩家；房主未受影响";
            return failed == 0 ? "已踢出其他玩家 " + kicked + " 人；房主未受影响" : "已踢出 " + kicked + " 人，失败 " + failed + " 人；房主未受影响";
        }

        private static string Execute(string line)
        {
            var parts = line.Trim().Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return "空命令";
            var command = parts[0].ToLowerInvariant();
            if (command == "checkpoint" && parts.Length >= 2)
            {
                try
                {
                    var name = parts[1] == "-" ? string.Empty : SanitizeCheckpointName(Encoding.UTF8.GetString(Convert.FromBase64String(parts[1])));
                    return SaveCheckpoint(name);
                }
                catch (Exception e) { return "存档名称无效: " + e.Message; }
            }
            if (command == "recover" && parts.Length >= 2) return RecoverCheckpoint(parts[1]);
            if (command == "deletecheckpoint" && parts.Length >= 2)
            {
                var path = ResolveCheckpointPath(parts[1]);
                if (path == null || IsReadOnlyLegacyCheckpoint(parts[1])) return "存档不存在或不可删除";
                try { File.Delete(path); LogEvent("checkpoint-deleted", "id=" + parts[1]); return "已删除所选存档"; }
                catch (Exception e) { return "删除存档失败: " + e.Message; }
            }
            if (command == "exportlogs") return ExportDiagnostics();
            if (command == "setroom" && parts.Length >= 2)
            {
                try
                {
                    var name = parts[1] == "-" ? string.Empty : SanitizeRoomName(Encoding.UTF8.GetString(Convert.FromBase64String(parts[1])));
                    return ApplyRoomName(name);
                }
                catch (Exception e) { Log.LogError("[MaydayHostControl] 保存大厅名称失败: " + e.Message); return "保存大厅名称失败: " + e.Message; }
            }
            if (command == "setcapacity" && parts.Length >= 2 && int.TryParse(parts[1], out var capacity)) return ApplyRoomCapacity(capacity);
            if (command == "settag" && parts.Length >= 3 && ulong.TryParse(parts[1], out var tagId))
            {
                try
                {
                    var tag = Encoding.UTF8.GetString(Convert.FromBase64String(parts[2]));
                    return ApplyPlayerTag(tagId.ToString(), tag);
                }
                catch (Exception e) { return "玩家标签无效: " + e.Message; }
            }
            if (command == "setjoinable" && parts.Length >= 2 && bool.TryParse(parts[1], out var joinable)) return ApplyRoomJoinable(joinable);
            if (command == "setvisibility" && parts.Length >= 2) return ApplyRoomVisibility(parts[1]);
            if (command == "kickall") return KickAllRemotePlayers();
            if (command == "help")
            {
                Log.LogInfo("[MaydayHostControl] players | kick <序号|SteamID64> | kickall | ban <SteamID64> | unban <SteamID64> | bans");
                return "可用命令帮助已输出到日志";
            }
            if (command == "players")
            {
                foreach (var pair in Online) Log.LogInfo("[MaydayHostControl] Name=" + pair.Value.Name + " SteamID=" + pair.Value.SteamId + " Connection=" + pair.Key.connectionId);
                if (Online.Count == 0) Log.LogInfo("[MaydayHostControl] 当前没有已认证玩家");
                return "在线玩家数: " + Online.Count;
            }
            if (command == "bans")
            {
                foreach (var id in Blacklist.OrderBy(x => x)) Log.LogInfo("[MaydayHostControl] 黑名单 SteamID=" + id);
                return "黑名单条目数: " + Blacklist.Count;
            }
            if (parts.Length < 2) { Log.LogWarning("[MaydayHostControl] 参数不足，输入 help 查看命令"); return "缺少命令参数"; }
            if (command == "ban" && ulong.TryParse(parts[1], out var banId))
            {
                if (IsLocalHostSteamId(banId)) return "不能拉黑或踢出房主自己";
                if (Blacklist.Add(banId))
                {
                    var player = Online.Values.FirstOrDefault(x => x.SteamId == banId);
                    BanRecords[banId] = new BanRecord { PlayerName = player?.Name, BannedAtUtc = DateTimeOffset.UtcNow };
                }
                SaveBlacklist(); var kicked = Disconnect(banId, "blacklisted");
                Log.LogInfo("[MaydayHostControl] 已加入黑名单并断开 SteamID=" + banId); return kicked ? "已加入黑名单并踢出玩家 " + banId : "已加入黑名单；该玩家当前不在线 " + banId;
            }
            if (command == "unban" && ulong.TryParse(parts[1], out var unbanId))
            {
                var removed = Blacklist.Remove(unbanId); BanRecords.Remove(unbanId); SaveBlacklist(); Log.LogInfo("[MaydayHostControl] 已移除黑名单 SteamID=" + unbanId); return removed ? "已解除封禁 " + unbanId : "该 SteamID 不在黑名单中";
            }
            if (command == "kick")
            {
                ulong id;
                if (ulong.TryParse(parts[1], out id)) return Disconnect(id, "manual") ? "已踢出玩家 " + id : "玩家当前不在线 " + id;
                if (int.TryParse(parts[1], out var index))
                {
                    var pair = Online.ElementAtOrDefault(index - 1);
                    if (pair.Key != null) return Disconnect(pair.Value.SteamId, "manual-index") ? "已踢出玩家 " + pair.Value.Name + " (" + pair.Value.SteamId + ")" : "不能踢出房主或玩家已离线";
                    return "玩家序号无效";
                }
                return "SteamID64 或玩家序号格式无效";
            }
            Log.LogWarning("[MaydayHostControl] 未知命令: " + command);
            return "未知命令: " + command;
        }

        private static bool Disconnect(ulong id, string reason)
        {
            var pair = Online.FirstOrDefault(x => x.Value.SteamId == id);
            if (pair.Key == null) { Log.LogInfo("[MaydayHostControl] 玩家当前不在线 SteamID=" + id); return false; }
            if (IsLocalHostConnection(pair.Key)) { LogEvent("disconnect-rejected", "steam=" + id + " reason=local_host"); return false; }
            pair.Key.Disconnect(); Online.Remove(pair.Key);
            Log.LogInfo("[MaydayHostControl] 已断开 Name=" + pair.Value.Name + " SteamID=" + id + " reason=" + reason);
            return true;
        }

        internal static void LoadBlacklist()
        {
            Blacklist = new HashSet<ulong>();
            BanRecords.Clear();
            if (!File.Exists(BlacklistPath)) { SaveBlacklist(); return; }
            foreach (var line in File.ReadAllLines(BlacklistPath))
            {
                var text = line.Trim();
                if (text.Length > 0 && !text.StartsWith("#") && ulong.TryParse(text, out var id)) Blacklist.Add(id);
            }
            if (File.Exists(BanRecordsPath))
            {
                try
                {
                    var records = JsonSerializer.Deserialize<Dictionary<ulong, BanRecord>>(File.ReadAllText(BanRecordsPath));
                    if (records != null)
                        foreach (var pair in records)
                            if (Blacklist.Contains(pair.Key) && pair.Value != null) BanRecords[pair.Key] = pair.Value;
                }
                catch (Exception e) { Log.LogWarning("[MaydayHostControl] 黑名单详情读取失败，保留 SteamID 封禁列表: " + e.Message); }
            }
            Log.LogInfo("[MaydayHostControl] 已加载黑名单条目: " + Blacklist.Count);
        }

        private static void SaveBlacklist()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(BlacklistPath));
            File.WriteAllLines(BlacklistPath, Blacklist.OrderBy(x => x).Select(x => x.ToString()));
            File.WriteAllText(BanRecordsPath, JsonSerializer.Serialize(BanRecords.Where(x => Blacklist.Contains(x.Key)).ToDictionary(x => x.Key, x => x.Value), JsonOptions));
        }

        internal static void HandlePlayerDisconnected(ulong steamId)
        {
            if (steamId == 0 || !PendingPlayerRecovery.ContainsKey(steamId)) return;
            PlayerRestoreStarted.Remove(steamId);
            PlayerItemsRemaining.Remove(steamId);
            PlayerRestoreIndex.Remove(steamId);
            PlayerRestoreDelegates.Remove(steamId);
            PlayerRestoreDeadlines.Remove(steamId);
            PlayerRestoreAttempts.Remove(steamId);
            LogEvent("player-restore-paused", "steam=" + steamId + " willRetryOnReconnect=true");
        }

        internal static bool IsBlacklisted(ulong id) => Blacklist.Contains(id);
        internal static bool IsRecoveryPoisoned => RecoveryPoisoned;
        internal static bool IsRecoveryInProgress => ActiveRecovery != null;
        internal static bool IsManualJoinAllowed => ManualJoinAllowed;
        internal static string RecoveryLockReason => RecoveryPoisonReason;
    }

    [HarmonyPatch]
    internal static class CommandPumpPatch
    {
        [HarmonyPostfix]
        [HarmonyPatch(typeof(Il2CppMayday.UI.ConsoleRelay), "Update")]
        private static void Postfix() => Plugin.PumpCommands();
    }

    internal static class ConsoleHotkeyPatchDisabled
    {
    }

    [HarmonyPatch]
    internal static class AuthPatch
    {
        [HarmonyPrefix]
        [HarmonyPatch(typeof(Il2CppMayday.Net.MaydayAuthenticator), "OnAuthRequest")]
        private static bool Prefix(NetworkConnectionToClient conn, Il2CppMayday.Net.NetAuthRequestMessage msg)
        {
            ulong id = msg.steamId;
            Plugin.LogEvent("auth-request", "steam=" + id + " conn=" + conn.connectionId + " name=" + (msg.name ?? "") + " lobbyActive=" + (Plugin.LobbyService != null && Plugin.LobbyService.InLobby) + " lobbyMember=" + (Il2CppMayday.Net.NetSession.Instance != null && Il2CppMayday.Net.NetSession.Instance.IsLobbyMember(id)) + " manualJoin=" + Plugin.IsManualJoinAllowed + " capacity=" + Plugin.AuthoritativeCapacity + " connections=" + (Il2CppMirror.NetworkServer.connections?.Count ?? 0));
            if (Plugin.IsRecoveryInProgress)
            {
                Plugin.Log.LogWarning("[MaydayHostControl] 恢复进行中，拒绝玩家加入 SteamID=" + id);
                conn.Disconnect();
                return false;
            }
            if (Plugin.IsRecoveryPoisoned)
            {
                Plugin.Log.LogError("[MaydayHostControl] 拒绝玩家加入恢复不完整的房间 SteamID=" + id + " reason=" + Plugin.RecoveryLockReason);
                conn.Disconnect();
                return false;
            }
            if (!Plugin.IsManualJoinAllowed)
            {
                Plugin.Log.LogWarning("[MaydayHostControl] 房主已关闭新玩家加入 SteamID=" + id);
                conn.Disconnect();
                return false;
            }
            if (Plugin.IsBlacklisted(id))
            {
                Plugin.Log.LogWarning("[MaydayHostControl] 拒绝黑名单玩家 SteamID=" + id);
                conn.Disconnect();
                return false;
            }
            var playerName = string.IsNullOrWhiteSpace(msg.name) ? "(未提供游戏名)" : msg.name.Trim();
            Plugin.Online[conn] = new Plugin.OnlinePlayer { SteamId = id, Name = playerName };
            Plugin.Log.LogInfo("[MaydayHostControl] 认证玩家 Name=" + playerName + " SteamID=" + id + " Connection=" + conn.connectionId);
            return true;
        }
    }

    [HarmonyPatch]
    internal static class LobbyCreationDefaultsPatch
    {
        [HarmonyPrefix]
        [HarmonyPatch(typeof(Il2CppMayday.Net.SteamLobbyService), "Create")]
        private static void Prefix(ref Il2CppMayday.LobbyVisibility __0)
        {
            if (!Plugin.RoomDefaultsConfigured.Value) return;
            if (Enum.TryParse<Il2CppMayday.LobbyVisibility>(Plugin.DefaultRoomVisibility.Value, true, out var visibility) &&
                (visibility == Il2CppMayday.LobbyVisibility.Private || visibility == Il2CppMayday.LobbyVisibility.FriendsOnly || visibility == Il2CppMayday.LobbyVisibility.Public))
                // Let vanilla Create carry the same enum into its visibility state and lobby metadata.
                __0 = visibility;
        }
    }

    [HarmonyPatch]
    internal static class LobbyNamePatch
    {
        [HarmonyPostfix]
        [HarmonyPriority(Priority.Last)]
        [HarmonyPatch(typeof(Il2CppMayday.Net.SteamLobbyService), "OnLobbyCreated")]
        private static void Postfix(Il2CppMayday.Net.SteamLobbyService __instance, Il2CppSteamworks.LobbyCreated_t result, bool ioFailure)
        {
            Plugin.LobbyService = __instance;
            var name = Plugin.RoomName.Value;
            if (ioFailure || !__instance.Lobby.IsValid()) { Plugin.LogEvent("lobby-created-failed", "ioFailure=" + ioFailure + " result=" + result.m_eResult); return; }
            try
            {
                var lobby = __instance.Lobby;
                var limitBefore = Il2CppSteamworks.SteamMatchmaking.GetLobbyMemberLimit(lobby);
                var targetLimit = Plugin.AuthoritativeCapacity;
                var limitSet = Il2CppSteamworks.SteamMatchmaking.SetLobbyMemberLimit(lobby, targetLimit);
                var desiredJoinable = Plugin.DefaultRoomJoinable.Value && !Plugin.IsRecoveryPoisoned && !Plugin.IsRecoveryInProgress;
                Plugin.ManualJoinAllowed = desiredJoinable;
                var joinableSet = Il2CppSteamworks.SteamMatchmaking.SetLobbyJoinable(lobby, desiredJoinable);
                Plugin.LastJoinableSetKnown = joinableSet;
                if (joinableSet) Plugin.LastJoinableSet = desiredJoinable;
                if (!string.IsNullOrWhiteSpace(name))
                {
                    var key = Il2CppMayday.Net.SteamLobbyService.KeyName;
                    var success = Il2CppSteamworks.SteamMatchmaking.SetLobbyData(lobby, key, name);
                    __instance.LobbyName = name;
                    Plugin.LogEvent("lobby-name", "success=" + success + " name=" + name);
                }
                var limitAfter = Il2CppSteamworks.SteamMatchmaking.GetLobbyMemberLimit(lobby);
                Plugin.LogEvent("lobby-created", "id=" + lobby.m_SteamID + " result=" + result.m_eResult + " visibility=" + __instance.Visibility + " advertise=" + __instance.Advertise + " requestedLimit=" + targetLimit + " limit=" + limitBefore + "->" + limitAfter + " limitSet=" + limitSet + " joinableRequested=" + desiredJoinable + " joinableSet=" + joinableSet + " members=" + Il2CppSteamworks.SteamMatchmaking.GetNumLobbyMembers(lobby));
            }
            catch (Exception e) { Plugin.LogEvent("lobby-created-error", e.ToString()); }
        }
    }

    [HarmonyPatch]
    internal static class PlayerAddedRecoveryPatch
    {
        [HarmonyPostfix]
        [HarmonyPatch(typeof(Il2CppMayday.Net.MaydayNetworkManager), "OnServerAddPlayer")]
        private static void Postfix(NetworkConnectionToClient conn)
        {
            try
            {
                var player = conn?.identity != null ? conn.identity.GetComponent<Il2CppMayday.PlayerNet>() : null;
                Plugin.LogEvent("player-added", "conn=" + (conn?.connectionId ?? -1) + " steam=" + (player?.SteamId.ToString() ?? "unknown") + " inventoryReady=" + (player?.Inventory != null));
                Plugin.PumpPlayerRecovery();
            }
            catch (Exception e) { Plugin.LogEvent("player-added-error", e.ToString()); }
        }
    }

    [HarmonyPatch]
    internal static class DisconnectPatch
    {
        [HarmonyPostfix]
        [HarmonyPatch(typeof(Il2CppMayday.Net.MaydayNetworkManager), "OnServerError")]
        private static void OnServerError(Il2CppMirror.NetworkConnectionToClient conn, Il2CppMirror.TransportError error, string reason)
        {
            Plugin.LogEvent("server-transport-error", "conn=" + (conn != null ? conn.connectionId : -1) + " address=" + (conn != null ? conn.address : "") + " error=" + error + " reason=" + (reason ?? "") + " current=" + (Il2CppMirror.NetworkServer.connections?.Count ?? 0) + " limit=" + Il2CppMirror.NetworkServer.maxConnections);
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Il2CppMayday.Net.MaydayNetworkManager), "OnServerDisconnect")]
        private static void Postfix(NetworkConnectionToClient conn)
        {
            Plugin.Online.TryGetValue(conn, out var player);
            Plugin.LogEvent("mirror-disconnect", "conn=" + conn.connectionId + " steam=" + (player?.SteamId.ToString() ?? "unknown") + " name=" + (player?.Name ?? "unknown") + " remaining=" + Il2CppMirror.NetworkServer.connections.Count + " mirrorLimit=" + Il2CppMirror.NetworkServer.maxConnections);
            if (player != null) Plugin.HandlePlayerDisconnected(player.SteamId);
            Plugin.Online.Remove(conn);
        }
    }

    [HarmonyPatch]
    internal static class ServerStartCapacityPatch
    {
        [HarmonyPostfix]
        [HarmonyPatch(typeof(Il2CppMayday.Net.MaydayNetworkManager), "OnStartServer")]
        private static void Postfix() => Plugin.OnServerStarted();
    }

    [HarmonyPatch]
    internal static class ServerStopRecoveryPatch
    {
        [HarmonyPostfix]
        [HarmonyPatch(typeof(Il2CppMayday.Net.MaydayNetworkManager), "OnStopServer")]
        private static void Postfix() => Plugin.OnServerStopped();
    }

    [HarmonyPatch(typeof(Il2CppMirror.NetworkManager), "SetupServer")]
    internal static class ServerSetupCapacityPatch
    {
        [HarmonyPrefix]
        private static void Prefix(Il2CppMirror.NetworkManager __instance)
        {
            // SetupServer starts Listen/ServerStart before OnStartServer and activeHost become available.
            if (__instance != null) __instance.maxConnections = Plugin.AuthoritativeCapacity;
        }
    }

    [HarmonyPatch]
    internal static class SteamTransportCreateCapacityPatch
    {
        private static IEnumerable<System.Reflection.MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(Il2CppMirror.FizzySteam.NextServer), "CreateServer");
            yield return AccessTools.Method(typeof(Il2CppMirror.FizzySteam.LegacyServer), "CreateServer");
        }

        [HarmonyPrefix]
        private static void Prefix(ref int __1) => __1 = Plugin.AuthoritativeCapacity;
    }

    [HarmonyPatch(typeof(Il2CppMirror.FizzySteam.NextServer), "OnConnectionStatusChanged")]
    internal static class SteamNextAdmissionCapacityPatch
    {
        [HarmonyPrefix]
        private static void Prefix(Il2CppMirror.FizzySteam.NextServer __instance)
        {
            if (Il2CppMirror.NetworkServer.activeHost && __instance != null)
                __instance.maxConnections = Plugin.AuthoritativeCapacity;
        }
    }

    [HarmonyPatch(typeof(Il2CppMirror.FizzySteam.LegacyServer), "OnNewConnection")]
    internal static class SteamLegacyAdmissionCapacityPatch
    {
        [HarmonyPrefix]
        private static void Prefix(Il2CppMirror.FizzySteam.LegacyServer __instance)
        {
            if (Il2CppMirror.NetworkServer.activeHost && __instance != null)
                __instance.maxConnections = Plugin.AuthoritativeCapacity;
        }
    }

    [HarmonyPatch]
    internal static class ServerConnectCapacityPatch
    {
        [HarmonyPrefix]
        [HarmonyPatch(typeof(Il2CppMayday.Net.MaydayNetworkManager), "OnServerConnect")]
        private static void Prefix(NetworkConnectionToClient conn)
        {
            Plugin.SyncMirrorCapacity("OnServerConnect");
            Plugin.LogEvent("mirror-connect", "conn=" + conn.connectionId + " address=" + conn.address + " current=" + Il2CppMirror.NetworkServer.connections.Count + " limit=" + Il2CppMirror.NetworkServer.maxConnections);
        }
    }

    [HarmonyPatch]
    internal static class IsConnectionAllowedCapacityPatch
    {
        [HarmonyPrefix]
        [HarmonyPatch(typeof(Il2CppMirror.NetworkServer), "IsConnectionAllowed")]
        private static bool Prefix(int connectionId, string address, ref bool __result)
        {
            Plugin.SyncMirrorCapacity("IsConnectionAllowed");
            Plugin.LogConnectionDecision("Il2CppMirror.IsConnectionAllowed", connectionId, address);
            if (!Il2CppMirror.NetworkServer.activeHost) return true;
            if (!Plugin.IsConfiguredConnectionLimitReached("Il2CppMirror.IsConnectionAllowed", connectionId, address)) return true;
            __result = false;
            return false;
        }
    }

    [HarmonyPatch]
    internal static class AuthFullCapacityPatch
    {
        [HarmonyPrefix]
        [HarmonyPatch(typeof(Il2CppMayday.Net.MaydayAuthenticator), "IsFull")]
        private static bool Prefix(ref bool __result)
        {
            if (!Il2CppMirror.NetworkServer.activeHost) return true;
            __result = Plugin.IsConfiguredAuthenticatorFull();
            return false;
        }
    }

    [HarmonyPatch]
    internal static class LobbyMemberUpdatePatch
    {
        [HarmonyPostfix]
        [HarmonyPatch(typeof(Il2CppMayday.Net.SteamLobbyService), "OnChatUpdate")]
        private static void Postfix(Il2CppMayday.Net.SteamLobbyService __instance, object[] __args)
        {
            Plugin.LobbyService = __instance;
            try
            {
                var update = (Il2CppSteamworks.LobbyChatUpdate_t)__args[0];
                var lobby = __instance.Lobby;
                if (!lobby.IsValid() || lobby.m_SteamID != update.m_ulSteamIDLobby) return;
                Plugin.LogEvent("lobby-member-change", "lobby=" + update.m_ulSteamIDLobby + " changed=" + update.m_ulSteamIDUserChanged + " actor=" + update.m_ulSteamIDMakingChange + " flags=0x" + update.m_rgfChatMemberStateChange.ToString("X") + " members=" + Il2CppSteamworks.SteamMatchmaking.GetNumLobbyMembers(lobby) + " limit=" + Il2CppSteamworks.SteamMatchmaking.GetLobbyMemberLimit(lobby) + " visibility=" + __instance.Visibility + " advertise=" + __instance.Advertise);
            }
            catch (Exception e) { Plugin.LogEvent("lobby-member-change-error", e.ToString()); }
        }
    }
}
