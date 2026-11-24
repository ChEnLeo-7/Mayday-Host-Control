using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppMayday;
using Il2CppMayday.Airport;
using Il2CppMayday.Passengers;
using Il2CppMayday.Systems;
using Il2CppMayday.UI;
using Il2CppMayday.World;
using Il2CppMirror;
using Il2CppTMPro;
using UnityEngine;

namespace MaydayHostControl
{
    public sealed partial class Plugin
    {
        private static ConfigEntry<bool> AutoDispatchDisabledConfig;
        internal static bool AutoDispatchBlocked;
        private static string WorldTimeInput = "08:00";
        private static int WorldWeatherIndex;
        private static int WorldIntensityIndex = 0;
        private static int WorldSpeedIndex;
        private static int PassengerEventIndex;
        private static int SpecialEventIndex;
        private static UIButton PassengerEventButton;
        private static UIButton SpecialEventButton;
        private static TMP_Text PassengerEventInfo;
        private static TMP_Text SpecialEventInfo;
        private static bool PassengerButtonYellow;
        private static bool SpecialButtonYellow;
        private static bool PassengerEventRejected;
        private static bool SpecialEventRejected;
        private static float PassengerEventActiveUntil;
        private static bool SpecialEventTracking;
        private static int WorldToiletIndex = 3;
        private static int WorldTemperIndex = 1;
        private static int WorldSleepIndex = 1;
        private static bool WorldInitialized;
        private static UISelector WorldWeatherSelector;
        private static UISelector WorldIntensitySelector;
        private static UISelector WorldSpeedSelector;
        private static UISelector WorldToiletSelector;
        private static UISelector WorldTemperSelector;
        private static UISelector WorldSleepSelector;
        private static UIToggle WorldDispatchToggle;
        private static int WorldDispatchVisualRefreshFrame = -1;
        private static readonly List<(UIButton Button, Func<bool> IsDefault)> WorldDefaultButtons = new List<(UIButton, Func<bool>)>();
        private static float WorldTimeSpeed = 1f;
        private static bool WorldTimeControlActive;
        private static TMP_InputField WorldTimeInputField;
        private static bool PendingAutoDispatch;
        private static int AppliedWeatherIndex = -1;
        private static int AppliedIntensityIndex = -1;
        private static int AppliedSpeedIndex;
        private static int AppliedToiletIndex = 3;
        private static int AppliedTemperIndex = 1;
        private static int AppliedSleepIndex = 1;
        private static bool AppliedAutoDispatch;
        private static float WorldTimeBaselineHour;
        private static bool WorldTimeDirty;
        private const float BaseClockRate = 0.02f;

        private static bool AiBasesCaptured;
        private static float BasePeeEveryMin, BasePeeEveryMax, BaseFirstPeeMin, BaseFirstPeeMax, BasePeeBladder, BasePeeMood;
        private static int BasePeeMax;
        private static float BaseRageAt, BaseAngerRate, BaseShoutAt;
        private static float BaseSleepMin, BaseSleepMax, BasePickMin, BasePickMax;

        private static string L(string zh, string en)
        {
            try
            {
                var code = Loc.LanguageCodes[Loc.CurrentIndex] ?? string.Empty;
                return code.StartsWith("zh", StringComparison.OrdinalIgnoreCase) ? zh : en;
            }
            catch { return en; }
        }

        private static void WorldDiscardPendingChanges()
        {
            WorldWeatherIndex = AppliedWeatherIndex;
            WorldIntensityIndex = AppliedIntensityIndex;
            WorldSpeedIndex = AppliedSpeedIndex;
            WorldToiletIndex = AppliedToiletIndex;
            WorldTemperIndex = AppliedTemperIndex;
            WorldSleepIndex = AppliedSleepIndex;
            PendingAutoDispatch = AppliedAutoDispatch;
            WorldTimeDirty = false;
            WorldTimeBaselineHour = CurrentHour();
            WorldTimeInput = FormatHour(WorldTimeBaselineHour);
        }

        private static void SetWorldStatus(string text)
        {
            Log?.LogInfo("[WorldControl] " + (text ?? string.Empty));
            RefreshWorldDefaultButtons();
        }

        // ---------------------------------------------------------------- UI

        private static string FormatHour(float hours)
        {
            var h = Mathf.Repeat(hours, 24f);
            var hh = Mathf.FloorToInt(h);
            var mm = Mathf.FloorToInt((h - hh) * 60f);
            return hh.ToString("00") + ":" + mm.ToString("00");
        }

        private static float CurrentHour()
        {
            try
            {
                var net = WorldNet.Live;
                if (net == null) return 0f;
                return (float)net.NetworkClock.HoursAt(WorldAuthority.Time);
            }
            catch { return 0f; }
        }

