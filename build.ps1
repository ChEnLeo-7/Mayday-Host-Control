[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Game,
    [switch]$Deploy
)

$ErrorActionPreference = 'Stop'
$gameDir = (Resolve-Path -LiteralPath $Game).Path
$interop = Join-Path $gameDir 'MelonLoader\Il2CppAssemblies'
if (-not (Test-Path -LiteralPath (Join-Path $interop 'Assembly-CSharp.dll'))) {
    throw '缺少 MelonLoader/Il2CppAssemblies；请先通过 Steam 启动一次游戏完成生成'
}
$project = Join-Path $PSScriptRoot 'src\MaydayHostControl.csproj'
& dotnet build $project -c Release "-p:GameDir=$gameDir" --nologo -v minimal
if ($LASTEXITCODE -ne 0) { throw "编译失败（退出码 $LASTEXITCODE）" }
$dll = Join-Path $PSScriptRoot 'src\bin\Release\net6.0\Mayday.HostControl.dll'
if (-not (Test-Path -LiteralPath $dll)) { throw "找不到产物：$dll" }
Write-Host "编译成功：$dll" -ForegroundColor Green

if ($Deploy) {
    $dest = Join-Path $gameDir 'Mods\MaydayPkg.mayday-host-control\Mods'
    New-Item -ItemType Directory -Force -Path $dest | Out-Null
    Copy-Item -LiteralPath $dll -Destination (Join-Path $dest 'Mayday.HostControl.dll') -Force
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'manifest.json') -Destination (Join-Path (Split-Path $dest -Parent) 'manifest.json') -Force
    Write-Host "已部署：$dest" -ForegroundColor Green
}
