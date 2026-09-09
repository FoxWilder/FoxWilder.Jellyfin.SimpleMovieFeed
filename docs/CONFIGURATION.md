# Configuration

## qBittorrent

SimpleMovieFeed expects qBittorrent's Web API to be available to the Jellyfin server.

The current implementation uses the local qBittorrent API endpoint:

```text
http://127.0.0.1:8080/api/v2/
```

Authentication material must not be stored in the Git repository.

The current Windows implementation reads an encrypted qBittorrent API key from:

```text
C:\ProgramData\Jellyfin\Server\secrets\qbt-api-key.bin
```

That file is runtime configuration and must never be committed.

## Storage

The current implementation uses:

```text
C:\JellyfinMovieCache
C:\JellyfinMovieFeedLibrary
```

JellyfinMovieCache contains disposable runtime/download state.

JellyfinMovieFeedLibrary contains lightweight Jellyfin movie identities such as .strm, poster and NFO metadata.

## Startup buffer

Playback waits for approximately **256 MiB** of downloaded movie data before starting.

## Cleanup

SimpleMovieFeed is designed to avoid retaining the full movie after playback.

Temporary torrent/download data is removed after playback stops, subject to the plugin's cleanup grace period. Lightweight Jellyfin identity and resume metadata are preserved.

## Multiple Jellyfin users

Resume state is associated with the Jellyfin user so separate users can maintain independent playback positions.

## Security

Do not place Jellyfin access tokens, qBittorrent API keys, passwords or other credentials in configuration files tracked by Git.