        private static void BuildWorldControlSections()
        {
            WorldDefaultButtons.Clear();
            if (!WorldTimeDirty) WorldTimeInput = FormatHour(CurrentHour());
            SyncWorldStateFromGame();

            NativeSettingsSection(GuiContent, Loc.T("WC.WorldControl"));

            var timeControl = NewNativeSettingRow(GuiContent, "WcTime", Loc.T("WC.GameTime"),
                Loc.T("WC.GameTimeHelp"), 64f);
            WorldTimeInputField = UIF.Input(timeControl, "WcTimeInput", WorldTimeInput, "HH:MM", 5,
                NativeStringAction(value => { WorldTimeInput = value ?? string.Empty; WorldTimeDirty = true; SetWorldStatus(PendingHint()); UpdateRoomSaveButtons(); }), 170f, 48f);
            WorldTimeBaselineHour = CurrentHour();
            AddWorldDefault(timeControl, "WcTimeDefault", ApplyDefaultTime, IsTimeAtDefault);

            WorldWeatherSelector = UIF.Selector(WorldControlRow("WcWeather", Loc.T("WC.Weather"), null),
                "WcWeatherSelector", new Il2CppStringArray(WorldWeatherNames()), WorldWeatherIndex,
                NativeIntAction(index =>
                {
                    WorldWeatherIndex = Mathf.Clamp(index, 0, 5);
                    SetWorldStatus(PendingHint());
                    UpdateRoomSaveButtons();
                }), 200f);
            FitSelectorWidth(WorldWeatherSelector, WorldWeatherNames(), 150f, 320f);
            AddWorldDefault(WorldWeatherSelector.transform.parent, "WcWeatherDefault", () =>
            {
                WorldWeatherIndex = 0;
                WorldWeatherSelector.SetIndex(0);
                WorldIntensityIndex = 0;
                if (WorldIntensitySelector != null) WorldIntensitySelector.SetIndex(0);
                SetWorldStatus(PendingHint());
                UpdateRoomSaveButtons();
            }, () => WorldWeatherIndex == 0 && WorldIntensityIndex == 0);

            WorldIntensitySelector = UIF.Selector(WorldControlRow("WcIntensity", Loc.T("WC.WeatherIntensity"), null),
                "WcIntensitySelector", new Il2CppStringArray(new[] { "0.05", "0.3", "0.6", "1.0" }), WorldIntensityIndex,
                NativeIntAction(index =>
                {
                    WorldIntensityIndex = Mathf.Clamp(index, 0, 3);
                    SetWorldStatus(PendingHint());
                    UpdateRoomSaveButtons();
                }), 150f);
            FitSelectorWidth(WorldIntensitySelector, new[] { "0.05", "0.3", "0.6", "1.0" }, 110f, 200f);
            AddWorldDefault(WorldIntensitySelector.transform.parent, "WcIntensityDefault", () =>
            {
                WorldIntensityIndex = 0;
                WorldIntensitySelector.SetIndex(0);
                SetWorldStatus(PendingHint());
                UpdateRoomSaveButtons();
            }, () => WorldIntensityIndex == 0);

            WorldSpeedSelector = UIF.Selector(WorldControlRow("WcSpeed", Loc.T("WC.TimeSpeed"), null),
                "WcSpeedSelector", new Il2CppStringArray(WorldSpeedNames()), WorldSpeedIndex,
                NativeIntAction(index =>
                {
                    WorldSpeedIndex = Mathf.Clamp(index, 0, 3);
                    SetWorldStatus(PendingHint());
                    UpdateRoomSaveButtons();
                }), 150f);
            FitSelectorWidth(WorldSpeedSelector, WorldSpeedNames(), 110f, 200f);
            AddWorldDefault(WorldSpeedSelector.transform.parent, "WcSpeedDefault", () =>
            {
                WorldSpeedIndex = 0;
                WorldSpeedSelector.SetIndex(0);
                SetWorldStatus(PendingHint());
                UpdateRoomSaveButtons();
            }, () => !WorldTimeControlActive);

            NativeSettingsSection(GuiContent, Loc.T("WC.Events"));
            var passengerDesc = EventInfoText(PassengerEventDesc[Mathf.Clamp(PassengerEventIndex, 0, PassengerEventDesc.Length - 1)]);
            var passengerControl = NewNativeSettingRow(GuiContent, "WcPassengerEvent", Loc.T("WC.PassengerEvents"), passengerDesc, 64f);
            var passengerSelector = UIF.Selector(passengerControl, "WcPassengerEventSelector",
                new Il2CppStringArray(PassengerEventNames()), PassengerEventIndex,
                NativeIntAction(index =>
                {
                    PassengerEventIndex = Mathf.Clamp(index, 0, PassengerEventCommands.Length - 1);
                    PassengerEventRejected = false;
                    RefreshEventButtons();
                }), 200f);
            FitSelectorWidth(passengerSelector, PassengerEventNames(), 170f, 340f);
            PassengerEventButton = NativeButton(passengerSelector.transform.parent, "WcPassengerEventTrigger", Loc.T("WC.Trigger"), ButtonStyle.Danger, 160f, 40f, TriggerPassengerEvent);
            if (PassengerEventButton != null) PassengerEventButton.hoverScale = 1f;
            PassengerEventInfo = FindRowDescription(passengerControl.parent, passengerControl, passengerDesc);

            var specialDesc = EventInfoText(SpecialEventDesc[Mathf.Clamp(SpecialEventIndex, 0, SpecialEventDesc.Length - 1)]);
            var specialControl = NewNativeSettingRow(GuiContent, "WcSpecialEvent", Loc.T("WC.SpecialEvents"), specialDesc, 64f);
            var specialSelector = UIF.Selector(specialControl, "WcSpecialEventSelector",
                new Il2CppStringArray(SpecialEventNames()), SpecialEventIndex,
                NativeIntAction(index =>
                {
                    SpecialEventIndex = Mathf.Clamp(index, 0, SpecialEventCommands.Length - 1);
                    SpecialEventRejected = false;
                    RefreshEventButtons();
                }), 200f);
            FitSelectorWidth(specialSelector, SpecialEventNames(), 170f, 340f);
            SpecialEventButton = NativeButton(specialSelector.transform.parent, "WcSpecialEventTrigger", Loc.T("WC.Trigger"), ButtonStyle.Danger, 160f, 40f, TriggerSpecialEvent);
            if (SpecialEventButton != null) SpecialEventButton.hoverScale = 1f;
            SpecialEventInfo = FindRowDescription(specialControl.parent, specialControl, specialDesc);



            NativeSettingsSection(GuiContent, Loc.T("WC.PassengerAI"));
            WorldToiletSelector = UIF.Selector(WorldControlRow("WcToilet", Loc.T("WC.ToiletRate"), null),
                "WcToiletSelector", new Il2CppStringArray(new[] { Loc.T("WC.Forbid"), "25%", "50%", "100%", "200%" }), WorldToiletIndex,
                NativeIntAction(index =>
                {
                    WorldToiletIndex = Mathf.Clamp(index, 0, 4);
                    SetWorldStatus(PendingHint());
                    UpdateRoomSaveButtons();
                }), 170f);
            FitSelectorWidth(WorldToiletSelector, new[] { Loc.T("WC.Forbid"), "25%", "50%", "100%", "200%" }, 130f, 240f);
            AddWorldDefault(WorldToiletSelector.transform.parent, "WcToiletDefault", () =>
            {
                WorldToiletIndex = 3;
                WorldToiletSelector.SetIndex(3);
                SetWorldStatus(PendingHint());
                UpdateRoomSaveButtons();
            }, () => WorldToiletIndex == 3);

            WorldTemperSelector = UIF.Selector(WorldControlRow("WcTemper", Loc.T("WC.Temper"), null),
                "WcTemperSelector", new Il2CppStringArray(new[] { Loc.T("WC.CalmTemper"), Loc.T("WC.Normal"), Loc.T("WC.Angry") }), WorldTemperIndex,
                NativeIntAction(index =>
                {
                    WorldTemperIndex = Mathf.Clamp(index, 0, 2);
                    SetWorldStatus(PendingHint());
                    UpdateRoomSaveButtons();
                }), 170f);
            FitSelectorWidth(WorldTemperSelector, new[] { Loc.T("WC.CalmTemper"), Loc.T("WC.Normal"), Loc.T("WC.Angry") }, 130f, 240f);
            AddWorldDefault(WorldTemperSelector.transform.parent, "WcTemperDefault", () =>
            {
                WorldTemperIndex = 1;
                WorldTemperSelector.SetIndex(1);
                SetWorldStatus(PendingHint());
                UpdateRoomSaveButtons();
            }, () => WorldTemperIndex == 1);

            WorldSleepSelector = UIF.Selector(WorldControlRow("WcSleep", Loc.T("WC.Sleep"), null),
                "WcSleepSelector", new Il2CppStringArray(new[] { Loc.T("WC.Less"), Loc.T("WC.Normal"), Loc.T("WC.More") }), WorldSleepIndex,
                NativeIntAction(index =>
                {
                    WorldSleepIndex = Mathf.Clamp(index, 0, 2);
                    SetWorldStatus(PendingHint());
                    UpdateRoomSaveButtons();
                }), 170f);
            FitSelectorWidth(WorldSleepSelector, new[] { Loc.T("WC.Less"), Loc.T("WC.Normal"), Loc.T("WC.More") }, 130f, 240f);
            AddWorldDefault(WorldSleepSelector.transform.parent, "WcSleepDefault", () =>
            {
                WorldSleepIndex = 1;
                WorldSleepSelector.SetIndex(1);
                SetWorldStatus(PendingHint());
                UpdateRoomSaveButtons();
            }, () => WorldSleepIndex == 1);

            NativeSettingsSection(GuiContent, Loc.T("WC.Maintenance"));
            var dispatchControl = NewNativeSettingRow(GuiContent, "WcDispatch", Loc.T("WC.AutoDispatch"),
                Loc.T("WC.AutoDispatchHelp"), 64f);
            WorldDispatchToggle = UIF.Toggle(dispatchControl, "WcDispatchToggle", !PendingAutoDispatch,
                NativeBoolAction(value =>
                {
                    PendingAutoDispatch = !value;
                    SetWorldStatus(PendingHint());
                    UpdateRoomSaveButtons();
                }));
            WorldDispatchToggle.SetValue(!PendingAutoDispatch, true);
            WorldDispatchToggle.Visual();
            WorldDispatchVisualRefreshFrame = Time.frameCount + 2;
            AddWorldDefault(dispatchControl, "WcDispatchDefault", () =>
            {
                PendingAutoDispatch = false;
                WorldDispatchToggle.SetValue(true, true);
                SetWorldStatus(PendingHint());
                UpdateRoomSaveButtons();
            }, () => !PendingAutoDispatch);

            var repairControl = NewNativeSettingRow(GuiContent, "WcRepair", Loc.T("WC.RepairAll"),
                Loc.T("WC.RepairHelp"), 64f);
            var repairButton = AddWorldButton(repairControl, "WcRepairButton", Loc.T("WC.Repair"), ButtonStyle.Danger, () => SetWorldStatus(RepairAll()));
            SetWorldButtonColors(repairButton, UITheme.Green, Color.white);

            var forceApControl = NewNativeSettingRow(GuiContent, "WcForceAutopilot", Loc.T("WC.ForceAutopilot"),
                Loc.T("WC.ForceAutopilotHelp"), 64f);
            var forceApToggle = UIF.Toggle(forceApControl, "WcForceAutopilotToggle", ForceAutopilot != null && ForceAutopilot.Value,
                NativeBoolAction(value =>
                {
                    if (ForceAutopilot != null)
                    {
                        ForceAutopilot.Value = value;
                        Settings.Save();
                    }
                }));
            forceApToggle.SetValue(ForceAutopilot != null && ForceAutopilot.Value, true);
            forceApToggle.Visual();
            ForceAutopilotToggle = forceApToggle;
            ForceAutopilotRefreshFrame = Time.frameCount + 1;

            RefreshWorldDefaultButtons();
        }

