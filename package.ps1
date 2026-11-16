[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Game
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
& (Join-Path $root 'verify.ps1') -Game $Game
if ($LASTEXITCODE -ne 0) { throw 'Verification failed' }

$dist = Join-Path $root 'dist'
$stage = Join-Path $dist 'Mayday-Host-Control-0.5.27'
$tempZip = Join-Path $dist 'Mayday-Host-Control-0.5.27.zip'
$package = Join-Path $dist 'Mayday-Host-Control-0.5.27.maydaymod'
$resolvedRoot = [System.IO.Path]::GetFullPath($root) + [System.IO.Path]::DirectorySeparatorChar
foreach ($path in @($stage, $tempZip, $package)) {
    if (-not [System.IO.Path]::GetFullPath($path).StartsWith($resolvedRoot, [System.StringComparison]::OrdinalIgnoreCase)) { throw "Unsafe package path: $path" }
}
Remove-Item -LiteralPath $stage -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath $tempZip -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath $package -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path (Join-Path $stage 'Mods') | Out-Null
Copy-Item -LiteralPath (Join-Path $root 'manifest.json') -Destination (Join-Path $stage 'manifest.json')
Copy-Item -LiteralPath (Join-Path $root 'src\bin\Release\net6.0\Mayday.HostControl.dll') -Destination (Join-Path $stage 'Mods\Mayday.HostControl.dll')
Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $tempZip -CompressionLevel Optimal
Move-Item -LiteralPath $tempZip -Destination $package
$hash = Get-FileHash -LiteralPath $package -Algorithm SHA256
Write-Host "Package: $package" -ForegroundColor Green
Write-Host "SHA256: $($hash.Hash)" -ForegroundColor Green
