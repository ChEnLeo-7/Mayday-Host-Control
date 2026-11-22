[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Game
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
& (Join-Path $root 'build.ps1') -Game $Game
if ($LASTEXITCODE -ne 0) { throw 'Release build failed' }

$plugin = Get-Content -LiteralPath (Join-Path $root 'src\Plugin.cs') -Raw
$gui = Get-Content -LiteralPath (Join-Path $root 'src\InGameGui.cs') -Raw
$compat = Get-Content -LiteralPath (Join-Path $root 'src\MelonCompat.cs') -Raw
$manifest = Get-Content -LiteralPath (Join-Path $root 'manifest.json') -Raw | ConvertFrom-Json

foreach ($token in @('[assembly: MelonInfo', 'public override void OnInitializeMelon()', 'loader=MelonLoader gui=GameUI', 'Il2CppMirror.NetworkServer', 'Il2CppMayday.GameState', 'MigrateLegacyData', 'ReadLegacyMelonValue', '.legacy-migration-v2', 'var melonRoot = Path.Combine(gameRoot, "MelonLoader")', 'IsConfiguredConnectionLimitReached', 'IsConfiguredAuthenticatorFull', 'connection-capacity-decision', 'auth-capacity-decision', 'server-transport-error', 'MaydayNetworkManager), "OnServerError"')) {
    if (-not $plugin.Contains($token)) { throw "Missing Melon port token: $token" }
}
$players = Get-Content -LiteralPath (Join-Path $root 'src\PlayerCards.cs') -Raw
if (-not $players.Contains('MakeRoundedAvatarTexture') -or -not $players.Contains('pixel.a = (byte)Mathf.RoundToInt(pixel.a * coverage)') -or -not $players.Contains('binding.Image.texture = binding.RoundedTexture') -or -not $players.Contains('Graphics.Blit(source, renderTexture)') -or -not $players.Contains('readback.ReadPixels')) { throw 'Steam avatar pixels must be GPU-read back and rounded before display' }
foreach ($token in @('NativePauseMenuEntryPatch', 'NativeHostControlBackPatch', 'CrewListScrollFix', 'PauseCrewScrollBuildPatch', 'PauseCrewScrollUpdatePatch', 'RectMask2D', 'ScrollRect', 'HandleNativeBack', 'HostControlButton', 'GlyphId.Key', 'NativeMainMenuHostEntryPatch', 'CanConfigureNextRoom', 'PanelOpenedForDefaults', 'PauseMenuButton.Label.Align = TextAlignmentOptions.Left', 'UIRoot.Instance', 'UIF.Panel', 'UIF.Text', 'UIF.Button', 'new Il2CppSystem.Nullable<GlyphId>()', 'UIF.Input', 'UIF.Scroll', 'UIF.Toggle', 'SettingsScreen.Section', 'SettingsScreen.Row', 'RoomVisibility', 'CapacityInput', 'SavePendingRoomSettings', 'SaveRoomSettingsAndClose', 'enableWordWrapping = false', 'UpdateRoomSaveButtons', 'HasPendingRoomChanges', 'RefreshFocusedRoomInputs', 'HandleBlankPanelClick', 'DeactivateInputField', 'PreviousLookCaptured', 'LookCaptured', 'GuiCanvasGroup', 'UITween.Fade', 'Ease.OutQuad', 'GuiClosing', 'ShortcutCapture', 'BeginShortcutCapture', 'IsBindableVirtualKey', 'GetPanelVirtualKey', 'GetAxisRaw', 'HostControlGameInputBlockPatch', 'ShouldBlockGameInput', 'OpenPanelKey', 'NetworkServer.activeHost', 'UITheme.Paper', 'ButtonStyle.Tab', 'BuildPlayers', 'SteamProfile', 'OpenBlacklistWindow', 'Close panel', 'OpenSteamProfile', 'AddSteamFriend', 'ActivateGameOverlayToUser', 'BuildRoom', 'BuildSaves', 'BuildSettings', 'GetAsyncKeyState', 'GuiConfirmDeleteId', 'GuiSavePage')) {
    if (-not $gui.Contains($token)) { throw "Missing game-native UI token: $token" }
}
if ($gui.Contains('public override void OnGUI()')) { throw 'Incompatible IMGUI callback remains in game-native build' }
if ($gui.Contains('Input.mousePosition')) { throw 'Legacy Unity Input mouse position is incompatible with active Input System' }
if (-not $gui.Contains('NativeInput.TryGetCursorPosition(out var cursorPosition)')) { throw 'Native cursor position compatibility path is missing' }
if ($gui.Contains('new GameObject("MaydayHostControl.Canvas")')) { throw 'Legacy standalone Canvas remains in game-native build' }
foreach ($token in @('MelonPreferences.CreateCategory', 'MelonEnvironment.UserDataDirectory', 'MelonLogger.Msg', 'ExistedAtBind')) {
    if (-not $compat.Contains($token)) { throw "Missing compatibility token: $token" }
}
if ($plugin.Contains('StartWebAdmin();')) { throw 'Manager port must not start the legacy HTTP service' }
if ($plugin.Contains('[BepInPlugin')) { throw 'BepInEx plugin metadata remains in Manager port' }
if ($manifest.schemaVersion -ne 1 -or $manifest.id -ne 'mayday-host-control' -or $manifest.version -notmatch '^\d+\.\d+\.\d+$') { throw 'Invalid Manager manifest' }
$version = $manifest.version
if (-not $plugin.Contains(('"更好的房主管理", "{0}", "Mayday Host Control"' -f $version)) -or -not $plugin.Contains('public const string Name = "更好的房主管理";') -or -not $plugin.Contains(('public const string Version = "{0}";' -f $version))) { throw 'Assembly/plugin version does not match Manager manifest' }
if ($gui.Contains('case 3: BuildSaves();') -or $gui.Contains('"Saves"')) { throw 'Save tab must remain hidden from navigation' }
if (-not $gui.Contains('GetMainMenuHostButtonWidth') -or -not $gui.Contains('TextOverflowModes.Overflow')) { throw 'Main menu host button must fit its localized label without truncation' }
if (-not $plugin.Contains('Application.systemLanguage') -or -not $plugin.Contains('Loc.FromSystemLanguage')) { throw 'Initial panel language must follow the game/system language' }
if (-not $gui.Contains('TryEndFocusedRoomInput()')) { throw 'Focused room input Escape handling is missing' }

foreach ($token in @('BuildDiagnostics', 'ExportDiagnostics', 'RuntimeEvents', 'EventScroll')) {
    if ($gui.Contains($token)) { throw "Removed diagnostic UI remains: $token" }
}
$baseLocale = Get-Content -LiteralPath (Join-Path $root 'src\Locales\en.json') -Raw | ConvertFrom-Json -AsHashtable
$codes = @('zh-CN','en','fr','de','es','pt-BR','tr','ru','ja','ko','pl')
foreach ($code in $codes) {
    $table = Get-Content -LiteralPath (Join-Path $root "src\Locales\$code.json") -Raw | ConvertFrom-Json -AsHashtable
    foreach ($key in $baseLocale.Keys) {
        if (-not $table.ContainsKey($key) -or [string]::IsNullOrWhiteSpace($table[$key])) { throw "Missing translation: $code / $key" }
        $expected = ([regex]::Matches($key, '\{\d+\}') | ForEach-Object Value | Sort-Object) -join ','
        $actual = ([regex]::Matches($table[$key], '\{\d+\}') | ForEach-Object Value | Sort-Object) -join ','
        if ($expected -ne $actual) { throw "Translation placeholders differ: $code / $key" }
    }
}
foreach ($match in [regex]::Matches($gui, 'Loc\.T\("([^"\r\n]+)"')) {
    if (-not $baseLocale.ContainsKey($match.Groups[1].Value)) { throw "Missing GUI translation key: $($match.Groups[1].Value)" }
}
Write-Host 'Localization verified: 11 languages; key coverage and placeholders match.'

$dll = Join-Path $root 'src\bin\Release\net6.0\Mayday.HostControl.dll'
$hash = Get-FileHash -LiteralPath $dll -Algorithm SHA256
Write-Host "Verification passed: $($hash.Hash)" -ForegroundColor Green