        private static string PendingHint()
        {
            return L("已修改：点右下角“保存并应用”生效", "Changed: use Save and apply at the bottom right");
        }

        private static string EventInfoText(string descKey)
        {
            return Loc.T(descKey) + "  ·  " + Loc.T("WC.EventIdle");
        }

        private static TMP_Text FindRowDescription(Transform row, Transform control, string initial)
        {
            if (row == null || string.IsNullOrEmpty(initial)) return null;
            foreach (var text in row.GetComponentsInChildren<TMP_Text>(true))
            {
                if (text == null || text.transform.IsChildOf(control)) continue;
                if (text.text == initial) return text;
            }
            return null;
        }

        private static bool IsEventRejected(string result)
        {
            if (string.IsNullOrEmpty(result)) return false;
            return result.Contains("不行") || result.Contains("busy") || result.Contains("already");
        }

        private static void TriggerPassengerEvent()
        {
            var command = PassengerEventCommands[Mathf.Clamp(PassengerEventIndex, 0, PassengerEventCommands.Length - 1)];
            var result = TriggerPaxEvent(command);
            PassengerEventRejected = IsEventRejected(result);
            SetWorldStatus(result);
            RefreshEventButtons();
        }

        private static void TriggerSpecialEvent()
        {
            var command = SpecialEventCommands[Mathf.Clamp(SpecialEventIndex, 0, SpecialEventCommands.Length - 1)];
            string result;
            if (command == "boss")
            {
                result = TriggerBoss();
                SpecialEventRejected = !ChaosEvents.IsBusy;
            }
            else
            {
                result = TriggerPaxEvent(command);
                SpecialEventRejected = IsEventRejected(result);
            }
            SetWorldStatus(result);
            RefreshEventButtons();
        }

        private static bool IsCommandActive(string command, string activeId)
        {
            if (string.IsNullOrEmpty(activeId)) return false;
            switch (command)
            {
                case "hijack":
                case "hijack bomb":
                    return activeId.EndsWith("hijack");
                case "intruder":
                    return activeId.EndsWith("intruder");
                case "brat":
                    return activeId.EndsWith("brat");
                case "rush main":
                    return activeId.EndsWith("rush");
                case "boss":
                    return activeId.Contains("boss") || activeId.Contains("caped");
                default:
                    return false;
            }
        }

        private static string DescribeChaosId(string id)
        {
            if (string.IsNullOrEmpty(id)) return string.Empty;
            if (id.EndsWith("hijack")) return Loc.T("WC.Hijack");
            if (id.EndsWith("rush")) return Loc.T("WC.Rush");
            if (id.EndsWith("intruder")) return Loc.T("WC.Intruder");
            if (id.EndsWith("brat")) return Loc.T("WC.Brat");
            if (id.Contains("boss")) return Loc.T("WC.CapedBoss");
            return id;
        }

