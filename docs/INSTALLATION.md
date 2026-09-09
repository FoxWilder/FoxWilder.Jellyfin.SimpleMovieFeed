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

## Validate the Windows service after installation

After starting Jellyfin, verify both the Windows service and the actual Jellyfin server process before treating the deployment as healthy.

On installations where JellyfinServer is managed by NSSM, the Windows service PID can belong to the NSSM wrapper rather than jellyfin.exe. Do not use the service PID alone to decide whether Jellyfin owns a listening HTTP endpoint.

1. Confirm the JellyfinServer Windows service is running.
2. Locate the actual jellyfin.exe server process.
3. Inspect listening TCP endpoints owned by that jellyfin.exe process.
4. Use the discovered Jellyfin HTTP endpoint for the server health/info request instead of assuming a fixed port solely from the service wrapper.
5. Confirm the Jellyfin log reports SimpleMovieFeed loading without new plugin errors.

A deployment should not be considered failed merely because the NSSM wrapper PID has no listening TCP endpoint. Listener validation must be performed against the real Jellyfin process.

## Configure

After installation, open the SimpleMovieFeed plugin settings page in the Jellyfin dashboard.

The RSS feed, movie API and qBittorrent API endpoints use supported fixed defaults and are read-only. Configure the cache and library directories, startup buffer, cleanup grace period and qBittorrent timeout as required.

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
