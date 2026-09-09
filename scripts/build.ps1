$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$proj = Join-Path $root 'src\Jellyfin.Plugin.SimpleMovieFeed\Jellyfin.Plugin.SimpleMovieFeed.csproj'
dotnet restore $proj
dotnet build $proj -c Release --no-restore
Write-Host "Build complete. DLL:" (Join-Path (Split-Path $proj) 'bin\Release\net9.0\Jellyfin.Plugin.SimpleMovieFeed.dll')