        private static void SetEventButton(UIButton button, bool yellow)
        {
            if (button == null) return;
            if (yellow)
            {
                SetWorldButtonColors(button, UITheme.Yellow, UITheme.Ink);
                SetButtonLabel(button, Loc.T("WC.InProgress"));
            }
            else
            {
                SetWorldButtonColors(button, UITheme.Red, Color.white);
                SetButtonLabel(button, Loc.T("WC.Trigger"));
            }
        }

        private static void SetButtonLabel(UIButton button, string value)
        {
            if (button == null) return;
            var label = button.GetComponentInChildren<TMP_Text>();
            if (label != null && !string.Equals(label.text, value, StringComparison.Ordinal)) label.text = value;
        }

        private static void SetInfoText(TMP_Text text, string value)
        {
            if (text != null && !string.Equals(text.text, value, StringComparison.Ordinal)) text.text = value;
        }

        private static void RefreshEventButtons()
        {
            if (PassengerEventButton == null) return;
            var busy = false;
            var activeId = string.Empty;
            try
            {
                busy = ChaosEvents.IsBusy;
                activeId = ChaosEvents.Active ?? string.Empty;
            }
            catch { }
            var activeName = DescribeChaosId(activeId);

            var passengerCommand = PassengerEventCommands[Mathf.Clamp(PassengerEventIndex, 0, PassengerEventCommands.Length - 1)];
            var passengerActive = IsCommandActive(passengerCommand, activeId);
            if (passengerActive != PassengerButtonYellow)
            {
                PassengerButtonYellow = passengerActive;
                SetEventButton(PassengerEventButton, passengerActive);
            }
            if (PassengerEventInfo != null)
            {
                var desc = Loc.T(PassengerEventDesc[Mathf.Clamp(PassengerEventIndex, 0, PassengerEventDesc.Length - 1)]);
                string status;
                if (PassengerEventRejected) status = Loc.T("WC.EventBusyHint");
                else if (passengerActive) status = Loc.T("WC.InProgress");
                else if (busy) status = Loc.T("WC.InProgress") + (activeName.Length > 0 ? "  ·  " + activeName : string.Empty);
                else status = Loc.T("WC.EventIdle");
                SetInfoText(PassengerEventInfo, desc + "  ·  " + status);
            }

            var specialCommand = SpecialEventCommands[Mathf.Clamp(SpecialEventIndex, 0, SpecialEventCommands.Length - 1)];
            var specialActive = IsCommandActive(specialCommand, activeId);
            if (specialActive != SpecialButtonYellow)
            {
                SpecialButtonYellow = specialActive;
                SetEventButton(SpecialEventButton, specialActive);
            }
            if (SpecialEventInfo != null)
            {
                var desc = Loc.T(SpecialEventDesc[Mathf.Clamp(SpecialEventIndex, 0, SpecialEventDesc.Length - 1)]);
                string status;
                if (SpecialEventRejected) status = Loc.T("WC.EventBusyHint") + (activeName.Length > 0 ? "  ·  " + activeName : string.Empty);
                else if (specialActive) status = Loc.T("WC.InProgress") + (activeName.Length > 0 ? "  ·  " + activeName : string.Empty);
                else status = Loc.T("WC.EventIdle");
                SetInfoText(SpecialEventInfo, desc + "  ·  " + status);
            }
        }

        private static void SetWorldButtonColors(UIButton button, Color background, Color foreground)
        {
            if (button == null) return;
            button.bgNormal = background;
            button.bgHover = Color.Lerp(background, Color.white, 0.12f);
            button.bgPress = Color.Lerp(background, Color.black, 0.12f);
            button.bgSelected = background;
            button.bgDisabled = background;
            button.fgNormal = foreground;
            button.fgHover = foreground;
            button.fgSelected = foreground;
            button.Snap();
        }

        private static Transform WorldControlRow(string name, string label, string description)
        {
            return NewNativeSettingRow(GuiContent, name, label, description, 60f);
        }

        private static UIButton AddWorldButton(Transform control, string name, string label, ButtonStyle style, Action action)
        {
            var button = NativeButton(control, name, label, style, 108f, 40f, action);
            button.hoverScale = 1f;
            return button;
        }

        private static void AddWorldDefault(Transform control, string name, Action action, Func<bool> isDefault)
        {
            var button = NativeButton(control, name, Loc.T("WC.Default"), ButtonStyle.Ghost, 62f, 38f, action);
            SetDefaultButtonColor(button, UITheme.InkSoft);
            button.hoverScale = 1f;
            WorldDefaultButtons.Add((button, isDefault));
        }

        private static void SetDefaultButtonColor(UIButton button, Color foreground)
        {
            if (button == null) return;
            var transparent = new Color(0f, 0f, 0f, 0f);
            button.bgNormal = transparent;
            button.bgHover = Color.Lerp(transparent, UITheme.Ink, 0.07f);
            button.bgPress = Color.Lerp(transparent, UITheme.Ink, 0.10f);
            button.bgSelected = transparent;
            button.bgDisabled = transparent;
            button.fgNormal = foreground;
            button.fgHover = foreground;
            button.fgSelected = foreground;
            button.hoverScale = 1f;
            button.Snap();
        }

        private static bool IsTimeAtDefault()
        {
            try
            {
                var seed = 0;
                var net = WorldNet.Live;
                if (net != null) seed = net.NetworkSync.seed;
                var defaultHour = Mathf.Repeat((float)WorldEnvironment.RunStartHour(seed), 24f);
                if (!TryParseTimeText(WorldTimeInput, out var hour)) return false;
                return Mathf.Abs(Mathf.Repeat(hour - defaultHour + 12f, 24f) - 12f) <= 0.02f;
            }
            catch { return false; }
        }

        private static void RefreshWorldDefaultButtons()
        {
            if (WorldDefaultButtons.Count == 0) return;
            var transparent = new Color(0f, 0f, 0f, 0f);
            var dim = new Color(UITheme.InkSoft.r, UITheme.InkSoft.g, UITheme.InkSoft.b, 0.35f);
            for (var i = 0; i < WorldDefaultButtons.Count; i++)
            {
                var entry = WorldDefaultButtons[i];
                if (entry.Button == null) continue;
                var atDefault = false;
                try { atDefault = entry.IsDefault(); } catch { }
                SetDefaultButtonColor(entry.Button, atDefault ? dim : UITheme.InkSoft);
                entry.Button.hoverScale = 1f;
            }
        }

