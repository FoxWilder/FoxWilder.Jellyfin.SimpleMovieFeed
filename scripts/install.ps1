$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$projDir = Join-Path $root 'src\Jellyfin.Plugin.SimpleMovieFeed'
$pluginDir = 'C:\ProgramData\Jellyfin\Server\plugins\SimpleMovieFeed_1.0.0.0'
New-Item -ItemType Directory -Force -Path $pluginDir | Out-Null
Copy-Item (Join-Path $projDir 'bin\Release\net9.0\Jellyfin.Plugin.SimpleMovieFeed.dll') $pluginDir -Force
Write-Host "Installed to $pluginDir"
Write-Host "Restart Jellyfin after installation."
