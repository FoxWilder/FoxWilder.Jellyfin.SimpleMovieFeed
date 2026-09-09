# Installation

## Supported environment

SimpleMovieFeed currently targets a Windows Jellyfin Server installation and Jellyfin Web.

It is developed and tested against:

- Jellyfin Server 10.11.11
- .NET 9 plugin runtime
- qBittorrent 5.2.x

## Install from a release

Official installable artifacts are published on the repository's **GitHub Releases** page.

1. Download the ZIP for the desired SimpleMovieFeed release.
2. Verify its published SHA-256 checksum when one is provided.
3. Stop the Jellyfin Server service.
4. Create a SimpleMovieFeed version directory beneath Jellyfin's plugin directory.
5. Extract the release package into that directory.
6. Start Jellyfin Server.
7. Open Jellyfin Web and hard-refresh the browser if necessary.

A typical Windows plugin location is:

```text
C:\ProgramData\Jellyfin\Server\plugins\SimpleMovieFeed_<version>\
```

Do not treat a DLL from a local source-tree build as an official release artifact. Official artifacts are built and published through the repository's GitHub workflows.

## Configure

After installation, open the SimpleMovieFeed plugin settings page in the Jellyfin dashboard.

Configure the movie feed/API endpoints, cache and library directories, qBittorrent API endpoint, startup buffer, cleanup grace period and qBittorrent timeout as required.

Enter the qBittorrent API key/password in its protected write-only field. Leaving the field blank preserves an existing credential.

See **[Configuration](CONFIGURATION.md)** for defaults, ranges and restart requirements.

## Jellyfin Web integration

The plugin includes Jellyfin Web integration. After installing or updating the plugin, restart Jellyfin and hard-refresh the browser so the latest embedded web resources are loaded.

## Updating

Before updating:

1. Stop Jellyfin.
2. Back up the currently installed plugin directory.
3. Replace the plugin files with files from the new GitHub Release.
4. Restart Jellyfin.
5. Hard-refresh Jellyfin Web.

Read the CHANGELOG before upgrading between releases.