        private static void RefreshWorldDispatchToggleVisual()
        {
            if (WorldDispatchToggle == null || WorldDispatchVisualRefreshFrame < 0 || Time.frameCount < WorldDispatchVisualRefreshFrame) return;
            if (GuiTab != 2 || GuiContent == null || !WorldDispatchToggle.gameObject.activeInHierarchy) return;
            WorldDispatchVisualRefreshFrame = -1;
            try
            {
                WorldDispatchToggle.SetValue(!PendingAutoDispatch, true);
                WorldDispatchToggle.Visual();
                Canvas.ForceUpdateCanvases();
            }
            catch (Exception e)
            {
                Log?.LogWarning("[MaydayHostControl] Failed to refresh dispatch toggle: " + e.Message);
            }
        }

        private static readonly float[] WorldIntensityValues = { 0.05f, 0.3f, 0.6f, 1f };
        private static readonly float[] WorldSpeedValues = { 1f, 4f, 16f, 0f };
        private static readonly int[] WorldToiletValues = { 0, 25, 50, 100, 200 };
        private static readonly int[] WorldSleepValues = { 50, 100, 200 };

        private static string[] WorldWeatherNames()
        {
            return new[] { Loc.T("WC.WeatherCalm"), Loc.T("WC.Cloudy"), Loc.T("WC.Rain"), Loc.T("WC.Storm"), Loc.T("WC.Fog"), Loc.T("WC.Snow") };
        }

        private static string[] WorldSpeedNames()
        {
            return new[] { "1x", "4x", "16x", Loc.T("WC.Pause") };
        }

        private static string[] PassengerEventNames()
        {
            return new[] { Loc.T("WC.Panic"), Loc.T("WC.Unruly"), Loc.T("WC.ToiletEvent"), Loc.T("WC.Coffee"),
                Loc.T("WC.Rush"), Loc.T("WC.Meltdown"), Loc.T("WC.Airsick"), Loc.T("WC.VIP"),
                Loc.T("WC.SleepAll"), Loc.T("WC.WakeAll"), Loc.T("WC.StopEvents") };
        }

        private static readonly string[] PassengerEventCommands =
        {
            "panic", "unruly", "toilet", "coffee all", "rush main", "meltdown", "airsick", "vip", "sleep all", "wake all", "stop"
        };

        private static readonly string[] PassengerEventDesc =
        {
            "WC.Desc.Panic", "WC.Desc.Unruly", "WC.Desc.Toilet", "WC.Desc.Coffee", "WC.Desc.Rush",
            "WC.Desc.Meltdown", "WC.Desc.Airsick", "WC.Desc.VIP", "WC.Desc.SleepAll", "WC.Desc.WakeAll", "WC.Desc.StopEvents"
        };

        private static string[] SpecialEventNames()
        {
            return new[] { Loc.T("WC.Hijack"), Loc.T("WC.Bomber"), Loc.T("WC.Intruder"), Loc.T("WC.Brat"), Loc.T("WC.CapedBoss") };
        }

        private static readonly string[] SpecialEventCommands = { "hijack", "hijack bomb", "intruder", "brat", "boss" };

        private static readonly string[] SpecialEventDesc = { "WC.Desc.Hijack", "WC.Desc.Bomber", "WC.Desc.Intruder", "WC.Desc.Brat", "WC.Desc.Boss" };

        private static void SyncWorldStateFromGame()
        {
            if (WorldInitialized) return;
            WorldInitialized = true;
            WorldTimeBaselineHour = CurrentHour();
            try
            {
                var net = WorldNet.Live;
                if (net != null)
                {
                    var sync = net.NetworkSync;
                    WorldWeatherIndex = Mathf.Clamp(sync.weather, 0, 5);
                    var severity = sync.severity;
                    WorldIntensityIndex = severity <= 0.2f ? 0 : severity <= 0.45f ? 1 : severity <= 0.8f ? 2 : 3;
                }
            }
            catch { }
            AppliedWeatherIndex = WorldWeatherIndex;
            AppliedIntensityIndex = WorldIntensityIndex;
            AppliedSpeedIndex = WorldSpeedIndex = 0;
            AppliedToiletIndex = WorldToiletIndex = 3;
            AppliedTemperIndex = WorldTemperIndex = 1;
            AppliedSleepIndex = WorldSleepIndex = 1;
            PendingAutoDispatch = AppliedAutoDispatch = AutoDispatchBlocked;
        }

        private static bool TryParseTimeText(string text, out float hour)
        {
            hour = 0f;
            if (string.IsNullOrWhiteSpace(text)) return false;
            text = text.Trim();
            if (text.Contains(":"))
            {
                var parts = text.Split(':');
                if (parts.Length != 2 || !int.TryParse(parts[0], out var hh) || !int.TryParse(parts[1], out var mm)) return false;
                hour = hh + mm / 60f;
                return true;
            }
            return float.TryParse(text, out hour);
        }

        private static bool HasPendingTime()
        {
            if (!WorldTimeDirty) return false;
            if (WorldTimeInputField != null && TryParseTimeText(WorldTimeInputField.text, out var typed))
                return Mathf.Abs(Mathf.Repeat(typed - WorldTimeBaselineHour + 12f, 24f) - 12f) > 0.02f;
            return false;
        }

        private static bool HasPendingWorldChanges()
        {
            if (!WorldInitialized) return false;
            return HasPendingTime() ||
                   WorldWeatherIndex != AppliedWeatherIndex ||
                   WorldIntensityIndex != AppliedIntensityIndex ||
                   WorldSpeedIndex != AppliedSpeedIndex ||
                   WorldToiletIndex != AppliedToiletIndex ||
                   WorldTemperIndex != AppliedTemperIndex ||
                   WorldSleepIndex != AppliedSleepIndex ||
                   PendingAutoDispatch != AppliedAutoDispatch;
        }

