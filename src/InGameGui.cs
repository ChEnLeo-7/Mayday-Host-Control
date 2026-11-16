using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using HarmonyLib;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppMayday.UI;
using Il2CppMirror;
using Il2CppTMPro;
using Il2CppSteamworks;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace MaydayHostControl
{
    public sealed partial class Plugin
    {
        private static bool GuiVisible;
        private static int GuiTab;
        private static string GuiStatus = string.Empty;
        private static string GuiConfirmRecoveryId;
        private static string GuiConfirmDeleteId;
        private static string GuiRoomNameInput = string.Empty;
        private static int GuiSavePage;
        private static bool GuiShortcutDown;
        private static bool GuiEscapeDown;
        private static bool GuiShortcutCapture;
        private static bool GuiShortcutCaptureEscapeDown;
        private static int PanelShortcutVirtualKey = 0x77;
        private static string PanelShortcutDisplay = "F8";
        private static int SuppressBackThroughFrame = -1;
        private static CursorLockMode PreviousCursorLock;
        private static bool PreviousCursorVisible;
        private static bool PreviousLookCaptured;
        private static RectTransform GuiRoot;
        private static CanvasGroup GuiCanvasGroup;
        private static bool GuiClosing;
        private static bool GuiLanguageRefreshPending;
        private static RectTransform GuiContent;
        private static RectTransform GuiPageContent;
        private static CanvasGroup GuiContentVisibility;
        private static RectTransform GuiScreenLayer;
        private static UIButton[] GuiNavButtons;
        private static UIButton PauseMenuButton;
        private static UIButton MainMenuHostButton;
        private static UIScreen SourceMainMenuScreen;
        private static bool MainMenuHidden;
        private static bool PanelOpenedForDefaults;

        private static bool CanConfigureNextRoom => UIRoot.Instance != null &&
            UIRoot.Instance.CurrentMode == UIRoot.Mode.Menu && !NetworkServer.active && !NetworkClient.active;
        private static bool CanOpenHostPanel => NetworkServer.activeHost || CanConfigureNextRoom;
        private static bool CurrentJoinAllowed => PanelOpenedForDefaults ? (DefaultRoomJoinable?.Value ?? true) : ManualJoinAllowed;
        private static Il2CppMayday.UI.PauseScreen SourcePauseScreen;
        private static UIScreen UnderlyingTopScreen;
        private static bool PauseScreenHidden;
        private static bool OpenedFromPauseMenu;
        private static bool RestoreGameplayCursorWhenUncovered;
        private static string PendingCapacityText;
        private static string RoomVisibilityPending;
        private static bool RoomJoinablePending;
        private static UIButton SaveRoomButton;
        private static UIButton SaveRoomApplyButton;
        private static TMP_InputField RoomNameInputField;
        private static TMP_InputField CapacityInputField;
        private static UIToggle RoomJoinableToggle;
        private static int RoomJoinableVisualRefreshFrame = -1;
        private static UIButton EliminateAllFooterButton;
        private static UIButton KickAllFooterButton;
        private static UIButton RespawnAllFooterButton;
        private static UIButton BlacklistFooterButton;
        private static UIButton CloseFooterButton;
        private static bool GuiMouseDown;
        private static float NextOverviewRefreshTime;
        private static TMP_Text OverviewOnlineValue;
        private static TMP_Text OverviewCapacityValue;
        private static TMP_Text OverviewCapacityDetail;
        private static TMP_Text OverviewJoinableValue;
        private static TMP_Text OverviewFlightPhaseValue;
        private static TMP_Text OverviewFlightLegValue;
        private static TMP_Text OverviewMoneyValue;

        private static readonly string[] GuiTabs = { "Overview", "Players", "Room", "Settings" };

        private static class NativeInput
        {
            [StructLayout(LayoutKind.Sequential)]
            internal struct Point
            {
                public int X;
                public int Y;
            }

            [StructLayout(LayoutKind.Sequential)]
            private struct Rect
            {
                public int Left;
                public int Top;
                public int Right;
                public int Bottom;
            }

            [DllImport("user32.dll")]
            internal static extern short GetAsyncKeyState(int virtualKey);

            [DllImport("user32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            private static extern bool GetCursorPos(out Point point);

            [DllImport("user32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            private static extern bool ScreenToClient(IntPtr window, ref Point point);

            [DllImport("user32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            private static extern bool GetClientRect(IntPtr window, out Rect rect);

            internal static bool TryGetCursorPosition(out Vector2 position)
            {
                position = default;
                var window = Process.GetCurrentProcess().MainWindowHandle;
                if (window == IntPtr.Zero || !GetCursorPos(out var point) ||
                    !ScreenToClient(window, ref point) || !GetClientRect(window, out var rect)) return false;

                var clientWidth = rect.Right - rect.Left;
                var clientHeight = rect.Bottom - rect.Top;
                var screenWidth = Screen.width;
                var screenHeight = Screen.height;
                if (clientWidth <= 0 || clientHeight <= 0 || screenWidth <= 0 || screenHeight <= 0) return false;

                position = new Vector2(
                    point.X * ((float)screenWidth / clientWidth),
                    (clientHeight - point.Y) * ((float)screenHeight / clientHeight));
                return true;
            }
        }

        internal static string PanelShortcutName => PanelShortcutDisplay;

        internal static void InitializePanelShortcut()
        {
            var configured = OpenPanelKey?.Value?.Trim() ?? "F8";
            if (int.TryParse(configured, out var virtualKey) && virtualKey >= 1 && virtualKey <= 255)
                PanelShortcutVirtualKey = virtualKey;
            else if (configured.Length >= 2 && (configured[0] == 'F' || configured[0] == 'f') &&
                     int.TryParse(configured.Substring(1), out var function) && function >= 1 && function <= 12)
                PanelShortcutVirtualKey = 0x70 + function - 1;
            else
                PanelShortcutVirtualKey = 0x77;
            PanelShortcutDisplay = GetVirtualKeyDisplayName(PanelShortcutVirtualKey);
        }

        private static int GetPanelVirtualKey()
        {
            var name = OpenPanelKey?.Value?.Trim() ?? "F8";
            if (int.TryParse(name, out var storedVirtualKey) && storedVirtualKey >= 1 && storedVirtualKey <= 255)
                return storedVirtualKey;
            if (name.Length >= 2 && (name[0] == 'F' || name[0] == 'f') && int.TryParse(name.Substring(1), out var function) && function >= 1 && function <= 12)
                return 0x70 + function - 1;
            return 0x77;
        }

        private static string GetVirtualKeyDisplayName(int virtualKey)
        {
            if (virtualKey == 0xC0) return "`";
            if (virtualKey >= 0x30 && virtualKey <= 0x39 || virtualKey >= 0x41 && virtualKey <= 0x5A)
                return ((char)virtualKey).ToString();
            if (virtualKey >= 0x70 && virtualKey <= 0x7B) return "F" + (virtualKey - 0x6F);
            switch (virtualKey)
            {
                case 0x08: return "Backspace";
                case 0x09: return "Tab";
                case 0x0D: return "Enter";
                case 0x1B: return "Esc";
                case 0x20: return "Space";
                case 0x21: return "PageUp";
                case 0x22: return "PageDown";
                case 0x23: return "End";
                case 0x24: return "Home";
                case 0x25: return "Left";
                case 0x26: return "Up";
                case 0x27: return "Right";
                case 0x28: return "Down";
                case 0x2D: return "Insert";
                case 0x2E: return "Delete";
                case 0xBA: return ";";
                case 0xBB: return "+";
                case 0xBC: return ",";
                case 0xBD: return "-";
                case 0xBE: return ".";
                case 0xBF: return "/";
                case 0xDB: return "[";
                case 0xDC: return "\\";
                case 0xDD: return "]";
                case 0xDE: return "'";
                default: return "VK_" + virtualKey.ToString("X2");
            }
        }

        private static bool IsModifierKey(int virtualKey)
        {
            return virtualKey == 0x10 || virtualKey == 0x11 || virtualKey == 0x12 ||
                   virtualKey == 0xA0 || virtualKey == 0xA1 || virtualKey == 0xA2 ||
                   virtualKey == 0xA3 || virtualKey == 0xA4 || virtualKey == 0xA5;
        }

        private static bool IsBindableVirtualKey(int virtualKey)
        {
            return (virtualKey >= 0x30 && virtualKey <= 0x39) ||
                   (virtualKey >= 0x41 && virtualKey <= 0x5A) ||
                   (virtualKey >= 0x60 && virtualKey <= 0x69) ||
                   (virtualKey >= 0x70 && virtualKey <= 0x87) ||
                   (virtualKey >= 0xBA && virtualKey <= 0xC0) ||
                   (virtualKey >= 0xDB && virtualKey <= 0xDF) ||
                   virtualKey == 0x08 || virtualKey == 0x09 || virtualKey == 0x0D ||
                   virtualKey == 0x13 || virtualKey == 0x14 || virtualKey == 0x1B ||
                   virtualKey == 0x20 || (virtualKey >= 0x21 && virtualKey <= 0x28) ||
                   virtualKey == 0x2D || virtualKey == 0x2E;
        }

        private static void BeginShortcutCapture()
        {
            GuiShortcutCapture = true;
            GuiShortcutCaptureEscapeDown = false;
            GuiStatus = Loc.T("Press a key to bind; Esc cancels");
            RebuildContent();
        }

        private static void CancelShortcutCapture()
        {
            GuiShortcutCapture = false;
            GuiShortcutCaptureEscapeDown = false;
            GuiStatus = Loc.T("Shortcut change cancelled");
            RebuildContent();
        }

        private static void SetPanelShortcut(int virtualKey)
        {
            PanelShortcutVirtualKey = virtualKey;
            PanelShortcutDisplay = GetVirtualKeyDisplayName(virtualKey);
            OpenPanelKey.Value = virtualKey.ToString();
            Settings.Save();
            GuiShortcutCapture = false;
            GuiShortcutCaptureEscapeDown = false;
            GuiShortcutDown = true;
            GuiStatus = Loc.T("Shortcut changed to {0}", PanelShortcutDisplay);
            LogEvent("panel-shortcut", "key=" + PanelShortcutDisplay + " vk=" + virtualKey);
            RebuildContent();
        }

        private static bool IsMouseAxis(string axisName)
        {
            return axisName == "Mouse X" || axisName == "Mouse Y" || axisName == "Mouse ScrollWheel";
        }

        internal static bool ShouldSuppressGameplayMouseAxis(string axisName)
        {
            return (GuiVisible || GuiShortcutCapture) && IsMouseAxis(axisName);
        }

        internal static bool ShouldBlockGameInput => GuiVisible || GuiShortcutCapture;

        internal static void RestoreCursorAfterReturningToGame()
        {
            if (!RestoreGameplayCursorWhenUncovered || GuiVisible || GuiClosing || GuiShortcutCapture) return;
            var root = UIRoot.Instance;
            if (root == null || root.CurrentMode != UIRoot.Mode.Game)
            {
                RestoreGameplayCursorWhenUncovered = false;
                return;
            }
            // Let native menus/dialogs finish closing before recapturing gameplay input.
            if (!Application.isFocused || root.Covered || root.TopScreen != null || (root.Pause != null && root.Pause.IsOpen) ||
                (root.Console != null && root.Console.IsOpen)) return;
            // In this game LookCaptured means another system owns look input:
            // GameInput.Look returns zero while it is true. Release that claim when resuming gameplay.
            Il2CppMayday.GameInput.LookCaptured = false;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            RestoreGameplayCursorWhenUncovered = false;
            LogEvent("gui-cursor-restored", "destination=game locked=true visible=false lookCaptured=" + Il2CppMayday.GameInput.LookCaptured + " inputBlocked=" + Il2CppMayday.GameInput.Blocked);
        }

        private static void KeepCursorAvailableForPanel()
        {
            if (!GuiVisible) return;
            if (Cursor.lockState != CursorLockMode.None) Cursor.lockState = CursorLockMode.None;
            if (!Cursor.visible) Cursor.visible = true;
        }

        public override void OnUpdate()
        {
            if (GuiLanguageRefreshPending) RefreshLocalizedShell();
            TryInstallPauseMenuButton();
            TryInstallMainMenuHostButton();
            if (GuiVisible)
            {
                KeepCursorAvailableForPanel();
                RefreshAdaptiveLayout();
                RefreshRoomJoinableToggleVisual();
                UpdatePlayerCardUi();
                if (GuiTab == 0 && Time.unscaledTime >= NextOverviewRefreshTime) RefreshOverviewMetrics();
                RefreshFocusedRoomInputs();
                HandleBlankPanelClick();
                var uiRoot = UIRoot.Instance;
                if (!CanOpenHostPanel || PanelOpenedForDefaults != CanConfigureNextRoom || uiRoot == null || uiRoot.ScreenLayer == null || GuiRoot == null || GuiScreenLayer != uiRoot.ScreenLayer)
                {
                    LogEvent("gui-context-changed", "closing=true activeHost=" + NetworkServer.activeHost);
                    SetGuiVisible(false);
                }
            }


            if (GuiShortcutCapture)
            {
                var escapeDown = (NativeInput.GetAsyncKeyState(0x1B) & 0x8000) != 0;
                if (escapeDown && !GuiShortcutCaptureEscapeDown) CancelShortcutCapture();
                GuiShortcutCaptureEscapeDown = escapeDown;
                if (GuiShortcutCapture)
                {
                    for (var key = 1; key < 256; key++)
                    {
                        if (!IsBindableVirtualKey(key) || IsModifierKey(key) || key == 0x1B) continue;
                        if ((NativeInput.GetAsyncKeyState(key) & 0x8000) == 0) continue;
                        SetPanelShortcut(key);
                        break;
                    }
                }
                GuiShortcutDown = true;
                GuiEscapeDown = escapeDown;
                return;
            }

            var shortcutDown = (NativeInput.GetAsyncKeyState(GetPanelVirtualKey()) & 0x8000) != 0;
            if (shortcutDown && !GuiShortcutDown)
            {
                if (GuiVisible) SetGuiVisible(false);
                else if (CanOpenHostPanel) SetGuiVisible(true);
                else LogEvent("gui-open-skipped", "reason=not-active-host key=" + PanelShortcutDisplay);
            }
            GuiShortcutDown = shortcutDown;

            var panelEscapeDown = (NativeInput.GetAsyncKeyState(0x1B) & 0x8000) != 0;
            if (GuiVisible && panelEscapeDown && !GuiEscapeDown) HandleNativeBack();
            GuiEscapeDown = panelEscapeDown;
        }

        internal static bool IsListPointerHeld => (NativeInput.GetAsyncKeyState(0x01) & 0x8000) != 0;

        public override void OnLateUpdate()
        {
            SmoothListScroll.Prune();
            RestoreCursorAfterReturningToGame();
        }

        public override void OnDeinitializeMelon()
        {
            SetGuiVisible(false);
            Running = false;
            try { Web?.Stop(); } catch { }
            if (GuiRoot != null) UnityEngine.Object.Destroy(GuiRoot.gameObject);
            GuiRoot = null;
            Log?.LogInfo("[MaydayHostControl] 已卸载");
        }

        internal static void InstallPauseMenuButton(Il2CppMayday.UI.PauseScreen screen)
        {
            if (!NetworkServer.activeHost || screen == null || screen.inviteBtn == null || PauseMenuButton != null) return;
            try
            {
                var parent = screen.inviteBtn.transform.parent;
                PauseMenuButton = UIF.Button(parent, "HostControlButton", Loc.T("Better Host Management"),
                    NativeAction(() => OpenFromPauseMenu(screen)), ButtonStyle.Secondary, 0f, UITheme.ButtonHeight,
                    new Il2CppSystem.Nullable<GlyphId>(GlyphId.Key), -1f, -1f);
                if (PauseMenuButton.Label != null) PauseMenuButton.Label.Align = TextAlignmentOptions.Left;
                PauseMenuButton.transform.SetSiblingIndex(screen.inviteBtn.transform.GetSiblingIndex() + 1);
                LogEvent("native-menu-entry", "installed=true");
            }
            catch (Exception e)
            {
                Log?.LogWarning("[MaydayHostControl] 添加机长菜单入口失败: " + e.Message);
            }
        }

        private static void TryInstallPauseMenuButton()
        {
            if (PauseMenuButton != null) return;
            var root = UIRoot.Instance;
            if (root != null && root.Pause != null && root.Pause.Built) InstallPauseMenuButton(root.Pause);
        }

        private static void TryInstallMainMenuHostButton()
        {
            var root = UIRoot.Instance;
            if (MainMenuHostButton != null || root == null || root.Menu == null || !root.Menu.Built || root.Menu.playBtn == null) return;
            InstallMainMenuHostButton(root.Menu);
        }

        internal static void InstallMainMenuHostButton(MainMenuScreen screen)
        {
            if (screen == null || screen.playBtn == null || MainMenuHostButton != null) return;
            var label = Loc.T("Better Host Management");
            MainMenuHostButton = UIF.Button(screen.Rt, "HostControlDefaultsButton", label,
                NativeAction(() => OpenFromMainMenu(screen)), ButtonStyle.Secondary, 260f, UITheme.ButtonHeight,
                new Il2CppSystem.Nullable<GlyphId>(GlyphId.Key), -1f, -1f);
            if (MainMenuHostButton.Label != null) MainMenuHostButton.Label.Align = TextAlignmentOptions.Left;
            var width = GetMainMenuHostButtonWidth(MainMenuHostButton, label);
            // Keep the game's animated button column unchanged; place the entry at the top right.
            UIF.At(MainMenuHostButton.GetComponent<RectTransform>(), new Vector2(1f, 1f), new Vector2(-32f, -32f), new Vector2(width, UITheme.ButtonHeight));
        }

        private static float GetMainMenuHostButtonWidth(UIButton button, string label)
        {
            var text = button != null ? button.GetComponentInChildren<TMP_Text>() : null;
            if (text == null) return 260f;
            text.enableAutoSizing = false;
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Overflow;
            text.text = label ?? string.Empty;
            var preferred = text.GetPreferredValues(text.text, float.PositiveInfinity, UITheme.ButtonHeight).x;
            var iconWidth = UITheme.ButtonHeight;
            return Mathf.Ceil(preferred + iconWidth + 48f);
        }

        private static void OpenFromMainMenu(MainMenuScreen screen)
        {
            if (!CanConfigureNextRoom) return;
            SourceMainMenuScreen = screen;
            if (!SetGuiVisible(true)) { SourceMainMenuScreen = null; return; }
        }

        private static void OpenFromPauseMenu(Il2CppMayday.UI.PauseScreen screen)
        {
            SourcePauseScreen = screen;
            if (!SetGuiVisible(true))
            {
                SourcePauseScreen = null;
                return;
            }
            if (screen != null && screen.Group != null)
            {
                screen.Group.alpha = 0f;
                screen.Group.interactable = false;
                screen.Group.blocksRaycasts = false;
                PauseScreenHidden = true;
            }
        }

        private static bool SetGuiVisible(bool visible)
        {
            if (GuiVisible == visible && !GuiClosing) return true;
            if (visible && !CanOpenHostPanel)
            {
                GuiStatus = Loc.T("Only available when hosting a room");
                LogEvent("gui-open-skipped", "reason=not-active-host");
                return false;
            }
            if (visible)
            {
                PanelOpenedForDefaults = CanConfigureNextRoom;
                if (PanelOpenedForDefaults)
                {
                    GuiTab = 2;
                    GuiStatus = string.Empty;
                }
                PendingCapacityText = PlayerLimit?.Value.ToString() ?? "6";
                RoomVisibilityPending = CurrentVisibility;
                RoomJoinablePending = CurrentJoinAllowed;
                EnsureNativeScreenCreated();
                if (GuiRoot == null)
                {
                    GuiStatus = Loc.T("Game UI is not initialized");
                    LogEvent("gui-open-skipped", "reason=ui-root-unavailable");
                    return false;
                }
                LayoutLocalizedTabs(GuiRoot);
                PreviousCursorLock = Cursor.lockState;
                PreviousCursorVisible = Cursor.visible;
                PreviousLookCaptured = Il2CppMayday.GameInput.LookCaptured;
                OpenedFromPauseMenu = SourcePauseScreen != null;
                UnderlyingTopScreen = UIRoot.Instance != null ? UIRoot.Instance.TopScreen : null;
                if (UIRoot.Instance != null && UIRoot.Instance.CurrentMode == UIRoot.Mode.Game &&
                    (OpenedFromPauseMenu || (UIRoot.Instance.Pause != null && UIRoot.Instance.Pause.IsOpen)))
                    RestoreGameplayCursorWhenUncovered = true;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                GuiRoomNameInput = RoomName?.Value ?? string.Empty;
                GuiVisible = true;
                GuiClosing = false;
                LogEvent("gui-visibility", "visible=true nativeGameUi=true");
                GuiRoot.gameObject.SetActive(true);
                GuiRoot.SetAsLastSibling();
                if (GuiCanvasGroup != null)
                {
                    Il2CppMayday.UI.UITween.Kill(GuiCanvasGroup);
                    GuiCanvasGroup.alpha = 0f;
                    GuiCanvasGroup.interactable = true;
                    GuiCanvasGroup.blocksRaycasts = true;
                }
                RebuildContent();
                FocusCurrentTab();
                PrepareAdaptiveLayoutBeforeShow();
                if (PanelOpenedForDefaults)
                {
                    SourceMainMenuScreen = UnderlyingTopScreen;
                    if (SourceMainMenuScreen != null && SourceMainMenuScreen.Group != null)
                    {
                        SourceMainMenuScreen.Group.alpha = 0f;
                        SourceMainMenuScreen.Group.interactable = false;
                        SourceMainMenuScreen.Group.blocksRaycasts = false;
                        MainMenuHidden = true;
                    }
                }
                if (GuiCanvasGroup != null) Il2CppMayday.UI.UITween.Fade(GuiCanvasGroup, 1f, 0.22f, Il2CppMayday.UI.Ease.OutQuad);
                return true;
            }

            CloseBlacklistWindow();
            if (GuiContent != null) SmoothListScroll.Cancel(GuiContent.GetComponentInParent<ScrollRect>());
            GuiVisible = false;
            if (GuiCanvasGroup != null)
            {
                GuiClosing = true;
                GuiCanvasGroup.interactable = false;
                GuiCanvasGroup.blocksRaycasts = false;
                Il2CppMayday.UI.UITween.Kill(GuiCanvasGroup);
                Il2CppMayday.UI.UITween.Fade(GuiCanvasGroup, 0f, 0.18f, Il2CppMayday.UI.Ease.OutQuad, 0f,
                    NativeAction(() =>
                    {
                        if (GuiClosing && GuiCanvasGroup != null && GuiRoot != null)
                        {
                            GuiRoot.gameObject.SetActive(false);
                            GuiClosing = false;
                        }
                    }));
            }
            else if (GuiRoot != null) GuiRoot.gameObject.SetActive(false);
            LogEvent("gui-visibility", "visible=false nativeGameUi=true");
            if (OpenedFromPauseMenu && PauseScreenHidden && SourcePauseScreen != null && SourcePauseScreen.Group != null)
            {
                SourcePauseScreen.Group.alpha = 1f;
                SourcePauseScreen.Group.interactable = true;
                SourcePauseScreen.Group.blocksRaycasts = true;
                if (PauseMenuButton != null) UIScreen.Select(PauseMenuButton.gameObject);
            }
            if (MainMenuHidden && SourceMainMenuScreen != null && SourceMainMenuScreen.Group != null)
            {
                SourceMainMenuScreen.Group.alpha = 1f;
                SourceMainMenuScreen.Group.interactable = true;
                SourceMainMenuScreen.Group.blocksRaycasts = true;
                SourceMainMenuScreen.FocusDefault();
            }
            MainMenuHidden = false;
            SourceMainMenuScreen = null;
            PauseScreenHidden = false;
            SourcePauseScreen = null;
            UnderlyingTopScreen = null;
            Il2CppMayday.GameInput.LookCaptured = PreviousLookCaptured;
            Cursor.lockState = PreviousCursorLock;
            Cursor.visible = PreviousCursorVisible;
            OpenedFromPauseMenu = false;
            GuiConfirmRecoveryId = null;
            GuiConfirmDeleteId = null;
            PendingCapacityText = null;
            RoomVisibilityPending = null;
            RoomJoinablePending = ManualJoinAllowed;
            GuiRoomNameInput = RoomName?.Value ?? string.Empty;
            UpdateRoomSaveButtons();
            return true;
        }

        private static void ClearNativeScreenReferences()
        {
            MainPanelInteraction = null;
            BlacklistWindow = null;
            BlacklistContent = null;
            BlacklistFooterButton = null;
            EliminateAllFooterButton = null;
            KickAllFooterButton = null;
            RespawnAllFooterButton = null;
            CloseFooterButton = null;
            PlayerAvatars.Clear();
            BlacklistAvatars.Clear();
            ResetOverviewReferences();
            RoomNameInputField = null;
            CapacityInputField = null;
            RoomJoinableToggle = null;
            RoomJoinableVisualRefreshFrame = -1;
            SaveRoomButton = null;
            SaveRoomApplyButton = null;
            GuiNavButtons = null;
            GuiContent = null;
            GuiPageContent = null;
            GuiContentVisibility = null;
            GuiCanvasGroup = null;
            GuiRoot = null;
            GuiScreenLayer = null;
        }

        private static void RefreshLocalizedShell()
        {
            GuiLanguageRefreshPending = false;
            // Preserve pending room values and the original pause/cursor restoration state.
            // This runs after the selector callback returns, before updating any GUI references.
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            if (GuiCanvasGroup != null) Il2CppMayday.UI.UITween.Kill(GuiCanvasGroup);
            if (GuiRoot != null)
            {
                GuiRoot.gameObject.SetActive(false);
                UnityEngine.Object.Destroy(GuiRoot.gameObject);
            }
            ClearNativeScreenReferences();
            GuiClosing = false;
            if (PauseMenuButton != null)
            {
                PauseMenuButton.gameObject.SetActive(false);
                UnityEngine.Object.Destroy(PauseMenuButton.gameObject);
            }
            PauseMenuButton = null;
            if (MainMenuHostButton != null)
            {
                MainMenuHostButton.gameObject.SetActive(false);
                UnityEngine.Object.Destroy(MainMenuHostButton.gameObject);
            }
            MainMenuHostButton = null;
            TryInstallPauseMenuButton();
            TryInstallMainMenuHostButton();
            if (!GuiVisible) return;
            EnsureNativeScreenCreated();
            if (GuiRoot == null)
            {
                SetGuiVisible(false);
                return;
            }
            GuiRoot.gameObject.SetActive(true);
            GuiRoot.SetAsLastSibling();
            RebuildContent();
            FocusCurrentTab();
            KeepCursorAvailableForPanel();
        }

        private static void EnsureNativeScreenCreated()
        {
            var uiRoot = UIRoot.Instance;
            if (uiRoot == null || uiRoot.ScreenLayer == null) return;
            if (GuiRoot != null && GuiScreenLayer == uiRoot.ScreenLayer) return;
            if (GuiCanvasGroup != null) Il2CppMayday.UI.UITween.Kill(GuiCanvasGroup);
            if (GuiRoot != null)
            {
                GuiRoot.gameObject.SetActive(false);
                UnityEngine.Object.Destroy(GuiRoot.gameObject);
            }
            ClearNativeScreenReferences();

            GuiScreenLayer = uiRoot.ScreenLayer;
            GuiRoot = UIF.Rect("MaydayHostControlScreen", GuiScreenLayer);
            GuiCanvasGroup = GuiRoot.gameObject.AddComponent<CanvasGroup>();
            GuiCanvasGroup.alpha = 1f;
            GuiCanvasGroup.interactable = true;
            GuiCanvasGroup.blocksRaycasts = true;
            UIF.Stretch(GuiRoot);
            UIF.Dimmer(GuiRoot, 0.55f);

            var card = UIF.Panel(GuiRoot, "Card", UITheme.Paper, UITheme.RadiusLarge, true, true);
            MainPanelInteraction = card.Root.gameObject.AddComponent<CanvasGroup>();
            UIF.At(card.Root, UIF.Center, Vector2.zero, new Vector2(1180f, 900f));

            var titleText = NativeText(card.Root, "Title", Loc.T("Better Host Management"), UITheme.SizeHeader, UITheme.Navy, TextAlignmentOptions.Left, 32f, 22f, 1116f, 48f, false);
            titleText.GetComponent<TMP_Text>().enableAutoSizing = true;
            titleText.GetComponent<TMP_Text>().fontSizeMin = UITheme.SizeBody;
            var subtitleText = NativeText(card.Root, "Subtitle", Loc.T("Better Host Management · Host-only"), UITheme.SizeSmall, UITheme.InkSoft, TextAlignmentOptions.Left, 32f, 68f, 1116f, 28f, false);
            subtitleText.GetComponent<TMP_Text>().enableAutoSizing = true;
            subtitleText.GetComponent<TMP_Text>().fontSizeMin = UITheme.SizeSmall * 0.8f;

            GuiNavButtons = new UIButton[GuiTabs.Length];
            var tabWidth = 216f;
            var tabGap = 8f;
            var tabsWidth = GuiTabs.Length * tabWidth + (GuiTabs.Length - 1) * tabGap;
            var tabsX = (1180f - tabsWidth) * 0.5f;
            for (var i = 0; i < GuiTabs.Length; i++)
            {
                var tabIndex = i;
                GuiNavButtons[i] = NativeButton(card.Root, "Tab" + i, Loc.T(GuiTabs[i]), ButtonStyle.Tab,
                    tabsX + i * (tabWidth + tabGap), 132f, tabWidth, 54f, () =>
                {
                    GuiTab = tabIndex;
                    GuiConfirmRecoveryId = null;
                    GuiConfirmDeleteId = null;
                    RebuildContent();
                    FocusCurrentTab();
                });
            }
            LayoutLocalizedTabs(card.Root);

            var contentScroll = UIF.Scroll(card.Root, "MainContentScroll", out GuiPageContent, pad: UIF.Pad(6, 6, 0, 12));
            UIF.At(contentScroll.GetComponent<RectTransform>(), UIF.TopLeft, new Vector2(32f, -194f), new Vector2(NativeContentWidth, 522f));
            contentScroll.horizontal = false;
            contentScroll.vertical = true;
            SmoothListScroll.Register(contentScroll);
            SmoothListScroll.SetEnabled(contentScroll, false);
            GuiContent = GuiPageContent;
            GuiContentVisibility = GuiContent.gameObject.GetComponent<CanvasGroup>();
            if (GuiContentVisibility == null) GuiContentVisibility = GuiContent.gameObject.AddComponent<CanvasGroup>();
            GuiContentVisibility.alpha = 1f;
            GuiContentVisibility.interactable = true;
            GuiContentVisibility.blocksRaycasts = true;
            SaveRoomButton = NativeFooterButton(card.Root, "SaveRoomSettings", Loc.T("Save"), ButtonStyle.Secondary, 580f, 142f, () => SavePendingRoomSettings(false));
            SaveRoomApplyButton = NativeFooterButton(card.Root, "SaveRoomSettingsAndClose", Loc.T("Save and apply"), ButtonStyle.Primary, 726f, 156f, () => SavePendingRoomSettings(true));
            EliminateAllFooterButton = NativeFooterButton(card.Root, "EliminateAll", Loc.T("Eliminate all"), ButtonStyle.Secondary, 32f, 200f,
                () => RunPanelAction(EliminateAllRemotePlayers));
            SetPlayerActionColors(EliminateAllFooterButton, EliminateYellow, UITheme.Ink);
            KickAllFooterButton = NativeFooterButton(card.Root, "KickAll", Loc.T("Kick all"), ButtonStyle.Secondary, 244f, 200f,
                () => RunPanelAction(KickAllRemotePlayers));
            SetPlayerActionColors(KickAllFooterButton, KickRed, Color.white);
            RespawnAllFooterButton = NativeFooterButton(card.Root, "RespawnAll", Loc.T("Revive all"), ButtonStyle.Secondary, 456f, 200f,
                () => RunPanelLocalizedAction(RespawnAllPlayers));
            SetPlayerActionColors(RespawnAllFooterButton, UITheme.TealDk, Color.white);
            BlacklistFooterButton = NativeFooterButton(card.Root, "OpenBlacklist", Loc.T("Blacklist"), ButtonStyle.Secondary, 690f, 188f, OpenBlacklistWindow);
            CloseFooterButton = NativeFooterButton(card.Root, "Close", Loc.T("Close panel"), ButtonStyle.Primary, 888f, 260f, () => SetGuiVisible(false));
            LayoutFooterButtons();
            UpdateRoomSaveButtons();
            GuiRoot.gameObject.SetActive(false);
        }

        private static void RebuildContent()
        {
            if (GuiContent == null) return;
            SmoothListScroll.SetEnabled(GuiContent.GetComponentInParent<ScrollRect>(), NetworkServer.activeHost && GuiTab == 1);
            var contentWasVisible = GuiContentVisibility == null || GuiContentVisibility.alpha > 0.99f;
            if (GuiContentVisibility != null)
            {
                GuiContentVisibility.alpha = 0f;
                GuiContentVisibility.interactable = false;
                GuiContentVisibility.blocksRaycasts = false;
            }
            PlayerAvatars.Clear();
            if (BlacklistFooterButton != null) BlacklistFooterButton.gameObject.SetActive(NetworkServer.activeHost && GuiTab == 1);
            if (EliminateAllFooterButton != null) EliminateAllFooterButton.gameObject.SetActive(NetworkServer.activeHost && GuiTab == 1);
            if (KickAllFooterButton != null) KickAllFooterButton.gameObject.SetActive(NetworkServer.activeHost && GuiTab == 1);
            if (RespawnAllFooterButton != null) RespawnAllFooterButton.gameObject.SetActive(NetworkServer.activeHost && GuiTab == 1 && HasEliminatedPlayers());
            LayoutFooterButtons();
            ResetOverviewReferences();
            RoomNameInputField = null;
            CapacityInputField = null;
            RoomJoinableToggle = null;
            RoomJoinableVisualRefreshFrame = -1;
            for (var i = GuiContent.childCount - 1; i >= 0; i--)
            {
                var child = GuiContent.GetChild(i).gameObject;
                child.SetActive(false);
                UnityEngine.Object.Destroy(child);
            }
            for (var i = 0; i < GuiNavButtons.Length; i++)
            {
                if (GuiNavButtons[i] == null) continue;
                GuiNavButtons[i].SetInteractable(!PanelOpenedForDefaults || i == 2 || i == 3);
                GuiNavButtons[i].SetSelected(i == GuiTab);
            }

            if (!NetworkServer.activeHost && !(PanelOpenedForDefaults && (GuiTab == 2 || GuiTab == 3)))
            {
                NativeSettingsSection(GuiContent, Loc.T("Host permissions"));
                NativeDisplayRow(GuiContent, Loc.T("Status"), Loc.T("Create a single-player or Steam room on this computer first."), true);
                if (contentWasVisible) FinishAdaptiveLayoutAfterRebuild();
                return;
            }

            switch (GuiTab)
            {
                case 0: BuildOverview(); break;
                case 1: BuildPlayers(); break;
                case 2: BuildRoom(); break;
                case 3: BuildSettings(); break;
            }
            UpdateRoomSaveButtons();
            if (contentWasVisible) FinishAdaptiveLayoutAfterRebuild();
        }

        private static void StabilizeContentLayout()
        {
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(GuiPageContent);
            RefreshAdaptiveLayout(force: true);
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(GuiPageContent);

            // The first pass assigns final widths. Re-measure wrapped text against those widths before showing.
            RefreshAdaptiveLayout(force: true);
            if (GuiTab == 1) FitPersonCards(GuiContent);
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(GuiPageContent);
        }

        private static void FinishAdaptiveLayoutAfterRebuild()
        {
            StabilizeContentLayout();
            if (GuiContentVisibility != null)
            {
                GuiContentVisibility.alpha = 1f;
                GuiContentVisibility.interactable = true;
                GuiContentVisibility.blocksRaycasts = true;
            }
        }

        private static void PrepareAdaptiveLayoutBeforeShow()
        {
            if (GuiContentVisibility != null)
            {
                GuiContentVisibility.alpha = 0f;
                GuiContentVisibility.interactable = false;
                GuiContentVisibility.blocksRaycasts = false;
            }
            StabilizeContentLayout();
            if (GuiContentVisibility != null)
            {
                GuiContentVisibility.alpha = 1f;
                GuiContentVisibility.interactable = true;
                GuiContentVisibility.blocksRaycasts = true;
            }
        }

        private static void BuildOverview()
        {
            ResetOverviewReferences();
            NativeSettingsSection(GuiContent, Loc.T("Room status"));
            var serverMetrics = UIF.Rect("Overview.ServerMetrics", GuiContent);
            UIF.LE(serverMetrics.gameObject, -1f, -1f, 1f, 0f, -1f, 1f);
            UIF.VLayout(serverMetrics.gameObject, 8f, UIF.Pad(0), TextAnchor.UpperLeft, true, true, true, false);


            var roomGrid = UIF.Rect("Overview.RoomGrid", serverMetrics);
            UIF.LE(roomGrid.gameObject, -1f, 0f, 1f, 0f, -1f, 0f);
            UIF.HLayout(roomGrid.gameObject, 10f, UIF.Pad(0), TextAnchor.UpperLeft, true, false, false, false);
            OverviewOnlineValue = AddOverviewMetricCard(roomGrid, Loc.T("Online remote players"), UITheme.SizeHeader, 1f);
            OverviewCapacityValue = AddOverviewMetricCard(roomGrid, Loc.T("Room capacity"), UITheme.SizeHeader, 1f, out OverviewCapacityDetail);
            OverviewJoinableValue = AddOverviewMetricCard(roomGrid, Loc.T("Allow joining"), UITheme.SizeBody, 1f);
            UIF.LE(OverviewOnlineValue.transform.parent.gameObject, -1f, 0f, 1f, 0f, 260f, 0f);
            UIF.LE(OverviewCapacityValue.transform.parent.gameObject, -1f, 0f, 1f, 0f, 260f, 0f);
            UIF.LE(OverviewJoinableValue.transform.parent.gameObject, -1f, 0f, 1f, 0f, 260f, 0f);
            var roomCardHeight = 0;
            foreach (var value in new[] { OverviewOnlineValue, OverviewCapacityValue, OverviewJoinableValue })
            {
                var card = value.transform.parent.GetComponent<RectTransform>();
                if (card == null) continue;
                var verticalLayout = card.GetComponent<VerticalLayoutGroup>();
                if (verticalLayout != null)
                {
                    var contentHeight = verticalLayout.padding.top + verticalLayout.padding.bottom;
                    for (var i = 0; i < card.childCount; i++)
                        contentHeight += Mathf.CeilToInt(LayoutUtility.GetPreferredHeight(card.GetChild(i) as RectTransform));
                    contentHeight += Mathf.Max(0, card.childCount - 1) * Mathf.CeilToInt(verticalLayout.spacing);
                    roomCardHeight = Math.Max(roomCardHeight, contentHeight);
                }
            }
            foreach (var value in new[] { OverviewOnlineValue, OverviewCapacityValue, OverviewJoinableValue })
            {
                var layout = value.transform.parent.GetComponent<LayoutElement>();
                if (layout != null)
                {
                    layout.minHeight = roomCardHeight;
                    layout.preferredHeight = roomCardHeight;
                    layout.flexibleHeight = 0f;
                }
            }
            var roomGridLayout = roomGrid.GetComponent<LayoutElement>();
            if (roomGridLayout != null) { roomGridLayout.minHeight = roomCardHeight; roomGridLayout.preferredHeight = roomCardHeight; }
            LayoutRebuilder.MarkLayoutForRebuild(roomGrid);

            NativeSettingsSection(GuiContent, Loc.T("Flight status"));
            var flightMetrics = UIF.Rect("Overview.FlightMetrics", GuiContent);
            UIF.LE(flightMetrics.gameObject, -1f, -1f, 1f, 0f, -1f, 1f);
            UIF.VLayout(flightMetrics.gameObject, 8f, UIF.Pad(0), TextAnchor.UpperLeft, true, true, true, false);

            var flightRow = AddOverviewMetricRow(flightMetrics,
                (Loc.T("Phase"), Il2CppMayday.GameState.Instance != null ? Loc.T(Il2CppMayday.GameState.Instance.Phase.ToString()) : Loc.T("Not initialized")),
                (Loc.T("Leg"), (Il2CppMayday.GameState.Instance != null ? Il2CppMayday.GameState.Instance.Leg : -1).ToString()),
                (Loc.T("Money"), "$" + (Il2CppMayday.GameState.Instance != null ? Il2CppMayday.GameState.Instance.Money : 0)));
            OverviewFlightPhaseValue = flightRow.First;
            OverviewFlightLegValue = flightRow.Second;
            OverviewMoneyValue = flightRow.Third;

            RefreshOverviewMetrics();
        }

        private static void ResetOverviewReferences()
        {
            OverviewOnlineValue = null;
            OverviewCapacityValue = null;
            OverviewCapacityDetail = null;
            OverviewJoinableValue = null;
            OverviewFlightPhaseValue = null;
            OverviewFlightLegValue = null;
            OverviewMoneyValue = null;
            NextOverviewRefreshTime = 0f;
        }

        private static TMP_Text AddOverviewMetricCard(Transform parent, string label, float valueSize, float flexibleWidth)
        {
            return AddOverviewMetricCard(parent, label, valueSize, flexibleWidth, false, out _);
        }

        private static TMP_Text AddOverviewMetricCard(Transform parent, string label, float valueSize, float flexibleWidth, out TMP_Text detailText)
        {
            return AddOverviewMetricCard(parent, label, valueSize, flexibleWidth, true, out detailText);
        }

        private static TMP_Text AddOverviewMetricCard(Transform parent, string label, float valueSize, float flexibleWidth, bool includeDetail, out TMP_Text detailText)
        {
            var panel = UIF.Panel(parent, "Overview.Metric." + label, UITheme.PaperDk, UITheme.RadiusSmall, false, false);
            UIF.LE(panel.Root.gameObject, 0f, -1f, flexibleWidth, 0f, 0f, -1f);
            UIF.VLayout(panel.Root.gameObject, 2f, UIF.Pad(14, 10, 10, 8), TextAnchor.UpperLeft, true, true, true, false);
            var labelText = NativeFlowText(panel.Root, "Label", label, UITheme.SizeSmall, UITheme.InkSoft, TextAlignmentOptions.Left, false);
            UIF.LE(labelText.gameObject, -1f, 22f, 1f, 0f, 1f, 22f);
            var valueText = NativeFlowText(panel.Root, "Value", string.Empty, valueSize, UITheme.Navy, TextAlignmentOptions.Left, true).GetComponent<TMP_Text>();
            UIF.LE(valueText.gameObject, -1f, valueSize >= UITheme.SizeHeader ? 38f : 32f, 1f, 0f, 1f, valueSize >= UITheme.SizeHeader ? 38f : 32f);
            detailText = null;
            if (includeDetail)
            {
                detailText = NativeFlowText(panel.Root, "Detail", string.Empty, UITheme.SizeSmall, UITheme.InkSoft, TextAlignmentOptions.Left, true).GetComponent<TMP_Text>();
                UIF.LE(detailText.gameObject, -1f, 20f, 1f, 0f, 1f, 20f);
            }
            return valueText;
        }

        private static (TMP_Text First, TMP_Text Second, TMP_Text Third) AddOverviewMetricRow(Transform parent,
            (string label, string value) first, (string label, string value) second, (string label, string value) third)
        {
            var row = UIF.Rect("Overview.MetricRow", parent);
            UIF.LE(row.gameObject, -1f, 78f, 1f, 0f, -1f, 78f);
            UIF.HLayout(row.gameObject, 10f, UIF.Pad(0), TextAnchor.MiddleCenter, true, true, true, false);
            var firstValue = AddOverviewMetricCard(row, first.label, UITheme.SizeBody, 1f);
            var secondValue = AddOverviewMetricCard(row, second.label, UITheme.SizeBody, 1f);
            var thirdValue = AddOverviewMetricCard(row, third.label, UITheme.SizeBody, 1f);
            firstValue.text = first.value ?? string.Empty;
            secondValue.text = second.value ?? string.Empty;
            thirdValue.text = third.value ?? string.Empty;
            return (firstValue, secondValue, thirdValue);
        }

        private static void RefreshOverviewMetrics()
        {
            NextOverviewRefreshTime = Time.unscaledTime + 0.5f;
            var state = Il2CppMayday.GameState.Instance;
            var configuredLimit = PlayerLimit?.Value ?? 0;
            var lobbyLimit = -1;
            var lobbyAvailable = false;
            var lobbyReadFailed = false;
            try
            {
                lobbyAvailable = LobbyService != null && LobbyService.InLobby && LobbyService.Lobby.IsValid();
                if (lobbyAvailable) lobbyLimit = Il2CppSteamworks.SteamMatchmaking.GetLobbyMemberLimit(LobbyService.Lobby);
            }
            catch (Exception e)
            {
                lobbyReadFailed = true;
                Log?.LogDebug("[MaydayHostControl] 读取 Steam 大厅上限失败: " + e.Message);
            }
            var manager = NetworkManager.singleton;
            var managerLimit = manager != null ? manager.maxConnections : -1;
            var networkLimit = NetworkServer.maxConnections;

            if (OverviewOnlineValue != null) OverviewOnlineValue.text = Online.Count.ToString();
            if (OverviewCapacityValue != null) OverviewCapacityValue.text = configuredLimit.ToString();
            if (OverviewCapacityDetail != null)
            {
                var limitsMatch = lobbyAvailable && lobbyLimit == configuredLimit && managerLimit == configuredLimit && networkLimit == configuredLimit;
                OverviewCapacityDetail.text = lobbyReadFailed
                    ? Loc.T("Steam limit is temporarily unavailable")
                    : (!lobbyAvailable
                        ? Loc.T("No lobby; using current configuration")
                        : (limitsMatch
                            ? Loc.T("Steam / Manager / Network limits match")
                            : Loc.T("Limits differ · Steam {0} / Manager {1} / Network {2}", lobbyLimit >= 0 ? lobbyLimit.ToString() : Loc.T("Unavailable"), managerLimit, networkLimit)));
                OverviewCapacityDetail.color = lobbyReadFailed || (!lobbyAvailable || limitsMatch) ? UITheme.InkSoft : UITheme.Red;
            }
            if (OverviewJoinableValue != null)
            {
                OverviewJoinableValue.text = ManualJoinAllowed ? Loc.T("Open") : Loc.T("Closed");
                OverviewJoinableValue.color = ManualJoinAllowed ? UITheme.TealDk : UITheme.Red;
            }
            if (OverviewFlightPhaseValue != null) OverviewFlightPhaseValue.text = state != null ? Loc.T(state.Phase.ToString()) : Loc.T("Not initialized");
            if (OverviewFlightLegValue != null) OverviewFlightLegValue.text = state != null ? state.Leg.ToString() : "-1";
            if (OverviewMoneyValue != null) OverviewMoneyValue.text = "$" + (state != null ? state.Money : 0);

        }

        private static string OpenSteamProfile(ulong steamId)
        {
            if (steamId == 0) return Loc.T("Invalid SteamID");
            var id = new CSteamID(steamId);
            try
            {
                SteamFriends.ActivateGameOverlayToUser("steamid", id);
                return Loc.T("Requested Steam profile; if the overlay does not open, visit {0}", "https://steamcommunity.com/profiles/" + steamId);
            }
            catch (Exception e)
            {
                var url = "https://steamcommunity.com/profiles/" + steamId;
                Log?.LogWarning("[MaydayHostControl] 打开 Steam 资料失败: " + e.Message);
                return Loc.T("Steam overlay failed to open; profile link: {0}", url);
            }
        }

        private static string AddSteamFriend(ulong steamId)
        {
            if (steamId == 0) return Loc.T("Invalid SteamID");
            try
            {
                var id = new CSteamID(steamId);
                var relationship = SteamFriends.GetFriendRelationship(id);
                if (relationship == EFriendRelationship.k_EFriendRelationshipFriend) return Loc.T("This player is already a Steam friend");
                SteamFriends.ActivateGameOverlayToUser("friendadd", id);
                return Loc.T("Requested the Steam add-friend panel; confirm in Steam");
            }
            catch (Exception e)
            {
                Log?.LogWarning("[MaydayHostControl] 打开 Steam 好友面板失败: " + e.Message);
                return Loc.T("Cannot open the Steam friends panel; check Steam client and overlay settings");
            }
        }

        private static void BuildPlayers()
        {
            BuildPlayerCards();
        }

        private static string CurrentVisibility => PanelOpenedForDefaults
            ? (DefaultRoomVisibility?.Value ?? "FriendsOnly")
            : (LobbyService != null ? LobbyService.Visibility.ToString() : "FriendsOnly");
        private static bool HasPendingRoomChanges
        {
            get
            {
                var hasValidCapacity = int.TryParse(PendingCapacityText, out var pendingCapacity) && pendingCapacity >= 1 && pendingCapacity <= 250;
                return SanitizeRoomName(GuiRoomNameInput) != (RoomName?.Value ?? string.Empty) ||
                       !hasValidCapacity || pendingCapacity != (PlayerLimit?.Value ?? 6) ||
                       RoomJoinablePending != CurrentJoinAllowed ||
                       (!PanelOpenedForDefaults && LastJoinableSetKnown && RoomJoinablePending != LastJoinableSet) ||
                       (!string.IsNullOrEmpty(RoomVisibilityPending) && RoomVisibilityPending != CurrentVisibility);
            }
        }

        private static void RefreshFocusedRoomInputs()
        {
            if (!GuiVisible || GuiTab != 2) return;
            var changed = false;
            if (RoomNameInputField != null && RoomNameInputField.isFocused && GuiRoomNameInput != RoomNameInputField.text)
            {
                GuiRoomNameInput = RoomNameInputField.text;
                changed = true;
            }
            if (CapacityInputField != null && CapacityInputField.isFocused)
            {
                var value = new string((CapacityInputField.text ?? string.Empty).Where(char.IsDigit).Take(3).ToArray());
                if (PendingCapacityText != value)
                {
                    PendingCapacityText = value;
                    changed = true;
                }
            }
            if (changed) UpdateRoomSaveButtons();
        }

        private static void HandleBlankPanelClick()
        {
            var down = (NativeInput.GetAsyncKeyState(0x01) & 0x8000) != 0;
            var pressed = down && !GuiMouseDown;
            GuiMouseDown = down;
            if (!pressed || !GuiVisible || GuiTab != 2) return;

            var eventSystem = EventSystem.current;
            if (eventSystem == null) return;
            var selected = eventSystem.currentSelectedGameObject;
            var nameInput = RoomNameInputField != null && RoomNameInputField.isFocused;
            var capacityInput = CapacityInputField != null && CapacityInputField.isFocused;
            if (!nameInput && !capacityInput && selected == null) return;

            if (!NativeInput.TryGetCursorPosition(out var cursorPosition)) return;
            var pointer = new PointerEventData(eventSystem) { position = cursorPosition };
            var hits = new Il2CppSystem.Collections.Generic.List<RaycastResult>();
            eventSystem.RaycastAll(pointer, hits);
            var hitSelectable = false;
            if (hits != null)
            {
                for (var i = 0; i < hits.Count; i++)
                {
                    var hit = hits[i].gameObject;
                    if (hit != null && hit.GetComponentInParent<Selectable>() != null)
                    {
                        hitSelectable = true;
                        break;
                    }
                }
            }
            if (hitSelectable) return;

            if (nameInput) RoomNameInputField.DeactivateInputField();
            if (capacityInput) CapacityInputField.DeactivateInputField();
            if (eventSystem != null) eventSystem.SetSelectedGameObject(null);
            UpdateRoomSaveButtons();
        }

        private static void RefreshRoomJoinableToggleVisual()
        {
            if (RoomJoinableToggle == null || RoomJoinableVisualRefreshFrame < 0 || Time.frameCount < RoomJoinableVisualRefreshFrame) return;
            if (GuiTab != 2 || GuiContent == null || !RoomJoinableToggle.gameObject.activeInHierarchy) return;
            RoomJoinableVisualRefreshFrame = -1;
            try
            {
                RoomJoinableToggle.SetValue(RoomJoinablePending, true);
                RoomJoinableToggle.Visual();
                Canvas.ForceUpdateCanvases();
                LogEvent("room-joinable-visual-refresh", "on=" + RoomJoinablePending + " frame=" + Time.frameCount);
            }
            catch (Exception e)
            {
                Log?.LogWarning("[MaydayHostControl] Failed to refresh room joinable toggle: " + e.Message);
            }
        }

        private static void UpdateRoomSaveButtons()
        {
            var visible = GuiVisible && GuiTab == 2 && HasPendingRoomChanges;
            if (SaveRoomButton != null) SaveRoomButton.gameObject.SetActive(visible);
            if (SaveRoomApplyButton != null) SaveRoomApplyButton.gameObject.SetActive(visible);
        }

        private static void SavePendingRoomSettings(bool closeAfterSave)
        {
            if (!int.TryParse(PendingCapacityText, out var capacity) || capacity < 1 || capacity > 250)
            {
                GuiStatus = Loc.T("Capacity must be between 1 and 250");
                return;
            }

            var pendingName = SanitizeRoomName(GuiRoomNameInput);
            if (PanelOpenedForDefaults)
            {
                if (!CanConfigureNextRoom) { SetGuiVisible(false); return; }
                if (RoomVisibilityPending != "Private" && RoomVisibilityPending != "FriendsOnly" && RoomVisibilityPending != "Public") return;
                RoomName.Value = pendingName;
                PlayerLimit.Value = capacity;
                DefaultRoomJoinable.Value = RoomJoinablePending;
                DefaultRoomVisibility.Value = RoomVisibilityPending;
                RoomDefaultsConfigured.Value = true;
                Settings.Save();
                GuiRoomNameInput = pendingName;
                GuiStatus = Loc.T("Room defaults saved; they will apply when you next host a room.");
                LogEvent("room-defaults-save", "capacity=" + capacity + " joinable=" + RoomJoinablePending + " visibility=" + RoomVisibilityPending);
                if (closeAfterSave) SetGuiVisible(false);
                else RebuildContent();
                return;
            }
            if (!NetworkServer.activeHost) { SetGuiVisible(false); return; }
            var failures = new System.Collections.Generic.List<string>();
            if (pendingName != (RoomName?.Value ?? string.Empty))
            {
                var result = ApplyRoomName(pendingName);
                if (result.Contains("失败") || result.Contains("拒絕") || result.Contains("拒绝")) failures.Add(Loc.T("Room name: {0}", Loc.RecoveryMessage(result)));
                else GuiRoomNameInput = pendingName;
            }
            if (capacity != (PlayerLimit?.Value ?? 6))
            {
                var result = ApplyRoomCapacity(capacity);
                if (result.Contains("失败") || result.Contains("未接受") || result.Contains("拒绝")) failures.Add(Loc.T("Capacity: {0}", Loc.RecoveryMessage(result)));
                else PendingCapacityText = capacity.ToString();
            }
            if (RoomJoinablePending != ManualJoinAllowed || (LastJoinableSetKnown && RoomJoinablePending != LastJoinableSet))
            {
                var result = ApplyRoomJoinable(RoomJoinablePending);
                if (result.Contains("失败") || result.Contains("不允许") || result.Contains("没有可修改") || result.Contains("拒绝") || result.Contains("未保存")) failures.Add(Loc.T("Allow joining: {0}", Loc.RecoveryMessage(result)));
            }
            if (!string.IsNullOrEmpty(RoomVisibilityPending) && RoomVisibilityPending != CurrentVisibility)
            {
                var result = ApplyRoomVisibility(RoomVisibilityPending);
                if (result.Contains("失败") || result.Contains("没有可修改")) failures.Add(Loc.T("Visibility: {0}", Loc.RecoveryMessage(result)));
            }

            NextStatusRefreshUtc = DateTime.MinValue;
            RefreshWebSnapshot();
            if (failures.Count > 0)
            {
                GuiStatus = Loc.T("Some settings were not applied: {0}", string.Join("; ", failures));
                LogEvent("room-settings-save-partial", string.Join(" | ", failures));
                RebuildContent();
                return;
            }

            Settings.Save();
            PendingCapacityText = (PlayerLimit?.Value ?? 6).ToString();
            RoomVisibilityPending = CurrentVisibility;
            RoomJoinablePending = ManualJoinAllowed;
            GuiStatus = Loc.T("Room settings saved and applied");
            LogEvent("room-settings-save", "closeAfterSave=" + closeAfterSave + " capacity=" + PendingCapacityText + " joinable=" + ManualJoinAllowed + " visibility=" + RoomVisibilityPending);
            if (closeAfterSave) SetGuiVisible(false);
            else RebuildContent();
        }

        private static void BuildRoom()
        {
            var capacityText = PendingCapacityText ?? (PlayerLimit?.Value ?? 6).ToString();
            var visibilityNames = new[] { Loc.T("Invite only"), Loc.T("Friends only"), Loc.T("Public") };
            var visibilityValues = new[] { "Private", "FriendsOnly", "Public" };
            var visibility = string.IsNullOrEmpty(RoomVisibilityPending)
                ? CurrentVisibility
                : RoomVisibilityPending;
            var visibilityIndex = Array.IndexOf(visibilityValues, visibility);
            if (visibilityIndex < 0) visibilityIndex = 1;

            NativeSettingsSection(GuiContent, Loc.T(PanelOpenedForDefaults ? "Next room defaults" : "Room settings"));
            if (PanelOpenedForDefaults && GuiStatus == Loc.T("Room defaults saved; they will apply when you next host a room."))
                NativeDisplayRow(GuiContent, Loc.T("Status"), GuiStatus, true);

            var nameControl = NewNativeSettingRow(GuiContent, "RoomName", Loc.T("Room name"), null, 64f);
            RoomNameInputField = UIF.Input(nameControl, "RoomNameInput", GuiRoomNameInput, "", 48,
                NativeStringAction(value =>
                {
                    GuiRoomNameInput = value;
                    UpdateRoomSaveButtons();
                }), 360f, 50f);

            var capacityControl = NewNativeSettingRow(GuiContent, "Capacity", Loc.T("Capacity"), Loc.T("Mod range: 1-250; subject to Steam limits"), 64f);
            CapacityInputField = UIF.Input(capacityControl, "CapacityInput", capacityText, "1-250", 3,
                NativeStringAction(value =>
                {
                    PendingCapacityText = new string((value ?? string.Empty).Where(char.IsDigit).Take(3).ToArray());
                    UpdateRoomSaveButtons();
                }), 160f, 50f);

            var joinControl = NewNativeSettingRow(GuiContent, "Joinable", Loc.T("Allow joining"), RoomJoinablePending ? Loc.T("Open (unsaved)") : Loc.T("Closed (unsaved)"), 64f);
            var joinableToggle = UIF.Toggle(joinControl, "JoinableToggle", RoomJoinablePending,
                NativeBoolAction(value =>
                {
                    RoomJoinablePending = value;
                    UpdateRoomSaveButtons();
                }));
            // UIF.Toggle may build with the correct value before its visual Awake/update pass; force the native state now.
            joinableToggle.SetValue(RoomJoinablePending, true);
            joinableToggle.Visual();
            RoomJoinableToggle = joinableToggle;
            RoomJoinableVisualRefreshFrame = Time.frameCount + 1;

            var visibilityControl = NewNativeSettingRow(GuiContent, "Visibility", Loc.T("Room visibility"), null, 64f);
            UIF.Selector(visibilityControl, "RoomVisibility", new Il2CppStringArray(visibilityNames), visibilityIndex,
                NativeIntAction(index =>
                {
                    // Compare against the live pending value, not the snapshot taken when this row was built.
                    // Otherwise selecting another option and returning to the original one leaves a stale pending value.
                    var pendingVisibility = RoomVisibilityPending ?? visibility;
                    if (index < 0 || index >= visibilityValues.Length || visibilityValues[index] == pendingVisibility) return;
                    RoomVisibilityPending = visibilityValues[index];
                    UpdateRoomSaveButtons();
                }), 360f);

        }

        private const float NativeContentWidth = 1116f;

        private static Transform NewNativeSettingRow(Transform parent, string name, string label, string description, float height)
        {
            // Use the game's own option-row factory, including its padding and description spacing.
            var control = SettingsScreen.Row(parent, label, description, height);
            var row = control.parent;
            row.name = "OfficialSettingRow." + name;
            control.name = "OfficialControl";
            return control;
        }

        private static void NativeSettingsSection(Transform parent, string title)
        {
            var first = parent.childCount;
            SettingsScreen.Section(parent, title);
            // Mark official section nodes so generic content measurement cannot erase native spacing.
            for (var i = first; i < parent.childCount; i++)
                parent.GetChild(i).name = "NativeSection." + i;
        }

        private static void BuildSaves()
        {
            var checkpoints = ListCheckpoints().ToArray();
            const int pageSize = 4;
            var pageCount = Math.Max(1, (checkpoints.Length + pageSize - 1) / pageSize);
            GuiSavePage = Math.Max(0, Math.Min(GuiSavePage, pageCount - 1));
            NativeSettingsSection(GuiContent, Loc.T("Flight saves"));
            var header = NewNativeSettingRow(GuiContent, "SaveHeader", Loc.T("Checkpoints {0} · {1}/{2}", checkpoints.Length, GuiSavePage + 1, pageCount),
                Loc.T("Restore precisely on the ground; in-flight saves return safely to the departure airport."), 64f);
            NativeButton(header, "NewSave", Loc.T("Create save"), ButtonStyle.Primary, 160f, 42f,
                () => RunPanelAction(() => SaveCheckpoint(string.Empty)));
            NativeButton(header, "SavePrev", Loc.T("Previous"), ButtonStyle.Ghost, 104f, 40f,
                () => { GuiSavePage = Math.Max(0, GuiSavePage - 1); RebuildContent(); });
            NativeButton(header, "SaveNext", Loc.T("Next"), ButtonStyle.Ghost, 104f, 40f,
                () => { GuiSavePage = Math.Min(pageCount - 1, GuiSavePage + 1); RebuildContent(); });

            var rows = GuiContent;
            var shown = checkpoints.Skip(GuiSavePage * pageSize).Take(pageSize).ToArray();
            if (shown.Length == 0) NativeDisplayRow(rows, Loc.T("Saved game"), Loc.T("No flight checkpoints yet."), true);
            foreach (var checkpoint in shown)
            {
                var detail = Loc.T("Leg {0} · {1} · {2} players", checkpoint.leg, checkpoint.airportName, checkpoint.players);
                if (!checkpoint.compatible) detail = Loc.T("Cannot recover · {0}", Loc.RecoveryMessage(checkpoint.issue));
                var control = NewNativeSettingRow(rows, "Checkpoint." + checkpoint.id, checkpoint.name, detail, 78f);
                if (GuiConfirmRecoveryId == checkpoint.id)
                {
                    NativeButton(control, "ConfirmRecover" + checkpoint.id, Loc.T("Confirm recovery"), ButtonStyle.Danger, 160f, 40f, () => { GuiConfirmRecoveryId = null; RunPanelAction(() => RecoverCheckpoint(checkpoint.id)); });
                    NativeButton(control, "CancelRecover" + checkpoint.id, Loc.T("Cancel"), ButtonStyle.Ghost, 96f, 40f, () => { GuiConfirmRecoveryId = null; RebuildContent(); });
                }
                else
                {
                    if (checkpoint.compatible && !RecoveryPoisoned) NativeButton(control, "Recover" + checkpoint.id, Loc.T("Recover"), ButtonStyle.Secondary, 96f, 40f, () => { GuiConfirmDeleteId = null; GuiConfirmRecoveryId = checkpoint.id; RebuildContent(); });
                    if (!checkpoint.legacy && GuiConfirmDeleteId == checkpoint.id)
                    {
                        NativeButton(control, "ConfirmDelete" + checkpoint.id, Loc.T("Confirm deletion"), ButtonStyle.Danger, 128f, 40f, () => { GuiConfirmDeleteId = null; RunPanelAction(() => Execute("deletecheckpoint " + checkpoint.id)); });
                        NativeButton(control, "CancelDelete" + checkpoint.id, Loc.T("Cancel"), ButtonStyle.Ghost, 96f, 40f, () => { GuiConfirmDeleteId = null; RebuildContent(); });
                    }
                    else if (!checkpoint.legacy) NativeButton(control, "Delete" + checkpoint.id, Loc.T("Delete"), ButtonStyle.Danger, 96f, 40f, () => { GuiConfirmRecoveryId = null; GuiConfirmDeleteId = checkpoint.id; RebuildContent(); });
                }
            }
        }

        private static void BuildSettings()
        {
            NativeSettingsSection(GuiContent, Loc.T("Language"));
            var languageControl = NewNativeSettingRow(GuiContent, "PanelLanguage", Loc.T("Panel language"), null, 64f);
            UIF.Selector(languageControl, "PanelLanguageSelector", new Il2CppStringArray(Loc.LanguageNames), Loc.CurrentIndex,
                NativeIntAction(index =>
                {
                    if (index < 0 || index >= Loc.LanguageCodes.Length || index == Loc.CurrentIndex) return;
                    PanelLanguage.Value = Loc.LanguageCodes[index];
                    Settings.Save();
                    // Recreate on the next update, after this selector's callback has returned.
                    GuiLanguageRefreshPending = true;
                }), 360f);

            NativeSettingsSection(GuiContent, Loc.T("Input settings"));
            var shortcutControl = NewNativeSettingRow(GuiContent, "Shortcut", Loc.T("Panel shortcut"),
                GuiShortcutCapture ? Loc.T("Press any key to bind; Esc cancels") : Loc.T("Click the button, then press a key to bind"), 64f);
            NativeButton(shortcutControl, "ShortcutCapture", GuiShortcutCapture ? Loc.T("Waiting for key…") : PanelShortcutDisplay,
                GuiShortcutCapture ? ButtonStyle.Primary : ButtonStyle.Secondary, 220f, 42f,
                () => { if (!GuiShortcutCapture) BeginShortcutCapture(); });
        }

        private static void RunPanelAction(Func<string> action) => RunPanelAction(action, false);

        private static void RunPanelLocalizedAction(Func<string> action) => RunPanelAction(action, true);

        private static void RunPanelAction(Func<string> action, bool alreadyLocalized)
        {
            try
            {
                var result = action();
                GuiStatus = alreadyLocalized ? result : Loc.RecoveryMessage(result);
                NextStatusRefreshUtc = DateTime.MinValue;
                RefreshWebSnapshot();
            }
            catch (Exception e)
            {
                GuiStatus = Loc.T("Action failed: {0}", e.Message);
                Log?.LogError("[MaydayHostControl] GUI action failed: " + e);
            }
            RebuildContent();
        }

        private static void NativeDisplayRow(Transform parent, string label, string value, bool wrap = false, bool danger = false)
        {
            var row = UIF.Row(parent, "DisplayRow." + label, 64f, 14f, UIF.Pad(10, 6, 0, 0));
            var labelText = NativeFlowText(row, "Label", label ?? string.Empty, UITheme.SizeBody,
                danger ? UITheme.Red : UITheme.Ink, TextAlignmentOptions.Left, false);
            UIF.LE(labelText.gameObject, -1f, -1f, 0f, -1f, 360f, -1f);

            var valueText = NativeFlowText(row, "Value", value ?? string.Empty, UITheme.SizeBody,
                danger ? UITheme.Red : UITheme.InkSoft, TextAlignmentOptions.Right, wrap);
            UIF.LE(valueText.gameObject, -1f, 64f, 1f, 0f, 360f, 36f);
        }

        private static void LayoutLocalizedTabs(Transform parent)
        {
            if (GuiNavButtons == null || GuiNavButtons.Length == 0) return;
            const float left = 32f;
            const float bounds = 1116f;
            const float gap = 8f;
            const float padding = 24f;
            var visibleIndices = PanelOpenedForDefaults
                ? new[] { 2, 3 }
                : Enumerable.Range(0, GuiNavButtons.Length).ToArray();
            foreach (var i in Enumerable.Range(0, GuiNavButtons.Length))
                if (GuiNavButtons[i] != null) GuiNavButtons[i].gameObject.SetActive(visibleIndices.Contains(i));

            var naturalWidths = new float[visibleIndices.Length];
            var total = gap * Math.Max(0, visibleIndices.Length - 1);
            for (var j = 0; j < visibleIndices.Length; j++)
            {
                var text = GuiNavButtons[visibleIndices[j]].GetComponentInChildren<TMP_Text>();
                if (text == null) continue;
                text.enableAutoSizing = false;
                text.enableWordWrapping = false;
                text.overflowMode = TextOverflowModes.Overflow;
                naturalWidths[j] = Mathf.Max(120f, text.GetPreferredValues(text.text, float.PositiveInfinity, 60f).x + padding);
                total += naturalWidths[j];
            }
            var scale = Mathf.Min(1f, bounds / Mathf.Max(1f, total));
            var widths = naturalWidths.Select(w => Mathf.Max(120f, w * scale)).ToArray();
            var maxTextWidth = naturalWidths.Length == 0 ? 1f : naturalWidths.Max() - padding;
            var commonFontScale = Mathf.Min(1f, widths.Min() / Mathf.Max(1f, maxTextWidth));
            var tabAreaWidth = widths.Sum() + gap * Math.Max(0, visibleIndices.Length - 1);
            var x = left + Mathf.Max(0f, (bounds - tabAreaWidth) * 0.5f);
            var tabY = GuiNavButtons[visibleIndices[0]].GetComponent<RectTransform>().anchoredPosition.y;
            for (var j = 0; j < visibleIndices.Length; j++)
            {
                var button = GuiNavButtons[visibleIndices[j]];
                var rect = button.GetComponent<RectTransform>();
                var text = button.GetComponentInChildren<TMP_Text>();
                if (rect == null || text == null) continue;
                rect.sizeDelta = new Vector2(widths[j], 54f);
                rect.anchoredPosition = new Vector2(x, tabY);
                text.enableAutoSizing = true;
                text.enableWordWrapping = false;
                text.overflowMode = TextOverflowModes.Overflow;
                text.fontSizeMax = UITheme.SizeBody * commonFontScale;
                text.fontSizeMin = Mathf.Min(text.fontSizeMax, UITheme.SizeSmall * 0.7f);
                text.ForceMeshUpdate();
                x += widths[j] + gap;
            }
        }

        private static float MeasureTextHeight(TMP_Text text, float width)
        {
            text.enableAutoSizing = false;
            text.enableWordWrapping = true;
            text.overflowMode = TextOverflowModes.Overflow;
            return Mathf.Ceil(text.GetPreferredValues(text.text ?? string.Empty, Mathf.Max(1f, width), float.PositiveInfinity).y) + 2f;
        }

        private static float NextAdaptiveLayoutTime;
        private static void RefreshAdaptiveLayout(bool force = false)
        {
            if (GuiContent == null || (!force && Time.unscaledTime < NextAdaptiveLayoutTime)) return;
            NextAdaptiveLayoutTime = Time.unscaledTime + 0.15f;
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(GuiContent);
            foreach (var card in GuiContent.GetComponentsInChildren<RectTransform>())
            {
                if (card.name.StartsWith("Overview.Metric."))
                    LayoutRebuilder.ForceRebuildLayoutImmediate(card);
            }
            // Read text widths only after parent and card layout have settled.
            foreach (var text in GuiContent.GetComponentsInChildren<TMP_Text>())
            {
                var node = text.transform;
                var parent = node.parent;
                if (node.name.StartsWith("NativeSection.")) continue;
                if (parent != null && parent.parent != null && parent.parent.name.StartsWith("OfficialSettingRow.")) continue;
                if (parent == null || parent.GetComponent<VerticalLayoutGroup>() == null) continue;
                var element = text.GetComponent<LayoutElement>();
                if (element == null) continue;
                var rect = text.GetComponent<RectTransform>();
                var width = rect.rect.width;
                if (width <= 1f) continue;
                var height = MeasureTextHeight(text, width);
                element.minHeight = height;
                element.preferredHeight = height;
                element.flexibleHeight = 0f;
            }
            foreach (var row in GuiContent.GetComponentsInChildren<LayoutElement>())
            {
                var rowNode = row.transform;
                if (rowNode.name.StartsWith("OfficialSettingRow."))
                {
                    // Preserve the native row's minimum; only grow it when translated text/actions need more space.
                    var texts = rowNode.Find("Texts");
                    var controls = rowNode.Find("OfficialControl");
                    var needed = row.preferredHeight;
                    if (texts != null)
                    {
                        var stack = texts.GetComponent<VerticalLayoutGroup>();
                        var textHeight = 0f;
                        foreach (var text in texts.GetComponentsInChildren<TMP_Text>())
                        {
                            var width = text.GetComponent<RectTransform>().rect.width;
                            if (width <= 1f) continue;
                            var required = MeasureTextHeight(text, width);
                            var le = text.GetComponent<LayoutElement>();
                            if (le != null) { le.minHeight = required; le.preferredHeight = required; }
                            textHeight += required;
                        }
                        if (stack != null) textHeight += stack.padding.top + stack.padding.bottom + Mathf.Max(0, texts.childCount - 1) * stack.spacing;
                        needed = Mathf.Max(needed, textHeight);
                    }
                    if (controls != null)
                        foreach (var button in controls.GetComponentsInChildren<UIButton>())
                            needed = Mathf.Max(needed, button.GetComponent<RectTransform>().rect.height);
                    row.minHeight = needed;
                    row.preferredHeight = needed;
                    continue;
                }
                if (rowNode.name.StartsWith("SettingRow.") || rowNode.name.StartsWith("Ban."))
                {
                    var textColumn = rowNode.Find("Texts");
                    var targetHeight = row.preferredHeight;
                    if (textColumn != null)
                    {
                        float textHeight = 0f;
                        foreach (var text in textColumn.GetComponentsInChildren<TMP_Text>())
                        {
                            var textElement = text.GetComponent<LayoutElement>();
                            var rect = text.GetComponent<RectTransform>();
                            if (textElement != null && rect != null && rect.rect.width > 1f)
                            {
                                var required = MeasureTextHeight(text, rect.rect.width);
                                textElement.minHeight = required;
                                textElement.preferredHeight = required;
                                textHeight += required;
                            }
                        }
                        var stack = textColumn.GetComponent<VerticalLayoutGroup>();
                        if (stack != null) textHeight += stack.padding.top + stack.padding.bottom + Mathf.Max(0, textColumn.childCount - 1) * stack.spacing;
                        targetHeight = Mathf.Max(targetHeight, textHeight + 12f);
                    }
                    row.minHeight = targetHeight;
                    row.preferredHeight = targetHeight;
                }
            }
            foreach (var layout in GuiContent.GetComponentsInChildren<VerticalLayoutGroup>())
            {
                var node = layout.transform;
                var element = node.GetComponent<LayoutElement>();
                if (element == null || !node.name.StartsWith("Overview.Metric.")) continue;
                var rect = node.GetComponent<RectTransform>();
                var width = rect.rect.width - layout.padding.left - layout.padding.right;
                if (width <= 1f) continue;
                var height = (float)(layout.padding.top + layout.padding.bottom);
                for (var i = 0; i < node.childCount; i++)
                {
                    var text = node.GetChild(i).GetComponent<TMP_Text>();
                    if (text == null) continue;
                    var required = MeasureTextHeight(text, width);
                    var textElement = text.GetComponent<LayoutElement>();
                    if (textElement != null)
                    {
                        textElement.layoutPriority = 10;
                        textElement.minHeight = required;
                        textElement.preferredHeight = required;
                        textElement.flexibleHeight = 0f;
                    }
                    height += required;
                }
                height += Mathf.Max(0, node.childCount - 1) * layout.spacing;
                element.layoutPriority = 10;
                element.minHeight = height;
                element.preferredHeight = height;
            }
            foreach (var layout in GuiContent.GetComponentsInChildren<HorizontalLayoutGroup>())
            {
                var node = layout.transform;
                var element = node.GetComponent<LayoutElement>();
                if (node.name == "Overview.RoomGrid")
                {
                    var cards = new List<LayoutElement>();
                    var height = 0;
                    for (var i = 0; i < node.childCount; i++)
                    {
                        var child = node.GetChild(i).GetComponent<RectTransform>();
                        if (child == null) continue;
                        var card = child.GetComponent<LayoutElement>();
                        if (card != null) cards.Add(card);
                        height = Math.Max(height, Mathf.CeilToInt(LayoutUtility.GetPreferredHeight(child)));
                    }
                    foreach (var card in cards)
                    {
                        card.minHeight = height;
                        card.preferredHeight = height;
                        card.flexibleHeight = 0f;
                        var cardRect = card.transform as RectTransform;
                        if (cardRect != null) cardRect.sizeDelta = new Vector2(cardRect.sizeDelta.x, height);
                    }
                    element.minHeight = height;
                    element.preferredHeight = height;
                    LayoutRebuilder.MarkLayoutForRebuild(node as RectTransform);
                    continue;
                }
                if (element == null) continue;
                if (node.name == "Overview.MetricRow" || node.name.StartsWith("SettingRow.") || node.name.StartsWith("Ban."))
                {
                    float needed = 0f;
                    for (var i = 0; i < node.childCount; i++)
                    {
                        var child = node.GetChild(i).GetComponent<RectTransform>();
                        if (child == null) continue;
                        var childElement = child.GetComponent<LayoutElement>();
                        if (node.name.StartsWith("Overview.") && childElement != null)
                        {
                            childElement.minHeight = 0f;
                            childElement.preferredHeight = -1f;
                            childElement.minWidth = 0f;
                            childElement.preferredWidth = 0f;
                        }
                        needed = Mathf.Max(needed, LayoutUtility.GetPreferredHeight(child));
                    }
                    var height = needed + layout.padding.top + layout.padding.bottom;
                    element.minHeight = height;
                    element.preferredHeight = height;
                }
                if (node.name == "Actions" || node.name == "Control" || node.name == "OfficialControl")
                {
                    float width = layout.padding.left + layout.padding.right;
                    for (var i = 0; i < node.childCount; i++)
                    {
                        var child = node.GetChild(i).GetComponent<RectTransform>();
                        if (child != null) width += LayoutUtility.GetPreferredWidth(child);
                    }
                    width += Mathf.Max(0, node.childCount - 1) * layout.spacing;
                    element.minWidth = width;
                    element.preferredWidth = width;
                }
            }
        }

        private static void SizeButtonToText(UIButton button, ref float width, ref float height, float maxWidth)
        {
            var text = button.GetComponentInChildren<TMP_Text>();
            if (text == null) return;
            text.enableAutoSizing = false;
            text.enableWordWrapping = true;
            text.overflowMode = TextOverflowModes.Overflow;
            var natural = text.GetPreferredValues(text.text, float.PositiveInfinity, float.PositiveInfinity);
            width = Mathf.Min(maxWidth, Mathf.Max(width, natural.x + 40f));
            height = Mathf.Max(height, MeasureTextHeight(text, width - 40f) + 20f);
        }

        private static UIText NativeFlowText(Transform parent, string name, string value, float size, Color color, TextAlignmentOptions alignment, bool wrap)
        {
            return UIF.Text(parent, name, value ?? string.Empty, size, color, alignment, false, wrap);
        }

        private static UIText NativeText(Transform parent, string name, string value, float size, Color color, TextAlignmentOptions alignment, float x, float y, float width, float height, bool wrap)
        {
            var text = NativeFlowText(parent, name, value, size, color, alignment, wrap);
            UIF.At(text.Rect, UIF.TopLeft, new Vector2(x, -y), new Vector2(width, height));
            return text;
        }

        private static UIButton NativeFooterButton(Transform parent, string name, string label, ButtonStyle style, float x, float width, Action action)
        {
            const float height = 58f;
            var button = UIF.Button(parent, name, label, NativeAction(action), style, width, height,
                new Il2CppSystem.Nullable<GlyphId>(), -1f, -1f);
            var text = button.GetComponentInChildren<TMP_Text>();
            if (text != null)
            {
                text.enableWordWrapping = false;
                text.enableAutoSizing = true;
                text.fontSizeMax = text.fontSize;
                text.fontSizeMin = Mathf.Min(text.fontSizeMax, UITheme.SizeSmall * 0.7f);
                text.overflowMode = TextOverflowModes.Truncate;
            }
            // All footer buttons share the same top, bottom and scale, including while selected/hovered.
            UIF.At(button.GetComponent<RectTransform>(), UIF.TopLeft, new Vector2(x, -810f), new Vector2(width, height));
            UIF.LE(button.gameObject, width, height, 0f, 0f, width, height);
            button.hoverScale = 1f;
            button.Snap();
            return button;
        }

        private static bool HasEliminatedPlayers()
        {
            if (!NetworkServer.activeHost) return false;
            foreach (var pair in Online.ToArray())
            {
                if (pair.Key == null || pair.Value == null || IsLocalHostConnection(pair.Key)) continue;
                var identity = pair.Key.identity;
                if (identity == null) continue;
                var player = identity.GetComponent<Il2CppMayday.PlayerNet>();
                if (player != null && IsPlayerEliminated(player)) return true;
            }
            return false;
        }

        private static float MeasureFooterButtonWidth(UIButton button, float minWidth, float maxWidth)
        {
            if (button == null) return minWidth;
            var text = button.GetComponentInChildren<TMP_Text>();
            if (text == null) return minWidth;
            var autoSizing = text.enableAutoSizing;
            var previousFontSize = text.fontSize;
            if (text.fontSizeMax > 0f) text.fontSize = text.fontSizeMax;
            text.enableAutoSizing = false;
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Overflow;
            var naturalWidth = 0f;
            try { naturalWidth = text.GetPreferredValues(text.text ?? string.Empty, float.PositiveInfinity, 60f).x; }
            catch { /* Keep the minimum when TMP metrics are not ready yet. */ }
            text.fontSize = previousFontSize;
            text.enableAutoSizing = autoSizing;
            text.overflowMode = TextOverflowModes.Truncate;
            return Mathf.Clamp(naturalWidth + 40f, minWidth, maxWidth);
        }

        private static void PlaceFooterButton(UIButton button, float x, float width)
        {
            if (button == null) return;
            const float height = 58f;
            var rect = button.GetComponent<RectTransform>();
            if (rect == null) return;
            UIF.At(rect, UIF.TopLeft, new Vector2(x, -810f), new Vector2(width, height));
            var layout = button.GetComponent<LayoutElement>();
            if (layout != null)
            {
                layout.minWidth = width;
                layout.preferredWidth = width;
                layout.flexibleWidth = 0f;
            }
        }

        private static void LayoutFooterButtons()
        {
            const float gap = 12f;
            const float reviveX = 456f;
            const float blacklistX = 690f;
            const float reviveMaxWidth = blacklistX - gap - reviveX;
            if (!NetworkServer.activeHost || GuiTab != 1)
            {
                // Reset to the original footer slots on every non-player tab, including the room tab.
                PlaceFooterButton(EliminateAllFooterButton, 32f, 200f);
                PlaceFooterButton(KickAllFooterButton, 244f, 200f);
                PlaceFooterButton(RespawnAllFooterButton, reviveX, 200f);
                PlaceFooterButton(BlacklistFooterButton, blacklistX, 188f);
                PlaceFooterButton(CloseFooterButton, 888f, 260f);
                return;
            }
            // Keep the established slots for the existing player actions; only the revive action sizes to its label.
            if (RespawnAllFooterButton != null && RespawnAllFooterButton.gameObject.activeSelf)
                PlaceFooterButton(RespawnAllFooterButton, reviveX, MeasureFooterButtonWidth(RespawnAllFooterButton, 150f, reviveMaxWidth));
        }

        private static UIButton NativeButton(Transform parent, string name, string label, ButtonStyle style, float x, float y, float width, float height, Action action)
        {
            var button = UIF.Button(parent, name, label, NativeAction(action), style, width, height,
                new Il2CppSystem.Nullable<GlyphId>(), -1f, -1f);
            var originalHeight = height;
            SizeButtonToText(button, ref width, ref height, width);
            UIF.At(button.GetComponent<RectTransform>(), UIF.TopLeft, new Vector2(x, -(y - (height - originalHeight))), new Vector2(width, height));
            return button;
        }

        private static UIButton NativeButton(Transform parent, string name, string label, ButtonStyle style, float width, float height, Action action)
        {
            var button = UIF.Button(parent, name, label, NativeAction(action), style, width, height,
                new Il2CppSystem.Nullable<GlyphId>(), -1f, -1f);
            SizeButtonToText(button, ref width, ref height, NativeContentWidth * 0.36f);
            button.GetComponent<RectTransform>().sizeDelta = new Vector2(width, height);
            UIF.LE(button.gameObject, width, height, 0f, 0f, width, height);
            return button;
        }

        private static Il2CppSystem.Action NativeAction(Action action)
        {
            return DelegateSupport.ConvertDelegate<Il2CppSystem.Action>(action);
        }

        private static Il2CppSystem.Action<int> NativeIntAction(Action<int> action)
        {
            return DelegateSupport.ConvertDelegate<Il2CppSystem.Action<int>>(action);
        }

        private static Il2CppSystem.Action<bool> NativeBoolAction(Action<bool> action)
        {
            return DelegateSupport.ConvertDelegate<Il2CppSystem.Action<bool>>(action);
        }

        private static Il2CppSystem.Action<string> NativeStringAction(Action<string> action)
        {
            return DelegateSupport.ConvertDelegate<Il2CppSystem.Action<string>>(action);
        }

        private static bool CanHandleNativeBack()
        {
            if (!GuiVisible) return false;
            var root = UIRoot.Instance;
            var top = root != null ? root.TopScreen : null;
            return top == null || UnderlyingTopScreen == null || top == UnderlyingTopScreen;
        }

        private static bool TryEndFocusedRoomInput()
        {
            if (!GuiVisible || GuiTab != 2) return false;
            var nameInput = RoomNameInputField != null && RoomNameInputField.isFocused;
            var capacityInput = CapacityInputField != null && CapacityInputField.isFocused;
            if (!nameInput && !capacityInput) return false;

            if (nameInput) GuiRoomNameInput = RoomNameInputField.text ?? string.Empty;
            if (capacityInput)
                PendingCapacityText = new string((CapacityInputField.text ?? string.Empty).Where(char.IsDigit).Take(3).ToArray());

            if (nameInput) RoomNameInputField.DeactivateInputField();
            if (capacityInput) CapacityInputField.DeactivateInputField();
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            UpdateRoomSaveButtons();
            return true;
        }

        internal static bool HandleNativeBack()
        {
            if (Time.frameCount <= SuppressBackThroughFrame) return true;
            if (!CanHandleNativeBack()) return false;
            if (BlacklistWindow != null)
            {
                CloseBlacklistWindow();
                SuppressBackThroughFrame = Time.frameCount + 1;
                return true;
            }
            if (TryEndFocusedRoomInput())
            {
                SuppressBackThroughFrame = Time.frameCount + 1;
                return true;
            }
            SuppressBackThroughFrame = Time.frameCount + 1;
            SetGuiVisible(false);
            return true;
        }

        private static void FocusCurrentTab()
        {
            if (GuiNavButtons != null && GuiTab >= 0 && GuiTab < GuiNavButtons.Length && GuiNavButtons[GuiTab] != null)
                UIScreen.Select(GuiNavButtons[GuiTab].gameObject);
        }
    }

    [HarmonyPatch]
    internal static class HostControlMouseAxisPatch
    {
        private static System.Reflection.MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(UnityEngine.Input), "GetAxis", new[] { typeof(string) });
        }

        [HarmonyPrefix]
        private static bool Prefix(string axisName, ref float __result)
        {
            if (!Plugin.ShouldSuppressGameplayMouseAxis(axisName)) return true;
            __result = 0f;
            return false;
        }
    }

    [HarmonyPatch]
    internal static class HostControlMouseAxisRawPatch
    {
        private static System.Reflection.MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(UnityEngine.Input), "GetAxisRaw", new[] { typeof(string) });
        }

        [HarmonyPrefix]
        private static bool Prefix(string axisName, ref float __result)
        {
            if (!Plugin.ShouldSuppressGameplayMouseAxis(axisName)) return true;
            __result = 0f;
            return false;
        }
    }

    [HarmonyPatch(typeof(Il2CppMayday.GameInput), "get_Blocked")]
    internal static class HostControlGameInputBlockPatch
    {
        [HarmonyPostfix]
        private static void Postfix(ref bool __result)
        {
            if (Plugin.ShouldBlockGameInput) __result = true;
        }
    }

    internal static class CrewListScrollFix
    {
        private const float ViewportHeight = 620f;
        private const string ViewportName = "HostControlCrewViewport";
        private static readonly FieldInfo CrewListField = AccessTools.Field(typeof(Il2CppMayday.UI.PauseScreen), "crewList");

        internal static void Apply(Il2CppMayday.UI.PauseScreen screen)
        {
            if (screen == null || CrewListField == null) return;
            try
            {
                var list = CrewListField.GetValue(screen) as RectTransform;
                if (list == null || list.parent == null) return;
                if (list.parent.name == ViewportName) return;

                var parent = list.parent;
                var viewport = UIF.Rect(ViewportName, parent);
                viewport.anchorMin = list.anchorMin;
                viewport.anchorMax = list.anchorMax;
                viewport.pivot = list.pivot;
                viewport.anchoredPosition = list.anchoredPosition;
                viewport.sizeDelta = list.sizeDelta;
                viewport.localScale = Vector3.one;
                viewport.localRotation = Quaternion.identity;
                viewport.SetSiblingIndex(list.GetSiblingIndex());

                var viewportElement = viewport.gameObject.AddComponent<LayoutElement>();
                viewportElement.preferredWidth = list.rect.width;
                viewportElement.preferredHeight = ViewportHeight;
                viewportElement.flexibleWidth = 1f;
                viewportElement.flexibleHeight = 0f;
                viewport.gameObject.AddComponent<RectMask2D>();

                list.SetParent(viewport, false);
                list.anchorMin = new Vector2(0f, 1f);
                list.anchorMax = new Vector2(1f, 1f);
                list.pivot = new Vector2(0.5f, 1f);
                list.anchoredPosition = Vector2.zero;
                list.sizeDelta = new Vector2(0f, ViewportHeight);
                ConfigureContent(list);

                var scroll = viewport.gameObject.AddComponent<ScrollRect>();
                scroll.content = list;
                scroll.viewport = viewport;
                scroll.horizontal = false;
                scroll.vertical = true;
                scroll.inertia = true;
                scroll.scrollSensitivity = 36f;
                scroll.movementType = ScrollRect.MovementType.Clamped;
                scroll.verticalNormalizedPosition = 1f;
                Plugin.Log?.LogInfo("[MaydayHostControl] PauseScreen crew list scroll viewport installed");
            }
            catch (Exception e)
            {
                Plugin.Log?.LogWarning("[MaydayHostControl] crew list scroll fix failed: " + e);
            }
        }

        private static void ConfigureContent(RectTransform list)
        {
            if (list == null) return;
            var fitter = list.GetComponent<ContentSizeFitter>();
            if (fitter == null) fitter = list.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var layout = list.GetComponent<VerticalLayoutGroup>();
            if (layout != null)
            {
                layout.childControlHeight = true;
                layout.childForceExpandHeight = false;
            }
            LayoutRebuilder.MarkLayoutForRebuild(list);
        }
    }

    [HarmonyPatch(typeof(Il2CppMayday.UI.PauseScreen), "Build")]
    internal static class PauseCrewScrollBuildPatch
    {
        [HarmonyPostfix]
        private static void Postfix(Il2CppMayday.UI.PauseScreen __instance) => CrewListScrollFix.Apply(__instance);
    }

    [HarmonyPatch(typeof(Il2CppMayday.UI.PauseScreen), "Update")]
    internal static class PauseCrewScrollUpdatePatch
    {
        [HarmonyPostfix]
        private static void Postfix(Il2CppMayday.UI.PauseScreen __instance) => CrewListScrollFix.Apply(__instance);
    }

    [HarmonyPatch(typeof(MainMenuScreen), "Build")]
    internal static class NativeMainMenuHostEntryPatch
    {
        [HarmonyPostfix]
        private static void Postfix(MainMenuScreen __instance) => Plugin.InstallMainMenuHostButton(__instance);
    }

    [HarmonyPatch(typeof(UIRoot), "HandleBack")]
    internal static class NativeHostControlBackPatch
    {
        [HarmonyPrefix]
        private static bool Prefix()
        {
            return !Plugin.HandleNativeBack();
        }
    }

    [HarmonyPatch(typeof(Il2CppMayday.UI.PauseScreen), "Build")]
    internal static class NativePauseMenuEntryPatch
    {
        [HarmonyPostfix]
        private static void Postfix(Il2CppMayday.UI.PauseScreen __instance)
        {
            Plugin.InstallPauseMenuButton(__instance);
        }
    }
}
