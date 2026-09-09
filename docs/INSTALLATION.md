# Installation

## Supported environment

The initial SimpleMovieFeed release targets a Windows Jellyfin Server installation and Jellyfin Web.

It was developed against:

- Jellyfin Server 10.11.11
- .NET 9
- qBittorrent 5.2.x

## Install from a release

1. Download the latest ZIP from the repository's **Releases** page.
2. Stop the Jellyfin Server service.
3. Create a SimpleMovieFeed plugin directory beneath Jellyfin's plugin directory.
4. Extract the release package into that directory.
5. Start Jellyfin Server.
6. Open Jellyfin Web and perform a hard refresh if necessary.

A typical Windows plugin location is:

```text
C:\ProgramData\Jellyfin\Server\plugins\SimpleMovieFeed_<version>\
```

## Build from source

```powershell
git clone https://github.com/FoxWilder/FoxWilder.Jellyfin.SimpleMovieFeed.git
cd FoxWilder.Jellyfin.SimpleMovieFeed
.\scripts\build.ps1
```

The Release DLL is produced beneath:

```text
src\Jellyfin.Plugin.SimpleMovieFeed\bin\Release\net9.0\
```

## Jellyfin Web integration

The current implementation includes Jellyfin Web integration. After installing or updating the plugin, restart Jellyfin and hard-refresh the browser so the latest embedded web resources are loaded.

## Updating

Before updating:

1. Stop Jellyfin.
2. Back up the currently installed plugin directory.
3. Replace the plugin files with the new release.
4. Restart Jellyfin.
5. Hard-refresh Jellyfin Web.

Read the CHANGELOG before upgrading between releases.