        private static string ApplyWorldSettings()
        {
            var parts = new List<string>();
            try
            {
                if (HasPendingTime())
                {
                    var text = WorldTimeInputField != null ? WorldTimeInputField.text : WorldTimeInput;
                    var hour = TryParseTimeText(text, out var typed) ? typed : WorldTimeBaselineHour;
                    parts.Add(ApplyGameHour(hour));
                    WorldTimeBaselineHour = Mathf.Repeat(hour, 24f);
                    WorldTimeDirty = false;
                    WorldTimeInput = FormatHour(WorldTimeBaselineHour);
                    if (WorldTimeInputField != null) WorldTimeInputField.SetTextWithoutNotify(WorldTimeInput);
                }
                if (WorldWeatherIndex != AppliedWeatherIndex || WorldIntensityIndex != AppliedIntensityIndex)
                {
                    LastWeatherSeverity = WorldIntensityValues[Mathf.Clamp(WorldIntensityIndex, 0, 3)];
                    parts.Add(ApplyWeather((WeatherKind)WorldWeatherIndex));
                    AppliedWeatherIndex = WorldWeatherIndex;
                    AppliedIntensityIndex = WorldIntensityIndex;
                }
                if (WorldSpeedIndex != AppliedSpeedIndex)
                {
                    parts.Add(SetTimeSpeed(WorldSpeedValues[WorldSpeedIndex]));
                    AppliedSpeedIndex = WorldSpeedIndex;
                }
                if (WorldToiletIndex != AppliedToiletIndex)
                {
                    parts.Add(SetToiletRate(WorldToiletValues[WorldToiletIndex]));
                    AppliedToiletIndex = WorldToiletIndex;
                }
                if (WorldTemperIndex != AppliedTemperIndex)
                {
                    parts.Add(SetTemper(WorldTemperIndex));
                    AppliedTemperIndex = WorldTemperIndex;
                }
                if (WorldSleepIndex != AppliedSleepIndex)
                {
                    parts.Add(SetSleepRate(WorldSleepValues[WorldSleepIndex]));
                    AppliedSleepIndex = WorldSleepIndex;
                }
                if (PendingAutoDispatch != AppliedAutoDispatch)
                {
                    parts.Add(SetAutoDispatch(PendingAutoDispatch));
                    AppliedAutoDispatch = PendingAutoDispatch;
                }
            }
            catch (Exception e) { parts.Add(e.Message); }
            return string.Join(" | ", parts);
        }

        private static void TickWorldTimeDisplay()
        {
            try
            {
                if (!GuiVisible || WorldTimeInputField == null) return;
                if (WorldTimeInputField.isFocused) return;
                if (WorldTimeDirty)
                {
                    if (HasPendingTime()) return;
                    WorldTimeDirty = false;
                }
                if (HasPendingTime()) return;
                var now = CurrentHour();
                var text = FormatHour(now);
                if (!string.Equals(WorldTimeInputField.text, text, StringComparison.Ordinal))
                {
                    WorldTimeInput = text;
                    WorldTimeInputField.SetTextWithoutNotify(text);
                }
                WorldTimeBaselineHour = now;
            }
            catch { }
        }

        private static void ApplyDefaultTime()
        {
            try
            {
                var seed = 0;
                var net = WorldNet.Live;
                if (net != null) seed = net.NetworkSync.seed;
                var hour = Mathf.Repeat((float)WorldEnvironment.RunStartHour(seed), 24f);
                WorldTimeInput = FormatHour(hour);
                if (WorldTimeInputField != null) WorldTimeInputField.SetTextWithoutNotify(WorldTimeInput);
                WorldTimeDirty = true;
                SetWorldStatus(PendingHint());
                UpdateRoomSaveButtons();
            }
            catch (Exception e) { SetWorldStatus(L("恢复默认时间失败: ", "Default time failed: ") + e.Message); }
        }

        // -------------------------------------------------------------- time


        private static bool TryParseStatusHour(string status, out float hour)
        {
            hour = 0f;
            if (string.IsNullOrEmpty(status)) return false;
            var match = System.Text.RegularExpressions.Regex.Match(status, @"h=([0-9]+(?:\.[0-9]+)?)");
            if (!match.Success) return false;
            return float.TryParse(match.Groups[1].Value, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out hour);
        }

        private static bool StatusHourMatches(string status, float target)
        {
            if (!TryParseStatusHour(status, out var current)) return false;
            var diff = Mathf.Abs(Mathf.Repeat(current - target + 12f, 24f) - 12f);
            return diff < 0.08f;
        }

        private static string ApplyGameHour(float hour)
        {
            try
            {
                var target = Mathf.Repeat(hour, 24f);
                var before = WorldEnvironment.DevTime(string.Empty) ?? string.Empty;
                WorldEnvironment.devHold = target;
                WorldEnvironment.devSpeed = 1f;
                WorldTimeControlActive = true;
                var after = WorldEnvironment.DevTime(string.Empty) ?? string.Empty;
                if (!StatusHourMatches(after, target))
                {
                    var invariant = System.Globalization.CultureInfo.InvariantCulture;
                    foreach (var arg in new[] { FormatHour(target), target.ToString("0.##", invariant), "hold " + target.ToString("0.##", invariant) })
                    {
                        var result = WorldEnvironment.DevTime(arg) ?? string.Empty;
                        LogEvent("world-time-dev", "arg=" + arg + " result=" + result);
                        if (StatusHourMatches(result, target)) { after = result; break; }
                    }
                }
                var ok = StatusHourMatches(after, target);
                LogEvent("world-time-set", "target=" + target.ToString("0.00") + " ok=" + ok + " before=" + before + " after=" + after);
                return (ok ? L("时间设为 ", "Time set to ") : L("时间已请求（未确认） ", "Time requested (unconfirmed) ")) + FormatHour(target);
            }
            catch (Exception e) { return L("设置时间失败: ", "Set time failed: ") + e.Message; }
        }

        private static string ShiftGameHour(float delta)
        {
            return ApplyGameHour(CurrentHour() + delta);
        }

        private static string SetTimeSpeed(float multiplier)
        {
            try
            {
                WorldTimeSpeed = Mathf.Max(0f, multiplier);
                WorldTimeControlActive = true;
                if (WorldTimeSpeed > 0f)
                {
                    var hold = (float)WorldEnvironment.devHold;
                    if (hold < 0f || hold > 24f) WorldEnvironment.devHold = CurrentHour();
                }
                LogEvent("world-time-speed", "speed=" + WorldTimeSpeed + " hold=" + WorldEnvironment.devHold);
                return WorldTimeSpeed <= 0f ? L("时间已暂停", "Time paused") : L("时间流速 ", "Time speed ") + WorldTimeSpeed.ToString("0.#") + "x";
            }
            catch (Exception e) { return L("设置流速失败: ", "Set speed failed: ") + e.Message; }
        }

        // ----------------------------------------------------------- weather

        private static float LastWeatherSeverity = 0.05f;

