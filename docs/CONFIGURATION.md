# Configuration
> [!NOTE]
> The RSS feed URL, Movie API URL, and qBittorrent API URL are fixed supported endpoints. They are displayed read-only in the Jellyfin settings page and cannot be overridden by saved configuration.

Fixed endpoints:

- RSS feed: https://atlas.rssly.org/feed/0/all/all/0/en
- Movie API: https://movies-api.accel.li/api/v2
- qBittorrent API: http://127.0.0.1:8080/api/v2/


SimpleMovieFeed runtime settings are configured from the plugin settings page in the Jellyfin dashboard.

Normal settings are stored using Jellyfin's plugin configuration system. The qBittorrent credential is deliberately stored separately and is never returned by the plugin configuration API.

## Settings

| Setting | Default | Validation | Restart required |
| --- | --- | --- | --- |
| RSS feed URL | `https://atlas.rssly.org/feed/0/all/all/0/en` | Fixed / read-only | No |
| Movie API URL | `https://movies-api.accel.li/api/v2` | Fixed / read-only | No |
| Cache directory | `C:\JellyfinMovieCache` | Absolute path | Yes |
| Movie library directory | `C:\JellyfinMovieFeedLibrary` | Absolute path | Yes |
| qBittorrent API URL | `http://127.0.0.1:8080/api/v2/` | Fixed / read-only | No |
| Startup buffer | `256 MiB` | 1-4096 MiB | No |
| Cleanup grace period | `30 seconds` | 0-3600 seconds | No |
| qBittorrent timeout | `30 seconds` | 5-300 seconds | Yes |

The server validates these values before saving them. Browser-side validation is only an additional convenience.

## qBittorrent credential

The settings page provides a write-only qBittorrent API key/password field. Leaving it blank keeps the currently protected credential.

On Windows, the credential is protected using Windows DPAPI machine scope and stored outside normal Jellyfin plugin configuration at:

```text
C:\ProgramData\Jellyfin\Server\secrets\qbt-api-key.bin
```

The secret is not returned by the status/configuration API and must never be committed to Git.

Protected credential storage is currently Windows-only.

## Storage

The cache directory contains disposable runtime/download state.

The movie library directory contains lightweight Jellyfin identities such as `.strm`, poster and NFO metadata. These files allow Jellyfin to preserve native details and resume behaviour after disposable movie payloads are removed.

Changing either directory requires a Jellyfin restart because services and playback-state storage capture these paths during plugin initialization.

## Startup buffer

Playback waits for the configured amount of downloaded movie data before starting. The default is **256 MiB**.

The Jellyfin Web integration obtains the current startup-buffer setting from the plugin runtime API rather than using a separate hardcoded threshold.

## Cleanup

SimpleMovieFeed is designed to avoid retaining the full movie after playback.

Temporary torrent/download data is removed after playback stops, subject to the configured cleanup grace period. Lightweight Jellyfin identity and resume metadata are preserved.

## Multiple Jellyfin users

Resume state is associated with the Jellyfin user so separate users can maintain independent playback positions.

## Security

Configuration changes and credential updates require Jellyfin administrator elevation.

Do not place Jellyfin access tokens, qBittorrent credentials, passwords or other secrets in files tracked by Git.