        private static string ApplyWeather(WeatherKind kind)
        {
            try
            {
                var net = WorldNet.Live;
                if (net == null) return L("世界尚未初始化", "World not ready");
                var sync = net.NetworkSync;
                var effective = kind == WeatherKind.Calm ? LastWeatherSeverity : Mathf.Max(LastWeatherSeverity, 0.35f);
                sync.weather = (byte)kind;
                sync.severity = effective;
                var state = Il2CppMayday.GameState.Instance;
                if (state != null) sync.weatherLeg = state.Leg;
                sync.version++;
                net.ServerSetSync(sync);
                LastWeatherKind = kind;
                var front = kind == WeatherKind.Rain ? FrontKind.Rain
                    : kind == WeatherKind.Storm ? FrontKind.Storm
                    : kind == WeatherKind.Fog ? FrontKind.Fog
                    : FrontKind.None;
                var frontResult = ApplyFront(front, effective);
                LogEvent("world-weather", "kind=" + kind + " severity=" + effective + " front=" + frontResult);
                return L("天气已设为 ", "Weather set to ") + kind + " (" + effective.ToString("0.0") + ") | " + frontResult;
            }
            catch (Exception e) { return L("设置天气失败: ", "Set weather failed: ") + e.Message; }
        }

        private static string ApplyFront(FrontKind kind, float severity)
        {
            try
            {
                var type = typeof(WeatherSystem);
                var instance = type.GetProperty("Instance", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(null);
                if (instance == null) return "front: no instance";
                if (kind == FrontKind.None)
                {
                    var end = type.GetMethod("EndFrontSoon", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (end == null) return "front: no EndFrontSoon";
                    end.Invoke(instance, new object[] { WorldAuthority.Time, 2f });
                    return "front: clear";
                }
                var start = type.GetMethod("StartFront", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (start == null) return "front: no StartFront";
                start.Invoke(instance, new object[] { kind, Mathf.Clamp01(severity), 1800f, WorldAuthority.Time });
                return "front: " + kind;
            }
            catch (Exception e) { return "front: " + e.GetType().Name + " " + e.Message; }
        }

        private static void TickWorldTimeFlow()
        {
            if (!WorldTimeControlActive) return;
            if (!NetworkServer.activeHost) return;
            try
            {
                if (WorldTimeSpeed <= 0f) return;
                var hold = (float)WorldEnvironment.devHold;
                if (hold < 0f || hold > 24f) hold = CurrentHour();
                hold = Mathf.Repeat(hold + BaseClockRate * WorldTimeSpeed * Time.deltaTime, 24f);
                WorldEnvironment.devHold = hold;
            }
            catch { }
        }

        private static void ResetTimeControl()
        {
            try
            {
                WorldTimeControlActive = false;
                WorldTimeSpeed = 1f;
                WorldEnvironment.devHold = -1;
                WorldEnvironment.devSpeed = 1f;
                SetWorldStatus(L("时间已恢复游戏默认", "Time back to game default"));
            }
            catch (Exception e) { SetWorldStatus(L("恢复失败: ", "Reset failed: ") + e.Message); }
        }

        private static string ApplyWeatherSeverity(float severity)
        {
            LastWeatherSeverity = Mathf.Clamp(severity, 0f, 1f);
            return ApplyWeather(LastWeatherKind);
        }

        private static WeatherKind LastWeatherKind = WeatherKind.Calm;

        // ------------------------------------------------------------- events

        private static string TriggerPaxEvent(string command)
        {
            try
            {
                var result = PassengerDirector.DevEvent(command);
                return string.IsNullOrEmpty(result) ? L("事件已触发: ", "Event triggered: ") + command
                    : L("事件: ", "Event: ") + result;
            }
            catch (Exception e) { return L("触发事件失败: ", "Trigger event failed: ") + e.Message; }
        }

        private static string TriggerBoss()
        {
            try
            {
                var status = CapedBoss.DevBoss(string.Empty) ?? string.Empty;
                if (status.IndexOf("not in flight", StringComparison.OrdinalIgnoreCase) >= 0)
                    return L("祖国人只能在飞行中触发", "Caped boss can only start in flight");
                foreach (var arg in new[] { "start", "begin", "go", "fight", "on", "1" })
                {
                    var result = CapedBoss.DevBoss(arg) ?? string.Empty;
                    LogEvent("world-boss-dev", "arg=" + arg + " ok=" + (!result.Contains("phase=Off")));
                    if (!result.Contains("phase=Off"))
                        return L("祖国人已触发", "Caped boss started");
                }
                return L("未能触发祖国人（参数待确认）", "Caped boss did not start (args unknown)");
            }
            catch (Exception e) { return L("触发祖国人失败: ", "Trigger caped boss failed: ") + e.Message; }
        }

        // ---------------------------------------------------------------- AI

        private static void CaptureAiBases()
        {
            if (AiBasesCaptured) return;
            AiBasesCaptured = true;
            BasePeeEveryMin = BeltTuning.PeeEveryMin;
            BasePeeEveryMax = BeltTuning.PeeEveryMax;
            BaseFirstPeeMin = BeltTuning.FirstPeeMin;
            BaseFirstPeeMax = BeltTuning.FirstPeeMax;
            BasePeeBladder = BeltTuning.PeeBladder;
            BasePeeMood = BeltTuning.PeeMood;
            BasePeeMax = BeltTuning.PeeMax;
            BaseRageAt = BeltTuning.RageAt;
            BaseAngerRate = BeltTuning.AngerRate;
            BaseShoutAt = BeltTuning.ShoutAt;
            BaseSleepMin = SleepTuning.SleepMin;
            BaseSleepMax = SleepTuning.SleepMax;
            BasePickMin = SleepTuning.PickMin;
            BasePickMax = SleepTuning.PickMax;
        }

        private static string SetToiletRate(int percent)
        {
            try
            {
                CaptureAiBases();
                if (percent <= 0)
                {
                    BeltTuning.PeeMax = 0;
                    BeltTuning.FirstPeeMin = 99999f;
                    BeltTuning.FirstPeeMax = 99999f;
                    BeltTuning.PeeEveryMin = 99999f;
                    BeltTuning.PeeEveryMax = 99999f;
                    return L("已禁止乘客上厕所", "Passenger toilet trips disabled");
                }
                var scale = 100f / Mathf.Clamp(percent, 1, 1000);
                BeltTuning.PeeMax = BasePeeMax;
                BeltTuning.FirstPeeMin = BaseFirstPeeMin * scale;
                BeltTuning.FirstPeeMax = BaseFirstPeeMax * scale;
                BeltTuning.PeeEveryMin = BasePeeEveryMin * scale;
                BeltTuning.PeeEveryMax = BasePeeEveryMax * scale;
                return L("如厕频率 ", "Toilet rate ") + percent + "%";
            }
            catch (Exception e) { return L("调整如厕频率失败: ", "Set toilet rate failed: ") + e.Message; }
        }

        private static string SetTemper(int level)
        {
            try
            {
                CaptureAiBases();
                switch (level)
                {
                    case 0:
                        BeltTuning.RageAt = BaseRageAt * 1.6f;
                        BeltTuning.AngerRate = BaseAngerRate * 0.6f;
                        BeltTuning.ShoutAt = BaseShoutAt * 1.5f;
                        return L("脾气：安静", "Temper: calm");
                    case 2:
                        BeltTuning.RageAt = BaseRageAt * 0.65f;
                        BeltTuning.AngerRate = BaseAngerRate * 1.5f;
                        BeltTuning.ShoutAt = BaseShoutAt * 0.7f;
                        return L("脾气：火爆", "Temper: angry");
                    default:
                        BeltTuning.RageAt = BaseRageAt;
                        BeltTuning.AngerRate = BaseAngerRate;
                        BeltTuning.ShoutAt = BaseShoutAt;
                        return L("脾气：正常", "Temper: normal");
                }
            }
            catch (Exception e) { return L("调整脾气失败: ", "Set temper failed: ") + e.Message; }
        }

        private static string SetSleepRate(int percent)
        {
            try
            {
                CaptureAiBases();
                var scale = 100f / Mathf.Clamp(percent, 10, 1000);
                SleepTuning.SleepMin = BaseSleepMin * scale;
                SleepTuning.SleepMax = BaseSleepMax * scale;
                SleepTuning.PickMin = BasePickMin * scale;
                SleepTuning.PickMax = BasePickMax * scale;
                return L("睡眠频率 ", "Sleep rate ") + percent + "%";
            }
            catch (Exception e) { return L("调整睡眠失败: ", "Set sleep failed: ") + e.Message; }
        }

        private static string ResetAiTuning()
        {
            try
            {
                if (AiBasesCaptured)
                {
                    BeltTuning.PeeMax = BasePeeMax;
                    BeltTuning.FirstPeeMin = BaseFirstPeeMin;
                    BeltTuning.FirstPeeMax = BaseFirstPeeMax;
                    BeltTuning.PeeEveryMin = BasePeeEveryMin;
                    BeltTuning.PeeEveryMax = BasePeeEveryMax;
                    BeltTuning.PeeBladder = BasePeeBladder;
                    BeltTuning.PeeMood = BasePeeMood;
                    BeltTuning.RageAt = BaseRageAt;
                    BeltTuning.AngerRate = BaseAngerRate;
                    BeltTuning.ShoutAt = BaseShoutAt;
                    SleepTuning.SleepMin = BaseSleepMin;
                    SleepTuning.SleepMax = BaseSleepMax;
                    SleepTuning.PickMin = BasePickMin;
                    SleepTuning.PickMax = BasePickMax;
                }
                return L("AI 参数已恢复默认", "AI tuning reset");
            }
            catch (Exception e) { return L("恢复默认失败: ", "Reset failed: ") + e.Message; }
        }

        // ---------------------------------------------------------- dispatch

        private static string SetAutoDispatch(bool disabled)
        {
            AutoDispatchBlocked = disabled;
            try
            {
                if (AutoDispatchDisabledConfig != null)
                {
                    AutoDispatchDisabledConfig.Value = disabled;
                    Settings.Save();
                }
            }
            catch (Exception e) { Log?.LogWarning("[WorldControl] save auto dispatch setting failed: " + e.Message); }
            return disabled ? L("自动接单已关闭", "Auto dispatch disabled") : L("自动接单已开启", "Auto dispatch enabled");
        }

        // ------------------------------------------------------------ repair

        private static string RepairAll()
        {
            var done = new List<string>();
            var failed = new List<string>();

            try
            {
                var seal = HullSystem.DevHoles("seal all");
                var rebuild = HullSystem.DevHoles("rebuild");
                LogEvent("world-repair-hull", "seal=" + seal + " rebuild=" + rebuild);
                done.Add(L("机身", "hull"));
            }
            catch (Exception e) { failed.Add(L("机身", "hull") + " (" + e.Message + ")"); }

            try
            {
                var reset = 0;
                for (var i = 0; i < 2; i++)
                {
                    var engine = SysRefs.Engine(i);
                    if (engine == null) continue;
                    engine.ServerResetForRun();
                    reset++;
                }
                LogEvent("world-repair-engine", "reset=" + reset);
                if (reset > 0) done.Add(L("发动机", "engines"));
                else failed.Add(L("发动机", "engines"));
            }
            catch (Exception e) { failed.Add(L("发动机", "engines") + " (" + e.Message + ")"); }

            try
            {
                var outResult = FireNode.DevFire("out");
                var clearResult = FireNode.DevFire("clear");
                var extinguished = 0;
                try
                {
                    var fires = UnityEngine.Object.FindObjectsOfType<FireNode>();
                    var method = typeof(FireNode).GetMethod("Extinguish", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (fires != null && method != null)
                    {
                        for (var i = 0; i < fires.Length; i++)
                        {
                            try { method.Invoke(fires[i], new object[] { null }); extinguished++; } catch { }
                        }
                    }
                }
                catch { }
                LogEvent("world-repair-fire", "out=" + outResult + " clear=" + clearResult + " extinguished=" + extinguished);
                done.Add(L("火情", "fires"));
            }
            catch (Exception e) { failed.Add(L("火情", "fires") + " (" + e.Message + ")"); }

            try
            {
                var main = FuelSystem.DevFuel("main 9999");
                var reserve = FuelSystem.DevFuel("reserve 9999");
                LogEvent("world-repair-fuel", "main=" + main + " reserve=" + reserve);
                done.Add(L("燃油", "fuel"));
            }
            catch (Exception e) { failed.Add(L("燃油", "fuel") + " (" + e.Message + ")"); }

            LogEvent("world-repair-fuse", "skipped breakers untouched");

            var summary = L("已修复: ", "Repaired: ") + string.Join(", ", done);
            if (failed.Count > 0) summary += " | " + L("未完成: ", "Failed: ") + string.Join(", ", failed);
            summary += " | " + L("详见日志", "see log for details");
            return summary;
        }
    }

    [HarmonyPatch(typeof(Il2CppMayday.Airport.GameDirector), "AutoDispatch")]
    internal static class AutoDispatchPatch
    {
        [HarmonyPrefix]
        private static bool Prefix()
        {
            return !Plugin.AutoDispatchBlocked;
        }
    }
}
